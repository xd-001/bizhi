using WallpaperChanger.Services;
using Timer = System.Timers.Timer;

namespace WallpaperChanger;

public class MainContext : ApplicationContext
{
    private NotifyIcon trayIcon;
    private AppSettings settings;
    private WallpaperManager manager;
    private Timer? timer;
    private System.Threading.Timer? gameCheckTimer;
    private bool isGameMode;
    private LikeDislikeForm? likeDislikeForm;

    public MainContext()
    {
        settings = AppSettings.Load();
        manager = new WallpaperManager();

        trayIcon = new NotifyIcon()
        {
            Icon = SystemIcons.Application,
            Text = "壁纸切换器",
            Visible = true
        };

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("立即切换", null, (s, e) => ChangeWallpaper());
        contextMenu.Items.Add("我喜欢", null, (s, e) => MarkAsLike());
        contextMenu.Items.Add("我不喜欢", null, (s, e) => MarkAsDislike());
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("设置", null, (s, e) => OpenSettings());
        contextMenu.Items.Add("退出", null, (s, e) => ExitApplication());
        trayIcon.ContextMenuStrip = contextMenu;
        trayIcon.DoubleClick += (s, e) => OpenSettings();

        ShowLikeDislikeButtons();
        InitializeWallpaper();
    }

    private void ShowLikeDislikeButtons()
    {
        likeDislikeForm = new LikeDislikeForm();
        likeDislikeForm.LikeClicked += MarkAsLike;
        likeDislikeForm.DislikeClicked += MarkAsDislike;
        likeDislikeForm.Show();
    }

    private string GetActiveFolder()
    {
        if (settings.GuestMode && !string.IsNullOrWhiteSpace(settings.GuestFolder) && Directory.Exists(settings.GuestFolder))
            return settings.GuestFolder;
        return settings.WallpaperFolder;
    }

    private void InitializeWallpaper()
    {
        string activeFolder = GetActiveFolder();
        if (Directory.Exists(activeFolder))
        {
            manager.LoadFolder(activeFolder);
            ChangeWallpaper(useTransition: false);
        }
        StartTimers();
    }

    private void StartTimers()
    {
        timer?.Stop();
        timer = new Timer(settings.IntervalSeconds * 1000);
        timer.Elapsed += (s, e) => ChangeWallpaperIfAllowed();
        timer.AutoReset = true;
        timer.Start();

        gameCheckTimer?.Dispose();
        gameCheckTimer = new System.Threading.Timer(_ =>
        {
            bool wasGameMode = isGameMode;
            isGameMode = GameDetector.IsFullScreenAppRunning() ||
                         GameDetector.IsProcessRunning(settings.GameProcessNames);
            if (wasGameMode && !isGameMode)
                ChangeWallpaperIfAllowed();
        }, null, 0, 1500);
    }

    private void ChangeWallpaperIfAllowed()
    {
        if (isGameMode) return;
        ChangeWallpaper();
    }

    private void ChangeWallpaper(bool useTransition = true)
    {
        // 访客模式下不移动文件
        if (!settings.GuestMode)
            manager.MoveCurrentToDefaultIfNotMoved();

        // 获取当前显示器数量
        uint monitorCount = 1;
        try
        {
            Type? type = Type.GetTypeFromProgID("DesktopWallpaper.Wallpaper");
            if (type != null)
            {
                dynamic wallpaper = Activator.CreateInstance(type)!;
                monitorCount = wallpaper.GetMonitorDevicePathCount();
            }
        }
        catch { }

        string[] images;
        if (settings.MultiMonitorSameWallpaper)
        {
            // 所有屏幕同一张
            var img = manager.GetRandomImage();
            images = img != null ? new[] { img } : Array.Empty<string>();
        }
        else
        {
            // 每个屏幕随机不同壁纸
            images = manager.GetRandomImages((int)monitorCount);
            // 如果返回的图片数量少于显示器数量（例如图片不够），循环填充保证每个屏幕都有图
            if (images.Length > 0 && images.Length < monitorCount)
            {
                var padded = new string[monitorCount];
                for (int i = 0; i < monitorCount; i++)
                    padded[i] = images[i % images.Length];
                images = padded;
            }
        }
        if (images.Length == 0) return;

        // 壁纸样式
        var style = (WallpaperHelper.DesktopWallpaperStyle)settings.WallpaperStyle;

        if (useTransition && settings.SmoothTransition)
        {
            // 异步执行过渡
            Task.Run(() =>
            {
                var transition = new TransitionForm(images, settings.TransitionSpeed);
                transition.ShowDialog();
                // 过渡结束后，记录当前壁纸（以便喜欢/不喜欢操作）
                manager.SetCurrentWallpapers(images);
            });
        }
        else
        {
            WallpaperHelper.SetWallpapers(images, style);
            manager.SetCurrentWallpapers(images);
        }
    }

    private void MarkAsLike()
    {
        if (settings.GuestMode) return;
        string root = settings.WallpaperFolder;
        if (string.IsNullOrEmpty(root)) return;
        string likeFolder = Path.Combine(root, AppSettings.LikeFolderName);
        manager.MoveCurrentWallpapers(likeFolder);
    }

    private void MarkAsDislike()
    {
        if (settings.GuestMode) return;
        string root = settings.WallpaperFolder;
        if (string.IsNullOrEmpty(root)) return;
        string dislikeFolder = Path.Combine(root, AppSettings.DislikeFolderName);
        manager.MoveCurrentWallpapers(dislikeFolder);
    }

    private void OpenSettings()
    {
        var form = new SettingsForm(settings);
        if (form.ShowDialog() == DialogResult.OK)
        {
            settings = AppSettings.Load();
            string activeFolder = GetActiveFolder();
            manager.LoadFolder(activeFolder);
            ChangeWallpaper(useTransition: false);
            StartTimers();
        }
    }

    private void ExitApplication()
    {
        timer?.Stop();
        gameCheckTimer?.Dispose();
        likeDislikeForm?.Close();
        likeDislikeForm?.Dispose();
        trayIcon.Visible = false;
        Application.Exit();
    }
}

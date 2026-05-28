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
    private List<LikeDislikeForm> likeDislikeForms = new();

    public MainContext()
    {
        settings = AppSettings.Load() ?? new AppSettings();
        manager = new WallpaperManager();

        // 安全加载托盘图标
        Icon? appIcon = null;
        try
        {
            // 尝试从本地文件加载你的 app.ico
            if (File.Exists("app.ico"))
                appIcon = new Icon("app.ico");
        }
        catch { }
        appIcon ??= SystemIcons.Application; // 兜底

        trayIcon = new NotifyIcon()
        {
            Icon = appIcon,
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

        try
        {
            ShowLikeDislikeButtons();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"创建桌面按钮失败：{ex.Message}\n{ex.StackTrace}", "警告",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        InitializeWallpaper();
    }

    private void ShowLikeDislikeButtons()
    {
        if (Screen.AllScreens.Length == 0) return;

        foreach (var screen in Screen.AllScreens)
        {
            if (screen == null) continue;
            var form = new LikeDislikeForm();
            form.LikeClicked += MarkAsLike;
            form.NextClicked += () => ChangeWallpaper();
            form.DislikeClicked += MarkAsDislike;

            var area = screen.WorkingArea;
            int x = Math.Max(area.Left + 10, area.Right - form.Width - 10);
            int y = Math.Max(area.Top + 10, area.Bottom - form.Height - 10);
            form.Location = new Point(x, y);
            form.Show();
            likeDislikeForms.Add(form);
        }
    }

    private string GetActiveFolder()
    {
        if (settings.GuestMode && !string.IsNullOrWhiteSpace(settings.GuestFolder) && Directory.Exists(settings.GuestFolder))
            return settings.GuestFolder;
        return settings.WallpaperFolder ?? "";
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
        timer = new Timer(Math.Max(1000, settings.IntervalSeconds * 1000));
        timer.Elapsed += (s, e) => ChangeWallpaperIfAllowed();
        timer.AutoReset = true;
        timer.Start();

        gameCheckTimer?.Dispose();
        gameCheckTimer = new System.Threading.Timer(_ =>
        {
            bool wasGameMode = isGameMode;
            isGameMode = GameDetector.IsFullScreenAppRunning() ||
                         (settings.GameProcessNames != null && GameDetector.IsProcessRunning(settings.GameProcessNames));
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
        if (!settings.GuestMode)
            manager.MoveCurrentToDefaultIfNotMoved();

        uint monitorCount = 1;
        try
        {
            Type? type = Type.GetTypeFromProgID("DesktopWallpaper.Wallpaper");
            if (type != null)
            {
                dynamic wallpaper = Activator.CreateInstance(type)!;
                if (wallpaper != null)
                    monitorCount = wallpaper.GetMonitorDevicePathCount();
            }
        }
        catch { }

        string[] images;
        if (settings.MultiMonitorSameWallpaper)
        {
            var img = manager.GetRandomImage();
            images = img != null ? new[] { img } : Array.Empty<string>();
        }
        else
        {
            images = manager.GetRandomImages((int)monitorCount);
            if (images.Length > 0 && images.Length < monitorCount)
            {
                var padded = new string[monitorCount];
                for (int i = 0; i < monitorCount; i++)
                    padded[i] = images[i % images.Length];
                images = padded;
            }
        }
        if (images.Length == 0) return;

        var style = (WallpaperHelper.DesktopWallpaperStyle)settings.WallpaperStyle;

        if (useTransition && settings.SmoothTransition)
        {
            Task.Run(() =>
            {
                try
                {
                    var transition = new TransitionForm(images, settings.TransitionSpeed);
                    transition.ShowDialog();
                }
                catch { }
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
        string root = settings.WallpaperFolder ?? "";
        if (string.IsNullOrEmpty(root)) return;
        string likeFolder = Path.Combine(root, AppSettings.LikeFolderName);
        manager.MoveCurrentWallpapers(likeFolder);
    }

    private void MarkAsDislike()
    {
        if (settings.GuestMode) return;
        string root = settings.WallpaperFolder ?? "";
        if (string.IsNullOrEmpty(root)) return;
        string dislikeFolder = Path.Combine(root, AppSettings.DislikeFolderName);
        manager.MoveCurrentWallpapers(dislikeFolder);
    }

    private void OpenSettings()
    {
        try
        {
            var form = new SettingsForm(settings);
            if (form.ShowDialog() == DialogResult.OK)
            {
                settings = AppSettings.Load() ?? new AppSettings();
                string activeFolder = GetActiveFolder();
                manager.LoadFolder(activeFolder);
                ChangeWallpaper(useTransition: false);
                StartTimers();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打开设置失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExitApplication()
    {
        timer?.Stop();
        gameCheckTimer?.Dispose();
        foreach (var f in likeDislikeForms)
        {
            try { f.Close(); f.Dispose(); } catch { }
        }
        likeDislikeForms.Clear();
        trayIcon.Visible = false;
        Application.Exit();
    }
}

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

        InitializeWallpaper();
    }

    private void InitializeWallpaper()
    {
        if (Directory.Exists(settings.WallpaperFolder))
        {
            manager.LoadFolder(settings.WallpaperFolder);
            ChangeWallpaper();
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
            // 游戏检测：全屏 或 指定进程运行
            isGameMode = GameDetector.IsFullScreenAppRunning() ||
                         GameDetector.IsProcessRunning(settings.GameProcessNames);
            if (wasGameMode && !isGameMode)
            {
                ChangeWallpaperIfAllowed();
            }
        }, null, 0, 1500);
    }

    private void ChangeWallpaperIfAllowed()
    {
        if (isGameMode) return;
        ChangeWallpaper();
    }

    private void ChangeWallpaper()
    {
        // 1. 将当前壁纸移动到“默认”文件夹（如果用户没手动移动过）
        manager.MoveCurrentToDefaultIfNotMoved();

        // 2. 获取新壁纸并设置
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

        var images = manager.GetRandomImages((int)monitorCount);
        if (images.Length > 0)
        {
            try
            {
                WallpaperHelper.SetWallpapers(images);
                manager.SetCurrentWallpapers(images);
            }
            catch { }
        }
    }

    private void MarkAsLike()
    {
        string root = settings.WallpaperFolder;
        if (string.IsNullOrEmpty(root)) return;
        string likeFolder = Path.Combine(root, AppSettings.LikeFolderName);
        manager.MoveCurrentWallpapers(likeFolder);
    }

    private void MarkAsDislike()
    {
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
            manager.LoadFolder(settings.WallpaperFolder);
            ChangeWallpaper();
            StartTimers();
        }
    }

    private void ExitApplication()
    {
        timer?.Stop();
        gameCheckTimer?.Dispose();
        trayIcon.Visible = false;
        Application.Exit();
    }
}

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
        contextMenu.Items.Add("设置", null, (s, e) => OpenSettings());
        contextMenu.Items.Add("-");
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
        timer = new Timer(settings.IntervalMinutes * 60 * 1000);
        timer.Elapsed += (s, e) => ChangeWallpaperIfAllowed();
        timer.AutoReset = true;
        timer.Start();

        // 游戏检测定时器（每1.5秒）
        gameCheckTimer?.Dispose();
        gameCheckTimer = new System.Threading.Timer(_ =>
        {
            bool wasGameMode = isGameMode;
            isGameMode = GameDetector.IsFullScreenAppRunning();
            if (wasGameMode && !isGameMode)
            {
                // 刚刚退出游戏，立刻换一张壁纸
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
        var img = manager.GetRandomImage();
        if (img != null)
        {
            try
            {
                WallpaperHelper.SetWallpaper(img);
            }
            catch { }
        }
    }

    private void OpenSettings()
    {
        var form = new SettingsForm(settings);
        if (form.ShowDialog() == DialogResult.OK)
        {
            settings = AppSettings.Load(); // 重新加载
            manager.LoadFolder(settings.WallpaperFolder);
            ChangeWallpaper();
            StartTimers(); // 重启定时器
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

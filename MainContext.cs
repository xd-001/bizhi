using Microsoft.Win32;
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
    private HotKeyManager? hotKeyManager;

    private ToolStripMenuItem? startupMenuItem;
    private ToolStripMenuItem? guestModeMenuItem;
    private bool _isSettingsOpen = false;

    // 轮流切换相关
    private int _currentScreenIndex = 0;
    private string? _currentWallpaperPath = null;

    public MainContext()
    {
        settings = AppSettings.Load() ?? new AppSettings();
        manager = new WallpaperManager();

        // 托盘图标（嵌入资源）
        Icon? appIcon = null;
        try
        {
            using var stream = typeof(MainContext).Assembly.GetManifestResourceStream("WallpaperChanger.app.ico");
            if (stream != null) appIcon = new Icon(stream);
        }
        catch { }
        appIcon ??= SystemIcons.Application;

        trayIcon = new NotifyIcon()
        {
            Icon = appIcon,
            Text = "壁纸切换器",
            Visible = true
        };

        BuildContextMenu();
        trayIcon.DoubleClick += (s, e) => OpenSettings();

        try { ShowLikeDislikeButtons(); }
        catch (Exception ex) { MessageBox.Show($"创建桌面按钮失败：{ex.Message}", "警告"); }

        RegisterHotKey();
        InitializeWallpaper();

        if (settings.FirstRun)
        {
            ShowHelp();
            settings.FirstRun = false;
            settings.Save();
        }
    }

    private void BuildContextMenu()
    {
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("立即切换", null, (s, e) => ChangeWallpaper());
        contextMenu.Items.Add("我喜欢", null, (s, e) => MarkAsLike());
        contextMenu.Items.Add("我不喜欢", null, (s, e) => MarkAsDislike());
        contextMenu.Items.Add("-");

        startupMenuItem = new ToolStripMenuItem("开机启动") { CheckOnClick = true, Checked = settings.StartWithWindows };
        startupMenuItem.Click += (s, e) =>
        {
            settings.StartWithWindows = startupMenuItem.Checked;
            settings.Save();
            SetWindowsStartup(settings.StartWithWindows);
        };
        contextMenu.Items.Add(startupMenuItem);

        guestModeMenuItem = new ToolStripMenuItem("访客模式") { CheckOnClick = true, Checked = settings.GuestMode };
        guestModeMenuItem.Click += (s, e) =>
        {
            settings.GuestMode = guestModeMenuItem.Checked;
            settings.Save();
            string activeFolder = GetActiveFolder();
            manager.LoadFolder(activeFolder);
            ChangeWallpaper(useTransition: false);
            StartTimers();
        };
        contextMenu.Items.Add(guestModeMenuItem);

        contextMenu.Items.Add("-");
        contextMenu.Items.Add("图库浏览", null, (s, e) => OpenGallery());
        contextMenu.Items.Add("设置", null, (s, e) => OpenSettings());
        contextMenu.Items.Add("使用说明", null, (s, e) => ShowHelp());
        contextMenu.Items.Add("退出", null, (s, e) => ExitApplication());

        trayIcon.ContextMenuStrip = contextMenu;
    }

    private void UpdateMenuChecks()
    {
        if (startupMenuItem != null) startupMenuItem.Checked = settings.StartWithWindows;
        if (guestModeMenuItem != null) guestModeMenuItem.Checked = settings.GuestMode;
    }

    private void RegisterHotKey()
    {
        hotKeyManager?.Dispose();
        hotKeyManager = new HotKeyManager(9001, settings.GetHotKeyModifiers(), settings.HotKeyKey, () => ChangeWallpaper());
    }

    private void ShowLikeDislikeButtons()
    {
        foreach (var f in likeDislikeForms) { try { f.Close(); f.Dispose(); } catch { } }
        likeDislikeForms.Clear();

        foreach (var screen in Screen.AllScreens)
        {
            var form = new LikeDislikeForm();
            form.LikeClicked += MarkAsLike;
            form.NextClicked += () => ChangeWallpaper();
            form.DislikeClicked += MarkAsDislike;
            var area = screen.WorkingArea;
            form.Location = new Point(area.Right - form.Width - 10, area.Bottom - form.Height - 10);
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
            // 启动时不进行过渡，直接给所有屏幕设一张初始壁纸（统一）
            var firstImg = manager.GetRandomImage();
            if (firstImg != null)
            {
                WallpaperHelper.SetWallpapers(new[] { firstImg }, (WallpaperHelper.DesktopWallpaperStyle)settings.WallpaperStyle);
                _currentWallpaperPath = firstImg;
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(activeFolder))
                MessageBox.Show("尚未设置壁纸文件夹，请右键托盘图标进入“设置”选择文件夹。", "提示");
            else
                MessageBox.Show($"壁纸文件夹不存在：{activeFolder}", "错误");
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
            if (wasGameMode && !isGameMode) ChangeWallpaperIfAllowed();
        }, null, 0, 1500);
    }

    private void ChangeWallpaperIfAllowed()
    {
        if (isGameMode) return;
        ChangeWallpaper();
    }

    /// <summary>
    /// 轮流切换：只换当前轮到的那一个屏幕
    /// </summary>
    private void ChangeWallpaper(bool useTransition = true)
    {
        var screens = Screen.AllScreens;
        if (screens.Length == 0) return;

        // 访客模式下不移动文件
        if (!settings.GuestMode && !string.IsNullOrEmpty(_currentWallpaperPath))
        {
            string? root = Path.GetDirectoryName(_currentWallpaperPath);
            if (root != null)
            {
                string defaultFolder = Path.Combine(root, AppSettings.DefaultFolderName);
                manager.MoveWallpaperToFolder(_currentWallpaperPath, defaultFolder);
            }
        }

        // 获取当前屏幕
        _currentScreenIndex = _currentScreenIndex % screens.Length;
        var targetScreen = screens[_currentScreenIndex];

        // 选取一张新壁纸
        var newImg = manager.GetRandomImage();
        if (newImg == null) return;
        _currentWallpaperPath = newImg;

        var style = (WallpaperHelper.DesktopWallpaperStyle)settings.WallpaperStyle;

        // 设置该屏幕的壁纸
        SetWallpaperForScreen(targetScreen, newImg, style);

        // 如果启用过渡，仅对当前屏幕区域做淡入
        if (useTransition && settings.SmoothTransition)
        {
            Task.Run(() =>
            {
                try
                {
                    var transition = new TransitionForm(targetScreen, newImg, settings.TransitionSpeed);
                    transition.ShowDialog();
                }
                catch { }
            });
        }

        // 索引递增，准备下一次换下一个屏幕
        _currentScreenIndex = (_currentScreenIndex + 1) % screens.Length;
    }

    /// <summary>
    /// 为指定屏幕设置壁纸
    /// </summary>
    private void SetWallpaperForScreen(Screen screen, string path, WallpaperHelper.DesktopWallpaperStyle style)
    {
        string[] monitorIds = WallpaperHelper.GetMonitorIds();
        int index = Array.IndexOf(Screen.AllScreens, screen);
        if (index >= 0 && index < monitorIds.Length)
        {
            WallpaperHelper.SetWallpaperForMonitor(monitorIds[index], path, style);
        }
        else
        {
            // 无法获取ID时，直接调用总设置（可能只影响主屏，但至少不报错）
            WallpaperHelper.SetWallpapers(new[] { path }, style);
        }
    }

    private void MarkAsLike()
    {
        if (settings.GuestMode || string.IsNullOrEmpty(_currentWallpaperPath)) return;
        string? root = Path.GetDirectoryName(_currentWallpaperPath);
        if (root == null) return;
        string likeFolder = Path.Combine(root, AppSettings.LikeFolderName);
        manager.MoveWallpaperToFolder(_currentWallpaperPath, likeFolder);
    }

    private void MarkAsDislike()
    {
        if (settings.GuestMode || string.IsNullOrEmpty(_currentWallpaperPath)) return;
        string? root = Path.GetDirectoryName(_currentWallpaperPath);
        if (root == null) return;
        string dislikeFolder = Path.Combine(root, AppSettings.DislikeFolderName);
        manager.MoveWallpaperToFolder(_currentWallpaperPath, dislikeFolder);
    }

    private void OpenSettings()
    {
        if (_isSettingsOpen) return;
        _isSettingsOpen = true;
        try
        {
            var form = new SettingsForm(settings);
            if (form.ShowDialog() == DialogResult.OK)
            {
                settings = AppSettings.Load() ?? new AppSettings();
                UpdateMenuChecks();
                RegisterHotKey();
                string activeFolder = GetActiveFolder();
                manager.LoadFolder(activeFolder);
                // 重置轮换索引，重新开始
                _currentScreenIndex = 0;
                ChangeWallpaper(useTransition: false);
                StartTimers();
            }
        }
        catch (Exception ex) { MessageBox.Show($"打开设置失败：{ex.Message}", "错误"); }
        finally { _isSettingsOpen = false; }
    }

    private void OpenGallery()
    {
        try
        {
            var gallery = new GalleryForm(settings);
            gallery.Show();
        }
        catch (Exception ex) { MessageBox.Show($"打开图库失败：{ex.Message}", "错误"); }
    }

    private void ShowHelp()
    {
        string help = @"
壁纸切换器 使用说明

• 托盘图标右键菜单可快速操作。
• 桌面右下角按钮：❤️喜欢、➡️下一张、❌不喜欢（可拖拽调整大小）。
• 快捷键（默认 Ctrl+Shift+W）可在设置中自定义，立即切换壁纸。
• 设置中可调整切换间隔、壁纸样式、过渡效果。
• 访客模式：启用后使用独立文件夹，不移动原图库。
• 游戏检测：支持全屏或指定进程名暂停切换。
• 每屏幕独立暂停：开启后，仅桌面空闲屏幕更换壁纸。
• 图库浏览：右键菜单进入，点击缩略图直接设为壁纸。
• 多屏轮流切换：按 A→B→C→A 的顺序每间隔N秒更换一个屏幕的壁纸。

更多问题请查看项目主页。
        ";
        MessageBox.Show(help, "使用说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SetWindowsStartup(bool enable)
    {
        RegistryKey? rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
        if (enable) rk?.SetValue("WallpaperChanger", Application.ExecutablePath);
        else rk?.DeleteValue("WallpaperChanger", false);
    }

    private void ExitApplication()
    {
        timer?.Stop();
        gameCheckTimer?.Dispose();
        hotKeyManager?.Dispose();
        foreach (var f in likeDislikeForms) { try { f.Close(); f.Dispose(); } catch { } }
        likeDislikeForms.Clear();
        trayIcon.Visible = false;
        Application.Exit();
    }
}

using Microsoft.Win32;
using System.Runtime.InteropServices;
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

    private int _currentScreenIndex = 0;
    private string? _currentWallpaperPath = null;
    private bool _comAvailable = false;

    public MainContext()
    {
        settings = AppSettings.Load() ?? new AppSettings();
        manager = new WallpaperManager();

        _comAvailable = WallpaperHelper.GetMonitorIds().Length > 0;

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
        if (!Directory.Exists(activeFolder))
        {
            if (string.IsNullOrWhiteSpace(activeFolder))
                MessageBox.Show("尚未设置壁纸文件夹，请右键托盘图标进入“设置”选择文件夹。", "提示");
            else
                MessageBox.Show($"壁纸文件夹不存在：{activeFolder}", "错误");
            StartTimers();
            return;
        }

        manager.LoadFolder(activeFolder);
        var screens = Screen.AllScreens;
        if (screens.Length == 0) { StartTimers(); return; }

        // 为每个屏幕设置独立的初始壁纸
        if (_comAvailable && screens.Length > 1)
        {
            string[] monitorIds = WallpaperHelper.GetMonitorIds();
            for (int i = 0; i < monitorIds.Length && i < screens.Length; i++)
            {
                var img = manager.GetRandomImage();
                if (img != null)
                {
                    WallpaperHelper.SetWallpaperForMonitor(monitorIds[i], img, (WallpaperHelper.DesktopWallpaperStyle)settings.WallpaperStyle);
                    if (i == 0) _currentWallpaperPath = img;
                }
            }
        }
        else
        {
            var img = manager.GetRandomImage();
            if (img != null)
            {
                WallpaperHelper.SetWallpapers(new[] { img }, (WallpaperHelper.DesktopWallpaperStyle)settings.WallpaperStyle);
                _currentWallpaperPath = img;
            }
        }

        _currentScreenIndex = 0;
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

    private void ChangeWallpaper(bool useTransition = true)
    {
        var screens = Screen.AllScreens;
        if (screens.Length == 0) return;

        _currentScreenIndex = _currentScreenIndex % screens.Length;
        var targetScreen = screens[_currentScreenIndex];

        // 独立暂停检测
        if (settings.PerMonitorPause && IsScreenOccupied(targetScreen))
        {
            _currentScreenIndex = (_currentScreenIndex + 1) % screens.Length;
            return;
        }

        // 移动旧壁纸
        if (!settings.GuestMode && !string.IsNullOrEmpty(_currentWallpaperPath))
        {
            string? root = Path.GetDirectoryName(_currentWallpaperPath);
            if (root != null)
            {
                string defaultFolder = Path.Combine(root, AppSettings.DefaultFolderName);
                manager.MoveWallpaperToFolder(_currentWallpaperPath, defaultFolder);
            }
        }

        var newImg = manager.GetRandomImage();
        if (newImg == null) return;
        _currentWallpaperPath = newImg;

        var style = (WallpaperHelper.DesktopWallpaperStyle)settings.WallpaperStyle;

        // 获取目标监视器ID
        string? monitorId = null;
        if (_comAvailable)
        {
            string[] ids = WallpaperHelper.GetMonitorIds();
            int idx = Array.IndexOf(screens, targetScreen);
            if (idx >= 0 && idx < ids.Length)
                monitorId = ids[idx];
        }

        // 设置壁纸
        if (monitorId != null)
            WallpaperHelper.SetWallpaperForMonitor(monitorId, newImg, style);
        else
            WallpaperHelper.SetWallpapers(new[] { newImg }, style); // 回退

        // 过渡动画（非模态，不遮挡）
        if (useTransition && settings.SmoothTransition)
        {
            Bitmap? oldScreenshot = CaptureScreen(targetScreen);
            if (oldScreenshot != null)
            {
                var transition = new TransitionForm(targetScreen, oldScreenshot, newImg, settings.TransitionSpeed, style);
                transition.Show(); // 非模态显示，不阻塞，不获取焦点
                // 窗口会在定时器结束后自动关闭释放
            }
        }

        _currentScreenIndex = (_currentScreenIndex + 1) % screens.Length;
    }

    private Bitmap? CaptureScreen(Screen screen)
    {
        try
        {
            var bmp = new Bitmap(screen.Bounds.Width, screen.Bounds.Height);
            using (var g = Graphics.FromImage(bmp))
                g.CopyFromScreen(screen.Bounds.Left, screen.Bounds.Top, 0, 0, bmp.Size);
            return bmp;
        }
        catch { return null; }
    }

    private bool IsScreenOccupied(Screen screen)
    {
        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        GetWindowRect(fg, out RECT rect);
        var windowScreen = Screen.FromHandle(fg);
        if (!windowScreen.DeviceName.Equals(screen.DeviceName)) return false;
        return rect.Left <= screen.Bounds.Left && rect.Top <= screen.Bounds.Top &&
               rect.Right >= screen.Bounds.Right && rect.Bottom >= screen.Bounds.Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern int GetWindowRect(IntPtr hwnd, out RECT rect);
    private struct RECT { public int Left, Top, Right, Bottom; }

    private void MarkAsLike()
    {
        if (settings.GuestMode || string.IsNullOrEmpty(_currentWallpaperPath)) return;
        string? root = Path.GetDirectoryName(_currentWallpaperPath);
        if (root == null) return;
        string likeFolder = Path.Combine(root, AppSettings.LikeFolderName);
        manager.MoveWallpaperToFolder(_currentWallpaperPath, likeFolder);
        _currentWallpaperPath = null;
    }

    private void MarkAsDislike()
    {
        if (settings.GuestMode || string.IsNullOrEmpty(_currentWallpaperPath)) return;
        string? root = Path.GetDirectoryName(_currentWallpaperPath);
        if (root == null) return;
        string dislikeFolder = Path.Combine(root, AppSettings.DislikeFolderName);
        manager.MoveWallpaperToFolder(_currentWallpaperPath, dislikeFolder);
        _currentWallpaperPath = null;
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
                InitializeWallpaper();
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
• 快捷键（默认 Ctrl+Shift+W）可自定义，立即切换壁纸。
• 设置中可调整切换间隔、壁纸样式、过渡效果。
• 访客模式：启用后使用独立文件夹，不移动原图库。
• 游戏检测：支持全屏或指定进程名暂停切换。
• 每屏幕独立暂停：开启后，仅桌面空闲屏幕更换壁纸。
• 图库浏览：右键菜单进入，点击缩略图直接设为壁纸。
• 多屏轮流切换：按 A→B→C→A 的顺序每间隔N秒更换一个屏幕的壁纸（需Windows 10+支持独立壁纸）。
• 若多屏仍同步，请确认系统已更新并允许不同监视器设置不同壁纸（默认支持）。

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

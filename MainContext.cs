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

    // 防止重复打开设置窗口
    private bool _isSettingsOpen = false;

    public MainContext()
    {
        settings = AppSettings.Load() ?? new AppSettings();
        manager = new WallpaperManager();

        // 加载托盘图标：从嵌入资源读取 app.ico
        Icon? appIcon = null;
        try
        {
            using var stream = typeof(MainContext).Assembly.GetManifestResourceStream("WallpaperChanger.app.ico");
            if (stream != null)
                appIcon = new Icon(stream);
        }
        catch { }
        appIcon ??= SystemIcons.Application; // 兜底

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

        // 首次运行弹窗
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
        else
        {
            // 文件夹无效，提示用户设置
            if (string.IsNullOrWhiteSpace(activeFolder))
                MessageBox.Show("尚未设置壁纸文件夹，请右键托盘图标进入“设置”选择文件夹。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show($"壁纸文件夹不存在：{activeFolder}\n请进入设置重新选择。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        string[] monitorIds = WallpaperHelper.GetMonitorIds();
        int monitorCount = monitorIds.Length;
        if (monitorCount == 0) monitorCount = 1;

        List<int> activeScreens = new();
        for (int i = 0; i < monitorCount; i++)
        {
            if (settings.PerMonitorPause && IsScreenOccupied(i))
                continue;
            activeScreens.Add(i);
        }
        if (activeScreens.Count == 0)
        {
            // 所有屏幕都被占用，不切换
            return;
        }

        string[] images;
        if (settings.MultiMonitorSameWallpaper)
        {
            var img = manager.GetRandomImage();
            images = img != null ? new[] { img } : Array.Empty<string>();
        }
        else
        {
            images = manager.GetRandomImages(activeScreens.Count);
            if (images.Length > 0 && images.Length < activeScreens.Count)
            {
                var padded = new string[activeScreens.Count];
                for (int idx = 0; idx < activeScreens.Count; idx++)
                    padded[idx] = images[idx % images.Length];
                images = padded;
            }
        }

        if (images.Length == 0)
        {
            // 没有可用的图片（文件夹空或未加载）
            return;
        }

        var style = (WallpaperHelper.DesktopWallpaperStyle)settings.WallpaperStyle;

        if (useTransition && settings.SmoothTransition)
        {
            if (activeScreens.Count == monitorCount)
            {
                var allImages = Enumerable.Range(0, monitorCount)
                    .Select(i => images[activeScreens.IndexOf(i) % images.Length])
                    .Where(s => s != null).Cast<string>().ToArray();
                Task.Run(() =>
                {
                    try
                    {
                        var transition = new TransitionForm(allImages, settings.TransitionSpeed);
                        transition.ShowDialog();
                    }
                    catch { }
                    SetWallpapersForMonitors(monitorIds, images, activeScreens, style);
                    manager.SetCurrentWallpapers(images);
                });
            }
            else
            {
                SetWallpapersForMonitors(monitorIds, images, activeScreens, style);
                manager.SetCurrentWallpapers(images);
            }
        }
        else
        {
            SetWallpapersForMonitors(monitorIds, images, activeScreens, style);
            manager.SetCurrentWallpapers(images);
        }
    }

    private void SetWallpapersForMonitors(string[] monitorIds, string[] images, List<int> screenIndices, WallpaperHelper.DesktopWallpaperStyle style)
    {
        for (int i = 0; i < screenIndices.Count; i++)
        {
            int idx = screenIndices[i];
            if (idx < monitorIds.Length && i < images.Length)
                WallpaperHelper.SetWallpaperForMonitor(monitorIds[idx], images[i], style);
        }
    }

    private bool IsScreenOccupied(int screenIndex)
    {
        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        var screen = Screen.AllScreens[screenIndex];
        GetWindowRect(fg, out RECT rect);
        var windowScreen = Screen.FromHandle(fg);
        if (windowScreen.DeviceName != screen.DeviceName) return false;
        return IsFullScreen(rect, screen);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern int GetWindowRect(IntPtr hwnd, out RECT rect);
    private struct RECT { public int Left, Top, Right, Bottom; }
    private bool IsFullScreen(RECT rect, Screen screen)
    {
        var bounds = screen.Bounds;
        return rect.Left <= bounds.Left && rect.Top <= bounds.Top &&
               rect.Right >= bounds.Right && rect.Bottom >= bounds.Bottom;
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
        // 防止重复打开设置窗口
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
                ChangeWallpaper(useTransition: false);
                StartTimers();
            }
        }
        catch (Exception ex) { MessageBox.Show($"打开设置失败：{ex.Message}", "错误"); }
        finally
        {
            _isSettingsOpen = false;
        }
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

更多问题请查看项目主页。
        ";
        MessageBox.Show(help, "使用说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SetWindowsStartup(bool enable)
    {
        RegistryKey? rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
        string appName = "WallpaperChanger";
        if (enable)
            rk?.SetValue(appName, Application.ExecutablePath);
        else
            rk?.DeleteValue(appName, false);
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

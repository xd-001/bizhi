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

    public MainContext()
    {
        settings = AppSettings.Load() ?? new AppSettings();
        manager = new WallpaperManager();

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
        if (Screen.AllScreens.Length == 0) return;

        foreach (var screen in Screen.AllScreens)
        {
            if (screen == null) continue;
            try
            {
                var form = new LikeDislikeForm();
                form.LikeClicked += MarkAsLike;
                form.NextClicked += () => ChangeWallpaper();
                form.DislikeClicked += MarkAsDislike;

                var area = screen.WorkingArea;
                int x = area.Right - form.Width - 10;
                int y = area.Bottom - form.Height - 10;
                x = Math.Max(area.Left, x);
                y = Math.Max(area.Top, y);
                form.Location = new Point(x, y);
                form.Show();
                likeDislikeForms.Add(form);
            }
            catch { }
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
        // 暂时完全禁用过渡，避免黑屏/未响应
        useTransition = false;

        if (!settings.GuestMode)
            manager.MoveCurrentToDefaultIfNotMoved();

        int screenCount = Screen.AllScreens.Length;
        string[] monitorIds = WallpaperHelper.GetMonitorIds();
        int monitorCount = monitorIds.Length;
        if (monitorCount == 0) monitorCount = screenCount;

        List<int> activeScreens = new();
        for (int i = 0; i < monitorCount; i++)
        {
            if (settings.PerMonitorPause && i < screenCount && IsScreenOccupied(i))
                continue;
            activeScreens.Add(i);
        }
        if (activeScreens.Count == 0) return;

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
        if (images.Length == 0) return;

        var style = (WallpaperHelper.DesktopWallpaperStyle)settings.WallpaperStyle;

        // 异步执行壁纸设置，避免阻塞 UI
        Task.Run(() =>
        {
            try
            {
                if (monitorIds.Length == 0)
                {
                    // 无法获取ID，直接整体设置，但保证多屏不同图：images 长度 = 活跃屏幕数，可能小于总屏幕数
                    // 这里做个降级：为所有屏幕设置 images 循环填充
                    string[] fullImages = new string[screenCount];
                    for (int i = 0; i < screenCount; i++)
                    {
                        if (activeScreens.Contains(i))
                        {
                            int idx = activeScreens.IndexOf(i);
                            fullImages[i] = idx < images.Length ? images[idx] : images[0];
                        }
                        else
                        {
                            // 保持当前壁纸？由于 SetWallpapers 会覆盖所有屏幕，这里需要传入全部屏幕的图片。
                            // 但我们没有旧壁纸路径。简单处理：为未活跃屏幕也传入第一张活跃图（不够完美）。
                            fullImages[i] = images[0];
                        }
                    }
                    WallpaperHelper.SetWallpapers(fullImages, style);
                }
                else
                {
                    // 逐个设置活跃屏幕
                    for (int i = 0; i < activeScreens.Count; i++)
                    {
                        int idx = activeScreens[i];
                        if (idx < monitorIds.Length && i < images.Length)
                            WallpaperHelper.SetWallpaperForMonitor(monitorIds[idx], images[i], style);
                    }
                }
                manager.SetCurrentWallpapers(images);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"壁纸设置失败：{ex.Message}");
            }
        });
    }

    private bool IsScreenOccupied(int screenIndex)
    {
        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero) return false;
        if (screenIndex >= Screen.AllScreens.Length) return false;
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
• 设置中可调整切换间隔、壁纸样式、过渡效果（暂时禁用）。
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
        if (enable) rk?.SetValue(appName, Application.ExecutablePath);
        else rk?.DeleteValue(appName, false);
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

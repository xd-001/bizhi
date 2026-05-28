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
            ChangeWallpaper(useTransition: false);
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

    private void ChangeWallpaper(bool useTransition = true)
    {
        if (!settings.GuestMode)
            manager.MoveCurrentToDefaultIfNotMoved();

        int monitorCount = Screen.AllScreens.Length;
        string[] monitorIds = WallpaperHelper.GetMonitorIds();
        if (monitorIds.Length == 0) monitorIds = new string[monitorCount]; // 用空字符串占位

        // 准备每个屏幕的图片（确保与屏幕数一致）
        string[] images;
        if (settings.MultiMonitorSameWallpaper)
        {
            var img = manager.GetRandomImage();
            if (img == null) return;
            images = Enumerable.Repeat(img, monitorCount).ToArray();
        }
        else
        {
            images = manager.GetRandomImages(monitorCount);
            // 补足数量
            if (images.Length < monitorCount)
                images = images.Concat(Enumerable.Repeat(images.Last(), monitorCount - images.Length)).ToArray();
        }

        var style = (WallpaperHelper.DesktopWallpaperStyle)settings.WallpaperStyle;

        if (useTransition && settings.SmoothTransition)
        {
            // 先截图旧桌面（全屏范围）
            Bitmap? oldScreenshot = null;
            try
            {
                var bounds = Screen.AllScreens.Aggregate(Rectangle.Empty, (a, s) => Rectangle.Union(a, s.Bounds));
                oldScreenshot = new Bitmap(bounds.Width, bounds.Height);
                using (var g = Graphics.FromImage(oldScreenshot))
                    g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, oldScreenshot.Size);
            }
            catch { }

            // 立即设置新壁纸（桌面马上更新）
            SetWallpapersForScreens(monitorIds, images, style);
            manager.SetCurrentWallpapers(images);

            // 异步显示淡入动画（用旧截图覆盖，逐渐消失）
            Task.Run(() =>
            {
                try
                {
                    var transition = new TransitionForm(oldScreenshot, images, settings.TransitionSpeed);
                    // 非模态显示，动画结束自动关闭
                    transition.ShowDialog(); // 内部无消息循环？实际上 ShowDialog 需要，但我们在后台线程调用，会阻塞。可以使用 Show 并等待。
                    // 替代方法：使用事件等待，但这样简单阻塞也 OK，因为不是 UI 线程。
                }
                catch { }
                finally
                {
                    oldScreenshot?.Dispose();
                }
            });
        }
        else
        {
            SetWallpapersForScreens(monitorIds, images, style);
            manager.SetCurrentWallpapers(images);
        }
    }

    private void SetWallpapersForScreens(string[] monitorIds, string[] images, WallpaperHelper.DesktopWallpaperStyle style)
    {
        // 尝试使用 COM 逐个设置（支持多屏不同壁纸）
        bool success = false;
        if (monitorIds.Length > 0 && images.Length >= monitorIds.Length)
        {
            try
            {
                for (int i = 0; i < monitorIds.Length; i++)
                    WallpaperHelper.SetWallpaperForMonitor(monitorIds[i], images[i], style);
                success = true;
            }
            catch { }
        }

        // 如果逐个设置失败，用整体设置作为回退（可能只能设主屏）
        if (!success)
            WallpaperHelper.SetWallpapers(images, style);
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

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
    private LikeDislikeForm? likeDislikeForm;

    // 全局热键相关
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 9001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly IntPtr _hwnd; // 用于接收热键消息

    public MainContext()
    {
        settings = AppSettings.Load();
        manager = new WallpaperManager();

        // 创建一个隐藏窗口来接收热键消息
        var dummyForm = new Form()
        {
            Width = 0,
            Height = 0,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            WindowState = FormWindowState.Minimized,
            Opacity = 0
        };
        dummyForm.Load += (s, e) =>
        {
            RegisterHotKey(dummyForm.Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, (uint)Keys.W);
            dummyForm.Activate(); // 确保有焦点接收消息
        };
        dummyForm.FormClosed += (s, e) => UnregisterHotKey(dummyForm.Handle, HOTKEY_ID);
        // 处理热键消息
        dummyForm.WndProc += HotKeyWndProc;
        // 需要重写 WndProc，我们用 lambda 不行，需要继承，简单方法：使用 NativeWindow
        // 更简单的方法：注册全局热键后，在 MainContext 中用一个隐藏 NativeWindow 监听
        // 这里改用 NativeWindow
        var hotKeyWindow = new HotKeyWindow(this);
        hotKeyWindow.CreateHandle(new CreateParams());
        _hwnd = hotKeyWindow.Handle;

        // 显示主窗体（隐藏）
        dummyForm.Show();
        dummyForm.Hide();

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

    // 接收热键的 NativeWindow
    private class HotKeyWindow : NativeWindow
    {
        private MainContext _ctx;
        public HotKeyWindow(MainContext ctx) { _ctx = ctx; }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                _ctx.ChangeWallpaper();
            }
            base.WndProc(ref m);
        }
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
        // 访客模式启用且文件夹有效，则返回访客文件夹
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
            ChangeWallpaper(useTransition: false); // 启动时不做过渡
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

    /// <summary>
    /// 切换壁纸（支持平滑过渡）
    /// </summary>
    private void ChangeWallpaper(bool useTransition = true)
    {
        // 访客模式下不移动文件
        if (!settings.GuestMode)
            manager.MoveCurrentToDefaultIfNotMoved();

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
            var img = manager.GetRandomImage();
            images = img != null ? new string[] { img } : Array.Empty<string>();
        }
        else
        {
            images = manager.GetRandomImages((int)monitorCount);
        }

        if (images.Length == 0) return;

        // 是否启用平滑过渡
        bool doTransition = useTransition && settings.SmoothTransition;

        if (doTransition)
        {
            // 异步执行过渡，避免阻塞界面
            Task.Run(() =>
            {
                var transition = new TransitionForm(images);
                transition.ShowDialog();
                // 过渡结束后直接应用壁纸（过渡窗口内部已设置）
            });
        }
        else
        {
            WallpaperHelper.SetWallpapers(images);
            manager.SetCurrentWallpapers(images);
        }
    }

    private void MarkAsLike()
    {
        if (settings.GuestMode) return; // 访客模式不允许移动
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
            // 设置已保存，重新加载
            settings = AppSettings.Load();
            string activeFolder = GetActiveFolder();
            manager.LoadFolder(activeFolder);
            ChangeWallpaper(useTransition: false); // 设置更改后立即切换一张新壁纸
            StartTimers();
        }
    }

    private void ExitApplication()
    {
        timer?.Stop();
        gameCheckTimer?.Dispose();
        UnregisterHotKey(_hwnd, HOTKEY_ID);
        likeDislikeForm?.Close();
        likeDislikeForm?.Dispose();
        trayIcon.Visible = false;
        Application.Exit();
    }
}

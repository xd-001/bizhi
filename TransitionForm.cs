using System.Drawing.Imaging;

namespace WallpaperChanger;

public class TransitionForm : Form
{
    private readonly string[] _newImages;
    private readonly int _totalDurationMs;  // 总过渡时间
    private System.Windows.Forms.Timer _timer;
    private DateTime _startTime;
    private bool _finished = false;

    public TransitionForm(string[] newImages, int speedLevel = 5)
    {
        _newImages = newImages;

        // speedLevel 1~10，对应总时间 2000ms ~ 200ms
        int speed = Math.Clamp(speedLevel, 1, 10);
        _totalDurationMs = 2200 - (speed * 200); // 1→2000, 5→1200, 10→200

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = false;              // 不置顶，防止覆盖正在使用的软件
        StartPosition = FormStartPosition.Manual;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.Opaque, true);
        BackColor = Color.Black;

        // 覆盖所有屏幕
        var allBounds = Screen.AllScreens.Aggregate(Rectangle.Empty, (current, screen) => Rectangle.Union(current, screen.Bounds));
        Bounds = allBounds;

        // 捕获当前桌面（旧壁纸）
        CaptureOldWallpapers();

        _startTime = DateTime.Now;
        _timer = new System.Windows.Forms.Timer { Interval = 20 };  // 快速刷新
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    private Dictionary<Screen, Bitmap> _oldBitmaps = new();

    private void CaptureOldWallpapers()
    {
        foreach (var screen in Screen.AllScreens)
        {
            Bitmap bmp = new Bitmap(screen.Bounds.Width, screen.Bounds.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(screen.Bounds.Left, screen.Bounds.Top, 0, 0, bmp.Size);
            }
            _oldBitmaps[screen] = bmp;
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        double elapsed = (DateTime.Now - _startTime).TotalMilliseconds;
        double progress = Math.Min(1.0, elapsed / _totalDurationMs);
        float alpha = (float)progress; // 新壁纸透明度从0到1

        Invalidate();  // 强制重绘

        if (progress >= 1.0)
        {
            _timer.Stop();
            _finished = true;
            FinishTransition();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;

        // 绘制旧壁纸（始终全不透明）
        foreach (var kvp in _oldBitmaps)
        {
            Screen screen = kvp.Key;
            Bitmap bmp = kvp.Value;
            Rectangle screenRect = screen.Bounds;
            int x = screenRect.Left - Bounds.Left;
            int y = screenRect.Top - Bounds.Top;
            g.DrawImage(bmp, new Rectangle(x, y, screenRect.Width, screenRect.Height),
                        0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel);
        }

        // 绘制新壁纸，通过透明度叠加
        double progress = (DateTime.Now - _startTime).TotalMilliseconds / _totalDurationMs;
        float alpha = (float)Math.Min(1.0, progress);
        if (alpha > 0 && _newImages.Length > 0)
        {
            ColorMatrix cm = new ColorMatrix { Matrix33 = alpha }; // 透明度矩阵
            ImageAttributes ia = new ImageAttributes();
            ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);

            foreach (var screen in Screen.AllScreens)
            {
                int screenIndex = Array.IndexOf(Screen.AllScreens, screen);
                string? imgPath = _newImages[screenIndex % _newImages.Length];
                if (!File.Exists(imgPath)) continue;
                using var img = Image.FromFile(imgPath);
                Rectangle screenRect = screen.Bounds;
                int x = screenRect.Left - Bounds.Left;
                int y = screenRect.Top - Bounds.Top;
                g.DrawImage(img, new Rectangle(x, y, screenRect.Width, screenRect.Height),
                    0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
            }
        }
    }

    private void FinishTransition()
    {
        // 直接设置最终壁纸（快速）
        WallpaperStyleToEnum style = (WallpaperStyleToEnum)new AppSettings().WallpaperStyle;
        Services.WallpaperHelper.SetWallpapers(_newImages, style);

        // 释放资源
        foreach (var bmp in _oldBitmaps.Values) bmp.Dispose();
        _oldBitmaps.Clear();

        BeginInvoke(new Action(() =>
        {
            Close();
            Dispose();
        }));
    }

    // 防止鼠标点击穿透到桌面，但又不抢夺焦点
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT 点击穿透，不影响操作
            return cp;
        }
    }
}

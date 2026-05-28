using System.Drawing.Imaging;

namespace WallpaperChanger;

public class TransitionForm : Form
{
    private readonly string[] _newImages;
    private readonly int _totalDurationMs;
    private System.Windows.Forms.Timer _timer;
    private DateTime _startTime;
    private bool _finished = false;

    public TransitionForm(string[] newImages, int speedLevel = 5)
    {
        _newImages = newImages;
        int speed = Math.Clamp(speedLevel, 1, 10);
        _totalDurationMs = 2200 - (speed * 200);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = false;
        StartPosition = FormStartPosition.Manual;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.Opaque, true);
        BackColor = Color.Black;

        var allBounds = Screen.AllScreens.Aggregate(Rectangle.Empty, (current, screen) => Rectangle.Union(current, screen.Bounds));
        Bounds = allBounds;

        CaptureOldWallpapers();

        _startTime = DateTime.Now;
        _timer = new System.Windows.Forms.Timer { Interval = 20 };
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
        Invalidate();
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

        double progress = (DateTime.Now - _startTime).TotalMilliseconds / _totalDurationMs;
        float alpha = (float)Math.Min(1.0, progress);
        if (alpha > 0 && _newImages.Length > 0)
        {
            ColorMatrix cm = new ColorMatrix { Matrix33 = alpha };
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
        // 修正：使用正确的类型名 WallpaperHelper.DesktopWallpaperStyle
        Services.WallpaperHelper.DesktopWallpaperStyle style =
            (Services.WallpaperHelper.DesktopWallpaperStyle)new AppSettings().WallpaperStyle;
        Services.WallpaperHelper.SetWallpapers(_newImages, style);

        foreach (var bmp in _oldBitmaps.Values) bmp.Dispose();
        _oldBitmaps.Clear();

        BeginInvoke(new Action(() =>
        {
            Close();
            Dispose();
        }));
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000;
            cp.ExStyle |= 0x00000020;
            return cp;
        }
    }
}

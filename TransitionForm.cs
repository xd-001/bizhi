using System.Drawing.Imaging;

namespace WallpaperChanger;

public class TransitionForm : Form
{
    private readonly Bitmap _oldScreenshot;
    private readonly Bitmap _newWallpaper;
    private readonly int _fadeSteps;
    private int _currentStep = 0;
    private System.Windows.Forms.Timer? _timer;

    public TransitionForm(Screen screen, Bitmap oldScreenshot, string newImagePath, int speedLevel = 5)
    {
        _oldScreenshot = oldScreenshot;

        // 将新壁纸缩放至屏幕尺寸
        _newWallpaper = new Bitmap(screen.Bounds.Width, screen.Bounds.Height);
        using (var g = Graphics.FromImage(_newWallpaper))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
            if (File.Exists(newImagePath))
            {
                using var img = Image.FromFile(newImagePath);
                g.DrawImage(img, 0, 0, _newWallpaper.Width, _newWallpaper.Height);
            }
        }

        // 速度：1~10 对应步数 40~10
        _fadeSteps = Math.Max(10, 40 - (speedLevel - 1) * 3);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.Opaque, true);
        Bounds = screen.Bounds;

        _timer = new System.Windows.Forms.Timer { Interval = 20 };
        _timer.Tick += Timer_Tick;
        Load += (s, e) => _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _currentStep++;
        Invalidate();
        if (_currentStep >= _fadeSteps)
        {
            _timer?.Stop();
            BeginInvoke(new Action(() => { Close(); Dispose(); }));
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        // 绘制旧截图（全不透明）
        g.DrawImage(_oldScreenshot, 0, 0);

        // 绘制新壁纸，透明度逐渐增加
        float alpha = (float)_currentStep / _fadeSteps;
        if (alpha > 0)
        {
            using var ia = new ImageAttributes();
            ColorMatrix cm = new ColorMatrix { Matrix33 = alpha };
            ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            g.DrawImage(_newWallpaper, new Rectangle(0, 0, _newWallpaper.Width, _newWallpaper.Height),
                0, 0, _newWallpaper.Width, _newWallpaper.Height, GraphicsUnit.Pixel, ia);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _newWallpaper.Dispose();
        // _oldScreenshot 由调用者负责释放
        base.OnFormClosed(e);
    }

    // 不获取焦点，鼠标穿透
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
            return cp;
        }
    }
}

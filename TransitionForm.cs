using System.Drawing.Imaging;

namespace WallpaperChanger;

public class TransitionForm : Form
{
    private readonly string[] _newImages;
    private readonly int _fadeSteps;
    private int _currentStep = 0;
    private System.Windows.Forms.Timer? _timer;
    private Bitmap? _oldComposite;
    private Bitmap? _newComposite;
    public event Action? TransitionCompleted;

    public TransitionForm(string[] newImages, int speedLevel = 5)
    {
        _newImages = newImages;
        // 根据 speedLevel 1~10 计算步数：1最慢(40步)，10最快(10步)，间隔20ms
        _fadeSteps = Math.Max(10, 40 - (speedLevel - 1) * 3);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.Opaque, true);
        Bounds = GetAllScreensBounds();

        // 捕获旧壁纸（所有屏幕的桌面截图）
        _oldComposite = CaptureDesktop();
        // 生成新壁纸的合成图
        _newComposite = CreateNewComposite();

        _timer = new System.Windows.Forms.Timer { Interval = 20 };
        _timer.Tick += Timer_Tick;
        Load += (s, e) => _timer.Start();
    }

    private Rectangle GetAllScreensBounds()
    {
        return Screen.AllScreens.Aggregate(Rectangle.Empty, (a, s) => Rectangle.Union(a, s.Bounds));
    }

    private Bitmap CaptureDesktop()
    {
        var bounds = GetAllScreensBounds();
        var bmp = new Bitmap(bounds.Width, bounds.Height);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bmp.Size);
        }
        return bmp;
    }

    private Bitmap CreateNewComposite()
    {
        var bounds = GetAllScreensBounds();
        var bmp = new Bitmap(bounds.Width, bounds.Height);
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
            for (int i = 0; i < Screen.AllScreens.Length; i++)
            {
                string path = _newImages[i % _newImages.Length];
                if (!File.Exists(path)) continue;
                using var img = Image.FromFile(path);
                var rect = Screen.AllScreens[i].Bounds;
                int x = rect.Left - bounds.Left;
                int y = rect.Top - bounds.Top;
                g.DrawImage(img, new Rectangle(x, y, rect.Width, rect.Height));
            }
        }
        return bmp;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _currentStep++;
        Invalidate();
        if (_currentStep >= _fadeSteps)
        {
            _timer?.Stop();
            TransitionCompleted?.Invoke();
            // 延迟一点关闭窗口，避免闪烁
            Task.Delay(100).ContinueWith(_ => BeginInvoke(() => { Close(); Dispose(); }));
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_oldComposite == null || _newComposite == null) return;

        float alpha = (float)_currentStep / _fadeSteps; // 新壁纸透明度
        var g = e.Graphics;
        g.DrawImage(_oldComposite, 0, 0);
        using var ia = new ImageAttributes();
        ColorMatrix cm = new ColorMatrix { Matrix33 = alpha };
        ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        g.DrawImage(_newComposite, new Rectangle(0, 0, _newComposite.Width, _newComposite.Height),
            0, 0, _newComposite.Width, _newComposite.Height, GraphicsUnit.Pixel, ia);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _oldComposite?.Dispose();
        _newComposite?.Dispose();
        base.OnFormClosed(e);
    }
}

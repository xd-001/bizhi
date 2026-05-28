using System.Drawing.Imaging;

namespace WallpaperChanger;

public class TransitionForm : Form
{
    private readonly string[] _newImages;
    private readonly Bitmap? _oldScreenshot;
    private readonly int _fadeSteps;
    private int _currentStep = 0;
    private System.Windows.Forms.Timer? _timer;
    private Bitmap? _newComposite;

    public TransitionForm(Bitmap? oldScreenshot, string[] newImages, int speedLevel = 5)
    {
        _oldScreenshot = oldScreenshot;
        _newImages = newImages;
        // 速度1~10，步数对应40~10
        _fadeSteps = Math.Max(10, 40 - (speedLevel - 1) * 3);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = false;                  // 不置顶
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.Opaque, true);
        Bounds = GetAllScreensBounds();

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
            // 动画结束，关闭窗口
            BeginInvoke(new Action(() => { Close(); Dispose(); }));
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // 绘制旧截图（始终全不透明），然后在其上绘制新壁纸并逐渐增加透明度
        var g = e.Graphics;
        if (_oldScreenshot != null)
            g.DrawImage(_oldScreenshot, 0, 0);

        float alpha = (float)_currentStep / _fadeSteps;
        if (_newComposite != null && alpha > 0)
        {
            using var ia = new ImageAttributes();
            ColorMatrix cm = new ColorMatrix { Matrix33 = alpha };
            ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            g.DrawImage(_newComposite, new Rectangle(0, 0, _newComposite.Width, _newComposite.Height),
                0, 0, _newComposite.Width, _newComposite.Height, GraphicsUnit.Pixel, ia);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _newComposite?.Dispose();
        base.OnFormClosed(e);
    }

    // 窗口不获取焦点，鼠标穿透（可点击下方内容）
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

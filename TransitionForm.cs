using System.Drawing.Imaging;

namespace WallpaperChanger;

public class TransitionForm : Form
{
    private readonly string[] _newImages;
    private readonly int _fadeSteps = 30;
    private readonly int _fadeInterval = 15; // 总时长=30*15=450ms，基本速度
    private int _currentStep = 0;
    private System.Windows.Forms.Timer? _timer;
    private Bitmap? _oldDesktop;
    private Bitmap? _newComposite;

    public TransitionForm(string[] newImages, int speedLevel = 5)
    {
        _newImages = newImages;
        // 根据 speedLevel 调整步数：1~10 -> 步数 60~10，间隔固定15ms
        int steps = Math.Max(10, 70 - speedLevel * 6);
        _fadeSteps = steps;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.Opaque, true);
        Bounds = Screen.AllScreens.Aggregate(Rectangle.Empty, (a, s) => Rectangle.Union(a, s.Bounds));

        // 截图旧桌面
        CaptureOldDesktop();
        // 创建新壁纸合成图
        CreateNewComposite();

        Opacity = 1;
        Show();
        BringToFront();
        Activate();

        _timer = new System.Windows.Forms.Timer { Interval = _fadeInterval };
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    private void CaptureOldDesktop()
    {
        _oldDesktop = new Bitmap(Bounds.Width, Bounds.Height);
        using (var g = Graphics.FromImage(_oldDesktop))
        {
            g.CopyFromScreen(Bounds.Left, Bounds.Top, 0, 0, _oldDesktop.Size);
        }
    }

    private void CreateNewComposite()
    {
        _newComposite = new Bitmap(Bounds.Width, Bounds.Height);
        using (var g = Graphics.FromImage(_newComposite))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Low;
            int i = 0;
            foreach (var screen in Screen.AllScreens)
            {
                string? path = _newImages[i % _newImages.Length];
                if (File.Exists(path))
                {
                    using var img = Image.FromFile(path);
                    var rect = screen.Bounds;
                    int x = rect.Left - Bounds.Left;
                    int y = rect.Top - Bounds.Top;
                    g.DrawImage(img, new Rectangle(x, y, rect.Width, rect.Height));
                }
                i++;
            }
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _currentStep++;
        if (_currentStep >= _fadeSteps)
        {
            _timer?.Stop();
            // 过渡完成，关闭窗口
            Close();
            Dispose();
            return;
        }

        // 重绘
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_oldDesktop == null || _newComposite == null) return;

        float alpha = (float)_currentStep / _fadeSteps; // 新壁纸的透明度
        var g = e.Graphics;
        g.DrawImage(_oldDesktop, 0, 0);
        using var ia = new ImageAttributes();
        ColorMatrix cm = new ColorMatrix { Matrix33 = alpha };
        ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        g.DrawImage(_newComposite, new Rectangle(0, 0, _newComposite.Width, _newComposite.Height),
            0, 0, _newComposite.Width, _newComposite.Height, GraphicsUnit.Pixel, ia);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _oldDesktop?.Dispose();
        _newComposite?.Dispose();
        base.OnFormClosed(e);
    }

    // 允许窗口显示但不获取焦点
    protected override bool ShowWithoutActivation => true;
}

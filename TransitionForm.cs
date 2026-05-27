using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace WallpaperChanger;

public class TransitionForm : Form
{
    private readonly string[] _newImages;
    private System.Windows.Forms.Timer _timer;
    private int _opacityStep = 0;
    private const int Steps = 20; // 过渡步数
    private const int Interval = 30; // 每步毫秒

    // 捕获当前所有屏幕的壁纸
    private readonly Dictionary<Screen, Bitmap> _oldWallpapers = new();

    public TransitionForm(string[] newImages)
    {
        _newImages = newImages;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer, true);
        BackColor = Color.Black;

        // 覆盖所有屏幕
        var allBounds = Screen.AllScreens.Aggregate(Rectangle.Empty, (current, screen) => Rectangle.Union(current, screen.Bounds));
        Bounds = allBounds;

        // 捕获当前桌面的壁纸（通过直接截取桌面背景？更好：获取系统壁纸路径并加载图片）
        // 简单方式：截取桌面壁纸，但可能会捕获图标。我们尝试获取当前壁纸路径。
        foreach (var screen in Screen.AllScreens)
        {
            // 利用 Windows API 获取当前壁纸路径（仅主显示器？不完美），这里采用截图方式
            // 我们创建一个临时 Bitmap，把桌面绘制上去
            Bitmap bmp = new Bitmap(screen.Bounds.Width, screen.Bounds.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(screen.Bounds.Left, screen.Bounds.Top, 0, 0, bmp.Size);
            }
            _oldWallpapers[screen] = bmp;
        }

        // 设置透明度为 1，先显示旧壁纸
        Opacity = 1;
        Paint += TransitionForm_Paint;

        // 启动渐变定时器
        _timer = new System.Windows.Forms.Timer { Interval = Interval };
        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    private void TransitionForm_Paint(object? sender, PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        // 绘制旧壁纸
        float oldAlpha = Math.Max(0, 1 - (_opacityStep / (float)Steps));
        if (oldAlpha > 0)
        {
            foreach (var kvp in _oldWallpapers)
            {
                Screen screen = kvp.Key;
                Bitmap bmp = kvp.Value;
                Rectangle screenRect = screen.Bounds;
                // 将屏幕坐标映射到窗体坐标
                int x = screenRect.Left - Bounds.Left;
                int y = screenRect.Top - Bounds.Top;
                using (var ia = new ImageAttributes())
                {
                    ColorMatrix cm = new ColorMatrix { Matrix33 = oldAlpha }; // 调整透明度
                    ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    g.DrawImage(bmp, new Rectangle(x, y, screenRect.Width, screenRect.Height),
                        0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, ia);
                }
            }
        }

        // 绘制新壁纸（随着步数增加，新壁纸逐渐显现）
        float newAlpha = Math.Min(1, _opacityStep / (float)Steps);
        if (newAlpha > 0 && _newImages.Length > 0)
        {
            // 多显示器处理：需要为每个屏幕绘制新壁纸
            foreach (var screen in Screen.AllScreens)
            {
                int screenIndex = Array.IndexOf(Screen.AllScreens, screen);
                string? imgPath = _newImages[screenIndex % _newImages.Length];
                if (!File.Exists(imgPath)) continue;
                using var img = Image.FromFile(imgPath);
                Rectangle screenRect = screen.Bounds;
                int x = screenRect.Left - Bounds.Left;
                int y = screenRect.Top - Bounds.Top;
                using var ia = new ImageAttributes();
                ColorMatrix cm = new ColorMatrix { Matrix33 = newAlpha };
                ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                g.DrawImage(img, new Rectangle(x, y, screenRect.Width, screenRect.Height),
                    0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
            }
        }
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _opacityStep++;
        Invalidate();
        if (_opacityStep >= Steps)
        {
            _timer.Stop();
            // 过渡结束，真正设置壁纸并关闭窗口
            FinishTransition();
        }
    }

    private void FinishTransition()
    {
        // 应用新壁纸
        Services.WallpaperHelper.SetWallpapers(_newImages);
        // 释放旧壁纸截图
        foreach (var bmp in _oldWallpapers.Values) bmp.Dispose();
        _oldWallpapers.Clear();
        // 异步关闭，避免在UI线程死锁
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
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            return cp;
        }
    }
}

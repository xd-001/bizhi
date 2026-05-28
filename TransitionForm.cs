using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using WallpaperChanger.Services;

namespace WallpaperChanger;

public class TransitionForm : Form
{
    private readonly Bitmap _oldScreenshot;
    private readonly Bitmap _newWallpaper;
    private readonly int _fadeSteps;
    private int _currentStep = 0;
    private System.Windows.Forms.Timer? _timer;

    public TransitionForm(Screen screen, Bitmap oldScreenshot, string newImagePath, int speedLevel = 5, WallpaperHelper.DesktopWallpaperStyle style = WallpaperHelper.DesktopWallpaperStyle.Stretch)
    {
        _oldScreenshot = oldScreenshot;

        int w = screen.Bounds.Width;
        int h = screen.Bounds.Height;
        _newWallpaper = new Bitmap(w, h);
        using (var g = Graphics.FromImage(_newWallpaper))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            if (File.Exists(newImagePath))
            {
                using var img = Image.FromFile(newImagePath);
                DrawImageWithStyle(g, img, w, h, style);
            }
        }

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

    private void DrawImageWithStyle(Graphics g, Image img, int width, int height, WallpaperHelper.DesktopWallpaperStyle style)
    {
        switch (style)
        {
            case WallpaperHelper.DesktopWallpaperStyle.Stretch:
                g.DrawImage(img, 0, 0, width, height);
                break;
            case WallpaperHelper.DesktopWallpaperStyle.Fill:
                {
                    float imgRatio = (float)img.Width / img.Height;
                    float screenRatio = (float)width / height;
                    int drawWidth, drawHeight, x, y;
                    if (imgRatio > screenRatio)
                    {
                        drawHeight = height;
                        drawWidth = (int)(height * imgRatio);
                        x = (width - drawWidth) / 2;
                        y = 0;
                    }
                    else
                    {
                        drawWidth = width;
                        drawHeight = (int)(width / imgRatio);
                        x = 0;
                        y = (height - drawHeight) / 2;
                    }
                    g.DrawImage(img, x, y, drawWidth, drawHeight);
                }
                break;
            case WallpaperHelper.DesktopWallpaperStyle.Tile:
                {
                    using var tileBrush = new TextureBrush(img, System.Drawing.Drawing2D.WrapMode.Tile);
                    g.FillRectangle(tileBrush, 0, 0, width, height);
                }
                break;
            case WallpaperHelper.DesktopWallpaperStyle.Center:
                {
                    int x = (width - img.Width) / 2;
                    int y = (height - img.Height) / 2;
                    g.DrawImage(img, x, y, img.Width, img.Height);
                }
                break;
            case WallpaperHelper.DesktopWallpaperStyle.Fit:
                {
                    float imgRatio = (float)img.Width / img.Height;
                    float screenRatio = (float)width / height;
                    int drawWidth, drawHeight;
                    if (imgRatio > screenRatio)
                    {
                        drawWidth = width;
                        drawHeight = (int)(width / imgRatio);
                    }
                    else
                    {
                        drawHeight = height;
                        drawWidth = (int)(height * imgRatio);
                    }
                    int x = (width - drawWidth) / 2;
                    int y = (height - drawHeight) / 2;
                    g.FillRectangle(Brushes.Black, 0, 0, width, height);
                    g.DrawImage(img, x, y, drawWidth, drawHeight);
                }
                break;
            default:
                g.DrawImage(img, 0, 0, width, height);
                break;
        }
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
        g.DrawImage(_oldScreenshot, 0, 0);

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
        base.OnFormClosed(e);
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

using WallpaperChanger.Services;

namespace WallpaperChanger;

public partial class GalleryForm : Form
{
    private readonly string _imageFolder;
    private FlowLayoutPanel panel;
    private readonly AppSettings _settings;

    public GalleryForm(AppSettings settings)
    {
        _settings = settings;
        _imageFolder = settings.GuestMode && !string.IsNullOrWhiteSpace(settings.GuestFolder) && Directory.Exists(settings.GuestFolder)
            ? settings.GuestFolder
            : settings.WallpaperFolder;

        Text = "图库浏览 - " + Path.GetFileName(_imageFolder);
        Width = 850;
        Height = 650;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei", 9);
        BackColor = Color.FromArgb(32, 32, 32);
        ForeColor = Color.White;

        panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(40, 40, 40),
            Padding = new Padding(5)
        };
        Controls.Add(panel);

        LoadImages();
    }

    private void LoadImages()
    {
        panel.Controls.Clear();
        var extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
        if (!Directory.Exists(_imageFolder)) return;

        var files = Directory.GetFiles(_imageFolder, "*.*", SearchOption.AllDirectories)
                             .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                             .Take(500)
                             .ToArray();

        foreach (var file in files)
        {
            try
            {
                var pic = new PictureBox
                {
                    Size = new Size(160, 110),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BorderStyle = BorderStyle.FixedSingle,
                    Margin = new Padding(4),
                    Tag = file
                };
                pic.Paint += (s, e) =>
                {
                    // 绘制文件名在底部
                    var g = e.Graphics;
                    string name = Path.GetFileName(file);
                    var font = new Font("Microsoft YaHei", 7);
                    var brush = new SolidBrush(Color.White);
                    var bgBrush = new SolidBrush(Color.FromArgb(60, 0, 0, 0));
                    var size = g.MeasureString(name, font);
                    g.FillRectangle(bgBrush, new RectangleF(0, pic.Height - size.Height - 2, pic.Width, size.Height + 2));
                    g.DrawString(name, font, brush, 2, pic.Height - size.Height - 1);
                };
                pic.Click += Pic_Click;
                using (var img = Image.FromFile(file))
                {
                    pic.Image = new Bitmap(img, pic.Size); // 缩略图
                }
                panel.Controls.Add(pic);
            }
            catch { }
        }
    }

    private void Pic_Click(object? sender, EventArgs e)
    {
        if (sender is PictureBox pic && pic.Tag is string path)
        {
            var style = (WallpaperHelper.DesktopWallpaperStyle)_settings.WallpaperStyle;
            uint monitorCount = (uint)Screen.AllScreens.Length;
            var images = Enumerable.Repeat(path, (int)monitorCount).ToArray();
            WallpaperHelper.SetWallpapers(images, style);
            MessageBox.Show($"已设置壁纸：{Path.GetFileName(path)}", "图库", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}

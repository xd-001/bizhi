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

        Text = "图库浏览 - " + _imageFolder;
        Width = 800;
        Height = 600;
        StartPosition = FormStartPosition.CenterScreen;

        panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true };
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
                             .Take(500) // 限制显示500张，避免卡顿
                             .ToArray();

        foreach (var file in files)
        {
            try
            {
                var pic = new PictureBox
                {
                    Size = new Size(150, 100),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = Image.FromFile(file),
                    Tag = file
                };
                pic.Click += Pic_Click;
                panel.Controls.Add(pic);
            }
            catch { }
        }
    }

    private void Pic_Click(object? sender, EventArgs e)
    {
        if (sender is PictureBox pic && pic.Tag is string path)
        {
            // 点击图片直接设为当前壁纸（根据多屏设置）
            var style = (WallpaperHelper.DesktopWallpaperStyle)_settings.WallpaperStyle;
            uint monitorCount = (uint)Screen.AllScreens.Length;
            var images = Enumerable.Repeat(path, (int)monitorCount).ToArray();
            WallpaperHelper.SetWallpapers(images, style);
            MessageBox.Show($"已设置壁纸：{Path.GetFileName(path)}", "图库");
        }
    }
}

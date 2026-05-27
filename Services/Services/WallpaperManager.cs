namespace WallpaperChanger.Services;

public class WallpaperManager
{
    private List<string> _images = new();
    private string? _lastImage;
    private readonly Random _random = new();

    public void LoadFolder(string folder)
    {
        if (!Directory.Exists(folder))
        {
            _images = new List<string>();
            return;
        }

        var extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
        _images = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
                          .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                          .ToList();
        _lastImage = null;
    }

    public string? GetRandomImage()
    {
        if (_images.Count == 0) return null;
        if (_images.Count == 1) return _images[0];

        string selected;
        do
        {
            selected = _images[_random.Next(_images.Count)];
        } while (selected == _lastImage && _images.Count > 1);

        _lastImage = selected;
        return selected;
    }
}

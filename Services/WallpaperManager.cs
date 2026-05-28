namespace WallpaperChanger.Services;

public class WallpaperManager
{
    private List<string> _images = new();
    private string? _lastImage;
    private readonly Random _random = new();
    private readonly object _lock = new();

    public void LoadFolder(string folder)
    {
        lock (_lock)
        {
            if (!Directory.Exists(folder))
            {
                _images = new List<string>();
                return;
            }
            var extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
            _images = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
                              .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                              .ToList();
            _lastImage = null;
        }
    }

    public string? GetRandomImage()
    {
        lock (_lock)
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

    /// <summary>
    /// 将指定壁纸文件移动到目标文件夹
    /// </summary>
    public void MoveWallpaperToFolder(string filePath, string targetFolder)
    {
        lock (_lock)
        {
            if (!File.Exists(filePath)) return;

            Directory.CreateDirectory(targetFolder);
            string dest = Path.Combine(targetFolder, Path.GetFileName(filePath));
            if (File.Exists(dest))
            {
                dest = Path.Combine(targetFolder,
                    Path.GetFileNameWithoutExtension(filePath) + "_" + DateTime.Now.Ticks + Path.GetExtension(filePath));
            }
            File.Move(filePath, dest);
            _images.Remove(filePath);
        }
    }
}

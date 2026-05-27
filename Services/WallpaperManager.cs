namespace WallpaperChanger.Services;

public class WallpaperManager
{
    private List<string> _images = new();
    private string? _lastImage;
    private readonly Random _random = new();
    private readonly object _lock = new();

    private List<string> _currentWallpapers = new();
    private bool _currentMoved = false;

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
            // 修改点：AllDirectories 让子文件夹（默认/喜欢/不喜欢）中的图片也参与随机
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

    public string[] GetRandomImages(int count)
    {
        lock (_lock)
        {
            if (_images.Count == 0) return Array.Empty<string>();
            if (_images.Count <= count)
            {
                var shuffled = _images.OrderBy(_ => _random.Next()).ToArray();
                _lastImage = shuffled[0];
                return shuffled;
            }

            var result = new List<string>();
            var tempList = new List<string>(_images);
            if (_lastImage != null && tempList.Count > 1)
                tempList.Remove(_lastImage);

            for (int i = 0; i < count; i++)
            {
                if (tempList.Count == 0) break;
                int idx = _random.Next(tempList.Count);
                result.Add(tempList[idx]);
                if (i == 0) _lastImage = tempList[idx];
                tempList.RemoveAt(idx);
            }
            return result.ToArray();
        }
    }

    public void SetCurrentWallpapers(string[] paths)
    {
        lock (_lock)
        {
            _currentWallpapers = new List<string>(paths);
            _currentMoved = false;
        }
    }

    public void MoveCurrentWallpapers(string targetFolder)
    {
        lock (_lock)
        {
            if (_currentWallpapers.Count == 0 || _currentMoved) return;

            Directory.CreateDirectory(targetFolder);
            foreach (var file in _currentWallpapers)
            {
                if (File.Exists(file))
                {
                    string dest = Path.Combine(targetFolder, Path.GetFileName(file));
                    if (File.Exists(dest))
                    {
                        dest = Path.Combine(targetFolder,
                            Path.GetFileNameWithoutExtension(file) + "_" + DateTime.Now.Ticks + Path.GetExtension(file));
                    }
                    File.Move(file, dest);
                    _images.Remove(file);
                }
            }
            _currentWallpapers.Clear();
            _currentMoved = true;
        }
    }

    public void MoveCurrentToDefaultIfNotMoved()
    {
        lock (_lock)
        {
            if (_currentWallpapers.Count == 0 || _currentMoved) return;

            string? root = Path.GetDirectoryName(_currentWallpapers[0]);
            if (root == null) return;

            string defaultFolder = Path.Combine(root, AppSettings.DefaultFolderName);
            MoveCurrentWallpapers(defaultFolder);
        }
    }
}

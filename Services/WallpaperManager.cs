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
    /// 获取 count 张互不相同的随机图片，如果图片不足 count 则尽可能不重复
    /// </summary>
    public string[] GetRandomImages(int count)
    {
        lock (_lock)
        {
            if (_images.Count == 0) return Array.Empty<string>();
            if (_images.Count <= count)
            {
                // 打乱全部图片，避免顺序重复
                var shuffled = _images.OrderBy(_ => _random.Next()).ToArray();
                if (shuffled.Length > 0) _lastImage = shuffled[0];
                return shuffled;
            }

            var result = new HashSet<string>();
            var available = new List<string>(_images);

            // 避免上一张图片出现在第一位
            if (_lastImage != null && available.Count > 1)
            {
                available.Remove(_lastImage);
                result.Add(_lastImage); // 不添加，只为了移除后首位不是它，后面再添加回来
                available = available.OrderBy(_ => _random.Next()).ToList();
                available.Insert(0, _lastImage); // 放回但不在首位
            }

            while (result.Count < count)
            {
                if (available.Count == 0) break;
                int idx = _random.Next(available.Count);
                string img = available[idx];
                if (result.Add(img))
                {
                    available.RemoveAt(idx);
                }
                // 防止死循环
                if (result.Count == available.Count) break;
            }
            // 如果还是不够，从剩余图片中随机补足
            var remaining = new List<string>(_images);
            remaining.RemoveAll(r => result.Contains(r));
            while (result.Count < count && remaining.Count > 0)
            {
                int idx = _random.Next(remaining.Count);
                result.Add(remaining[idx]);
                remaining.RemoveAt(idx);
            }

            var arr = result.ToArray();
            if (arr.Length > 0) _lastImage = arr[0];
            return arr;
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

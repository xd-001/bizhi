namespace WallpaperChanger.Services;

public class WallpaperManager
{
    private List<string> _images = new();
    private string? _lastImage;
    private readonly Random _random = new();
    private readonly object _lock = new();

    // 当前正在显示的壁纸路径（可能多显示器）
    private List<string> _currentWallpapers = new();
    // 记录当前壁纸是否已被用户手动移动过（喜欢/不喜欢），防止重复移动
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
            _images = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
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

    /// <summary>
    /// 设置当前显示的壁纸列表
    /// </summary>
    public void SetCurrentWallpapers(string[] paths)
    {
        lock (_lock)
        {
            _currentWallpapers = new List<string>(paths);
            _currentMoved = false; // 新壁纸，用户未移动
        }
    }

    /// <summary>
    /// 将当前所有壁纸移动到指定文件夹
    /// </summary>
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
                    // 如果目标已存在，加数字后缀避免覆盖
                    if (File.Exists(dest))
                    {
                        dest = Path.Combine(targetFolder,
                            Path.GetFileNameWithoutExtension(file) + "_" + DateTime.Now.Ticks + Path.GetExtension(file));
                    }
                    File.Move(file, dest);
                    // 从缓存列表中移除
                    _images.Remove(file);
                }
            }
            _currentWallpapers.Clear();
            _currentMoved = true;
        }
    }

    /// <summary>
    /// 将当前壁纸移动到默认文件夹（切换壁纸时调用，且用户未手动移动过）
    /// </summary>
    public void MoveCurrentToDefaultIfNotMoved()
    {
        lock (_lock)
        {
            if (_currentWallpapers.Count == 0 || _currentMoved) return;

            // 获取壁纸根文件夹（从第一张图片路径推断）
            string? root = Path.GetDirectoryName(_currentWallpapers[0]);
            if (root == null) return;

            string defaultFolder = Path.Combine(root, AppSettings.DefaultFolderName);
            MoveCurrentWallpapers(defaultFolder);
        }
    }
}

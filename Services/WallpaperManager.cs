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

    /// <summary>
    /// 获取单张随机壁纸（避免与上一张重复）
    /// </summary>
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

    /// <summary>
    /// 获取指定数量的随机壁纸（彼此不重复，且不与上一组的第一张相同）
    /// </summary>
    public string[] GetRandomImages(int count)
    {
        if (_images.Count == 0) return Array.Empty<string>();
        if (_images.Count <= count)
        {
            // 图片不够，全打乱返回
            var shuffled = _images.OrderBy(_ => _random.Next()).ToArray();
            _lastImage = shuffled[0];
            return shuffled;
        }

        var result = new List<string>();
        var tempList = new List<string>(_images);
        // 去掉上一张，避免第一张相同
        if (_lastImage != null && tempList.Count > 1)
            tempList.Remove(_lastImage);

        for (int i = 0; i < count; i++)
        {
            if (tempList.Count == 0) break;
            int idx = _random.Next(tempList.Count);
            result.Add(tempList[idx]);
            if (i == 0) _lastImage = tempList[idx]; // 更新lastImage为第一个
            tempList.RemoveAt(idx); // 保证同一批次不重复
        }

        return result.ToArray();
    }
}

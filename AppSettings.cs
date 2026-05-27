using System.Text.Json;

namespace WallpaperChanger;

public class AppSettings
{
    public string WallpaperFolder { get; set; } = "";
    public int IntervalSeconds { get; set; } = 600;
    public bool StartWithWindows { get; set; } = false;
    public List<string> GameProcessNames { get; set; } = new();   // 新增

    // 子文件夹名称（相对于壁纸文件夹）
    public static readonly string DefaultFolderName = "默认";
    public static readonly string LikeFolderName = "喜欢";
    public static readonly string DislikeFolderName = "不喜欢";

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WallpaperChanger", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

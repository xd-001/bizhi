using System.Text.Json;

namespace WallpaperChanger;

public class AppSettings
{
    public string WallpaperFolder { get; set; } = "";
    public int IntervalSeconds { get; set; } = 600;
    public bool StartWithWindows { get; set; } = false;
    public List<string> GameProcessNames { get; set; } = new();
    public bool MultiMonitorSameWallpaper { get; set; } = false;

    // 壁纸样式：0=拉伸, 1=填充, 2=平铺, 3=居中, 4=适应
    public int WallpaperStyle { get; set; } = 0;

    // 过渡设置
    public bool SmoothTransition { get; set; } = false;
    public int TransitionSpeed { get; set; } = 5;   // 1~10，越大越快（总时间越短）

    public bool GuestMode { get; set; } = false;
    public string GuestFolder { get; set; } = "";

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

using System.Text.Json;

namespace WallpaperChanger;

public class AppSettings
{
    public string WallpaperFolder { get; set; } = "";
    public int IntervalSeconds { get; set; } = 600;
    public bool StartWithWindows { get; set; } = false;
    public List<string> GameProcessNames { get; set; } = new();
    public bool MultiMonitorSameWallpaper { get; set; } = false;
    public int WallpaperStyle { get; set; } = 0;
    public bool SmoothTransition { get; set; } = false;
    public int TransitionSpeed { get; set; } = 5;
    public bool GuestMode { get; set; } = false;
    public string GuestFolder { get; set; } = "";

    // 快捷键
    public bool HotKeyCtrl { get; set; } = true;
    public bool HotKeyShift { get; set; } = true;
    public bool HotKeyAlt { get; set; } = false;
    public Keys HotKeyKey { get; set; } = Keys.W;

    public bool PerMonitorPause { get; set; } = false;

    // 首次运行标志
    public bool FirstRun { get; set; } = true;

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

    public uint GetHotKeyModifiers()
    {
        uint mod = 0;
        if (HotKeyCtrl) mod |= 0x0002;
        if (HotKeyShift) mod |= 0x0004;
        if (HotKeyAlt) mod |= 0x0001;
        return mod;
    }
}

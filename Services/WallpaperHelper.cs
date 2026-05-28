using System.Runtime.InteropServices;

namespace WallpaperChanger.Services;

public static class WallpaperHelper
{
    public enum DesktopWallpaperStyle
    {
        Stretch = 0,
        Fill = 10,
        Tile = 1,
        Center = 2,
        Fit = 6
    }

    public static void SetWallpapers(string[] imagePaths, DesktopWallpaperStyle style = DesktopWallpaperStyle.Stretch)
    {
        if (imagePaths.Length == 0) return;

        try
        {
            Type? type = Type.GetTypeFromProgID("DesktopWallpaper.Wallpaper");
            if (type != null)
            {
                dynamic wallpaper = Activator.CreateInstance(type)!;
                uint monitorCount = wallpaper.GetMonitorDevicePathCount();

                for (uint i = 0; i < monitorCount; i++)
                {
                    string path = imagePaths[i % imagePaths.Length];
                    string monitorId = wallpaper.GetMonitorDevicePathAt(i);
                    wallpaper.SetWallpaper(monitorId, path);
                    wallpaper.SetPosition((int)style);
                }
                return;
            }
        }
        catch { }

        // 回退：设置主显示器壁纸
        if (imagePaths.Length > 0 && File.Exists(imagePaths[0]))
        {
            const int SPI_SETDESKWALLPAPER = 20;
            const int SPIF_UPDATEINIFILE = 0x01;
            const int SPIF_SENDCHANGE = 0x02;
            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, imagePaths[0], SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        }
    }

    /// <summary>
    /// 设置单个监视器壁纸
    /// </summary>
    public static void SetWallpaperForMonitor(string monitorId, string path, DesktopWallpaperStyle style)
    {
        try
        {
            Type? type = Type.GetTypeFromProgID("DesktopWallpaper.Wallpaper");
            if (type != null)
            {
                dynamic wallpaper = Activator.CreateInstance(type)!;
                wallpaper.SetWallpaper(monitorId, path);
                wallpaper.SetPosition((int)style);
            }
        }
        catch
        {
            // 失败时尝试整体设置
            SetWallpapers(new[] { path }, style);
        }
    }

    /// <summary>
    /// 获取所有监视器ID
    /// </summary>
    public static string[] GetMonitorIds()
    {
        try
        {
            Type? type = Type.GetTypeFromProgID("DesktopWallpaper.Wallpaper");
            if (type != null)
            {
                dynamic wallpaper = Activator.CreateInstance(type)!;
                uint count = wallpaper.GetMonitorDevicePathCount();
                var ids = new string[count];
                for (uint i = 0; i < count; i++)
                    ids[i] = wallpaper.GetMonitorDevicePathAt(i);
                return ids;
            }
        }
        catch { }
        return Array.Empty<string>();
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
}

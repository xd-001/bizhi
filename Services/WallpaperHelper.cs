using System.Runtime.InteropServices;

namespace WallpaperChanger.Services;

public static class WallpaperHelper
{
    // 壁纸样式枚举，与 Windows COM 接口常量对应
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

        // 回退：单屏系统
        if (imagePaths.Length > 0 && File.Exists(imagePaths[0]))
        {
            const int SPI_SETDESKWALLPAPER = 20;
            const int SPIF_UPDATEINIFILE = 0x01;
            const int SPIF_SENDCHANGE = 0x02;
            SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, imagePaths[0], SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
}

using System.Runtime.InteropServices;

namespace WallpaperChanger.Services;

public static class WallpaperHelper
{
    // 使用 IDesktopWallpaper 接口（Windows 8+ 均支持）
    public static void SetWallpapers(string[] imagePaths)
    {
        try
        {
            Type? type = Type.GetTypeFromProgID("DesktopWallpaper.Wallpaper");
            if (type == null) return;

            dynamic wallpaper = Activator.CreateInstance(type)!;
            uint monitorCount = wallpaper.GetMonitorDevicePathCount();

            for (uint i = 0; i < monitorCount; i++)
            {
                // 循环使用图片，如果图片数量少于屏幕，会重复
                string path = imagePaths[i % imagePaths.Length];
                string monitorId = wallpaper.GetMonitorDevicePathAt(i);
                wallpaper.SetWallpaper(monitorId, path);
            }
        }
        catch
        {
            // 如果 COM 调用失败（极旧的系统），回退到单屏壁纸
            if (imagePaths.Length > 0)
                FallbackSetWallpaper(imagePaths[0]);
        }
    }

    // 单屏回退
    private static void FallbackSetWallpaper(string filePath)
    {
        const int SPI_SETDESKWALLPAPER = 20;
        const int SPIF_UPDATEINIFILE = 0x01;
        const int SPIF_SENDCHANGE = 0x02;

        SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, filePath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
}

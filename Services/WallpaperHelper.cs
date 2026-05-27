using System.Runtime.InteropServices;

namespace WallpaperChanger.Services;

public static class WallpaperHelper
{
    /// <summary>
    /// 设置多显示器壁纸（无过渡）
    /// </summary>
    public static void SetWallpapers(string[] imagePaths)
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
                }
                return;
            }
        }
        catch { }

        // 回退单屏
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

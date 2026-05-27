using System.Runtime.InteropServices;

namespace WallpaperChanger.Services;

public static class GameDetector
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    private struct RECT { public int Left, Top, Right, Bottom; }

    public static bool IsFullScreenAppRunning()
    {
        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == GetDesktopWindow() || fg == GetShellWindow())
            return false;

        if (!IsWindowVisible(fg)) return false;

        GetWindowRect(fg, out RECT rect);
        var screen = Screen.FromHandle(fg);
        var screenBounds = screen.Bounds;

        return rect.Left <= screenBounds.Left && rect.Top <= screenBounds.Top &&
               rect.Right >= screenBounds.Right && rect.Bottom >= screenBounds.Bottom;
    }
}

using System.Diagnostics;
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

    /// <summary>
    /// 检查指定进程是否正在运行
    /// </summary>
    public static bool IsProcessRunning(List<string> processNames)
    {
        if (processNames == null || processNames.Count == 0) return false;
        foreach (var name in processNames)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                var procs = Process.GetProcessesByName(name.Trim());
                if (procs.Length > 0) return true;
            }
        }
        return false;
    }
}

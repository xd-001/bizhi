using System.Runtime.InteropServices;

namespace WallpaperChanger;

public class HotKeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private readonly int _id;
    private readonly Action _callback;
    private HotKeyWindow? _window;

    public HotKeyManager(int id, uint modifiers, Keys key, Action callback)
    {
        _id = id;
        _callback = callback;
        _window = new HotKeyWindow(this);
        _window.CreateHandle(new CreateParams());
        RegisterHotKey(_window.Handle, _id, modifiers, (uint)key);
    }

    public void Unregister()
    {
        if (_window != null && _window.Handle != IntPtr.Zero)
        {
            UnregisterHotKey(_window.Handle, _id);
            _window.DestroyHandle();
        }
    }

    public void UpdateHotKey(uint modifiers, Keys key)
    {
        Unregister();
        _window = new HotKeyWindow(this);
        _window.CreateHandle(new CreateParams());
        RegisterHotKey(_window.Handle, _id, modifiers, (uint)key);
    }

    private void OnHotKeyPressed()
    {
        _callback?.Invoke();
    }

    private class HotKeyWindow : NativeWindow
    {
        private readonly HotKeyManager _manager;
        public HotKeyWindow(HotKeyManager manager) => _manager = manager;
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == _manager._id)
                _manager.OnHotKeyPressed();
            base.WndProc(ref m);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public void Dispose()
    {
        Unregister();
    }
}

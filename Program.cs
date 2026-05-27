namespace WallpaperChanger;

static class Program
{
    [STAThread]
    static void Main()
    {
        bool createdNew;
        using var mutex = new Mutex(true, "WallpaperChangerSingleInstance", out createdNew);
        if (!createdNew) return; // 防止多开

        ApplicationConfiguration.Initialize();
        Application.Run(new MainContext());
    }
}

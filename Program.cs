namespace WallpaperChanger;

static class Program
{
    [STAThread]
    static void Main()
    {
        bool createdNew;
        using var mutex = new Mutex(true, "WallpaperChangerSingleInstance", out createdNew);
        if (!createdNew)
        {
            MessageBox.Show("壁纸切换器已经在运行中。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return; // 防止多开
        }

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainContext());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"程序启动失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

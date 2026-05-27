using Microsoft.Win32;

namespace WallpaperChanger;

public partial class SettingsForm : Form
{
    private AppSettings _settings;
    private TextBox txtFolder = new();
    private NumericUpDown numIntervalSeconds = new();
    private CheckBox chkStartup = new();
    private TextBox txtGameProcesses = new();
    private CheckBox chkSameWallpaper = new();
    private CheckBox chkSmooth = new();          // 平滑过渡
    private CheckBox chkGuestMode = new();       // 访客模式
    private TextBox txtGuestFolder = new();      // 访客文件夹
    private Button btnBrowseMain = new();
    private Button btnBrowseGuest = new();
    private Button btnSave = new();

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;
        Text = "壁纸切换器设置";
        Width = 520;
        Height = 460;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        BuildUI();
        LoadSettings();
    }

    private void BuildUI()
    {
        int y = 20;
        // 主文件夹
        var lblFolder = new Label { Text = "主文件夹:", Left = 20, Top = y, Width = 80 };
        txtFolder = new TextBox { Left = 110, Top = y - 2, Width = 260 };
        btnBrowseMain = new Button { Text = "浏览...", Left = 380, Top = y - 4, Width = 70 };
        btnBrowseMain.Click += (s, e) => {
            using var fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == DialogResult.OK) txtFolder.Text = fbd.SelectedPath;
        };
        y += 35;

        // 间隔
        var lblInterval = new Label { Text = "切换间隔(秒):", Left = 20, Top = y, Width = 100 };
        numIntervalSeconds = new NumericUpDown { Left = 130, Top = y - 2, Width = 80 };
        numIntervalSeconds.Minimum = 15;
        numIntervalSeconds.Maximum = 86400;
        numIntervalSeconds.Value = 600;
        y += 35;

        // 开机启动
        chkStartup = new CheckBox { Text = "开机自动启动", Left = 20, Top = y, Width = 150 };
        y += 30;

        // 游戏进程
        var lblGame = new Label { Text = "游戏进程名(逗号分隔):", Left = 20, Top = y, Width = 160 };
        txtGameProcesses = new TextBox { Left = 180, Top = y - 2, Width = 230 };
        txtGameProcesses.PlaceholderText = "例: r5apex,notepad";
        y += 35;

        // 多屏同一壁纸
        chkSameWallpaper = new CheckBox { Text = "所有显示器使用同一张壁纸", Left = 20, Top = y, Width = 240 };
        y += 30;

        // 平滑过渡
        chkSmooth = new CheckBox { Text = "启用平滑过渡（淡入淡出）", Left = 20, Top = y, Width = 230 };
        y += 30;

        // 访客模式分隔
        var lblGuest = new Label { Text = "访客模式", Left = 20, Top = y, Width = 80, Font = new Font(Font, FontStyle.Bold) };
        y += 25;
        chkGuestMode = new CheckBox { Text = "启用访客模式（临时使用下方文件夹）", Left = 30, Top = y, Width = 320 };
        y += 30;
        var lblGuestFolder = new Label { Text = "访客文件夹:", Left = 40, Top = y, Width = 80 };
        txtGuestFolder = new TextBox { Left = 130, Top = y - 2, Width = 260 };
        btnBrowseGuest = new Button { Text = "浏览...", Left = 400, Top = y - 4, Width = 70 };
        btnBrowseGuest.Click += (s, e) => {
            using var fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == DialogResult.OK) txtGuestFolder.Text = fbd.SelectedPath;
        };
        y += 40;

        // 保存按钮
        btnSave = new Button { Text = "保存", Left = 200, Top = y + 10, Width = 80 };
        btnSave.Click += BtnSave_Click;

        Controls.AddRange(new Control[] {
            lblFolder, txtFolder, btnBrowseMain,
            lblInterval, numIntervalSeconds,
            chkStartup,
            lblGame, txtGameProcesses,
            chkSameWallpaper,
            chkSmooth,
            lblGuest,
            chkGuestMode, lblGuestFolder, txtGuestFolder, btnBrowseGuest,
            btnSave
        });
    }

    private void LoadSettings()
    {
        txtFolder.Text = _settings.WallpaperFolder;
        numIntervalSeconds.Value = _settings.IntervalSeconds;
        chkStartup.Checked = _settings.StartWithWindows;
        txtGameProcesses.Text = string.Join(",", _settings.GameProcessNames);
        chkSameWallpaper.Checked = _settings.MultiMonitorSameWallpaper;
        chkSmooth.Checked = _settings.SmoothTransition;
        chkGuestMode.Checked = _settings.GuestMode;
        txtGuestFolder.Text = _settings.GuestFolder;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        _settings.WallpaperFolder = txtFolder.Text;
        _settings.IntervalSeconds = (int)numIntervalSeconds.Value;
        _settings.StartWithWindows = chkStartup.Checked;
        _settings.GameProcessNames = txtGameProcesses.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
        _settings.MultiMonitorSameWallpaper = chkSameWallpaper.Checked;
        _settings.SmoothTransition = chkSmooth.Checked;
        _settings.GuestMode = chkGuestMode.Checked;
        _settings.GuestFolder = txtGuestFolder.Text;
        _settings.Save();

        RegistryKey? rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
        string appName = "WallpaperChanger";
        if (_settings.StartWithWindows)
            rk?.SetValue(appName, Application.ExecutablePath);
        else
            rk?.DeleteValue(appName, false);

        DialogResult = DialogResult.OK;
        Close();
    }
}

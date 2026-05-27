using Microsoft.Win32;

namespace WallpaperChanger;

public partial class SettingsForm : Form
{
    private AppSettings _settings;
    private TextBox txtFolder = new();
    private NumericUpDown numIntervalSeconds = new();
    private CheckBox chkStartup = new();
    private TextBox txtGameProcesses = new();
    private CheckBox chkSameWallpaper = new();   // 新增
    private Button btnBrowse = new();
    private Button btnSave = new();

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;
        Text = "壁纸切换器设置";
        Width = 500;
        Height = 350;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        BuildUI();
        LoadSettings();
    }

    private void BuildUI()
    {
        var lblFolder = new Label { Text = "壁纸文件夹:", Left = 20, Top = 20, Width = 80 };
        txtFolder = new TextBox { Left = 110, Top = 18, Width = 250 };
        btnBrowse = new Button { Text = "浏览...", Left = 370, Top = 16, Width = 70 };
        btnBrowse.Click += (s, e) =>
        {
            using var fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == DialogResult.OK)
                txtFolder.Text = fbd.SelectedPath;
        };

        var lblInterval = new Label { Text = "切换间隔(秒):", Left = 20, Top = 60, Width = 100 };
        numIntervalSeconds = new NumericUpDown { Left = 130, Top = 57, Width = 80 };
        numIntervalSeconds.Minimum = 15;
        numIntervalSeconds.Maximum = 86400;
        numIntervalSeconds.Value = 600;
        numIntervalSeconds.DecimalPlaces = 0;

        chkStartup = new CheckBox { Text = "开机自动启动", Left = 20, Top = 95, Width = 150 };

        var lblGame = new Label { Text = "游戏进程名\n(逗号分隔):", Left = 20, Top = 130, Width = 100 };
        lblGame.AutoSize = false;
        lblGame.Height = 35;
        txtGameProcesses = new TextBox { Left = 130, Top = 135, Width = 280 };
        txtGameProcesses.PlaceholderText = "例: r5apex,notepad";

        // 新增：多屏统一壁纸复选框
        chkSameWallpaper = new CheckBox { Text = "所有显示器使用同一张壁纸", Left = 20, Top = 180, Width = 220 };

        btnSave = new Button { Text = "保存", Left = 200, Top = 230, Width = 80 };
        btnSave.Click += BtnSave_Click;

        Controls.AddRange(new Control[] {
            lblFolder, txtFolder, btnBrowse,
            lblInterval, numIntervalSeconds,
            chkStartup,
            lblGame, txtGameProcesses,
            chkSameWallpaper,
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

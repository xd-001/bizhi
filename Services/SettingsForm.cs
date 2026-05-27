using Microsoft.Win32;

namespace WallpaperChanger;

public partial class SettingsForm : Form
{
    private AppSettings _settings;
    private TextBox txtFolder = new();
    private NumericUpDown numInterval = new();
    private CheckBox chkStartup = new();
    private Button btnBrowse = new();
    private Button btnSave = new();

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;
        Text = "壁纸切换器设置";
        Width = 450;
        Height = 250;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        BuildUI();
        LoadSettings();
    }

    private void BuildUI()
    {
        var lblFolder = new Label { Text = "壁纸文件夹:", Left = 20, Top = 20, Width = 80 };
        txtFolder = new TextBox { Left = 100, Top = 18, Width = 250 };
        btnBrowse = new Button { Text = "浏览...", Left = 360, Top = 16, Width = 60 };
        btnBrowse.Click += (s, e) =>
        {
            using var fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() == DialogResult.OK)
                txtFolder.Text = fbd.SelectedPath;
        };

        var lblInterval = new Label { Text = "切换间隔(分钟):", Left = 20, Top = 60, Width = 110 };
        numInterval = new NumericUpDown { Left = 135, Top = 57, Width = 60, Minimum = 1, Maximum = 1440 };

        chkStartup = new CheckBox { Text = "开机自动启动", Left = 100, Top = 95, Width = 150 };

        btnSave = new Button { Text = "保存", Left = 180, Top = 150, Width = 80 };
        btnSave.Click += BtnSave_Click;

        Controls.AddRange(new Control[] {
            lblFolder, txtFolder, btnBrowse,
            lblInterval, numInterval,
            chkStartup,
            btnSave
        });
    }

    private void LoadSettings()
    {
        txtFolder.Text = _settings.WallpaperFolder;
        numInterval.Value = _settings.IntervalMinutes;
        chkStartup.Checked = _settings.StartWithWindows;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        _settings.WallpaperFolder = txtFolder.Text;
        _settings.IntervalMinutes = (int)numInterval.Value;
        _settings.StartWithWindows = chkStartup.Checked;
        _settings.Save();

        // 注册表开机启动
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

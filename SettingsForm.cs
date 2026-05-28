using Microsoft.Win32;

namespace WallpaperChanger;

public partial class SettingsForm : Form
{
    private AppSettings _settings;
    private TextBox txtFolder = new();
    private TrackBar tbInterval = new();        // 换成滑动条
    private Label lblIntervalVal = new();       // 显示当前秒数
    private CheckBox chkStartup = new();
    private TextBox txtGameProcesses = new();
    private CheckBox chkSameWallpaper = new();
    private CheckBox chkSmooth = new();
    private TrackBar tbSpeed = new();
    private Label lblSpeedVal = new();
    private ComboBox cmbStyle = new();
    private CheckBox chkGuestMode = new();
    private TextBox txtGuestFolder = new();
    private Button btnBrowseMain = new();
    private Button btnBrowseGuest = new();
    private Button btnSave = new();

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;
        Text = "壁纸切换器设置";
        Width = 540;
        Height = 580;
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
        btnBrowseMain.Click += (s, e) => { using var fbd = new FolderBrowserDialog(); if (fbd.ShowDialog() == DialogResult.OK) txtFolder.Text = fbd.SelectedPath; };
        y += 35;

        // 切换间隔滑动条（秒）
        var lblInterval = new Label { Text = "切换间隔:", Left = 20, Top = y, Width = 80 };
        tbInterval = new TrackBar { Left = 100, Top = y - 2, Width = 200, Minimum = 15, Maximum = 86400, TickFrequency = 1800, SmallChange = 300, LargeChange = 3600 };
        tbInterval.ValueChanged += (s, e) => lblIntervalVal.Text = tbInterval.Value + " 秒";
        lblIntervalVal = new Label { Text = "600 秒", Left = 310, Top = y, Width = 100 };
        y += 45;

        // 开机启动
        chkStartup = new CheckBox { Text = "开机自动启动", Left = 20, Top = y, Width = 150 };
        y += 30;

        // 游戏进程
        var lblGame = new Label { Text = "游戏进程名(逗号分隔):", Left = 20, Top = y, Width = 160 };
        txtGameProcesses = new TextBox { Left = 180, Top = y - 2, Width = 230, PlaceholderText = "例: r5apex,notepad" };
        y += 35;

        // 多屏同一壁纸
        chkSameWallpaper = new CheckBox { Text = "所有显示器使用同一张壁纸", Left = 20, Top = y, Width = 240 };
        y += 30;

        // 壁纸样式
        var lblStyle = new Label { Text = "壁纸样式:", Left = 20, Top = y, Width = 80 };
        cmbStyle = new ComboBox { Left = 100, Top = y - 2, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbStyle.Items.AddRange(new[] { "拉伸", "填充", "平铺", "居中", "适应" });
        y += 35;

        // 平滑过渡
        chkSmooth = new CheckBox { Text = "启用平滑过渡", Left = 20, Top = y, Width = 150 };
        y += 25;
        var lblSpeed = new Label { Text = "过渡速度:", Left = 40, Top = y, Width = 80 };
        tbSpeed = new TrackBar { Left = 120, Top = y - 2, Width = 150, Minimum = 1, Maximum = 10, TickFrequency = 1 };
        tbSpeed.ValueChanged += (s, e) => lblSpeedVal.Text = tbSpeed.Value.ToString();
        lblSpeedVal = new Label { Text = "5", Left = 280, Top = y, Width = 30 };
        y += 40;

        // 访客模式
        var lblGuest = new Label { Text = "访客模式", Left = 20, Top = y, Width = 80, Font = new Font(Font, FontStyle.Bold) };
        y += 25;
        chkGuestMode = new CheckBox { Text = "启用访客模式（临时使用下方文件夹）", Left = 30, Top = y, Width = 320 };
        y += 30;
        var lblGuestFolder = new Label { Text = "访客文件夹:", Left = 40, Top = y, Width = 80 };
        txtGuestFolder = new TextBox { Left = 130, Top = y - 2, Width = 260 };
        btnBrowseGuest = new Button { Text = "浏览...", Left = 400, Top = y - 4, Width = 70 };
        btnBrowseGuest.Click += (s, e) => { using var fbd = new FolderBrowserDialog(); if (fbd.ShowDialog() == DialogResult.OK) txtGuestFolder.Text = fbd.SelectedPath; };
        y += 40;

        // 保存按钮
        btnSave = new Button { Text = "保存", Left = 220, Top = y + 10, Width = 80 };
        btnSave.Click += BtnSave_Click;

        Controls.AddRange(new Control[] {
            lblFolder, txtFolder, btnBrowseMain,
            lblInterval, tbInterval, lblIntervalVal,
            chkStartup,
            lblGame, txtGameProcesses,
            chkSameWallpaper,
            lblStyle, cmbStyle,
            chkSmooth, lblSpeed, tbSpeed, lblSpeedVal,
            lblGuest, chkGuestMode, lblGuestFolder, txtGuestFolder, btnBrowseGuest,
            btnSave
        });
    }

    private void LoadSettings()
    {
        txtFolder.Text = _settings.WallpaperFolder;
        // 限制值在滑块范围内
        int interval = Math.Clamp(_settings.IntervalSeconds, tbInterval.Minimum, tbInterval.Maximum);
        tbInterval.Value = interval;
        lblIntervalVal.Text = interval + " 秒";

        chkStartup.Checked = _settings.StartWithWindows;
        txtGameProcesses.Text = string.Join(",", _settings.GameProcessNames);
        chkSameWallpaper.Checked = _settings.MultiMonitorSameWallpaper;
        cmbStyle.SelectedIndex = _settings.WallpaperStyle < cmbStyle.Items.Count ? _settings.WallpaperStyle : 0;
        chkSmooth.Checked = _settings.SmoothTransition;
        tbSpeed.Value = Math.Clamp(_settings.TransitionSpeed, 1, 10);
        lblSpeedVal.Text = tbSpeed.Value.ToString();
        chkGuestMode.Checked = _settings.GuestMode;
        txtGuestFolder.Text = _settings.GuestFolder;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        _settings.WallpaperFolder = txtFolder.Text;
        _settings.IntervalSeconds = tbInterval.Value;          // 直接用滑块值（秒）
        _settings.StartWithWindows = chkStartup.Checked;
        _settings.GameProcessNames = txtGameProcesses.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
        _settings.MultiMonitorSameWallpaper = chkSameWallpaper.Checked;
        _settings.WallpaperStyle = cmbStyle.SelectedIndex;
        _settings.SmoothTransition = chkSmooth.Checked;
        _settings.TransitionSpeed = tbSpeed.Value;
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

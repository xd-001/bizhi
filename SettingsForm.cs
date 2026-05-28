using Microsoft.Win32;

namespace WallpaperChanger;

public partial class SettingsForm : Form
{
    private AppSettings _settings;
    private TextBox txtFolder = new();
    private TrackBar tbInterval = new();
    private Label lblIntervalVal = new();
    private CheckBox chkStartup = new();
    private TextBox txtGameProcesses = new();
    private Button btnBrowseProcess = new();        // 浏览进程
    private CheckBox chkSameWallpaper = new();
    private ComboBox cmbStyle = new();
    private CheckBox chkSmooth = new();
    private TrackBar tbSpeed = new();
    private Label lblSpeedVal = new();

    // 快捷键
    private CheckBox chkCtrl = new();
    private CheckBox chkShift = new();
    private CheckBox chkAlt = new();
    private ComboBox cmbKey = new();

    // 独立屏幕暂停
    private CheckBox chkPerMonitor = new();

    private CheckBox chkGuestMode = new();
    private TextBox txtGuestFolder = new();
    private Button btnBrowseMain = new(), btnBrowseGuest = new(), btnSave = new();

    public SettingsForm(AppSettings settings)
    {
        _settings = settings;
        Text = "壁纸切换器设置";
        Width = 550;
        Height = 700;
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
        AddLabel("主文件夹:", 20, y); txtFolder.Bounds = new Rectangle(110, y - 2, 260, 20);
        btnBrowseMain = new Button { Text = "浏览...", Left = 380, Top = y - 4, Width = 70 };
        btnBrowseMain.Click += (s, e) => { using var fbd = new FolderBrowserDialog(); if (fbd.ShowDialog() == DialogResult.OK) txtFolder.Text = fbd.SelectedPath; };
        y += 35;
        // 间隔
        AddLabel("切换间隔:", 20, y); tbInterval.Bounds = new Rectangle(100, y - 2, 220, 45);
        tbInterval.Minimum = 15; tbInterval.Maximum = 86400; tbInterval.TickFrequency = 1800; tbInterval.SmallChange = 300; tbInterval.LargeChange = 3600;
        tbInterval.ValueChanged += (s, e) => lblIntervalVal.Text = tbInterval.Value + " 秒";
        lblIntervalVal = new Label { Text = "600 秒", Left = 330, Top = y + 5, Width = 100 };
        y += 50;
        // 开机启动
        chkStartup = new CheckBox { Text = "开机自动启动", Left = 20, Top = y, Width = 150 }; y += 30;
        // 游戏进程
        AddLabel("游戏进程名:", 20, y); txtGameProcesses = new TextBox { Left = 110, Top = y - 2, Width = 200, PlaceholderText = "逗号分隔" };
        btnBrowseProcess = new Button { Text = "浏览...", Left = 320, Top = y - 4, Width = 60 };
        btnBrowseProcess.Click += (s, e) => {
            using var ofd = new OpenFileDialog { Filter = "可执行文件|*.exe", Title = "选择游戏程序" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                string name = Path.GetFileNameWithoutExtension(ofd.FileName);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    var current = txtGameProcesses.Text.Trim();
                    txtGameProcesses.Text = string.IsNullOrEmpty(current) ? name : current + "," + name;
                }
            }
        };
        y += 35;
        // 多屏同一
        chkSameWallpaper = new CheckBox { Text = "所有显示器使用同一张壁纸", Left = 20, Top = y, Width = 240 }; y += 30;
        // 壁纸样式
        AddLabel("壁纸样式:", 20, y); cmbStyle.Bounds = new Rectangle(100, y - 2, 120, 20);
        cmbStyle.DropDownStyle = ComboBoxStyle.DropDownList; cmbStyle.Items.AddRange(new[] { "拉伸", "填充", "平铺", "居中", "适应" }); y += 35;
        // 平滑过渡
        chkSmooth = new CheckBox { Text = "启用平滑过渡", Left = 20, Top = y, Width = 150 }; y += 25;
        AddLabel("过渡速度:", 40, y); tbSpeed.Bounds = new Rectangle(120, y - 2, 150, 45);
        tbSpeed.Minimum = 1; tbSpeed.Maximum = 10; tbSpeed.TickFrequency = 1;
        tbSpeed.ValueChanged += (s, e) => lblSpeedVal.Text = tbSpeed.Value.ToString();
        lblSpeedVal = new Label { Text = "5", Left = 280, Top = y + 5, Width = 30 }; y += 45;
        // 快捷键
        var lblHotkey = new Label { Text = "快捷键:", Left = 20, Top = y, Width = 80, Font = new Font(Font, FontStyle.Bold) }; y += 25;
        chkCtrl = new CheckBox { Text = "Ctrl", Left = 30, Top = y, Width = 60 };
        chkShift = new CheckBox { Text = "Shift", Left = 100, Top = y, Width = 60 };
        chkAlt = new CheckBox { Text = "Alt", Left = 170, Top = y, Width = 60 };
        cmbKey = new ComboBox { Left = 240, Top = y, Width = 80, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbKey.Items.AddRange(new object[] { "A","B","C","D","E","F","G","H","I","J","K","L","M","N","O","P","Q","R","S","T","U","V","W","X","Y","Z",
            "F1","F2","F3","F4","F5","F6","F7","F8","F9","F10","F11","F12","Space","Enter","Tab","Escape" });
        y += 35;
        // 独立屏幕暂停
        chkPerMonitor = new CheckBox { Text = "每屏幕独立暂停（前台有窗口的屏幕不换壁纸）", Left = 20, Top = y, Width = 400 }; y += 30;
        // 访客模式
        var lblGuest = new Label { Text = "访客模式", Left = 20, Top = y, Width = 80, Font = new Font(Font, FontStyle.Bold) }; y += 25;
        chkGuestMode = new CheckBox { Text = "启用访客模式", Left = 30, Top = y, Width = 200 }; y += 30;
        AddLabel("访客文件夹:", 40, y); txtGuestFolder = new TextBox { Left = 130, Top = y - 2, Width = 260 };
        btnBrowseGuest = new Button { Text = "浏览...", Left = 400, Top = y - 4, Width = 70 };
        btnBrowseGuest.Click += (s, e) => { using var fbd = new FolderBrowserDialog(); if (fbd.ShowDialog() == DialogResult.OK) txtGuestFolder.Text = fbd.SelectedPath; };
        y += 40;
        // 保存
        btnSave = new Button { Text = "保存", Left = 220, Top = y + 10, Width = 80 };
        btnSave.Click += BtnSave_Click;

        Controls.AddRange(new Control[] {
            lblFolder(), txtFolder, btnBrowseMain,
            lblInterval(), tbInterval, lblIntervalVal,
            chkStartup,
            lblGame(), txtGameProcesses, btnBrowseProcess,
            chkSameWallpaper,
            lblStyle(), cmbStyle,
            chkSmooth, lblSpeed(), tbSpeed, lblSpeedVal,
            lblHotkey, chkCtrl, chkShift, chkAlt, cmbKey,
            chkPerMonitor,
            lblGuest, chkGuestMode, lblGuestFolder(), txtGuestFolder, btnBrowseGuest,
            btnSave
        });
    }

    // 辅助标签方法，避免重复声明
    private Label lblFolder() => new Label { Text = "主文件夹:", Left = 20, Top = 20, Width = 80 };
    private Label lblInterval() => new Label { Text = "切换间隔:", Left = 20, Top = 55, Width = 80 };
    private Label lblGame() => new Label { Text = "游戏进程名:", Left = 20, Top = 140, Width = 90 };
    private Label lblStyle() => new Label { Text = "壁纸样式:", Left = 20, Top = 210, Width = 80 };
    private Label lblSpeed() => new Label { Text = "过渡速度:", Left = 40, Top = 255, Width = 80 };
    private Label lblGuestFolder() => new Label { Text = "访客文件夹:", Left = 40, Top = 380, Width = 80 };

    private void LoadSettings()
    {
        txtFolder.Text = _settings.WallpaperFolder;
        int interval = Math.Clamp(_settings.IntervalSeconds, 15, 86400);
        tbInterval.Value = interval; lblIntervalVal.Text = interval + " 秒";
        chkStartup.Checked = _settings.StartWithWindows;
        txtGameProcesses.Text = string.Join(",", _settings.GameProcessNames);
        chkSameWallpaper.Checked = _settings.MultiMonitorSameWallpaper;
        cmbStyle.SelectedIndex = Math.Clamp(_settings.WallpaperStyle, 0, cmbStyle.Items.Count - 1);
        chkSmooth.Checked = _settings.SmoothTransition;
        tbSpeed.Value = Math.Clamp(_settings.TransitionSpeed, 1, 10); lblSpeedVal.Text = tbSpeed.Value.ToString();
        chkCtrl.Checked = _settings.HotKeyCtrl;
        chkShift.Checked = _settings.HotKeyShift;
        chkAlt.Checked = _settings.HotKeyAlt;
        string keyName = _settings.HotKeyKey.ToString();
        cmbKey.SelectedIndex = cmbKey.Items.IndexOf(keyName) >= 0 ? cmbKey.Items.IndexOf(keyName) : 0;
        chkPerMonitor.Checked = _settings.PerMonitorPause;
        chkGuestMode.Checked = _settings.GuestMode;
        txtGuestFolder.Text = _settings.GuestFolder;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        _settings.WallpaperFolder = txtFolder.Text;
        _settings.IntervalSeconds = tbInterval.Value;
        _settings.StartWithWindows = chkStartup.Checked;
        _settings.GameProcessNames = txtGameProcesses.Text.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
        _settings.MultiMonitorSameWallpaper = chkSameWallpaper.Checked;
        _settings.WallpaperStyle = cmbStyle.SelectedIndex;
        _settings.SmoothTransition = chkSmooth.Checked;
        _settings.TransitionSpeed = tbSpeed.Value;
        _settings.HotKeyCtrl = chkCtrl.Checked;
        _settings.HotKeyShift = chkShift.Checked;
        _settings.HotKeyAlt = chkAlt.Checked;
        string? selKey = cmbKey.SelectedItem?.ToString();
        if (selKey != null && Enum.TryParse<Keys>(selKey, out var key)) _settings.HotKeyKey = key;
        _settings.PerMonitorPause = chkPerMonitor.Checked;
        _settings.GuestMode = chkGuestMode.Checked;
        _settings.GuestFolder = txtGuestFolder.Text;
        _settings.Save();

        RegistryKey? rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
        if (_settings.StartWithWindows) rk?.SetValue("WallpaperChanger", Application.ExecutablePath);
        else rk?.DeleteValue("WallpaperChanger", false);

        DialogResult = DialogResult.OK;
        Close();
    }
}

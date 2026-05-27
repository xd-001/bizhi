namespace WallpaperChanger;

public partial class LikeDislikeForm : Form
{
    public event Action? LikeClicked;
    public event Action? DislikeClicked;

    public LikeDislikeForm()
    {
        // 窗体属性
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(100, 50);
        BackColor = Color.Black;
        TransparencyKey = Color.Black; // 黑色透明，只显示按钮
        AllowTransparency = true;

        // 喜欢按钮（❤️）
        var btnLike = new Button
        {
            Text = "❤️",
            Font = new Font("Segoe UI", 18, FontStyle.Regular),
            ForeColor = Color.Red,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(45, 40),
            Location = new Point(5, 5),
            BackColor = Color.Transparent
        };
        btnLike.FlatAppearance.BorderSize = 0;
        btnLike.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 255, 80, 80);
        btnLike.Click += (s, e) => LikeClicked?.Invoke();

        // 不喜欢按钮（❌）
        var btnDislike = new Button
        {
            Text = "❌",
            Font = new Font("Segoe UI", 16, FontStyle.Regular),
            ForeColor = Color.Gray,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(45, 40),
            Location = new Point(50, 5),
            BackColor = Color.Transparent
        };
        btnDislike.FlatAppearance.BorderSize = 0;
        btnDislike.FlatAppearance.MouseOverBackColor = Color.FromArgb(80, 255, 80, 80);
        btnDislike.Click += (s, e) => DislikeClicked?.Invoke();

        Controls.Add(btnLike);
        Controls.Add(btnDislike);

        // 定位到屏幕右下角（主显示器的工作区域）
        var screen = Screen.PrimaryScreen?.WorkingArea ?? Screen.PrimaryScreen.Bounds;
        Location = new Point(screen.Right - Width - 10, screen.Bottom - Height - 10);
    }

    // 防止窗体获得焦点或影响其他窗口（点击穿透背景）
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            cp.ExStyle |= 0x8000000;  // WS_EX_LAYERED
            return cp;
        }
    }
}

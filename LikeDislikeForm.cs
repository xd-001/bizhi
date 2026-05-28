namespace WallpaperChanger;

public partial class LikeDislikeForm : Form
{
    public event Action? LikeClicked;
    public event Action? NextClicked;
    public event Action? DislikeClicked;

    private Button btnLike, btnNext, btnDislike;
    private Size _minSize = new Size(120, 40);
    private Point _dragOffset;
    private bool _isDragging = false;

    public LikeDislikeForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = false;                      // 不再置顶
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        TransparencyKey = Color.Black;        // 黑色透明
        AllowTransparency = true;
        MinimumSize = _minSize;
        Size = _minSize;

        // 三个按钮，均匀分布
        btnLike = CreateButton("❤️", Color.Red);
        btnLike.Click += (s, e) => LikeClicked?.Invoke();

        btnNext = CreateButton("➡️", Color.White);
        btnNext.Click += (s, e) => NextClicked?.Invoke();

        btnDislike = CreateButton("❌", Color.Gray);
        btnDislike.Click += (s, e) => DislikeClicked?.Invoke();

        Controls.Add(btnLike);
        Controls.Add(btnNext);
        Controls.Add(btnDislike);

        ArrangeButtons();

        // 鼠标事件用于拖动窗口
        MouseDown += Form_MouseDown;
        MouseMove += Form_MouseMove;
        MouseUp += Form_MouseUp;

        // 允许通过边缘调整大小（处理 WM_NCHITTEST）
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    private Button CreateButton(string text, Color foreColor)
    {
        var btn = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            ForeColor = foreColor,
            Font = new Font("Segoe UI", 12, FontStyle.Regular),
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 255, 255, 255);
        return btn;
    }

    private void ArrangeButtons()
    {
        int w = ClientSize.Width / 3;
        int h = ClientSize.Height;
        btnLike.Size = new Size(w, h);
        btnLike.Location = new Point(0, 0);

        btnNext.Size = new Size(w, h);
        btnNext.Location = new Point(w, 0);

        btnDislike.Size = new Size(w, h);
        btnDislike.Location = new Point(w * 2, 0);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ArrangeButtons();
    }

    // 拖拽移动
    private void Form_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isDragging = true;
            _dragOffset = e.Location;
        }
    }

    private void Form_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            Location = new Point(
                Location.X + e.X - _dragOffset.X,
                Location.Y + e.Y - _dragOffset.Y);
        }
    }

    private void Form_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            _isDragging = false;
    }

    // 边缘调整大小处理
    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x84;
        const int HTLEFT = 10;
        const int HTRIGHT = 11;
        const int HTTOP = 12;
        const int HTTOPLEFT = 13;
        const int HTTOPRIGHT = 14;
        const int HTBOTTOM = 15;
        const int HTBOTTOMLEFT = 16;
        const int HTBOTTOMRIGHT = 17;
        const int HTCLIENT = 1;

        base.WndProc(ref m);

        if (m.Msg == WM_NCHITTEST)
        {
            Point pt = PointToClient(new Point(m.LParam.ToInt32() & 0xffff, (m.LParam.ToInt32() >> 16) & 0xffff));
            Size clientSize = ClientSize;
            int borderWidth = 5; // 边缘检测宽度

            bool left = pt.X <= borderWidth;
            bool right = pt.X >= clientSize.Width - borderWidth;
            bool top = pt.Y <= borderWidth;
            bool bottom = pt.Y >= clientSize.Height - borderWidth;

            if (top && left) m.Result = (IntPtr)HTTOPLEFT;
            else if (top && right) m.Result = (IntPtr)HTTOPRIGHT;
            else if (bottom && left) m.Result = (IntPtr)HTBOTTOMLEFT;
            else if (bottom && right) m.Result = (IntPtr)HTBOTTOMRIGHT;
            else if (top) m.Result = (IntPtr)HTTOP;
            else if (bottom) m.Result = (IntPtr)HTBOTTOM;
            else if (left) m.Result = (IntPtr)HTLEFT;
            else if (right) m.Result = (IntPtr)HTRIGHT;
            else m.Result = (IntPtr)HTCLIENT;
        }
    }
}

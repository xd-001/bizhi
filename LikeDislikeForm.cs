namespace WallpaperChanger;

public partial class LikeDislikeForm : Form
{
    public event Action? LikeClicked;
    public event Action? NextClicked;
    public event Action? DislikeClicked;

    private Panel buttonPanel;
    private Button btnLike, btnNext, btnDislike;
    private readonly Size _minSize = new Size(200, 60);
    private Point _dragOffset;
    private bool _isDragging;

    public LikeDislikeForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(30, 30, 30);
        MinimumSize = _minSize;
        Padding = new Padding(3);     // 留出边缘用于调整大小和拖动

        buttonPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        btnLike = CreateButton("❤️ 喜欢", Color.FromArgb(255, 80, 80));
        btnLike.Click += (s, e) => LikeClicked?.Invoke();
        btnLike.MouseDown += Button_MouseDown;
        btnLike.MouseMove += Button_MouseMove;
        btnLike.MouseUp += Button_MouseUp;

        btnNext = CreateButton("➡️ 下一张", Color.White);
        btnNext.Click += (s, e) => NextClicked?.Invoke();
        btnNext.MouseDown += Button_MouseDown;
        btnNext.MouseMove += Button_MouseMove;
        btnNext.MouseUp += Button_MouseUp;

        btnDislike = CreateButton("❌ 不喜欢", Color.FromArgb(180, 180, 180));
        btnDislike.Click += (s, e) => DislikeClicked?.Invoke();
        btnDislike.MouseDown += Button_MouseDown;
        btnDislike.MouseMove += Button_MouseMove;
        btnDislike.MouseUp += Button_MouseUp;

        buttonPanel.Controls.Add(btnLike);
        buttonPanel.Controls.Add(btnNext);
        buttonPanel.Controls.Add(btnDislike);
        Controls.Add(buttonPanel);

        Size = _minSize;
        ArrangeButtons();

        // 窗体本身也支持拖拽（空白边缘）
        MouseDown += Form_MouseDown;
        MouseMove += Form_MouseMove;
        MouseUp += Form_MouseUp;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    private Button CreateButton(string text, Color foreColor)
    {
        var btn = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            ForeColor = foreColor,
            Font = new Font("Microsoft YaHei", 11, FontStyle.Bold),
            BackColor = Color.FromArgb(45, 45, 48),
            Margin = new Padding(2),
            Padding = new Padding(2),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(70, 70, 75);
        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(100, 100, 105);
        return btn;
    }

    private void ArrangeButtons()
    {
        if (buttonPanel == null) return;
        int w = buttonPanel.ClientSize.Width / 3;
        int h = buttonPanel.ClientSize.Height;
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

    // 按钮上的拖拽事件（转发给窗体处理）
    private void Button_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isDragging = true;
            _dragOffset = e.Location;
            // 将坐标转换为窗体坐标
            if (sender is Control c)
                _dragOffset = new Point(e.X + c.Location.X + Padding.Left, e.Y + c.Location.Y + Padding.Top);
        }
    }

    private void Button_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            Point screenPos = PointToScreen(new Point(e.X + ((Control)sender).Location.X + Padding.Left,
                                                      e.Y + ((Control)sender).Location.Y + Padding.Top));
            Location = new Point(screenPos.X - _dragOffset.X, screenPos.Y - _dragOffset.Y);
        }
    }

    private void Button_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            _isDragging = false;
    }

    // 窗体自身的拖拽（边缘空白处）
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
        const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14,
            HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17, HTCLIENT = 1;

        base.WndProc(ref m);
        if (m.Msg == WM_NCHITTEST)
        {
            Point pt = PointToClient(new Point(m.LParam.ToInt32() & 0xffff, (m.LParam.ToInt32() >> 16) & 0xffff));
            Size sz = ClientSize;
            int bw = 6; // 边缘检测宽度
            bool l = pt.X <= bw, r = pt.X >= sz.Width - bw, t = pt.Y <= bw, b = pt.Y >= sz.Height - bw;
            if (t && l) m.Result = (IntPtr)HTTOPLEFT;
            else if (t && r) m.Result = (IntPtr)HTTOPRIGHT;
            else if (b && l) m.Result = (IntPtr)HTBOTTOMLEFT;
            else if (b && r) m.Result = (IntPtr)HTBOTTOMRIGHT;
            else if (t) m.Result = (IntPtr)HTTOP;
            else if (b) m.Result = (IntPtr)HTBOTTOM;
            else if (l) m.Result = (IntPtr)HTLEFT;
            else if (r) m.Result = (IntPtr)HTRIGHT;
            else m.Result = (IntPtr)HTCLIENT;
        }
    }
}

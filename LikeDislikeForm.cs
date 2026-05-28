namespace WallpaperChanger;

public partial class LikeDislikeForm : Form
{
    public event Action? LikeClicked;
    public event Action? NextClicked;
    public event Action? DislikeClicked;

    private Button btnLike, btnNext, btnDislike;
    private readonly Size _minSize = new Size(240, 64);
    private Point _dragOffset;
    private bool _isDragging;

    public LikeDislikeForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = false;
        StartPosition = FormStartPosition.Manual;
        // 背景完全透明
        BackColor = Color.Black;
        TransparencyKey = Color.Black;
        AllowTransparency = true;
        MinimumSize = _minSize;
        Padding = new Padding(4);

        btnLike = CreateButton("❤️ 喜欢", Color.FromArgb(255, 100, 100));
        btnLike.Click += (s, e) => LikeClicked?.Invoke();

        btnNext = CreateButton("➡️ 下一张", Color.White);
        btnNext.Click += (s, e) => NextClicked?.Invoke();

        btnDislike = CreateButton("❌ 不喜欢", Color.FromArgb(200, 200, 200));
        btnDislike.Click += (s, e) => DislikeClicked?.Invoke();

        Controls.Add(btnLike);
        Controls.Add(btnNext);
        Controls.Add(btnDislike);

        Size = _minSize;
        ArrangeButtons();

        MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { _isDragging = true; _dragOffset = e.Location; } };
        MouseMove += (s, e) => { if (_isDragging) { Location = new Point(Location.X + e.X - _dragOffset.X, Location.Y + e.Y - _dragOffset.Y); } };
        MouseUp += (s, e) => { if (e.Button == MouseButtons.Left) _isDragging = false; };
    }

    private Button CreateButton(string text, Color foreColor)
    {
        var btn = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            ForeColor = foreColor,
            Font = new Font("Microsoft YaHei", 12, FontStyle.Bold),
            BackColor = Color.FromArgb(30, 30, 30),
            Margin = new Padding(2),
            Padding = new Padding(2),
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 2;
        btn.FlatAppearance.BorderColor = Color.FromArgb(200, 200, 200);
        btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 60);
        btn.FlatAppearance.MouseDownBackColor = Color.FromArgb(80, 80, 80);
        return btn;
    }

    private void ArrangeButtons()
    {
        if (btnLike == null) return;
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

    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x84;
        const int HTLEFT = 10, HTRIGHT = 11, HTTOP = 12, HTTOPLEFT = 13, HTTOPRIGHT = 14,
            HTBOTTOM = 15, HTBOTTOMLEFT = 16, HTBOTTOMRIGHT = 17;

        base.WndProc(ref m);
        if (m.Msg == WM_NCHITTEST)
        {
            Point pt = PointToClient(new Point(m.LParam.ToInt32() & 0xffff, (m.LParam.ToInt32() >> 16) & 0xffff));
            Size sz = ClientSize;
            int bw = 8;
            bool l = pt.X <= bw, r = pt.X >= sz.Width - bw, t = pt.Y <= bw, b = pt.Y >= sz.Height - bw;
            if (t && l) m.Result = (IntPtr)HTTOPLEFT;
            else if (t && r) m.Result = (IntPtr)HTTOPRIGHT;
            else if (b && l) m.Result = (IntPtr)HTBOTTOMLEFT;
            else if (b && r) m.Result = (IntPtr)HTBOTTOMRIGHT;
            else if (t) m.Result = (IntPtr)HTTOP;
            else if (b) m.Result = (IntPtr)HTBOTTOM;
            else if (l) m.Result = (IntPtr)HTLEFT;
            else if (r) m.Result = (IntPtr)HTRIGHT;
        }
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 详情、目标编辑和许可共用 Ore 自绘窗口。
// 移动到不同 DPI 显示器时按逻辑尺寸重排，标题按钮与正文不会重叠；Esc 关闭详情。
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

sealed class RerollDetailsWindow : Form
{
    internal readonly Panel Content = new Panel { Dock = DockStyle.Fill, BackColor = OreTheme.Background };
    readonly Panel header = new Panel { Dock = DockStyle.Top, BackColor = OreTheme.Field };
    readonly PixelLabel title = new PixelLabel { Text = L10n.T("装备详情"), Wrap = false, VerticalCenter = true, EnglishHeading = true };
    readonly OreButton minimize = new OreButton { Text = "−", AccessibleName = L10n.T("最小化") };
    readonly OreButton closeTitle = new OreButton { Text = "×", Destructive = true, AccessibleName = L10n.T("关闭") };
    Control gearIcon, gearName, details;
    OreButton queueAction, closeBody;
    float scale;
    bool arranging;
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);

    internal event Action<float> EditorLayout;
    internal RerollDetailsWindow(Font font, string titleKey = "装备详情", int logicalWidth = 570, int logicalHeight = 528)
    {
        Text = title.Text = L10n.T(titleKey); Font = font; FormBorderStyle = FormBorderStyle.None;
        AutoScaleMode = AutoScaleMode.None; BackColor = OreTheme.Background; DoubleBuffered = true;
        ShowInTaskbar = false; ControlBox = false; MinimizeBox = false; MaximizeBox = false; StartPosition = FormStartPosition.CenterParent; KeyPreview = true;
        scale = OreDpi.Scale(IntPtr.Zero);
        Padding = new Padding(Math.Max(1, (int)Math.Round(2 * scale)));
        MinimumSize = new Size(P(420), P(360)); ClientSize = new Size(P(logicalWidth), P(logicalHeight));
        Controls.Add(Content); Controls.Add(header); header.Controls.Add(title);
        header.Controls.Add(minimize); header.Controls.Add(closeTitle);
        minimize.Click += delegate { WindowState = FormWindowState.Minimized; };
        closeTitle.Click += delegate { Close(); };
        MouseEventHandler drag = delegate(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            ReleaseCapture(); SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
        };
        header.MouseDown += drag; title.MouseDown += drag;
        Resize += delegate { Arrange(); };
        Content.Resize += delegate { Arrange(); };
        KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; } };
        Arrange();
    }
    int P(int logical) { return Math.Max(1, (int)Math.Round(logical * scale)); }
    internal void SetContents(Control icon, Control name, Control text, OreButton action, OreButton close)
    {
        gearIcon = icon; gearName = name; details = text; queueAction = action; closeBody = close;
        Arrange();
    }
    void Arrange()
    {
        if (arranging || WindowState == FormWindowState.Minimized) return;
        arranging = true;
        try
        {
            header.Height = P(48);
            closeTitle.Bounds = new Rectangle(header.Width - P(42), P(8), P(34), P(32));
            minimize.Bounds = new Rectangle(header.Width - P(84), P(8), P(34), P(32));
            title.Bounds = new Rectangle(P(16), P(8), Math.Max(1, header.Width - P(108)), P(32));
            title.PixelScale = 1.05f * scale;
            minimize.PixelScale = closeTitle.PixelScale = 1.2f * scale;
            if (EditorLayout != null) EditorLayout(scale);
            if (gearIcon != null)
            {
                gearIcon.Bounds = new Rectangle(P(16), P(16), P(90), P(90));
                gearName.Bounds = new Rectangle(P(120), P(20), Math.Max(1, Content.Width - P(136)), P(80));
                details.Bounds = new Rectangle(P(16), P(118), Math.Max(1, Content.Width - P(32)), Math.Max(P(40), Content.Height - P(188)));
                int width = Math.Max(1, (Content.Width - P(48)) / 2), y = Content.Height - P(54);
                queueAction.Bounds = new Rectangle(P(16), y, width, P(42));
                closeBody.Bounds = new Rectangle(Content.Width - P(16) - width, y, width, P(42));
                queueAction.PixelScale = closeBody.PixelScale = .9f * scale;
            }
        }
        finally { arranging = false; }
    }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        float previous = scale; scale = OreDpi.Scale(Handle);
        MinimumSize = new Size(P(420), P(360)); Padding = new Padding(P(2));
        var screen = Screen.FromControl(Owner ?? this).WorkingArea;
        ClientSize = new Size(Math.Min(screen.Width - 24, (int)Math.Round(ClientSize.Width * scale / previous)),
            Math.Min(screen.Height - 24, (int)Math.Round(ClientSize.Height * scale / previous)));
        CenterToParent(); Arrange();
    }
    protected override CreateParams CreateParams
    {
        get
        {
            var result = base.CreateParams;
            // 保留边缘缩放，但明确清掉系统标题栏/菜单/按钮；句柄重建与最小化后也只能显示 Ore 标题。
            result.Style &= ~(0x00C00000 | 0x00080000 | 0x00020000 | 0x00010000);
            result.Style |= 0x00040000;
            return result;
        }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); OreTheme.Border(e.Graphics, ClientRectangle, OreTheme.Line, P(2));
    }
    protected override void WndProc(ref Message message)
    {
        // 两种 NCCALCSIZE 都使用完整客户区；不让默认非客户区绘制叠出第二条标题栏。
        if (message.Msg == 0x0083 || message.Msg == 0x0085) { message.Result = IntPtr.Zero; return; }
        if (message.Msg == 0x0086) { message.Result = new IntPtr(1); return; }
        if (message.Msg == 0x02E0)
        {
            var bounds = (OreDpi.NativeRect)Marshal.PtrToStructure(message.LParam, typeof(OreDpi.NativeRect));
            scale = (message.WParam.ToInt64() & 0xffff) / 96f;
            MinimumSize = new Size(P(420), P(360)); Padding = new Padding(P(2));
            Bounds = Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
            Arrange(); message.Result = IntPtr.Zero; return;
        }
        base.WndProc(ref message);
        if (message.Msg == 0x0084 && WindowState == FormWindowState.Normal)
        {
            long position = message.LParam.ToInt64();
            Point point = PointToClient(new Point(unchecked((short)(position & 65535)), unchecked((short)((position >> 16) & 65535))));
            int edge = P(6);
            bool left = point.X < edge, right = point.X >= ClientSize.Width - edge, top = point.Y < edge, bottom = point.Y >= ClientSize.Height - edge;
            if (left || right || top || bottom) message.Result = new IntPtr(top ? (left ? 13 : right ? 14 : 12) : bottom ? (left ? 16 : right ? 17 : 15) : left ? 10 : 11);
        }
    }
}

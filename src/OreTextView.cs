// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 只读文本保留 Windows 选择/复制与键盘操作，滚动条统一使用 Ore 绘制。
// 行数取原生换行后的可视文本行，不用字符数猜高度；滚轮只滚动当前文本区。
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

sealed class OreTextView : Panel
{
    sealed class TextBody : TextBox
    {
        internal Action ChangedViewport;
        internal Action<int> Wheel;
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x020A && Wheel != null)
            {
                Wheel(unchecked((short)((message.WParam.ToInt64() >> 16) & 65535)));
                message.Result = IntPtr.Zero; return;
            }
            base.WndProc(ref message);
            if (ChangedViewport != null && (message.Msg == 0x0101 || message.Msg == 0x0202 || message.Msg == 0x0115 ||
                message.Msg == 0x00B1 || message.Msg == 0x00B6 || message.Msg == 0x00B7)) ChangedViewport();
        }
    }
    readonly TextBody body = new TextBody { Multiline = true, ReadOnly = true, WordWrap = true,
        ScrollBars = ScrollBars.None, BorderStyle = BorderStyle.None, ShortcutsEnabled = true };
    readonly OreScrollBar bar = new OreScrollBar();
    bool syncing;
    int wheelRemainder;
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    public override string Text
    {
        get { return body == null ? base.Text : body.Text; }
        set { if (body == null) { base.Text = value; return; } if (body.Text != value) body.Text = value; SyncBar(); }
    }
    internal OreTextView()
    {
        BackColor = OreTheme.Field; ForeColor = OreTheme.Text; TabStop = false;
        Controls.Add(body); Controls.Add(bar);
        body.ChangedViewport = SyncBar;
        body.Wheel = ScrollWheel;
        body.TextChanged += delegate { SyncBar(); };
        body.HandleCreated += delegate { Arrange(); };
        bar.Changed += ScrollTo;
        Arrange();
    }
    void Arrange()
    {
        if (body == null || bar == null) return;
        float scale = OreMetrics.Scale(this);
        int inset = Math.Max(2, (int)Math.Round(3 * scale)), width = Math.Max(12, (int)Math.Round(16 * scale));
        body.Bounds = new Rectangle(inset, inset, Math.Max(1, Width - inset * 2 - width), Math.Max(1, Height - inset * 2));
        bar.Bounds = new Rectangle(Math.Max(inset, Width - inset - width), inset, width, Math.Max(1, Height - inset * 2));
        body.Font = Font; body.BackColor = BackColor; body.ForeColor = ForeColor;
        SyncBar(); Invalidate();
    }
    void SyncBar()
    {
        if (syncing || body == null || bar == null || !body.IsHandleCreated) return;
        syncing = true;
        try
        {
            int lines = SendMessage(body.Handle, 0x00BA, IntPtr.Zero, IntPtr.Zero).ToInt32();
            float lineHeight;
            using (var graphics = body.CreateGraphics()) lineHeight = body.Font.GetHeight(graphics);
            int page = Math.Max(1, (int)Math.Floor(body.ClientSize.Height / Math.Max(1d, Math.Ceiling(lineHeight))));
            int first = SendMessage(body.Handle, 0x00CE, IntPtr.Zero, IntPtr.Zero).ToInt32();
            bar.Maximum = Math.Max(0, lines - page); bar.PageSize = page;
            int position = Math.Max(0, Math.Min(bar.Maximum, first));
            if (position != first) SendMessage(body.Handle, 0x00B6, IntPtr.Zero, new IntPtr(position - first));
            bar.Value = position; bar.Visible = bar.Maximum > 0; bar.Invalidate();
        }
        finally { syncing = false; }
    }
    void ScrollTo(int line)
    {
        if (!body.IsHandleCreated) return;
        int first = SendMessage(body.Handle, 0x00CE, IntPtr.Zero, IntPtr.Zero).ToInt32();
        int next = Math.Max(0, Math.Min(bar.Maximum, line));
        SendMessage(body.Handle, 0x00B6, IntPtr.Zero, new IntPtr(next - first)); SyncBar();
    }
    void ScrollWheel(int delta)
    {
        wheelRemainder += delta;
        int ticks = wheelRemainder / 120; wheelRemainder %= 120;
        int lines = SystemInformation.MouseWheelScrollLines;
        if (ticks != 0) ScrollTo(bar.Value - ticks * (lines < 0 ? bar.PageSize : lines));
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        ScrollWheel(e.Delta);
        var handled = e as HandledMouseEventArgs; if (handled != null) handled.Handled = true;
    }
    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); Arrange(); }
    protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); Arrange(); }
    protected override void OnForeColorChanged(EventArgs e) { base.OnForeColorChanged(e); Arrange(); }
    protected override void OnBackColorChanged(EventArgs e) { base.OnBackColorChanged(e); Arrange(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); OreRenderer.Border(e.Graphics, ClientRectangle, OreTheme.Edge, Math.Max(1, (int)Math.Round(OreMetrics.Scale(this))));
    }
}

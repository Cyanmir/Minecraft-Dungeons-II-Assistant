// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
using System;
using System.Drawing;
using System.Windows.Forms;

// 保留 Checked / CheckedChanged 和 CheckBox 的键盘与辅助技术语义，完全自绘方形开关。
// Tile 为旧调用兼容字段；新建槽位控件使用 OreCheckbox，不绘制系统 Checkbox。
class OreToggle : CheckBox
{
    public bool Tile;
    bool hover, pressed;
    // 绑定检查状态、鼠标和焦点重绘；CheckedChanged 的业务绑定由页面保留。
    public OreToggle()
    {
        AutoSize = false;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
        MouseDown += delegate (object sender, MouseEventArgs e)
        {
            pressed = e.Button == MouseButtons.Left;
            Invalidate();
        };
        MouseUp += delegate
        {
            pressed = false;
            Invalidate();
        };
        CheckedChanged += delegate
        {
            Invalidate();
        };
        MouseEnter += delegate
        {
            hover = true;
            Invalidate();
        };
        MouseLeave += delegate
        {
            hover = pressed = false;
            Invalidate();
        };
        GotFocus += delegate
        {
            Invalidate();
        };
        LostFocus += delegate
        {
            pressed = false;
            Invalidate();
        };
        EnabledChanged += delegate
        {
            Invalidate();
        };
    }

    // 键盘空格沿用 CheckBox 的切换语义，仅同步视觉按下状态。
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space)
        {
            pressed = true;
            Invalidate();
        }

        base.OnKeyDown(e);
    }

    // 松开键盘后结束按下态，实际 Checked 更新仍由基类处理。
    protected override void OnKeyUp(KeyEventArgs e)
    {
        pressed = false;
        Invalidate();
        base.OnKeyUp(e);
    }

    // 不调用系统背景，避免主题开关闪烁。
    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    // 开关 56×30、复选框 24×24；尺寸按窗口 DPI 缩放，不被所在行高度影响。
    protected override void OnPaint(PaintEventArgs e)
    {
        OreRenderer.Fill(e.Graphics, ClientRectangle, Parent == null ? OreTheme.Background : Parent.BackColor);
        float scale = OreMetrics.Scale(this);
        int px = Math.Max(1, (int)Math.Round(2 * scale));
        Color foreground = Enabled ? OreTheme.Text : OreTheme.DisabledText;
        Color fill = !Enabled ? OreTheme.LightPressed : Checked ? pressed ? OreTheme.GreenPressed : hover ? OreTheme.GreenHover : OreTheme.Green : pressed ? OreTheme.Edge : hover ? OreTheme.Muted : Color.FromArgb(140, 141, 144);
        if (Tile)
        {
            int size = Math.Max(12, (int)Math.Round(24 * scale)), x = px * 2, y = (Height - size) / 2;
            var box = new Rectangle(x, y, size, size);
            OreRenderer.Fill(e.Graphics, box, Enabled ? OreTheme.Line : OreTheme.Edge);
            OreRenderer.Rim(e.Graphics, Rectangle.Inflate(box, -px, -px), fill, OreRenderer.Highlight(fill), OreRenderer.Highlight(fill), px);
            // 逐格画勾，边缘对齐像素，不依赖字体或斜线抗锯齿。
            if (Checked)
            {
                string[] tick =
                {
                    "00000001",
                    "00000011",
                    "00000110",
                    "10001100",
                    "11011000",
                    "01110000",
                    "00100000",
                    "00000000"
                };
                for (int row = 0; row < tick.Length; row++)
                    for (int col = 0; col < tick[row].Length; col++)
                        if (tick[row][col] == '1')
                            OreRenderer.Fill(e.Graphics, Rectangle.FromLTRB(x + (int)Math.Round((4 + col * 2) * scale), y + (int)Math.Round((4 + row * 2) * scale), x + (int)Math.Round((6 + col * 2) * scale), y + (int)Math.Round((6 + row * 2) * scale)), foreground);
            }

            PixelText.DrawCjkOrBody(e.Graphics, Text, new Rectangle(x + size + px * 4, 0, Math.Max(1, Width - x - size - px * 6), Height), foreground, scale, false);
            if (Focused && ShowFocusCues)
                OreRenderer.Focus(e.Graphics, Rectangle.Inflate(box, px * 2, px * 2), px);
        }
        else
        {
            int width = Math.Min(Width, (int)Math.Round(56 * scale)), height = Math.Min(Height, (int)Math.Round(30 * scale));
            int thumb = Math.Min(height, (int)Math.Round(30 * scale)), y = (Height - height) / 2;
            var track = new Rectangle(Checked ? 0 : thumb - px * 2, y + px * 2, width - thumb + px * 2, height - px * 2);
            OreRenderer.Fill(e.Graphics, track, Enabled ? OreTheme.Line : OreTheme.Edge);
            OreRenderer.Rim(e.Graphics, Rectangle.Inflate(track, -px, -px), fill, OreRenderer.Highlight(fill), OreRenderer.Highlight(fill), px);
            int markX = track.X + track.Width / 2, markY = track.Y + track.Height / 2;
            if (Checked)
                OreRenderer.Fill(e.Graphics, new Rectangle(markX, markY - px * 2, px, px * 4), foreground);
            else
                OreRenderer.Border(e.Graphics, new Rectangle(markX - px * 2, markY - px * 2, px * 4, px * 4), foreground, px);
            Color face = !Enabled ? OreTheme.LightPressed : hover ? OreTheme.LightHover : OreTheme.Light;
            OreRenderer.Raised(e.Graphics, new Rectangle(Checked ? width - thumb : 0, y, thumb, thumb), face, OreRenderer.Highlight(face, 40), Enabled ? OreTheme.Edge : OreTheme.Muted, px, false);
            if (Text.Length > 0)
                PixelText.DrawCjkOrBody(e.Graphics, Text, new Rectangle(width + px * 6, 0, Math.Max(1, Width - width - px * 8), Height), foreground, scale, false);
            if (Focused && ShowFocusCues)
                OreRenderer.Focus(e.Graphics, new Rectangle(0, y, width, height), px);
        }
    }
}

// 无文字的功能开关；名称明确对应 Minecraft Settings 的 Switch。
sealed class OreSwitch : OreToggle
{
}

// 槽位/条件选择使用方形 Checkbox，事件接口不变。
sealed class OreCheckbox : OreToggle
{
    // 槽位与保护条件使用方形选择框，沿用 OreToggle 的事件和输入语义。
    public OreCheckbox()
    {
        Tile = true;
    }
}

// 预留互斥选项控件，与现有 RadioButton 分组及 CheckedChanged 兼容。
sealed class OreRadio : RadioButton
{
    bool hover;
    // 互斥选择只自绘外观，分组与 Checked 更新由 WinForms 管理。
    public OreRadio()
    {
        AutoSize = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        CheckedChanged += delegate
        {
            Invalidate();
        };
        MouseEnter += delegate
        {
            hover = true;
            Invalidate();
        };
        MouseLeave += delegate
        {
            hover = false;
            Invalidate();
        };
    }

    // 同一色彩和像素边缘，Radio 保留圆点语义。
    protected override void OnPaint(PaintEventArgs e)
    {
        OreRenderer.Fill(e.Graphics, ClientRectangle, Parent == null ? OreTheme.Background : Parent.BackColor);
        int size = Math.Min(24, Height - 4), y = (Height - size) / 2;
        using (var pen = new Pen(Enabled ? hover ? OreTheme.Text : OreTheme.Muted : OreTheme.Edge, 2))
            e.Graphics.DrawEllipse(pen, 2, y, size, size);
        if (Checked)
            using (var brush = new SolidBrush(Enabled ? OreTheme.GreenHover : OreTheme.Edge))
                e.Graphics.FillEllipse(brush, 7, y + 5, size - 10, size - 10);
        PixelText.DrawCjkOrBody(e.Graphics, Text, new Rectangle(size + 16, 0, Width - size - 20, Height), Enabled ? OreTheme.Text : OreTheme.Muted, 1, false);
        if (Focused && ShowFocusCues)
            OreRenderer.Focus(e.Graphics, ClientRectangle, 1);
    }
}

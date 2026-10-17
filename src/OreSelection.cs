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

    // 开关 44×24 胶囊、复选框 20×20 圆角方块；尺寸按窗口 DPI 缩放，不被所在行高度影响。
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Color back = Parent == null ? OreTheme.Background : Parent.BackColor;
        OreRenderer.Fill(g, ClientRectangle, back);
        float scale = OreMetrics.Scale(this);
        int px = Math.Max(1, (int)Math.Round(2 * scale)), line = Math.Max(1, px / 2);
        Color foreground = Enabled ? OreTheme.Text : OreTheme.DisabledText;
        Color on = pressed ? OreTheme.GreenPressed : hover ? OreTheme.GreenHover : OreTheme.Green;
        if (Tile)
        {
            int size = Math.Max(12, (int)Math.Round(20 * scale)), x = px * 2, y = (Height - size) / 2, radius = Math.Max(2, (int)Math.Round(5 * scale));
            var box = new Rectangle(x, y, size, size);
            if (!Enabled)
                OreRenderer.Plate(g, box, Checked ? OreTheme.Edge : OreTheme.LightPressed, OreTheme.Line, radius, line);
            else if (Checked)
                OreRenderer.FillRound(g, box, on, radius);
            else
                OreRenderer.Plate(g, box, pressed ? OreTheme.Field : OreTheme.Background, hover ? OreRenderer.Mix(OreTheme.Edge, OreTheme.Text, 30) : OreRenderer.Mix(OreTheme.Edge, OreTheme.Text, 12), radius, Math.Max(1, (int)Math.Round(1.5f * scale)));
            // 抗锯齿对勾，按方块尺寸比例取点。
            if (Checked)
            {
                var state = g.Save();
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var pen = new Pen(Enabled ? Color.White : OreTheme.Muted, Math.Max(1.5f, size / 9f)))
                {
                    pen.StartCap = pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
                    g.DrawLines(pen, new[]
                    {
                        new PointF(x + size * .26f, y + size * .52f),
                        new PointF(x + size * .43f, y + size * .69f),
                        new PointF(x + size * .75f, y + size * .33f)
                    });
                }

                g.Restore(state);
            }

            PixelText.DrawCjkOrBody(g, Text, new Rectangle(x + size + px * 4, 0, Math.Max(1, Width - x - size - px * 6), Height), foreground, scale, false);
            if (Focused && ShowFocusCues)
                OreRenderer.Focus(g, Rectangle.Inflate(box, px * 2, px * 2), px);
        }
        else
        {
            int width = Math.Min(Width, (int)Math.Round(44 * scale)), height = Math.Min(Height, (int)Math.Round(24 * scale));
            // 无文字开关贴右对齐，与同一行右侧的数字框、下拉框边缘保持一致。
            int left = Text.Length == 0 ? Width - width : 0, y = (Height - height) / 2;
            var track = new Rectangle(left, y, width, height);
            if (!Enabled)
                OreRenderer.Plate(g, track, OreTheme.LightPressed, OreTheme.Line, height / 2, line);
            else if (Checked)
                OreRenderer.FillRound(g, track, on, height / 2);
            else
                OreRenderer.Plate(g, track, pressed ? OreTheme.Field : OreTheme.Background, hover ? OreRenderer.Mix(OreTheme.Edge, OreTheme.Text, 30) : OreRenderer.Mix(OreTheme.Edge, OreTheme.Text, 12), height / 2, Math.Max(1, (int)Math.Round(1.5f * scale)));
            // 圆形拇指：开启时白色靠右，关闭时灰色靠左。
            int gap = Math.Max(2, (int)Math.Round(3 * scale)), knob = height - gap * 2;
            var thumb = new Rectangle(Checked ? track.Right - gap - knob : track.X + gap, y + gap, knob, knob);
            if (Enabled && Checked)
                OreRenderer.FillRound(g, new Rectangle(thumb.X, thumb.Y + line, thumb.Width, thumb.Height), Color.FromArgb(60, Color.Black), knob);
            Color face = !Enabled ? OreTheme.Edge : Checked ? Color.White : hover ? OreTheme.Text : OreTheme.Muted;
            OreRenderer.FillRound(g, thumb, face, knob);
            if (Text.Length > 0)
                PixelText.DrawCjkOrBody(g, Text, new Rectangle(width + px * 6, 0, Math.Max(1, Width - width - px * 8), Height), foreground, scale, false);
            if (Focused && ShowFocusCues)
                OreRenderer.Focus(g, Rectangle.FromLTRB(Math.Max(0, track.X - px), track.Y - px, Math.Min(Width, track.Right + px), track.Bottom + px), px);
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

    // 与复选框同一色板，Radio 保留圆点语义。
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        OreRenderer.Fill(g, ClientRectangle, Parent == null ? OreTheme.Background : Parent.BackColor);
        float scale = OreMetrics.Scale(this);
        int size = Math.Max(12, Math.Min((int)Math.Round(20 * scale), Height - 4)), y = (Height - size) / 2, x = 2;
        var ring = new Rectangle(x, y, size, size);
        if (Checked && Enabled)
        {
            OreRenderer.FillRound(g, ring, hover ? OreTheme.GreenHover : OreTheme.Green, size);
            int dot = Math.Max(4, size * 2 / 5);
            OreRenderer.FillRound(g, new Rectangle(x + (size - dot) / 2, y + (size - dot) / 2, dot, dot), Color.White, dot);
        }
        else
            OreRenderer.Plate(g, ring, Enabled ? OreTheme.Background : OreTheme.LightPressed, Enabled ? hover ? OreRenderer.Mix(OreTheme.Edge, OreTheme.Text, 30) : OreRenderer.Mix(OreTheme.Edge, OreTheme.Text, 12) : OreTheme.Line, size, Math.Max(1, (int)Math.Round(1.5f * scale)));
        PixelText.DrawCjkOrBody(g, Text, new Rectangle(size + (int)Math.Round(12 * scale), 0, Math.Max(1, Width - size - (int)Math.Round(16 * scale)), Height), Enabled ? OreTheme.Text : OreTheme.Muted, scale, false);
        if (Focused && ShowFocusCues)
            OreRenderer.Focus(g, Rectangle.Inflate(ring, 3, 3), 1);
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
using System;
using System.Drawing;
using System.Windows.Forms;

// 视觉类型与业务用途分离；历史 Primary / Destructive 字段仍兼容原有调用。
enum OreButtonKind
{
    Primary,
    Secondary,
    Neutral,
    Danger
}

// 使用 WinForms 按钮的点击、快捷键与可访问性语义，完整替换默认绘制。
class OreButton : Button
{
    // 历史标志保留调用兼容；Primary/Destructive 优先于 Kind，不能借绘制标志改变动作路由。
    public bool Primary, Destructive, Navigation, Selected, KeyBinding, FixedTypography;
    // 标题栏等低调按钮：无底色，悬停时才显示；Destructive 悬停为红色。
    public bool Ghost;
    public OreButtonKind Kind = OreButtonKind.Secondary;
    public float PixelScale = 1.15f;
    // 仅主侧栏设置图标编号；下拉菜单不占图标列。
    public int IconIndex = -1;
    public Image ContentIcon;
    bool hover, pressed;
    // 只绑定视觉状态；业务 Click 由原有页面构造器绑定一次。
    public OreButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
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
        MouseDown += delegate (object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                pressed = true;
            Invalidate();
        };
        MouseUp += delegate
        {
            pressed = false;
            Invalidate();
        };
        EnabledChanged += delegate
        {
            pressed = false;
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
    }

    // 空格与 Enter 同样下沉；实际点击继续由 Button 处理。
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
        {
            pressed = true;
            Invalidate();
        }

        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        pressed = false;
        Invalidate();
        base.OnKeyUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        float scale = OreMetrics.Scale(this);
        int px = OreMetrics.Pixel(this, 2, OreMetrics.ControlHeight), line = Math.Max(1, px / 2), radius = px * 3;
        var kind = Destructive ? OreButtonKind.Danger : Primary ? OreButtonKind.Primary : KeyBinding ? OreButtonKind.Neutral : Kind;
        Color back = Parent == null ? OreTheme.Card : Parent.BackColor;
        Color foreground = Enabled ? OreTheme.Text : OreTheme.DisabledText, iconColor = foreground;
        var face = ClientRectangle;
        OreRenderer.Fill(g, ClientRectangle, back);
        if (Navigation)
        {
            // 导航/菜单项：默认透明，悬停浅色底，选中为绿色调底 + 左侧强调条。
            foreground = !Enabled ? OreTheme.DisabledText : Selected || hover ? OreTheme.Text : OreTheme.Soft;
            iconColor = !Enabled ? OreTheme.DisabledText : Selected ? OreTheme.Accent : hover ? OreTheme.Text : OreTheme.Muted;
            if (Enabled && Selected)
                OreRenderer.FillRound(g, face, OreRenderer.Mix(back, OreTheme.Green, hover ? 30 : 22), radius);
            else if (Enabled && (hover || pressed))
                OreRenderer.FillRound(g, face, OreRenderer.Mix(back, OreTheme.Text, pressed ? 4 : 7), radius);
            if (Enabled && Selected)
            {
                int bar = Math.Max(2, (int)Math.Round(3 * scale)), barHeight = Math.Max(px * 4, Height / 2);
                OreRenderer.FillRound(g, new Rectangle(0, (Height - barHeight) / 2, bar, barHeight), OreTheme.Accent, bar);
            }
        }
        else if (Ghost)
        {
            // 标题栏按钮：平时只显示图标，悬停才出现底色；关闭键悬停转红。
            foreground = !Enabled ? OreTheme.DisabledText : hover || pressed ? OreTheme.Text : OreTheme.Soft;
            if (Enabled && (hover || pressed))
            {
                Color fill = kind == OreButtonKind.Danger ? (pressed ? OreTheme.DangerPressed : OreTheme.Danger) : OreRenderer.Mix(back, OreTheme.Text, pressed ? 6 : 10);
                OreRenderer.FillRound(g, face, fill, radius);
            }
        }
        else if (kind == OreButtonKind.Neutral)
        {
            // 键位按钮画成键帽：底部 2px 侧边，按下时键面下沉。
            int depth = Math.Max(1, px);
            Color fill = !Enabled ? OreTheme.LightPressed : pressed ? OreRenderer.Mix(OreTheme.Key, Color.Black, 10) : hover ? OreRenderer.Mix(OreTheme.Key, OreTheme.Text, 6) : OreTheme.Key;
            Color side = Enabled && hover ? OreRenderer.Mix(OreTheme.Edge, OreTheme.Text, 12) : OreTheme.Edge;
            OreRenderer.FillRound(g, face, side, radius);
            face = new Rectangle(line, line + (pressed ? depth : 0), Width - line * 2, Height - line * 2 - depth);
            OreRenderer.FillRoundGradient(g, face, pressed ? fill : OreRenderer.Mix(fill, OreTheme.Text, 4), fill, radius - line);
            foreground = Enabled ? OreTheme.Text : OreTheme.DisabledText;
        }
        else
        {
            Color normal = kind == OreButtonKind.Primary ? OreTheme.Green : kind == OreButtonKind.Danger ? OreTheme.Danger : OreTheme.Surface;
            Color over = kind == OreButtonKind.Primary ? OreTheme.GreenHover : kind == OreButtonKind.Danger ? OreTheme.DangerHover : OreRenderer.Mix(OreTheme.Surface, OreTheme.Text, 7);
            Color down = kind == OreButtonKind.Primary ? OreTheme.GreenPressed : kind == OreButtonKind.Danger ? OreTheme.DangerPressed : OreRenderer.Mix(OreTheme.Surface, Color.Black, 18);
            Color border = kind == OreButtonKind.Primary ? OreTheme.GreenShadow : kind == OreButtonKind.Danger ? OreTheme.DangerShadow : hover && Enabled ? OreRenderer.Mix(OreTheme.Edge, OreTheme.Text, 12) : OreTheme.Edge;
            Color fill = Enabled ? (pressed ? down : hover ? over : normal) : OreTheme.LightPressed;
            if (!Enabled)
                border = OreTheme.Line;
            OreRenderer.FillRound(g, face, border, radius);
            var inner = Rectangle.Inflate(face, -line, -line);
            OreRenderer.FillRoundGradient(g, inner, Enabled && !pressed ? OreRenderer.Highlight(fill, kind == OreButtonKind.Secondary ? 5 : 9) : fill, fill, radius - line);
            // 顶部 1px 内高光，按下或禁用时省略。
            if (Enabled && !pressed)
                using (var sheen = new SolidBrush(Color.FromArgb(kind == OreButtonKind.Secondary ? 14 : 34, Color.White)))
                    g.FillRectangle(sheen, new Rectangle(inner.X + radius, inner.Y, Math.Max(0, inner.Width - radius * 2), line));
        }

        int inset = Navigation ? px * 7 : px * 4;
        var bounds = new Rectangle(inset, face.Y, Width - inset * 2, face.Height);
        if (Navigation && IconIndex >= 0)
        {
            // 图标保持 10 格整像素网格，避免缩放后格子粗细不均。
            DrawNavigationIcon(g, new Rectangle(px * 7, (Height - px * 10) / 2, px * 10, px * 10), iconColor);
            bounds.X += px * 15;
            bounds.Width -= px * 15;
        }

        if (ContentIcon != null)
        {
            int size = Math.Max(1, Math.Min(Height - px * 6, px * 14));
            var state = g.Save();
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            g.DrawImage(ContentIcon, new Rectangle(bounds.Left, (Height - size) / 2, size, size));
            g.Restore(state);
            bounds.X += size + px * 3; bounds.Width -= size + px * 3;
        }
        if (FixedTypography)
            PixelText.DrawMenu(g, Text, bounds);
        else if (Navigation)
            PixelText.DrawCjkOrBody(g, Text, bounds, foreground, PixelScale, false);
        else
            PixelText.Draw(g, Text, bounds, foreground, PixelScale, true, false, false);
        if (Focused && ShowFocusCues)
            OreRenderer.Focus(g, ClientRectangle, px);
    }

    // 原创 10×10 方块图标：首页、恢复、交互、战斗、装备、设置；不依赖外部图片。
    // 按 1/10 网格逐格填充，保留像素风格，与圆角界面形成游戏工具的识别度。
    void DrawNavigationIcon(Graphics graphics, Rectangle bounds, Color color)
    {
        string[] shapes =
        {
            "0001100000/0011110000/0111111000/1111111100/0010010000/0010010000/0011110000/0011110000/0000000000/0000000000",
            "0110011000/1111111100/1111111100/0111111000/0011110000/0001100000/0000000000/0000000000/0000000000/0000000000",
            "0000000000/0111111100/0100000100/0101100100/0111111100/0101100100/0100000100/0111111100/0000000000/0000000000",
            "0000001100/0000011100/0000111000/0001110000/0011100000/0111000000/0011000000/0100100000/1000010000/0000000000",
            "0010010000/0111111000/1111111100/0111111000/0011110000/0011110000/0011110000/0011110000/0000000000/0000000000",
            "0001111000/0010000100/0100000010/1100000010/1110000010/0000000111/0100000011/0100000010/0010000100/0001111000",
            "0001100000/0111111000/0111111000/1110011100/1110011100/0111111000/0111111000/0001100000/0000000000/0000000000"
        };
        if (IconIndex >= shapes.Length)
            return;
        string[] rows = shapes[IconIndex].Split('/');
        for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
                if (rows[y][x] == '1')
                    OreRenderer.Fill(graphics, Rectangle.FromLTRB(bounds.X + x * bounds.Width / 10, bounds.Y + y * bounds.Height / 10, bounds.X + (x + 1) * bounds.Width / 10, bounds.Y + (y + 1) * bounds.Height / 10), color);
    }
}

// 主导航和下拉列表共享选中、悬停和焦点行为，使用相同的圆角底和强调条。
sealed class OreNavItem : OreButton
{
    // 导航项共享按钮状态，只设置视觉类型；页面切换仍由原 Click 绑定处理。
    public OreNavItem()
    {
        Navigation = true;
        Kind = OreButtonKind.Neutral;
    }
}

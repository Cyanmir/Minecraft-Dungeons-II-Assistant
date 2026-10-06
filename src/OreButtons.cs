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
    public OreButtonKind Kind = OreButtonKind.Secondary;
    public float PixelScale = 1.15f;
    // 仅主侧栏设置图标编号；下拉菜单不占图标列。
    public int IconIndex = -1;
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

    // 释放键盘按下态。
    protected override void OnKeyUp(KeyEventArgs e)
    {
        pressed = false;
        Invalidate();
        base.OnKeyUp(e);
    }

    // 渲染 OreUI 四种按钮和导航；不参与业务调度。
    protected override void OnPaint(PaintEventArgs e)
    {
        int px = OreMetrics.Pixel(this, 2, OreMetrics.ControlHeight);
        var kind = Destructive ? OreButtonKind.Danger : Primary ? OreButtonKind.Primary : KeyBinding ? OreButtonKind.Neutral : Kind;
        Color normal = kind == OreButtonKind.Primary ? OreTheme.Green : kind == OreButtonKind.Danger ? OreTheme.Danger : kind == OreButtonKind.Neutral ? OreTheme.Surface : OreTheme.Light;
        Color over = kind == OreButtonKind.Primary ? OreTheme.GreenHover : kind == OreButtonKind.Danger ? OreTheme.DangerHover : kind == OreButtonKind.Neutral ? OreTheme.Edge : OreTheme.LightHover;
        Color down = kind == OreButtonKind.Primary ? OreTheme.GreenPressed : kind == OreButtonKind.Danger ? OreTheme.DangerPressed : kind == OreButtonKind.Neutral ? OreTheme.Background : OreTheme.LightPressed;
        Color shadow = kind == OreButtonKind.Primary ? OreTheme.GreenShadow : kind == OreButtonKind.Danger ? OreTheme.DangerShadow : kind == OreButtonKind.Neutral ? OreTheme.Background : OreTheme.Edge;
        Color color = Enabled ? (pressed ? down : hover ? over : normal) : OreTheme.LightPressed;
        Color foreground = !Enabled ? OreTheme.DisabledText : kind == OreButtonKind.Secondary ? OreTheme.Field : OreTheme.Text;
        if (Navigation)
        {
            color = !Enabled ? OreTheme.LightPressed : Selected ? (hover ? OreTheme.GreenHover : OreTheme.Green) : pressed ? OreTheme.Background : hover ? OreTheme.Edge : OreTheme.Surface;
            foreground = Enabled ? OreTheme.Text : OreTheme.DisabledText;
            shadow = Selected ? OreTheme.GreenShadow : OreTheme.Background;
        }

        OreRenderer.Fill(e.Graphics, ClientRectangle, Parent == null ? OreTheme.Card : Parent.BackColor);
        OreRenderer.Raised(e.Graphics, ClientRectangle, color, Enabled ? OreRenderer.Highlight(color, kind == OreButtonKind.Secondary && !Navigation ? 40 : kind == OreButtonKind.Danger ? 10 : 20) : color, shadow, px, pressed);
        if (Navigation && Selected)
            OreRenderer.Fill(e.Graphics, new Rectangle(px, px * 2, px * 2, Height - px * 6), OreTheme.Text);
        int inset = Navigation ? px * 8 : px * 4;
        var bounds = new Rectangle(inset, pressed ? px * 2 : 0, Width - inset * 2, Height - px * 3);
        if (Navigation && IconIndex >= 0)
        {
            DrawNavigationIcon(e.Graphics, new Rectangle(px * 8, (Height - px * 10) / 2 - px, px * 10, px * 10), foreground);
            bounds.X += px * 15;
            bounds.Width -= px * 15;
        }

        if (FixedTypography)
            PixelText.DrawMenu(e.Graphics, Text, bounds);
        else if (Navigation)
            PixelText.DrawCjkOrBody(e.Graphics, Text, bounds, foreground, PixelScale, false);
        else
            PixelText.Draw(e.Graphics, Text, bounds, foreground, PixelScale, true, false, false);
        if (Focused && ShowFocusCues)
            OreRenderer.Focus(e.Graphics, ClientRectangle, px);
    }

    // 原创 10×10 方块图标：首页、恢复、交互、战斗、装备、设置；不依赖外部图片。
    void DrawNavigationIcon(Graphics graphics, Rectangle bounds, Color color)
    {
        string[] shapes =
        {
            "0001100000/0011110000/0111111000/1111111100/0010010000/0010010000/0011110000/0011110000/0000000000/0000000000",
            "0110011000/1111111100/1111111100/0111111000/0011110000/0001100000/0000000000/0000000000/0000000000/0000000000",
            "0000000000/0111111100/0100000100/0101100100/0111111100/0101100100/0100000100/0111111100/0000000000/0000000000",
            "0000001100/0000011100/0000111000/0001110000/0011100000/0111000000/0011000000/0100100000/1000010000/0000000000",
            "0010010000/0111111000/1111111100/0111111000/0011110000/0011110000/0011110000/0011110000/0000000000/0000000000",
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

// 主导航和下拉列表共享选中、悬停和焦点行为，使用相同的凸面和像素边缘。
sealed class OreNavItem : OreButton
{
    // 导航项共享按钮状态，只设置视觉类型；页面切换仍由原 Click 绑定处理。
    public OreNavItem()
    {
        Navigation = true;
        Kind = OreButtonKind.Neutral;
    }
}

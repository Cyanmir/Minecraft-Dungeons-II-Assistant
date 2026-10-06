// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
using System;
using System.Drawing;

// 只负责像素矩形、高光、阴影和焦点；禁止在绘制中修改配置或发送游戏请求。
static class OreRenderer
{
    // 小窗口或缩放后出现空矩形时直接跳过，避免 GDI 异常。
    public static void Fill(Graphics graphics, Rectangle bounds, Color color)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        using (var brush = new SolidBrush(color))
            graphics.FillRectangle(brush, bounds);
    }

    // 用内侧矩形构成边缘，不让 Pen 的半像素线越出控件。
    public static void Border(Graphics graphics, Rectangle bounds, Color color, int width)
    {
        Fill(graphics, new Rectangle(bounds.Left, bounds.Top, bounds.Width, width), color);
        Fill(graphics, new Rectangle(bounds.Left, bounds.Bottom - width, bounds.Width, width), color);
        Fill(graphics, new Rectangle(bounds.Left, bounds.Top, width, bounds.Height), color);
        Fill(graphics, new Rectangle(bounds.Right - width, bounds.Top, width, bounds.Height), color);
    }

    // 面板四边高光/暗边共用同一像素网格，避免只有顶线的扁平外观。
    public static void Rim(Graphics graphics, Rectangle bounds, Color fill, Color highlight, Color shade, int pixel)
    {
        Fill(graphics, bounds, fill);
        Fill(graphics, new Rectangle(bounds.X, bounds.Y, bounds.Width, pixel), highlight);
        Fill(graphics, new Rectangle(bounds.X, bounds.Y, pixel, bounds.Height - pixel), highlight);
        Fill(graphics, new Rectangle(bounds.X, bounds.Bottom - pixel, bounds.Width, pixel), shade);
        Fill(graphics, new Rectangle(bounds.Right - pixel, bounds.Y + pixel, pixel, bounds.Height - pixel), shade);
    }

    // 对应参考按钮的 2px 黑框、2px 高光与 4px 底部投影；按下时面向下移动 4px。
    public static void Raised(Graphics graphics, Rectangle bounds, Color fill, Color highlight, Color shadow, int pixel, bool pressed)
    {
        if (pressed)
        {
            bounds.Y += pixel * 2;
            bounds.Height -= pixel * 2;
        }

        Fill(graphics, bounds, OreTheme.Line);
        var inside = Rectangle.Inflate(bounds, -pixel, -pixel);
        Fill(graphics, inside, shadow);
        var face = inside;
        if (!pressed)
            face.Height -= pixel * 2;
        Rim(graphics, face, fill, highlight, highlight, pixel);
    }

    // 输入框是灰色凹面，顶部有 4px 内阴影；焦点与边界保持清楚。
    public static void Field(Graphics graphics, Rectangle bounds, int pixel, bool hover, bool focus, bool enabled)
    {
        Fill(graphics, bounds, enabled ? OreTheme.Line : OreTheme.Edge);
        var inside = Rectangle.Inflate(bounds, -pixel, -pixel);
        Fill(graphics, inside, OreTheme.Card);
        inside.Y += pixel * 2;
        inside.Height -= pixel * 2;
        Fill(graphics, inside, enabled ? (hover ? OreTheme.Surface : OreTheme.Background) : OreTheme.LightPressed);
        if (focus && enabled)
            Focus(graphics, bounds, pixel);
    }

    // 焦点环独立于选中态，键盘导航始终可见。
    public static void Focus(Graphics graphics, Rectangle bounds, int pixel)
    {
        Border(graphics, Rectangle.Inflate(bounds, -pixel, -pixel), OreTheme.Text, pixel);
    }

    // 普通表面为 20% 白色高光；浅灰按钮用 40%，红色按钮用 10%。
    public static Color Highlight(Color color)
    {
        return Highlight(color, 20);
    }

    // 显式百分比用于不同按钮色板，高光计算不引入新的状态。
    public static Color Highlight(Color color, int percent)
    {
        return Color.FromArgb(color.R + (255 - color.R) * percent / 100, color.G + (255 - color.G) * percent / 100, color.B + (255 - color.B) * percent / 100);
    }
}

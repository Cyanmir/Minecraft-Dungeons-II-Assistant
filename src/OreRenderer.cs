// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

// 只负责矩形、圆角面、描边和焦点；禁止在绘制中修改配置或发送游戏请求。
// 圆角统一抗锯齿并按像素中心对齐，调用方先用父容器底色铺满控件以免角落残留。
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

    // 圆角路径；半径超过短边一半时退化为胶囊形。
    public static GraphicsPath RoundPath(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter < 1)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    // 抗锯齿圆角填充；PixelOffsetMode.Half 让整数矩形恰好覆盖对应像素。
    public static void FillRound(Graphics graphics, Rectangle bounds, Color color, int radius)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        using (var path = RoundPath(bounds, radius))
        using (var brush = new SolidBrush(color))
            graphics.FillPath(brush, path);
        graphics.Restore(state);
    }

    // 自上而下的轻微渐变，给按钮一点体积感而不出现硬边高光。
    public static void FillRoundGradient(Graphics graphics, Rectangle bounds, Color top, Color bottom, int radius)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        if (top == bottom)
        {
            FillRound(graphics, bounds, top, radius);
            return;
        }

        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        // 渐变矩形上下各放宽 1px，避开 GDI+ 在首行回绕出底色的已知问题。
        var area = new Rectangle(bounds.X, bounds.Y - 1, bounds.Width, bounds.Height + 2);
        using (var path = RoundPath(bounds, radius))
        using (var brush = new LinearGradientBrush(area, top, bottom, LinearGradientMode.Vertical))
            graphics.FillPath(brush, path);
        graphics.Restore(state);
    }

    // 沿内侧描边，线宽完全落在 bounds 内。
    public static void StrokeRound(Graphics graphics, Rectangle bounds, Color color, int radius, float width)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || width <= 0)
            return;
        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        var inner = new RectangleF(bounds.X + width / 2, bounds.Y + width / 2, bounds.Width - width, bounds.Height - width);
        using (var path = RoundPath(inner, Math.Max(0, radius - width / 2)))
        using (var pen = new Pen(color, width))
            graphics.DrawPath(pen, path);
        graphics.Restore(state);
    }

    // 带细描边的面：先铺描边色，再在内侧铺面色，圆角处不会出现缝隙。
    public static void Plate(Graphics graphics, Rectangle bounds, Color fill, Color border, int radius, int line)
    {
        FillRound(graphics, bounds, border, radius);
        FillRound(graphics, Rectangle.Inflate(bounds, -line, -line), fill, Math.Max(0, radius - line));
    }

    // 卡片与滚动条拇指的兼容入口：圆角面 + 描边，highlight 仅保留参数兼容。
    public static void Rim(Graphics graphics, Rectangle bounds, Color fill, Color highlight, Color shade, int pixel)
    {
        Plate(graphics, bounds, fill, shade, pixel * 3, Math.Max(1, pixel / 2));
    }

    // 兼容旧凸面调用：渐变面 + 深色细描边；按下时去掉渐变高光并略微变暗。
    public static void Raised(Graphics graphics, Rectangle bounds, Color fill, Color highlight, Color shadow, int pixel, bool pressed)
    {
        int line = Math.Max(1, pixel / 2), radius = pixel * 3;
        FillRound(graphics, bounds, shadow, radius);
        var face = Rectangle.Inflate(bounds, -line, -line);
        if (pressed)
            FillRound(graphics, face, Mix(fill, Color.Black, 8), radius - line);
        else
            FillRoundGradient(graphics, face, Mix(fill, highlight, 35), fill, radius - line);
    }

    // 输入框是比卡片更深的凹槽；悬停提亮描边，焦点改为强调色描边。
    public static void Field(Graphics graphics, Rectangle bounds, int pixel, bool hover, bool focus, bool enabled)
    {
        int line = Math.Max(1, pixel / 2), radius = pixel * 3;
        Color border = !enabled ? OreTheme.Line : focus ? OreTheme.Green : hover ? Mix(OreTheme.Edge, OreTheme.Text, 14) : OreTheme.Edge;
        Plate(graphics, bounds, enabled ? OreTheme.Background : OreTheme.LightPressed, border, radius, line);
        if (focus && enabled)
            StrokeRound(graphics, bounds, Color.FromArgb(70, OreTheme.Accent), radius, line * 2);
    }

    // 焦点环独立于选中态，键盘导航始终可见。
    public static void Focus(Graphics graphics, Rectangle bounds, int pixel)
    {
        StrokeRound(graphics, bounds, Color.FromArgb(200, OreTheme.Accent), pixel * 3, Math.Max(1, pixel));
    }

    // 普通表面为 20% 白色高光；浅灰按钮用 40%，红色按钮用 10%。
    public static Color Highlight(Color color)
    {
        return Highlight(color, 20);
    }

    // 显式百分比用于不同按钮色板，高光计算不引入新的状态。
    public static Color Highlight(Color color, int percent)
    {
        return Mix(color, Color.White, percent);
    }

    // 两色线性混合，percent 为 target 所占比例；用于悬停、选中底色等派生色。
    public static Color Mix(Color color, Color target, int percent)
    {
        percent = Math.Max(0, Math.Min(100, percent));
        return Color.FromArgb(color.R + (target.R - color.R) * percent / 100, color.G + (target.G - color.G) * percent / 100, color.B + (target.B - color.B) * percent / 100);
    }
}

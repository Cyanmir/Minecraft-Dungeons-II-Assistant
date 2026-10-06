// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
using System.Drawing;

// OreUI 语义色；颜色统一在此调整，业务控件不再各自定义青蓝色主题。
static class OreTheme
{
    public static readonly Color Background = ColorTranslator.FromHtml("#313233");
    public static readonly Color Card = ColorTranslator.FromHtml("#242425");
    public static readonly Color Field = ColorTranslator.FromHtml("#1E1E1F");
    public static readonly Color Line = Color.Black;
    public static readonly Color Green = ColorTranslator.FromHtml("#3C8527");
    public static readonly Color GreenHover = ColorTranslator.FromHtml("#52A535");
    public static readonly Color GreenPressed = ColorTranslator.FromHtml("#2A641C");
    public static readonly Color GreenShadow = ColorTranslator.FromHtml("#1D4D13");
    public static readonly Color Accent = ColorTranslator.FromHtml("#A0E081");
    public static readonly Color Text = Color.White;
    public static readonly Color Muted = ColorTranslator.FromHtml("#B1B2B5");
    public static readonly Color Edge = ColorTranslator.FromHtml("#58585A");
    public static readonly Color Surface = ColorTranslator.FromHtml("#48494A");
    public static readonly Color Light = ColorTranslator.FromHtml("#D0D1D4");
    public static readonly Color LightHover = ColorTranslator.FromHtml("#F4F6F9");
    public static readonly Color LightPressed = ColorTranslator.FromHtml("#B1B2B5");
    public static readonly Color DisabledText = ColorTranslator.FromHtml("#58585A");
    public static readonly Color Danger = ColorTranslator.FromHtml("#CA3636");
    public static readonly Color DangerHover = ColorTranslator.FromHtml("#DF5050");
    public static readonly Color DangerPressed = ColorTranslator.FromHtml("#C02D2D");
    public static readonly Color DangerShadow = ColorTranslator.FromHtml("#AD1D1D");
    // 兼容历史绘制调用；通用渲染实现集中到 OreRenderer。
    public static void Fill(Graphics graphics, Rectangle bounds, Color color)
    {
        OreRenderer.Fill(graphics, bounds, color);
    }

    // 旧绘制调用的兼容入口；边框仍由统一渲染器在控件内部绘制。
    public static void Border(Graphics graphics, Rectangle bounds, Color color, int width)
    {
        OreRenderer.Border(graphics, bounds, color, width);
    }

    // 历史凸面调用转到公共渲染器；新 DPI 控件应直接传入缩放后的像素边缘。
    public static void Bevel(Graphics graphics, Rectangle bounds, Color fill, Color highlight, Color shadow)
    {
        OreRenderer.Raised(graphics, bounds, fill, highlight, shadow, 2, false);
    }
}

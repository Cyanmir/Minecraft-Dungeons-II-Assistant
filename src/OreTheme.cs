// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
using System.Drawing;

// OreUI 语义色；颜色统一在此调整，业务控件不再各自定义主题。
// 深色石板底 + 绿宝石强调色：层级由浅到深为 Surface > Card > Background > Field。
static class OreTheme
{
    // 页面底色、卡片、输入槽与窗口外框。
    public static readonly Color Background = ColorTranslator.FromHtml("#101318");
    public static readonly Color Card = ColorTranslator.FromHtml("#171B22");
    public static readonly Color CardAlt = ColorTranslator.FromHtml("#1C212A");
    public static readonly Color Field = ColorTranslator.FromHtml("#0B0E12");
    public static readonly Color Line = ColorTranslator.FromHtml("#242A35");
    // 卡片描边比分隔线略暗，避免卡片轮廓抢过内容。
    public static readonly Color Outline = ColorTranslator.FromHtml("#212732");
    // 主色：绿宝石。Shadow 用作按钮描边与按下深色。
    public static readonly Color Green = ColorTranslator.FromHtml("#1E9E5E");
    public static readonly Color GreenHover = ColorTranslator.FromHtml("#25B26B");
    public static readonly Color GreenPressed = ColorTranslator.FromHtml("#188650");
    public static readonly Color GreenShadow = ColorTranslator.FromHtml("#0F5C37");
    public static readonly Color Accent = ColorTranslator.FromHtml("#5EDC9C");
    // 文字层级：正文、导航/次要、说明、禁用。
    public static readonly Color Text = ColorTranslator.FromHtml("#E8ECF2");
    public static readonly Color Soft = ColorTranslator.FromHtml("#B4BCC9");
    public static readonly Color Muted = ColorTranslator.FromHtml("#8A93A4");
    public static readonly Color DisabledText = ColorTranslator.FromHtml("#4C5464");
    // 中性控件：Edge 为描边/分隔，Surface 为次要按钮面，Key 为键帽面。
    public static readonly Color Edge = ColorTranslator.FromHtml("#2E3542");
    public static readonly Color Surface = ColorTranslator.FromHtml("#222833");
    public static readonly Color Key = ColorTranslator.FromHtml("#1D222B");
    // 浅色只用于滑块/开关的拇指；LightPressed 兼作禁用控件底色。
    public static readonly Color Light = ColorTranslator.FromHtml("#E3E8EF");
    public static readonly Color LightHover = Color.White;
    public static readonly Color LightPressed = ColorTranslator.FromHtml("#1A1E26");
    public static readonly Color Danger = ColorTranslator.FromHtml("#D2434B");
    public static readonly Color DangerHover = ColorTranslator.FromHtml("#E2565D");
    public static readonly Color DangerPressed = ColorTranslator.FromHtml("#B3373E");
    public static readonly Color DangerShadow = ColorTranslator.FromHtml("#782329");
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

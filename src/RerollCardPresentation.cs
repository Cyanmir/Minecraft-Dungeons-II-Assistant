// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 力量与附魔角标仅影响绘制。
using System;
using System.Drawing;

static class RerollCardPresentation
{
    // 原创 5×7 数字点阵，固定整数像素与空白列；小卡片不用粗标题字体缩放，避免字形黏连。
    static readonly string[] numberGlyphs = {
        "01110/10001/10001/10001/10001/10001/01110", "00100/01100/00100/00100/00100/00100/01110",
        "01110/10001/00001/00010/00100/01000/11111", "11110/00001/00001/01110/00001/00001/11110",
        "00010/00110/01010/10010/11111/00010/00010", "11111/10000/10000/11110/00001/00001/11110",
        "01110/10000/10000/11110/10001/10001/01110", "11111/00001/00010/00100/01000/01000/01000",
        "01110/10001/10001/01110/10001/10001/01110", "01110/10001/10001/01111/00001/00001/01110" };
    static readonly string[] narrowGlyphs = {
        "111/101/101/101/111", "010/110/010/010/111", "110/001/010/100/111", "110/001/010/001/110",
        "101/101/111/001/001", "111/100/110/001/110", "011/100/111/101/111", "111/001/010/010/010",
        "111/101/111/101/111", "111/101/111/001/110" };
    internal static string EnchantmentText(bool? enchanted)
    {
        return L10n.T(!enchanted.HasValue ? "附魔状态未读取" : enchanted.Value ? "已附魔" : "未附魔");
    }
    internal static void Draw(Graphics graphics, Rectangle bounds, string power, bool? enchanted, float scale)
    {
        // 数字统一用 Minecraft 字体，不让中文界面把数字换成细 CJK 字体。
        // 按实际字宽靠右；底部留足黑底与阴影空间，避免框边遮住力量数字。
        float size = Math.Min(scale, bounds.Width / 105f);
        int inset = Math.Max(3, bounds.Width / 14);
        if (!String.IsNullOrEmpty(power))
        {
            int width = MinecraftText.LineWidth(graphics, power, size, true);
            while (width > bounds.Width - inset * 2 && size > .3f)
            { size -= .05f; width = MinecraftText.LineWidth(graphics, power, size, true); }
            int height = MinecraftText.InkHeight(size, true) + 2;
            var text = new Rectangle(bounds.Right - inset - width - 2, bounds.Bottom - inset - height, width + 2, height);
            using (var brush = new SolidBrush(Color.FromArgb(235, 12, 12, 12))) graphics.FillRectangle(brush, Rectangle.Inflate(text, 2, 1));
            MinecraftText.Draw(graphics, power, text, Color.White, size, false, false, true, true);
        }
        if (enchanted != true) return;
        // 紫色像素旋纹表示附魔，未附魔无角标。此为程序绘制的状态符号，不冒充原版贴图。
        int unit = Math.Max(1, (int)Math.Round(bounds.Width / 24f));
        int x = bounds.X + inset, y = bounds.Bottom - inset - 7 * unit;
        string[] spiral = { "0111110", "1100001", "1001101", "1010011", "1011110", "1100000", "0111110" };
        using (var dark = new SolidBrush(Color.FromArgb(25, 10, 34))) graphics.FillRectangle(dark, x - unit, y - unit, 9 * unit, 9 * unit);
        using (var brush = new SolidBrush(Color.FromArgb(200, 111, 248)))
            for (int row = 0; row < spiral.Length; row++)
                for (int column = 0; column < spiral[row].Length; column++)
                    if (spiral[row][column] == '1') graphics.FillRectangle(brush, x + column * unit, y + row * unit, unit, unit);
    }
    // 八列小卡片保留原始力量精度。只使用整数像素，不做图形变换或字体位图压缩。
    internal static void DrawCompactPower(Graphics graphics, string power, Rectangle bounds, float scale)
    {
        // 同一网格统一采用 3×5 点阵和固定像素单元，以三位整数预留宽度。
        // 不能按每个值的字数放大：否则 87/93 会比 124 大一倍。小数超长时才缩小单元。
        int unit = Math.Max(1, Math.Min((int)Math.Round(2 * scale), Math.Min(bounds.Width / 11, bounds.Height / 5)));
        DrawNumber(graphics, power, bounds, OreTheme.Text, unit, true, true);
    }
    internal static void DrawNumber(Graphics graphics, string value, Rectangle bounds, Color color)
    {
        DrawNumber(graphics, value, bounds, color, 1, false, false);
    }
    static void DrawNumber(Graphics graphics, string value, Rectangle bounds, Color color, int preferredUnit, bool narrow, bool alignRight)
    {
        if (String.IsNullOrEmpty(value)) return;
        int width = NumberWidth(value, narrow), height = narrow ? 5 : 7;
        if (!narrow && (width > bounds.Width || height > bounds.Height))
        { narrow = true; width = NumberWidth(value, true); height = 5; }
        int unit = Math.Max(1, Math.Min(preferredUnit, Math.Min(bounds.Width / Math.Max(1, width), bounds.Height / height)));
        int x = bounds.X + Math.Max(0, (bounds.Width - width * unit) / (alignRight ? 1 : 2));
        int y = bounds.Y + Math.Max(0, (bounds.Height - height * unit) / 2);
        var state = graphics.Save();
        graphics.SetClip(bounds, System.Drawing.Drawing2D.CombineMode.Intersect);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
        using (var brush = new SolidBrush(color))
            foreach (char digit in value)
            {
                int glyphWidth = digit == '.' ? 1 : narrow ? 3 : 5;
                if (digit == '.') graphics.FillRectangle(brush, x, y + (height - 1) * unit, unit, unit);
                else if (digit >= '0' && digit <= '9')
                {
                    string[] rows = (narrow ? narrowGlyphs : numberGlyphs)[digit - '0'].Split('/');
                    for (int row = 0; row < rows.Length; row++)
                        for (int column = 0; column < rows[row].Length; column++)
                            if (rows[row][column] == '1') graphics.FillRectangle(brush, x + column * unit, y + row * unit, unit, unit);
                }
                x += (glyphWidth + (narrow ? 1 : 2)) * unit;
            }
        graphics.Restore(state);
    }
    static int NumberWidth(string value, bool narrow)
    {
        int width = 0, gap = narrow ? 1 : 2;
        foreach (char digit in value) width += (digit == '.' ? 1 : narrow ? 3 : 5) + gap;
        return Math.Max(0, width - gap);
    }
}

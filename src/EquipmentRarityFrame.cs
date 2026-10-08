// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 使用独立资源包的原始槽框、品质标记和风暴纹理绘制。
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

static class EquipmentRarityFrame
{
    // 与截图的游戏色系一致；Unreal 动态材质不在 WinForms 中运行，使用原始纹理着色。
    static Color ColorFor(EquipmentRarity rarity)
    {
        switch (rarity)
        {
            case EquipmentRarity.Common: return Color.FromArgb(185, 185, 185);
            case EquipmentRarity.Rare: return Color.FromArgb(93, 194, 78);
            case EquipmentRarity.Special: return Color.FromArgb(39, 174, 224);
            case EquipmentRarity.Unique: return Color.FromArgb(255, 162, 28);
            default: return Color.FromArgb(119, 119, 119);
        }
    }
    // 只给绿色遮罩着色；图标本身仍显示原始贴图，不改变武器颜色。
    static void Mask(Graphics graphics, Bitmap image, Rectangle bounds, Color color)
    {
        if (image == null) return;
        var matrix = new ColorMatrix(new[] {
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { color.R / 255f, color.G / 255f, color.B / 255f, 0, 0 },
            new float[] { 0, 0, 0, 0, 0 },
            new float[] { 0, 0, 0, 1, 0 },
            new float[] { 0, 0, 0, 0, 1 }
        });
        using (var attributes = new ImageAttributes())
        {
            attributes.SetColorMatrix(matrix);
            graphics.DrawImage(image, bounds, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
        }
    }
    // 单一绘制入口同时用于装备清单和铁匠候选预览；未读品质仅用灰框，不猜测颜色。
    internal static void Draw(Graphics graphics, EquipmentItem item, Rectangle bounds, bool reserveTopLeft = false)
    {
        var previous = graphics.InterpolationMode;
        var previousPixels = graphics.PixelOffsetMode;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        var color = ColorFor(item.Rarity);
        using (var fill = new SolidBrush(OreTheme.Field)) graphics.FillRectangle(fill, bounds);
        var background = EquipmentGamePresentation.Texture("slotBackground");
        if (background != null) graphics.DrawImage(background, bounds);
        var frame = item.Rarity == EquipmentRarity.Off ? null : EquipmentGamePresentation.Texture("rarity" + item.Rarity);
        if (frame != null)
        {
            // 原始材质遮罩包含实心内部，只绘制边缘，避免遮住装备图标；保留原图的角和高光。
            var saved = graphics.Save();
            using (var border = new Region(bounds))
            {
                int edge = Math.Max(2, bounds.Width / 16);
                border.Exclude(Rectangle.Inflate(bounds, -edge, -edge));
                graphics.SetClip(border, CombineMode.Intersect);
                Mask(graphics, frame, bounds, color);
            }
            graphics.Restore(saved);
        }
        else using (var pen = new Pen(color, Math.Max(1, bounds.Width / 32f)))
            graphics.DrawRectangle(pen, bounds.X + 1, bounds.Y + 1, bounds.Width - 3, bounds.Height - 3);
        // 图标内缩，保留原始槽框；低分辨率采用最近邻以保持像素边缘。
        var icon = EquipmentGamePresentation.Icon(item);
        int pad = Math.Max(3, bounds.Width / 10);
        var inner = Rectangle.Inflate(bounds, -pad, -pad);
        if (icon != null) graphics.DrawImage(icon, inner);
        int marker = Math.Max(6, bounds.Width / 5);
        // 紧凑刷词条网格将左上角留给复选框，品质角标移到右下角；其他页面保留原位置。
        if (item.Rarity != EquipmentRarity.Off)
            Mask(graphics, EquipmentGamePresentation.Texture(item.SoulStorm ? "soulMarkers" : "rarityMarkers"),
                reserveTopLeft ? new Rectangle(bounds.Right - marker - 2, bounds.Bottom - marker - 2, marker, marker) :
                    new Rectangle(bounds.X + 3, bounds.Y + 3, marker, marker), color);
        if (item.SoulStorm)
        {
            // 风暴是独立状态，不能把风暴图标当作品质或直接推算三槽。
            var pip = EquipmentGamePresentation.Texture("stormPip");
            if (pip != null) graphics.DrawImage(pip, new Rectangle(bounds.Right - marker, bounds.Y - 2, marker, marker));
        }
        graphics.InterpolationMode = previous;
        graphics.PixelOffsetMode = previousPixels;
    }
}

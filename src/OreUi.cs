// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 保留 Minecraft / CJK 字体加载、测量与标签绘制。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;
using System.Drawing.Text;
using System.Runtime.InteropServices;

static class MinecraftText
{
    static readonly PrivateFontCollection headingFonts = new PrivateFontCollection(), bodyFonts = new PrivateFontCollection();
    static readonly List<IntPtr> fontMemory = new List<IntPtr>();
    static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
    static readonly Dictionary<string, Rectangle> ink = new Dictionary<string, Rectangle>();
    static readonly Dictionary<string, float> advances = new Dictionary<string, float>();
    static readonly FontFamily heading = Load(headingFonts, "minecraft-ten.ttf"), body = Load(bodyFonts, "minecraft-body.ttf");
    static FontFamily Load(PrivateFontCollection collection, string resource)
    {
        using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
        {
            if (stream == null)
                throw new Exception("Missing embedded font: " + resource);
            byte[] bytes = new byte[stream.Length];
            int offset = 0;
            while (offset < bytes.Length)
            {
                int count = stream.Read(bytes, offset, bytes.Length - offset);
                if (count == 0)
                    throw new EndOfStreamException();
                offset += count;
            }

            IntPtr memory = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, memory, bytes.Length);
            fontMemory.Add(memory);
            collection.AddMemoryFont(memory, bytes.Length);
            return collection.Families[0];
        }
    }

    // HeadingName 的只读/受控访问入口；使用该属性而不绕过访问器中的校验和更新逻辑。
    public static string HeadingName
    {
        get
        {
            return heading.Name;
        }
    }

    // BodyName 的只读/受控访问入口；使用该属性而不绕过访问器中的校验和更新逻辑。
    public static string BodyName
    {
        get
        {
            return body.Name;
        }
    }

    // 计算当前字体的像素高度，布局变更应复用测量结果。
    public static float Height(float scale)
    {
        return InkFor(FontFor(false, scale)).Height;
    }

    // 规范显示文本/换行，不修改配置键或协议字段。
    static string Normalize(string text)
    {
        return text.Replace("→", "->").Replace("·", "/").Replace("−", "-").Replace("’", "'").Replace("“", "\"").Replace("”", "\"");
    }

    // 检查字形是否可绘制，必要时交给回退字体。
    public static bool CanDraw(string text)
    {
        return Normalize(text).All(c => c <= 255 || c == '\n' || c == '\r');
    }

    // 创建文本布局格式，调用者负责正确释放。
    static StringFormat Format()
    {
        var format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
        return format;
    }

    // 按语言、字符和字重选择已加载字体。
    static Font FontFor(bool title, float scale)
    {
        string key = (title ? "h" : "b") + Math.Max(12, (int)Math.Round((title ? 22 : 16) * scale));
        Font font;
        if (!fonts.TryGetValue(key, out font))
        {
            font = new Font(title ? heading : body, Math.Max(12, (int)Math.Round((title ? 22 : 16) * scale)), FontStyle.Regular, GraphicsUnit.Pixel);
            fonts[key] = font;
        }

        return font;
    }

    // 测量字形实际墨迹范围，避免裁切标题。
    static Rectangle InkFor(Font font)
    {
        string key = font.FontFamily.Name + font.Size;
        Rectangle result;
        if (ink.TryGetValue(key, out result))
            return result;
        using (var bitmap = new Bitmap(500, Math.Max(80, (int)font.GetHeight() + 20)))
        using (var graphics = Graphics.FromImage(bitmap))
        using (var format = Format())
        {
            graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            graphics.DrawString("AgjpqXY0123", font, Brushes.White, PointF.Empty, format);
            int top = bitmap.Height, bottom = 0;
            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                    if (bitmap.GetPixel(x, y).A > 0)
                    {
                        top = Math.Min(top, y);
                        bottom = Math.Max(bottom, y);
                    }

            result = new Rectangle(0, top, 0, bottom - top + 1);
            ink[key] = result;
            return result;
        }
    }

    // 计算字符的排版前进宽度，空白与 CJK 分开处理。
    static float Advance(Graphics graphics, char value, Font font, StringFormat format)
    {
        string key = font.FontFamily.Name + font.Size + ":" + value;
        float width;
        if (!advances.TryGetValue(key, out width))
        {
            width = graphics.MeasureString(value.ToString(), font, Int32.MaxValue, format).Width;
            advances[key] = width;
        }

        return width;
    }

    // 测量文本像素尺寸，供控件布局和裁剪使用。
    static float Measure(Graphics graphics, string text, Font font, StringFormat format, float tracking, float wordSpace)
    {
        float width = 0;
        foreach (char c in text)
            width += Advance(graphics, c, font, format) + tracking + (c == ' ' ? wordSpace : 0);
        return Math.Max(0, width - tracking);
    }

    // 按字体和字符间距绘制一行文字。
    static void PaintLine(Graphics graphics, string text, Font font, Brush brush, float x, float y, StringFormat format, float tracking, float wordSpace)
    {
        foreach (char c in text)
        {
            if (c != ' ')
                graphics.DrawString(c.ToString(), font, brush, new PointF((float)Math.Round(x), y), format);
            x += Advance(graphics, c, font, format) + tracking + (c == ' ' ? wordSpace : 0);
        }
    }

    // 读取实际字形高度，用于垂直居中。
    public static int InkHeight(float scale, bool title)
    {
        return InkFor(FontFor(title, scale)).Height;
    }

    // 在给定矩形绘制文本，遵守裁剪、字体和语言设置。
    public static void Draw(Graphics graphics, string text, Rectangle bounds, Color color, float scale, bool center, bool wrap, bool shadow, bool title)
    {
        text = Normalize(text);
        if (title)
            text = text.ToUpperInvariant();
        Font font = FontFor(title, scale);
        Rectangle glyph = InkFor(font);
        var state = graphics.Save();
        graphics.SetClip(bounds, CombineMode.Intersect);
        graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        using (var format = Format())
        using (var brush = new SolidBrush(color))
        using (var dark = new SolidBrush(OreTheme.Field))
        {
            float tracking = title ? 0 : 0.1f * scale, wordSpace = title ? 3 * scale : 1.5f * scale;
            var lines = new List<string>();
            string line = "";
            foreach (char c in text.Replace("\r", ""))
            {
                if (c == '\n')
                {
                    lines.Add(line);
                    line = "";
                    continue;
                }

                line += c;
                if (wrap && Measure(graphics, line, font, format, tracking, wordSpace) > bounds.Width && line.Length > 1)
                {
                    int split = line.LastIndexOf(' ');
                    if (split <= 0)
                        split = line.Length - 1;
                    lines.Add(line.Substring(0, split));
                    line = line.Substring(split).TrimStart();
                }
            }

            lines.Add(line);
            float advance = glyph.Height + Math.Max(3, (int)Math.Round(4 * scale));
            float height = glyph.Height + (lines.Count - 1) * advance;
            float y = center ? bounds.Y + (bounds.Height - height) / 2 : bounds.Y;
            foreach (string value in lines)
            {
                float width = Measure(graphics, value, font, format, tracking, wordSpace);
                float x = center ? bounds.X + (bounds.Width - width) / 2 : bounds.X;
                if (shadow)
                    PaintLine(graphics, value, font, dark, x + 1, (float)Math.Round(y) - glyph.Top + 1, format, tracking, wordSpace);
                PaintLine(graphics, value, font, brush, x, (float)Math.Round(y) - glyph.Top, format, tracking, wordSpace);
                y += advance;
            }
        }

        graphics.Restore(state);
    }
}

static class PixelText
{
    static readonly Dictionary<string, PrivateFontCollection> cjkCollections = new Dictionary<string, PrivateFontCollection>();
    static readonly List<IntPtr> cjkMemory = new List<IntPtr>();
    // 按字符和当前语言取得匹配的 CJK 字体族。
    static FontFamily CjkFamily(int lang, bool heading = false)
    {
        string region = lang == 2 ? "jp" : lang == 3 ? "kr" : lang == 4 ? "hk" : lang == 5 ? "tw" : "sc";
        string resource = "source-han-" + region + (region == "jp" || region == "kr" ? (heading ? "-heavy" : "") : (heading ? "-heavy" : "-regular")) + ".otf";
        PrivateFontCollection collection;
        if (!cjkCollections.TryGetValue(resource, out collection))
        {
            collection = new PrivateFontCollection();
            using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                if (stream == null)
                    throw new InvalidOperationException("Missing Source Han Sans font: " + resource);
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                var memory = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, memory, bytes.Length);
                cjkMemory.Add(memory);
                collection.AddMemoryFont(memory, bytes.Length);
            }

            cjkCollections[resource] = collection;
        }

        return collection.Families[0];
    }

    // 用对应 CJK 字体绘制中日韩文字。
    public static void DrawCjk(Graphics g, string text, Rectangle bounds, Color color, float scale, bool center, bool wrap, bool shadow, bool heading, bool vertical = false)
    {
        using (var font = new Font(CjkFamily(L10n.Language, heading), Math.Max(13, 16 * scale), FontStyle.Regular, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(color))
        using (var format = new StringFormat())
        {
            format.Alignment = center ? StringAlignment.Center : StringAlignment.Near;
            format.LineAlignment = center || vertical ? StringAlignment.Center : StringAlignment.Near;
            format.Trimming = StringTrimming.EllipsisCharacter;
            if (!wrap)
                format.FormatFlags |= StringFormatFlags.NoWrap;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.DrawString(text, font, brush, bounds, format);
        }
    }

    // 绘制菜单文本并保持选中/禁用颜色语义。
    public static void DrawMenu(Graphics g, string text, Rectangle bounds, bool center = false)
    {
        using (var font = new Font(CjkFamily(3), Math.Max(13, 14 * bounds.Height / 44f), FontStyle.Regular, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(OreTheme.Text))
        using (var format = new StringFormat())
        {
            format.Alignment = center ? StringAlignment.Center : StringAlignment.Near;
            format.LineAlignment = StringAlignment.Center;
            format.FormatFlags = StringFormatFlags.NoWrap;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.DrawString(text, font, brush, bounds, format);
        }
    }

    // 输入与导航文字垂直居中；英文使用 Minecraft Body，CJK 保留原地区字体回退。
    public static void DrawCjkOrBody(Graphics graphics, string text, Rectangle bounds, Color color, float scale, bool center)
    {
        if (!L10n.English || !MinecraftText.CanDraw(text))
            DrawCjk(graphics, text, bounds, color, scale, center, false, false, false, true);
        else
        {
            if (!center)
            {
                bounds.Y += Math.Max(0, (bounds.Height - MinecraftText.InkHeight(scale, false)) / 2);
                bounds.Height = MinecraftText.InkHeight(scale, false) + 4;
            }

            Draw(graphics, text, bounds, color, scale, center, false, false);
        }
    }

    sealed class Glyph
    {
        public int Width;
        public byte[] Data;
    }

    static Dictionary<char, Glyph> glyphs = new Dictionary<char, Glyph>();
    static Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>();
    static PixelText()
    {
        var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("unifont.hex.gz");
        if (stream == null)
            return;
        using (stream)
        using (var gzip = new GZipStream(stream, CompressionMode.Decompress))
        using (var reader = new StreamReader(gzip))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                int colon = line.IndexOf(':');
                if (colon < 0)
                    continue;
                int code = int.Parse(line.Substring(0, colon), NumberStyles.HexNumber);
                if (code > 65535)
                    continue;
                string bits = line.Substring(colon + 1);
                if (bits.Length != 32 && bits.Length != 64)
                    continue;
                byte[] data = new byte[bits.Length / 2];
                for (int i = 0; i < data.Length; i++)
                    data[i] = byte.Parse(bits.Substring(i * 2, 2), NumberStyles.HexNumber);
                glyphs[(char)code] = new Glyph
                {
                    Width = data.Length / 2,
                    Data = data
                };
            }
        }
    }

    // 取得像素字形宽度，供同一测量/绘制算法使用。
    static int CharWidth(char c)
    {
        Glyph glyph;
        return glyphs.TryGetValue(c, out glyph) ? glyph.Width : 16;
    }

    // 按字符宽度计算整段文本宽度。
    public static float Width(string text, float scale)
    {
        float width = 0;
        foreach (char c in text ?? "")
            width += (CharWidth(c) + 1) * scale;
        return width;
    }

    // 把字形转为像素点阵，缓存结果用于重复绘制。
    static Bitmap Raster(string text, Color color)
    {
        string key = color.ToArgb() + "/" + text;
        Bitmap found;
        if (cache.TryGetValue(key, out found))
            return found;
        int w = Math.Max(1, (int)Width(text, 1));
        var b = new Bitmap(w, 16);
        int x = 0;
        foreach (char c in text)
        {
            Glyph glyph;
            if (glyphs.TryGetValue(c, out glyph))
            {
                int rowbytes = glyph.Width / 8;
                for (int y = 0; y < 16; y++)
                    for (int k = 0; k < glyph.Width; k++)
                        if ((glyph.Data[y * rowbytes + k / 8] & (128 >> (k % 8))) != 0)
                            b.SetPixel(x + k, y, color);
            }
            else
            {
                for (int y = 2; y < 14; y++)
                    for (int k = 2; k < 14; k++)
                        if (y == 2 || y == 13 || k == 2 || k == 13)
                            b.SetPixel(x + k, y, color);
            }

            x += CharWidth(c) + 1;
        }

        if (cache.Count > 700)
        {
            foreach (var old in cache.Values)
                old.Dispose();
            cache.Clear();
        }

        cache[key] = b;
        return b;
    }

    // 在给定矩形绘制文本，遵守裁剪、字体和语言设置。
    public static void Draw(Graphics g, string text, Rectangle bounds, Color color, float scale, bool center, bool wrap, bool shadow, bool heading = false)
    {
        if (string.IsNullOrEmpty(text) || bounds.Width <= 0 || bounds.Height <= 0)
            return;
        if (L10n.English && MinecraftText.CanDraw(text))
        {
            MinecraftText.Draw(g, text, bounds, color, scale, center, wrap, shadow, heading);
            return;
        }

        DrawCjk(g, text, bounds, color, scale, center, wrap, shadow, heading);
    }
}

sealed class PixelLabel : Label
{
    public bool VerticalCenter;
    public bool Wrap = true;
    public bool BrandHeading;
    public float PixelScale = 1, EnglishScale = 1;
    public bool Shadow, Center = false, EnglishHeading;
    public PixelLabel()
    {
        AutoSize = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
        ForeColor = OreTheme.Text;
    }

    // 绘制控件背景，避免重复填充导致的闪烁。
    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    // 根据控件当前状态绘制外观；不要在绘制阶段修改游戏或业务状态。
    protected override void OnPaint(PaintEventArgs e)
    {
        OreTheme.Fill(e.Graphics, ClientRectangle, Parent == null ? OreTheme.Card : Parent.BackColor);
        if (BrandHeading)
        {
            float brandScale = PixelScale * EnglishScale;
            var brandBounds = ClientRectangle;
            brandBounds.Y = (Height - MinecraftText.InkHeight(brandScale, true)) / 2;
            brandBounds.Height = Height - brandBounds.Y;
            MinecraftText.Draw(e.Graphics, Text, brandBounds, ForeColor, brandScale, false, false, Shadow, true);
            return;
        }

        float scale = PixelScale * (L10n.English ? EnglishScale : 1);
        Rectangle bounds = ClientRectangle;
        if (VerticalCenter && (!L10n.English || !MinecraftText.CanDraw(Text)))
        {
            PixelText.DrawCjk(e.Graphics, Text, bounds, ForeColor, scale, Center, Wrap, Shadow, EnglishHeading, true);
            return;
        }

        if (VerticalCenter)
        {
            int height = L10n.English && MinecraftText.CanDraw(Text) ? MinecraftText.InkHeight(scale, EnglishHeading) : (int)Math.Round(16 * scale);
            bounds.Y = Math.Max(0, (Height - height) / 2);
            bounds.Height = Height - bounds.Y;
        }

        PixelText.Draw(e.Graphics, Text, bounds, ForeColor, scale, Center, Wrap, Shadow, EnglishHeading);
    }
}

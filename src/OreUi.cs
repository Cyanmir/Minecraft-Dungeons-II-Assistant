// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 中文维护说明：自绘像素风 WinForms 控件、字体测量和滚动/数字输入。控件坐标和尺寸使用像素；Number/Slider 的 ValueChanged 与范围约束要一致，绘制对象和嵌入字体需要正确释放。
// 源码导航和常改参数见 docs/maintenance.md；协议、反射标识及类型白名单修改前先核对两端。
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

// MinecraftText 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
static class MinecraftText
{
    static readonly PrivateFontCollection headingFonts = new PrivateFontCollection(), bodyFonts = new PrivateFontCollection();
    static readonly List<IntPtr> fontMemory = new List<IntPtr>();
    static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
    static readonly Dictionary<string, Rectangle> ink = new Dictionary<string, Rectangle>();
    static readonly Dictionary<string, float> advances = new Dictionary<string, float>();
    static readonly FontFamily heading = Load(headingFonts, "minecraft-ten.ttf"), body = Load(bodyFonts, "minecraft-body.ttf");
    // 加载本模块的配置或内嵌目录，并使用实现中的校验/回退规则。
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
        using (var dark = new SolidBrush(Color.FromArgb(20, 30, 33)))
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

// OreTheme 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
static class OreTheme
{
    public static readonly Color Background = Color.FromArgb(12, 36, 46), Card = Color.FromArgb(8, 22, 28), Field = Color.FromArgb(27, 45, 52), Line = Color.FromArgb(3, 13, 18), Green = Color.FromArgb(66, 180, 192), Text = Color.FromArgb(235, 243, 245), Muted = Color.FromArgb(145, 169, 177), Edge = Color.FromArgb(60, 104, 117), Accent = Color.FromArgb(117, 229, 239);
    // 使用给定颜色填充矩形，不改变控件业务状态。
    public static void Fill(Graphics g, Rectangle r, Color c)
    {
        using (var b = new SolidBrush(c))
            g.FillRectangle(b, r);
    }

    // 按像素线宽绘制边框，保留内外尺寸关系。
    public static void Border(Graphics g, Rectangle r, Color c, int width)
    {
        using (var p = new Pen(c, width))
            g.DrawRectangle(p, r.X, r.Y, r.Width - 1, r.Height - 1);
    }

    // 用亮边/暗边绘制像素风凸起效果。
    public static void Bevel(Graphics g, Rectangle r, Color fill, Color top, Color bottom)
    {
        Fill(g, r, Line);
        Fill(g, new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, r.Height - 4), fill);
        Fill(g, new Rectangle(r.X + 2, r.Y + 2, r.Width - 4, 2), top);
        Fill(g, new Rectangle(r.X + 2, r.Y + r.Height - 7, r.Width - 4, 5), bottom);
    }
}

// PixelText 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
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
        using (var font = new Font(CjkFamily(L10n.Language, heading), Math.Max(13, Math.Min(30, 16 * scale)), FontStyle.Regular, GraphicsUnit.Pixel))
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
        using (var font = new Font(CjkFamily(3), 14, FontStyle.Regular, GraphicsUnit.Pixel))
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

    // Glyph 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
    sealed class Glyph
    {
        public int Width;
        public byte[] Data;
    }

    static Dictionary<char, Glyph> glyphs = new Dictionary<char, Glyph>();
    static Dictionary<string, Bitmap> cache = new Dictionary<string, Bitmap>();
    // 初始化 PixelText 的本地状态、依赖和必要绑定；实例释放时使用对应清理流程。
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

// PixelLabel 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
sealed class PixelLabel : Label
{
    public bool VerticalCenter;
    public bool Wrap = true;
    public bool BrandHeading;
    public float PixelScale = 1, EnglishScale = 1;
    public bool Shadow, Center = false, EnglishHeading;
    // 初始化 PixelLabel 的本地状态、依赖和必要绑定；实例释放时使用对应清理流程。
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

// OreButton 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
class OreButton : Button
{
    public bool Primary, Destructive, Navigation, Selected, KeyBinding, FixedTypography;
    public float PixelScale = 1.15f;
    bool hover, pressed;
    // 初始化 OreButton 的本地状态、依赖和必要绑定；实例释放时使用对应清理流程。
    public OreButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        MouseEnter += delegate
        {
            hover = true;
            Invalidate();
        };
        MouseLeave += delegate
        {
            hover = false;
            pressed = false;
            Invalidate();
        };
        MouseDown += delegate
        {
            pressed = true;
            Invalidate();
        };
        MouseUp += delegate
        {
            pressed = false;
            Invalidate();
        };
    }

    // 根据控件当前状态绘制外观；不要在绘制阶段修改游戏或业务状态。
    protected override void OnPaint(PaintEventArgs e)
    {
        bool selected = Navigation && Selected;
        Color fill = Primary ? Color.FromArgb(83, 176, 188) : (Destructive ? Color.FromArgb(103, 45, 43) : Color.FromArgb(123, 157, 169));
        if (Navigation)
            fill = selected ? Color.FromArgb(33, 77, 89) : Color.FromArgb(18, 40, 48);
        if (KeyBinding)
            fill = OreTheme.Field;
        if (hover)
            fill = Primary ? Color.FromArgb(115, 210, 220) : (Destructive ? Color.FromArgb(142, 62, 58) : (Navigation || KeyBinding ? Color.FromArgb(43, 76, 87) : Color.FromArgb(163, 191, 202)));
        if (pressed)
            fill = Color.FromArgb(44, 86, 99);
        if (!Enabled)
            fill = Color.FromArgb(47, 64, 72);
        OreTheme.Bevel(e.Graphics, ClientRectangle, fill, Primary ? OreTheme.Accent : OreTheme.Edge, Color.FromArgb(8, 25, 32));
        if (KeyBinding)
        {
            OreTheme.Fill(e.Graphics, new Rectangle(3, 4, Width - 6, Height - 9), fill);
            OreTheme.Border(e.Graphics, ClientRectangle, OreTheme.Line, 3);
        }

        if (FixedTypography)
        {
            PixelText.DrawMenu(e.Graphics, Text, new Rectangle(12, 0, Width - 24, Height));
            return;
        }

        Color text = Primary ? OreTheme.Line : (Destructive || Navigation || KeyBinding ? OreTheme.Text : OreTheme.Line);
        if (!Enabled)
            text = OreTheme.Muted;
        Rectangle rect = new Rectangle(Navigation ? 18 : 8, Navigation ? (Height - (int)(20 * PixelScale)) / 2 : 3, Width - (Navigation ? 30 : 16), Height - 8);
        PixelText.Draw(e.Graphics, Text, rect, text, PixelScale, !Navigation, false, Navigation, Navigation || Primary || Destructive);
        if (selected)
            OreTheme.Fill(e.Graphics, new Rectangle(4, 5, 4, Height - 13), OreTheme.Accent);
        if (Focused && ShowFocusCues)
            OreTheme.Border(e.Graphics, new Rectangle(4, 4, Width - 8, Height - 11), Color.White, 1);
    }
}

// OreCard 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
sealed class OreCard : Panel
{
    // 初始化 OreCard 的本地状态、依赖和必要绑定；实例释放时使用对应清理流程。
    public OreCard()
    {
        BackColor = OreTheme.Card;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    // 根据控件当前状态绘制外观；不要在绘制阶段修改游戏或业务状态。
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        OreTheme.Border(e.Graphics, ClientRectangle, OreTheme.Line, 2);
        OreTheme.Fill(e.Graphics, new Rectangle(2, 2, Width - 4, 2), OreTheme.Edge);
        OreTheme.Fill(e.Graphics, new Rectangle(2, Height - 4, Width - 4, 2), OreTheme.Line);
    }
}

// OreScrollBar 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
sealed class OreScrollBar : Control
{
    public int Maximum, PageSize, Value;
    public event Action<int> Changed;
    bool dragging;
    int dragY, dragValue;
    // 初始化 OreScrollBar 的本地状态、依赖和必要绑定；实例释放时使用对应清理流程。
    public OreScrollBar()
    {
        Width = 16;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
    }

    // 根据可见比例计算滚动条滑块像素高度。
    int ThumbHeight
    {
        get
        {
            return Math.Max(30, (int)(Height * PageSize / (float)Math.Max(1, PageSize + Maximum)));
        }
    }

    // 根据滚动值计算滑块的像素位置。
    int ThumbY
    {
        get
        {
            return Maximum == 0 ? 0 : (int)((Height - ThumbHeight) * Value / (float)Maximum);
        }
    }

    // 根据控件当前状态绘制外观；不要在绘制阶段修改游戏或业务状态。
    protected override void OnPaint(PaintEventArgs e)
    {
        OreTheme.Fill(e.Graphics, ClientRectangle, OreTheme.Line);
        OreTheme.Fill(e.Graphics, new Rectangle(4, 2, Width - 8, Height - 4), OreTheme.Field);
        OreTheme.Bevel(e.Graphics, new Rectangle(1, ThumbY, Width - 2, ThumbHeight), OreTheme.Muted, OreTheme.Edge, OreTheme.Line);
    }

    // 处理按下位置并更新控件交互状态，不发送游戏输入。
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Y >= ThumbY && e.Y <= ThumbY + ThumbHeight)
        {
            dragging = true;
            dragY = e.Y;
            dragValue = Value;
            Capture = true;
        }
        else if (Changed != null)
            Changed(Value + (e.Y < ThumbY ? -PageSize : PageSize));
        base.OnMouseDown(e);
    }

    // 根据当前拖动状态更新控件值或悬停位置。
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (dragging && Changed != null)
            Changed(dragValue + (int)((e.Y - dragY) * Maximum / (float)Math.Max(1, Height - ThumbHeight)));
        base.OnMouseMove(e);
    }

    // 结束控件拖动，释放鼠标捕获。
    protected override void OnMouseUp(MouseEventArgs e)
    {
        dragging = false;
        Capture = false;
        base.OnMouseUp(e);
    }
}

// OreScrollPanel 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
sealed class OreScrollPanel : Panel
{
    readonly OreScrollBar scrollbar = new OreScrollBar();
    int offset, contentHeight = 680;
    // 页面实际内容高度，单位为像素，用于滚动范围计算。
    public int ContentHeight
    {
        get
        {
            return contentHeight;
        }

        set
        {
            contentHeight = value;
            RefreshScrollBar();
        }
    }

    // 当前滚动像素偏移，限制在内容与视口差值之内。
    public int ScrollOffset
    {
        get
        {
            return offset;
        }

        set
        {
            int next = Math.Max(0, Math.Min(Math.Max(0, contentHeight - ClientSize.Height), value));
            int delta = offset - next;
            offset = next;
            foreach (Control c in Controls)
                if (c != scrollbar)
                    c.Top += delta;
            RefreshScrollBar();
            Invalidate();
        }
    }

    // 初始化 OreScrollPanel 的本地状态、依赖和必要绑定；实例释放时使用对应清理流程。
    public OreScrollPanel()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        Controls.Add(scrollbar);
        scrollbar.Changed += delegate (int value)
        {
            ScrollOffset = value;
        };
    }

    // 按内容高度和可见高度重算滚动条位置与范围。
    void RefreshScrollBar()
    {
        scrollbar.Maximum = Math.Max(0, contentHeight - ClientSize.Height);
        scrollbar.PageSize = ClientSize.Height;
        scrollbar.Value = offset;
        scrollbar.Visible = scrollbar.Maximum > 0;
        scrollbar.Bounds = new Rectangle(Math.Max(0, Width - 16), 0, 16, Height);
        scrollbar.BringToFront();
        scrollbar.Invalidate();
    }

    // 尺寸改变后重算显示/滚动范围，不改变业务配置。
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (scrollbar != null)
            RefreshScrollBar();
    }

    // 按滚轮增量调整列表或页面滚动，并保留边界限制。
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        ScrollOffset -= e.Delta / 120 * 48;
        var handled = e as HandledMouseEventArgs;
        if (handled != null)
            handled.Handled = true;
        base.OnMouseWheel(e);
    }
}

// OreToggle 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
sealed class OreToggle : CheckBox
{
    public bool Tile;
    // 初始化 OreToggle 的本地状态、依赖和必要绑定；实例释放时使用对应清理流程。
    public OreToggle()
    {
        AutoSize = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Opaque, true);
        CheckedChanged += delegate
        {
            Invalidate();
        };
    }

    // 绘制控件背景，避免重复填充导致的闪烁。
    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    // 根据控件当前状态绘制外观；不要在绘制阶段修改游戏或业务状态。
    protected override void OnPaint(PaintEventArgs e)
    {
        OreTheme.Fill(e.Graphics, ClientRectangle, Parent == null ? OreTheme.Card : Parent.BackColor);
        if (Tile)
        {
            OreTheme.Bevel(e.Graphics, ClientRectangle, Checked ? Color.FromArgb(32, 80, 91) : OreTheme.Field, OreTheme.Edge, OreTheme.Line);
            var box = new Rectangle(13, (Height - 24) / 2, 24, 24);
            OreTheme.Fill(e.Graphics, box, OreTheme.Line);
            OreTheme.Fill(e.Graphics, new Rectangle(box.X + 2, box.Y + 2, 20, 20), Checked ? OreTheme.Green : OreTheme.Field);
            if (Checked)
                PixelText.Draw(e.Graphics, "✓", new Rectangle(box.X + 3, box.Y + 2, 18, 20), Color.White, 1, true, false, false);
            PixelText.Draw(e.Graphics, Text, new Rectangle(46, 4, Width - 51, Height - 8), OreTheme.Text, 1.1f, true, false, false);
        }
        else
        {
            var r = new Rectangle(0, (Height - 32) / 2, 64, 32);
            OreTheme.Fill(e.Graphics, r, OreTheme.Line);
            OreTheme.Fill(e.Graphics, new Rectangle(2, r.Y + 2, 60, 28), Checked ? OreTheme.Green : OreTheme.Field);
            int thumb = Checked ? 32 : 2;
            OreTheme.Bevel(e.Graphics, new Rectangle(thumb, r.Y + 2, 30, 28), Color.FromArgb(207, 208, 211), Color.FromArgb(243, 243, 244), Color.FromArgb(137, 138, 140));
            PixelText.Draw(e.Graphics, Checked ? "I" : "O", new Rectangle(Checked ? 3 : 33, r.Y + 5, 28, 23), OreTheme.Text, 1, true, false, false);
            if (Text.Length > 0)
            {
                int textHeight = (int)Math.Ceiling(L10n.English && MinecraftText.CanDraw(Text) ? MinecraftText.Height(1) : 16);
                int y = Math.Max(2, (Height - textHeight) / 2);
                PixelText.Draw(e.Graphics, Text, new Rectangle(77, y, Width - 80, Height - y - 2), OreTheme.Text, 1, false, false, false);
            }
        }

        if (Focused && ShowFocusCues)
            OreTheme.Border(e.Graphics, new Rectangle(1, 1, Width - 2, Height - 2), Color.White, 1);
    }
}

// OreNumber 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
sealed class OreNumber : Control
{
    decimal min, max = 1000, value;
    string editing;
    public string Suffix = "";
    public event EventHandler ValueChanged;
    // 数字控件允许的最小值；与配置解析和滑块边界同步维护。
    public decimal Minimum
    {
        get
        {
            return min;
        }

        set
        {
            min = value;
            Value = this.value;
        }
    }

    // 数字控件允许的最大值；不得只放宽 UI 而绕过业务限制。
    public decimal Maximum
    {
        get
        {
            return max;
        }

        set
        {
            max = value;
            Value = this.value;
        }
    }

    // 控件当前数值；setter 保持范围限制并在变化时通知配置层。
    public decimal Value
    {
        get
        {
            return value;
        }

        set
        {
            decimal n = Math.Max(min, Math.Min(max, value));
            if (n == this.value)
                return;
            this.value = n;
            editing = null;
            Invalidate();
            if (ValueChanged != null)
                ValueChanged(this, EventArgs.Empty);
        }
    }

    // 初始化 OreNumber 的本地状态、依赖和必要绑定；实例释放时使用对应清理流程。
    public OreNumber()
    {
        Size = new Size(170, 44);
        TabStop = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        Cursor = Cursors.Hand;
    }

    // 根据控件当前状态绘制外观；不要在绘制阶段修改游戏或业务状态。
    protected override void OnPaint(PaintEventArgs e)
    {
        OreTheme.Bevel(e.Graphics, ClientRectangle, OreTheme.Field, OreTheme.Edge, OreTheme.Line);
        PixelText.Draw(e.Graphics, "−", new Rectangle(4, 4, 29, Height - 8), OreTheme.Text, 1.2f, true, false, false);
        PixelText.Draw(e.Graphics, "+", new Rectangle(Width - 32, 4, 29, Height - 8), OreTheme.Text, 1.2f, true, false, false);
        PixelText.Draw(e.Graphics, (editing ?? Value.ToString("0")) + Suffix, new Rectangle(35, 4, Width - 70, Height - 8), OreTheme.Text, 1.15f, true, false, false);
        if (Focused)
            OreTheme.Border(e.Graphics, new Rectangle(2, 2, Width - 4, Height - 4), Color.White, 1);
    }

    // 处理按下位置并更新控件交互状态，不发送游戏输入。
    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (e.X < 35)
            Value--;
        else if (e.X > Width - 35)
            Value++;
        base.OnMouseDown(e);
    }

    // 声明控件自行处理的导航键，避免 WinForms 把它当作焦点切换。
    protected override bool IsInputKey(Keys keyData)
    {
        return keyData == Keys.Left || keyData == Keys.Right || keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData);
    }

    // 处理控件的方向键、提交或取消操作。
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Right)
        {
            Value++;
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Left)
        {
            Value--;
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            Commit();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            editing = null;
            Invalidate();
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    // 处理直接输入的字符，限制输入格式并保持数值边界。
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (char.IsDigit(e.KeyChar))
        {
            editing = (editing ?? "") + e.KeyChar;
            if (editing.Length > 6)
                editing = editing.Substring(1);
            e.Handled = true;
            Invalidate();
        }
        else if (e.KeyChar == '\b')
        {
            editing = editing ?? Value.ToString("0");
            if (editing.Length > 0)
                editing = editing.Substring(0, editing.Length - 1);
            e.Handled = true;
            Invalidate();
        }

        base.OnKeyPress(e);
    }

    // 提交数字输入缓冲，合法值限幅后触发修改事件。
    void Commit()
    {
        decimal parsed;
        if (editing != null && decimal.TryParse(editing, out parsed))
            Value = parsed;
        editing = null;
        Invalidate();
    }

    // 失去焦点时提交数字编辑，避免半截输入直接进入设置。
    protected override void OnLostFocus(EventArgs e)
    {
        Commit();
        base.OnLostFocus(e);
    }
}

// OreSlider 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
sealed class OreSlider : Control
{
    public int Minimum = 1, Maximum = 99;
    int value;
    bool dragging;
    public event EventHandler ValueChanged;
    // 控件当前数值；setter 保持范围限制并在变化时通知配置层。
    public int Value
    {
        get
        {
            return value;
        }

        set
        {
            int n = Math.Max(Minimum, Math.Min(Maximum, value));
            if (n == this.value)
                return;
            this.value = n;
            Invalidate();
            if (ValueChanged != null)
                ValueChanged(this, EventArgs.Empty);
        }
    }

    // 初始化 OreSlider 的本地状态、依赖和必要绑定；实例释放时使用对应清理流程。
    public OreSlider()
    {
        Height = 39;
        TabStop = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        Cursor = Cursors.Hand;
    }

    // 根据控件当前状态绘制外观；不要在绘制阶段修改游戏或业务状态。
    protected override void OnPaint(PaintEventArgs e)
    {
        int x = 15 + (int)((Width - 30) * (Value - Minimum) / (float)Math.Max(1, Maximum - Minimum));
        OreTheme.Fill(e.Graphics, new Rectangle(1, 16, Width - 2, 10), OreTheme.Line);
        OreTheme.Fill(e.Graphics, new Rectangle(3, 18, Width - 6, 6), Color.FromArgb(58, 88, 99));
        OreTheme.Fill(e.Graphics, new Rectangle(3, 18, Math.Max(0, x - 3), 6), OreTheme.Green);
        OreTheme.Bevel(e.Graphics, new Rectangle(x - 14, 6, 28, 30), Color.FromArgb(206, 207, 210), Color.FromArgb(240, 241, 242), Color.FromArgb(97, 98, 99));
        if (Focused)
            OreTheme.Border(e.Graphics, new Rectangle(x - 17, 3, 34, 36), Color.White, 1);
    }

    // 把鼠标坐标映射到滑块值并限制边界。
    void Position(int x)
    {
        Value = Minimum + (int)Math.Round(Math.Max(0, Math.Min(1, (x - 15) / (float)Math.Max(1, Width - 30))) * (Maximum - Minimum));
    }

    // 处理按下位置并更新控件交互状态，不发送游戏输入。
    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        dragging = true;
        Capture = true;
        Position(e.X);
        base.OnMouseDown(e);
    }

    // 根据当前拖动状态更新控件值或悬停位置。
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (dragging)
            Position(e.X);
        base.OnMouseMove(e);
    }

    // 结束控件拖动，释放鼠标捕获。
    protected override void OnMouseUp(MouseEventArgs e)
    {
        dragging = false;
        Capture = false;
        base.OnMouseUp(e);
    }

    // 声明控件自行处理的导航键，避免 WinForms 把它当作焦点切换。
    protected override bool IsInputKey(Keys keyData)
    {
        return keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);
    }

    // 处理控件的方向键、提交或取消操作。
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Left)
        {
            Value--;
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Right)
        {
            Value++;
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }
}

// OreSelect 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
sealed class OreSelect : Control
{
    public bool FixedTypography;
    public List<object> Items = new List<object>();
    int index = -1;
    public event EventHandler SelectedIndexChanged;
    // 下拉选项索引；索引顺序与对应枚举/配置值一致。
    public int SelectedIndex
    {
        get
        {
            return index;
        }

        set
        {
            if (value == index)
                return;
            index = value;
            Invalidate();
            if (SelectedIndexChanged != null)
                SelectedIndexChanged(this, EventArgs.Empty);
        }
    }

    // 初始化 OreSelect 的本地状态、依赖和必要绑定；实例释放时使用对应清理流程。
    public OreSelect()
    {
        Height = 44;
        TabStop = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        Cursor = Cursors.Hand;
    }

    // 根据控件当前状态绘制外观；不要在绘制阶段修改游戏或业务状态。
    protected override void OnPaint(PaintEventArgs e)
    {
        OreTheme.Bevel(e.Graphics, ClientRectangle, OreTheme.Field, OreTheme.Edge, OreTheme.Line);
        if (FixedTypography)
        {
            PixelText.DrawMenu(e.Graphics, index >= 0 && index < Items.Count ? Items[index].ToString() : "", new Rectangle(13, 0, Width - 42, Height));
        }
        else
            PixelText.Draw(e.Graphics, index >= 0 && index < Items.Count ? Items[index].ToString() : L10n.T("选择槽位"), new Rectangle(13, 9, Width - 42, Height - 13), OreTheme.Text, 1, false, false, false);
        PixelText.DrawMenu(e.Graphics, "⌄", new Rectangle(Width - 31, 0, 24, Height), true);
        if (Focused)
            OreTheme.Border(e.Graphics, new Rectangle(2, 2, Width - 4, Height - 4), Color.White, 1);
    }

    // 响应控件点击，切换或打开当前选项。
    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Open();
    }

    // 处理控件的方向键、提交或取消操作。
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
        {
            Open();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Up)
        {
            SelectedIndex = Math.Max(0, index - 1);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Down)
        {
            SelectedIndex = Math.Min(Items.Count - 1, index + 1);
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    // 打开下拉选项界面，选择结果通过统一索引更新。
    void Open()
    {
        if (Items.Count == 0)
            return;
        var popup = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            ShowInTaskbar = false,
            BackColor = OreTheme.Line,
            ClientSize = new Size(Width, Items.Count * 43 + 4)
        };
        Point p = PointToScreen(new Point(0, Height));
        Rectangle screen = Screen.FromControl(this).WorkingArea;
        if (p.Y + popup.Height > screen.Bottom)
            p.Y = PointToScreen(Point.Empty).Y - popup.Height;
        popup.Location = p;
        for (int i = 0; i < Items.Count; i++)
        {
            int n = i;
            var b = new OreButton
            {
                Text = Items[i].ToString() + (i == index ? " ✓" : ""),
                Navigation = true,
                FixedTypography = FixedTypography,
                Selected = i == index,
                Location = new Point(2, 2 + i * 43),
                Size = new Size(Width - 4, 43),
                PixelScale = 1
            };
            b.Click += delegate
            {
                SelectedIndex = n;
                popup.Close();
            };
            popup.Controls.Add(b);
        }

        popup.Deactivate += delegate
        {
            popup.Close();
        };
        popup.FormClosed += delegate
        {
            popup.Dispose();
        };
        popup.Show(FindForm());
    }
}

// OreHealthBar 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
sealed class OreHealthBar : Control
{
    float percent;
    // 血条显示百分比，未知或越界值按控件规则处理。
    public float Percent
    {
        get
        {
            return percent;
        }

        set
        {
            percent = Math.Max(0, Math.Min(100, value));
            Invalidate();
        }
    }

    // 初始化 OreHealthBar 的本地状态、依赖和必要绑定；实例释放时使用对应清理流程。
    public OreHealthBar()
    {
        Height = 10;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    // 根据控件当前状态绘制外观；不要在绘制阶段修改游戏或业务状态。
    protected override void OnPaint(PaintEventArgs e)
    {
        OreTheme.Fill(e.Graphics, ClientRectangle, OreTheme.Line);
        OreTheme.Fill(e.Graphics, new Rectangle(2, 2, Width - 4, Height - 4), OreTheme.Field);
        OreTheme.Fill(e.Graphics, new Rectangle(2, 2, (int)((Width - 4) * Percent / 100), Height - 4), Percent < 40 ? Color.FromArgb(190, 48, 48) : OreTheme.Green);
    }
}

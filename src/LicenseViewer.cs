// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 汇总内嵌 MIT 许可与第三方声明，并显示许可对话框。
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

static class LicenseViewer
{
    static readonly string[] resources =
    {
        "LICENSE",
        "LICENSE.zh-CN.md",
        "THIRD_PARTY_NOTICES.md",
        "Mojang-fonts-license.txt",
        "Mojang-fonts-OFL.txt",
        "OFL-1.1.txt",
        "SourceHanSans-LICENSE.txt"
    };
    public static string Contents()
    {
        var text = new StringBuilder("MCD2A\r\nCopyright (c) 2026 Cyanmir\r\nhttps://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant\r\n\r\n");
        foreach (string name in resources)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (stream == null)
                    throw new InvalidOperationException("Missing embedded license: " + name);
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    text.AppendLine("================ " + name + " ================");
                    text.AppendLine(reader.ReadToEnd().Replace("\r\n", "\n").Replace("\n", "\r\n"));
                    text.AppendLine();
                }
            }
        }

        return text.ToString();
    }

    // 许可正文保持原样；复用 Ore 自绘窗口与滚动文本，不出现系统标题和白色箭头滚动条。
    public static void Show(IWin32Window owner)
    {
        var ownerControl = owner as Control;
        var font = ownerControl == null ? SystemFonts.MessageBoxFont : ownerControl.Font;
        using (var dialog = new RerollDetailsWindow(font, "许可与致谢", 760, 640))
        {
            var body = new OreTextView { Text = Contents(), Font = font, BackColor = OreTheme.Field, ForeColor = OreTheme.Text };
            var close = new OreButton { Text = L10n.T("关闭"), Primary = true };
            dialog.Content.Controls.Add(body);
            dialog.Content.Controls.Add(close);
            close.Click += delegate { dialog.Close(); };
            dialog.EditorLayout += delegate(float scale)
            {
                int inset = Math.Max(8, (int)Math.Round(16 * scale));
                int height = (int)Math.Round(44 * scale), bottom = (int)Math.Round(72 * scale);
                body.Bounds = new Rectangle(inset, inset, Math.Max(1, dialog.Content.Width - inset * 2),
                    Math.Max(1, dialog.Content.Height - bottom - inset));
                close.Bounds = new Rectangle(Math.Max(inset, dialog.Content.Width - inset - (int)(140 * scale)),
                    dialog.Content.Height - inset - height, (int)(140 * scale), height);
                close.PixelScale = .95f * scale;
            };
            dialog.CancelButton = close;
            dialog.ShowDialog(owner);
        }
    }
}

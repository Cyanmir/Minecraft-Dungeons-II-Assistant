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
    // 生成当前模块的可读文本，供界面或导出报告使用。
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

    // 显示只读许可与致谢对话框，窗口关闭时释放控件。
    public static void Show(IWin32Window owner)
    {
        using (var dialog = new Form
        {
            Text = L10n.T("许可与致谢"),
            ClientSize = new Size(760, 640),
            MinimumSize = new Size(600, 450),
            StartPosition = FormStartPosition.CenterParent,
            ShowInTaskbar = false,
            BackColor = OreTheme.Background
        }

        )
        {
            var body = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Text = Contents(),
                BackColor = OreTheme.Field,
                ForeColor = OreTheme.Text,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10)
            };
            var bottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                Padding = new Padding(8),
                BackColor = OreTheme.Background
            };
            var close = new OreButton
            {
                Text = L10n.T("关闭"),
                Dock = DockStyle.Right,
                Width = 110,
                Primary = true
            };
            close.Click += delegate
            {
                dialog.Close();
            };
            bottom.Controls.Add(close);
            dialog.Controls.Add(body);
            dialog.Controls.Add(bottom);
            dialog.CancelButton = close;
            dialog.ShowDialog(owner);
        }
    }
}

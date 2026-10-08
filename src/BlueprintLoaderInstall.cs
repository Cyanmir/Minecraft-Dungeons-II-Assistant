// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 发现 Blueprint Loader 下载 ZIP，校验后备份并安装到游戏目录。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

static class BlueprintLoaderInstaller
{
    internal const string Source = "https://www.nexusmods.com/minecraftdungeons2/mods/2?tab=files";
    internal static readonly string[] Files =
    {
        "BlueprintLoader_P.pak",
        "BlueprintLoader_P.utoc",
        "BlueprintLoader_P.ucas"
    };
    const int MaxArchive = 16 * 1024 * 1024, MaxPayload = 32 * 1024 * 1024;
    static string Cache
    {
        get
        {
            return Path.Combine(Path.GetDirectoryName(ToolboxSettings.ConfigPath), "dependency-cache", "BlueprintLoader.zip");
        }
    }

    static string Hash(byte[] data)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "");
    }

    // 查询 Windows 已知下载目录，避免写死用户路径。
    [DllImport("shell32.dll")]
    static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid id, uint flags, IntPtr token, out IntPtr path);
    // 取得当前用户下载文件夹及受支持候选来源。
    internal static string Downloads()
    {
        IntPtr path = IntPtr.Zero;
        try
        {
            if (SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, out path) == 0)
                return Marshal.PtrToStringUni(path);
        }
        finally
        {
            if (path != IntPtr.Zero)
                Marshal.FreeCoTaskMem(path);
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    // 列出可识别的加载器目录结构。
    static IEnumerable<string> LoaderFolders(string paks)
    {
        foreach (string name in new[]
        {
            "~mods",
            "mods"
        }

        )
        {
            string mods = Path.Combine(paks, name);
            if (!Directory.Exists(mods))
                continue;
            var pending = new Stack<string>();
            pending.Push(mods);
            int count = 0;
            while (pending.Count > 0)
            {
                if (++count > 2048)
                    throw new IOException(L10n.T("模组目录过多，请检查蓝图加载器安装目录"));
                string folder = pending.Pop();
                if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
                    continue;
                if (Files.Any(f => File.Exists(Path.Combine(folder, f))))
                    yield return folder;
                foreach (string child in Directory.GetDirectories(folder))
                    pending.Push(child);
            }
        }
    }

    internal static void ValidatePayload(Dictionary<string, byte[]> data)
    {
        if (data.Count != 3 || Files.Any(f => !data.ContainsKey(f) || data[f].Length == 0) || data.Values.Sum(v => (long)v.Length) > MaxPayload)
            throw new InvalidDataException(L10n.T("蓝图加载器文件不完整或过大，未安装"));
        byte[] toc = data[Files[1]], pak = data[Files[0]];
        if (toc.Length < 16 || Encoding.ASCII.GetString(toc, 0, 16) != "-==--==--==--==-")
            throw new InvalidDataException(L10n.T("蓝图加载器 UTOC 格式无效，未安装"));
        bool magic = false;
        for (int i = Math.Max(0, pak.Length - 512); i <= pak.Length - 4; i++)
            if (pak[i] == 0xE1 && pak[i + 1] == 0x12 && pak[i + 2] == 0x6F && pak[i + 3] == 0x5A)
            {
                magic = true;
                break;
            }

        if (!magic)
            throw new InvalidDataException(L10n.T("蓝图加载器 PAK 格式无效，未安装"));
    }

    // 有界读取 ZIP，拒绝越界、重复、损坏或 HTML 下载替代物。
    internal static Dictionary<string, byte[]> ReadArchive(string path)
    {
        if (new FileInfo(path).Length > MaxArchive)
            throw new InvalidDataException(L10n.T("请选择 Blueprint Loader 的 ZIP，不是模组开发模板"));
        var data = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        string prefix = null;
        int count = 0;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            if (zip.Entries.Count > 128)
                throw new InvalidDataException(L10n.T("请选择 Blueprint Loader 的 ZIP，不是模组开发模板"));
            foreach (var entry in zip.Entries)
            {
                string name = entry.FullName.Replace('\\', '/');
                if (name.StartsWith("/") || name.Contains(":") || name.Split('/').Any(p => p == ".." || p == "."))
                    throw new InvalidDataException(L10n.T("安装包包含无效路径，未安装"));
                string leaf = name.Substring(name.LastIndexOf('/') + 1);
                if (!Files.Contains(leaf, StringComparer.OrdinalIgnoreCase))
                    continue;
                string current = name.Substring(0, name.Length - leaf.Length);
                if (prefix != null && !String.Equals(prefix, current, StringComparison.OrdinalIgnoreCase) || data.ContainsKey(leaf))
                    throw new InvalidDataException(L10n.T("安装包包含重复加载器，未安装"));
                prefix = current;
                if (entry.Length <= 0 || entry.Length > MaxPayload || (count += checked((int)entry.Length)) > MaxPayload)
                    throw new InvalidDataException(L10n.T("蓝图加载器文件不完整或过大，未安装"));
                using (var input = entry.Open())
                using (var output = new MemoryStream())
                {
                    byte[] buffer = new byte[8192];
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (output.Length + read > entry.Length)
                            throw new InvalidDataException("Archive size mismatch");
                        output.Write(buffer, 0, read);
                    }

                    if (output.Length != entry.Length)
                        throw new InvalidDataException("Archive truncated");
                    data[leaf] = output.ToArray();
                }
            }
        }

        ValidatePayload(data);
        return data;
    }

    internal static bool IsInstalled(string paks)
    {
        foreach (string folder in LoaderFolders(paks))
        {
            if (!Files.All(f => File.Exists(Path.Combine(folder, f))))
                continue;
            try
            {
                var data = Files.ToDictionary(f => f, f => ReadBounded(Path.Combine(folder, f), MaxPayload));
                ValidatePayload(data);
                return true;
            }
            catch (InvalidDataException)
            {
            }
            catch (IOException)
            {
            }
        }

        return false;
    }

    // 限制单文件读取大小，避免无界处理异常下载。
    static byte[] ReadBounded(string path, int limit)
    {
        if (new FileInfo(path).Length > limit)
            throw new InvalidDataException("File too large");
        return File.ReadAllBytes(path);
    }

    // 匹配加载器下载包文件名。
    internal static bool ArchiveName(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path).Replace(" ", "").Replace("_", "").ToLowerInvariant();
        return String.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase) && (name == "blueprintloader" || name.StartsWith("blueprintloader-")) && !name.Contains("template");
    }

    // 从已指定/下载/缓存来源寻找经过校验的完整 ZIP。
    internal static string FindArchive(string downloads, DateTime notBefore)
    {
        if (!Directory.Exists(downloads))
            return null;
        foreach (string path in Directory.GetFiles(downloads, "*.zip").Where(ArchiveName).Where(p => File.GetLastWriteTimeUtc(p) >= notBefore).OrderByDescending(File.GetLastWriteTimeUtc).Take(20))
        {
            try
            {
                ReadArchive(path);
                return path;
            }
            catch (IOException)
            {
            }
            catch (InvalidDataException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return null;
    }

    // 判断当前运行进程是否阻止组件更新。
    static bool Blocked()
    {
        if (GameProcess.IsRunning())
            return true;
        var processes = Process.GetProcessesByName("MinecraftLauncher");
        try
        {
            return processes.Length > 0;
        }
        finally
        {
            foreach (var p in processes)
                p.Dispose();
        }
    }

    // 要求游戏进程结束后才能修改加载器或组件文件。
    static void RequireStopped(Func<bool> blocked)
    {
        if (blocked())
            throw new IOException(L10n.T("请完全退出游戏和 Minecraft Launcher，再安装蓝图加载器"));
    }

    // 先检查归属并备份旧文件，再安装已验证下载内容。
    internal static string InstallPayload(string paks, Dictionary<string, byte[]> data, string backups, Func<bool> blocked)
    {
        RequireStopped(blocked);
        ValidatePayload(data);
        string full = Path.GetFullPath(paks), target = Path.Combine(full, "~mods", "BlueprintLoader");
        var existing = LoaderFolders(full).ToArray();
        if (existing.Length > 1)
            throw new IOException(L10n.T("检测到多个不完整的蓝图加载器，请先检查模组目录"));
        if (existing.Length == 1)
            target = existing[0];
        if (!target.StartsWith(full + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Invalid loader directory");
        for (var dir = new DirectoryInfo(target); dir != null; dir = dir.Parent)
        {
            if (dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Loader directory is a link");
            if (String.Equals(dir.FullName, full, StringComparison.OrdinalIgnoreCase))
                break;
        }

        var old = new Dictionary<string, byte[]>();
        foreach (string f in Files)
        {
            string path = Path.Combine(target, f);
            if (File.Exists(path))
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Loader file is a link");
                old[f] = ReadBounded(path, MaxPayload);
            }
        }

        if (old.Count > 0)
        {
            string backup = Path.Combine(backups, "BlueprintLoader_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backup);
            foreach (var row in old)
                File.WriteAllBytes(Path.Combine(backup, row.Key), row.Value);
        }

        RequireStopped(blocked);
        Directory.CreateDirectory(target);
        string stage = Path.Combine(target, ".toolbox-loader-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        var changed = new List<string>();
        try
        {
            foreach (string file in Files)
                File.WriteAllBytes(Path.Combine(stage, file), data[file]);
            RequireStopped(blocked);
            foreach (string file in Files)
            {
                string path = Path.Combine(target, file);
                changed.Add(file);
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(Path.Combine(stage, file), path);
            }

            foreach (string file in Files)
                if (Hash(File.ReadAllBytes(Path.Combine(target, file))) != Hash(data[file]))
                    throw new IOException("Loader verification failed");
        }
        catch
        {
            foreach (string file in changed)
            {
                string path = Path.Combine(target, file);
                if (old.ContainsKey(file))
                    File.WriteAllBytes(path, old[file]);
                else if (File.Exists(path))
                    File.Delete(path);
            }

            throw;
        }
        finally
        {
            foreach (string path in Directory.GetFiles(stage))
                File.Delete(path);
            Directory.Delete(stage);
        }

        return target;
    }

    // 确保加载器已存在，缺失时从完整下载包安装。
    public static void Ensure(string paks, Func<string> acquire = null)
    {
        if (IsInstalled(paks))
            return;
        RequireStopped(Blocked);
        string archive = null;
        if (File.Exists(Cache))
            try
            {
                ReadArchive(Cache);
                archive = Cache;
            }
            catch (InvalidDataException)
            {
            }
            catch (IOException)
            {
            }

        if (archive == null)
            archive = FindArchive(Downloads(), DateTime.MinValue);
        if (archive == null && acquire != null)
            archive = acquire();
        if (String.IsNullOrEmpty(archive))
            throw new OperationCanceledException(L10n.T("缺少蓝图加载器：请从官网下载安装包后重试"));
        var payload = ReadArchive(archive);
        string target = InstallPayload(paks, payload, Path.Combine(Path.GetDirectoryName(ToolboxSettings.ConfigPath), "component-backups"), Blocked);
        ToolboxLog.Write("Dependency.BlueprintLoader", "Installed and verified three loader files from user download");
        try
        {
            if (!String.Equals(Path.GetFullPath(archive), Path.GetFullPath(Cache), StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Cache));
                File.Copy(archive, Cache, true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        if (!IsInstalled(paks))
            throw new IOException("Loader verification failed");
    }

    // 把加载器检测/作者下载页和安装流程接入组件安装界面。
    public static void EnsureForUi(string root, IWin32Window owner)
    {
        Ensure(Path.Combine(root, "Dungeons", "Content", "Paks"), delegate
        {
            using (var dialog = new BlueprintLoaderDownloadForm())
                return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.ArchivePath : null;
        });
    }
}

sealed class BlueprintLoaderDownloadForm : Form
{
    internal string ArchivePath;
    readonly Label note;
    readonly Timer timer = new Timer();
    readonly DateTime started = DateTime.UtcNow;
    bool scanning;
    public BlueprintLoaderDownloadForm(bool startDownload = true)
    {
        Text = L10n.T("自动安装蓝图加载器");
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(650, 250);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = OreTheme.Background;
        ForeColor = OreTheme.Text;
        note = new PixelLabel
        {
            Location = new Point(20, 18),
            Size = new Size(610, 105),
            Text = L10n.T("未检测到完整蓝图加载器。\n请在官网登录并下载 Blueprint Loader（不是开发模板）。\n下载完成后自动识别、安装，再继续安装组件。")
        };
        Controls.Add(note);
        var web = new OreButton
        {
            Text = L10n.T("打开官方下载页"),
            Location = new Point(20, 143),
            Size = new Size(200, 40)
        };
        web.Click += delegate
        {
            OpenSource();
        };
        Controls.Add(web);
        var local = new OreButton
        {
            Text = L10n.T("选择已下载 ZIP"),
            Location = new Point(232, 143),
            Size = new Size(200, 40)
        };
        local.Click += delegate
        {
            using (var picker = new OpenFileDialog
            {
                Filter = "ZIP (*.zip)|*.zip",
                Title = L10n.T("选择 Blueprint Loader 安装包")
            }

            )
                if (picker.ShowDialog(this) == DialogResult.OK)
                    try
                    {
                        BlueprintLoaderInstaller.ReadArchive(picker.FileName);
                        ArchivePath = picker.FileName;
                        DialogResult = DialogResult.OK;
                    }
                    catch (Exception e)
                    {
                        note.Text = e.Message;
                    }
        };
        Controls.Add(local);
        var cancel = new OreButton
        {
            Text = L10n.T("取消"),
            Location = new Point(444, 143),
            Size = new Size(185, 40),
            DialogResult = DialogResult.Cancel
        };
        Controls.Add(cancel);
        CancelButton = cancel;
        Controls.Add(new Label { Location = new Point(20, 200), Size = new Size(610, 36), Text = L10n.T("正在监测系统下载文件夹；其他保存位置可点击选择 ZIP。") });
        timer.Interval = 1500;
        timer.Tick += async delegate
        {
            if (scanning)
                return;
            scanning = true;
            try
            {
                string path = await Task.Run(() => BlueprintLoaderInstaller.FindArchive(BlueprintLoaderInstaller.Downloads(), started.AddSeconds(-2)));
                if (!IsDisposed && path != null)
                {
                    ArchivePath = path;
                    DialogResult = DialogResult.OK;
                }
            }
            catch (Exception e)
            {
                if (!IsDisposed)
                    note.Text = e.Message;
            }
            finally
            {
                scanning = false;
            }
        };
        foreach (Control control in Controls)
        {
            var button = control as Button;
            if (button != null)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.BackColor = OreTheme.Field;
                button.ForeColor = OreTheme.Text;
                button.FlatAppearance.BorderColor = OreTheme.Edge;
            }
        }

        Shown += delegate
        {
            if (startDownload)
            {
                timer.Start();
                OpenSource();
            }
        };
        FormClosed += delegate
        {
            timer.Stop();
        };
    }

    void OpenSource()
    {
        try
        {
            Process.Start(new ProcessStartInfo(BlueprintLoaderInstaller.Source) { UseShellExecute = true });
        }
        catch (Exception)
        {
            note.Text = L10n.T("无法打开浏览器，请手动访问 Nexus Mods 的 Blueprint Loader 页面，或选择已下载 ZIP。");
        }
    }

    // 释放本对象拥有的句柄、绘图对象或监听资源，避免退出后继续占用。
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            timer.Dispose();
        base.Dispose(disposing);
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 从进程、Steam 库、注册表与本地缓存识别安装根目录。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

sealed class GameInstallResult
{
    public string Root, Source;
    public string[] Candidates;
}

// 只读取已登记安装，每个候选都必须包含真实游戏目录结构。
static class GameInstallLocator
{
    // 上次验证安装目录的本地缓存，仅缓存有效根目录。
    static string CachePath
    {
        get
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MinecraftDungeons2Toolbox", "last-game-directory.txt");
        }
    }

    static readonly Regex pairs = new Regex("\"(?<key>(?:\\\\.|[^\"\\\\])*)\"\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"");
    // 还原 Steam VDF 的反斜杠/引号转义，不能作为 Shell 转义使用。
    static string Unescape(string value)
    {
        return Regex.Replace(value, @"\\([\\" + "\"" + @"])", m => m.Groups[1].Value);
    }

    static IEnumerable<KeyValuePair<string, string>> Pairs(string text)
    {
        foreach (Match m in pairs.Matches(text ?? ""))
            yield return new KeyValuePair<string, string>(Unescape(m.Groups["key"].Value), Unescape(m.Groups["value"].Value));
    }

    // 只读有界小文件，权限或文件缺失时返回空候选。
    static string ReadSmall(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > 1024 * 1024)
                return "";
            return File.ReadAllText(path);
        }
        catch (IOException)
        {
            return "";
        }
        catch (UnauthorizedAccessException)
        {
            return "";
        }
        catch (System.Security.SecurityException)
        {
            return "";
        }
    }

    // 从白名单 EXE 和 Binaries 层级反推游戏根目录。
    internal static string RootFromExecutable(string executable)
    {
        try
        {
            string path = Path.GetFullPath(executable);
            if (!GameProcess.Supported(Path.GetFileNameWithoutExtension(path)))
                return null;
            var folder = new DirectoryInfo(Path.GetDirectoryName(path));
            if (folder.Parent == null || folder.Parent.Parent == null || folder.Parent.Parent.Parent == null)
                return null;
            if (!String.Equals(folder.Parent.Name, "Binaries", StringComparison.OrdinalIgnoreCase) || !String.Equals(folder.Parent.Parent.Name, "Dungeons", StringComparison.OrdinalIgnoreCase))
                return null;
            if (!String.Equals(folder.Name, "Win64", StringComparison.OrdinalIgnoreCase) && !String.Equals(folder.Name, "WinGDK", StringComparison.OrdinalIgnoreCase))
                return null;
            return folder.Parent.Parent.Parent.FullName;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (PathTooLongException)
        {
            return null;
        }
    }

    // 校验对象或数据是否满足当前模块的使用前提。
    static string[] Valid(IEnumerable<string> roots, Func<string, bool> directoryExists, Func<string, bool> fileExists)
    {
        var result = new List<string>();
        foreach (string candidate in roots)
        {
            if (String.IsNullOrWhiteSpace(candidate))
                continue;
            try
            {
                result.Add(GameProcess.InstallRoot(candidate, directoryExists, fileExists).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
            catch (Exception e)
            {
                if (!(e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException || e is ArgumentException || e is NotSupportedException) && e.GetType() != typeof(Exception))
                    throw;
            }
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    // 按运行进程、已验证缓存、注册安装顺序选择候选，多候选交给用户选择。
    internal static GameInstallResult Resolve(IEnumerable<string> running, string cached, IEnumerable<string> registered, Func<string, bool> directoryExists, Func<string, bool> fileExists)
    {
        string[] roots = Valid(running, directoryExists, fileExists);
        if (roots.Length > 0)
            return new GameInstallResult
            {
                Root = roots.Length == 1 ? roots[0] : null,
                Source = "running game",
                Candidates = roots
            };
        roots = Valid(new[] { cached }, directoryExists, fileExists);
        if (roots.Length == 1)
            return new GameInstallResult
            {
                Root = roots[0],
                Source = "last verified game directory",
                Candidates = roots
            };
        roots = Valid(registered, directoryExists, fileExists);
        return new GameInstallResult
        {
            Root = roots.Length == 1 ? roots[0] : null,
            Source = "registered installation",
            Candidates = roots
        };
    }

    // 解析 Steam 库配置，支持游戏安装在其他磁盘。
    internal static string[] SteamLibraries(string steamRoot, string text)
    {
        var result = new List<string>
        {
            steamRoot
        };
        foreach (var pair in Pairs(text))
        {
            int index;
            if (pair.Key == "path" || Int32.TryParse(pair.Key, out index))
            {
                try
                {
                    if (Path.IsPathRooted(pair.Value))
                        result.Add(Path.GetFullPath(pair.Value));
                }
                catch (ArgumentException)
                {
                }
                catch (NotSupportedException)
                {
                }
                catch (PathTooLongException)
                {
                }
            }
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    // 从应用清单定位真实游戏目录。
    internal static string SteamGameRoot(string library, string manifest)
    {
        string[] titles = Pairs(manifest).Where(p => p.Key == "name").Select(p => p.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (titles.Length != 1 || !TargetTitle(titles[0]))
            return null;
        string[] dirs = Pairs(manifest).Where(p => p.Key == "installdir").Select(p => p.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (dirs.Length != 1)
            return null;
        string dir = dirs[0];
        if (String.IsNullOrWhiteSpace(dir) || dir == "." || dir == ".." || dir.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return null;
        return Path.Combine(library, "steamapps", "common", dir);
    }

    // 核对已登记应用的标题，避免混淆其他 Minecraft 产品。
    static bool TargetTitle(string title)
    {
        return String.Equals((title ?? "").Trim(), "Minecraft Dungeons II", StringComparison.OrdinalIgnoreCase);
    }

    // 两款游戏都使用 Dungeons-Win64-Shipping 名称；需要拒绝已识别的原作 Steam 安装。
    static bool KnownSteamOriginal(string root)
    {
        if (String.IsNullOrWhiteSpace(root))
            return false;
        try
        {
            var common = new DirectoryInfo(root).Parent;
            if (common == null || common.Parent == null || !String.Equals(common.Name, "common", StringComparison.OrdinalIgnoreCase) || !String.Equals(common.Parent.Name, "steamapps", StringComparison.OrdinalIgnoreCase))
                return false;
            foreach (string path in Directory.GetFiles(common.Parent.FullName, "appmanifest_*.acf", SearchOption.TopDirectoryOnly))
            {
                var fields = Pairs(ReadSmall(path)).ToArray();
                if (fields.Any(p => p.Key == "installdir" && String.Equals(p.Value, Path.GetFileName(root), StringComparison.OrdinalIgnoreCase)) && fields.Any(p => p.Key == "name" && String.Equals(p.Value.Trim(), "Minecraft Dungeons", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (System.Security.SecurityException)
        {
        }
        catch (ArgumentException)
        {
        }
        catch (NotSupportedException)
        {
        }

        return false;
    }

    // 只读取得安装注册项，缺失时保留为空候选。
    static string RegistryValue(RegistryHive hive, RegistryView view, string path, string value)
    {
        try
        {
            using (var root = RegistryKey.OpenBaseKey(hive, view))
            using (var key = root.OpenSubKey(path))
                return key == null ? null : key.GetValue(value) as string;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (System.Security.SecurityException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    // 收集 Steam 根目录来源，不自行扫描任意磁盘。
    static IEnumerable<string> SteamRoots()
    {
        var roots = new List<string>();
        foreach (var hive in new[]
        {
            RegistryHive.CurrentUser,
            RegistryHive.LocalMachine
        }

        )
            foreach (var view in new[]
            {
                RegistryView.Registry64,
                RegistryView.Registry32
            }

            )
            {
                roots.Add(RegistryValue(hive, view, @"SOFTWARE\Valve\Steam", "SteamPath"));
                roots.Add(RegistryValue(hive, view, @"SOFTWARE\Valve\Steam", "InstallPath"));
            }

        foreach (string basePath in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
        }

        )
            if (!String.IsNullOrEmpty(basePath))
                roots.Add(Path.Combine(basePath, "Steam"));
        return roots.Where(p => !String.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    // 收集已登记 Steam/Xbox 等安装候选。
    static IEnumerable<string> RegisteredRoots()
    {
        var candidates = new List<string>();
        foreach (string steam in SteamRoots())
            foreach (string library in SteamLibraries(steam, ReadSmall(Path.Combine(steam, "steamapps", "libraryfolders.vdf"))))
            {
                try
                {
                    string apps = Path.Combine(library, "steamapps");
                    if (!Directory.Exists(apps))
                        continue;
                    foreach (string manifest in Directory.GetFiles(apps, "appmanifest_*.acf", SearchOption.TopDirectoryOnly))
                        candidates.Add(SteamGameRoot(library, ReadSmall(manifest)));
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (System.Security.SecurityException)
                {
                }
            }

        foreach (var hive in new[]
        {
            RegistryHive.CurrentUser,
            RegistryHive.LocalMachine
        }

        )
            foreach (var view in new[]
            {
                RegistryView.Registry64,
                RegistryView.Registry32
            }

            )
            {
                try
                {
                    using (var root = RegistryKey.OpenBaseKey(hive, view))
                    using (var uninstall = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        if (uninstall == null)
                            continue;
                        foreach (string name in uninstall.GetSubKeyNames())
                        {
                            try
                            {
                                using (var key = uninstall.OpenSubKey(name))
                                {
                                    if (key != null && TargetTitle(key.GetValue("DisplayName") as string))
                                        candidates.Add(key.GetValue("InstallLocation") as string);
                                }
                            }
                            catch (UnauthorizedAccessException)
                            {
                            }
                            catch (System.Security.SecurityException)
                            {
                            }
                            catch (IOException)
                            {
                            }
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (System.Security.SecurityException)
                {
                }
                catch (IOException)
                {
                }
            }

        return candidates;
    }

    // 从实际游戏进程的 EXE 解析经校验安装根目录。
    static IEnumerable<string> RunningRoots()
    {
        var roots = new List<string>();
        foreach (string name in new[]
        {
            "Dungeons-Win64-Shipping",
            "Dungeons-WinGDK-Shipping"
        }

        )
            foreach (Process p in Process.GetProcessesByName(name))
            {
                using (p)
                {
                    try
                    {
                        string root = RootFromExecutable(p.MainModule.FileName);
                        if (!KnownSteamOriginal(root))
                            roots.Add(root);
                    }
                    catch (System.ComponentModel.Win32Exception)
                    {
                    }
                    catch (InvalidOperationException)
                    {
                    }
                    catch (NotSupportedException)
                    {
                    }
                }
            }

        return roots;
    }

    // 只检查注册安装来源，用于游戏退出后的安装识别。
    internal static GameInstallResult DiscoverRegistered()
    {
        return Resolve(new string[0], null, RegisteredRoots(), Directory.Exists, File.Exists);
    }

    // 综合运行、缓存及注册来源发现游戏目录。
    internal static GameInstallResult Discover()
    {
        string cached = ReadSmall(CachePath).Trim();
        if (KnownSteamOriginal(cached))
            cached = null;
        var preferred = Resolve(RunningRoots(), cached, new string[0], Directory.Exists, File.Exists);
        return preferred.Candidates.Length > 0 ? preferred : DiscoverRegistered();
    }

    // 只缓存经过真实布局校验的根目录。
    internal static void Remember(string root)
    {
        try
        {
            string[] valid = Valid(new[] { root }, Directory.Exists, File.Exists);
            if (valid.Length != 1)
                return;
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath));
            File.WriteAllText(CachePath, valid[0], Encoding.UTF8);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (System.Security.SecurityException)
        {
        }
        catch (ArgumentException)
        {
        }
    }

    // 从指定进程取得并记住有效安装目录。
    internal static void RememberProcess(Process process)
    {
        try
        {
            string root = RootFromExecutable(process.MainModule.FileName);
            if (root != null && !KnownSteamOriginal(root) && Valid(new[] { root }, Directory.Exists, File.Exists).Length == 1)
                Remember(root);
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (NotSupportedException)
        {
        }
    }

    // 尝试从当前游戏进程更新目录缓存。
    internal static void RememberRunning()
    {
        string[] roots = Valid(RunningRoots(), Directory.Exists, File.Exists);
        if (roots.Length == 1)
            Remember(roots[0]);
    }

    // 单一候选直接采用，多候选或缺失时展示目录选择。
    internal static string Choose(IWin32Window owner)
    {
        var found = Discover();
        if (found.Root != null)
        {
            ToolboxLog.Write("Component.GameDirectory", "auto-detected via " + found.Source + ": " + found.Root);
            return found.Root;
        }

        using (var dialog = new FolderBrowserDialog
        {
            Description = L10n.T(found.Candidates.Length > 1 ? "检测到多个游戏目录，请选择要安装组件的目录" : "未自动找到游戏目录，请选择包含 Dungeons 文件夹的安装目录"),
            ShowNewFolderButton = false
        }

        )
        {
            if (found.Candidates.Length > 0)
                dialog.SelectedPath = found.Candidates[0];
            if (dialog.ShowDialog(owner) != DialogResult.OK)
                return null;
            return GameProcess.InstallRoot(dialog.SelectedPath, Directory.Exists, File.Exists);
        }
    }

}

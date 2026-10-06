// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// 三类组件共享一次预检、备份和安装；装备组件始终使用内嵌预编译资源。
static class ComponentBundleInstaller
{
    // 组件名与内嵌资源和 ~mods 目录一致；新增类型须同步三件套、归属和回滚计划。
    internal static readonly string[] Components =
    {
        "MCD2NearbyLootBridge",
        "MCD2CombatBridge",
        "MCD2EquipmentBridge"
    };
    static readonly string[] Extensions =
    {
        ".pak",
        ".utoc",
        ".ucas"
    };
    // 记录本工具拥有的文件哈希；缺失或外部修改时保留文件并停止覆盖。
    const string Manifest = "toolbox-component.json";
    // 保存每个组件的安装计划及旧内容，失败时同时恢复文件和归属清单。
    sealed class Plan
    {
        public string Name, Folder;
        public bool Existed;
        public Dictionary<string, byte[]> Old = new Dictionary<string, byte[]>();
        public Dictionary<string, byte[]> Next = new Dictionary<string, byte[]>();
    }

    // 哈希比较只用于确认本工具拥有的文件；外部修改不会被强行覆盖。
    internal static string Hash(byte[] bytes)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
    }

    // 只检查 Paks 内的安装路径，与加载器保持相同边界。
    // 目录链接检查仅限 Xbox 游戏目录，不追溯到盘符根目录。
    // Paks、~mods 和组件目录仍拒绝链接；安装预检、写入及回滚共用此保护。
    static void CheckPath(string paks, string folder)
    {
        string root = Path.GetFullPath(paks).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string target = Path.GetFullPath(folder);
        // 带分隔符比较，避免相邻目录（例如 Paks-other）被误认为位于 Paks 内。
        if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException(L10n.T("组件安装路径包含目录链接，请选择实际游戏目录"));
        for (string path = target; path != null; path = Path.GetDirectoryName(path))
        {
            if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException(L10n.T("组件安装路径包含目录链接，请选择实际游戏目录"));
            if (String.Equals(path, root, StringComparison.OrdinalIgnoreCase))
                return;
        }

        throw new IOException(L10n.T("组件安装路径包含目录链接，请选择实际游戏目录"));
    }

    // 构造全部计划后再写入；一个组件归属不明时，其他组件也不会先被更新。
    static List<Plan> Prepare(string paks, Func<string, byte[]> resource)
    {
        var plans = new List<Plan>();
        var json = new JavaScriptSerializer();
        foreach (string name in Components)
        {
            var plan = new Plan
            {
                Name = name,
                Folder = Path.Combine(paks, "~mods", name)
            };
            CheckPath(paks, plan.Folder);
            plan.Existed = Directory.Exists(plan.Folder);
            string manifest = Path.Combine(plan.Folder, Manifest);
            Dictionary<string, string> ownership = null;
            if (plan.Existed)
            {
                if (!File.Exists(manifest))
                    throw new IOException(String.Format(L10n.T("组件目录没有归属清单，已保留原文件：{0}"), name));
                if ((File.GetAttributes(manifest) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException(String.Format(L10n.T("组件清单包含文件链接，已停止：{0}"), name));
                plan.Old[Manifest] = File.ReadAllBytes(manifest);
                ownership = json.Deserialize<Dictionary<string, string>>(File.ReadAllText(manifest));
                if (ownership == null)
                    throw new IOException(String.Format(L10n.T("组件归属清单无效：{0}"), name));
            }

            foreach (string extension in Extensions)
            {
                string file = name + "_P" + extension;
                byte[] bytes = resource(file);
                if (bytes == null || bytes.Length == 0)
                    throw new IOException(String.Format(L10n.T("内嵌组件不完整：{0}"), file));
                plan.Next[file] = bytes;
                if (plan.Existed)
                {
                    string path = Path.Combine(plan.Folder, file);
                    if (!File.Exists(path) || !ownership.ContainsKey(file))
                        throw new IOException(String.Format(L10n.T("已安装组件不完整，已保留原文件：{0}"), file));
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException(String.Format(L10n.T("组件文件包含链接，已停止：{0}"), file));
                    byte[] old = File.ReadAllBytes(path);
                    if (!String.Equals(Hash(old), ownership[file], StringComparison.OrdinalIgnoreCase))
                        throw new IOException(String.Format(L10n.T("组件文件被外部修改，未覆盖：{0}"), file));
                    plan.Old[file] = old;
                }
            }

            plan.Next[Manifest] = System.Text.Encoding.UTF8.GetBytes(json.Serialize(plan.Next.ToDictionary(x => x.Key, x => Hash(x.Value))));
            plans.Add(plan);
        }

        return plans;
    }

    // 从 EXE 读取组件，不重编译、不替换为旧的出售源码 stub。
    static byte[] Resource(string file)
    {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(file))
        {
            if (stream == null)
                return null;
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }
    }

    // 一次安装收集、战斗、装备组件；加载器由原有官网获取流程处理。
    public static string Install(string root, Action ensureLoader = null)
    {
        root = GameProcess.InstallRoot(root, Directory.Exists, File.Exists);
        string paks = Path.Combine(root, "Dungeons", "Content", "Paks");
        string backup = Path.Combine(Path.GetDirectoryName(ToolboxSettings.ConfigPath), "component-backups", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "-" + Guid.NewGuid().ToString("N"));
        return InstallAt(paks, backup, Resource, GameProcess.IsRunning, ensureLoader ?? (() => BlueprintLoaderInstaller.Ensure(paks)), null);
    }

    // 文件事务也用于离线故障注入；写入前校验所有资源、归属和游戏退出状态。
    internal static string InstallAt(string paks, string backup, Func<string, byte[]> resource, Func<bool> running, Action loader, Action<int> afterWrite)
    {
        Action guard = () =>
        {
            if (running())
                throw new IOException(L10n.T("请完全退出游戏，再安装或更新组件"));
        };
        guard();
        var plans = Prepare(paks, resource);
        loader();
        guard();
        foreach (var plan in plans.Where(x => x.Old.Count > 0))
        {
            string folder = Path.Combine(backup, plan.Name);
            Directory.CreateDirectory(folder);
            foreach (var file in plan.Old)
                File.WriteAllBytes(Path.Combine(folder, file.Key), file.Value);
        }

        var touched = new List<Plan>();
        int count = 0;
        try
        {
            foreach (var plan in plans)
            {
                guard();
                CheckPath(paks, plan.Folder);
                if (!plan.Existed && Directory.Exists(plan.Folder))
                    throw new IOException(String.Format(L10n.T("安装期间出现外部组件目录，未覆盖：{0}"), plan.Name));
                // 再次核对旧内容，拒绝预检后被外部程序改写的文件。
                foreach (var old in plan.Old)
                    if ((File.GetAttributes(Path.Combine(plan.Folder, old.Key)) & FileAttributes.ReparsePoint) != 0 || !File.ReadAllBytes(Path.Combine(plan.Folder, old.Key)).SequenceEqual(old.Value))
                        throw new IOException(String.Format(L10n.T("安装期间组件发生变化，已停止：{0}"), plan.Name));
                touched.Add(plan);
                Directory.CreateDirectory(plan.Folder);
                foreach (var file in plan.Next)
                {
                    guard();
                    File.WriteAllBytes(Path.Combine(plan.Folder, file.Key), file.Value);
                    if (afterWrite != null)
                        afterWrite(++count);
                }
            }

            foreach (var plan in plans)
                foreach (var file in plan.Next)
                    if (!File.ReadAllBytes(Path.Combine(plan.Folder, file.Key)).SequenceEqual(file.Value))
                        throw new IOException(String.Format(L10n.T("安装后哈希核验失败：{0}"), file.Key));
        }
        catch (Exception error)
        {
            // 游戏意外启动时停止写入，旧内容仍保存在备份目录供退出后恢复。
            var rollbackErrors = new List<string>();
            if (!running())
                foreach (var plan in touched)
                {
                    foreach (var file in plan.Next.Keys)
                        try
                        {
                            guard();
                            CheckPath(paks, plan.Folder);
                            string path = Path.Combine(plan.Folder, file);
                            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                                throw new IOException(path);
                            if (plan.Old.ContainsKey(file))
                                File.WriteAllBytes(path, plan.Old[file]);
                            else if (File.Exists(path))
                                File.Delete(path);
                        }
                        catch (Exception rollbackError)
                        {
                            rollbackErrors.Add(rollbackError.Message);
                        }

                    try
                    {
                        if (!plan.Existed && !Directory.EnumerateFileSystemEntries(plan.Folder).Any())
                            Directory.Delete(plan.Folder);
                    }
                    catch (Exception rollbackError)
                    {
                        rollbackErrors.Add(rollbackError.Message);
                    }
                }

            string details = error.Message;
            if (rollbackErrors.Count > 0)
                details += "\n" + String.Join("\n", rollbackErrors);
            throw new IOException(String.Format(L10n.T("组件安装未完成：{0}\n旧文件备份：{1}"), details, backup), error);
        }

        return backup;
    }
}

// 所有安装入口共用组件管理页；自动识别目录只执行一次。
sealed partial class ToolboxForm
{
    PixelLabel componentInstallNote;
    Button componentInstallButton;
    // 安装前停止全部功能，现有加载器获取窗口仍可选择下载包或官网来源。
    void InstallAllComponents()
    {
        Arm(false);
        GameInstallLocator.RememberRunning();
        componentInstallButton.Enabled = false;
        try
        {
            if (GameProcess.IsRunning())
                throw new IOException(L10n.T("请完全退出游戏，再安装或更新组件"));
            string root = GameInstallLocator.Choose(this);
            if (root == null)
                return;
            string backup = ComponentBundleInstaller.Install(root, () => BlueprintLoaderInstaller.EnsureForUi(root, this));
            GameInstallLocator.Remember(root);
            componentInstallNote.Text = L10n.T("全部组件已安装，请重启游戏后连接") + "\n" + root;
            nativeCombatNote.Text = nearbyLootNote.Text = equipmentState.Text = L10n.T("组件已更新，请重启游戏后连接");
            ToolboxLog.Write("Components.Install", "root=" + root + "; backup=" + backup);
        }
        catch (Exception e)
        {
            componentInstallNote.Text = e.Message;
            ToolboxLog.Error("Components.Install", e);
        }
        finally
        {
            componentInstallButton.Enabled = true;
        }
    }
}

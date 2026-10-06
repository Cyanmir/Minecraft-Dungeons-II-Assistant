// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 安装内嵌原生战斗组件并保留旧版本备份。
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Windows.Forms;

static class CombatComponentInstaller
{
    static readonly string[] files =
    {
        "MCD2CombatBridge_P.pak",
        "MCD2CombatBridge_P.utoc",
        "MCD2CombatBridge_P.ucas"
    };
    // 计算文件 SHA256，供安装校验及备份归属检查。
    static string Hash(byte[] data)
    {
        using (var s = SHA256.Create())
            return BitConverter.ToString(s.ComputeHash(data)).Replace("-", "");
    }

    // 从内嵌资源取得完整组件三件套，检查游戏已退出并备份后安装。
    public static string Install(string gameRoot)
    {
        if (GameProcess.IsRunning())
            throw new Exception(L10n.T("请先退出游戏，再安装或更新原生战斗组件"));
        string root = GameProcess.InstallRoot(gameRoot, Directory.Exists, File.Exists);
        string paks = Path.Combine(root, "Dungeons", "Content", "Paks");
        BlueprintLoaderInstaller.Ensure(paks);
        if (GameProcess.IsRunning())
            throw new Exception(L10n.T("请先退出游戏，再安装组件"));
        string target = Path.GetFullPath(Path.Combine(paks, "~mods", "MCD2CombatBridge"));
        if (!target.StartsWith(Path.GetFullPath(paks) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Invalid component path");
        string manifest = Path.Combine(target, "toolbox-component.json");
        var json = new JavaScriptSerializer();
        var old = new Dictionary<string, byte[]>();
        if (Directory.Exists(target))
        {
            if (!File.Exists(manifest))
                throw new Exception(L10n.T("组件目录已有其他文件，未覆盖；请保留原文件并检查安装目录"));
            var ownership = json.Deserialize<Dictionary<string, string>>(File.ReadAllText(manifest));
            foreach (string file in files)
            {
                string path = Path.Combine(target, file);
                if (!File.Exists(path) || !ownership.ContainsKey(file))
                    throw new Exception("Incomplete installed component");
                byte[] data = File.ReadAllBytes(path);
                if (Hash(data) != ownership[file])
                    throw new Exception("Installed component was modified; files kept");
                old[file] = data;
            }
        }

        var next = new Dictionary<string, byte[]>();
        foreach (string file in files)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(file))
            {
                if (stream == null)
                    throw new Exception("Missing nearby collection component resource");
                using (var m = new MemoryStream())
                {
                    stream.CopyTo(m);
                    next[file] = m.ToArray();
                }
            }
        }

        string backup = Path.Combine(Path.GetDirectoryName(ToolboxSettings.ConfigPath), "component-backups", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
        if (old.Count > 0)
        {
            Directory.CreateDirectory(backup);
            foreach (var row in old)
                File.WriteAllBytes(Path.Combine(backup, row.Key), row.Value);
            File.Copy(manifest, Path.Combine(backup, "toolbox-component.json"));
        }

        Directory.CreateDirectory(target);
        try
        {
            foreach (var row in next)
                File.WriteAllBytes(Path.Combine(target, row.Key), row.Value);
            File.WriteAllText(manifest, json.Serialize(next.ToDictionary(r => r.Key, r => Hash(r.Value))));
        }
        catch
        {
            if (!GameProcess.IsRunning())
                foreach (var row in old)
                    File.WriteAllBytes(Path.Combine(target, row.Key), row.Value);
            throw;
        }

        return target;
    }
}

// 主窗口的一个 partial 部分；事件处理与异步任务共用主窗口状态，退出时统一清理。
sealed partial class ToolboxForm
{
    // 让 UI 选择经验证游戏目录，确保加载器存在后安装本页组件并报告结果。
    void InstallCombatComponent()
    {
        Arm(false);
        GameInstallLocator.RememberRunning();
        if (GameProcess.IsRunning())
        {
            nativeCombatNote.Text = L10n.T("请先退出游戏，再安装或更新原生战斗组件");
            return;
        }

        try
        {
            string root = GameInstallLocator.Choose(this);
            if (root == null)
                return;
            BlueprintLoaderInstaller.EnsureForUi(root, this);
            string target = CombatComponentInstaller.Install(root);
            GameInstallLocator.Remember(root);
            nativeCombatNote.Text = L10n.T("原生战斗组件已安装，请重启游戏后重新连接") + "\n" + String.Format(L10n.T("游戏目录：{0}"), root);
            ToolboxLog.Write("Combat.NativeInstall", target);
        }
        catch (Exception e)
        {
            nativeCombatNote.Text = e.Message;
            ToolboxLog.Error("Combat.NativeInstall", e);
        }
    }
}

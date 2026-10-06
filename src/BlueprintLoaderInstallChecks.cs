// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 中文维护说明：使用临时合成 ZIP 验证加载器安装规则。这里的 Test 是离线自检；修改安装器校验后同步维护有效/无效样例，不要用真实存档或游戏安装目录作为测试夹具。
// 源码导航和常改参数见 docs/maintenance.md；协议、反射标识及类型白名单修改前先核对两端。
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

// BlueprintLoaderInstallChecks 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
static class BlueprintLoaderInstallChecks
{
    // 检查本模块约束，不满足时抛出异常而不继续执行。
    static void Check(bool value, string reason)
    {
        if (!value)
            throw new Exception(reason);
    }

    // 确认无效输入被拒绝，用于维护拒绝分支的回归样例。
    static void Reject(Action action, string reason)
    {
        try
        {
            action();
        }
        catch (InvalidDataException)
        {
            return;
        }

        throw new Exception(reason);
    }

    // 在隔离临时目录创建合成下载包样例。
    static void Zip(string path, Dictionary<string, byte[]> payload)
    {
        using (var file = File.Create(path))
        using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            foreach (var row in payload)
                using (var output = zip.CreateEntry(row.Key).Open())
                    output.Write(row.Value, 0, row.Value.Length);
    }

    // 运行本模块的离线规则自检；返回 PASS 摘要，失败抛出异常供命令行报告。
    public static string Test()
    {
        string parent = Path.GetFullPath(Path.GetTempPath()), root = Path.Combine(parent, "MCD2-loader-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string paks = Path.Combine(root, "Dungeons", "Content", "Paks"), downloads = Path.Combine(root, "Downloads"), backups = Path.Combine(root, "backups");
            Directory.CreateDirectory(paks);
            Directory.CreateDirectory(downloads);
            var data = new Dictionary<string, byte[]>
            {
                {
                    BlueprintLoaderInstaller.Files[0],
                    new byte[]
                    {
                        0xE1,
                        0x12,
                        0x6F,
                        0x5A,
                        1
                    }
                },
                {
                    BlueprintLoaderInstaller.Files[1],
                    Encoding.ASCII.GetBytes("-==--==--==--==-")
                },
                {
                    BlueprintLoaderInstaller.Files[2],
                    new byte[]
                    {
                        1,
                        2,
                        3
                    }
                }
            };
            string archive = Path.Combine(downloads, "Blueprint Loader-2-2-1.zip");
            Zip(archive, data.ToDictionary(r => "BlueprintLoader/" + r.Key, r => r.Value));
            var read = BlueprintLoaderInstaller.ReadArchive(archive);
            Check(read.Count == 3 && read.All(r => r.Value.SequenceEqual(data[r.Key])), "Valid nested ZIP payload lost");
            Check(BlueprintLoaderInstaller.FindArchive(downloads, DateTime.MinValue) == archive, "Completed browser ZIP not detected");
            Check(!BlueprintLoaderInstaller.ArchiveName("BlueprintModTemplate.zip") && !BlueprintLoaderInstaller.ArchiveName("BlueprintLoader.zip.crdownload") && !BlueprintLoaderInstaller.ArchiveName("OtherLoader.zip"), "Wrong download matched");
            string wrong = Path.Combine(root, "wrong.zip");
            File.WriteAllText(wrong, "<html>Sign in</html>");
            Reject(() => BlueprintLoaderInstaller.ReadArchive(wrong), "Login HTML accepted");
            string missing = Path.Combine(root, "missing.zip");
            Zip(missing, data.Where(r => r.Key != BlueprintLoaderInstaller.Files[2]).ToDictionary(r => r.Key, r => r.Value));
            Reject(() => BlueprintLoaderInstaller.ReadArchive(missing), "Partial payload accepted");
            var bad = new Dictionary<string, byte[]>(data);
            bad[BlueprintLoaderInstaller.Files[1]] = new byte[]
            {
                1,
                2,
                3
            };
            Zip(wrong, bad);
            Reject(() => BlueprintLoaderInstaller.ReadArchive(wrong), "Corrupt UTOC accepted");
            bad = new Dictionary<string, byte[]>(data);
            bad["../escape.txt"] = new byte[]
            {
                1
            };
            Zip(wrong, bad);
            Reject(() => BlueprintLoaderInstaller.ReadArchive(wrong), "ZIP traversal accepted");
            bad = data.ToDictionary(r => "a/" + r.Key, r => r.Value);
            bad["b/" + BlueprintLoaderInstaller.Files[0]] = data[BlueprintLoaderInstaller.Files[0]];
            Zip(wrong, bad);
            Reject(() => BlueprintLoaderInstaller.ReadArchive(wrong), "Duplicate payload accepted");
            Check(!BlueprintLoaderInstaller.IsInstalled(paks), "Empty mod folder reported installed");
            string target = Path.Combine(paks, "~mods", "BlueprintLoader");
            Directory.CreateDirectory(target);
            File.WriteAllBytes(Path.Combine(target, BlueprintLoaderInstaller.Files[1]), data[BlueprintLoaderInstaller.Files[1]]);
            File.WriteAllText(Path.Combine(target, "keep.txt"), "unrelated");
            Check(!BlueprintLoaderInstaller.IsInstalled(paks), "UTOC-only false positive");
            bool rejected = false;
            try
            {
                BlueprintLoaderInstaller.InstallPayload(paks, data, backups, () => true);
            }
            catch (IOException)
            {
                rejected = true;
            }

            Check(rejected && !File.Exists(Path.Combine(target, BlueprintLoaderInstaller.Files[0])), "Running game allowed mutation");
            BlueprintLoaderInstaller.InstallPayload(paks, data, backups, () => false);
            Check(BlueprintLoaderInstaller.IsInstalled(paks), "Repair not detected");
            Check(File.ReadAllText(Path.Combine(target, "keep.txt")) == "unrelated", "Unrelated loader files replaced");
            Check(Directory.GetFiles(backups, BlueprintLoaderInstaller.Files[1], SearchOption.AllDirectories).Length == 1, "Partial install backup missing");
            int reads = 0;
            rejected = false;
            try
            {
                BlueprintLoaderInstaller.InstallPayload(paks, data, backups, () => ++reads >= 3);
            }
            catch (IOException)
            {
                rejected = true;
            }

            Check(rejected && BlueprintLoaderInstaller.IsInstalled(paks) && !Directory.GetDirectories(target, ".toolbox-loader-*").Any(), "Game starts during staging cleanup failed");
            string activeMods = Path.Combine(paks, "mods", "Nested");
            Directory.CreateDirectory(activeMods);
            foreach (string f in BlueprintLoaderInstaller.Files)
                File.Move(Path.Combine(target, f), Path.Combine(activeMods, f));
            Check(BlueprintLoaderInstaller.IsInstalled(paks), "Nested mods installation not detected");
            Check(!Directory.GetFiles(root, "escape.txt", SearchOption.AllDirectories).Any(), "ZIP escaped destination");
            return "PASS: isolated loader ZIP detection, complete triplet and binary headers, incomplete download/template/HTML/corrupt/missing/duplicate/traversal rejection, partial-install backup, unrelated file preservation, running-game/staging guards and nested mods detection. Synthetic fixtures only; no browser, network, game installation or real loader execution.\r\n";
        }
        finally
        {
            if (!root.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(root).StartsWith("MCD2-loader-test-", StringComparison.Ordinal))
                throw new Exception("Invalid test cleanup path");
            Directory.Delete(root, true);
        }
    }
}

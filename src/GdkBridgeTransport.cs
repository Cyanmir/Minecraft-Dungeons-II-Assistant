// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// Xbox/WinGDK 的私有 WGS 请求/回执传输。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

// 旧三字节诊断只允许显式调用；玩法固定邮箱由 NativeBridgeChannel 在包/布局和握手核验后处理。
static class GdkBridgeTransport
{
    internal const string RejectedCommand = "M2?";
    // Pair 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
    internal sealed class Pair
    {
        internal string Request, Receipt, Mod, Protocol, Root;
    }

    // 组件独立回执存档类；字段和序列化格式必须与工具端解析保持一致。
    internal sealed class Receipt
    {
        public string Instance, Status;
        public double Clock;
    }

    // Root 的只读/受控访问入口；使用该属性而不绕过访问器中的校验和更新逻辑。
    internal static string Root
    {
        get
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", "Microsoft.MinecraftDungeons2_8wekyb3d8bbwe");
        }
    }

    // 将文件错误转为不含个人路径的诊断文字。
    internal static string SafeError(Exception error)
    {
        if (error is FileNotFoundException || error is DirectoryNotFoundException)
            return "WGS component metadata/blob rotated or is missing; retry window exhausted";
        return System.Text.RegularExpressions.Regex.Replace(error.Message, @"[A-Za-z]:\\[^\r\n\""']+", "<local path>");
    }

    static byte[] Read(string path, int max)
    {
        var before = new FileInfo(path);
        long size = before.Length;
        var touched = before.LastWriteTimeUtc;
        if (size < 64 || size > max)
            throw new Exception("GDK component file size rejected");
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            var bytes = new byte[(int)size];
            int at = 0, n;
            while (at < bytes.Length && (n = file.Read(bytes, at, bytes.Length - at)) > 0)
                at += n;
            if (at != bytes.Length || file.Length != size)
                throw new IOException("GDK component file changed during read");
            var after = new FileInfo(path);
            if (after.Length != size || after.LastWriteTimeUtc != touched)
                throw new IOException("GDK component timestamp changed during read");
            return bytes;
        }
    }

    // 核对私有 Request/Receipt 保存类与容器元数据。
    static byte[] Metadata(string path, int max, string root)
    {
        GdkSaveStorage.RequireContained(path, root);
        var item = new FileInfo(path);
        long size = item.Length;
        var touched = item.LastWriteTimeUtc;
        if (size < 8 || size > max)
            throw new Exception("GDK metadata size rejected");
        byte[] bytes = File.ReadAllBytes(path);
        item = new FileInfo(path);
        if (bytes.Length != size || item.Length != size || item.LastWriteTimeUtc != touched)
            throw new IOException("GDK metadata changed during read");
        return bytes;
    }

    // 有界重试描述符滚动，取得同一用户/实例的唯一请求和回执关联。
    internal static Pair Resolve(string root, string mod, string protocolOverride = null)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return ResolveOnce(root, mod, protocolOverride);
            }
            catch (IOException e)
            {
                if (watch.ElapsedMilliseconds >= 1200)
                    throw new IOException(SafeError(e));
                Thread.Sleep(20);
            }
        }
    }

    // 解析一次当前 WGS 索引与描述符，不跨用户或容器拼接两份消息。
    static Pair ResolveOnce(string root, string mod, string protocolOverride)
    {
        string prefix, protocol;
        if (mod == "MCD2CombatBridge")
        {
            prefix = "MCD2Combat";
            protocol = "3";
        }
        else if (mod == "MCD2NearbyLootBridge")
        {
            prefix = "MCD2NearbyLoot";
            protocol = "5";
        }
        else
            throw new Exception("GDK write probe is restricted to the two captured components");
        if (protocolOverride != null)
        {
            if (mod == "MCD2CombatBridge" && protocolOverride != "5" || mod == "MCD2NearbyLootBridge" && protocolOverride != "6")
                throw new Exception("Unsupported fixed mailbox protocol");
            protocol = protocolOverride;
        }

        var paths = new Dictionary<string, List<string>>
        {
            {
                prefix + "Request",
                new List<string>()
            },
            {
                prefix + "Receipt",
                new List<string>()
            }
        };
        string wgs = Path.Combine(root, "SystemAppData", "wgs");
        if (!Directory.Exists(wgs))
            throw new Exception("No WGS storage for the validated package");
        GdkSaveStorage.RequireContained(wgs, root);
        string[] profiles = Directory.GetDirectories(wgs);
        if (profiles.Length > 64)
            throw new Exception("GDK profile count rejected");
        foreach (string profile in profiles)
        {
            if (Path.GetFileName(profile) == "t" || Path.GetFileName(profile).IndexOf("backup", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            GdkSaveStorage.RequireContained(profile, root);
            string indexPath = Path.Combine(profile, "containers.index");
            if (!File.Exists(indexPath))
                continue;
            var index = GdkSaveStorage.ParseIndex(Metadata(indexPath, 4 * 1024 * 1024, root));
            if (index.Version != 14)
                throw new Exception("WGS index version has not been captured/validated");
            foreach (var entry in index.Entries)
            {
                string slot = GdkSaveStorage.Slot(entry.Name);
                if (slot == null || !paths.ContainsKey(slot))
                    continue;
                string directory = GdkSaveStorage.ResolveGuid(profile, entry.Folder, root, true);
                if (directory == null)
                    throw new DirectoryNotFoundException("GDK component container is missing");
                string descriptor = Path.Combine(directory, "container." + entry.Revision);
                var blobs = GdkSaveStorage.ParseDescriptor(Metadata(descriptor, 8 + 128 * 160, root));
                if (blobs.Count != 1 || blobs[0].Name != "Data")
                    throw new Exception("GDK component blob layout differs from the captured report");
                var blob = blobs[0];
                string local = GdkSaveStorage.ResolveGuid(directory, blob.Local, root, false), cloud = GdkSaveStorage.ResolveGuid(directory, blob.Cloud, root, false);
                if (local != null && cloud != null && !String.Equals(local, cloud, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Ambiguous GDK local/cloud blob paths");
                string payload = local ?? cloud;
                if (payload == null)
                    throw new FileNotFoundException("GDK component blob is rotating or missing");
                paths[slot].Add(payload);
            }
        }

        if (paths.Values.Any(p => p.Count != 1))
            throw new Exception("GDK component slots are missing or ambiguous across profiles");
        var pair = new Pair
        {
            Root = root,
            Mod = mod,
            Protocol = protocol,
            Request = paths[prefix + "Request"][0],
            Receipt = paths[prefix + "Receipt"][0]
        };
        if (!String.Equals(Path.GetDirectoryName(Path.GetDirectoryName(pair.Request)), Path.GetDirectoryName(Path.GetDirectoryName(pair.Receipt)), StringComparison.OrdinalIgnoreCase))
            throw new Exception("GDK request/receipt belong to different profiles");
        EquipmentSaveCodec.Parse(Read(pair.Request, 65536), "Request", "Command", mod);
        EquipmentSaveCodec.Parse(Read(pair.Receipt, 65536), "Receipt", "Status", mod);
        return pair;
    }

    // 解析组件回执的协议、实例、场景、序号、状态和时间。
    internal static Receipt ParseReceipt(string text, string protocol)
    {
        var f = text.Split('|');
        int sequence;
        double clock;
        if (f.Length != 6 || f[0] != protocol || f[1].Length < 1 || f[1].Length > 160 || !Int32.TryParse(f[3], out sequence) || sequence < 0 || !Double.TryParse(f[5], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out clock) || Double.IsNaN(clock) || Double.IsInfinity(clock) || clock < 0)
            throw new Exception("Unsupported GDK component receipt");
        return new Receipt
        {
            Instance = f[1],
            Status = f[4],
            Clock = clock
        };
    }

    // 核对回执是否属于本次请求且仍在有效期内。
    internal static Receipt Fresh(Pair pair, DateTime gameStart)
    {
        GdkSaveStorage.RequireContained(pair.Receipt, pair.Root);
        DateTime touched = File.GetLastWriteTimeUtc(pair.Receipt);
        if (touched < gameStart || DateTime.UtcNow - touched > TimeSpan.FromSeconds(1.5) || touched - DateTime.UtcNow > TimeSpan.FromSeconds(1))
            throw new Exception("GDK receipt is not fresh for the running game");
        return ReadReceipt(pair);
    }

    internal static Receipt ReadReceipt(Pair pair)
    {
        GdkSaveStorage.RequireContained(pair.Receipt, pair.Root);
        return ParseReceipt(EquipmentSaveCodec.Parse(ReadReliable(pair.Receipt), "Receipt", "Status", pair.Mod).Value, pair.Protocol);
    }

    // 在允许的读重试范围内处理文件滚动和共享状态变化。
    internal static byte[] ReadReliable(string path)
    {
        // Stop 的场景核对也会读取回执；短暂文件占用不能跳过 OFF 清理。
        var watch = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return Read(path, 65536);
            }
            catch (IOException)
            {
                if (watch.ElapsedMilliseconds >= 1200)
                    throw;
                Thread.Sleep(20);
            }
        }
    }

    // 识别断续写入的命令，不能把半份数据交给游戏执行。
    static bool InterruptedCommand(string value)
    {
        return value != null && value.Length == 3 && Enumerable.Range(0, 3).All(i => value[i] == "OFF"[i] || value[i] == RejectedCommand[i]);
    }

    // 构造固定长度 Command 替换结果，保留原文件其他字节。
    internal static byte[] FixedReplacement(byte[] before, string expected, string next, string mod)
    {
        if ((expected != "OFF" && !(next == "OFF" && InterruptedCommand(expected))) || next != "OFF" && next != RejectedCommand || expected == next)
            throw new Exception("GDK probe command not allowed");
        var field = EquipmentSaveCodec.Parse(before, "Request", "Command", mod);
        if (field.Value != expected || field.PayloadSize != 8 || BitConverter.ToInt32(before, field.PayloadOffset) != 4)
            throw new Exception("GDK probe requires the exact captured three-byte ASCII command field");
        byte[] after = EquipmentSaveCodec.Replace(before, next, mod);
        if (after.Length != before.Length)
            throw new Exception("GDK probe must not resize the blob");
        for (int i = 0; i < before.Length; i++)
            if ((i < field.PayloadOffset + 4 || i >= field.PayloadOffset + 7) && before[i] != after[i])
                throw new Exception("GDK probe changed bytes outside its command");
        return after;
    }

    // 原位写入自有 Command 字节，不扩容、不改 WGS 元数据。
    internal static byte[] WriteFixed(Pair pair, string expected, string next)
    {
        GdkSaveStorage.RequireContained(pair.Request, pair.Root);
        byte[] before = Read(pair.Request, 65536), after = FixedReplacement(before, expected, next, pair.Mod);
        int position = EquipmentSaveCodec.Parse(before, "Request", "Command", pair.Mod).PayloadOffset + 4;
        // 保持同一 GUID 文件、长度、属性边界和云元数据，仅改变三个 ASCII 字节。
        using (var stream = new FileStream(pair.Request, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        {
            if (stream.Length != before.Length)
                throw new Exception("GDK command blob changed before write");
            var check = new byte[before.Length];
            int at = 0, n;
            while (at < check.Length && (n = stream.Read(check, at, check.Length - at)) > 0)
                at += n;
            if (at != check.Length || !check.SequenceEqual(before))
                throw new Exception("GDK request was replaced by another writer");
            stream.Position = position;
            stream.Write(after, position, 3);
            stream.Flush(true);
        }

        if (!Read(pair.Request, 65536).SequenceEqual(after))
            throw new Exception("GDK three-byte write could not be verified");
        return before;
    }

    // 比较请求/回执是否仍绑定到同一私有实例与用户资料。
    static Pair SamePair(Pair pair)
    {
        var current = Resolve(pair.Root, pair.Mod);
        if (!String.Equals(current.Request, pair.Request, StringComparison.OrdinalIgnoreCase))
            throw new Exception("GDK request GUID changed during probe");
        return current;
    }

    // 在时限内等待指定握手状态，超时按失败处理。
    static Receipt Wait(Pair pair, string instance, string status, double previous, DateTime start, int milliseconds)
    {
        var watch = Stopwatch.StartNew();
        Exception last = null;
        while (watch.ElapsedMilliseconds < milliseconds)
        {
            try
            {
                var receipt = Fresh(SamePair(pair), start);
                if (receipt.Instance != instance)
                    throw new InvalidOperationException("Component scene instance changed");
                if (receipt.Status == status && receipt.Clock > previous)
                    return receipt;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception e)
            {
                last = e;
            }

            Thread.Sleep(80);
        }

        throw new Exception("GDK component did not acknowledge " + status + "; " + (last == null ? "receipt status did not change" : last.Message));
    }

    // 生成或发送独立无玩法探针，验证通信后恢复 OFF。
    internal static object Probe(string root, string mod, DateTime start, string output)
    {
        var result = new Dictionary<string, object>
        {
            {
                "component",
                mod
            },
            {
                "requestWritten",
                false
            },
            {
                "acknowledged",
                false
            },
            {
                "restoredOff",
                false
            },
            {
                "disabledAcknowledged",
                false
            },
            {
                "testPassed",
                false
            }
        };
        Pair pair = null;
        Receipt initial = null;
        bool attempted = false;
        byte[] backup = null;
        try
        {
            pair = Resolve(root, mod);
            initial = Fresh(pair, start);
            result["before"] = initial;
            if (initial.Status != "Disabled")
                throw new Exception("Stop the toolbox with F9; component is not Disabled");
            var current = Wait(pair, initial.Instance, "Disabled", initial.Clock, start, 2000);
            if (current.Instance != initial.Instance)
                throw new Exception("Component scene changed");
            backup = Read(pair.Request, 65536);
            FixedReplacement(backup, "OFF", RejectedCommand, mod);
            string backupFile = mod + "-request-before.sav";
            File.WriteAllBytes(Path.Combine(output, backupFile), backup);
            result["requestBackup"] = backupFile;
            var checkedPair = SamePair(pair);
            var checkedReceipt = Fresh(checkedPair, start);
            if (checkedReceipt.Instance != initial.Instance || checkedReceipt.Status != "Disabled")
                throw new Exception("Component state changed before probe");
            attempted = true;
            WriteFixed(checkedPair, "OFF", RejectedCommand);
            result["requestWritten"] = true;
            // 已安装的两个 Poll 会先拒绝三字符命令，之后才可能解析动作、查找 Actor 或执行能力。
            var rejected = Wait(pair, initial.Instance, "InvalidRequest", checkedReceipt.Clock, start, 2500);
            result["afterRejectedCommand"] = rejected;
            result["acknowledged"] = true;
        }
        catch (Exception e)
        {
            result["error"] = SafeError(e);
        }
        finally
        {
            if (attempted && pair != null)
                try
                {
                    // 心跳停止也必须允许恢复本通道自己的无效命令；即使时间过期仍核对类、实例和内容。
                    var current = SamePair(pair);
                    var receipt = ReadReceipt(current);
                    if (receipt.Instance != initial.Instance)
                        throw new Exception("Scene changed; do not write into the new scene request");
                    string value = EquipmentSaveCodec.Parse(Read(current.Request, 65536), "Request", "Command", mod).Value;
                    if (value != "OFF" && InterruptedCommand(value))
                        WriteFixed(current, value, "OFF");
                    else if (value != "OFF")
                        throw new Exception("Another command replaced the probe; do not overwrite it");
                    result["restoredOff"] = true;
                    if (!Read(current.Request, 65536).SequenceEqual(backup))
                        throw new Exception("Restored request differs from the original bytes");
                    result["afterRestore"] = Wait(current, initial.Instance, "Disabled", receipt.Clock, start, 2500);
                    result["disabledAcknowledged"] = true;
                }
                catch (Exception e)
                {
                    result["restoreError"] = SafeError(e);
                }
        }

        result["testPassed"] = (bool)result["requestWritten"] && (bool)result["acknowledged"] && (bool)result["restoredOff"] && (bool)result["disabledAcknowledged"] && !result.ContainsKey("restoreError");
        return result;
    }

}

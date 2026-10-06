// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 中文维护说明：离线验证固定信封、撕裂边界、GDK 描述符轮换与握手回退。夹具全部位于独立临时目录；这组 PASS 不等于游戏拾取、伤害、回血或掉落效果已验证。
// 源码导航和常改参数见 docs/maintenance.md；协议、反射标识及类型白名单修改前先核对两端。
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Reflection;

// NativeMailboxChecks 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
static class NativeMailboxChecks
{
    // 检查本模块约束，不满足时抛出异常而不继续执行。
    static void Check(bool valid, string reason)
    {
        if (!valid)
            throw new Exception(reason);
    }

    // 确认无效输入被拒绝，用于维护拒绝分支的回归样例。
    static void Reject(Action action, string reason)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch
        {
            rejected = true;
        }

        Check(rejected, reason);
    }

    // 构造带真实长度/编码头的隔离 SaveGame 字符串夹具。
    static void FString(BinaryWriter writer, string value)
    {
        byte[] b = Encoding.UTF8.GetBytes(value + "\0");
        writer.Write(b.Length);
        writer.Write(b);
    }

    // 构造合成协议回执，供序号/场景/时限校验。
    static byte[] Receipt(string mod, string value)
    {
        using (var m = new MemoryStream())
        using (var w = new BinaryWriter(m))
        {
            w.Write(0x53415647);
            w.Write(3);
            w.Write(522);
            w.Write(1017);
            w.Write((ushort)5);
            w.Write((ushort)6);
            w.Write((ushort)1);
            w.Write(0);
            FString(w, "UE5");
            w.Write(3);
            w.Write(0);
            FString(w, "/Game/Mods/" + mod + "/Receipt.Receipt_C");
            w.Write((byte)0);
            FString(w, "Status");
            FString(w, "StrProperty");
            w.Write(0);
            w.Write(Encoding.UTF8.GetByteCount(value) + 5);
            w.Write((byte)0);
            FString(w, value);
            FString(w, "None");
            w.Write(0);
            return m.ToArray();
        }
    }

    // 把合成字符串编码为 UTF-16 字节，不写游戏存档。
    static void Utf16(BinaryWriter w, string value)
    {
        w.Write(value.Length);
        w.Write(Encoding.Unicode.GetBytes(value));
    }

    // 构造 WGS 索引测试数据，覆盖滚动与关联规则。
    static byte[] Index(GdkStorageAudit.Entry[] entries)
    {
        using (var m = new MemoryStream())
        using (var w = new BinaryWriter(m))
        {
            w.Write(14);
            w.Write(entries.Length);
            Utf16(w, "");
            Utf16(w, "Microsoft.MinecraftDungeons2_8wekyb3d8bbwe!App");
            w.Write(DateTime.UtcNow.ToFileTimeUtc());
            w.Write(1);
            Utf16(w, "");
            w.Write(0L);
            foreach (var e in entries)
            {
                Utf16(w, e.Name);
                Utf16(w, e.Name);
                Utf16(w, "\"1\"");
                w.Write(e.Revision);
                w.Write(0);
                w.Write(e.Folder.ToByteArray());
                w.Write(DateTime.UtcNow.ToFileTimeUtc());
                w.Write(new byte[16]);
            }

            return m.ToArray();
        }
    }

    // 创建独立临时 blob 夹具，保持真实角色存档隔离。
    static string Blob(string profile, GdkStorageAudit.Entry entry, Guid guid, byte[] bytes)
    {
        string folder = Path.Combine(profile, entry.Folder.ToString("N").ToUpperInvariant());
        Directory.CreateDirectory(folder);
        using (var stream = File.Create(Path.Combine(folder, "container." + entry.Revision)))
        using (var w = new BinaryWriter(stream))
        {
            w.Write(4);
            w.Write(1);
            var name = new byte[128];
            Encoding.Unicode.GetBytes("Data").CopyTo(name, 0);
            w.Write(name);
            w.Write(guid.ToByteArray());
            w.Write(guid.ToByteArray());
        }

        string path = Path.Combine(folder, guid.ToString("N").ToUpperInvariant());
        File.WriteAllBytes(path, bytes);
        return path;
    }

    // 解析外部数据并检查结构约束；格式或协议失配不能继续使用。
    static NearbyLootReceipt Parse(string value, string protocol)
    {
        var r = GdkBridgeTransport.ParseReceipt(value, protocol);
        var f = value.Split('|');
        return new NearbyLootReceipt
        {
            Instance = r.Instance,
            Status = r.Status,
            Clock = r.Clock,
            Epoch = f[2],
            Sequence = Int32.Parse(f[3])
        };
    }

    // 枚举命令写入中断边界，验证不完整信封始终被拒绝。
    static void Tearing(string one, string two)
    {
        string before = NativeMailboxCodec.Encode(one), after = NativeMailboxCodec.Encode(two);
        for (int cut = 0; cut <= before.Length; cut++)
        {
            string torn = after.Substring(0, cut) + before.Substring(cut);
            try
            {
                string decoded = NativeMailboxCodec.Decode(torn);
                Check(decoded == one || decoded == two, "Mixed request accepted at byte " + cut);
            }
            catch (Exception e)
            {
                if (e.Message.StartsWith("Mixed request"))
                    throw;
            }
        }
    }

    // 运行本模块的离线规则自检；返回 PASS 摘要，失败抛出异常供命令行报告。
    public static string Test()
    {
        string off = NativeMailboxCodec.Encode("OFF");
        foreach (string mod in new[]
        {
            "MCD2NearbyLootBridge",
            "MCD2CombatBridge"
        }

        )
        {
            string protocol = mod == "MCD2CombatBridge" ? "4" : "6";
            string probe = NativeMailboxCodec.Probe(protocol, "instance@1", new string ('a', 32), 12), second = probe.Replace(new string ('a', 32), new string ('b', 32));
            var fields = probe.Split('|');
            Check(fields.Length == (protocol == "6" ? 16 : 20) && fields[protocol == "6" ? 12 : 8] == "1" && fields[4] == "probe", "Invalid inert probe layout");
            byte[] before = EquipmentBridgeChecks.Fixture(mod, off), after = NativeMailboxCodec.Replace(before, NativeMailboxCodec.Encode(probe), mod);
            var f = EquipmentSaveCodec.Parse(before, "Request", "Command", mod);
            Check(after.Length == before.Length && NativeMailboxCodec.Decode(EquipmentSaveCodec.Parse(after, "Request", "Command", mod).Value) == probe, "Fixed-width round trip failed");
            for (int i = 0; i < before.Length; i++)
                if (i < f.PayloadOffset + 4 || i >= f.PayloadOffset + 4 + NativeMailboxCodec.Width)
                    Check(before[i] == after[i], "Mailbox framing changed");
            Check(NativeMailboxCodec.Replace(after, off, mod).SequenceEqual(before), "OFF did not restore original bytes");
            Reject(() => NativeMailboxCodec.Replace(EquipmentBridgeChecks.Fixture(mod), off, mod), "Old narrow template accepted");
            Tearing("OFF", probe);
            Tearing(probe, second);
            Tearing(probe, probe.Substring(0, probe.Length - 2) + "99");
        }

        Check(NativeMailboxCodec.Decode(NativeMailboxCodec.Encode(new string ('x', 1023))).Length == 1023, "Maximum valid request rejected");
        foreach (string value in new[]
        {
            "",
            new string ('x', 1024),
            "OFF#attack",
            "OFF\0",
            "中文",
            "OFF "
        }

        )
            Reject(() => NativeMailboxCodec.Encode(value), "Invalid mailbox payload accepted");
        Reject(() => NativeMailboxCodec.Replace(EquipmentBridgeChecks.Fixture(), off, "MCD2EquipmentBridge"), "Sale mailbox modified");
        Check(NativeBridgeProfile.MatchesGdk("Dungeons-WinGDK-Shipping", NativeBridgeProfile.GdkPackage, NativeBridgeProfile.GdkImageSize, true), "Captured package rejected");
        Check(!NativeBridgeProfile.MatchesGdk("Dungeons-Win64-Shipping", NativeBridgeProfile.GdkPackage, NativeBridgeProfile.GdkImageSize, true) && !NativeBridgeProfile.MatchesGdk("Dungeons-WinGDK-Shipping", NativeBridgeProfile.GdkPackage, NativeBridgeProfile.GdkImageSize, false) && !NativeBridgeProfile.MatchesGdk("Dungeons-WinGDK-Shipping", NativeBridgeProfile.GdkPackage + "-changed", NativeBridgeProfile.GdkImageSize, true) && !NativeBridgeProfile.MatchesGdk("Dungeons-WinGDK-Shipping", NativeBridgeProfile.GdkPackage, NativeBridgeProfile.GdkImageSize + 1, true), "Uncaptured package/layout accepted");
        string path = Path.Combine(Path.GetTempPath(), "MCD2-NativeMailbox-test-" + Guid.NewGuid().ToString("N")), root = Path.Combine(path, "package"), profile = Path.Combine(root, "SystemAppData", "wgs", "synthetic-profile");
        Directory.CreateDirectory(profile);
        try
        {
            foreach (string mod in new[]
            {
                "MCD2NearbyLootBridge",
                "MCD2CombatBridge"
            }

            )
                TestChannel(root, profile, mod);
        }
        finally
        {
            string full = Path.GetFullPath(path);
            Check(String.Equals(Path.GetDirectoryName(full), Path.GetTempPath().TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("MCD2-NativeMailbox-test-", StringComparison.Ordinal), "Cleanup path rejected");
            Directory.Delete(full, true);
        }

        Check(!GdkBridgeTransport.SafeError(new FileNotFoundException("missing", @"C:\Users\private\XUID\container.41")).Contains("private"), "Private WGS error path leaked");
        return "PASS: nearby 6 / combat 4 fixed 2048-byte ASCII mailboxes, duplicate framing and every torn-write boundary; byte-exact OFF restoration; old templates, oversized/foreign commands and unvalidated GDK packages rejected.\r\nPASS: private WGS fixtures exercise descriptor rollover retry, both handshake/Disabled acknowledgements, missing handshake restoration, scene changes and foreign writer rejection. No actual game, user saves, input, memory writes or gameplay calls.\r\n";
    }

    // 用临时 WGS 文件验证握手、OFF 恢复与外部写入拒绝。
    static void TestChannel(string root, string profile, string mod)
    {
        string prefix = mod == "MCD2CombatBridge" ? "MCD2Combat" : "MCD2NearbyLoot", protocol = mod == "MCD2CombatBridge" ? "4" : "6";
        var request = new GdkStorageAudit.Entry
        {
            Name = prefix + "Request",
            Folder = Guid.NewGuid(),
            Revision = 1
        };
        var receipt = new GdkStorageAudit.Entry
        {
            Name = prefix + "Receipt",
            Folder = Guid.NewGuid(),
            Revision = 1
        };
        byte[] original = EquipmentBridgeChecks.Fixture(mod, NativeMailboxCodec.Encode("OFF"));
        Guid reqGuid = Guid.NewGuid(), recGuid = Guid.NewGuid();
        string req = Blob(profile, request, reqGuid, original), rec = Blob(profile, receipt, recGuid, Receipt(mod, protocol + "|fixture-instance||0|Disabled|0"));
        // 模拟已报告竞态：索引指向的描述符刚被容器轮换删除。
        request.Revision = 41;
        File.WriteAllBytes(Path.Combine(profile, "containers.index"), Index(new[] { request, receipt }));
        var publish = new Thread(() =>
        {
            Thread.Sleep(120);
            Blob(profile, request, reqGuid, original);
        });
        publish.Start();
        var pair = GdkBridgeTransport.Resolve(root, mod, protocol);
        publish.Join();
        Check(pair.Request == req, "Descriptor rollover retry chose another request");
        bool stop = false, ack = true;
        string scene = "fixture-instance";
        Exception failure = null;
        var timer = Stopwatch.StartNew();
        var backend = new Thread(() =>
        {
            try
            {
                string epoch = "";
                int seq = 0;
                while (!Volatile.Read(ref stop))
                {
                    Thread.Sleep(40);
                    string value;
                    try
                    {
                        value = NativeMailboxCodec.Decode(EquipmentSaveCodec.Parse(File.ReadAllBytes(req), "Request", "Command", mod).Value);
                    }
                    catch
                    {
                        value = "invalid";
                    }

                    string status = "Disabled";
                    if (value != "OFF")
                    {
                        var fields = value.Split('|');
                        if (fields.Length == (protocol == "6" ? 16 : 20) && fields[4] == "probe" && Volatile.Read(ref ack))
                        {
                            epoch = fields[2];
                            seq = Int32.Parse(fields[3]);
                            status = "TransportReady";
                        }
                        else
                            status = "InvalidRequest";
                    }

                    string temp = rec + ".fixture.tmp";
                    File.WriteAllBytes(temp, Receipt(mod, protocol + "|" + Volatile.Read(ref scene) + "|" + epoch + "|" + seq + "|" + status + "|" + timer.Elapsed.TotalSeconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
                    File.Replace(temp, rec, null);
                }
            }
            catch (Exception e)
            {
                failure = e;
            }
        });
        backend.Start();
        try
        {
            var channel = new NativeBridgeChannel(root, mod, protocol, DateTime.UtcNow.AddMinutes(-1), v => Parse(v, protocol));
            Check(channel.Read().Status == "Disabled" && File.ReadAllBytes(req).SequenceEqual(original), "Handshake did not acknowledge and restore OFF");
            string own = NativeMailboxCodec.Probe(protocol, channel.Instance, new string ('c', 32), channel.Read().Clock);
            channel.Write(own);
            channel.Stop();
            Check(File.ReadAllBytes(req).SequenceEqual(original), "Normal channel stop changed framing");
            string ownEncoded = NativeMailboxCodec.Encode(own), off = NativeMailboxCodec.Encode("OFF");
            // 模拟固定写入/恢复边界的磁盘中断；只修改私有测试夹具，检查通道恢复路径。
            foreach (bool restoring in new[]
            {
                false,
                true
            }

            )
            {
                if (restoring)
                    channel.Write(own);
                string previous = restoring ? ownEncoded : off, next = restoring ? off : ownEncoded;
                typeof(NativeBridgeChannel).GetField("previousEncoded", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(channel, previous);
                typeof(NativeBridgeChannel).GetField("pendingEncoded", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(channel, next);
                File.WriteAllBytes(req, NativeMailboxCodec.Replace(original, next.Substring(0, 128) + previous.Substring(128), mod));
                channel.Stop();
                Check(File.ReadAllBytes(req).SequenceEqual(original), "Interrupted request/OFF restoration failed");
            }

            string foreign = NativeMailboxCodec.Encode(NativeMailboxCodec.Probe(protocol, channel.Instance, new string ('d', 32), channel.Read().Clock));
            File.WriteAllBytes(req, NativeMailboxCodec.Replace(original, foreign, mod));
            Reject(() => channel.Write(own), "Foreign command overwritten");
            Reject(() => channel.Stop(), "Foreign command stopped");
            Check(EquipmentSaveCodec.Parse(File.ReadAllBytes(req), "Request", "Command", mod).Value == foreign, "Foreign writer data changed");
            File.WriteAllBytes(req, original);
            string rotated = Blob(profile, request, Guid.NewGuid(), original);
            Reject(() => channel.Write(own), "Rotated request GUID accepted");
            Reject(() => channel.Stop(), "Rotated request GUID stopped");
            Check(File.ReadAllBytes(rotated).SequenceEqual(original), "Rotated request changed");
            Blob(profile, request, reqGuid, original);
            Volatile.Write(ref scene, "new-scene");
            Thread.Sleep(100);
            Reject(() => channel.Write(own), "New scene accepted by old channel");
            Check(File.ReadAllBytes(req).SequenceEqual(original), "Scene change wrote request");
            Volatile.Write(ref scene, "fixture-instance");
            Thread.Sleep(100);
            Volatile.Write(ref ack, false);
            Reject(() => new NativeBridgeChannel(root, mod, protocol, DateTime.UtcNow.AddMinutes(-1), v => Parse(v, protocol)), "Missing handshake accepted");
            Check(File.ReadAllBytes(req).SequenceEqual(original), "Failed handshake did not restore OFF");
            Volatile.Write(ref ack, true);
        }
        finally
        {
            Volatile.Write(ref stop, true);
            backend.Join(2000);
        }

        if (failure != null)
            throw failure;
        string other = Path.Combine(root, "SystemAppData", "wgs", "other-synthetic-profile");
        Directory.CreateDirectory(other);
        File.WriteAllBytes(Path.Combine(profile, "containers.index"), Index(new[] { request }));
        File.WriteAllBytes(Path.Combine(other, "containers.index"), Index(new[] { receipt }));
        Blob(other, receipt, recGuid, Receipt(mod, protocol + "|fixture-instance||0|Disabled|0"));
        Reject(() => GdkBridgeTransport.Resolve(root, mod, protocol), "Cross-profile request/receipt paired");
    }
}

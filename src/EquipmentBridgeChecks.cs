// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 中文维护说明：验证装备通信格式并提供明确区分的离线/现场诊断入口。Test 使用合成数据；Live/Existing 可能连接实际游戏，调用前必须检查参数是否会执行真实出售。
// 源码导航和常改参数见 docs/maintenance.md；协议、反射标识及类型白名单修改前先核对两端。
using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading;

// EquipmentBridgeChecks 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
static class EquipmentBridgeChecks
{
    // 创建测试 FString 字节序列，涵盖 ASCII/Unicode 边界。
    static void String(BinaryWriter w, string text)
    {
        byte[] b = Encoding.UTF8.GetBytes(text + "\0");
        w.Write(b.Length);
        w.Write(b);
    }

    // 构造隔离的自有通信模板测试数据。
    public static byte[] Fixture(string mod = "MCD2EquipmentBridge", string command = "OFF")
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
            String(w, "UE5");
            w.Write(3);
            w.Write(0);
            String(w, "/Game/Mods/" + mod + "/Request.Request_C");
            w.Write((byte)0);
            String(w, "Command");
            String(w, "StrProperty");
            w.Write(0);
            w.Write(Encoding.UTF8.GetByteCount(command) + 5);
            w.Write((byte)0);
            String(w, command);
            String(w, "None");
            w.Write(0);
            return m.ToArray();
        }
    }

    // 校验调用前提，失败立即终止当前操作。
    static void Require(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    // 运行本模块的离线规则自检；返回 PASS 摘要，失败抛出异常供命令行报告。
    public static string Test()
    {
        byte[] bytes = Fixture();
        Require(EquipmentSaveCodec.Parse(bytes, "Request", "Command").Value == "OFF", "Mod save fixture decode");
        string command = "1|0123456789abcdef0123456789abcdef|1|1|1111111|0000000|0111|1";
        byte[] replaced = EquipmentSaveCodec.Replace(bytes, command);
        Require(EquipmentSaveCodec.Parse(replaced, "Request", "Command").Value == command, "Mod command replacement");
        Require(EquipmentSaveCodec.Parse(EquipmentSaveCodec.Replace(replaced, "OFF"), "Request", "Command").Value == "OFF", "Mod command stop replacement");
        bool rejected = false;
        try
        {
            EquipmentSaveCodec.Parse(bytes, "Receipt", "Status");
        }
        catch
        {
            rejected = true;
        }

        Require(rejected, "Foreign save class rejected");
        byte[] malformed = (byte[])bytes.Clone();
        var field = EquipmentSaveCodec.Parse(bytes, "Request", "Command");
        malformed[field.SizeOffset] = 255;
        rejected = false;
        try
        {
            EquipmentSaveCodec.Replace(malformed, "OFF");
        }
        catch
        {
            rejected = true;
        }

        Require(rejected, "Malformed property length rejected");
        rejected = false;
        try
        {
            EquipmentSaveCodec.Replace(bytes, new string ('X', 513));
        }
        catch
        {
            rejected = true;
        }

        Require(rejected, "Command length limit");
        Require(EquipmentDropOrigins.Known("SW.Mob.Creeper.SoulStated") && !EquipmentDropOrigins.Known("SW.Vendor.Unknown"), "Known drop origins only");
        return "PASS: native mod save codec, command/stop round trip, foreign class rejection, malformed size rejection, command bounds and exact origin whitelist. No game writes or inputs.";
    }

    // 对实际游戏执行显式请求并观察原生回执；这不是离线自检。
    public static string Live()
    {
        using (var reader = new HealthReader())
        {
            var bridge = new EquipmentBridge(reader.Pid, new EquipmentPolicy());
            string original = EquipmentSaveCodec.Parse(File.ReadAllBytes(EquipmentBridge.RequestPath), "Request", "Command").Value;
            if (original != "OFF")
                throw new Exception("Stop active equipment organizer before this test");
            var watch = Stopwatch.StartNew();
            bool acknowledged = false;
            EquipmentBridgeState state = null;
            try
            {
                while (watch.ElapsedMilliseconds < 3000)
                {
                    bridge.Pulse(true);
                    Thread.Sleep(300);
                    state = bridge.Read();
                    if (bridge.Acknowledged(state) && state.Status == "PreviewOnly")
                    {
                        acknowledged = true;
                        break;
                    }
                }

                Require(acknowledged, "Native dry-run acknowledgement missing: " + (state == null ? "none" : state.Status));
                Require(state.Sold == 0 && state.Eligible == 0, "Initial inventory baseline must not sell or select existing gear");
                Thread.Sleep(1800);
                state = bridge.Read();
                Require(state.Status == "LeaseExpired", "Heartbeat expiry must stop native organizer: " + state.Status);
                var second = new EquipmentBridge(reader.Pid, new EquipmentPolicy());
                acknowledged = false;
                watch.Restart();
                while (watch.ElapsedMilliseconds < 3000)
                {
                    second.Pulse(true);
                    Thread.Sleep(300);
                    state = second.Read();
                    if (second.Acknowledged(state) && state.Status == "PreviewOnly")
                    {
                        acknowledged = true;
                        break;
                    }
                }

                Require(acknowledged && state.Sold == 0 && state.Eligible == 0, "Native organizer restart / baseline recapture");
            }
            finally
            {
                EquipmentBridge.Stop();
            }

            Thread.Sleep(400);
            state = bridge.Read();
            Require(state.Status == "Disabled", "Native stop request acknowledgement");
            return "PASS: live Blueprint Loader component, native Request/Receipt save serialization, dry-run activation, existing gear baseline, heartbeat lease expiry, fresh activation, and stop acknowledgement. No sales, character-save edits, memory writes or game input.";
        }
    }

    // 检查已有装备的预览/单次回收入口，执行标志决定是否真实回收。
    public static string Existing(bool executeOne)
    {
        using (var reader = new HealthReader())
        {
            var policy = new EquipmentPolicy();
            var before = reader.ReadInventoryAudit();
            var items = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(before, EquipmentItem.Decode));
            var expected = policy.Plan(items, null, true);
            int eligible = System.Linq.Enumerable.Count(expected, d => d.Sell);
            var bridge = new EquipmentBridge(reader.Pid, policy, true, executeOne ? 1 : 0);
            var watch = Stopwatch.StartNew();
            EquipmentBridgeState state = null;
            int nativeEligible = -1;
            bool acknowledged = false;
            try
            {
                while (watch.ElapsedMilliseconds < 10000)
                {
                    bridge.Pulse(!executeOne);
                    Thread.Sleep(300);
                    state = bridge.Read();
                    if (bridge.Acknowledged(state))
                    {
                        acknowledged = true;
                        nativeEligible = Math.Max(nativeEligible, state.Eligible);
                        if (executeOne && state.Status == "Completed")
                            break;
                        if (!executeOne && state.Status == "PreviewOnly")
                            break;
                        if (state.Status != "Monitoring" && state.Status != "AwaitingReceipt" && state.Status != "PreviewOnly")
                            throw new Exception("Native existing-gear state: " + state.Status);
                    }
                }
            }
            finally
            {
                EquipmentBridge.Stop();
            }

            Require(acknowledged, "Existing-gear native acknowledgement missing");
            if (!executeOne)
            {
                Require(state.Sold == 0, "Dry preview must never sell");
                return "PASS: native existing equipment preview; tool eligible=" + eligible + ", native eligible=" + nativeEligible + ". No items sold.";
            }

            Require(state.Status == "Completed" && state.Sold == 1, "Exactly one native salvage must complete");
            var after = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(reader.ReadInventoryAudit(), EquipmentItem.Decode));
            var removed = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(items, i => !System.Linq.Enumerable.Any(after, a => a.Id == i.Id)));
            Require(removed.Count == 1 && System.Linq.Enumerable.Any(expected, d => d.Sell && d.Item.Id == removed[0].Id), "Removed gear must match the authorized current policy");
            var changed = removed[0];
            Require(!changed.Equipped && !changed.Locked && !changed.Enchanted && !changed.SourceProtected && !changed.OtherProtected, "Protected item must never be salvaged");
            return "PASS: exactly one existing common item salvaged through the native game-thread helper; disappearance confirmed, all other identities retained. Item=" + changed.Type + ", category=" + changed.Category + ", power=" + changed.Power + ", rarity=" + changed.Rarity + ". Equipped/locked/enchanted/origin/unknown protections checked. Organizer stopped.";
        }
    }
}

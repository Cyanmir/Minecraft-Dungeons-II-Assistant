// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 工具端收集协议 6：目标身份、场景、范围和一次请求回执。
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;

// 协议 6 的收集回执快照；Instance/Epoch/Sequence 对应一次请求，Status 是独立结果证据。
sealed class NearbyLootReceipt
{
    public string Instance, Epoch, Status;
    public int Sequence;
    public double Clock;
    // 解析外部数据并检查结构约束；格式或协议失配不能继续使用。
    public static NearbyLootReceipt Parse(string value)
    {
        string[] p = value.Split('|');
        int sequence;
        double clock;
        if (p.Length == 6 && p[0] != "6")
            throw new Exception(L10n.T("直接收集组件需要更新，请退出游戏后重新安装组件"));
        if (p.Length != 6 || p[0] != "6" || String.IsNullOrEmpty(p[1]) || !Int32.TryParse(p[3], out sequence) || sequence < 0 || !Double.TryParse(p[5], NumberStyles.Float, CultureInfo.InvariantCulture, out clock) || Double.IsNaN(clock) || Double.IsInfinity(clock) || clock < 0)
            throw new Exception("Invalid nearby collection receipt");
        return new NearbyLootReceipt
        {
            Instance = p[1],
            Epoch = p[2],
            Sequence = sequence,
            Status = p[4],
            Clock = clock
        };
    }
}

sealed class NearbyLootBridge
{
    const string Mod = "MCD2NearbyLootBridge";
    readonly string epoch = Guid.NewGuid().ToString("N"), instance;
    readonly NativeBridgeChannel channel;
    int sequence;
    public NearbyLootBridge(int pid)
    {
        channel = new NativeBridgeChannel(pid, Mod, "6", NearbyLootReceipt.Parse);
        instance = channel.Instance;
    }

    public NearbyLootReceipt Read()
    {
        return channel.Read();
    }

    // 检查单个协议字段，拒绝分隔符或超长内容造成的消息歧义。
    static string Field(string value)
    {
        if (String.IsNullOrEmpty(value) || value.Length > 160 || value.Any(c => c < ' ' || c > 126 || c == '|'))
            throw new Exception("Invalid nearby actor identity");
        return value;
    }

    // 限制直接收集可处理的已核实目标类型。
    static bool Supported(NearbyLootTarget t)
    {
        return t != null && NearbyLootCatalog.Contains(t.Type) && (t.Kind == "item" && !NearbyLootCatalog.Book(t.Type) && !NearbyLootCatalog.Food(t.Type) && !NearbyLootCatalog.Tnt(t.Type) && !NearbyLootCatalog.EmeraldCurrency(t.Type) || t.Kind == "book" && NearbyLootCatalog.Book(t.Type) || t.Kind == "chest" && t.Type.StartsWith("SW.LootActor.", StringComparison.Ordinal) && t.Type.Contains("Chest") || t.Kind == "food" && NearbyLootCatalog.Food(t.Type) || t.Kind == "tnt" && NearbyLootCatalog.Tnt(t.Type) || t.Kind == "pot" && NearbyLootCatalog.EmeraldPot(t.Type));
    }

    // 按目标类型判断回执是否包含所需成功证据，Dispatched 不能当成功。
    public static bool Confirmed(NearbyLootTarget t, string status)
    {
        return t != null && (t.Kind == "item" && status == "PickedUp" || t.Kind == "book" && status == "BookPickedUp" || t.Kind == "chest" && status == "Opened" || t.Kind == "food" && status == "Consumed" || t.Kind == "tnt" && status == "Carried" || t.Kind == "pot" && status == "BrokenWithDrops");
    }

    // 编码协议、场景、玩家、目标身份/位置、类型及期限。
    public static string Command(NearbyLootReceipt receipt, string epoch, int sequence, NearbyLootTarget target, string pawn, bool dry, bool allowMoving = false, int intervalMs = 500, int foodIntervalMs = 1000)
    {
        if (!Supported(target) || !target.Position.Valid || String.IsNullOrEmpty(target.Id) || epoch == null || epoch.Length != 32 || sequence <= 0)
            throw new Exception("Invalid direct collection target");
        if (intervalMs != NearbyLootTiming.Clamp(intervalMs) || foodIntervalMs != NearbyLootTiming.Clamp(foodIntervalMs))
            throw new Exception("Invalid direct collection interval");
        Func<double, string> number = v => v.ToString("R", CultureInfo.InvariantCulture);
        return "6|" + Field(receipt.Instance) + "|" + Field(epoch) + "|" + sequence + "|" + target.Kind + "|" + Field(target.ActorName) + "|" + Field(target.Type) + "|" + Field(pawn) + "|" + number(target.Position.X) + "|" + number(target.Position.Y) + "|" + number(target.Position.Z) + "|" + number(receipt.Clock + .6) + "|" + (dry ? "1" : "0") + "|" + (allowMoving ? "1" : "0") + "|" + intervalMs + "|" + foodIntervalMs;
    }

    // 向独立收集槽发送一次预览或真实原生交互请求。
    public void Dispatch(NearbyLootTarget target, string pawn, bool dry = false, bool allowMoving = false, int intervalMs = 500, int foodIntervalMs = 1000)
    {
        var receipt = Read();
        sequence++;
        Write(Command(receipt, epoch, sequence, target, pawn, dry, allowMoving, intervalMs, foodIntervalMs));
    }

    // 验证当前身份、序号或基线是否仍属于同一次操作。
    public bool Matches(NearbyLootReceipt receipt)
    {
        return receipt.Instance == instance && receipt.Epoch == epoch && receipt.Sequence == sequence;
    }

    // 写入本组件的自有通信数据；不能写入角色存档。
    void Write(string command)
    {
        channel.Write(command);
    }

    public void Stop()
    {
        try
        {
            channel.Stop();
        }
        catch (Exception e)
        {
            ToolboxLog.Error("Loot.DirectStop", e);
        }
    }

    // PointProof 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。

}

// 主窗口的一个 partial 部分；事件处理与异步任务共用主窗口状态，退出时统一清理。
sealed partial class ToolboxForm
{
    NearbyLootBridge nearbyDirectBridge;
    // 关闭当前直接收集请求并释放工具端通道。
    void StopDirectLoot()
    {
        nearbyObservation.Cancel();
        var bridge = nearbyDirectBridge;
        nearbyDirectBridge = null;
        if (bridge != null)
            bridge.Stop();
    }

    // 执行已筛选目标的原生交互，并异步观察对应结果与取消信号。
    async Task RunDirectLoot(InputRequest request, NearbyLootState state, NearbyLootTarget target, CancellationToken cancel)
    {
        if (!Active(request) || cancel.IsCancellationRequested || request.Generation != generation || clock.ElapsedMilliseconds > request.Expires)
            return;
        bool sent = false, failed = false, allowMoving = settings.NearbyAllowMoving;
        try
        {
            if (nearbyDirectBridge == null)
                nearbyDirectBridge = new NearbyLootBridge(reader.Pid);
            if (!Active(request) || cancel.IsCancellationRequested || request.Generation != generation || clock.ElapsedMilliseconds > request.Expires || !ToolboxInput.Front(reader.Pid) || !NearbyInputReady(clock.ElapsedMilliseconds))
                return;
            System.Drawing.Point cursorBefore;
            if (!ToolboxInput.CombatCursor(out cursorBefore))
                return;
            var bridge = nearbyDirectBridge;
            bridge.Dispatch(target, state.PawnName, false, allowMoving, settings.NearbyIntervalMs, settings.NearbyFoodIntervalMs);
            sent = true;
            nearbyLootTiming.Attempt(target.Kind, clock.ElapsedMilliseconds);
            long deadline = clock.ElapsedMilliseconds + 6500;
            nearbyLootNote.Text = L10n.T("正在直接收集附近目标，不移动鼠标");
            ToolboxLog.Write("Loot.DirectRequest", "session=" + state.Combat.Session + " target=" + target.Id + " actor=" + target.ActorName + " type=" + target.Type + " allowManualMovement=" + allowMoving + " intervalMs=" + settings.NearbyIntervalMs + " foodIntervalMs=" + settings.NearbyFoodIntervalMs + "; no tool cursor movement; original interaction event and " + (target.Kind == "pot" ? "native PlayerMelee event" : "authoritative granted GA_Interact request"));
            while (clock.ElapsedMilliseconds < deadline)
            {
                await Task.Delay(100, cancel);
                if (!Active(request) || cancel.IsCancellationRequested || request.Generation != generation)
                    return;
                var receipt = bridge.Read();
                if (!bridge.Matches(receipt))
                    continue;
                if (receipt.Status == "Dispatched")
                {
                    nearbyLootRule.Attempt(target);
                    continue;
                }

                if (NearbyLootBridge.Confirmed(target, receipt.Status))
                {
                    nearbyLootRule.Attempt(target);
                    System.Drawing.Point cursorAfter;
                    var after = reader.ReadNearbyLoot();
                    if (!ToolboxInput.CombatCursor(out cursorAfter) || !after.Known || after.Combat.Session != state.Combat.Session || (!allowMoving && (cursorAfter != cursorBefore || (after.Combat.Player - state.Combat.Player).Length > .001)))
                    {
                        Arm(false);
                        nearbyLootNote.Text = L10n.T("鼠标或角色位置发生变化，已停止附近交互");
                        ToolboxLog.Write("Loot.DirectProofFailed", "target=" + target.Id + " native=" + receipt.Status);
                        return;
                    }

                    nearbyLootNote.Text = L10n.T(receipt.Status == "BookPickedUp" ? "游戏已确认附魔书拾取" : receipt.Status == "PickedUp" ? "游戏已确认装备拾取" : receipt.Status == "Opened" ? "游戏已确认宝箱开启" : receipt.Status == "Consumed" ? "游戏已确认食物消耗与原生效果" : receipt.Status == "Carried" ? "游戏已确认 TNT 拾取与携带" : "游戏已确认绿宝石罐破坏与掉落");
                    ToolboxLog.Write("Loot.DirectResult", "target=" + target.Id + " state=" + receipt.Status + "; allowManualMovement=" + allowMoving + " cursorUnchanged=" + (cursorAfter == cursorBefore) + " playerUnchanged=" + ((after.Combat.Player - state.Combat.Player).Length <= .001));
                    return;
                }

                if (receipt.Status == "ConsumedUnconfirmed" || receipt.Status == "BrokenUnconfirmed")
                {
                    nearbyLootRule.Attempt(target);
                    nearbyLootNote.Text = L10n.T(receipt.Status == "ConsumedUnconfirmed" ? "食物实体已消耗，原生效果未确认" : "罐子已破坏，掉落结果未确认");
                    ToolboxLog.Write("Loot.DirectObserved", "target=" + target.Id + " state=" + receipt.Status + "; entity removed; effect unconfirmed");
                    return;
                }

                nearbyLootRule.Defer(target, clock.ElapsedMilliseconds, receipt.Status == "Throttled" ? 100 : 2000);
                if (receipt.Status == "Unconfirmed" || receipt.Status == "AlreadyAttempted" || receipt.Status == "TargetOutOfRange")
                    nearbyLootRule.Attempt(target);
                nearbyLootNote.Text = L10n.T("直接收集未完成，详见日志");
                ToolboxLog.Limited("Loot.DirectBlocked", "target=" + target.Id + " state=" + receipt.Status);
                return;
            }

            nearbyLootRule.Attempt(target);
            ToolboxLog.Limited("Loot.DirectBlocked", "target=" + target.Id + " native acknowledgement timeout");
        }
        catch (OperationCanceledException)
        {
            failed = true;
            if (sent)
                nearbyLootRule.Attempt(target);
            throw;
        }
        catch (Exception e)
        {
            failed = true;
            if (sent)
                nearbyLootRule.Attempt(target);
            else
                nearbyLootRule.Defer(target, clock.ElapsedMilliseconds);
            nearbyLootNote.Text = e.Message;
            ToolboxLog.Limited("Loot.DirectUnavailable", e.Message);
        }
        // 同一场景复用已核实通道，但每次动作仍恢复 OFF；F9、取消、场景/传输错误会丢弃通道并重新握手。
        finally
        {
            if (failed || cancel.IsCancellationRequested)
                StopDirectLoot();
            else if (nearbyDirectBridge != null)
                nearbyDirectBridge.Stop();
        }
    }
}

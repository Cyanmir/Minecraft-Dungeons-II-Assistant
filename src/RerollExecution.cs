// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 本体只安排单次请求，成功回执后才重算最大匹配并决定下一槽。
// 三类原生货币独立计账，计数按 Confirmed 增加，暂停仍处理在途回执但不派发下一次。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

sealed class RerollExecutionBridge
{
    readonly NativeBridgeChannel channel;
    readonly string epoch = Guid.NewGuid().ToString("N");
    int sequence;
    internal string Instance { get { return channel.Instance; } }
    internal RerollSnapshot Last;
    internal RerollExecutionBridge(int pid)
    {
        channel = new NativeBridgeChannel(pid, "MCD2RerollBridge", "7", ParseReceipt);
    }
    NearbyLootReceipt ParseReceipt(string value)
    {
        Last = RerollSnapshot.Parse(value);
        return new NearbyLootReceipt { Instance = Last.Instance, Epoch = Last.Epoch, Sequence = Last.ActionSequence, Status = Last.ActionStatus, Clock = Last.Clock };
    }
    internal RerollSnapshot Read() { channel.Read(); return Last; }
    internal bool Matches(RerollSnapshot snapshot) { return snapshot.Instance == Instance && snapshot.Epoch == epoch && snapshot.ActionSequence == sequence; }
    internal void Dispatch(RerollSnapshot snapshot, RerollEffect effect, int[] remaining)
    {
        if (snapshot.State != "Selected" || snapshot.Instance != Instance || remaining.Length != 3 || remaining.Any(v => v < 0)) throw new InvalidOperationException("Invalid reroll dispatch");
        // 效果标签来自刚读到的实际效果，绝不以 UI 行号/槽号代替。
        string[] fields = { "7", snapshot.Instance, epoch, (++sequence).ToString(CultureInfo.InvariantCulture), "reroll", snapshot.SelectedUid,
            snapshot.Type, effect.Type, snapshot.NativeRerolls.ToString(CultureInfo.InvariantCulture), effect.Template,
            effect.Intensity.ToString("R", CultureInfo.InvariantCulture), effect.Cost[0].ToString(CultureInfo.InvariantCulture), effect.Cost[1].ToString(CultureInfo.InvariantCulture), effect.Cost[2].ToString(CultureInfo.InvariantCulture),
            remaining[0].ToString(CultureInfo.InvariantCulture), remaining[1].ToString(CultureInfo.InvariantCulture), remaining[2].ToString(CultureInfo.InvariantCulture),
            (snapshot.Clock + 1.5).ToString("R", CultureInfo.InvariantCulture), snapshot.Power.ToString(CultureInfo.InvariantCulture), snapshot.Rarity,
            snapshot.Effects.Count.ToString(CultureInfo.InvariantCulture), snapshot.MessageCount.ToString(CultureInfo.InvariantCulture), snapshot.RawPower.ToString("R", CultureInfo.InvariantCulture) };
        channel.Write(String.Join("|", fields));
    }
    internal void Stop() { channel.Stop(); }
    // 协议 7 的选择命令没有费用动作；装备身份必须在后续原生 SelectedItem 回执中再次核对。
    internal void Select(RerollSnapshot snapshot, RerollInventoryItem item)
    {
        channel.Write(String.Join("|", new[] { "7", snapshot.Instance, epoch, (++sequence).ToString(CultureInfo.InvariantCulture),
            "select", item.Uid, item.Item.Type, "SW.Rarity." + item.Item.Rarity, item.RawPower.ToString("R", CultureInfo.InvariantCulture),
            snapshot.SelectedUid, (snapshot.Clock + 1).ToString("R", CultureInfo.InvariantCulture), snapshot.MessageCount.ToString(CultureInfo.InvariantCulture) }));
    }
}

enum RerollRunState { Running, Paused, Stopped, Complete, Uncertain }
sealed class RerollRun
{
    readonly RerollExecutionBridge bridge;
    readonly RerollConfiguration configuration;
    readonly List<RerollGoal> goals;
    readonly string uid, type, rarity, componentInstance;
    readonly int power, slots;
    readonly double rawPower;
    readonly bool storm;
    RerollSnapshot before;
    RerollEffect pending;
    long dispatchedAt;
    int pauseBarrierSample;
    int lastSample, lastMessages;
    double lastClock, lastMessageClock;
    readonly object gate = new object();
    internal volatile RerollRunState State = RerollRunState.Running;
    internal string Reason = "正在自动刷新";
    internal int Completed;
    internal int[] Spent = new int[3];
    internal RerollMatch Match;
    internal RerollSnapshot Snapshot;
    internal bool HasPending { get { return pending != null; } }

    internal RerollRun(int pid, RerollConfiguration source, RerollSnapshot initial, RerollExecutionBridge shared = null)
    {
        source.Validate(); configuration = source.Copy();
        if (configuration.Targets.Count == 0) throw new InvalidOperationException(L10n.T("请至少选择一个目标词条"));
        if (initial == null || initial.State != "Selected" || configuration.EquipmentType != initial.Type || initial.Effects.Count == 0)
            throw new InvalidOperationException(L10n.T("配置装备与铁匠当前选中装备不一致"));
        if (configuration.ManualUnverified)
        {
            if (configuration.Targets.Any(t => t.Level == 0)) throw new InvalidOperationException(L10n.T("请为每个目标选择等级"));
            if (configuration.Targets.Any(t => !RerollEffectLevels.Supports(t.Type, t.Level))) throw new InvalidOperationException(L10n.T("此效果不支持所选等级"));
            goals = configuration.Targets.Select(t => new RerollGoal(t.Type, t.Level)).ToList();
        }
        else
        {
            // 当前 SDK 没有经确认的最终候选池入口；原始表并集不得作为最高等级/可达性证明。
            throw new InvalidOperationException(L10n.T("请重新保存装备目标"));
        }
        uid = initial.SelectedUid; type = initial.Type; rarity = initial.Rarity; power = initial.Power; storm = initial.Storm; slots = initial.Effects.Count;
        rawPower = initial.RawPower;
        // 从用户核对的快照绑定场景，建桥期间切场景也必须拒绝，而不自动沿用相同装备 UID。
        componentInstance = initial.Instance;
        lastSample = initial.Sequence; lastClock = initial.Clock; lastMessages = initial.MessageCount; lastMessageClock = initial.MessageClock;
        bridge = shared ?? new RerollExecutionBridge(pid);
        Snapshot = bridge.Read();
        ValidateObservation(Snapshot);
        ValidateContext(Snapshot);
        Match = RerollMatching.Match(goals, Snapshot.Effects);
    }
    void ValidateContext(RerollSnapshot snapshot)
    {
        if (snapshot.Instance != componentInstance || snapshot.State != "Selected" || snapshot.SelectedUid != uid || snapshot.Type != type || snapshot.Rarity != rarity || snapshot.Power != power || snapshot.RawPower != rawPower || snapshot.Storm != storm || snapshot.Effects.Count != slots)
            throw new InvalidOperationException("SelectionChanged");
        if (snapshot.MessageObserver != 1) throw new InvalidOperationException("ConfirmationUnavailable");
        // 不可读等级既不能保留为达标，也不能当成低等级继续刷，以免刷新掉已有目标。
        if (snapshot.Effects.Any(e => e.Level < 1)) throw new InvalidOperationException("Unreadable");
    }
    // 同实例允许重复读取同一新鲜样本，但禁止编号、时钟或消息证据倒退；中间态没有效果数据。
    void ValidateObservation(RerollSnapshot snapshot)
    {
        if (snapshot.Sequence < lastSample || snapshot.Clock < lastClock || snapshot.State == "Selected" &&
            (snapshot.MessageCount < lastMessages || snapshot.MessageClock < lastMessageClock)) throw new InvalidOperationException("Unreadable");
        lastSample = snapshot.Sequence; lastClock = snapshot.Clock;
        if (snapshot.State == "Selected") { lastMessages = snapshot.MessageCount; lastMessageClock = snapshot.MessageClock; }
    }
    // 输入快照只读；在途时只处理对应 epoch/sequence，旧回执和观察编号不算完成。
    internal void Tick(long now)
    {
        lock (gate) TickCore(now);
    }
    void TickCore(long now)
    {
        if (State == RerollRunState.Complete || State == RerollRunState.Stopped || State == RerollRunState.Uncertain) return;
        try
        {
            Snapshot = bridge.Read();
            ValidateObservation(Snapshot);
            // 只等待组件确认的列表重建中间态，不把缺失快照判为成功，不派发下一次。
            if (pending != null && Snapshot.State == "SelectionPending" && bridge.Matches(Snapshot) && Snapshot.ActionStatus == "Dispatched")
            { if (now - dispatchedAt > 8000) Finish("ResultTimedOut", true); return; }
            ValidateContext(Snapshot);
            if (pending != null)
            {
                // OFF 会先于组件接受请求到达。仅使用 OFF 写入并重新读取屏障后的新样本、
                // 非在途状态以及计数/通知/余额全部未动，才能撤销本体等待，不增加完成次数。
                bool idleAfterPause = Snapshot.ActionStatus == "Disabled" || Snapshot.ActionStatus == "Cancelled" ||
                    Snapshot.ActionStatus == "Confirmed" && !bridge.Matches(Snapshot);
                if (State == RerollRunState.Paused && Snapshot.Sequence > pauseBarrierSample && idleAfterPause &&
                    Snapshot.NativeRerolls == before.NativeRerolls && Snapshot.MessageCount == before.MessageCount &&
                    Enumerable.Range(0, 3).All(i => Snapshot.Balance[i] == before.Balance[i]))
                { pending = null; before = null; Match = RerollMatching.Match(goals, Snapshot.Effects); return; }
                if (bridge.Matches(Snapshot))
                {
                    if (Snapshot.ActionStatus == "Confirmed")
                    {
                        // 本体再核对同装备原生计数和三类精确借记，不仅信任单一成功字符串。
                        if (Snapshot.NativeRerolls != before.NativeRerolls + 1 || Snapshot.MessageCount != before.MessageCount + 1 ||
                            Enumerable.Range(0, 3).Any(i => Snapshot.Balance[i] != before.Balance[i] - pending.Cost[i]))
                        { Finish("ResultUncertain", true); return; }
                        for (int i = 0; i < 3; i++) Spent[i] = checked(Spent[i] + pending.Cost[i]);
                        Completed++; pending = null; before = null;
                        Match = RerollMatching.Match(goals, Snapshot.Effects);
                        if (Match.Complete) { State = RerollRunState.Complete; Reason = "目标已完成"; bridge.Stop(); return; }
                    }
                    else if (Snapshot.ActionStatus != "Queued" && Snapshot.ActionStatus != "Dispatched")
                    {
                        if (Snapshot.ActionStatus == "Cancelled" && State == RerollRunState.Paused)
                        { pending = null; before = null; Match = RerollMatching.Match(goals, Snapshot.Effects); return; }
                        Finish(Snapshot.ActionStatus, Snapshot.ActionStatus == "ResultUncertain" || Snapshot.ActionStatus == "ResultTimedOut"); return;
                    }
                }
                if (pending != null)
                {
                    if (now - dispatchedAt > 8000) Finish("ResultTimedOut", true);
                    return;
                }
            }
            if (State == RerollRunState.Paused) return;
            Match = RerollMatching.Match(goals, Snapshot.Effects);
            if (Match.Complete) { State = RerollRunState.Complete; Reason = "目标已完成"; bridge.Stop(); return; }
            if (Completed >= configuration.MaximumRerolls) { Finish("已达到最大刷新次数", false); return; }
            if (Snapshot.NativeRerolls >= 255) { Finish("CounterLimit", false); return; }
            if (Snapshot.Failures != 0) { Finish("NativeConditionsFailed", false); return; }
            if (Snapshot.Effects.Select(e => e.Type).Distinct(StringComparer.Ordinal).Count() != slots) { Finish("AmbiguousEffects", false); return; }
            // 只刷新最大匹配中未分配的槽；已达标目标保持原位，任意目标组合都可停止。
            int candidate = Enumerable.Range(0, slots).First(i => Match.SlotToGoal[i] < 0);
            var effect = Snapshot.Effects[candidate];
            if (effect.Failures != 0) { Finish("NativeConditionsFailed", false); return; }
            int[] remaining = Enumerable.Range(0, 3).Select(i => configuration.Budgets[i] - Spent[i]).ToArray();
            if (Enumerable.Range(0, 3).Any(i => effect.Cost[i] > remaining[i])) { Finish("BudgetLimit", false); return; }
            if (Enumerable.Range(0, 3).Any(i => effect.Cost[i] > Snapshot.Balance[i])) { Finish("InsufficientFunds", false); return; }
            // UI 暂停/F9 可以在背景读取期间先改变状态；写请求前再次核对，不让迟到读取重启执行。
            if (State != RerollRunState.Running) return;
            before = Snapshot; pending = effect; dispatchedAt = now;
            // 先登记在途，即使固定槽写入失败，也禁止盲目重发，转为结果不确定并恢复 OFF。
            bridge.Dispatch(before, pending, remaining);
        }
        catch (Exception error)
        {
            ToolboxLog.Error("Reroll.Run", error);
            Finish(pending == null ? error.Message : "ResultUncertain", pending != null);
        }
    }
    internal void Pause()
    {
        if (State != RerollRunState.Running) return;
        State = RerollRunState.Paused; Reason = "自动刷新已暂停";
        lock (gate)
            try { bridge.Stop(); pauseBarrierSample = bridge.Read().Sequence; }
            catch (Exception error) { ToolboxLog.Error("Reroll.Pause", error); Finish("ResultUncertain", true); }
    }
    internal void Resume()
    {
        lock (gate)
        {
            if (State != RerollRunState.Paused || pending != null) return;
            try { Snapshot = bridge.Read(); ValidateObservation(Snapshot); ValidateContext(Snapshot); State = RerollRunState.Running; Reason = "正在自动刷新"; }
            catch (Exception error) { Finish(error.Message, false); }
        }
    }
    internal void Stop()
    {
        State = RerollRunState.Stopped;
        lock (gate) Finish(pending == null ? "自动刷新已停止" : "ResultUncertain", pending != null);
    }
    void Finish(string reason, bool uncertain)
    {
        State = uncertain ? RerollRunState.Uncertain : RerollRunState.Stopped; Reason = reason;
        try { bridge.Stop(); } catch (Exception error) { ToolboxLog.Error("Reroll.Stop", error); State = RerollRunState.Uncertain; Reason = "ResultUncertain"; }
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 队列只保存稳定原生 UID 和已读身份。
// select 不算刷新，不消费预算；SelectedItem 回执以及实际 UID/品质/力量全部核对后才创建单件执行器。
// 整批复用同一 epoch，避免反复建桥和历史 epoch 上限；停止/F9 不重试选择或刷新。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

sealed class RerollInventoryItem
{
    internal EquipmentItem Item;
    internal string Uid;
    internal int Slots;
    internal double RawPower;
    internal bool DisplayEnchanted;
    internal bool DisplayMetadataKnown;
    // 只读详情保留本次已读效果批次，不额外读游戏或推算未知等级/候选范围。
    internal List<Dictionary<string, object>> DisplayEffectBatches = new List<Dictionary<string, object>>();
    internal static RerollInventoryItem Decode(Dictionary<string, object> row)
    {
        var item = EquipmentItem.Decode(row);
        var data = row.ContainsKey("ItemData") ? row["ItemData"] as Dictionary<string, object> : null;
        object raw;
        long uid;
        if (!item.Known || item.Category == EquipmentCategory.Unsupported || data == null || !data.TryGetValue("NativeSessionUID", out raw) ||
            !(raw is string) || !Int64.TryParse((string)raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out uid) || uid == 0 ||
            uid.ToString(CultureInfo.InvariantCulture) != (string)raw) return null;
        // 只数实际 Rerollable 批次，不按品质/风暴推算，也不把固定或附魔词条计入。
        int slots = 0;
        bool enchanted = false;
        foreach (var batch in ((System.Collections.IEnumerable)data["Effects"]).Cast<Dictionary<string, object>>())
        {
            var effects = ((System.Collections.IEnumerable)batch["EffectsInThisBatch"]).Cast<Dictionary<string, object>>().ToArray();
            if ((string)batch["TypeTag"] == "SW.Item.Effect.Rerollable")
                slots += effects.Length;
            // 展示只认非空附魔批次或实际投入点数；回收的保守附魔保护规则继续独立使用。
            // 空附魔批次不画紫色角标，不能为美化卡片放宽回收保护或刷新准入。
            enchanted |= (string)batch["TypeTag"] == "SW.Item.Effect.Enchantment" && effects.Length > 0 ||
                effects.Any(e => Convert.ToInt32(e["EnchantmentPointsInvested"]) > 0);
        }
        if (slots > 16) return null;
        float power = Convert.ToSingle(((Dictionary<string, object>)((Dictionary<string, object>)data["GeneratorData"])["PowerGeneratorValues"])["ItemPower"]);
        if (Single.IsNaN(power) || Single.IsInfinity(power) || power < 0 || power > 100000) return null;
        return new RerollInventoryItem { Item = item, Uid = (string)raw, Slots = slots, RawPower = power, DisplayEnchanted = enchanted, DisplayMetadataKnown = true,
            DisplayEffectBatches = ((System.Collections.IEnumerable)data["Effects"]).Cast<Dictionary<string, object>>().ToList() };
    }
    internal bool Matches(RerollSnapshot value)
    {
        return value.State == "Selected" && value.SelectedUid == Uid && value.Type == Item.Type &&
            value.Rarity == "SW.Rarity." + Item.Rarity && value.RawPower == RawPower && value.Storm == Item.SoulStorm && value.Effects.Count == Slots;
    }
}

sealed class RerollBatch
{
    readonly int pid;
    readonly RerollConfiguration configuration;
    readonly List<RerollInventoryItem> queue;
    // 每件任务冻结独立目标，整批仍共用配置中的次数/预算；不能把编辑下一件的目标套到前一件。
    readonly List<List<RerollTargetSetting>> taskTargets;
    int matchedTask;
    readonly RerollExecutionBridge bridge;
    readonly object gate = new object();
    RerollRun current;
    RerollMatch lastMatch;
    bool selecting, selectionCancelling;
    long selectedAt;
    int selectionSample, selectionMessages, cancellationSample, lastSample, accumulated;
    double lastClock;
    readonly int[] accumulatedSpent = new int[3];
    readonly int[] taskRerolls;
    internal volatile RerollRunState State = RerollRunState.Running;
    internal string Reason = "正在自动刷新";
    internal RerollSnapshot Snapshot;
    internal int FinishedItems;
    internal int TotalItems { get { return queue.Count; } }
    // 一次在队列锁内取完整展示快照；UI 不读取可变 current，也不改变正在执行的队列。
    internal RerollTaskInfo[] Tasks
    {
        get
        {
            lock (gate)
                return queue.Select((item, index) => new RerollTaskInfo { Item = item, Number = index + 1,
                    Targets = RerollConfiguration.CopyTargets(taskTargets[index]),
                    Rerolls = taskRerolls[index] + (index == FinishedItems && current != null ? current.Completed : 0),
                    Status = index < FinishedItems ? "已完成" : index > FinishedItems ? "待刷" :
                        State == RerollRunState.Uncertain ? "未确认" : State == RerollRunState.Stopped ? "已停止" :
                        State == RerollRunState.Paused ? "已暂停" : selecting ? "切换中" : current == null ? "核对中" : "刷取中" }).ToArray();
        }
    }
    internal RerollInventoryItem ActiveItem { get { lock (gate) return queue[Math.Min(FinishedItems, queue.Count - 1)]; } }
    // UI 与后台 Tick 共用队列锁，保证停止、交接和统计一致。
    internal int Completed { get { lock (gate) return accumulated + (current == null ? 0 : current.Completed); } }
    internal int[] Spent { get { lock (gate) return Enumerable.Range(0, 3).Select(i => accumulatedSpent[i] + (current == null ? 0 : current.Spent[i])).ToArray(); } }
    internal RerollMatch Match { get { lock (gate) return current == null ? lastMatch : current.Match; } }
    internal string[] MatchedTypes { get { lock (gate) { var match = Match; return match == null ? new string[0] : match.SlotToGoal.Where(i => i >= 0).Distinct().Select(i => taskTargets[matchedTask][i].Type).ToArray(); } } }
    internal List<RerollTargetSetting> ActiveTargets { get { lock (gate) return RerollConfiguration.CopyTargets(taskTargets[Math.Min(FinishedItems, queue.Count - 1)]); } }
    internal bool HasPending { get { var run = current; return selecting || selectionCancelling || run != null && run.HasPending; } }
    internal bool HasPendingReroll { get { var run = current; return run != null && run.HasPending; } }

    internal RerollBatch(int pid, RerollConfiguration settings, RerollSnapshot initial, List<RerollInventoryItem> items,
        Dictionary<string, List<RerollTargetSetting>> gearTargets = null)
    {
        configuration = settings.Copy(); configuration.Validate(); this.pid = pid;
        if (!configuration.ManualUnverified)
            throw new InvalidOperationException("请为每个目标选择等级");
        if (initial == null || initial.State != "Selected") throw new InvalidOperationException("SelectionChanged");
        // 未勾选时保留单件模式；勾选后使用本次快照冻结队列，不随列表排序或分类改变。
        queue = items.Count == 0 ? new List<RerollInventoryItem> { new RerollInventoryItem { Uid = initial.SelectedUid, Slots = initial.Effects.Count, RawPower = initial.RawPower,
            Item = new EquipmentItem { Type = initial.Type, Rarity = (EquipmentRarity)Enum.Parse(typeof(EquipmentRarity), initial.Rarity.Substring(10)),
                Power = initial.Power, SoulStorm = initial.Storm } } } : items.ToList();
        if (queue.Count > 4096 || queue.Any(i => i.Slots < 1) || queue.Select(i => i.Uid).Distinct().Count() != queue.Count) throw new InvalidOperationException("SelectionChanged");
        taskTargets = queue.Select(item => {
            List<RerollTargetSetting> targets;
            // 多件模式必须有明确的逐件配置；不能静默回退成整批共用最后编辑的目标。
            if (items.Count > 0 && (gearTargets == null || !gearTargets.TryGetValue(item.Uid, out targets)))
                throw new InvalidOperationException("请为每件队列装备设置目标词条");
            if (items.Count == 0) targets = configuration.Targets;
            else targets = gearTargets[item.Uid];
            RerollConfiguration.ValidateTargets(targets);
            if (targets.Count == 0) throw new InvalidOperationException("请为每件队列装备设置目标词条");
            if (targets.Any(t => !RerollEffectLevels.Supports(t.Type, t.Level))) throw new InvalidOperationException("此效果不支持所选等级");
            return RerollConfiguration.CopyTargets(targets);
        }).ToList();
        taskRerolls = new int[queue.Count];
        bridge = new RerollExecutionBridge(pid);
        Snapshot = bridge.Read();
        if (Snapshot.Instance != initial.Instance || Snapshot.State != "Selected" || Snapshot.SelectedUid != initial.SelectedUid ||
            Snapshot.MessageCount != initial.MessageCount) { bridge.Stop(); throw new InvalidOperationException("SelectionChanged"); }
        lastSample = Snapshot.Sequence; lastClock = Snapshot.Clock;
    }

    internal void Tick(long now)
    {
        lock (gate)
        {
            if (State != RerollRunState.Running && State != RerollRunState.Paused) return;
            try
            {
                if (current != null)
                {
                    current.Tick(now); Snapshot = current.Snapshot; Reason = current.Reason;
                    lastMatch = current.Match; matchedTask = FinishedItems;
                    lastSample = Snapshot.Sequence; lastClock = Snapshot.Clock;
                    if (current.State == RerollRunState.Complete)
                    {
                        taskRerolls[FinishedItems] = current.Completed;
                        accumulated += current.Completed;
                        for (int i = 0; i < 3; i++) accumulatedSpent[i] += current.Spent[i];
                        FinishedItems++; current = null;
                        if (FinishedItems == queue.Count) { State = RerollRunState.Complete; Reason = "目标已完成"; return; }
                        // 下一轮才选择下一件，停止/暂停始终有机会阻断队列。
                        Reason = State == RerollRunState.Paused ? "自动刷新已暂停" : "正在切换装备";
                        lastMatch = null;
                    }
                    else if (current.State == RerollRunState.Stopped || current.State == RerollRunState.Uncertain) State = current.State;
                    return;
                }
                if (State == RerollRunState.Paused)
                {
                    // OFF 屏障后确认没有选择命令在途，再恢复执行。
                    if (selectionCancelling)
                    {
                        var cancelled = bridge.Read();
                        if (cancelled.Instance != Snapshot.Instance) throw new InvalidOperationException("SelectionChanged");
                        if (cancelled.Sequence > cancellationSample && cancelled.ActionStatus != "Queued" && cancelled.ActionStatus != "Selecting")
                        { selectionCancelling = false; Snapshot = cancelled; }
                    }
                    return;
                }
                var observed = bridge.Read();
                if (observed.Instance != Snapshot.Instance || observed.Sequence < lastSample || observed.Clock < lastClock)
                    throw new InvalidOperationException("SelectionChanged");
                lastSample = observed.Sequence; lastClock = observed.Clock; Snapshot = observed;
                var target = queue[FinishedItems];
                lastMatch = null;
                if (selecting)
                {
                    if (now - selectedAt > 7000) throw new InvalidOperationException("SelectionFailed");
                    if (!bridge.Matches(observed)) return;
                    if (observed.ActionStatus == "Queued" || observed.ActionStatus == "Selecting") return;
                    if (observed.ActionStatus != "SelectedItem" || observed.Sequence <= selectionSample ||
                        observed.MessageCount != selectionMessages || !target.Matches(observed)) throw new InvalidOperationException("SelectionFailed");
                    selecting = false; bridge.Stop();
                }
                else if (!target.Matches(observed))
                {
                    if (observed.State != "Selected") throw new InvalidOperationException("SelectionChanged");
                    selectionSample = observed.Sequence; selectionMessages = observed.MessageCount;
                    Reason = "正在切换装备";
                    if (State != RerollRunState.Running) return;
                    selecting = true; selectedAt = now; bridge.Select(observed, target); return;
                }
                // 即使整批次数已耗尽，下一件原本就达标仍应记录完成，不消耗一次刷新。
                var match = RerollMatching.Match(taskTargets[FinishedItems].Select(t => new RerollGoal(t.Type, t.Level)).ToList(), observed.Effects);
                if (match.Complete)
                {
                    lastMatch = match; matchedTask = FinishedItems;
                    FinishedItems++;
                    if (FinishedItems == queue.Count) { State = RerollRunState.Complete; Reason = "目标已完成"; bridge.Stop(); }
                    else { Reason = "正在切换装备"; lastMatch = null; }
                    return;
                }
                if (Completed >= configuration.MaximumRerolls) throw new InvalidOperationException("已达到最大刷新次数");
                var remaining = configuration.Copy(); remaining.EquipmentType = target.Item.Type;
                remaining.Targets = RerollConfiguration.CopyTargets(taskTargets[FinishedItems]);
                matchedTask = FinishedItems;
                remaining.MaximumRerolls -= accumulated;
                remaining.Budgets = Enumerable.Range(0, 3).Select(i => configuration.Budgets[i] - accumulatedSpent[i]).ToArray();
                if (State != RerollRunState.Running) return;
                current = new RerollRun(pid, remaining, observed, bridge);
                Reason = current.Reason;
            }
            catch (Exception error)
            {
                ToolboxLog.Error("Reroll.Batch", error);
                State = current != null && current.HasPending ? RerollRunState.Uncertain : RerollRunState.Stopped;
                Reason = State == RerollRunState.Uncertain ? "ResultUncertain" : error.Message;
                try { bridge.Stop(); } catch { State = RerollRunState.Uncertain; Reason = "ResultUncertain"; }
                selecting = false;
            }
        }
    }
    internal void Pause()
    {
        if (State != RerollRunState.Running) return;
        State = RerollRunState.Paused;
        var run = current; if (run != null) run.Pause();
        lock (gate)
        {
            if (FinishedItems == queue.Count && current == null) { State = RerollRunState.Complete; Reason = "目标已完成"; return; }
            // 创建执行器后在队列锁内重新读取 current，确保新实例收到暂停。
            if (current != null && current != run) { current.Pause(); run = current; }
            try
            {
                State = RerollRunState.Paused; selectionCancelling = selecting;
                bridge.Stop(); selecting = false;
                if (selectionCancelling) cancellationSample = bridge.Read().Sequence;
                Reason = "自动刷新已暂停";
            }
            catch { State = RerollRunState.Uncertain; Reason = "ResultUncertain"; }
            if (run != null && run.State == RerollRunState.Uncertain) { State = run.State; Reason = run.Reason; }
            else if (run != null && run.State == RerollRunState.Stopped) { State = run.State; Reason = run.Reason; }
        }
    }
    internal void Resume()
    {
        lock (gate)
        {
            if (State != RerollRunState.Paused || HasPending) return;
            try
            {
                if (current != null) { current.Resume(); State = current.State; Reason = current.Reason; return; }
                var observed = bridge.Read();
                if (observed.Instance != Snapshot.Instance || observed.State != "Selected" || observed.ActionStatus == "Selecting" || observed.ActionStatus == "Queued")
                    throw new InvalidOperationException("SelectionChanged");
                Snapshot = observed; State = RerollRunState.Running; Reason = "正在自动刷新";
            }
            catch { State = RerollRunState.Stopped; Reason = "SelectionChanged"; }
        }
    }
    internal void Stop()
    {
        State = RerollRunState.Stopped;
        var run = current; if (run != null) run.Stop();
        lock (gate)
        {
            if (current != null && current != run) { current.Stop(); run = current; }
            try { State = RerollRunState.Stopped; bridge.Stop(); selecting = selectionCancelling = false; Reason = "自动刷新已停止"; }
            catch { State = RerollRunState.Uncertain; Reason = "ResultUncertain"; }
            if (run != null && run.State == RerollRunState.Uncertain) { State = run.State; Reason = run.Reason; }
        }
    }
}

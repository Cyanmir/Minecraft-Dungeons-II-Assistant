// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 本体异步运行 UI。
// 一次仅一个后台 Tick；读数失败后不恢复运行，结果不确定时同组件实例禁用重新开始。
using System;
using System.Linq;
using System.Threading.Tasks;

sealed partial class ToolboxForm
{
    RerollBatch rerollRun;
    bool rerollStarting, rerollRunReading;
    string rerollBlockedInstance;
    bool RerollRunning { get { return rerollRun != null && (rerollRun.State == RerollRunState.Running || rerollRun.State == RerollRunState.Paused); } }

    async void StartRerollRun()
    {
        if (rerollStarting || rerollRunReading || rerollInventoryReading || reader == null || closing || connecting) return;
        if (rerollRun != null && rerollRun.State == RerollRunState.Paused)
        {
            var resumed = rerollRun;
            int resumeGeneration = rerollGeneration, resumePid = reader.Pid;
            rerollRunReading = true;
            try { await Task.Run(() => resumed.Resume()); }
            finally { rerollRunReading = false; }
            if (!closing && !IsDisposed && resumeGeneration == rerollGeneration && reader != null && reader.Pid == resumePid && resumed == rerollRun)
            {
                rerollMessage = RerollRunMessage(resumed.Reason);
                if (resumed.State == RerollRunState.Uncertain) rerollBlockedInstance = resumed.Snapshot.Instance;
                RefreshRerollDisplay();
            }
            return;
        }
        if (rerollSnapshot == null || rerollSnapshot.State != "Selected" || rerollSnapshot.Instance == rerollBlockedInstance) return;
        if (rerollInventory.Selected.Count == 0) { rerollMessage = "先点装备，再选目标词条"; RefreshRerollDisplay(); return; }
        var initial = rerollSnapshot;
        SyncQueuedGearTargets();
        var configuration = rerollConfiguration.Copy();
        var targetPlans = rerollGearTargets.ToDictionary(pair => pair.Key, pair => RerollConfiguration.CopyTargets(pair.Value), StringComparer.Ordinal);
        // 按勾选顺序冻结队列；不能再按品质、力量、分类或 HashSet 的枚举顺序重排。
        var selectedItems = rerollInventory.OrderedItems();
        if (selectedItems.Count != rerollInventory.Selected.Count)
        { rerollMessage = "装备切换失败，已停止；请重新读取背包"; RefreshRerollDisplay(); return; }
        int pid = reader.Pid;
        // 铁匠执行期间关闭本工具已有战斗/回收/收集自动化，防止修改同件装备或费用证据。
        Arm(false);
        rerollRun = null;
        int expected = rerollGeneration;
        rerollStarting = true;
        rerollMessage = "正在核对装备与原生通道";
        RefreshRerollDisplay();
        RerollBatch created = null;
        try
        {
            created = await Task.Run(() => new RerollBatch(pid, configuration, initial, selectedItems, targetPlans));
            if (closing || IsDisposed || expected != rerollGeneration || reader == null || reader.Pid != pid)
            { created.Stop(); return; }
            rerollRun = created;
            rerollSnapshot = created.Snapshot;
            rerollMessage = RerollRunMessage(created.Reason);
            lastRerollRead = -10000;
        }
        catch (Exception error)
        {
            ToolboxLog.Error("Reroll.Start", error);
            if (!closing && !IsDisposed && expected == rerollGeneration)
                rerollMessage = error is GameCompatibilityException ? ((GameCompatibilityException)error).ReasonKey : RerollRunMessage(error.Message);
        }
        finally
        {
            rerollStarting = false;
            if (!closing && !IsDisposed) RefreshRerollDisplay();
        }
    }
    async void ReadRerollRun(long now)
    {
        if (rerollRunReading || rerollStarting || reader == null || closing || !RerollRunning) return;
        var running = rerollRun;
        int expected = rerollGeneration, pid = reader.Pid;
        rerollRunReading = true;
        try
        {
            await Task.Run(() => running.Tick(now));
            if (closing || IsDisposed || expected != rerollGeneration || reader == null || reader.Pid != pid || running != rerollRun) return;
            rerollSnapshot = running.Snapshot;
            rerollMessage = RerollRunMessage(running.Reason);
            if (running.State == RerollRunState.Uncertain) rerollBlockedInstance = running.Snapshot.Instance;
            RefreshRerollDisplay();
        }
        finally { rerollRunReading = false; }
    }
    void RefreshRerollButtons()
    {
        if (rerollStart == null || rerollConfiguration == null) return;
        bool active = RerollRunning;
        rerollInventoryCard.Enabled = true;
        rerollTargetsCard.Enabled = !active && !rerollStarting;
        rerollLimitsCard.Enabled = !active && !rerollStarting;
        rerollSearch.Enabled = !active && !rerollStarting;
        rerollEquipmentCategory.Enabled = !active && !rerollStarting;
        rerollSlotFilter.Enabled = !active && !rerollStarting;
        rerollSelectAll.Enabled = rerollClearSelection.Enabled = !active && !rerollStarting && !rerollInventoryReading;
        rerollInventory.SelectionEnabled = !active && !rerollStarting && !rerollInventoryReading;
        if (rerollDetailAction != null && !rerollDetailAction.IsDisposed)
        {
            rerollDetailAction.Enabled = rerollInventory.SelectionEnabled && rerollDetailItem != null &&
                rerollInventory.Items.Any(item => item.Uid == rerollDetailItem.Uid && item.Slots > 0);
            if (rerollDetailItem != null) rerollDetailAction.Text = L10n.T(rerollInventory.Selected.Contains(rerollDetailItem.Uid) ? "移出队列" : "加入队列");
        }
        rerollInventoryRead.Enabled = !active && !rerollStarting && !rerollInventoryReading && reader != null;
        rerollPause.Enabled = active && rerollRun.State == RerollRunState.Running;
        rerollStart.Text = L10n.T(active && rerollRun.State == RerollRunState.Paused ? "恢复" : "开始");
        RefreshRerollEditingHeading();
        bool configured = RerollGearTargetsReady();
        rerollStart.Enabled = !rerollStarting && !rerollRunReading && !rerollInventoryReading && (active ? rerollRun.State == RerollRunState.Paused && !rerollRun.HasPending :
            reader != null && rerollSnapshot != null && rerollSnapshot.State == "Selected" && rerollSnapshot.Instance != rerollBlockedInstance &&
            rerollSnapshot.MessageObserver == 1 && rerollSnapshot.Effects.Count > 0 && rerollSnapshot.Effects.All(e => e.Level > 0) &&
            configured && rerollConfiguration.ManualUnverified);
        rerollStop.Enabled = rerollStarting || rerollObserving || active;
        if (!active && !rerollStarting && rerollSnapshot != null && rerollSnapshot.State == "Selected" && !rerollStart.Enabled)
        {
            bool invalidLevels = rerollInventory.Selected.Any(uid => rerollGearTargets.ContainsKey(uid) &&
                rerollGearTargets[uid].Any(t => !RerollEffectLevels.Supports(t.Type, t.Level)));
            string why = !configured ? invalidLevels ? "此效果不支持所选等级" : "请为每件队列装备设置目标词条" : !rerollConfiguration.ManualUnverified ? "请重新保存装备目标" :
                rerollSnapshot.Effects.Count == 0 ? "没有可刷新词条" : rerollSnapshot.Effects.Any(e => e.Level < 1) ? "词条等级不可读，不能开始" :
                rerollSnapshot.MessageObserver != 1 ? "刷新消息监听未建立，不能用于完成确认" : null;
            if (why != null && !rerollProgressDetails.Text.Contains(L10n.T(why))) rerollProgressDetails.Text += "\r\n" + L10n.T(why);
        }
    }
    string RerollProgress(string details)
    {
        if (rerollRun == null) return details;
        var run = rerollRun;
        string progress = String.Format(L10n.T("刷新 {0} 次；达标 {1}/{2}"), run.Completed, run.Match == null ? 0 : run.Match.Count, run.Match == null ? 0 : run.Match.Required);
        progress = String.Format(L10n.T("装备完成：{0}/{1}"), run.FinishedItems, run.TotalItems) + "\r\n" + progress;
        progress += "\r\n" + L10n.T("已花费") + "：" + RerollCurrencyText(run.Spent);
        if (run.MatchedTypes.Length > 0) progress += "\r\n" + L10n.T("达标词条") + "：" + String.Join(" / ", run.MatchedTypes.Select(RerollEffectName));
        progress += "\r\n" + L10n.T("状态：") + " " + L10n.T(RerollRunMessage(run.Reason));
        if (run.HasPendingReroll) progress += "\r\n" + L10n.T(RerollRunning ? "正在等待单次原生刷新结果，不会重复发送" : "最后一次请求结果未确认，未计入完成次数");
        else if (run.HasPending) progress += "\r\n" + L10n.T("正在核对选中装备");
        return progress + "\r\n" + details;
    }
    // 所有原生协议原因转换成六语言维护键；日志保留技术细节，界面不展示异常路径。
    static string RerollRunMessage(string reason)
    {
        switch (reason)
        {
            case "ResultUncertain": return "刷新结果不确定，已停止；请重新进入场景后读取";
            case "ResultTimedOut": return "刷新结果超时，已停止；请重新进入场景后读取";
            case "SelectionChanged": return "装备身份或条件已变化，自动刷新已停止";
            case "ConfirmationUnavailable": return "原生完成证据不可用，自动刷新已停止";
            case "CounterLimit": return "装备已达到安全刷新上限，已停止";
            case "SelectionFailed":
            case "SelectionUnavailable": return "装备切换失败，已停止；请重新读取背包";
            case "正在切换装备": return "正在切换装备";
            case "BudgetLimit": return "下一次原生费用将超过预算，已停止";
            case "InsufficientFunds": return "材料或货币不足";
            case "NativeConditionsFailed": return "原生刷新条件未满足，已停止";
            case "AmbiguousEffects": return "重复效果标签无法安全定位，已停止";
            case "CostChanged": return "原生费用已变化，已停止；请重新读取";
            case "Cancelled": return "自动刷新已停止";
            case "Expired": return "请求已过期，已停止；请重新读取";
            case "InvalidRequest": return "铁匠组件异常，自动刷新已停止";
            case "NoPlayer": case "MultiplePlayers": case "Unreadable": case "BlacksmithClosed": case "NoSelection": case "SelectionUnverified": case "UnknownCost": return ObservationMessage(reason);
            default:
                // 平台/传输异常详细原因只进入日志；只允许维护过的六语言键进入运行提示。
                string[] known = { "正在自动刷新", "目标已完成", "已达到最大刷新次数", "自动刷新已暂停", "自动刷新已停止",
                    "请至少选择一个目标词条", "配置装备与铁匠当前选中装备不一致", "请为每个目标选择等级",
                    "请重新保存装备目标", "请为每件队列装备设置目标词条", "此效果不支持所选等级" };
                if (known.Any(key => reason == key || reason == L10n.T(key))) return reason;
                return "铁匠组件异常，自动刷新已停止";
        }
    }
}

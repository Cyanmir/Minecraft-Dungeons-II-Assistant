// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 点击装备后编辑该 UID 的独立目标，入队顺序保持不变。
// 切装备、切分类和读背包只影响配置展示，不触发游戏选择或刷新；执行前另行冻结逐件目标。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

sealed partial class ToolboxForm
{
    readonly Dictionary<string, List<RerollTargetSetting>> rerollGearTargets = new Dictionary<string, List<RerollTargetSetting>>(StringComparer.Ordinal);
    RerollInventoryItem rerollEditingGear;
    PixelLabel rerollEditingHeading;
    Button rerollQueueEdited;

    void StoreRerollGearTargets()
    {
        if (rerollEditingGear == null || RerollRunning || rerollStarting) return;
        rerollGearTargets[rerollEditingGear.Uid] = RerollConfiguration.CopyTargets(rerollConfiguration.Targets);
        rerollConfiguration.EquipmentType = rerollEditingGear.Item.Type;
        var profiles = rerollConfiguration.GearProfiles;
        if (!profiles.ContainsKey(rerollEditingGear.Item.Type) && profiles.Count >= 32) profiles.Remove(profiles.Keys.First());
        profiles[rerollEditingGear.Item.Type] = RerollConfiguration.CopyTargets(rerollConfiguration.Targets);
    }

    void EditRerollGearTargets(RerollInventoryItem item)
    {
        if (item == null || RerollRunning || rerollStarting || rerollConfiguration == null) return;
        if (rerollEditingGear != null && rerollEditingGear.Uid == item.Uid) return;
        StoreRerollGearTargets();
        List<RerollTargetSetting> targets;
        if (!rerollGearTargets.TryGetValue(item.Uid, out targets) && !rerollConfiguration.GearProfiles.TryGetValue(item.Item.Type, out targets))
            targets = rerollConfiguration.EquipmentType == item.Item.Type ? rerollConfiguration.Targets : new List<RerollTargetSetting>();
        rerollTargetDraftChanged = false;
        rerollEditingGear = item;
        rerollConfiguration.Targets = RerollConfiguration.CopyTargets(targets);
        rerollConfiguration.EquipmentType = item.Item.Type;
        // 自动给出浏览分类；这仅缩小译名列表，绝不声称是该装备实际可达的候选池。
        rerollConfigLoading = true;
        try
        {
            rerollEffectSearch.Text = "";
            rerollEffectCategory.SelectedIndex = item.Item.Category == EquipmentCategory.Melee ? 1 :
                item.Item.Category == EquipmentCategory.Ranged ? 2 : item.Item.Category == EquipmentCategory.Artifact ? 4 : 3;
        }
        finally { rerollConfigLoading = false; }
        SaveRerollConfiguration();
        RefreshRerollTargets(); RefreshRerollDisplay();
        ToolboxLog.Write("Reroll.Edit", "uid=" + item.Uid + " slots=" + item.Slots + " targets=" + rerollConfiguration.Targets.Count);
    }

    void SyncQueuedGearTargets()
    {
        if (RerollRunning || rerollStarting) return;
        StoreRerollGearTargets();
        foreach (var item in rerollInventory.OrderedItems())
        {
            if (rerollGearTargets.ContainsKey(item.Uid)) continue;
            List<RerollTargetSetting> targets;
            if (!rerollConfiguration.GearProfiles.TryGetValue(item.Item.Type, out targets)) targets = new List<RerollTargetSetting>();
            rerollGearTargets[item.Uid] = RerollConfiguration.CopyTargets(targets);
        }
    }

    bool RerollGearTargetsReady()
    {
        Func<List<RerollTargetSetting>, bool> valid = targets => targets != null && targets.Count > 0 && targets.All(t => RerollEffectLevels.Supports(t.Type, t.Level));
        // 必须明确选择任务装备，不以零件队列隐式刷新当前装备。
        if (rerollInventory.Selected.Count == 0) return false;
        return rerollInventory.Selected.All(uid => rerollGearTargets.ContainsKey(uid) && valid(rerollGearTargets[uid]));
    }

    bool QueueEditedRerollGear()
    {
        if (rerollEditingGear == null || RerollRunning || rerollStarting || rerollInventoryReading) return false;
        // 每次按当前背包 UID 核对装备，不能用重新读取前的旧槽数或把上一件的草稿带入下一件。
        var item = rerollInventory.Items.FirstOrDefault(gear => gear.Uid == rerollEditingGear.Uid);
        if (item == null) { RejectRerollQueue("装备已不在背包，请重新读取背包"); return false; }
        rerollEditingGear = item;
        if (item.Slots < 1) { RejectRerollQueue("没有可刷新词条"); return false; }
        // 单个目标直接选好后入队即可；多目标仍可逐项添加。已有列表仅补存人工改过的草稿。
        if ((rerollTargetDraftChanged || rerollConfiguration.Targets.Count == 0) && !ApplyRerollTargetDraft(true))
        { RejectRerollQueue(rerollMessage); return false; }
        if (rerollConfiguration.Targets.Any(t => !RerollEffectLevels.Supports(t.Type, t.Level)))
        { RejectRerollQueue("此效果不支持所选等级"); return false; }
        if (!SaveRerollConfiguration()) return false;
        rerollTargetDraftChanged = false;
        if (!rerollInventory.AddSelection(item.Uid)) { RejectRerollQueue("装备已不在背包，请重新读取背包"); return false; }
        rerollRun = null;
        rerollMessage = "已保存并加入队列";
        ToolboxLog.Write("Reroll.Queue", "uid=" + item.Uid + " position=" + rerollInventory.Selected.Position(item.Uid) +
            " targets=" + String.Join(",", rerollConfiguration.Targets.Select(t => t.Type + "=" + t.Level)));
        RefreshRerollTargets(); RefreshRerollDisplay();
        return true;
    }
    void RejectRerollQueue(string reason)
    {
        rerollMessage = reason;
        SetRerollTargetMessage(reason);
        ToolboxLog.Write("Reroll.QueueRejected", "uid=" + (rerollEditingGear == null ? "none" : rerollEditingGear.Uid) + " reason=" + reason);
        RefreshRerollDisplay();
    }

    void RefreshRerollEditingHeading()
    {
        if (rerollEditingHeading == null) return;
        var item = RerollRunning ? rerollRun.ActiveItem : rerollEditingGear;
        rerollEditingHeading.Text = item == null ? L10n.T("先点装备，再选目标词条") :
            String.Format(L10n.T("目标词条（效果） · {0}"), EquipmentGamePresentation.Name(item.Item));
        if (rerollTargetIcon != null)
        {
            rerollTargetIcon.Item = item == null ? null : item.Item;
            rerollTargetIcon.PowerText = item == null ? "" : item.RawPower.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            rerollTargetIcon.DisplayEnchanted = item == null || !item.DisplayMetadataKnown ? (bool?)null : item.DisplayEnchanted;
            rerollTargetIcon.Invalidate();
        }
        rerollQueueEdited.Enabled = !RerollRunning && !rerollStarting && !rerollInventoryReading && rerollEditingGear != null;
    }
}

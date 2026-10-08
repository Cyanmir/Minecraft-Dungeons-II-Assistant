// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 有序选择保存稳定 UID。
// 任务队列只展示已冻结执行快照；详情窗口只读背包/当前铁匠快照，不能发原生选择或刷新请求。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

sealed class RerollOrderedSelection : IEnumerable<string>
{
    readonly List<string> order = new List<string>();
    readonly HashSet<string> members = new HashSet<string>(StringComparer.Ordinal);
    internal int Count { get { return order.Count; } }
    internal bool Contains(string uid) { return members.Contains(uid); }
    internal int Position(string uid) { return order.IndexOf(uid) + 1; }
    internal bool Add(string uid) { if (!members.Add(uid)) return false; order.Add(uid); return true; }
    internal bool Remove(string uid) { if (!members.Remove(uid)) return false; order.Remove(uid); return true; }
    internal void Clear() { members.Clear(); order.Clear(); }
    internal void IntersectWith(IEnumerable<string> remaining)
    {
        var available = new HashSet<string>(remaining, StringComparer.Ordinal);
        order.RemoveAll(uid => !available.Contains(uid)); members.IntersectWith(available);
    }
    public IEnumerator<string> GetEnumerator() { return order.GetEnumerator(); }
    IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
}

sealed class RerollTaskInfo
{
    internal RerollInventoryItem Item;
    internal int Number, Rerolls;
    internal string Status;
    internal List<RerollTargetSetting> Targets;
}

sealed partial class ToolboxForm
{
    Panel rerollQueueCard;
    RerollInventoryGrid rerollQueueList;
    PixelLabel rerollQueueHeading;
    RerollTaskInfo[] rerollQueueRows = new RerollTaskInfo[0];
    string rerollQueueSignature = "";
    string rerollQueueActiveUid;
    Form rerollDetailWindow;
    Button rerollDetailAction;
    RerollInventoryItem rerollDetailItem;

    void BuildRerollQueue()
    {
        rerollQueueCard = StatusCard(rerollRightPane, 0, 160); rerollQueueCard.Width = 420;
        rerollQueueHeading = (PixelLabel)LabelAt(rerollQueueCard, L10n.T("任务队列"), 20, 12, 380, 30);
        rerollQueueHeading.PixelScale = 1;
        rerollQueueList = new RerollInventoryGrid { Location = new Point(16, 40), Size = new Size(388, 120),
            SelectionEnabled = false, TaskRows = new RerollTaskInfo[0] };
        rerollQueueCard.Controls.Add(rerollQueueList);
        rerollQueueList.PreviewRequested += delegate(RerollInventoryItem item) { EditRerollGearTargets(item); ShowRerollGearDetails(item); };
    }
    void RefreshRerollQueue()
    {
        if (rerollQueueList == null) return;
        // 开始后使用冻结的任务，不跟随背包排序或之后的勾选变化；未开始时显示选择预览。
        var rows = rerollRun == null ? rerollInventory.OrderedItems().Select((item, index) =>
            new RerollTaskInfo { Item = item, Number = index + 1,
                Targets = rerollGearTargets.ContainsKey(item.Uid) ? RerollConfiguration.CopyTargets(rerollGearTargets[item.Uid]) : new List<RerollTargetSetting>(),
                Status = rerollGearTargets.ContainsKey(item.Uid) && rerollGearTargets[item.Uid].Count > 0 ? "待刷" : "未设目标" }).ToArray() : rerollRun.Tasks;
        rerollQueueHeading.Text = String.Format(L10n.T("任务队列 · {0} 件"), rows.Length);
        string[] lines = rows.Select(row => row.Number + ". " + EquipmentGamePresentation.Name(row.Item.Item) + " · " +
            L10n.T(row.Status) + " · " + String.Format(L10n.T("{0} 次"), row.Rerolls)).ToArray();
        string signature = String.Join("\n", rows.Select(row => row.Item.Uid + ":" + String.Join(",", row.Targets.Select(t => t.Type + "=" + t.Level)))) + "\n" + String.Join("\n", lines);
        rerollQueueRows = rows;
        if (signature == rerollQueueSignature) return;
        rerollQueueSignature = signature;
        int activeIndex = rerollRun == null ? -1 : Math.Min(rerollRun.FinishedItems, rows.Length - 1);
        string activeUid = activeIndex < 0 ? null : rows[activeIndex].Item.Uid;
        rerollQueueList.TaskRows = rows;
        rerollQueueList.RefreshItems(false);
        // 仅切换任务时跟随到当前图标；普通更新保留人工查看队列的滚动位置。
        if (activeUid != null && activeUid != rerollQueueActiveUid) rerollQueueList.Reveal(activeIndex);
        rerollQueueActiveUid = activeUid;
    }

    // 单击图标只查看。是否加入队列由明确按钮/复选框决定，不把查看动作伪装成游戏选择。
    void ShowRerollGearDetails(RerollInventoryItem item)
    {
        if (item == null || closing) return;
        if (rerollDetailWindow != null && !rerollDetailWindow.IsDisposed) rerollDetailWindow.Close();
        var window = new RerollDetailsWindow(Font);
        var body = window.Content;
        rerollDetailWindow = window;
        rerollDetailItem = item;
        var icon = new EquipmentIconView { Item = item.Item, PowerText = item.RawPower.ToString("0.##", CultureInfo.InvariantCulture),
            DisplayEnchanted = item.DisplayMetadataKnown ? (bool?)item.DisplayEnchanted : null, Bounds = new Rectangle(16, 16, 90, 90) };
        body.Controls.Add(icon);
        var name = new Label { Text = EquipmentGamePresentation.Name(item.Item) + "\r\n" +
            EquipmentGamePresentation.Category(item.Item.Category) + " · " + EquipmentGamePresentation.Rarity(item.Item.Rarity) +
            "\r\n" + RerollCardPresentation.EnchantmentText(icon.DisplayEnchanted), Bounds = new Rectangle(120, 20, 410, 80),
            ForeColor = OreTheme.Text, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        body.Controls.Add(name);
        var observed = rerollSnapshot;
        bool nativeCurrent = observed != null && item.Matches(observed);
        if (nativeCurrent) icon.PowerText = observed.Power.ToString();
        string text = L10n.T("背包详情来自最近读取；等级和费用以铁匠读数为准。") + "\r\n" +
            String.Format(L10n.T("可刷新词条：{0}"), item.Slots) + "\r\n" + EquipmentGamePresentation.Term("header_power") + "：" + icon.PowerText;
        if (nativeCurrent)
        {
            // 只在完整身份一致时使用当前原生等级/显示数值，不能只凭装备名称或相同标签套到别件。
            foreach (var effect in observed.Effects)
                text += "\r\n" + RerollEffectName(effect.Type) + " · " + String.Format(L10n.T("等级 {0}"), effect.Level < 0 ? "?" : effect.Level.ToString()) +
                    " · " + effect.NativeValue + "\r\n" + L10n.T("刷新费用") + "：" + RerollCurrencyText(effect.Cost);
        }
        foreach (var batch in item.DisplayEffectBatches)
        {
            string tag = Convert.ToString(batch["TypeTag"]);
            string title = tag == "SW.Item.Effect.Rerollable" ? "可刷新词条" : tag == "SW.Item.Effect.Enchantment" ? "附魔词条" :
                tag == "SW.Item.Effect.Static" ? "固定词条" : tag == "SW.Item.Effect.Upgradable" ? "升级词条" : "其他";
            var effects = ((IEnumerable)batch["EffectsInThisBatch"]).Cast<Dictionary<string, object>>().ToArray();
            if (effects.Length == 0) continue;
            text += "\r\n\r\n" + L10n.T(title);
            foreach (var effect in effects)
            {
                object intensity;
                text += "\r\n" + RerollEffectName(Convert.ToString(effect["TypeTag"]));
                if (effect.TryGetValue("Intensity", out intensity)) text += " · " + L10n.T("原始数值") + "：" + Convert.ToString(intensity, CultureInfo.InvariantCulture);
            }
        }
        var details = new OreTextView { Text = text,
            Bounds = new Rectangle(16, 118, 522, 288), BackColor = OreTheme.Field, ForeColor = OreTheme.Text,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
        body.Controls.Add(details);
        var action = new OreButton { Bounds = new Rectangle(16, 418, 252, 42), PixelScale = .9f,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left, Enabled = rerollInventory.SelectionEnabled && item.Slots > 0 && rerollInventory.Items.Any(gear => gear.Uid == item.Uid) };
        rerollDetailAction = action; body.Controls.Add(action);
        Action sync = delegate { action.Text = L10n.T(rerollInventory.Selected.Contains(item.Uid) ? "移出队列" : "加入队列"); };
        sync();
        action.Click += delegate { rerollInventory.ToggleSelection(item.Uid); sync(); };
        // 详情确认后回到右栏编辑该件目标，不发出游戏动作；运行时仍只读关闭。
        var close = new OreButton { Text = L10n.T(RerollRunning ? "关闭" : "设置目标"), Bounds = new Rectangle(282, 418, 252, 42), PixelScale = .9f,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        body.Controls.Add(close); close.Click += delegate { window.Close(); ShowRerollTargetEditor(item); };
        window.SetContents(icon, name, details, action, close);
        window.FormClosed += delegate { if (rerollDetailWindow == window) { rerollDetailWindow = null; rerollDetailAction = null; rerollDetailItem = null; } };
        window.Show(this);
    }
}

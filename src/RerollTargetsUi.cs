// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 目标名称来自原始效果译名，不等于候选池。
// 配置只保存目标和上限，运行中冻结编辑；装备筛选按实际批次，执行以原生当前槽数为准。
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

sealed partial class ToolboxForm
{
    const int RerollLimitsContentHeight = 320;
    RerollConfiguration rerollConfiguration;
    Panel rerollTargetsCard, rerollLimitsCard;
    ListBox rerollTargetsList;
    OreSelect rerollEffectChoice, rerollEffectCategory;
    TextBox rerollEffectSearch;
    OreNumber rerollMaximum;
    readonly OreNumber[] rerollBudgetNumbers = new OreNumber[3];
    readonly Action[] rerollBudgetSync = new Action[3];
    OreNumber rerollTargetLevel;
    PixelLabel[] rerollBudgetLabels = new PixelLabel[3];
    string[] rerollEffectTypes = new string[0];
    bool rerollConfigLoading;
    bool rerollTargetDraftChanged;
    string rerollTargetDraftType;
    int rerollTargetDraftLevel;
    string rerollDisplayedTargetUid = "";
    PixelLabel rerollLevelLimit;
    Button rerollAddTarget;
    Action rerollLevelSliderSync;

    // 配置页单独可保存，即使尚未连接游戏；没有任何自动启用或启动时执行行为。
    void BuildRerollTargets()
    {
        rerollConfiguration = RerollConfiguration.Load();
        rerollTargetsCard = Card(rerollTargetParking, 0, RerollTargetContentHeight); rerollTargetsCard.Width = 420;
        rerollEditingHeading = (PixelLabel)LabelAt(rerollTargetsCard, L10n.T("先点装备，再选目标词条"), 108, 16, 292, 60);
        rerollEditingHeading.PixelScale = .75f;
        rerollTargetIcon = new EquipmentIconView { Bounds = new Rectangle(20, 12, 72, 72), BackColor = OreTheme.Field };
        rerollTargetsCard.Controls.Add(rerollTargetIcon);
        rerollTargetsList = new ListBox { Location = new Point(20, 112), Size = new Size(380, 104), HorizontalScrollbar = true, BackColor = OreTheme.Field, ForeColor = OreTheme.Text, BorderStyle = BorderStyle.FixedSingle, Font = Font,
            DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 32 };
        // 图标严格取资源目录的真实效果引用，缺图时留空；不能用装备图或手绘伪装原版词条图标。
        rerollTargetsList.DrawItem += delegate(object sender, DrawItemEventArgs e)
        {
            var targets = RerollRunning ? rerollRun.ActiveTargets : rerollConfiguration.Targets;
            if (e.Index < 0 || e.Index >= targets.Count || e.Index >= rerollTargetsList.Items.Count) return;
            using (var brush = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? OreTheme.Green : OreTheme.Field)) e.Graphics.FillRectangle(brush, e.Bounds);
            var image = EquipmentGamePresentation.Icon(targets[e.Index].Type);
            int edge = Math.Max(1, (int)Math.Round(2 * OreMetrics.Scale(rerollTargetsList)));
            int iconSize = Math.Max(1, e.Bounds.Height - edge * 2);
            if (image != null)
            {
                // 原版效果图标按整数像素采样，避免高 DPI 时默认插值把边缘糊掉。
                var interpolation = e.Graphics.InterpolationMode;
                e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                e.Graphics.DrawImage(image, new Rectangle(e.Bounds.Left + edge, e.Bounds.Top + edge, iconSize, iconSize));
                e.Graphics.InterpolationMode = interpolation;
            }
            TextRenderer.DrawText(e.Graphics, rerollTargetsList.Items[e.Index].ToString(), Font,
                new Rectangle(e.Bounds.Left + iconSize + 8, e.Bounds.Top, Math.Max(1, e.Bounds.Width - iconSize - 10), e.Bounds.Height), OreTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            e.DrawFocusRectangle();
        };
        rerollTargetsCard.Controls.Add(rerollTargetsList);
        BuildRerollEffectFilter();
        rerollEffectChoice = new OreSelect { Location = new Point(20, 276), Size = new Size(380, 40) };
        rerollEffectChoice.ItemIcon = index => index >= 0 && index < rerollEffectTypes.Length ? EquipmentGamePresentation.Icon(rerollEffectTypes[index]) : null;
        rerollTargetsCard.Controls.Add(rerollEffectChoice);
        LabelAt(rerollTargetsCard, L10n.T("最低等级"), 20, 328, 170, 26);
        rerollLevelLimit = (PixelLabel)LabelAt(rerollTargetsCard, "", 198, 328, 202, 26);
        rerollLevelLimit.PixelScale = .75f;
        rerollTargetLevel = new OreNumber { Location = new Point(260, 364), Size = new Size(140, 40), Minimum = 1, Maximum = 1, Value = 1 };
        rerollTargetsCard.Controls.Add(rerollTargetLevel);
        rerollLevelSliderSync = BindRerollNumberSlider(rerollTargetsCard, rerollTargetLevel, 364);
        rerollEffectChoice.SelectedIndexChanged += delegate
        {
            RefreshRerollLevelLimit();
            MarkRerollTargetDraft();
        };
        rerollTargetLevel.ValueChanged += delegate { MarkRerollTargetDraft(); };
        var add = rerollAddTarget = ButtonAt(rerollTargetsCard, L10n.T("添加词条"), 20, 416, 184);
        var remove = ButtonAt(rerollTargetsCard, L10n.T("删除词条"), 212, 416, 188);
        add.Height = remove.Height = 38;
        ((OreButton)add).PixelScale = ((OreButton)remove).PixelScale = .85f;
        add.Click += delegate { SaveRerollTarget(); };
        remove.Click += delegate
        {
            if (rerollTargetsList.SelectedIndex < 0) return;
            rerollConfiguration.Targets.RemoveAt(rerollTargetsList.SelectedIndex); SaveRerollConfiguration(); RefreshRerollTargets();
        };
        rerollQueueEdited = ButtonAt(rerollTargetParking, L10n.T("保存并加入队列"), 20, 416, 380);
        ((OreButton)rerollQueueEdited).PixelScale = .85f;
        rerollQueueEdited.Click += delegate { if (QueueEditedRerollGear() && rerollTargetWindow != null) rerollTargetWindow.Close(); };
        rerollTargetsList.SelectedIndexChanged += delegate
        {
            if (rerollConfigLoading || rerollTargetsList.SelectedIndex < 0) return;
            var target = rerollConfiguration.Targets[rerollTargetsList.SelectedIndex];
            int index = Array.IndexOf(rerollEffectTypes, target.Type);
            if (index < 0)
            {
                rerollEffectCategory.SelectedIndex = 0; rerollEffectSearch.Text = "";
                RefreshRerollTargets(); index = Array.IndexOf(rerollEffectTypes, target.Type);
            }
            if (index >= 0) rerollEffectChoice.SelectedIndex = index;
            rerollTargetLevel.Value = target.Level;
        };

        rerollLimitsCard = Card(rerollLeftPane, 442, RerollLimitsContentHeight); rerollLimitsCard.Width = 420;
        LabelAt(rerollLimitsCard, L10n.T("最多刷新"), 20, 12, 380, 24);
        var maximum = rerollMaximum = new OreNumber { Location = new Point(260, 40), Size = new Size(140, 36), Minimum = 1, Maximum = RerollConfiguration.CountLimit, Value = rerollConfiguration.MaximumRerolls };
        rerollLimitsCard.Controls.Add(maximum);
        BindRerollNumberSlider(rerollLimitsCard, maximum, 40);
        maximum.ValueChanged += delegate { if (!rerollConfigLoading) { rerollConfiguration.MaximumRerolls = (int)maximum.Value; SaveRerollConfiguration(); } };
        for (int i = 0; i < 3; i++)
        {
            int currency = i;
            rerollBudgetLabels[i] = (PixelLabel)LabelAt(rerollLimitsCard, "", 20, 88 + i * 76, 380, 24);
            rerollBudgetLabels[i].PixelScale = .75f;
            var budget = rerollBudgetNumbers[i] = new OreNumber { Location = new Point(260, 116 + i * 76), Size = new Size(140, 36), Minimum = 0, Maximum = 10000, Value = Math.Min(10000, rerollConfiguration.Budgets[i]) };
            rerollLimitsCard.Controls.Add(budget);
            rerollBudgetSync[i] = BindRerollNumberSlider(rerollLimitsCard, budget, 116 + i * 76);
            budget.ValueChanged += delegate { if (!rerollConfigLoading) { rerollConfiguration.Budgets[currency] = (int)budget.Value; SaveRerollConfiguration(); } };
        }
        RecordLayout(rerollTargetParking);
        RefreshRerollTargets();
    }
    bool SaveRerollConfiguration()
    {
        try { StoreRerollGearTargets(); if (interactive) rerollConfiguration.Save(); return true; }
        catch (Exception error) { ToolboxLog.Error("Reroll.Save", error); rerollMessage = "目标配置保存失败"; SetRerollTargetMessage(rerollMessage); RefreshRerollDisplay(); return false; }
    }
    // 自动观察/切分类不产生草稿；只有人工选择效果或改等级才在入队时补存当前编辑。
    void MarkRerollTargetDraft()
    {
        if (rerollConfigLoading || rerollEditingGear == null || RerollRunning || rerollStarting) return;
        int index = rerollEffectChoice.SelectedIndex;
        rerollTargetDraftType = index < 0 || index >= rerollEffectTypes.Length ? null : rerollEffectTypes[index];
        rerollTargetDraftLevel = (int)rerollTargetLevel.Value;
        rerollTargetDraftChanged = true;
        RefreshRerollEditingHeading();
    }
    void SaveRerollTarget()
    {
        if (!ApplyRerollTargetDraft()) { SetRerollTargetMessage(rerollMessage); RefreshRerollDisplay(); return; }
        if (SaveRerollConfiguration()) rerollTargetDraftChanged = false;
        RefreshRerollTargets(); RefreshRerollDisplay();
    }
    // 手动添加与保存入队共用同一校验，不能将未提交的下拉框/等级静默忽略。
    bool ApplyRerollTargetDraft(bool usePending = false)
    {
        int index = rerollEffectChoice.SelectedIndex;
        // 草稿保存实际标签/等级，搜索过滤或定时重排不能把未提交目标换成列表第一项。
        string type = usePending && rerollTargetDraftChanged ? rerollTargetDraftType :
            index < 0 || index >= rerollEffectTypes.Length ? null : rerollEffectTypes[index];
        int level = usePending && rerollTargetDraftChanged ? rerollTargetDraftLevel : (int)rerollTargetLevel.Value;
        if (type == null) { rerollMessage = "请先选择词条"; return false; }
        if (!RerollEffectLevels.Supports(type, level))
        { rerollMessage = "此效果不支持所选等级"; return false; }
        var target = rerollConfiguration.Targets.FirstOrDefault(t => t.Type == type);
        if (target == null)
        {
            if (rerollConfiguration.Targets.Count >= 64) { rerollMessage = "目标词条已达到上限"; return false; }
            target = new RerollTargetSetting { Type = type }; rerollConfiguration.Targets.Add(target);
        }
        target.Level = level;
        return true;
    }
    void RefreshRerollTargets()
    {
        if (rerollEffectChoice == null || rerollConfigLoading) return;
        rerollConfigLoading = true;
        try
        {
            var targets = RerollRunning ? rerollRun.ActiveTargets : rerollConfiguration.Targets;
            string targetUid = RerollRunning ? rerollRun.ActiveItem.Uid : rerollEditingGear == null ? "" : rerollEditingGear.Uid;
            bool contextChanged = rerollDisplayedTargetUid != targetUid;
            // 定时读数不关闭用户正在操作的菜单；切换任务装备才使旧配置上下文失效。
            if (contextChanged) { rerollEffectChoice.ClosePopup(); rerollEffectCategory.ClosePopup(); }
            rerollDisplayedTargetUid = targetUid;
            RefreshRerollEffectCategories();
            // 删除后 ListBox 会暂存旧索引，刷新前重新检查配置边界。
            string listSelected = !contextChanged && rerollTargetsList.SelectedIndex >= 0 && rerollTargetsList.SelectedIndex < targets.Count ? targets[rerollTargetsList.SelectedIndex].Type : "";
            string selected = rerollEffectChoice.SelectedIndex >= 0 && rerollEffectChoice.SelectedIndex < rerollEffectTypes.Length ? rerollEffectTypes[rerollEffectChoice.SelectedIndex] : "";
            // 缓存更新后重新取原始效果名；当前游戏读到的效果也可配置，仍不证明未来可刷出。
            var effectTypes = EquipmentGamePresentation.EffectNames().Concat(targets.Select(t => t.Type))
                .Concat(rerollSnapshot == null ? new string[0] : rerollSnapshot.Effects.Select(e => e.Type)).Distinct(StringComparer.Ordinal).Where(VisibleRerollEffect).ToArray();
            // 身份变化时关闭旧菜单并更新图标索引，不能只比较显示名称。
            if (!rerollEffectTypes.SequenceEqual(effectTypes)) rerollEffectChoice.ClosePopup();
            rerollEffectTypes = effectTypes;
            ReplaceRerollOptions(rerollEffectChoice, rerollEffectTypes.Select(t => (object)RerollEffectName(t)).ToArray());
            rerollEffectChoice.SelectedIndex = rerollEffectTypes.Length == 0 ? -1 : Math.Max(0, Array.IndexOf(rerollEffectTypes, selected));
            RefreshRerollLevelLimit();
            var rows = targets.Select(target => (object)(RerollEffectName(target.Type) + " · " +
                    String.Format(L10n.T("要求等级：{0}"), target.Level == 0 ? L10n.T("请选择等级") : target.Level.ToString()))).ToArray();
            if (contextChanged || !rerollTargetsList.Items.Cast<object>().SequenceEqual(rows))
            {
                // 同内容保留选择与滚动位置，避免每秒清空列表让点击和预览闪烁。
                rerollTargetsList.BeginUpdate();
                try { rerollTargetsList.Items.Clear(); rerollTargetsList.Items.AddRange(rows); rerollTargetsList.SelectedIndex = targets.FindIndex(t => t.Type == listSelected); }
                finally { rerollTargetsList.EndUpdate(); }
            }
            for (int i = 0; i < 3; i++) rerollBudgetLabels[i].Text = EquipmentGamePresentation.Term(new[] { "Currency_Emerald", "Currency_SpringStone", "Currency_EnchantmentPoint" }[i]) + " · " + L10n.T("最多花费");
        }
        finally { rerollConfigLoading = false; }
        RefreshRerollEditingHeading();
    }

    // 这不设置“自动最高”目标，也不保证本次装备/品质/铁匠条件能获得定义上限。
    void RefreshRerollLevelLimit()
    {
        if (rerollTargetLevel == null) return;
        int index = rerollEffectChoice.SelectedIndex;
        int maximum = index < 0 || index >= rerollEffectTypes.Length ? 0 : RerollEffectLevels.Maximum(rerollEffectTypes[index]);
        rerollTargetLevel.Maximum = Math.Max(1, maximum);
        rerollTargetLevel.Enabled = maximum > 0;
        rerollLevelLimit.Text = maximum > 0 ? String.Format(L10n.T("上限：{0} 级"), maximum) : L10n.T("此效果没有可设置的等级");
        if (rerollAddTarget != null) rerollAddTarget.Enabled = maximum > 0;
        if (rerollLevelSliderSync != null) rerollLevelSliderSync();
    }

    // 不丢弃保存目标或真实读到的无译名效果，也不把开发占位文本伪装成正式游戏名称。
    static string RerollEffectName(string type)
    {
        return EquipmentGamePresentation.HasGameName(type, L10n.Language) ? EquipmentGamePresentation.Name(type, L10n.Language) :
            String.Format(L10n.T("游戏未提供译名 · {0}"), type);
    }
}

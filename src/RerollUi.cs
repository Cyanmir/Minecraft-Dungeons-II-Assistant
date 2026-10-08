// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

sealed class EquipmentIconView : Control
{
    public EquipmentItem Item;
    public string PowerText;
    public bool? DisplayEnchanted;
    public EquipmentIconView() { DoubleBuffered = true; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Item != null)
        {
            var bounds = new Rectangle(3, 3, Math.Min(Width, Height) - 6, Math.Min(Width, Height) - 6);
            EquipmentRarityFrame.Draw(e.Graphics, Item, bounds);
            RerollCardPresentation.Draw(e.Graphics, bounds, PowerText ?? "", DisplayEnchanted, OreMetrics.Scale(this));
        }
    }
}
sealed partial class ToolboxForm
{
    TextBox rerollSearch;
    EquipmentIconView rerollIcon;
    PixelLabel rerollNote;
    OreTextView rerollDetails;
    OreTextView rerollProgressDetails;
    PixelLabel rerollCurrentHeading;
    Panel rerollLeftPane, rerollRightPane, rerollCurrentCard, rerollControlsCard;
    Button rerollPause, rerollStart, rerollStop;
    CheckBox rerollShowDetails;
    RerollSnapshot rerollSnapshot;
    bool rerollObserving, rerollReading, rerollSearchUpdating;
    int rerollGeneration;
    long lastRerollRead = -10000;
    string rerollMessage = "请先读取背包并设置装备目标";
    // 页面明确交付状态，不能让“原生查询成功”等同于“自动刷词条完成”。
    void BuildRerollPage()
    {
        // 左侧背包/整批预算，右侧队列/当前装备/进度；目标在单独窗口设置。
        rerollLeftPane = new OreScrollPanel { Size = new Size(420, 550), BackColor = OreTheme.Background };
        rerollRightPane = new OreScrollPanel { Location = new Point(432, 0), Size = new Size(420, 550), BackColor = OreTheme.Background };
        pages[7].Controls.Add(rerollLeftPane); pages[7].Controls.Add(rerollRightPane);
        var search = Card(rerollLeftPane, 0, 430); search.Width = 420;
        BuildRerollInventory(search);
        BuildRerollTargets();
        var reading = rerollCurrentCard = StatusCard(rerollRightPane, 172, 220); reading.Width = 420;
        rerollCurrentHeading = (PixelLabel)LabelAt(reading, L10n.T("当前刷取装备"), 20, 12, 200, 30);
        rerollShowDetails = new OreCheckbox { Text = L10n.T("显示详情"), Location = new Point(234, 8), Size = new Size(170, 28) };
        reading.Controls.Add(rerollShowDetails);
        rerollShowDetails.CheckedChanged += delegate { RefreshRerollDisplay(); };
        rerollIcon = new EquipmentIconView { Location = new Point(16, 42), Size = new Size(74, 74), BackColor = OreTheme.Field };
        reading.Controls.Add(rerollIcon);
        rerollNote = (PixelLabel)LabelAt(reading, "", 98, 42, 306, 74);
        rerollNote.PixelScale = .7f;
        // 最多 16 个实际效果，使用滚动文本，不能裁掉低位效果或费用。
        rerollDetails = new OreTextView { Location = new Point(16, 122), Size = new Size(388, 84),
            BackColor = OreTheme.Field, ForeColor = OreTheme.Text,
            Font = Font };
        reading.Controls.Add(rerollDetails);
        BuildRerollQueue();
        rerollControlsCard = StatusCard(rerollRightPane, 372, 178); rerollControlsCard.Width = 420;
        rerollProgressDetails = new OreTextView { Location = new Point(16, 10), Size = new Size(388, 64), BackColor = OreTheme.Field,
            ForeColor = OreTheme.Text, Font = Font };
        rerollControlsCard.Controls.Add(rerollProgressDetails);
        rerollStart = ButtonAt(rerollControlsCard, L10n.T("开始"), 16, 84, 388);
        rerollPause = ButtonAt(rerollControlsCard, L10n.T("暂停"), 16, 130, 190);
        rerollStop = ButtonAt(rerollControlsCard, L10n.T("停止"), 214, 130, 190);
        rerollStart.Height = rerollPause.Height = rerollStop.Height = 40;
        rerollStart.Enabled = false;
        rerollPause.Enabled = false;
        rerollStart.Click += delegate { StartRerollRun(); };
        rerollPause.Click += delegate
        {
            if (rerollRun != null)
            {
                rerollRun.Pause();
                rerollMessage = RerollRunMessage(rerollRun.Reason);
                if (rerollRun.State == RerollRunState.Uncertain) rerollBlockedInstance = rerollRun.Snapshot.Instance;
            }
            RefreshRerollDisplay();
        };
        rerollStop.Click += delegate { StopRerollObservation(); };
        FormClosed += delegate { StopRerollObservation(); };
        SearchRerollEquipment();
        RefreshRerollDisplay();
    }
    // 搜索直接筛选实际背包装备；切换分类保持已选 UID，不能用展示类型代替执行装备。
    void SearchRerollEquipment()
    {
        if (rerollInventory == null || rerollSearchUpdating) return;
        rerollSearchUpdating = true;
        try
        {
            int category = Math.Max(0, rerollEquipmentCategory.SelectedIndex);
            rerollEquipmentCategory.Items.Clear();
            rerollEquipmentCategory.Items.Add(L10n.T("全部"));
            for (int i = 0; i < 7; i++) rerollEquipmentCategory.Items.Add(EquipmentGamePresentation.Category((EquipmentCategory)i));
            rerollEquipmentCategory.SelectedIndex = category;
            int slots = Math.Max(0, rerollSlotFilter.SelectedIndex);
            rerollSlotFilter.Items.Clear();
            rerollSlotFilter.Items.AddRange(new object[] { L10n.T("全部词条数"), L10n.T("1 词条"), L10n.T("2 词条"), L10n.T("3 词条") });
            rerollSlotFilter.SelectedIndex = slots;
            rerollInventory.SlotsFilter = slots;
            rerollInventory.Category = category; rerollInventory.Search = rerollSearch.Text;
            ((RerollSearchBox)rerollSearch).RefreshHint();
            rerollInventory.RefreshItems(true);
            RefreshRerollInventoryNote();
        }
        finally { rerollSearchUpdating = false; }
        RefreshRerollTargets(); RefreshRerollDisplay();
    }
    // 与主轮询隔离的异步读数；没有任何写请求、游戏调用或购买行为。
    async void ReadRerollObservation()
    {
        if (rerollReading || !rerollObserving || reader == null || closing) return;
        rerollReading = true;
        int pid = reader.Pid, expected = rerollGeneration;
        try
        {
            var observed = await Task.Run(() => RerollObserver.Read(pid));
            if (closing || IsDisposed || expected != rerollGeneration || !rerollObserving || reader == null || reader.Pid != pid) return;
            if (rerollSnapshot != null && rerollSnapshot.Instance != observed.Instance)
            { rerollInventory.Items.Clear(); rerollInventory.Selected.Clear(); rerollInventory.RefreshItems(true); RefreshRerollInventoryNote(); }
            rerollSnapshot = observed;
            RefreshRerollTargets();
            if (rerollRun == null || observed.State != "Selected") rerollMessage = ObservationMessage(observed.State);
            if (observed.Instance == rerollBlockedInstance || observed.ActionStatus == "ResultUncertain" || observed.ActionStatus == "ResultTimedOut")
            { rerollBlockedInstance = observed.Instance; rerollMessage = RerollRunMessage("ResultUncertain"); }
            RefreshRerollDisplay();
        }
        catch (Exception error)
        {
            if (!closing && !IsDisposed && expected == rerollGeneration)
            {
                // 失败读数不能保留“可开始”的旧选中状态；正常停止仍可展示最后已确认进度。
                rerollSnapshot = null;
                StopRerollObservation();
                // 区分本体准入、组件协议、缺失文件及失效读数，不能一律误导用户重新安装。
                var compatibility = error as GameCompatibilityException;
                rerollMessage = compatibility != null ? compatibility.ReasonKey :
                    error is RerollComponentVersionException ? "铁匠组件版本不匹配，请完全退出游戏后更新全部组件" :
                    error is System.IO.FileNotFoundException || error is System.IO.DirectoryNotFoundException ?
                        "未检测到铁匠组件，请更新全部组件并重启游戏" :
                        "铁匠读数已失效，请重新连接游戏后读取";
                RefreshRerollDisplay();
            }
            ToolboxLog.Error("Reroll.Observe", error);
        }
        finally { rerollReading = false; }
    }
    // F9、暂停、断线和退出增加代数，迟到的异步结果不能恢复读取或显示旧装备。
    void StopRerollObservation()
    {
        if (rerollRun != null && RerollRunning)
        {
            rerollRun.Stop();
            if (rerollRun.State == RerollRunState.Uncertain) rerollBlockedInstance = rerollRun.Snapshot.Instance;
        }
        rerollObserving = false;
        rerollGeneration++;
        if (reader == null) { rerollSnapshot = null; if (rerollInventory != null) { rerollInventory.Items.Clear(); rerollInventory.Selected.Clear(); rerollInventory.RefreshItems(true); } }
        rerollMessage = rerollRun == null ? "读取已停止；未执行刷新。" : RerollRunMessage(rerollRun.Reason);
        if (rerollPause != null && !rerollPause.IsDisposed) rerollPause.Enabled = false;
        RefreshRerollDisplay();
    }
    void PollRerollObservation(long now)
    {
        if (RerollRunning)
        {
            if (now - lastRerollRead >= 250) { lastRerollRead = now; ReadRerollRun(now); }
            return;
        }
        if (pageIndex == 7 && reader != null && !rerollStarting && !rerollRunReading && !rerollInventoryReading && !rerollObserving)
            rerollObserving = true;
        if (rerollObserving && now - lastRerollRead >= 1000)
        {
            lastRerollRead = now;
            ReadRerollObservation();
        }
    }
    // 状态消息分别解释缺少玩家、取消选择、列表刷新、费用不认识和身份切换，方便人工反馈。
    static string ObservationMessage(string state)
    {
        switch (state)
        {
            case "Selected": return "已核对当前选中装备；只读取，不消耗材料。";
            case "NoPlayer": return "未读取到本地玩家，请进入游戏场景";
            case "MultiplePlayers": return "检测到多个本地玩家，铁匠读取已停止";
            case "BlacksmithClosed": return "先打开游戏铁匠，再开始；每件装备使用自己的目标。";
            case "NoSelection": return "请在游戏铁匠列表中选中装备";
            case "SelectionUnverified": return "铁匠列表正在刷新或无法核对选中装备，请重新选择后读取";
            case "SelectionChanged": return "读取过程中选中装备已变化，请重新读取";
            case "UnknownCost": return "原生费用包含无法识别的类型或数值，读取已停止";
            default: return "铁匠装备数据不可读或尚未选择装备";
        }
    }
    // 原生显示值/原始强度/原生等级和费用并列显示；显示文本不能成为数值比较或最高要求的依据。
    void RefreshRerollDisplay()
    {
        if (rerollNote == null || rerollNote.IsDisposed || rerollSearchUpdating) return;
        string targetUid = RerollRunning ? rerollRun.ActiveItem.Uid : rerollEditingGear == null ? "" : rerollEditingGear.Uid;
        if (rerollDisplayedTargetUid != targetUid) RefreshRerollTargets();
        // 运行提示只出现在下方状态区；装备区域只显示装备或空状态，避免同一句说明重复。
        rerollNote.Text = L10n.T("暂无刷取装备");
        string details = "";
        var activeItem = rerollRun == null ? null : rerollRun.ActiveItem;
        rerollIcon.Item = null;
        rerollIcon.DisplayEnchanted = null;
        if (rerollSnapshot != null && rerollSnapshot.State == "Selected" && (activeItem == null || activeItem.Matches(rerollSnapshot)))
        {
            var r = rerollSnapshot;
            EquipmentRarity rarity;
            Enum.TryParse(r.Rarity.Substring(r.Rarity.LastIndexOf('.') + 1), out rarity);
            rerollIcon.Item = new EquipmentItem { Type = r.Type, Rarity = rarity, SoulStorm = r.Storm };
            rerollIcon.PowerText = r.Power.ToString();
            var backpackItem = rerollInventory.Items.FirstOrDefault(i => i.Matches(r));
            rerollIcon.DisplayEnchanted = backpackItem == null ? (bool?)null : backpackItem.DisplayEnchanted;
            rerollNote.Text = EquipmentGamePresentation.Name(r.Type, L10n.Language) + "\n" + EquipmentGamePresentation.Term("header_power") + " " + r.Power +
                " · " + EquipmentGamePresentation.Rarity(rarity) + "\n" + RerollCardPresentation.EnchantmentText(rerollIcon.DisplayEnchanted);
            details = String.Format(L10n.T("词条槽：{0}；所需铁匠等级：{1}"), r.Effects.Count, r.RequiredBlacksmithLevel);
            foreach (var effect in r.Effects)
            {
                details += "\r\n" + RerollEffectName(effect.Type) + " · " + String.Format(L10n.T("等级 {0}"), effect.Level < 0 ? "?" : effect.Level.ToString()) +
                    (String.IsNullOrEmpty(effect.NativeValue) ? "" : " · " + effect.NativeValue);
                if (rerollShowDetails.Checked) details += "\r\n" + L10n.T("刷新费用") + "：" + RerollCurrencyText(effect.Cost);
                if (rerollShowDetails.Checked)
                    details += "\r\n" + L10n.T("原始数值") + "：" + effect.Intensity;
                if (effect.Failures != 0) details += "\r\n" + L10n.T("原生刷新条件未满足：") + " " + FailureMessage(effect.Failures);
            }
            details += "\r\n" + L10n.T("当前余额") + "：" + RerollCurrencyText(r.Balance);
            if (rerollShowDetails.Checked)
            {
            details += "\r\nUID：" + r.SelectedUid;
            details += "\r\n" + String.Format(L10n.T("游戏刷新计数：{0}；收到玩家刷新通知：{1}（仅观察）"), r.NativeRerolls, r.MessageCount);
            details += "\r\n" + L10n.T(r.MessageObserver == 1 ? "已连接铁匠" :
                r.MessageObserver == 2 ? "刷新消息载荷不可读，不能用于完成确认" : "刷新消息监听未建立，不能用于完成确认");
            details += "\r\n" + String.Format(L10n.T("费用及余额顺序：{0}、{1}、{2}"),
                EquipmentGamePresentation.Term("Currency_Emerald"), EquipmentGamePresentation.Term("Currency_SpringStone"), EquipmentGamePresentation.Term("Currency_EnchantmentPoint"));
            }
            if (r.Failures != 0) details += "\r\n" + L10n.T("原生刷新条件未满足：") + " " + FailureMessage(r.Failures);
        }
        else if (activeItem != null)
        {
            // 切换尚未确认时仅显示任务装备预览，绝不把上一件的词条当成下一件的读数。
            rerollIcon.Item = activeItem.Item;
            rerollIcon.PowerText = activeItem.RawPower.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            rerollIcon.DisplayEnchanted = activeItem.DisplayMetadataKnown ? (bool?)activeItem.DisplayEnchanted : null;
            rerollNote.Text = EquipmentGamePresentation.Name(activeItem.Item);
            details = L10n.T("当前任务装备尚未确认，暂不显示其他装备的词条。");
        }
        // 未变化的快照不重新赋值，保留用户查看后方效果的滚动位置。
        string progress = rerollRun == null ? L10n.T(rerollMessage) : RerollProgress("");
        if (rerollProgressDetails.Text != progress) rerollProgressDetails.Text = progress;
        if (rerollDetails.Text != details) rerollDetails.Text = details;
        RefreshRerollQueue();
        rerollIcon.Invalidate();
        rerollInventory.CurrentUid = rerollSnapshot != null && rerollSnapshot.State == "Selected" ? rerollSnapshot.SelectedUid : null;
        rerollInventory.CurrentPower = rerollSnapshot == null ? 0 : rerollSnapshot.Power;
        rerollInventory.Invalidate();
        rerollQueueList.CurrentUid = activeItem != null && rerollSnapshot != null && activeItem.Matches(rerollSnapshot) ? rerollSnapshot.SelectedUid : null;
        rerollQueueList.CurrentPower = rerollSnapshot == null ? 0 : rerollSnapshot.Power;
        rerollQueueList.Invalidate();
        RefreshRerollBudgetControls();
        RefreshRerollButtons();
    }
    // 每个枚举位有独立含义；即使未来 SDK 增加失败项，未知位也不能显示成成功。
    static string FailureMessage(int bits)
    {
        var messages = new System.Collections.Generic.List<string>();
        if ((bits & 1) != 0) messages.Add(L10n.T("装备或上下文无效"));
        if ((bits & 2) != 0) messages.Add(L10n.T("铁匠等级不足"));
        if ((bits & 4) != 0) messages.Add(L10n.T("材料或货币不足"));
        if ((bits & 8) != 0) messages.Add(L10n.T("没有可刷新词条"));
        if ((bits & 16) != 0) messages.Add(L10n.T("未知原生失败原因"));
        return String.Join(" / ", messages);
    }
}

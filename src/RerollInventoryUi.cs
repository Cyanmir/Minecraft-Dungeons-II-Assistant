// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 分类/搜索只过滤显示。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Globalization;

// 原生占位提示只显示搜索用途，空字符串仍表示无过滤；不能把提示文本当查询内容。
sealed class RerollSearchBox : TextBox
{
    internal string HintKey;
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessage(IntPtr window, int message, IntPtr parameter, string text);
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); RefreshHint(); }
    internal void RefreshHint() { if (IsHandleCreated) SendMessage(Handle, 0x1501, new IntPtr(1), L10n.T(HintKey)); }
}

sealed class RerollInventoryGrid : Control
{
    internal List<RerollInventoryItem> Items = new List<RerollInventoryItem>();
    internal readonly RerollOrderedSelection Selected = new RerollOrderedSelection();
    internal bool SelectionEnabled = true;
    // 非 null 表示只读任务网格：严格保留冻结任务顺序，不经过背包搜索/排序。
    internal RerollTaskInfo[] TaskRows;
    internal string Search = "", CurrentUid;
    internal int CurrentPower;
    internal int Category;
    internal int SlotsFilter;
    internal event Action SelectionChanged;
    internal event Action<RerollInventoryItem> PreviewRequested;
    internal event Action<RerollInventoryItem> TargetsRequested;
    readonly OreScrollBar scroll = new OreScrollBar();
    readonly ToolTip tip = new ToolTip();
    List<RerollInventoryItem> visible = new List<RerollInventoryItem>();
    int offset, focused;
    // 用户要求固定每行八件；紧凑卡片只保留图标/力量/附魔角标，长名称放详情和悬停提示。
    int Columns { get { return 8; } }
    int ScrollWidth { get { return Math.Max(12, (int)(14 * OreMetrics.Scale(this))); } }
    int CellWidth { get { return Math.Max(1, (Width - ScrollWidth - 4) / Columns); } }
    int PictureSize { get { return Math.Max(1, Math.Min(CellWidth - Math.Max(4, (int)(4 * OreMetrics.Scale(this))), (int)(64 * OreMetrics.Scale(this)))); } }
    int PowerHeight { get { return Math.Max(12, (int)(14 * OreMetrics.Scale(this))); } }
    // 勾选框直接叠在图标左上角，不再为每行预留一条空白复选框栏。
    int CellHeight { get { return PictureSize + PowerHeight + Math.Max(4, (int)(4 * OreMetrics.Scale(this))); } }
    internal RerollInventoryGrid()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        TabStop = true; BackColor = OreTheme.Field;
        Controls.Add(scroll);
        scroll.Changed += delegate(int value) { offset = Math.Max(0, Math.Min(scroll.Maximum, value)); RefreshItems(false); };
    }
    internal void RefreshItems(bool reset)
    {
        if (reset) { offset = 0; focused = 0; }
        string query = Search.Trim();
        visible = TaskRows != null ? TaskRows.Select(row => row.Item).ToList() : Items.Where(i => (Category == 0 || (int)i.Item.Category == Category - 1) && (SlotsFilter == 0 || i.Slots == SlotsFilter) &&
            (query.Length == 0 || EquipmentGamePresentation.Name(i.Item).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                i.Item.Type.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
            .OrderBy(i => i.Item.Category).ThenByDescending(i => i.RawPower).ThenBy(i => i.Uid, StringComparer.Ordinal).ToList();
        int page = Math.Max(1, Height / CellHeight);
        scroll.Maximum = Math.Max(0, (visible.Count + Columns - 1) / Columns - page);
        scroll.PageSize = page; offset = Math.Min(offset, scroll.Maximum); scroll.Value = offset;
        scroll.Bounds = new Rectangle(Width - ScrollWidth, 0, ScrollWidth, Height); scroll.Visible = scroll.Maximum > 0; scroll.Invalidate();
        focused = Math.Max(0, Math.Min(focused, visible.Count - 1)); Invalidate();
    }
    internal void SelectVisible()
    {
        if (!SelectionEnabled) return;
        foreach (var item in visible.Where(i => i.Slots > 0)) Selected.Add(item.Uid);
        Invalidate(); if (SelectionChanged != null) SelectionChanged();
    }
    internal void ClearSelection()
    {
        if (!SelectionEnabled) return;
        Selected.Clear(); Invalidate(); if (SelectionChanged != null) SelectionChanged();
    }
    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); if (scroll != null) RefreshItems(false); }
    Rectangle Cell(int index)
    {
        int size = PictureSize;
        return new Rectangle(index % Columns * CellWidth + (CellWidth - size) / 2, (index / Columns - offset) * CellHeight + 2, size, size);
    }
    Rectangle CheckboxBounds(int index)
    {
        var picture = Cell(index);
        int size = Math.Max(1, Math.Min(picture.Width, Math.Max(9, (int)(10 * OreMetrics.Scale(this)))));
        return new Rectangle(picture.Left, picture.Top, size, size);
    }
    int Hit(Point point)
    {
        if (point.X < 0 || point.X >= CellWidth * Columns || point.Y < 0 || point.Y >= Math.Max(1, Height / CellHeight) * CellHeight) return -1;
        int index = (offset + point.Y / CellHeight) * Columns + point.X / CellWidth;
        if (index < 0 || index >= visible.Count) return -1;
        var picture = Cell(index);
        var card = new Rectangle(picture.Left, picture.Top, picture.Width, picture.Height + PowerHeight);
        return card.Contains(point) && card.Bottom <= Height ? index : -1;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        float scale = OreMetrics.Scale(this);
        if (visible.Count == 0)
        {
            PixelText.DrawCjkOrBody(e.Graphics, L10n.T(TaskRows != null ? "先点装备，再选目标词条" : Items.Count == 0 ? "读取背包后显示装备" : "没有符合筛选的装备"), ClientRectangle, OreTheme.Muted, scale * .8f, true);
            return;
        }
        int last = Math.Min(visible.Count, (offset + Math.Max(1, Height / CellHeight)) * Columns);
        for (int i = offset * Columns; i < last; i++)
        {
            var bounds = Cell(i); if (bounds.Bottom + PowerHeight > Height) break;
            var item = visible[i]; EquipmentRarityFrame.Draw(e.Graphics, item.Item, bounds, true);
            bool selected = TaskRows == null && Selected.Contains(item.Uid);
            var task = TaskRows == null ? null : TaskRows[i];
            bool activeTask = task != null && task.Status != "待刷" && task.Status != "已完成";
            if (selected || item.Uid == CurrentUid || Focused && i == focused || activeTask)
                using (var pen = new Pen(activeTask ? TaskColor(task.Status) : selected ? OreTheme.Accent : item.Uid == CurrentUid ? Color.FromArgb(56, 189, 235) : OreTheme.Muted, Math.Max(1, 2 * scale))) e.Graphics.DrawRectangle(pen, Rectangle.Inflate(bounds, 1, 1));
            RerollCardPresentation.Draw(e.Graphics, bounds, "", item.DisplayMetadataKnown ? (bool?)item.DisplayEnchanted : null, scale);
            var checkbox = CheckboxBounds(i); int boxSize = checkbox.Width;
            OreTheme.Fill(e.Graphics, checkbox, task != null ? TaskColor(task.Status) : selected ? OreTheme.Green : OreTheme.Field);
            using (var pen = new Pen(task != null || SelectionEnabled && item.Slots > 0 ? OreTheme.Text : OreTheme.Muted))
                e.Graphics.DrawRectangle(pen, checkbox.Left, checkbox.Top, Math.Max(0, checkbox.Width - 1), Math.Max(0, checkbox.Height - 1));
            if (selected || task != null && task.Status == "已完成")
            {
                using (var pen = new Pen(Color.White, Math.Max(1, scale)))
                { e.Graphics.DrawLine(pen, checkbox.Left + 2, checkbox.Top + boxSize / 2, checkbox.Left + boxSize / 2, checkbox.Bottom - 3);
                  e.Graphics.DrawLine(pen, checkbox.Left + boxSize / 2, checkbox.Bottom - 3, checkbox.Right - 2, checkbox.Top + 2); }
            }
            // 力量区域使用图标框的左右边界；数字靠右对齐，不能按列宽挤到相邻图标。
            // 序号保留在悬停提示，八列小图标不叠加序号，以免遮住风暴/附魔或挤占力量。
            RerollCardPresentation.DrawCompactPower(e.Graphics, PowerText(item), new Rectangle(bounds.Left, bounds.Bottom, bounds.Width, PowerHeight), scale);
        }
    }
    static Color TaskColor(string status)
    {
        switch (status)
        {
            case "已完成": return OreTheme.GreenHover;
            case "刷取中": return Color.FromArgb(56, 189, 235);
            case "已停止": case "未确认": case "未设目标": return OreTheme.DangerHover;
            case "已暂停": case "切换中": case "核对中": return Color.FromArgb(255, 188, 72);
            default: return OreTheme.Muted;
        }
    }
    internal void Reveal(int index)
    {
        if (index < 0 || index >= visible.Count) return;
        focused = index;
        int row = index / Columns, page = Math.Max(1, Height / CellHeight);
        if (row < offset) offset = row; else if (row >= offset + page) offset = row - page + 1;
        RefreshItems(false);
    }
    void Toggle(int index)
    {
        if (TaskRows != null || index < 0 || index >= visible.Count) return;
        focused = index; ToggleSelection(visible[index].Uid);
    }
    internal void ToggleSelection(string uid)
    {
        if (!SelectionEnabled || !Items.Any(item => item.Uid == uid && item.Slots > 0)) return;
        if (!Selected.Contains(uid) && TargetsRequested != null) TargetsRequested(Items.First(item => item.Uid == uid));
        if (!Selected.Remove(uid)) Selected.Add(uid);
        Invalidate(); if (SelectionChanged != null) SelectionChanged();
    }
    internal bool AddSelection(string uid)
    {
        if (TaskRows != null || !SelectionEnabled || !Items.Any(item => item.Uid == uid && item.Slots > 0)) return false;
        if (Selected.Add(uid)) { Invalidate(); if (SelectionChanged != null) SelectionChanged(); }
        return true;
    }
    internal List<RerollInventoryItem> OrderedItems()
    {
        var byUid = Items.ToDictionary(item => item.Uid, StringComparer.Ordinal);
        return Selected.Where(byUid.ContainsKey).Select(uid => byUid[uid]).ToList();
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); Focus(); if (e.Button != MouseButtons.Left) return;
        int index = Hit(e.Location); if (index < 0) return; focused = index;
        // 仅左上角实际复选框入队；图片/力量查看详情，格子间隙不触发任何动作。
        if (TaskRows == null && CheckboxBounds(index).Contains(e.Location)) Toggle(index);
        else if (PreviewRequested != null) PreviewRequested(visible[index]);
        Invalidate();
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e); int index = Hit(e.Location);
        string value = index < 0 ? "" : EquipmentGamePresentation.Name(visible[index].Item) + "\n" +
            EquipmentGamePresentation.Category(visible[index].Item.Category) + " · " + EquipmentGamePresentation.Rarity(visible[index].Item.Rarity) +
            " · " + EquipmentGamePresentation.Term("header_power") + " " + PowerText(visible[index]) + "\n" +
            String.Format(L10n.T("可刷新词条：{0}"), visible[index].Slots) + " · " + RerollCardPresentation.EnchantmentText(visible[index].DisplayMetadataKnown ? (bool?)visible[index].DisplayEnchanted : null) +
            "\n" + L10n.T("单击查看详情；勾选加入队列");
        if (index >= 0 && TaskRows != null)
            value = TaskRows[index].Number + ". " + value.Replace(L10n.T("单击查看详情；勾选加入队列"), L10n.T("单击查看详情")) +
                "\n" + L10n.T(TaskRows[index].Status) + " · " + String.Format(L10n.T("{0} 次"), TaskRows[index].Rerolls) +
                "\n" + L10n.T("目标词条") + "：" + String.Join(" / ", TaskRows[index].Targets.Select(t => EquipmentGamePresentation.Name(t.Type, L10n.Language) + " " + t.Level));
        else if (index >= 0 && Selected.Contains(visible[index].Uid))
            value = Selected.Position(visible[index].Uid) + ". " + value;
        if (tip.GetToolTip(this) != value) tip.SetToolTip(this, value);
    }
    string PowerText(RerollInventoryItem item) { return item.Uid == CurrentUid ? CurrentPower.ToString() : item.RawPower.ToString("0.##", CultureInfo.InvariantCulture); }
    protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); offset = Math.Max(0, Math.Min(scroll.Maximum, offset - Math.Sign(e.Delta))); RefreshItems(false); }
    protected override bool IsInputKey(Keys key) { return new[] { Keys.Left, Keys.Right, Keys.Up, Keys.Down }.Contains(key) || base.IsInputKey(key); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Space) { Toggle(focused); e.Handled = true; return; }
        if (e.KeyCode == Keys.Enter && visible.Count > 0) { if (PreviewRequested != null) PreviewRequested(visible[focused]); e.Handled = true; return; }
        int move = e.KeyCode == Keys.Left ? -1 : e.KeyCode == Keys.Right ? 1 : e.KeyCode == Keys.Up ? -Columns : e.KeyCode == Keys.Down ? Columns : 0;
        if (move == 0) return;
        focused = Math.Max(0, Math.Min(visible.Count - 1, focused + move));
        int row = focused / Columns, page = Math.Max(1, Height / CellHeight);
        if (row < offset) offset = row; else if (row >= offset + page) offset = row - page + 1;
        RefreshItems(false); e.Handled = true;
    }
    protected override void Dispose(bool disposing) { if (disposing) tip.Dispose(); base.Dispose(disposing); }
}

sealed partial class ToolboxForm
{
    RerollInventoryGrid rerollInventory;
    OreSelect rerollEquipmentCategory;
    OreSelect rerollSlotFilter;
    PixelLabel rerollInventoryNote, rerollInventoryHeading;
    Button rerollInventoryRead;
    bool rerollInventoryReading;
    Panel rerollInventoryCard;
    Button rerollSelectAll, rerollClearSelection;
    int rerollInventoryPid;
    void BuildRerollInventory(Panel parent)
    {
        rerollInventoryCard = parent;
        var heading = rerollInventoryHeading = (PixelLabel)LabelAt(parent, L10n.T("背包"), 20, 8, 380, 30); heading.PixelScale = 1; heading.Wrap = false; heading.FlushLeft = true;
        rerollInventoryRead = ButtonAt(parent, L10n.T("读取背包"), 244, 44, 160);
        ((OreButton)rerollInventoryRead).PixelScale = .85f;
        rerollInventoryRead.Height = 36;
        rerollEquipmentCategory = new OreSelect { Location = new Point(20, 88), Size = new Size(190, 36) };
        parent.Controls.Add(rerollEquipmentCategory);
        rerollSlotFilter = new OreSelect { Location = new Point(218, 88), Size = new Size(182, 36) };
        parent.Controls.Add(rerollSlotFilter);
        rerollSearch = new RerollSearchBox { HintKey = "搜索装备", AutoSize = false, Location = new Point(20, 48), Size = new Size(216, 28), BackColor = OreTheme.Field, ForeColor = OreTheme.Text, BorderStyle = BorderStyle.FixedSingle };
        parent.Controls.Add(rerollSearch);
        rerollInventory = new RerollInventoryGrid { Location = new Point(20, 130), Size = new Size(380, 188) };
        parent.Controls.Add(rerollInventory);
        var selectAll = rerollSelectAll = ButtonAt(parent, L10n.T("全选"), 16, 326, 190);
        var clear = rerollClearSelection = ButtonAt(parent, L10n.T("清空选择"), 214, 326, 190);
        ((OreButton)selectAll).PixelScale = ((OreButton)clear).PixelScale = .85f;
        selectAll.Click += delegate { if (!RerollRunning && !rerollStarting) rerollInventory.SelectVisible(); };
        clear.Click += delegate { if (!RerollRunning && !rerollStarting) rerollInventory.ClearSelection(); };
        rerollInventoryNote = (PixelLabel)LabelAt(parent, "", 16, 374, 388, 48); rerollInventoryNote.PixelScale = .7f;
        rerollEquipmentCategory.SelectedIndexChanged += delegate { if (!rerollSearchUpdating) SearchRerollEquipment(); };
        rerollSlotFilter.SelectedIndexChanged += delegate { if (!rerollSearchUpdating) SearchRerollEquipment(); };
        rerollSearch.TextChanged += delegate { SearchRerollEquipment(); };
        rerollInventory.SelectionChanged += delegate
        {
            // 修改勾选表示准备新一批任务；运行中选择被冻结，不会清掉正在执行的队列。
            if (!RerollRunning && !rerollStarting) rerollRun = null;
            SyncQueuedGearTargets();
            RefreshRerollInventoryNote(); RefreshRerollDisplay();
        };
        rerollInventory.PreviewRequested += delegate(RerollInventoryItem item) { EditRerollGearTargets(item); ShowRerollGearDetails(item); };
        rerollInventory.TargetsRequested += EditRerollGearTargets;
        rerollInventoryRead.Click += delegate { ReadRerollInventory(); rerollObserving = true; ReadRerollObservation(); };
    }
    void RefreshRerollInventoryNote()
    {
        if (rerollInventoryNote == null) return;
        rerollInventoryNote.Text = String.Format(L10n.T("已选 {0} 件；未勾选时刷当前装备。"), rerollInventory.Selected.Count) + "\n" + L10n.T("按勾选顺序执行；单击图标查看详情");
    }
    async void ReadRerollInventory()
    {
        if (reader == null || rerollInventoryReading || RerollRunning || rerollStarting || closing || connecting) return;
        rerollInventoryReading = true; int expected = rerollGeneration, pid = reader.Pid; var activeReader = reader;
        RefreshRerollButtons();
        try
        {
            string session = reader.InventorySession();
            var items = await Task.Run(() =>
            {
                // 与 UI 主读取器分离缓存，避免后台采样与健康轮询同时修改反射字典。
                using (var sampling = new HealthReader())
                {
                if (!sampling.Alive || sampling.Pid != pid || sampling.InventorySession() != session) throw new InvalidOperationException("Inventory session changed");
                var rows = sampling.ReadInventory().Select(RerollInventoryItem.Decode).Where(i => i != null).ToList();
                if (sampling.InventorySession() != session || rows.Select(i => i.Uid).Distinct().Count() != rows.Count)
                    throw new InvalidOperationException("Inventory changed");
                return rows;
                }
            });
            if (closing || IsDisposed || expected != rerollGeneration || reader != activeReader || reader.Pid != pid || reader.InventorySession() != session) return;
            if (rerollInventoryPid != pid) { rerollInventory.Selected.Clear(); rerollGearTargets.Clear(); rerollEditingGear = null; }
            rerollInventoryPid = pid;
            rerollInventory.Items = items;
            rerollInventory.Selected.IntersectWith(items.Select(i => i.Uid));
            foreach (string uid in rerollGearTargets.Keys.Where(uid => !items.Any(item => item.Uid == uid)).ToArray()) rerollGearTargets.Remove(uid);
            if (rerollEditingGear != null) rerollEditingGear = items.FirstOrDefault(item => item.Uid == rerollEditingGear.Uid);
            rerollInventory.RefreshItems(false); RefreshRerollInventoryNote(); RefreshRerollDisplay();
        }
        catch (Exception error)
        {
            ToolboxLog.Error("Reroll.Inventory", error);
            if (!closing && !IsDisposed && expected == rerollGeneration)
            { rerollInventory.Items.Clear(); rerollInventory.Selected.Clear(); rerollInventory.RefreshItems(true); rerollInventoryNote.Text = L10n.T("背包读取失败，请重新连接游戏"); }
        }
        finally { rerollInventoryReading = false; if (!closing && !IsDisposed) RefreshRerollButtons(); }
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 显示装备计划和出售回执，管理列表滚动与只读采样。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

// 回执只确认数量。物品从背包消失不作为组件出售的依据。
sealed class EquipmentVisualRow
{
    public EquipmentDecision Decision;
    public bool Departed;
}

sealed class EquipmentVisualState
{
    public readonly List<EquipmentVisualRow> Rows = new List<EquipmentVisualRow>();
    public int Confirmed, NativePending;
    public bool HasReceipt, Live;
    public string Note = "尚未生成清单；当前不会出售装备";
    public void Reset(IEnumerable<EquipmentDecision> plan, bool live)
    {
        Rows.Clear();
        Confirmed = NativePending = 0;
        HasReceipt = false;
        Live = live;
        Update(plan);
        Note = live ? "正在等待游戏端确认，已有装备保留" : "仅预览，未出售任何物品";
    }

    // 清空采样/统计状态，断开连接时使用。
    public void Clear()
    {
        Rows.Clear();
        Confirmed = NativePending = 0;
        HasReceipt = Live = false;
        Note = "规则已更新，请重新生成清单";
    }

    // 用背包快照更新行，不将暂时消失的物品直接计为已出售。
    public void Update(IEnumerable<EquipmentDecision> plan)
    {
        var list = plan.Where(d => d.Item.Category != EquipmentCategory.Unsupported).ToList();
        if (list.Any(d => String.IsNullOrEmpty(d.Item.Id)) || list.Select(d => d.Item.Id).Distinct().Count() != list.Count)
            throw new Exception("Visualization inventory identities are incomplete");
        var present = new HashSet<string>(list.Select(d => d.Item.Id));
        foreach (var row in Rows)
            if (!present.Contains(row.Decision.Item.Id))
                row.Departed = true;
        foreach (var decision in list)
        {
            var row = Rows.FirstOrDefault(r => r.Decision.Item.Id == decision.Item.Id);
            if (row == null)
                Rows.Add(new EquipmentVisualRow { Decision = decision });
            else
            {
                row.Decision = decision;
                row.Departed = false;
            }
        }

        // 限制长会话历史增长，但不能丢掉当前背包任何显示行。
        foreach (var row in Rows.Where(r => r.Departed).Take(Math.Max(0, Rows.Count - 2048)).ToList())
            Rows.Remove(row);
    }

    public void Receipt(int sold, int pending, string status)
    {
        if (sold < 0 || pending < 0 || (HasReceipt && sold < Confirmed))
            throw new Exception("Visualization receipt regressed");
        Confirmed = sold;
        NativePending = pending;
        HasReceipt = true;
        Note = status == "Completed" ? "已有装备整理完成" : status == "NativeRejected" ? "游戏拒绝回收此物品，已保留并跳过" : "正在整理";
    }
}

sealed class EquipmentVisualList : Control
{
    public EquipmentVisualState State;
    public int Filter;
    readonly OreScrollBar scrollbar = new OreScrollBar();
    readonly ToolTip tip = new ToolTip();
    int offset;
    string hovered;
    static readonly Color warning = Color.FromArgb(239, 191, 104);
    public EquipmentVisualList()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        TabStop = true;
        Controls.Add(scrollbar);
        scrollbar.Changed += delegate (int value)
        {
            offset = Math.Max(0, Math.Min(scrollbar.Maximum, value));
            RefreshRows();
        };
    }

    List<EquipmentVisualRow> VisibleRows()
    {
        return State == null ? new List<EquipmentVisualRow>() : State.Rows.Where(r => Filter == 0 || (Filter == 1 && !r.Departed && r.Decision.Sell) || (Filter == 2 && !r.Departed && !r.Decision.Sell) || (Filter == 3 && r.Departed)).OrderBy(r => r.Departed ? 1 : r.Decision.Sell ? 0 : 2).ThenBy(r => r.Decision.Item.Category).ThenBy(r => r.Decision.Item.Type, StringComparer.Ordinal).ThenBy(r => r.Decision.Item.Id, StringComparer.Ordinal).ToList();
    }

    // 装备列表一行的像素高度，绘制与滚动使用同一值。
    int RowHeight
    {
        get
        {
            return Math.Max(44, (int)Math.Round(50 * Height / 230f));
        }
    }

    // 装备列表表头像素高度。
    int HeaderHeight
    {
        get
        {
            return Math.Max(26, (int)Math.Round(29 * Height / 230f));
        }
    }

    // 更新列表数据并重算滚动边界。
    public void RefreshRows()
    {
        int count = VisibleRows().Count;
        int page = Math.Max(1, (Height - HeaderHeight) / RowHeight);
        scrollbar.Maximum = Math.Max(0, count - page);
        scrollbar.PageSize = page;
        offset = Math.Min(offset, scrollbar.Maximum);
        scrollbar.Value = offset;
        scrollbar.Visible = scrollbar.Maximum > 0;
        scrollbar.Bounds = new Rectangle(Math.Max(0, Width - 16), HeaderHeight, 16, Math.Max(1, Height - HeaderHeight));
        scrollbar.Invalidate();
        Invalidate();
    }

    // 把装备列表滚动位置复位到顶部。
    public void ResetScroll()
    {
        offset = 0;
        RefreshRows();
    }

    // 尺寸改变后重算显示/滚动范围，不改变业务配置。
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (scrollbar != null)
            RefreshRows();
    }

    internal static string Caption(EquipmentVisualRow row)
    {
        return row.Departed ? L10n.T("已离开背包") : row.Decision.Sell ? L10n.T("符合回收规则") : ToolboxForm.EquipmentReasonCaption(row.Decision.Reason);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        OreTheme.Fill(e.Graphics, ClientRectangle, OreTheme.Card);
        var rows = VisibleRows();
        int width = Width - (scrollbar.Visible ? 20 : 2);
        int split = (int)(width * .58f);
        float scale = Math.Min(Width / 704f, Height / 230f) * .85f;
        OreTheme.Fill(e.Graphics, new Rectangle(0, 0, width, HeaderHeight), OreTheme.Field);
        PixelText.Draw(e.Graphics, L10n.T("物品") + " / " + EquipmentGamePresentation.Term("inventory_sort_rarity") + " / " + EquipmentGamePresentation.Term("header_power"), new Rectangle(10, 4, split - 18, HeaderHeight - 6), OreTheme.Muted, scale, false, false, false);
        PixelText.Draw(e.Graphics, L10n.T("判定"), new Rectangle(split + 10, 4, width - split - 18, HeaderHeight - 6), OreTheme.Muted, scale, false, false, false);
        if (rows.Count == 0)
        {
            PixelText.Draw(e.Graphics, L10n.T(State == null || State.Rows.Count == 0 ? "生成预览或启用整理后显示装备" : "此分类暂无装备"), new Rectangle(15, HeaderHeight + 18, width - 30, Height - HeaderHeight - 26), OreTheme.Muted, scale, false, true, false);
            return;
        }

        int visible = Math.Max(1, (Height - HeaderHeight) / RowHeight);
        for (int i = 0; i < visible && i + offset < rows.Count; i++)
        {
            var row = rows[i + offset];
            var item = row.Decision.Item;
            int y = HeaderHeight + i * RowHeight;
            Color color = row.Departed ? warning : row.Decision.Sell ? OreTheme.Accent : OreTheme.Muted;
            OreTheme.Fill(e.Graphics, new Rectangle(0, y, width, RowHeight - 2), i % 2 == 0 ? OreTheme.Card : Color.FromArgb(15, 33, 40));
            OreTheme.Fill(e.Graphics, new Rectangle(0, y + 4, 3, RowHeight - 10), color);
            string name = EquipmentGamePresentation.Name(item);
            int iconSize = Math.Max(28, RowHeight - 10);
            EquipmentRarityFrame.Draw(e.Graphics, item, new Rectangle(10, y + 5, iconSize, iconSize));
            int textX = iconSize + 20;
            PixelText.Draw(e.Graphics, name, new Rectangle(textX, y + 5, split - textX - 10, RowHeight / 2 - 4), OreTheme.Text, scale, false, false, false);
            string detail = ToolboxForm.EquipmentCategoryCaption(item.Category) + " / " + (item.Rarity >= EquipmentRarity.Common && item.Rarity <= EquipmentRarity.Unique ? ToolboxForm.EquipmentRarityCaption(item.Rarity) : "?") + " / " + item.Power;
            PixelText.Draw(e.Graphics, detail, new Rectangle(textX, y + RowHeight / 2, split - textX - 10, RowHeight / 2 - 4), OreTheme.Muted, scale * .9f, false, false, false);
            PixelText.Draw(e.Graphics, Caption(row), new Rectangle(split + 10, y + 7, width - split - 20, RowHeight - 12), color, scale, false, true, false);
        }
    }

    // 按滚轮增量调整列表或页面滚动，并保留边界限制。
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        offset = Math.Max(0, Math.Min(scrollbar.Maximum, offset - e.Delta / 120 * 3));
        RefreshRows();
        var handled = e as HandledMouseEventArgs;
        if (handled != null)
            handled.Handled = true;
    }

    // 声明控件自行处理的导航键，避免 WinForms 把它当作焦点切换。
    protected override bool IsInputKey(Keys keyData)
    {
        return keyData == Keys.Down || keyData == Keys.Up || keyData == Keys.PageDown || keyData == Keys.PageUp || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int delta = e.KeyCode == Keys.Down ? 1 : e.KeyCode == Keys.Up ? -1 : e.KeyCode == Keys.PageDown ? scrollbar.PageSize : e.KeyCode == Keys.PageUp ? -scrollbar.PageSize : 0;
        if (delta != 0)
        {
            offset = Math.Max(0, Math.Min(scrollbar.Maximum, offset + delta));
            RefreshRows();
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var rows = VisibleRows();
        int index = e.Y < HeaderHeight ? -1 : offset + (e.Y - HeaderHeight) / RowHeight;
        string text = null;
        if (index >= 0 && index < rows.Count)
        {
            var row = rows[index];
            text = EquipmentGamePresentation.Name(row.Decision.Item) + "\n" + (row.Decision.Item.Type ?? "?") + "\n" + Caption(row) + (row.Departed ? "\n" + L10n.T("物品消失不代表已回收；回收数量以游戏回执为准") : "");
        }

        if (text != hovered)
        {
            hovered = text;
            tip.SetToolTip(this, text);
        }

        base.OnMouseMove(e);
    }

    // 释放本对象拥有的句柄、绘图对象或监听资源，避免退出后继续占用。
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            tip.Dispose();
        base.Dispose(disposing);
    }
}

// 独立读取句柄把昂贵查找移出 UI/心跳；只有此采样器使用句柄，快照和释放在此串行协调。
sealed class EquipmentVisualSampler : IDisposable
{
    readonly object gate = new object ();
    HealthReader cached;
    public List<EquipmentItem> Read(int pid, string expected)
    {
        lock (gate)
            try
            {
                if (cached == null)
                    cached = new HealthReader();
                if (!cached.Alive || cached.Pid != pid || cached.InventorySession() != expected)
                    throw new Exception("Visualization session changed");
                var items = cached.ReadInventory().Select(EquipmentItem.Decode).ToList();
                if (cached.InventorySession() != expected)
                    throw new Exception("Visualization session changed");
                return items;
            }
            catch
            {
                if (cached != null)
                {
                    cached.Dispose();
                    cached = null;
                }

                throw;
            }
    }

    // 释放本对象拥有的句柄、绘图对象或监听资源，避免退出后继续占用。
    public void Dispose()
    {
        lock (gate)
            if (cached != null)
            {
                cached.Dispose();
                cached = null;
            }
    }
}

// 主窗口的一个 partial 部分；事件处理与异步任务共用主窗口状态，退出时统一清理。
sealed partial class ToolboxForm
{
    readonly EquipmentVisualState equipmentVisual = new EquipmentVisualState();
    readonly EquipmentVisualSampler equipmentVisualSampler = new EquipmentVisualSampler();
    EquipmentVisualList equipmentVisualList;
    OreSelect equipmentVisualFilter;
    PixelLabel equipmentVisualSummary, equipmentVisualNote;
    Button equipmentVisualPreview;
    bool equipmentVisualReading, equipmentVisualFinalRead, equipmentVisualIncludeExisting;
    int equipmentVisualGeneration;
    long lastEquipmentVisualRead = -10000;
    static readonly string[] equipmentVisualFilters =
    {
        "全部装备",
        "待回收",
        "保留",
        "已离开背包"
    };
    // 把保护原因转换为可读界面文案，不改变整理判断。
    internal static string EquipmentReasonCaption(string reason)
    {
        return reason == "Equipped" ? EquipmentGamePresentation.Term("tag_equipped") : reason == "Enchanted" ? EquipmentGamePresentation.Term("Label_Enchanted") : L10n.T(ReasonCaption(reason));
    }

    // 按类别枚举显示名称，索引保持七类顺序。
    internal static string EquipmentCategoryCaption(EquipmentCategory category)
    {
        return EquipmentGamePresentation.Category(category);
    }

    // 显示品质名称，不调整枚举阈值语义。
    internal static string EquipmentRarityCaption(EquipmentRarity rarity)
    {
        return EquipmentGamePresentation.Rarity(rarity);
    }

    void BuildEquipmentVisual()
    {
        var card = StatusCard(pages[5], 1382, 428);
        LabelAt(card, L10n.T("回收清单与进度"), 20, 16, 704, 30);
        equipmentVisualSummary = (PixelLabel)LabelAt(card, "", 20, 55, 704, 38);
        equipmentVisualSummary.PixelScale = .85f;
        equipmentVisualFilter = new OreSelect
        {
            Location = new Point(20, 101),
            Size = new Size(338, 40)
        };
        foreach (var name in equipmentVisualFilters)
            equipmentVisualFilter.Items.Add(L10n.T(name));
        equipmentVisualFilter.SelectedIndex = 0;
        card.Controls.Add(equipmentVisualFilter);
        equipmentVisualPreview = ButtonAt(card, L10n.T("预览全部已有"), 380, 101, 344);
        equipmentVisualPreview.Click += async delegate
        {
            await PreviewEquipment(true);
        };
        equipmentVisualList = new EquipmentVisualList
        {
            Location = new Point(20, 157),
            Size = new Size(704, 230),
            State = equipmentVisual
        };
        card.Controls.Add(equipmentVisualList);
        equipmentVisualFilter.SelectedIndexChanged += delegate
        {
            equipmentVisualList.Filter = equipmentVisualFilter.SelectedIndex;
            equipmentVisualList.ResetScroll();
        };
        equipmentVisualNote = (PixelLabel)LabelAt(card, "", 20, 396, 704, 26);
        equipmentVisualNote.PixelScale = .75f;
        equipmentVisualNote.ForeColor = OreTheme.Muted;
        RefreshEquipmentVisual();
    }

    // 刷新已建立的装备列表/统计显示。
    void RefreshEquipmentVisual()
    {
        if (equipmentVisualList == null)
            return;
        int eligible = equipmentVisual.Rows.Count(r => !r.Departed && r.Decision.Sell), kept = equipmentVisual.Rows.Count(r => !r.Departed && !r.Decision.Sell);
        equipmentVisualSummary.Text = String.Format(L10n.T("待回收 {0} · 保留 {1} · 游戏确认回收 {2}"), eligible, kept, equipmentVisual.HasReceipt ? equipmentVisual.Confirmed.ToString() : "--");
        equipmentVisualNote.Text = L10n.T(equipmentVisual.Note);
        if (equipmentVisual.Live && equipmentResult != null)
            equipmentResult.Text = equipmentVisualSummary.Text + "\n" + equipmentVisualNote.Text;
        equipmentVisualList.RefreshRows();
    }

    // 切换装备显示文案，不重新发送回收命令。
    void RefreshEquipmentVisualLanguage()
    {
        if (equipmentVisualFilter == null)
            return;
        for (int i = 0; i < equipmentVisualFilters.Length; i++)
            equipmentVisualFilter.Items[i] = L10n.T(equipmentVisualFilters[i]);
        RefreshEquipmentVisual();
    }

    // 按节流读取只读背包展示快照，失败保持可说明状态。
    async void PollEquipmentVisual(long now)
    {
        if (equipmentVisualReading || (!equipmentVisualFinalRead && (now - lastEquipmentVisualRead < 1500 || !equipmentArmed || equipmentBridge == null)) || reader == null)
            return;
        equipmentVisualFinalRead = false;
        equipmentVisualReading = true;
        lastEquipmentVisualRead = now;
        int generation = equipmentVisualGeneration;
        try
        {
            int pid = reader.Pid;
            string expected = reader.InventorySession();
            bool all = equipmentVisualIncludeExisting;
            var baseline = new HashSet<string>(inventoryBaseline.Existing);
            var items = await Task.Run(delegate
            {
                return equipmentVisualSampler.Read(pid, expected);
            });
            if (closing || IsDisposed || generation != equipmentVisualGeneration || reader == null || reader.Pid != pid || !inventoryBaseline.Matches(expected) || reader.InventorySession() != expected)
                return;
            equipmentPlan = equipmentPolicy.Plan(items, baseline, all);
            if (all)
                foreach (var decision in equipmentPlan)
                    if (!baseline.Contains(decision.Item.Id))
                    {
                        decision.Sell = false;
                        decision.Reason = "Existing";
                    }

            equipmentVisual.Update(equipmentPlan);
            RefreshEquipmentVisual();
        }
        catch (Exception e)
        {
            if (!closing && !IsDisposed && equipmentArmed && generation == equipmentVisualGeneration)
            {
                equipmentVisual.Note = "清单暂未更新，显示上次读取结果";
                RefreshEquipmentVisual();
                ToolboxLog.Error("Equipment.Visual", e);
            }
        }
        finally
        {
            equipmentVisualReading = false;
            if (equipmentVisualFinalRead && !closing && !IsDisposed)
                PollEquipmentVisual(lastEquipmentVisualRead);
            else if (!equipmentArmed)
                ReleaseEquipmentVisualReader();
        }
    }

    // 释放展示采样器，避免退出或重连后持有旧进程。
    void ReleaseEquipmentVisualReader()
    {
        Task.Run(delegate
        {
            equipmentVisualSampler.Dispose();
        });
    }

}

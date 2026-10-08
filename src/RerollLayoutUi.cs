// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 左侧背包/预算，右侧队列/当前刷取装备/操作。
// 重排只使用构建时的设计坐标，不把滚动后的坐标再次当基准，也不触发游戏读取或刷新。
using System;
using System.Drawing;
using System.Windows.Forms;

sealed partial class ToolboxForm
{
    void LayoutRerollChildren(Control parent, float scale)
    {
        Rectangle originalParent;
        if (!designBounds.TryGetValue(parent, out originalParent)) return;
        float ratio = parent.Width / (float)originalParent.Width;
        foreach (Control child in parent.Controls)
        {
            Rectangle original;
            if (!designBounds.TryGetValue(child, out original)) continue;
            child.Bounds = new Rectangle((int)Math.Round(original.X * ratio), (int)Math.Round(original.Y * scale),
                Math.Max(1, (int)Math.Round(original.Width * ratio)), Math.Max(1, (int)Math.Round(original.Height * scale)));
            LayoutRerollChildren(child, scale);
        }
    }
    void LayoutRerollPage(float scale)
    {
        if (rerollLeftPane == null) return;
        var page = (OreScrollPanel)pages[7];
        page.ScrollOffset = 0;
        // 收紧栏间与卡片间距；内容区域仍独立滚动，操作按钮不能盖住装备网格。
        int gap = Math.Max(6, (int)Math.Round(8 * scale));
        int width = (page.Width - gap) / 2;
        // 当前支持的最小窗口也采用两列；内部网格/文字滚动，避免目标与装备交错到同一长页面。
        rerollLeftPane.Bounds = new Rectangle(0, 0, width, page.Height);
        rerollRightPane.Bounds = new Rectangle(width + gap, 0, page.Width - width - gap, page.Height);
        var leftScroll = (OreScrollPanel)rerollLeftPane;
        leftScroll.ScrollOffset = 0;
        leftScroll.BarWidth = (int)(16 * scale);
        int limitsHeight = (int)Math.Round(RerollLimitsContentHeight * scale);
        int inventoryHeight = Math.Max((int)(280 * scale), page.Height - limitsHeight - gap);
        // 两列始终预留相同滚动条槽，不能一列出现滚动条后让卡片宽度错开。
        int leftWidth = width - leftScroll.BarWidth;
        rerollInventoryCard.Bounds = new Rectangle(0, 0, leftWidth, inventoryHeight);
        rerollLimitsCard.Bounds = new Rectangle(0, inventoryHeight + gap, leftWidth, limitsHeight);
        leftScroll.ContentHeight = inventoryHeight + limitsHeight + gap;
        LayoutRerollChildren(rerollInventoryCard, scale);
        LayoutRerollChildren(rerollLimitsCard, scale);
        int inset = Math.Max(8, (int)Math.Round(20 * scale));
        int buttonHeight = (int)(34 * scale);
        int buttonY = inventoryHeight - buttonHeight - (int)(12 * scale);
        rerollSelectAll.Top = rerollClearSelection.Top = buttonY;
        rerollSelectAll.Height = rerollClearSelection.Height = buttonHeight;
        // 紧凑背包不再重复底部说明，数量集中到队列标题；手势提示保留在卡片悬停详情。
        rerollInventoryNote.Visible = false;
        rerollInventory.Height = Math.Max(36, buttonY - rerollInventory.Top - (int)(8 * scale));
        var rightScroll = (OreScrollPanel)rerollRightPane;
        rightScroll.ScrollOffset = 0;
        rightScroll.BarWidth = (int)Math.Round(16 * scale);
        int controlsHeight = (int)Math.Round(184 * scale);
        int queueHeight = Math.Max((int)(112 * scale), Math.Min((int)(180 * scale), page.Height / 4));
        int currentHeight = Math.Max((int)(172 * scale), page.Height - queueHeight - controlsHeight - gap * 2);
        int rightHeight = queueHeight + currentHeight + controlsHeight + gap * 2;
        int rightWidth = rerollRightPane.Width - rightScroll.BarWidth;
        rerollQueueCard.Bounds = new Rectangle(0, 0, rightWidth, queueHeight);
        rerollCurrentCard.Bounds = new Rectangle(0, queueHeight + gap, rightWidth, currentHeight);
        rerollControlsCard.Bounds = new Rectangle(0, queueHeight + currentHeight + gap * 2, rightWidth, controlsHeight);
        rightScroll.ContentHeight = rightHeight;
        LayoutRerollChildren(rerollQueueCard, scale);
        LayoutRerollChildren(rerollCurrentCard, scale);
        LayoutRerollChildren(rerollControlsCard, scale);
        rerollQueueList.Height = Math.Max(36, queueHeight - rerollQueueList.Top - (int)(12 * scale));
        rerollQueueList.RefreshItems(false);
        // 主标题、背包标题及两列正文共用 20 逻辑像素边距。
        int innerGap = Math.Max(6, (int)Math.Round(8 * scale));
        // 标题单独成行，搜索与读取按钮同排，避免长译名挤占搜索框。
        rerollInventoryHeading.Bounds = new Rectangle(inset, (int)(8 * scale), leftWidth - inset * 2, (int)(30 * scale));
        if (page.Visible) pageTitle.Left = pageDescription.Left = page.Left + inset;
        rerollInventoryRead.Width = (int)Math.Round(140 * scale);
        rerollInventoryRead.Left = leftWidth - inset - rerollInventoryRead.Width;
        rerollSearch.Left = inset;
        rerollSearch.Width = Math.Max(24, rerollInventoryRead.Left - innerGap - rerollSearch.Left);
        int half = Math.Max(1, (leftWidth - inset * 2 - innerGap) / 2);
        rerollEquipmentCategory.Left = rerollSelectAll.Left = inset;
        rerollEquipmentCategory.Width = rerollSelectAll.Width = half;
        rerollSlotFilter.Left = rerollClearSelection.Left = inset + half + innerGap;
        rerollSlotFilter.Width = rerollClearSelection.Width = leftWidth - inset - rerollSlotFilter.Left;
        rerollInventory.Left = inset;
        rerollInventory.Width = Math.Max(1, leftWidth - inset * 2);
        foreach (Control control in rerollLimitsCard.Controls)
        {
            if (control is PixelLabel) { control.Left = inset; control.Width = Math.Max(1, leftWidth - inset * 2); }
            else if (control is OreNumber) { control.Width = (int)Math.Round(140 * scale); control.Left = leftWidth - inset - control.Width; }
            else if (control is OreSlider) { control.Left = inset; control.Width = Math.Max(1, leftWidth - inset * 2 - (int)Math.Round(160 * scale)); }
        }
        rerollQueueHeading.Left = rerollQueueList.Left = inset;
        rerollQueueHeading.Width = rerollQueueList.Width = Math.Max(1, rightWidth - inset * 2);
        rerollCurrentHeading.Left = inset;
        rerollShowDetails.Left = rightWidth - inset - rerollShowDetails.Width;
        rerollCurrentHeading.Width = Math.Max(1, rerollShowDetails.Left - inset - innerGap);
        rerollDetails.Left = rerollProgressDetails.Left = rerollStart.Left = rerollPause.Left = inset;
        rerollDetails.Width = rerollProgressDetails.Width = rerollStart.Width = Math.Max(1, rightWidth - inset * 2);
        rerollPause.Width = Math.Max(1, (rerollStart.Width - innerGap) / 2);
        rerollStop.Left = rerollPause.Right + innerGap;
        rerollStop.Width = Math.Max(1, rightWidth - inset - rerollStop.Left);
        rerollIcon.Left = inset;
        rerollIcon.Top = (int)Math.Round(48 * scale);
        rerollNote.Top = rerollIcon.Top;
        rerollIcon.Size = new Size((int)(58 * scale), (int)(58 * scale));
        rerollNote.Left = rerollIcon.Right + (int)(8 * scale);
        rerollNote.Width = Math.Max(1, rightWidth - inset - rerollNote.Left);
        rerollNote.Height = (int)(58 * scale);
        rerollDetails.Top = (int)(116 * scale);
        rerollDetails.Height = Math.Max(24, currentHeight - rerollDetails.Top - (int)(12 * scale));
        page.ContentHeight = page.Height;
        rerollInventory.RefreshItems(false);
    }
}

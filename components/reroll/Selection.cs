// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 协议 7 新增 select，只切换原生铁匠 UI，不装备/出售/改写物品。
// 原生 GetItemCategoryTag 得到分类，HandleButtonClicked 触发现有分类按钮的点击回调。
// 不能直接调用 AngelScript 的 OnTabSelectedByPlayer：SDK 明确禁止，会使游戏崩溃。
// 列表索引仅在同一游戏线程内由目标 UID 重新求出；回执须经过 Selected 的完整交叉核对。
using NeoRune;
using UE.Angelscript;
using UE.Engine;
using UE.Dungeons;
using MCD2RerollNativeBindings;
using UE.InventorySystem;
using UE.SWCoreGameplay;
using UE.UIStateContainer;

namespace MCD2RerollBridge;
public partial class ModActor
{
    bool selecting, selectionClicked;
    double selectionDeadline;
    FSWSessionUID selectionUid;
    string selectionType, selectionRarity, selectionCategory, selectionCommand;
    int selectionMessages;
    float selectionRawPower;

    bool AdvanceSelection(APlayerCharacter player, UStore store, FAS_OverallUIState state)
    {
        if (!selecting && (queued == "" || !queued.Contains("|select|"))) return false;
        var id = state.PrimaryPlayerID;
        if (!selecting)
        {
            string command = queued; queued = "";
            selectionCommand = command;
            var p = UKismetStringLibrary.ParseIntoArray(command, "|", false);
            if (suspended || poisoned || waiting || !StillRequested(command) ||
                !Selected(player, state, out var from) || "" + (long)from.UID != p[9] || "" + messageCount != p[11])
            { status = "SelectionChanged"; return false; }
            double rawPower = UKismetStringLibrary.Conv_StringToDouble(p[8]);
            if (!(rawPower >= 0 && rawPower <= 100000))
            { status = "InvalidRequest"; return false; }
            selectionRawPower = (float)rawPower;
            if (!(UKismetStringLibrary.Conv_StringToDouble(p[10]) >= World.RealTime(this)))
            { status = "Expired"; return false; }
            selectionType = p[6]; selectionRarity = p[7]; selectionMessages = messageCount;
            // 从原生所属分类的真实条目取得 UID 结构，SDK 不支持 Int64 -> UInt64 强制转换。
            // 不重解释字符串为内存，不构造地址，更不使用哈希 UID。
            var category = UInventoryHelperLibrary.GetItemCategoryTag(player, new UE.GameplayTags.FGameplayTag { TagName = selectionType });
            int matches = 0;
            foreach (var candidate in UInventoryHelperLibrary.GetEquippableItemsInCategory(player, category))
                if (candidate.ItemData.SessionUID.UID != 0 && "" + (long)candidate.ItemData.SessionUID.UID == p[5])
                { selectionUid = candidate.ItemData.SessionUID; matches++; }
            if (matches != 1) { status = "SelectionUnavailable"; return false; }
            if (!SelectionEntry(player)) { status = "SelectionChanged"; return false; }
            var grid = blacksmith.ItemGrid as UAS_InventoryGridPanel;
            if (!Valid(grid) || !Valid(grid.CategorySelector)) { status = "SelectionUnavailable"; return false; }
            selectionCategory = Tag(category);
            bool changeCategory = Tag(state.PlayerInventories[id].GearSelection.SelectedCategory.Tag) != selectionCategory;
            UE.CommonUI.UCommonButtonBase categoryButton = null;
            foreach (var tab in grid.CategorySelector.RegisteredTabs)
                if (Tag(tab.RegisteredName) == selectionCategory && Valid(tab.CreatedButton) && tab.CreatedButton.GetIsEnabled() &&
                    tab.CreatedButton.IsInteractionEnabled() && tab.DisableReasons.Count == 0) categoryButton = tab.CreatedButton;
            // 分类已选中时，直接按 UID 选择装备。
            if (changeCategory && !Valid(categoryButton)) { status = "SelectionUnavailable"; return false; }
            selecting = true; selectionClicked = false; selectionDeadline = World.RealTime(this) + 5;
            status = "Selecting";
            if (changeCategory)
            {
                // 只调用原生控件函数；如果游戏没有更新对应分类，会超时停止，不能伪造成功。
                if (!StillRequested(selectionCommand)) { selecting = false; status = "Cancelled"; return false; }
                categoryButton.HandleButtonClicked();
                return true;
            }
        }
        if (suspended || poisoned || World.RealTime(this) >= selectionDeadline || !SelectionEntry(player) || messageCount != selectionMessages)
        { selecting = false; status = "SelectionFailed"; return false; }
        // Selected 同时重新取得唯一渲染控件；即使重建期间无选择，也不能沿用旧 grid 地址。
        bool hasSelected = Selected(player, state, out var current);
        if (!Valid(blacksmith) || !Valid(blacksmith.ItemGrid) || !Valid(blacksmith.ItemGrid.ItemGridListContainer))
        { selecting = false; status = "SelectionFailed"; return false; }
        var list = blacksmith.ItemGrid.ItemGridListContainer.ListView;
        if (!Valid(list)) { selecting = false; status = "SelectionFailed"; return false; }
        if (list.IsRefreshPending() || Tag(state.PlayerInventories[id].GearSelection.SelectedCategory.Tag) != selectionCategory) return true;
        var slots = state.PlayerInventories[id].GearSelection.SelectedCategory.Items;
        if (list.GetNumItems() != slots.Count) return true;
        int target = -1;
        for (int i = 0; i < slots.Count; i++)
            if (!slots[i].IsEmpty && slots[i].UID.UID == selectionUid.UID)
            {
                if (target != -1) { selecting = false; status = "SelectionFailed"; return false; }
                target = i;
            }
        if (target < 0) { selecting = false; status = "SelectionFailed"; return false; }
        if (hasSelected && current.UID == selectionUid.UID)
        { selecting = false; status = "SelectedItem"; return false; }
        if (selectionClicked) return true;
        var item = list.GetItemAt(target) as UE.SpicewoodUI.UListItemIndex;
        if (!Valid(item) || item.Index != target || list.GetIndexForItem(item) != target)
        { selecting = false; status = "SelectionFailed"; return false; }
        selectionClicked = true;
        if (!StillRequested(selectionCommand)) { selecting = false; status = "Cancelled"; return false; }
        list.BP_SetSelectedItem(item);
        // 等待下一观察，不把 void 方法返回当成功。
        return true;
    }

    bool SelectionEntry(APlayerCharacter player)
    {
        return UInventoryHelperLibrary.GetInventoryEntryFromID(player, selectionUid, out var entry) &&
            entry.ItemData.SessionUID.UID == selectionUid.UID && UInventoryHelperLibrary.IsItemValid(entry) &&
            !UInventoryHelperLibrary.IsItemSold(entry) && Tag(entry.ItemData.TypeTag) == selectionType &&
            Tag(entry.ItemData.RarityTag) == selectionRarity && entry.ItemData.GeneratorData.PowerGeneratorValues.ItemPower == selectionRawPower;
    }
}

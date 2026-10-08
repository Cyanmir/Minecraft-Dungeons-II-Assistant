// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 协议 7 核对选中 UID、费用、余额及原生消息，执行单次原生刷新。
// 读取铁匠状态并执行选择、刷新命令；目标由工具配置，结果由游戏返回。
// 保存的只是本组件自有邮箱，不写角色存档、词条、材料、货币或随机概率。
using NeoRune;
using MCD2RerollNativeBindings;
using System.Collections.Generic;
using UE.CoreUObject;
using UE.Engine;
using UE.Dungeons;
using UE.Angelscript;
using UE.SWCoreGameplay;
using UE.SWCoreCommon;
using UE.GameplayTags;
using UE.InventorySystem;
using UE.UIStateContainer;
using UE.UMG;
using UE.SpicewoodUI;
using UE.GameplayMessageRuntime;

namespace MCD2RerollBridge;

// 自有固定双份请求信封；同一 epoch/sequence 只接收一次，不能把观察编号当请求编号。
public class Request : USaveGame { public string Command; }
// 6 字段头沿用平台传输读取，Status 内的 ~ / ; / , 分隔原生观测数据，显示文本转义分隔符。
public class Receipt : USaveGame { public string Status; }

public partial class ModActor : AActor
{
    UAS_Blacksmith blacksmith;
    string instance = "";
    int sample;
    UTypedRerollMessage rerollMessages;
    int messageCount;
    double messageClock;
    long messageSelectedUid;
    bool messageUnreadable;
    const string RerolledChannel = "SW.GameplayMessage.PlayerRerolledItemEffect";
    string Tag(FGameplayTag tag) => UBlueprintGameplayTagLibrary.GetDebugStringFromGameplayTag(tag);
    bool Valid(UObject o) => o != null && UKismetSystemLibrary.IsValid(o);

    // 启动只创建独立邮箱、被动监听和定时观察；原生刷新仅由 Execution 的单次请求派发。
    protected override void ReceiveBeginPlay()
    {
        instance = UKismetSystemLibrary.GetObjectName(this) + "@" + World.RealTime(this);
        var request = UGameplayStatics.CreateSaveGameObject(Unreal.ClassOf<Request>()) as Request;
        if (request == null) return;
        request.Command = UKismetStringLibrary.RightPad("OFF#OFF", 2048);
        UGameplayStatics.SaveGameToSlot(request, "MCD2RerollRequest", 0);
        StartMessageObserver();
        Timer.Start(this, nameof(Poll), .1f, loop: true);
        Observe();
    }

    // 只发布本次状态；不可读/菜单退出时不沿用上一件装备。
    void Write(string data)
    {
        var receipt = UGameplayStatics.CreateSaveGameObject(Unreal.ClassOf<Receipt>()) as Receipt;
        if (receipt == null) return;
        sample++;
        ObservationFailed(data);
        receipt.Status = "7|" + instance + "|" + epoch + "|" + sequence + "|" + status + "~" + sample + "~" + data + "|" + World.RealTime(this);
        UGameplayStatics.SaveGameToSlot(receipt, "MCD2RerollReceipt", 0);
    }

    // 游戏缓存的 BlacksmithReducer 将 PlayerRerolledItemEffect 注册为 ActionActor 消息。
    // 监听只观察游戏广播，绝不广播消息、调用刷新、修改商人状态或激活能力。
    void StartMessageObserver()
    {
        var payload = Unreal.ObjectAt<UScriptStruct>("/Script/SWCorePayloads.ActionActor");
        if (!Valid(payload)) return;
        rerollMessages = UAsyncAction_ListenForGameplayMessage.ListenForGameplayMessages(this,
            new FGameplayTag { TagName = RerolledChannel }, payload, EGameplayMessageMatch.ExactMatch) as UTypedRerollMessage;
        if (!Valid(rerollMessages)) return;
        rerollMessages.OnMessageReceived += OnRerolledMessage;
        rerollMessages.Activate();
    }

    // 回调数只记录当前本地玩家的消息，不等于工具已完成次数。
    // messageSelectedUid 是回调时列表的观测值，载荷未包含 UID，不能据此证明刷新作用于该装备。
    void OnRerolledMessage(UAsyncAction_ListenForGameplayMessage listener, FGameplayTag channel)
    {
        if (listener != rerollMessages || Tag(channel) != RerolledChannel) return;
        if (!rerollMessages.ReadActor(out var payload)) { messageUnreadable = true; return; }
        var player = UGameplayStatics.GetPlayerPawn(this, 0) as APlayerCharacter;
        if (!Valid(player) || payload.Actor != player || Valid(UGameplayStatics.GetPlayerPawn(this, 1))) return;
        if (messageCount == 2147483647) { messageUnreadable = true; return; }
        messageCount++;
        messageClock = World.RealTime(this);
        messageSelectedUid = 0;
        var subsystem = UUIStoreSubsystemLibrary.Get();
        if (!Valid(subsystem)) return;
        var store = subsystem.GetStore();
        if (!Valid(store)) return;
        UTypedUiState.Read(out var result, store.GetStateCopy(), out var state);
        if (result == EStructUtilsResult.Valid && Selected(player, state, out var uid)) messageSelectedUid = (long)uid.UID;
    }

    protected override void ReceiveEndPlay(EEndPlayReason reason)
    {
        if (Valid(rerollMessages)) rerollMessages.Cancel();
        rerollMessages = null;
    }

    // 失败位按 SDK 枚举显式映射；未知枚举保留为 16，不能解释为空集合成功。
    int Failures(HashSet<EGA_RerollItemEffect_FailReason> reasons)
    {
        int bits = 0;
        foreach (var reason in reasons)
        {
            if (reason == EGA_RerollItemEffect_FailReason.Invalid) bits |= 1;
            else if (reason == EGA_RerollItemEffect_FailReason.VendorLevel) bits |= 2;
            else if (reason == EGA_RerollItemEffect_FailReason.Cost) bits |= 4;
            else if (reason == EGA_RerollItemEffect_FailReason.NoEffects) bits |= 8;
            else bits |= 16;
        }
        return bits;
    }

    // 预算种类严格对应原生 CurrencyBundle；缺失键按原生零费用处理，未知种类阻止使用。
    int Cost(FCurrencyBundle cost, ECurrency kind) => cost.Currencies.ContainsKey(kind) ? cost.Currencies[kind] : 0;

    // Intensity 使用 float；序列化时保留精度，避免改变等级比较结果。
    // 用 double 运算输出九位有效数字的科学计数法，足以往返还原任意有限 float，不依赖语言小数符。
    string IntensityText(float value)
    {
        double magnitude = value < 0 ? -(double)value : (double)value;
        if (!(magnitude >= 0 && magnitude <= 3.4028234663852886e38)) return "unreadable";
        if (magnitude == 0) return "0";
        int exponent = 0;
        while (magnitude >= 10) { magnitude /= 10; exponent++; }
        while (magnitude < 1) { magnitude *= 10; exponent--; }
        int significand = (int)(magnitude * 100000000 + .5);
        return (value < 0 ? "-" : "") + significand + "e" + (exponent - 8);
    }

    // 显示游戏自己格式化的数值，避免把强度 -0.2 猜成无条件 -20%；文本绝不参与匹配。
    string DisplayValue(APlayerCharacter player, FItemEffect effect)
    {
        string text = UKismetTextLibrary.Conv_TextToString(UInventoryHelperLibrary.GetTotalEffectValue(player, effect));
        if (text.Length > 128) return "";
        text = text.Replace("%", "%25").Replace("|", "%7C").Replace("~", "%7E").Replace(";", "%3B").Replace(",", "%2C").Replace("\n", "%0A").Replace("\r", "%0D");
        return text;
    }

    // 当前列表对象是 UListItemIndex；同时核对实际列表序号、当前分类数组和历史 UID。
    // 列表排序变化不会改变绑定 UID；未选中、空格、列表正在刷新或映射不一致均拒绝。
    bool Selected(APlayerCharacter player, FAS_OverallUIState state, out FSWSessionUID uid)
    {
        uid = default;
        var id = state.PrimaryPlayerID;
        if (!state.Vendors.ContainsKey(id) || state.Vendors[id].ActiveVendor != EAS_Vendor.Blacksmith ||
            !state.PlayerInventories.ContainsKey(id) || !state.PlayerInventoriesSelectedItem.ContainsKey(id)) return false;
        // 每次都核对唯一渲染控件，不能因缓存仍有效而漏掉后来出现的第二个铁匠控件。
        blacksmith = null;
        UWidgetBlueprintLibrary.GetAllWidgetsOfClass(this, out var widgets, Unreal.ClassOf<UAS_Blacksmith>(), false);
        foreach (var widget in widgets)
        {
            var found = widget as UAS_Blacksmith;
            if (!Valid(found) || !found.IsRendered() || found.GetOwningPlayerPawn() != player) continue;
            if (blacksmith != null) { blacksmith = null; return false; }
            blacksmith = found;
        }
        if (!Valid(blacksmith) || !Valid(blacksmith.ItemGrid) || !Valid(blacksmith.ItemGrid.ItemGridListContainer)) return false;
        var list = blacksmith.ItemGrid.ItemGridListContainer.ListView;
        if (!Valid(list) || list.IsRefreshPending() || list.BP_GetNumItemsSelected() != 1) return false;
        var selected = list.BP_GetSelectedItem() as UListItemIndex;
        var items = state.PlayerInventories[id].GearSelection.SelectedCategory.Items;
        if (!Valid(selected) || selected.Index < 0 || selected.Index >= items.Count ||
            list.GetNumItems() != items.Count || list.GetIndexForItem(selected) != selected.Index || items[selected.Index].IsEmpty) return false;
        uid = items[selected.Index].UID;
        return uid.UID != 0 && uid.UID == state.PlayerInventoriesSelectedItem[id].LastValidSelectedItem.UID;
    }

    // 原生铁匠渲染状态提供效果等级；与模板等级都可读时必须一致，不能借装备战力猜测。
    int EffectLevel(FItemEffect effect, FAS_EffectRerollState ui)
    {
        int level = -1;
        if (!USWTypeSystemHelperUtils.GetTagLevel(effect.GeneratorData.GeneratorParentTemplate, out level)) level = -1;
        int uiLevel = -1;
        foreach (var entry in ui.RerollableEffects.Effects)
        {
            if (Tag(entry.Tag) != Tag(effect.TypeTag) || entry.Unique || entry.Level < 1) continue;
            if (uiLevel >= 0 && uiLevel != entry.Level) return -1;
            uiLevel = entry.Level;
        }
        if (uiLevel >= 0 && level >= 0 && uiLevel != level) return -1;
        return uiLevel >= 0 ? uiLevel : level;
    }

    // 读取单一本地玩家和铁匠状态；列表当前选择与 LastValid UID 必须同时一致。
    void Observe()
    {
        var player = UGameplayStatics.GetPlayerPawn(this, 0) as APlayerCharacter;
        if (!Valid(player)) { Write("NoPlayer"); return; }
        if (Valid(UGameplayStatics.GetPlayerPawn(this, 1))) { Write("MultiplePlayers"); return; }
        // 使用 SDK 明确为原生的子系统入口；AngelScript 的 StoreUtil.GetStore 不可从蓝图调用。
        var subsystem = UUIStoreSubsystemLibrary.Get();
        if (!Valid(subsystem)) { Write("Unreadable"); return; }
        var store = subsystem.GetStore();
        if (!Valid(store)) { Write("Unreadable"); return; }
        UTypedUiState.Read(out var result, store.GetStateCopy(), out var state);
        if (result != EStructUtilsResult.Valid) { Write("Unreadable"); return; }
        var id = state.PrimaryPlayerID;
        if (!state.Vendors.ContainsKey(id) || state.Vendors[id].ActiveVendor != EAS_Vendor.Blacksmith)
        { Write("BlacksmithClosed"); return; }
        if (!state.PlayerInventoriesSelectedItem.ContainsKey(id)) { Write("NoSelection"); return; }
        // 选择请求只走游戏 UI 回调；每次异步列表重建后重新按 UID 定位，完成前不能刷新。
        if (AdvanceSelection(player, store, state)) { Write("SelectionPending"); return; }
        if (!Selected(player, state, out var uid))
        {
            // 列表重建时，仅在玩家、铁匠、历史 UID 一致且 IsRefreshPending 时等待。
            // 这不是选中成功，也不复用旧效果；重建结束后仍须再次完整核对真实选择。
            if (waiting && state.PlayerInventoriesSelectedItem[id].LastValidSelectedItem.UID == pendingUid.UID &&
                Valid(blacksmith) && blacksmith.IsRendered() && blacksmith.GetOwningPlayerPawn() == player &&
                Valid(blacksmith.ItemGrid) && Valid(blacksmith.ItemGrid.ItemGridListContainer) &&
                Valid(blacksmith.ItemGrid.ItemGridListContainer.ListView) && blacksmith.ItemGrid.ItemGridListContainer.ListView.IsRefreshPending())
            { Write("SelectionPending"); return; }
            Write("SelectionUnverified"); return;
        }
        if (!UInventoryHelperLibrary.GetInventoryEntryFromID(player, uid, out var entry))
        { Write("NoSelection"); return; }
        // 返回条目的 SessionUID 必须仍是请求身份，不能用列表位置或历史选择代替。
        if (entry.ItemData.SessionUID.UID != uid.UID) { Write("SelectionChanged"); return; }
        if (!UInventoryHelperLibrary.IsItemValid(entry) || UInventoryHelperLibrary.IsItemSold(entry))
        { Write("Unreadable"); return; }
        var effects = UGA_RerollItemEffect.GetRerollableEffectsFromItem(player, uid);
        if (effects.Count > 16) { Write("Unreadable"); return; }
        if (!UInventoryHelperLibrary.GetItemPowerLevel(entry.ItemData, out int power))
        { Write("Unreadable"); return; }
        // 邮箱用有符号 64 位十进制保留相同位模式，避免 NeoRune 缺少 UInt64 字符串转换。
        int emerald = UInventoryHelperLibrary.GetPlayerCurrency(player, ECurrency.Emerald);
        int springStone = UInventoryHelperLibrary.GetPlayerCurrency(player, ECurrency.SpringStone);
        int enchantment = UInventoryHelperLibrary.GetPlayerCurrency(player, ECurrency.EnchantmentPoint);
        if (emerald < 0 || springStone < 0 || enchantment < 0) { Write("Unreadable"); return; }
        string data = "Selected~" + (long)uid.UID + "~" + Tag(entry.ItemData.TypeTag) + "~" + Tag(entry.ItemData.RarityTag) + "~" + power + "~" +
            UGA_RerollItemEffect.GetRequiredBlacksmithLevel(player, uid) + "~" +
            Failures(UGA_RerollItemEffect.CanItemReroll(player, uid)) + "~" + entry.ItemData.EffectRerolls + "~" +
            (UInventoryHelperLibrary.IsSoulStormGearItem(player, uid) ? "1" : "0") + "~" + emerald + "," + springStone + "," + enchantment + "~";
        bool first = true;
        foreach (var effect in effects)
        {
            // 查询参数是真实效果标签。重复标签在执行接口中不能当成两个可独立定位的槽位。
            var cost = UGA_RerollItemEffect.GetRerollItemEffectCost(player, uid, effect.TypeTag);
            foreach (var currency in cost.Currencies.Keys)
                if (currency != ECurrency.Emerald && currency != ECurrency.SpringStone && currency != ECurrency.EnchantmentPoint)
                { Write("UnknownCost"); return; }
            if (Cost(cost, ECurrency.Emerald) < 0 || Cost(cost, ECurrency.SpringStone) < 0 || Cost(cost, ECurrency.EnchantmentPoint) < 0)
            { Write("UnknownCost"); return; }
            // 等级直接向原生查询模板标签；失败记为 -1，不按力量等级或 Roman 后缀自行推断。
            int level = EffectLevel(effect, state.Vendors[id].Blacksmith.EffectReroll);
            if (!first) data += ";";
            first = false;
            data += Tag(effect.TypeTag) + "," + IntensityText(effect.Intensity) + "," + Tag(effect.GeneratorData.GeneratorParentTemplate) + "," + level + "," +
                Failures(UGA_RerollItemEffect.CanRerollItemEffect(player, uid, effect.TypeTag)) + "," +
                Cost(cost, ECurrency.Emerald) + "," + Cost(cost, ECurrency.SpringStone) + "," + Cost(cost, ECurrency.EnchantmentPoint) + "," + DisplayValue(player, effect);
        }
        // 再次核对真实列表选择，拒绝查询过程中切换菜单或身份，不能仅沿用历史选择。
        UTypedUiState.Read(out var afterResult, store.GetStateCopy(), out var after);
        if (afterResult != EStructUtilsResult.Valid || after.PrimaryPlayerID.ID != id.ID || !Selected(player, after, out var afterUid) || afterUid.UID != uid.UID)
        { Write("SelectionChanged"); return; }
        // 回调证据单列，不能用余额变化、原生字节计数或观察编号替代请求完成确认。
        data += "~" + (messageUnreadable ? "2" : Valid(rerollMessages) ? "1" : "0") + "," + messageCount + "," + messageClock + "," + messageSelectedUid;
        data += "~" + IntensityText(entry.ItemData.GeneratorData.PowerGeneratorValues.ItemPower);
        // Status 上限留在现有存档字段读取限幅内；协议只含标签和原始数值，不传私有资源。
        if (data.Length > 12000) { Write("Unreadable"); return; }
        AdvanceRequest(player, uid, entry, effects);
        Write(data);
    }
}

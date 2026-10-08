// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 协议 7 单次真实刷新。DoRerollItemEffect 只在所有原生前置查询通过后调用一次。
// 结果确认同时要求玩家消息、UID、原生计数、精确费用和未刷效果保持不变；同结果也可以确认。
// 超时/身份变化/费用异常后禁止再发，必须重新加载组件；不写效果、货币、材料或概率。
using NeoRune;
using System.Collections.Generic;
using UE.Engine;
using UE.Dungeons;
using UE.SWCoreGameplay;
using UE.SWCoreCommon;
using UE.InventorySystem;

namespace MCD2RerollBridge;
public partial class ModActor
{
    string epoch = "-", status = "Disabled", seenCommand = "OFF", queued = "";
    int sequence;
    bool waiting, poisoned, suspended;
    double nextObservation, deadline;
    FSWSessionUID pendingUid;
    string pendingType, pendingRarity;
    int pendingPower;
    float pendingRawPower;
    List<FItemEffect> retained = new();
    int beforeCounter, beforeMessages, beforeCount;
    int beforeEmerald, beforeStone, beforeEnchantment, costEmerald, costStone, costEnchantment;
    HashSet<string> epochs = new();

    // 0.1 秒读请求；空闲观察每 0.5 秒，等待结果时每 0.1 秒，无需重复触发刷新。
    void Poll()
    {
        ReadCommand();
        double now = World.RealTime(this);
        if (waiting || selecting || queued != "" || now >= nextObservation)
        {
            nextObservation = now + .5;
            Observe();
        }
    }

    // 只允许规范十进制非负整数，拒绝 StringToInt 将非法文本解释为 0 的情况。
    bool Number(string text, out int value)
    {
        value = UKismetStringLibrary.Conv_StringToInt(text);
        return value >= 0 && "" + value == text;
    }
    bool Epoch(string text)
    {
        if (text.Length != 32) return false;
        for (int i = 0; i < 32; i++)
            if (!"0123456789abcdef".Contains(UKismetStringLibrary.GetSubstring(text, i, 1))) return false;
        return true;
    }

    // OFF 只撤销未发出的请求；已经进入游戏原生调用的操作无法撤销，仍等待其完成证据。
    // 已见命令不重读执行；历史 epoch 不复用；同一 epoch 序号必须严格递增。
    void ReadCommand()
    {
        var request = UGameplayStatics.LoadGameFromSlot("MCD2RerollRequest", 0) as Request;
        if (request == null || request.Command.Length != 2048) return;
        var copies = UKismetStringLibrary.ParseIntoArray(UKismetStringLibrary.TrimTrailing(request.Command), "#", false);
        if (copies.Count != 2 || copies[0] != copies[1] || copies[0].Length == 0 || copies[0].Length > 1023) return;
        string command = copies[0];
        if (command == seenCommand) return;
        seenCommand = command;
        if (command == "OFF")
        {
            bool cancelled = queued != "";
            queued = "";
            selecting = false;
            suspended = true;
            // 已确认回执保留到下一请求，防止暂停恰好发生在确认后而覆盖最终结果。
            if (!waiting) status = poisoned ? "ResultUncertain" : cancelled ? "Cancelled" : status == "Confirmed" ? "Confirmed" : "Disabled";
            nextObservation = 0;
            return;
        }
        var p = UKismetStringLibrary.ParseIntoArray(command, "|", false);
        if ((p.Count != 6 && p.Count != 23 && p.Count != 12) || p[0] != "7" || p[1] != instance || !Epoch(p[2]) || !Number(p[3], out var number) || number < 1) return;
        if (waiting || selecting || queued != "") { poisoned = true; status = "ResultUncertain"; return; }
        if (p[2] == epoch)
        {
            if (number != sequence + 1) return;
        }
        else
        {
            if (number != 1 || epochs.Contains(p[2]) || epochs.Count >= 128) return;
            epochs.Add(p[2]);
        }
        epoch = p[2]; sequence = number; suspended = false;
        double expiry = UKismetStringLibrary.Conv_StringToDouble(p[p.Count == 6 ? 5 : p.Count == 12 ? 10 : 17]);
        double now = World.RealTime(this);
        if (!(expiry >= now && expiry <= now + 2)) { status = "Expired"; return; }
        if (poisoned) { status = "ResultUncertain"; return; }
        if (p.Count == 6 && p[4] == "probe") { status = "TransportReady"; nextObservation = 0; return; }
        if (p.Count == 12 && p[4] == "select") { queued = command; status = "Queued"; return; }
        if (p.Count != 23 || p[4] != "reroll") { status = "InvalidRequest"; return; }
        queued = command;
        status = "Queued";
    }

    // 观察本身失败时不能沿用旧装备；已派发结果不确定时永久锁住此组件实例的执行。
    void ObservationFailed(string data)
    {
        if (data.StartsWith("Selected~")) return;
        if (selecting)
        {
            if (data == "SelectionPending") return;
            selecting = false; status = "SelectionFailed";
        }
        if (data == "SelectionPending" && waiting)
        {
            if (World.RealTime(this) >= deadline) { waiting = false; poisoned = true; status = "ResultTimedOut"; }
            return;
        }
        if (waiting) { waiting = false; poisoned = true; status = "ResultUncertain"; }
        else if (queued != "") status = data;
        queued = "";
    }
    bool Same(FItemEffect a, FItemEffect b) => Tag(a.TypeTag) == Tag(b.TypeTag) &&
        Tag(a.GeneratorData.GeneratorParentTemplate) == Tag(b.GeneratorData.GeneratorParentTemplate) &&
        a.Intensity == b.Intensity && a.EnchantmentPointsInvested == b.EnchantmentPointsInvested;

    // 最终发送前再次读取自有邮箱，F9/暂停在查询期间到达时不继续使用旧局部命令。
    bool StillRequested(string command)
    {
        var request = UGameplayStatics.LoadGameFromSlot("MCD2RerollRequest", 0) as Request;
        return request != null && request.Command == UKismetStringLibrary.RightPad(command + "#" + command, 2048);
    }

    // 所有读数来自同一游戏线程；传入前后已核对真实列表选择。
    void AdvanceRequest(APlayerCharacter player, FSWSessionUID uid, FInventoryEntry entry, List<FItemEffect> effects)
    {
        if (waiting)
        {
            if (poisoned || uid.UID != pendingUid.UID || Tag(entry.ItemData.TypeTag) != pendingType || Tag(entry.ItemData.RarityTag) != pendingRarity ||
                !UInventoryHelperLibrary.GetItemPowerLevel(entry.ItemData, out var currentPower) || currentPower != pendingPower ||
                entry.ItemData.GeneratorData.PowerGeneratorValues.ItemPower != pendingRawPower || effects.Count != beforeCount ||
                messageUnreadable || !Valid(rerollMessages) || messageCount > beforeMessages + 1)
            { waiting = false; poisoned = true; status = "ResultUncertain"; return; }
            int emerald = UInventoryHelperLibrary.GetPlayerCurrency(player, ECurrency.Emerald);
            int stone = UInventoryHelperLibrary.GetPlayerCurrency(player, ECurrency.SpringStone);
            int enchantment = UInventoryHelperLibrary.GetPlayerCurrency(player, ECurrency.EnchantmentPoint);
            bool unchanged = true;
            foreach (var keep in retained)
            {
                int found = 0;
                foreach (var effect in effects) if (Same(keep, effect)) found++;
                if (found != 1) unchanged = false;
            }
            // 字节计数达到 255 时不派发，避免猜测饱和/回绕语义。相同随机结果无需字符串变化。
            if (messageCount == beforeMessages + 1 && entry.ItemData.EffectRerolls == beforeCounter + 1 && unchanged &&
                emerald == beforeEmerald - costEmerald && stone == beforeStone - costStone && enchantment == beforeEnchantment - costEnchantment)
            { waiting = false; status = "Confirmed"; return; }
            if (World.RealTime(this) >= deadline)
            { waiting = false; poisoned = true; status = "ResultTimedOut"; }
            return;
        }
        if (queued == "" || poisoned || suspended) return;
        string command = queued; queued = "";
        var p = UKismetStringLibrary.ParseIntoArray(command, "|", false);
        double expiry = UKismetStringLibrary.Conv_StringToDouble(p[17]);
        if (!(expiry >= World.RealTime(this) && expiry <= World.RealTime(this) + 2)) { status = "Expired"; return; }
        if ("" + (long)uid.UID != p[5] || Tag(entry.ItemData.TypeTag) != p[6] || Tag(entry.ItemData.RarityTag) != p[19] ||
            !UInventoryHelperLibrary.GetItemPowerLevel(entry.ItemData, out var power) || "" + power != p[18] ||
            "" + effects.Count != p[20] || "" + entry.ItemData.EffectRerolls != p[8] || "" + messageCount != p[21] ||
            entry.ItemData.GeneratorData.PowerGeneratorValues.ItemPower != (float)UKismetStringLibrary.Conv_StringToDouble(p[22]))
        { status = "SelectionChanged"; return; }
        if (entry.ItemData.EffectRerolls >= 255 || effects.Count < 1 || effects.Count > 16 || messageUnreadable || !Valid(rerollMessages))
        { status = "ConfirmationUnavailable"; return; }
        var costs = new List<int>(); var budgets = new List<int>();
        for (int i = 0; i < 3; i++)
        {
            if (!Number(p[11 + i], out var amount) || !Number(p[14 + i], out var cap) || amount > cap)
            { status = "BudgetLimit"; return; }
            costs.Add(amount); budgets.Add(cap);
        }
        int selected = -1;
        var types = new HashSet<string>();
        for (int i = 0; i < effects.Count; i++)
        {
            string type = Tag(effects[i].TypeTag);
            // 原生调用以标签寻址，重复标签不能安全定位，绝不把槽号转换成标签。
            if (types.Contains(type)) { status = "AmbiguousEffects"; return; }
            types.Add(type);
            if (type == p[7]) selected = i;
        }
        if (selected < 0 || Tag(effects[selected].GeneratorData.GeneratorParentTemplate) != p[9] ||
            effects[selected].Intensity != (float)UKismetStringLibrary.Conv_StringToDouble(p[10]))
        { status = "SelectionChanged"; return; }
        if (Failures(UGA_RerollItemEffect.CanItemReroll(player, uid)) != 0 ||
            Failures(UGA_RerollItemEffect.CanRerollItemEffect(player, uid, effects[selected].TypeTag)) != 0)
        { status = "NativeConditionsFailed"; return; }
        var cost = UGA_RerollItemEffect.GetRerollItemEffectCost(player, uid, effects[selected].TypeTag);
        foreach (var currency in cost.Currencies.Keys)
            if (currency != ECurrency.Emerald && currency != ECurrency.SpringStone && currency != ECurrency.EnchantmentPoint)
            { status = "UnknownCost"; return; }
        if (Cost(cost, ECurrency.Emerald) != costs[0] || Cost(cost, ECurrency.SpringStone) != costs[1] || Cost(cost, ECurrency.EnchantmentPoint) != costs[2])
        { status = "CostChanged"; return; }
        beforeEmerald = UInventoryHelperLibrary.GetPlayerCurrency(player, ECurrency.Emerald);
        beforeStone = UInventoryHelperLibrary.GetPlayerCurrency(player, ECurrency.SpringStone);
        beforeEnchantment = UInventoryHelperLibrary.GetPlayerCurrency(player, ECurrency.EnchantmentPoint);
        if (beforeEmerald < costs[0] || beforeStone < costs[1] || beforeEnchantment < costs[2]) { status = "InsufficientFunds"; return; }
        retained.Clear();
        for (int i = 0; i < effects.Count; i++) if (i != selected) retained.Add(effects[i]);
        if (!StillRequested(command)) { status = "Cancelled"; return; }
        pendingUid = uid; pendingType = Tag(entry.ItemData.TypeTag); pendingRarity = Tag(entry.ItemData.RarityTag); pendingPower = power; beforeCount = effects.Count;
        pendingRawPower = entry.ItemData.GeneratorData.PowerGeneratorValues.ItemPower;
        beforeCounter = entry.ItemData.EffectRerolls; beforeMessages = messageCount;
        costEmerald = costs[0]; costStone = costs[1]; costEnchantment = costs[2];
        deadline = World.RealTime(this) + 5;
        // 先设在途标志再调用，原生同步广播也只进入被动回调，不会递归发送下一次。
        waiting = true; status = "Dispatched";
        UGA_RerollItemEffect.DoRerollItemEffect(player, uid, effects[selected].TypeTag);
    }
}

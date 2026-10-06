// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 游戏端附近交互 Actor：轮询独立请求槽，调用已授予的原生能力并观察真实结果。
using System.Collections.Generic;
using NeoRune;
using UE.Engine;
using UE.CoreUObject;
using UE.Dungeons;
using UE.SpicewoodGAS;
using UE.SWCoreGAS;
using UE.SWCoreGameplay;
using UE.InventorySystem;
using UE.GameplayTags;
using UE.LootActors;
using UE.GameplayAbilities;
using UE.GameplayMessageRuntime;
using MCD2NativeBindings;

namespace MCD2NearbyLootBridge;
// 组件独立请求存档类；类名与 Command 字段属于工具端识别 ABI，不能单边改名。
public class Request : USaveGame
{
    public string Command;
}

// 组件独立回执存档类；字段和序列化格式必须与工具端解析保持一致。
public class Receipt : USaveGame
{
    public string Status;
}

// 只处理工具明确指定的单个新鲜请求；不设置鼠标/高亮、不移动玩家、不发物品、不生成宝箱。
// NeoRune 编译为游戏蓝图 Actor 的组件入口；实际执行的是游戏原生能力及函数。
public class ModActor : AActor
{
    // 当前请求归属：实例、场景和序号与工具端一起匹配，换场景后旧请求不可复用。
    string instance = "", epoch = "", actorName = "", kind = "", type = "", pawnName = "";
    string status = "Disabled";
    int sequence;
    // waiting 仅表示动作结果仍待观察，不代表交互已成功。
    bool waiting;
    // 结果截止时间使用游戏实时时钟（秒）；超时写拒绝/未确认结果而不是成功。
    double resultDeadline;
    bool allowMoving;
    // 食物与其他交互分别节流，单位毫秒；读取命令时还会限幅，默认与工具设置一致。
    int intervalMs = 500, foodIntervalMs = 1000;
    double lastDispatchAt = -1000000, lastOtherAt = -1000000, lastFoodAt = -1000000, entityEndedAt = -1;
    AActor pending;
    USWAbilityTask_TryActivateAbilityAndWait activationTask;
    UAbilityAsync_WaitGameplayEffectApplied foodEffectObserver;
    UTypedGameplayMessageListener foodMessageObserver;
    UAbilityAsync_WaitGameplayEvent currencyObserver;
    UObject pendingFoodSource;
    APlayerState pendingFoodPlayerState;
    FItemData pendingFoodData;
    ulong pendingFoodUID;
    ulong diagnosticFoodMessageUID;
    bool foodMessageMatched, foodHealthRiseObserved;
    float foodHealthBefore, foodHealthMaxBefore, foodHealthAfter;
    int diagnosticEffectCallbacks, diagnosticFoodMessages, diagnosticCurrencyEvents;
    string diagnosticEffectSource = "", diagnosticCurrencyTags = "", diagnosticCurrencyActors = "";
    string diagnosticFoodMessage = "";
    float diagnosticCurrencyMagnitude;
    // 动作前背包 UID 和已尝试 Actor 的基线；同一实体不会在会话内重复请求。
    HashSet<ulong> before = new();
    HashSet<string> attempts = new();
    HashSet<string> foodEffectsBefore = new(), emeraldsBefore = new();
    bool foodEffectObserved, emeraldDropObserved;
    float heldBefore;
    FVector pendingPosition;
    string diagnosticEvent = "", diagnosticRoute = "";
    int diagnosticHandle, activationModeBefore = -1, activationModeAfter = -1;
    // 读取 GameplayTag 的原生调试名称，供精确标签比较。
    string Tag(FGameplayTag tag) => UBlueprintGameplayTagLibrary.GetDebugStringFromGameplayTag(tag);
    // 将已知标签名构造为原生 GameplayTag。
    FGameplayTag GameplayTag(string value) => new FGameplayTag
    {
        TagName = value
    };
    // 校验对象或数据是否满足当前模块的使用前提。
    bool Valid(UObject value) => value != null && UKismetSystemLibrary.IsValid(value);
    // 创建收集专用请求/回执槽并启动轮询，默认 OFF，不自动触发收集。
    protected override void ReceiveBeginPlay()
    {
        instance = UKismetSystemLibrary.GetObjectName(this) + "@" + World.RealTime(this);
        var request = UGameplayStatics.CreateSaveGameObject(Unreal.ClassOf<Request>()) as Request;
        if (request == null)
            return;
        request.Command = UKismetStringLibrary.RightPad("OFF#OFF", 2048);
        UGameplayStatics.SaveGameToSlot(request, "MCD2NearbyLootRequest", 0);
        WriteReceipt();
        Timer.Start(this, nameof(Poll), 0.1f, loop: true);
        // 等待罐子回执时及时观察掉落，防止拾取后丢失结果。
        Timer.Start(this, nameof(ObservePendingDrops), 0.025f, loop: true);
    }

    // 将协议、实例、场景 epoch、序号、状态和游戏时间写入独立回执槽。
    void WriteReceipt()
    {
        var receipt = UGameplayStatics.CreateSaveGameObject(Unreal.ClassOf<Receipt>()) as Receipt;
        if (receipt == null)
            return;
        receipt.Status = "6|" + instance + "|" + epoch + "|" + sequence + "|" + status + "|" + World.RealTime(this);
        UGameplayStatics.SaveGameToSlot(receipt, "MCD2NearbyLootReceipt", 0);
    }

    // 检查玩家存活、菜单及交互可用性，允许已适配的战斗中交互。
    bool PlayerReady(APlayerCharacter player, AGameplayPlayerController controller)
    {
        if (!Valid(player) || !Valid(controller) || controller.GetControlledPlayerCharacter() != player || !controller.HasValidLocalPlayer() || !controller.IsPlayerInputEnabled() || controller.IsInputCapturedByUI() || controller.IsInputCapturedByTeleport() || controller.IsPlayerImmovable() || (!allowMoving && controller.GetIsMoving()))
            return false;
        var asc = USWAbilitySystemComponent.GetASC(player);
        var movement = player.CharacterMovement;
        if (!Valid(asc) || !Valid(movement) || asc.GetGameplayTagCount(GameplayTag("SW.State.Life.Alive")) <= 0 || asc.GetGameplayTagCount(GameplayTag("SW.State.Stunned")) > 0 || asc.GetGameplayTagCount(GameplayTag("SW.State.Immobile")) > 0 || (movement.MovementMode != EMovementMode.MOVE_Walking && movement.MovementMode != EMovementMode.MOVE_NavWalking) || !movement.CurrentFloor.bBlockingHit || !movement.CurrentFloor.bWalkableFloor || movement.CurrentFloor.FloorDist > 10 || movement.CurrentFloor.FloorDist < -5)
            return false;
        var velocity = player.GetVelocity();
        return allowMoving || velocity.X * velocity.X + velocity.Y * velocity.Y + velocity.Z * velocity.Z <= 900;
    }

    // 使用原生交互距离和保守边界判断目标是否可作用。
    bool WithinRange(APlayerCharacter player, AActor target)
    {
        var asc = USWAbilitySystemComponent.GetASC(player);
        if (!Valid(asc))
            return false;
        double range = asc.GetGameplayAttributeCurrentValue(Unreal.ClassOf<UATR_Movement>(), "InteractionRange");
        if (!(range >= 20 && range <= 600))
            return false;
        var a = player.K2_GetActorLocation();
        var b = target.K2_GetActorLocation();
        double x = a.X - b.X, y = a.Y - b.Y, z = a.Z - b.Z;
        // 在原生交互范围内部保留边界，避免触发游戏的接近目标路径。
        return x * x + y * y <= (range - 10) * (range - 10) && z >= -150 && z <= 150;
    }

    // 按已核实类型识别可拾取附魔书，未知书类不放行。
    bool KnownBook(string type)
    {
        return type == "SW.Item.EnchantmentBook.Arcane" || type == "SW.Item.EnchantmentBook.Blowback" || type == "SW.Item.EnchantmentBook.Borealis" || type == "SW.Item.EnchantmentBook.BurstBowstring" || type == "SW.Item.EnchantmentBook.ChainReaction" || type == "SW.Item.EnchantmentBook.Channeling" || type == "SW.Item.EnchantmentBook.CriticalQuiver" || type == "SW.Item.EnchantmentBook.Dynamo" || type == "SW.Item.EnchantmentBook.ExpandedQuiver" || type == "SW.Item.EnchantmentBook.FireAspect" || type == "SW.Item.EnchantmentBook.FrostCrescent" || type == "SW.Item.EnchantmentBook.GravityPulse" || type == "SW.Item.EnchantmentBook.GuardingStrike" || type == "SW.Item.EnchantmentBook.HealthSynergy" || type == "SW.Item.EnchantmentBook.LingeringPower" || type == "SW.Item.EnchantmentBook.MultiPotion" || type == "SW.Item.EnchantmentBook.MultiRoll" || type == "SW.Item.EnchantmentBook.Piercing" || type == "SW.Item.EnchantmentBook.PoisonFog" || type == "SW.Item.EnchantmentBook.PotionBarrier" || type == "SW.Item.EnchantmentBook.PotionSharing" || type == "SW.Item.EnchantmentBook.Radiance" || type == "SW.Item.EnchantmentBook.Ricochet" || type == "SW.Item.EnchantmentBook.ShadowStrike" || type == "SW.Item.EnchantmentBook.Shockwave" || type == "SW.Item.EnchantmentBook.SoulAspect" || type == "SW.Item.EnchantmentBook.SoulInfusedPotion" || type == "SW.Item.EnchantmentBook.SpringLoaded" || type == "SW.Item.EnchantmentBook.Swirling" || type == "SW.Item.EnchantmentBook.TempoTheft" || type == "SW.Item.EnchantmentBook.Thundering" || type == "SW.Item.EnchantmentBook.Unstoppable";
    }

    // 按原生物品槽标签识别装备类别。
    bool EquipmentSlot(string value)
    {
        return value == "SW.ItemSlot.Inventory.MeleeWeapon" || value == "SW.ItemSlot.Inventory.RangedWeapon" || value == "SW.ItemSlot.Inventory.Armor.Helmet" || value == "SW.ItemSlot.Inventory.Armor.Chest" || value == "SW.ItemSlot.Inventory.Armor.Leggings" || value == "SW.ItemSlot.Inventory.Armor.Boots" || value == "SW.ItemSlot.Inventory.Artifact" || value == "SW.ItemSlot.Equipment.MeleeWeapon" || value == "SW.ItemSlot.Equipment.RangedWeapon" || value == "SW.ItemSlot.Equipment.Armor.Helmet" || value == "SW.ItemSlot.Equipment.Armor.Chest" || value == "SW.ItemSlot.Equipment.Armor.Leggings" || value == "SW.ItemSlot.Equipment.Armor.Boots" || value == "SW.ItemSlot.Equipment.Artifact.Slot1" || value == "SW.ItemSlot.Equipment.Artifact.Slot2" || value == "SW.ItemSlot.Equipment.Artifact.Slot3";
    }

    // 核对目标类型、归属、生命/开启状态及原生交互前提。
    bool TargetReady(APlayerCharacter player, AActor actor)
    {
        if (!Valid(actor) || actor.IsActorBeingDestroyed() || actor.bHidden || !WithinRange(player, actor))
            return false;
        if (kind == "pot")
        {
            var pot = actor as ASWASCActor;
            var playerASC = USWAbilitySystemComponent.GetASC(player);
            if (!Valid(pot) || Tag(pot.TypeInfo.TypeTag) != type || !KnownPot(type) || !Valid(pot.AbilitySystemComponent) || playerASC.GetGameplayAttributeCurrentValue(Unreal.ClassOf<UATR_Throwable>(), "ThrowableHeldCount") != 0)
                return false;
            var tags = pot.AbilitySystemComponent;
            var a = player.K2_GetActorLocation();
            var b = actor.K2_GetActorLocation();
            double x = a.X - b.X, y = a.Y - b.Y;
            return x * x + y * y <= 150 * 150 && tags.GetGameplayTagCount(GameplayTag("SW.TargetingProperties.BreakablePot")) > 0 && tags.GetGameplayTagCount(GameplayTag("SW.State.Life.Alive")) > 0 && tags.GetGameplayTagCount(GameplayTag("SW.Property.Attackable")) > 0 && tags.GetGameplayTagCount(GameplayTag("SW.Property.Targetable")) > 0 && tags.GetGameplayTagCount(GameplayTag("SW.State.Untargetable")) == 0;
        }

        if (kind == "food" || kind == "tnt")
        {
            var item = actor as ASWItemActor;
            if (!Valid(item) || !Valid(item.GASItemComponent) || !item.GASItemComponent.bAssetsLoadComplete || Tag(item.GASItemComponent.ItemData.TypeTag) != type)
                return false;
            var owner = item.GASItemComponent.GetOwningPlayerPawn();
            if (owner != null && owner != player)
                return false;
            if (kind == "food")
                return KnownFood(type);
            var throwableASC = USWAbilitySystemComponent.GetASC(player);
            float held = throwableASC.GetGameplayAttributeCurrentValue(Unreal.ClassOf<UATR_Throwable>(), "ThrowableHeldCount");
            float maximum = throwableASC.GetGameplayAttributeCurrentValue(Unreal.ClassOf<UATR_Throwable>(), "ThrowableHeldCountMax");
            return type == "SW.Item.Consumable.Throwable.TNT" && held >= 0 && maximum > held && maximum <= 100;
        }

        if (kind == "book")
        {
            var book = actor as ASWItemActor;
            if (!Valid(book) || !Valid(book.GASItemComponent) || !book.GASItemComponent.bAssetsLoadComplete || !KnownBook(type) || Tag(book.GASItemComponent.ItemData.TypeTag) != type)
                return false;
            var owner = book.GASItemComponent.GetOwningPlayerPawn();
            return (owner == null || owner == player) && Tag(UInventoryHelperLibrary.GetItemSlotTag(player, book.GASItemComponent.ItemData.TypeTag)) == "SW.ItemSlot.Inventory.EnchantmentBook";
        }

        if (kind == "item")
        {
            var item = actor as ASWItemActor;
            if (!Valid(item) || !Valid(item.GASItemComponent) || !item.GASItemComponent.bAssetsLoadComplete || item.GASItemComponent.GetOwningPlayerPawn() != player || Tag(item.GASItemComponent.ItemData.TypeTag) != type)
                return false;
            return EquipmentSlot(Tag(UInventoryHelperLibrary.GetItemSlotTag(player, item.GASItemComponent.ItemData.TypeTag)));
        }

        var chest = actor as ASWASCChestActor;
        if (kind != "chest" || !Valid(chest) || Tag(chest.TypeInfo.TypeTag) != type || !KnownChest(type))
            return false;
        var asc = chest.AbilitySystemComponent;
        return Valid(asc) && asc.GetGameplayTagCount(GameplayTag("SW.State.Access.Open")) == 0 && asc.GetGameplayTagCount(GameplayTag("SW.State.Untargetable")) == 0;
    }

    // 读取玩家当前背包 UID 集合，作为装备/附魔书入包结果的前后基线。
    List<FInventoryEntry> Inventory(APawn player)
    {
        var manager = UInventoryManagerComponent.GetComponent(player);
        var result = new List<FInventoryEntry>();
        if (!Valid(manager))
            return result;
        foreach (var slot in manager.GetInventory())
            foreach (var item in slot.ItemInventoryEntries)
                result.Add(item);
        return result;
    }

    // 从玩家已授予的能力定义唯一查找 Interact，不要求实例已提前创建。
    bool FindInteractAbility(APlayerCharacter player, out FGameplayAbilitySpecHandle handle)
    {
        handle = new FGameplayAbilitySpecHandle();
        var asc = USWAbilitySystemComponent.GetASC(player);
        if (!Valid(asc))
            return false;
        int count = 0;
        foreach (var spec in asc.SWAbilitySpecs.Items)
        {
            if (kind == "pot")
            {
                if (Valid(spec.Definition) && spec.Definition.AbilityClass == Unreal.ClassOf<UGA_PlayerMeleeAttack>())
                {
                    handle = spec.Handle;
                    count++;
                }

                continue;
            }

            if (!Valid(spec.Definition) || Tag(spec.Definition.Definition.AbilityCallsign) != "SW.AbilityCallsign.Interact")
                continue;
            // GA_Interact 在激活时才创建实例；预激活实例缺失不能证明玩家未被授予该能力。
            if (spec.Definition.AbilityClass != Unreal.ClassOf<UGA_Interact>())
                continue;
            handle = spec.Handle;
            count++;
        }

        return count == 1;
    }

    // 查找目标原生交互事件的有效上下文，拒绝不确定入口。
    bool FindNativeInteractionEvent(APlayerCharacter player, AActor target, out FGameplayTag eventTag)
    {
        eventTag = new FGameplayTag();
        if (kind == "pot")
        {
            var asc = USWAbilitySystemComponent.GetASC(player);
            int matches = 0;
            // 原生类在能力 CDO 上登记触发器，该能力的 Definition.TriggeredByGameplayEvents 为空。
            FGameplayAbilitySpecHandle melee;
            if (!FindInteractAbility(player, out melee))
                return false;
            foreach (var spec in asc.ActivatableAbilities.Items)
                if (spec.Handle.Handle == melee.Handle && Valid(spec.Ability as UGA_PlayerMeleeAttack))
                    foreach (var trigger in spec.Ability.AbilityTriggers)
                        if (trigger.TriggerSource == EGameplayAbilityTriggerSource.GameplayEvent && Tag(trigger.TriggerTag) == "SW.GameplayEvent.PlayerMelee")
                        {
                            eventTag = trigger.TriggerTag;
                            matches++;
                        }

            return matches == 1;
        }

        var targetASC = USWAbilitySystemComponent.GetASC(target);
        int count = 0;
        if (Valid(targetASC))
            foreach (var spec in targetASC.SWAbilitySpecs.Items)
            {
                if (!Valid(spec.Definition))
                    continue;
                foreach (var value in spec.Definition.Definition.CommonKeyValues)
                    if (Tag(value.TypeTag) == "SW.KeyValue.InteractionEvent" && Tag(value.GameplayTag) != "None")
                    {
                        eventTag = value.GameplayTag;
                        count++;
                    }

                foreach (var value in spec.Definition.Definition.SpecificKeyValues)
                    if (Tag(value.TypeTag) == "SW.KeyValue.InteractionEvent" && Tag(value.GameplayTag) != "None")
                    {
                        eventTag = value.GameplayTag;
                        count++;
                    }
            }

        if (count == 1)
            return true;
        if (count > 1)
            return false;
        var types = USWTypeSystem.GetSWTypeSystemFromActor(target);
        FInstancedStruct source;
        if (!Valid(types) || !types.GetInstancedStruct(GameplayTag(type), GameplayTag("SW.InstancedStruct.InteractionRedirection"), out source))
            return false;
        EStructUtilsResult result;
        FInteractionRedirection redirection;
        UTypedInstancedStructLibrary.GetInteractionRedirection(out result, source, out redirection);
        if (result != EStructUtilsResult.Valid || Tag(redirection.CallsignToActivate) == "None")
            return false;
        eventTag = redirection.CallsignToActivate;
        // 入口始终是已经授予的原生能力；不授予替代能力，不直接应用食用/拾取结果。
        foreach (var spec in USWAbilitySystemComponent.GetASC(player).SWAbilitySpecs.Items)
            if (Valid(spec.Definition) && Tag(spec.Definition.Definition.AbilityCallsign) == Tag(eventTag))
                count++;
        return count == 1;
    }

    // 查找与真实目标关联的原生任务归属。
    UGameplayAbility FindTaskOwner(APlayerCharacter player)
    {
        var asc = USWAbilitySystemComponent.GetASC(player);
        FGameplayAbilitySpecHandle pickup = new FGameplayAbilitySpecHandle();
        int definitions = 0;
        foreach (var spec in asc.SWAbilitySpecs.Items)
            if (Valid(spec.Definition) && spec.Definition.AbilityClass == Unreal.ClassOf<UGA_ItemPickup>() && Tag(spec.Definition.Definition.AbilityCallsign) == "SW.AbilityCallsign.ItemPickup")
            {
                pickup = spec.Handle;
                definitions++;
            }

        if (definitions != 1)
            return null;
        UGameplayAbility selected = null;
        int count = 0;
        foreach (var spec in asc.ActivatableAbilities.Items)
        {
            if (spec.Handle.Handle != pickup.Handle || spec.PendingRemove)
                continue;
            foreach (var ability in spec.NonReplicatedInstances)
                if (Valid(ability) && ability is UGA_ItemPickup && ability.GetAvatarActorFromActorInfo() == player && ability.GetAbilitySystemComponentFromActorInfo() == asc)
                {
                    selected = ability;
                    count++;
                }

            foreach (var ability in spec.ReplicatedInstances)
                if (Valid(ability) && ability is UGA_ItemPickup && ability.GetAvatarActorFromActorInfo() == player && ability.GetAbilitySystemComponentFromActorInfo() == asc)
                {
                    selected = ability;
                    count++;
                }
        }

        return count == 1 ? selected : null;
    }

    // 停止本请求拥有的能力等待任务，清理后续激活回调。
    void EndActivationTask()
    {
        if (Valid(activationTask))
            activationTask.EndTask();
        activationTask = null;
        EndResultObservers();
    }

    // 解除食物和货币消息/效果监听，避免旧请求接收新目标结果。
    void EndResultObservers()
    {
        if (Valid(foodEffectObserver))
            foodEffectObserver.EndAction();
        if (Valid(foodMessageObserver))
            foodMessageObserver.Cancel();
        if (Valid(currencyObserver))
            currencyObserver.EndAction();
        foodEffectObserver = null;
        foodMessageObserver = null;
        currencyObserver = null;
    }

    // 在动作前安装食物/货币结果监听，以捕获瞬时原生效果。
    bool StartResultObservers(APlayerCharacter player)
    {
        EndResultObservers();
        if (kind == "food")
        {
            var payload = Unreal.ObjectAt<UScriptStruct>("/Script/SpicewoodGAS.ConsumableUsedPlayerMessage");
            if (!Valid(payload))
                return false;
            foodMessageObserver = UAsyncAction_ListenForGameplayMessage.ListenForGameplayMessages(this, GameplayTag("SW.GameplayMessage.Ability.UseFoodConsumable"), payload, EGameplayMessageMatch.ExactMatch) as UTypedGameplayMessageListener;
            if (!Valid(foodMessageObserver))
                return false;
            foodMessageObserver.OnMessageReceived += OnFoodMessage;
            foodMessageObserver.Activate();
            foodEffectObserver = UAbilityAsync_WaitGameplayEffectApplied.WaitGameplayEffectAppliedToActor(player, new FGameplayTargetDataFilterHandle(), new FGameplayTagRequirements(), new FGameplayTagRequirements(), false, false);
            if (Valid(foodEffectObserver))
            {
                foodEffectObserver.OnApplied += OnFoodEffectApplied;
                foodEffectObserver.Activate();
            }
        }
        else if (kind == "pot")
        {
            // 监听原生 GA_CurrencyPickup CDO 触发器，与普通物品拾取不同；这是被动监听，不发送货币事件。
            currencyObserver = UAbilityAsync_WaitGameplayEvent.WaitGameplayEventToActor(player, GameplayTag("SW.GameplayEvent.CurrencyPickup"), false, true);
            if (!Valid(currencyObserver))
                return false;
            currencyObserver.EventReceived += OnCurrencyEvent;
            currencyObserver.Activate();
        }

        return true;
    }

    // 读取 Actor 的完整原生对象名，供请求归属和结果观察使用。
    string ObjectName(UObject value) => Valid(value) ? UKismetSystemLibrary.GetObjectName(value) : "Unavailable";
    // 记录与本次食物对应的原生食用消息，避免把其他食物消息算入结果。
    void OnFoodMessage(UAsyncAction_ListenForGameplayMessage listener, FGameplayTag channel)
    {
        if (!waiting || kind != "food" || listener != foodMessageObserver || World.RealTime(this) > resultDeadline || Tag(channel) != "SW.GameplayMessage.Ability.UseFoodConsumable")
            return;
        diagnosticFoodMessages++;
        FConsumableUsedPlayerMessage payload;
        if (!foodMessageObserver.GetFoodPayload(out payload))
        {
            diagnosticFoodMessage = "PayloadUnavailable";
            return;
        }

        var messagePlayer = UGameplayStatics.GetPlayerStateFromUniqueNetId(this, payload.UniqueID);
        diagnosticFoodMessageUID = payload.ItemData.SessionUID.UID;
        diagnosticFoodMessage = Tag(payload.ItemData.TypeTag) + "|" + payload.ItemData.OwningPlayerID + "|" + Tag(payload.ItemData.DropSource) + "|" + ObjectName(messagePlayer);
        // 当前原生消耗品 SessionUID 为 0，零不能当成唯一身份；还需核对本地玩家网络身份、物品字段和指定 Actor 销毁。
        if (Valid(pendingFoodPlayerState) && messagePlayer == pendingFoodPlayerState && payload.ItemData.SessionUID.UID == pendingFoodUID && Tag(payload.ItemData.TypeTag) == type && payload.ItemData.OwningPlayerID == pendingFoodData.OwningPlayerID && Tag(payload.ItemData.DropSource) == Tag(pendingFoodData.DropSource) && Tag(payload.ItemData.RarityTag) == Tag(pendingFoodData.RarityTag) && payload.ItemData.PickupTimestamp == pendingFoodData.PickupTimestamp)
            foodMessageMatched = true;
    }

    // 观察玩家应用的原生食物效果，补充实体销毁以外的成功证据。
    void OnFoodEffectApplied(AActor sourceActor, FGameplayEffectSpecHandle spec, FActiveGameplayEffectHandle active)
    {
        if (!waiting || kind != "food" || World.RealTime(this) > resultDeadline)
            return;
        diagnosticEffectCallbacks++;
        var context = UAbilitySystemBlueprintLibrary.GetEffectContext(spec);
        if (!UAbilitySystemBlueprintLibrary.EffectContextIsValid(context))
            return;
        var source = UAbilitySystemBlueprintLibrary.EffectContextGetSourceObject(context);
        var causer = UAbilitySystemBlueprintLibrary.EffectContextGetEffectCauser(context);
        diagnosticEffectSource = ObjectName(sourceActor) + "|" + ObjectName(source) + "|" + ObjectName(causer);
        if (source == pending || causer == pending || (pendingFoodSource != null && source == pendingFoodSource))
            foodEffectObserved = true;
    }

    // 观察本次罐子破坏附近的原生货币事件。
    void OnCurrencyEvent(FGameplayEventData data)
    {
        if (!waiting || kind != "pot" || World.RealTime(this) > resultDeadline || Tag(data.EventTag) != "SW.GameplayEvent.CurrencyPickup")
            return;
        diagnosticCurrencyEvents++;
        diagnosticCurrencyTags = UBlueprintGameplayTagLibrary.GetDebugStringFromGameplayTagContainer(data.TargetTags);
        diagnosticCurrencyMagnitude = data.EventMagnitude;
        diagnosticCurrencyActors = ObjectName(data.Instigator) + "|" + ObjectName(data.Target) + "|" + ObjectName(data.OptionalObject) + "|" + ObjectName(data.OptionalObject2);
        // 只有真实新生成的原生绿宝石 Actor 算自动掉落证据；货币事件或余额变化单独只算诊断数据。
        ObserveEmeraldActor(data.Target as ASWActor);
        ObserveEmeraldActor(data.OptionalObject as ASWActor);
        ObserveEmeraldActor(data.OptionalObject2 as ASWActor);
    }

    // 构造可比较的效果身份，避免重复计数同一条食物效果。
    string EffectKey(FActiveGameplayEffect effect)
    {
        return effect.ReplicationID + "@" + effect.StartServerWorldTime + "@" + effect.Spec.StackCount;
    }

    // 读取并比较原生食物效果，不自行给玩家应用或生成效果。
    void ObserveFoodEffect(APlayerCharacter player)
    {
        var asc = USWAbilitySystemComponent.GetASC(player);
        foreach (var effect in asc.ActiveGameplayEffects.GameplayEffects_Internal)
        {
            if (effect.bIsInhibited || foodEffectsBefore.Contains(EffectKey(effect)))
                continue;
            var context = effect.Spec.EffectContext;
            if (!UAbilitySystemBlueprintLibrary.EffectContextIsValid(context))
                continue;
            var source = UAbilitySystemBlueprintLibrary.EffectContextGetSourceObject(context);
            var causer = UAbilitySystemBlueprintLibrary.EffectContextGetEffectCauser(context);
            if (source == pending || causer == pending || (pendingFoodSource != null && source == pendingFoodSource))
                foodEffectObserved = true;
        }
    }

    // 按已核实货币类型识别掉落绿宝石 Actor。
    bool IsEmeraldActor(ASWActor actor)
    {
        if (!Valid(actor))
            return false;
        // 货币外观为 GearActor 蓝图，真实类型保存在 GASItemComponent.ItemData，与食物/TNT 一样。
        var item = actor as ASWItemActor;
        if (Valid(item) && Valid(item.GASItemComponent) && KnownEmerald(Tag(item.GASItemComponent.ItemData.TypeTag)))
            return true;
        return KnownEmerald(Tag(actor.TypeInfo.TypeTag));
    }

    // 继续观察等待中的掉落证据，遵守结果时限。
    void ObservePendingDrops()
    {
        if (waiting && kind == "pot" && !emeraldDropObserved && World.RealTime(this) <= resultDeadline)
            ObserveEmeraldDrops();
    }

    // 比较罐子周边绿宝石掉落和货币变化，不能只凭罐子消失认定完整成功。
    void ObserveEmeraldDrops()
    {
        foreach (var actor in World.FindAll(this, Unreal.ClassOf<ASWActor>()))
            ObserveEmeraldActor(actor as ASWActor);
    }

    // 追踪已识别绿宝石 Actor 的状态；实际拾取仍由游戏自身处理。
    void ObserveEmeraldActor(ASWActor emerald)
    {
        // 原生拾取可先隐藏掉落再销毁；基线也包含隐藏 Actor，避免把旧绿宝石算成新掉落。
        if (!IsEmeraldActor(emerald))
            return;
        var p = emerald.K2_GetActorLocation();
        double x = p.X - pendingPosition.X, y = p.Y - pendingPosition.Y, z = p.Z - pendingPosition.Z;
        if (x * x + y * y + z * z <= 600 * 600 && !emeraldsBefore.Contains(UKismetSystemLibrary.GetObjectName(emerald)))
            emeraldDropObserved = true;
    }

    // 分别核对装备 UID、宝箱 Open 标签、食物效果、TNT 及罐子破坏/掉落结果。
    void CheckResult(APlayerCharacter player)
    {
        ObserveActivationState(player);
        // 请求不追随目标；玩家交互未完成就跑出范围时停止激活任务，不能启动接近路径。
        if (allowMoving && Valid(pending) && !pending.IsActorBeingDestroyed() && !WithinRange(player, pending))
        {
            status = "TargetOutOfRange";
            waiting = false;
            EndActivationTask();
            return;
        }

        if (kind == "food")
        {
            ObserveFoodEffect(player);
            var asc = USWAbilitySystemComponent.GetASC(player);
            foodHealthAfter = asc.GetGameplayAttributeCurrentValue(Unreal.ClassOf<UATR_Health>(), "Health");
            float maximum = asc.GetGameplayAttributeCurrentValue(Unreal.ClassOf<UATR_Health>(), "HealthMax");
            if (foodHealthBefore >= 0 && foodHealthMaxBefore > foodHealthBefore && maximum == foodHealthMaxBefore && foodHealthAfter > foodHealthBefore && foodHealthAfter <= maximum)
                foodHealthRiseObserved = true;
            // 满血时回血食物没有持续效果；需要本玩家/物品的原生食用消息加回血，或能归属该物品的效果上下文。
            if ((!Valid(pending) || pending.IsActorBeingDestroyed()) && (foodEffectObserved || (foodMessageMatched && foodHealthRiseObserved)))
            {
                status = "Consumed";
                waiting = false;
            }
            else if (!Valid(pending) || pending.IsActorBeingDestroyed())
            {
                if (entityEndedAt < 0)
                    entityEndedAt = World.RealTime(this);
                // 满血时，实体消耗不等于效果生效；结束本次观察，避免阻塞后续食物请求。
                if (World.RealTime(this) - entityEndedAt >= 0.2)
                {
                    status = "ConsumedUnconfirmed";
                    waiting = false;
                }
            }
        }
        else if (kind == "tnt")
        {
            float held = USWAbilitySystemComponent.GetASC(player).GetGameplayAttributeCurrentValue(Unreal.ClassOf<UATR_Throwable>(), "ThrowableHeldCount");
            if ((!Valid(pending) || pending.IsActorBeingDestroyed()) && held > heldBefore)
            {
                status = "Carried";
                waiting = false;
            }
        }
        else if (kind == "pot")
        {
            ObserveEmeraldDrops();
            var pot = pending as ASWASCActor;
            bool dead = !Valid(pot) || pot.IsActorBeingDestroyed() || (Valid(pot.AbilitySystemComponent) && pot.AbilitySystemComponent.GetGameplayTagCount(GameplayTag("SW.State.Life.Dead")) > 0);
            if (dead && emeraldDropObserved)
            {
                status = "BrokenWithDrops";
                waiting = false;
            }
            else if (dead)
            {
                if (entityEndedAt < 0)
                    entityEndedAt = World.RealTime(this);
                if (World.RealTime(this) - entityEndedAt >= 0.2)
                {
                    status = "BrokenUnconfirmed";
                    waiting = false;
                }
            }
        }
        else if (kind == "chest")
        {
            var chest = pending as ASWASCChestActor;
            if (Valid(chest) && Valid(chest.AbilitySystemComponent) && chest.AbilitySystemComponent.GetGameplayTagCount(GameplayTag("SW.State.Access.Open")) > 0)
            {
                status = "Opened";
                waiting = false;
            }
        }
        else if (!Valid(pending) || pending.IsActorBeingDestroyed())
        {
            foreach (var item in Inventory(player))
                if (!before.Contains(item.ItemData.SessionUID.UID) && Tag(item.ItemData.TypeTag) == type)
                {
                    status = kind == "book" ? "BookPickedUp" : "PickedUp";
                    waiting = false;
                }
        }

        if (waiting && World.RealTime(this) >= resultDeadline)
        {
            status = "Unconfirmed";
            waiting = false;
        }

        if (!waiting)
            EndActivationTask();
    }

    // 观察原生交互能力的激活状态，用于推进请求而非直接判为收集成功。
    void ObserveActivationState(APlayerCharacter player)
    {
        var asc = USWAbilitySystemComponent.GetASC(player);
        if (!Valid(asc))
            return;
        foreach (var spec in asc.ActivatableAbilities.Items)
            if (spec.Handle.Handle == diagnosticHandle)
                activationModeAfter = (int)spec.ActivationInfo.ActivationMode;
    }

    // 先核对 OFF/probe 与双份信封，再验证真实玩家/目标/场景/期限，调用原生入口。
    void Poll()
    {
        var request = UGameplayStatics.LoadGameFromSlot("MCD2NearbyLootRequest", 0) as Request;
        if (request == null)
        {
            EndActivationTask();
            waiting = false;
            status = "Disabled";
            WriteReceipt();
            return;
        }

        var copies = UKismetStringLibrary.ParseIntoArray(UKismetStringLibrary.TrimTrailing(request.Command), "#", false);
        if (request.Command.Length != 2048 || copies.Count != 2 || copies[0] != copies[1])
        {
            EndActivationTask();
            waiting = false;
            status = "InvalidRequest";
            WriteReceipt();
            return;
        }

        string command = copies[0];
        if (command == "OFF")
        {
            EndActivationTask();
            waiting = false;
            status = "Disabled";
            WriteReceipt();
            return;
        }

        var parts = UKismetStringLibrary.ParseIntoArray(command, "|", false);
        if (parts.Count != 16 || parts[0] != "6" || parts[1] != instance)
        {
            EndActivationTask();
            waiting = false;
            status = "InvalidRequest";
            WriteReceipt();
            return;
        }

        int nextInterval = UKismetStringLibrary.Conv_StringToInt(parts[14]);
        int nextFoodInterval = UKismetStringLibrary.Conv_StringToInt(parts[15]);
        if ((parts[13] != "0" && parts[13] != "1") || nextInterval < 100 || nextInterval > 30000 || nextFoodInterval < 100 || nextFoodInterval > 30000)
        {
            EndActivationTask();
            waiting = false;
            status = "InvalidRequest";
            WriteReceipt();
            return;
        }

        string nextEpoch = parts[2];
        int nextSequence = UKismetStringLibrary.Conv_StringToInt(parts[3]);
        if (parts[4] == "probe")
        {
            double probeExpiry = UKismetStringLibrary.Conv_StringToDouble(parts[11]);
            if (waiting || nextEpoch.Length != 32 || nextSequence <= 0 || parts[12] != "1" || !(probeExpiry >= World.RealTime(this) && probeExpiry <= World.RealTime(this) + 2))
            {
                status = "InvalidProbe";
                WriteReceipt();
                return;
            }

            epoch = nextEpoch;
            sequence = nextSequence;
            status = "TransportReady";
            WriteReceipt();
            return;
        }

        var player = World.Player(this) as APlayerCharacter;
        var controller = World.PlayerController(this) as AGameplayPlayerController;
        if (waiting)
        {
            if (!Valid(player) || UKismetSystemLibrary.GetObjectName(player) != pawnName)
            {
                EndActivationTask();
                status = "PlayerChanged";
                waiting = false;
            }
            else
                CheckResult(player);
            WriteReceipt();
            return;
        }

        if (nextEpoch == epoch && nextSequence == sequence)
        {
            WriteReceipt();
            return;
        }

        epoch = nextEpoch;
        sequence = nextSequence;
        kind = parts[4];
        actorName = parts[5];
        type = parts[6];
        pawnName = parts[7];
        allowMoving = parts[13] == "1";
        intervalMs = nextInterval;
        foodIntervalMs = nextFoodInterval;
        if (!PlayerReady(player, controller))
        {
            waiting = false;
            status = "PlayerBlocked";
            WriteReceipt();
            return;
        }

        double expires = UKismetStringLibrary.Conv_StringToDouble(parts[11]);
        if (epoch.Length != 32 || sequence <= 0 || (kind != "item" && kind != "book" && kind != "chest" && kind != "food" && kind != "tnt" && kind != "pot") || UKismetSystemLibrary.GetObjectName(player) != pawnName || !(expires >= World.RealTime(this) && expires <= World.RealTime(this) + 1))
        {
            status = "ExpiredOrChanged";
            WriteReceipt();
            return;
        }

        var candidates = kind == "pot" ? World.FindAll(this, Unreal.ClassOf<ASWASCActor>()) : kind == "chest" ? World.FindAll(this, Unreal.ClassOf<ASWASCChestActor>()) : World.FindAll(this, Unreal.ClassOf<ASWItemActor>());
        AActor selected = null;
        int matches = 0;
        foreach (var actor in candidates)
            if (Valid(actor) && UKismetSystemLibrary.GetObjectName(actor) == actorName)
            {
                selected = actor;
                matches++;
            }

        if (matches != 1 || !TargetReady(player, selected))
        {
            status = "TargetBlocked";
            WriteReceipt();
            return;
        }

        var p = selected.K2_GetActorLocation();
        double x = p.X - UKismetStringLibrary.Conv_StringToDouble(parts[8]);
        double y = p.Y - UKismetStringLibrary.Conv_StringToDouble(parts[9]);
        double z = p.Z - UKismetStringLibrary.Conv_StringToDouble(parts[10]);
        if (!(x * x + y * y + z * z <= 9))
        {
            status = "TargetChanged";
            WriteReceipt();
            return;
        }

        FGameplayAbilitySpecHandle interact;
        if (!FindInteractAbility(player, out interact))
        {
            status = "InteractionUnavailable";
            WriteReceipt();
            return;
        }

        FGameplayTag nativeEvent;
        if (!FindNativeInteractionEvent(player, selected, out nativeEvent))
        {
            status = "InteractionEventUnavailable";
            WriteReceipt();
            return;
        }

        var taskOwner = FindTaskOwner(player);
        if (!Valid(taskOwner))
        {
            status = "ActivationContextUnavailable";
            WriteReceipt();
            return;
        }

        if (parts[12] == "1")
        {
            status = "PreviewReady";
            WriteReceipt();
            return;
        }

        if (parts[12] != "0")
        {
            status = "InvalidRequest";
            WriteReceipt();
            return;
        }

        double now = World.RealTime(this);
        if (now - lastDispatchAt < 0.1 || (kind == "food" ? now - lastFoodAt < foodIntervalMs / 1000.0 : now - lastOtherAt < intervalMs / 1000.0))
        {
            status = "Throttled";
            WriteReceipt();
            return;
        }

        string key = pawnName + "|" + actorName;
        if (attempts.Contains(key))
        {
            status = "AlreadyAttempted";
            WriteReceipt();
            return;
        }

        before.Clear();
        foreach (var item in Inventory(player))
            before.Add(item.ItemData.SessionUID.UID);
        pending = selected;
        pendingPosition = selected.K2_GetActorLocation();
        attempts.Add(key);
        waiting = true;
        resultDeadline = now + 5;
        entityEndedAt = -1;
        lastDispatchAt = now;
        if (kind == "food")
            lastFoodAt = now;
        else
            lastOtherAt = now;
        foodEffectsBefore.Clear();
        foodEffectObserved = false;
        emeraldsBefore.Clear();
        emeraldDropObserved = false;
        pendingFoodSource = null;
        pendingFoodPlayerState = null;
        pendingFoodUID = 0;
        diagnosticFoodMessageUID = 0;
        foodMessageMatched = false;
        foodHealthRiseObserved = false;
        diagnosticEffectCallbacks = 0;
        diagnosticFoodMessages = 0;
        diagnosticCurrencyEvents = 0;
        diagnosticEffectSource = "";
        diagnosticFoodMessage = "";
        diagnosticCurrencyTags = "";
        diagnosticCurrencyActors = "";
        diagnosticCurrencyMagnitude = 0;
        var playerASC = USWAbilitySystemComponent.GetASC(player);
        heldBefore = playerASC.GetGameplayAttributeCurrentValue(Unreal.ClassOf<UATR_Throwable>(), "ThrowableHeldCount");
        if (kind == "food")
        {
            var food = selected as ASWItemActor;
            pendingFoodSource = food.GASItemComponent;
            pendingFoodData = food.GASItemComponent.ItemData;
            pendingFoodUID = pendingFoodData.SessionUID.UID;
            pendingFoodPlayerState = player.PlayerState;
            foodHealthBefore = playerASC.GetGameplayAttributeCurrentValue(Unreal.ClassOf<UATR_Health>(), "Health");
            foodHealthMaxBefore = playerASC.GetGameplayAttributeCurrentValue(Unreal.ClassOf<UATR_Health>(), "HealthMax");
            foodHealthAfter = foodHealthBefore;
            foreach (var effect in playerASC.ActiveGameplayEffects.GameplayEffects_Internal)
                foodEffectsBefore.Add(EffectKey(effect));
        }

        if (kind == "pot")
            foreach (var actor in World.FindAll(this, Unreal.ClassOf<ASWActor>()))
            {
                var emerald = actor as ASWActor;
                if (IsEmeraldActor(emerald))
                    emeraldsBefore.Add(UKismetSystemLibrary.GetObjectName(emerald));
            }

        if (!StartResultObservers(player))
        {
            EndActivationTask();
            waiting = false;
            status = "EvidenceObserverUnavailable";
            WriteReceipt();
            return;
        }

        // 保留目标解析出的原生交互事件；GA_Interact 负责吃/捡/开箱，原生激活任务负责预测和联网，不伪造 RPC 键。
        var asc = USWAbilitySystemComponent.GetASC(player);
        var data = new FGameplayEventData
        {
            EventTag = nativeEvent,
            Instigator = player,
            Target = selected,
            TargetData = UAbilitySystemBlueprintLibrary.AbilityTargetDataFromActor(selected)
        };
        diagnosticEvent = Tag(nativeEvent);
        diagnosticHandle = interact.Handle;
        diagnosticRoute = kind == "pot" ? "NativeMeleeEvent" : "NativeActivationTask";
        activationModeAfter = -1;
        ObserveActivationState(player);
        activationModeBefore = activationModeAfter;
        if (kind == "pot")
        {
            // 原生玩家输入转向目标后使用此事件；控制器旋转只改朝向，不改鼠标、目标选择器、移动输入或路径。
            var rotation = UKismetMathLibrary.FindLookAtRotation(player.K2_GetActorLocation(), selected.K2_GetActorLocation());
            rotation.Pitch = 0;
            rotation.Roll = 0;
            controller.SetControlRotation(rotation);
            UAbilitySystemBlueprintLibrary.SendGameplayEventToActor(player, nativeEvent, data);
            ObserveActivationState(player);
            status = "Dispatched";
            WriteReceipt();
            return;
        }

        activationTask = USWAbilityTask_TryActivateAbilityAndWait.TryActivate(taskOwner, interact, data, asc);
        if (!Valid(activationTask))
        {
            EndActivationTask();
            waiting = false;
            status = "ActivationUnavailable";
            WriteReceipt();
            return;
        }

        activationTask.ReadyForActivation();
        ObserveActivationState(player);
        status = "Dispatched";
        WriteReceipt();
    }

    // 只识别已核实的已有宝箱 Actor 类型。
    bool KnownChest(string value)
    {
        return value == "SW.LootActor.WoodenChest" || value == "SW.LootActor.CampWoodenChest" || value == "SW.LootActor.FancyChest" || value == "SW.LootActor.FancyChest.Procedural" || value == "SW.LootActor.DeepDarkChest" || value == "SW.LootActor.FancyChestHidden" || value == "SW.LootActor.KeyChest" || value == "SW.LootActor.SoulChest" || value == "SW.LootActor.WellspringChest" || value == "SW.LootActor.WellspringChest.Procedural" || value == "SW.LootActor.StorminatorChest" || value == "SW.LootActor.StorminatorChest.Easy" || value == "SW.LootActor.StorminatorChest.Medium" || value == "SW.LootActor.StorminatorChest.Hard" || value == "SW.LootActor.StorminatorChest.Final" || value == "SW.LootActor.StorminatorChest.Final.High" || value == "SW.LootActor.StorminatorChest.Final.Low" || value == "SW.LootActor.StorminatorChest.Final.Medium" || value == "SW.LootActor.TalismansChest" || value == "SW.LootActor.TalismansChest.DoubleDrop" || value == "SW.LootActor.TalismansChest.EmeraldIncrease" || value == "SW.LootActor.TalismansChest.FiringEmeralds" || value == "SW.LootActor.TalismansChest.Plummeting" || value == "SW.LootActor.TalismansChest.PotionCooldown" || value == "SW.LootActor.TalismansChest.RangedBuff" || value == "SW.LootActor.TalismansChest.SoulCapacity" || value == "SW.LootActor.TalismansChest.SpeedIncrement" || value == "SW.LootActor.TalismansChest.LlamaCompanion";
    }

    // 按完整已核实食物类型白名单识别目标，不生成食物或奖励。
    bool KnownFood(string value)
    {
        return value == "SW.Item.Consumable.Food.Apple" || value == "SW.Item.Consumable.Food.Carrot" || value == "SW.Item.Consumable.Food.Melon" || value == "SW.Item.Consumable.Food.Potato" || value == "SW.Item.Consumable.Food.PumpkinPie" || value == "SW.Item.Consumable.Food.SoulPotato";
    }

    // 区分已核实的小型/大型绿宝石罐，交给原生破坏逻辑。
    bool KnownPot(string value) => value == "SW.LootActor.Pot.Small.Emerald" || value == "SW.LootActor.Pot.Large.Emerald";
    // 核对绿宝石货币类型，仅用于结果观察。
    bool KnownEmerald(string value) => value == "SW.Item.Currency.Emerald" || value == "SW.Item.Currency.EmeraldBlock";
}

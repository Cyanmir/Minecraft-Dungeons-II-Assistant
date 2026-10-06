// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 游戏端原生战斗 Actor：原生近战、法器激活/蓄力/引导和前滚。
using NeoRune;
using MCD2CombatNativeBindings;
using System.Collections.Generic;
using UE.Engine;
using UE.CoreUObject;
using UE.Dungeons;
using UE.SpicewoodGAS;
using UE.SWCoreGAS;
using UE.SWCoreGameplay;
using UE.InventorySystem;
using UE.GameplayTags;
using UE.GameplayAbilities;

namespace MCD2CombatBridge;
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

// 战斗组件与收集/出售独立；每个新鲜请求只执行一次明确指定动作，不注入输入、不靠鼠标选目标、不寻路或发奖励。
// NeoRune 编译为游戏蓝图 Actor 的组件入口；实际执行的是游戏原生能力及函数。
public class ModActor : AActor
{
    // 当前请求归属：实例、场景和序号与工具端一起匹配，换场景后旧请求不可复用。
    string instance = "", epoch = "", status = "Disabled", kind = "";
    int sequence, handle, slot;
    bool encounter, waiting, abilityObserved, aimed, released, targetDelivered, heldObserved;
    double holdSeconds, startedAt, releaseAt;
    int heldHandle;
    string artifactType = "", requestIdentity = "";
    FSWSessionUID artifactUID;
    AMobCharacter pendingTarget;
    UGA_Artifact activeArtifact;
    UGameplayAbility activeHeld;
    // 真实激活的预测键，仅用于对应原生能力实例；不能用全零键替代。
    FPredictionKey artifactKey, heldKey;
    // 秒单位的结果时限/动作节流；不能与工具端毫秒间隔直接相减。
    double deadline, lastMelee = -10000, lastArtifact = -10000, lastRoll = -10000;
    float rollBefore;
    FVector playerBefore, rollDirection;
    // 原生等待任务拥有本次激活上下文，结束/失败时通过 EndTask 清理。
    USWAbilityTask_TryActivateAbilityAndWait task;
    APlayerCharacter pendingPlayer;
    // 读取 GameplayTag 的原生调试名称，供精确标签比较。
    string Tag(FGameplayTag tag) => UBlueprintGameplayTagLibrary.GetDebugStringFromGameplayTag(tag);
    // 将已知标签名构造为原生 GameplayTag。
    FGameplayTag G(string name) => new FGameplayTag
    {
        TagName = name
    };
    // 校验对象或数据是否满足当前模块的使用前提。
    bool Valid(UObject o) => o != null && UKismetSystemLibrary.IsValid(o);
    protected override void ReceiveBeginPlay()
    {
        instance = UKismetSystemLibrary.GetObjectName(this) + "@" + World.RealTime(this);
        var r = UGameplayStatics.CreateSaveGameObject(Unreal.ClassOf<Request>()) as Request;
        if (r == null)
            return;
        r.Command = UKismetStringLibrary.RightPad("OFF#OFF", 2048);
        UGameplayStatics.SaveGameToSlot(r, "MCD2CombatRequest", 0);
        Write();
        Timer.Start(this, nameof(Poll), .05f, loop: true);
        Timer.Start(this, nameof(Observe), .025f, loop: true);
    }

    // 写入本组件的自有通信数据；不能写入角色存档。
    void Write()
    {
        var r = UGameplayStatics.CreateSaveGameObject(Unreal.ClassOf<Receipt>()) as Receipt;
        if (r == null)
            return;
        r.Status = "4|" + instance + "|" + epoch + "|" + sequence + "|" + status + "|" + World.RealTime(this);
        UGameplayStatics.SaveGameToSlot(r, "MCD2CombatReceipt", 0);
    }

    // 仅取消本次槽位和装备 UID 所属的能力实例；不能伪造含未反射原生上下文的 FSWGameplayAbilityInfo。
    // 比较完整预测键，确认待取消能力确实属于本次原生激活。
    bool SameActivation(UGameplayAbility b, FPredictionKey key)
    {
        var actual = new List<FPredictionKey>();
        actual.Add(b.CurrentActivationInfo.PredictionKeyWhenActivated);
        var expected = new List<FPredictionKey>();
        expected.Add(key);
        return UPredictionArrayLibrary.SameKeys(actual, expected);
    }

    // 只取消本工具拥有的法器激活，保留玩家自行启动或已换装备的能力。
    void CancelOwned()
    {
        if (waiting && kind == "artifact" && (aimed || holdSeconds > 0))
        {
            if (Valid(activeArtifact) && activeArtifact.GetAvatarActorFromActorInfo() == pendingPlayer && SameActivation(activeArtifact, artifactKey))
                activeArtifact.K2_CancelAbility();
            if (Valid(activeHeld) && activeHeld.GetAvatarActorFromActorInfo() == pendingPlayer && SameActivation(activeHeld, heldKey))
                activeHeld.K2_CancelAbility();
        }
    }

    // 释放当前能力等待任务，避免后续回调继续使用旧目标。
    void EndTask()
    {
        if (Valid(task))
            task.EndTask();
        task = null;
        waiting = false;
        pendingPlayer = null;
        pendingTarget = null;
        activeArtifact = null;
        activeHeld = null;
    }

    // 中止当前请求并清理能力/任务，将失败原因写入回执。
    void Abort(string reason)
    {
        CancelOwned();
        EndTask();
        status = reason;
    }

    // 取得装备的稳定会话标识，避免蓄力过程中换槽后继续作用于另一件装备。
    string Identity(List<string> f)
    {
        string v = "";
        for (int i = 0; i < 19; i++)
            v = v + f[i] + "|";
        return v;
    }

    // 为已验证目标构造游戏原生目标数据，不使用鼠标选中结果。
    FGameplayAbilityTargetDataHandle TargetData(APlayerCharacter p, AMobCharacter target)
    {
        var here = p.K2_GetActorLocation();
        var there = target.K2_GetActorLocation();
        var origin = new FGameplayAbilityTargetingLocationInfo
        {
            LocationType = EGameplayAbilityTargetingLocationType.LiteralTransform,
            LiteralTransform = new FTransform
            {
                Translation = here,
                Rotation = new FQuat
                {
                    W = 1
                },
                Scale3D = new FVector
                {
                    X = 1,
                    Y = 1,
                    Z = 1
                }
            }
        };
        var end = new FGameplayAbilityTargetingLocationInfo
        {
            LocationType = EGameplayAbilityTargetingLocationType.ActorTransform,
            SourceActor = target
        };
        return UAbilitySystemBlueprintLibrary.AppendTargetDataHandle(UAbilitySystemBlueprintLibrary.AbilityTargetDataFromActor(target), UAbilitySystemBlueprintLibrary.AbilityTargetDataFromLocations(origin, end));
    }

    // 重新核对槽位、装备 UID 和能力上下文，拒绝过期法器请求。
    bool ArtifactStillValid(APlayerCharacter p, AGameplayPlayerController c)
    {
        // 继续本工具发起的施法时，忽略该施法自身的输入与激活阻止标签。
        if (!Valid(p) || !Valid(c) || c.GetControlledPlayerCharacter() != p || !c.HasValidLocalPlayer() || c.IsInputCapturedByUI() || c.IsInputCapturedByTeleport() || p.IsActorBeingDestroyed())
            return false;
        var playerASC = USWAbilitySystemComponent.GetASC(p);
        if (!Valid(playerASC) || playerASC.GetGameplayTagCount(G("SW.State.Life.Alive")) <= 0 || playerASC.GetGameplayTagCount(G("SW.State.Stunned")) > 0)
            return false;
        if (!Valid(pendingTarget) || pendingTarget.IsActorBeingDestroyed() || pendingTarget.bHidden || !pendingTarget.IsHostileTowards(p) || !pendingTarget.IsTargetable())
            return false;
        var a = USWAbilitySystemComponent.GetASC(pendingTarget);
        if (!Valid(a) || a.GetGameplayTagCount(G("SW.State.Life.Alive")) <= 0 || !Near(p.K2_GetActorLocation(), pendingTarget.K2_GetActorLocation(), 800, 250) || !c.LineOfSightTo(pendingTarget, p.K2_GetActorLocation(), true))
            return false;
        var equipped = UInventoryHelperLibrary.GetEquippedItemsInSlot(p, G("SW.ItemSlot.Equipment.Artifact.Slot" + slot));
        return equipped.Count == 1 && equipped[0].ItemData.SessionUID.UID == artifactUID.UID && Tag(equipped[0].ItemData.TypeTag) == artifactType;
    }

    // 查找本次蓄力/引导能力的有效实例与预测键。
    bool HeldContext(APlayerCharacter p, bool charging)
    {
        int matches = 0;
        heldHandle = 0;
        var a = USWAbilitySystemComponent.GetASC(p);
        foreach (var s in a.SWAbilitySpecs.Items)
            if (Valid(s.Definition) && s.OwnerUID.UID == artifactUID.UID && Tag(s.GrantingType) == artifactType && (charging ? s.Definition.AbilityClass == Unreal.ClassOf<UGA_ChargeSouls>() : s.Definition.AbilityClass == Unreal.ClassOf<UGA_ChannelSouls>()))
            {
                heldHandle = s.Handle.Handle;
                matches++;
            }

        if (matches != 1)
            return false;
        matches = 0;
        foreach (var s in a.ActivatableAbilities.Items)
            if (s.Handle.Handle == heldHandle && !s.PendingRemove && s.ActiveCount == 0)
                matches++;
        return matches == 1;
    }

    // 推进法器瞄准、按住与释放阶段，并持续核对请求和激活归属。
    void DriveArtifact(AGameplayPlayerController c, double now)
    {
        var a = USWAbilitySystemComponent.GetASC(pendingPlayer);
        if (Valid(activeArtifact) && !SameActivation(activeArtifact, artifactKey) || Valid(activeHeld) && !SameActivation(activeHeld, heldKey))
        {
            Abort("ActivationChanged");
            return;
        }

        if (Valid(activeArtifact) && aimed && !targetDelivered)
        {
            // 原生 GA_Artifact 以 spec handle 和真实预测键绑定 ASC 目标数据委托；原生回调保留完整能力上下文与背包载荷。
            var data = TargetData(pendingPlayer, pendingTarget);
            var key = artifactKey;
            activeArtifact.ArtifactTargetDataHandle = data;
            targetDelivered = true;
            a.ServerSetReplicatedTargetData(new FGameplayAbilitySpecHandle { Handle = handle }, key, data, new FGameplayTag(), key);
        }

        if (holdSeconds > 0 && !released)
        {
            if (heldObserved && releaseAt == 0)
                releaseAt = now + holdSeconds;
            if (releaseAt == 0 && now - startedAt > 1.2)
            {
                Abort("HeldActivationUnconfirmed");
                return;
            }

            if (releaseAt > 0 && now >= releaseAt && Valid(activeArtifact))
            {
                // 使用与 WaitInputRelease 相同的原生 InputReleased 事件，只携带真实实例/键，不用零值或伪造预测上下文。
                var key = artifactKey;
                released = true;
                deadline = now + 1.5;
                a.ServerSetInputReleased(new FGameplayAbilitySpecHandle { Handle = handle });
                a.ServerSetReplicatedEvent(EAbilityGenericReplicatedEvent.InputReleased, new FGameplayAbilitySpecHandle { Handle = handle }, key, key);
                status = "ReleaseRequested";
            }
            else
                status = heldObserved ? "Holding" : "Dispatched";
        }

        if ((holdSeconds > 0 || aimed) && !released)
        {
            var here = pendingPlayer.K2_GetActorLocation();
            var there = pendingTarget.K2_GetActorLocation();
            var rot = UKismetMathLibrary.FindLookAtRotation(here, there);
            rot.Pitch = 0;
            rot.Roll = 0;
            c.SetControlRotation(rot);
        }
    }

    // 检查玩家是否存活、处于可操作状态且没有阻断动作的原生状态。
    bool Ready(APlayerCharacter p, AGameplayPlayerController c)
    {
        if (!Valid(p) || !Valid(c) || c.GetControlledPlayerCharacter() != p || !c.HasValidLocalPlayer() || !c.IsPlayerInputEnabled() || c.IsInputCapturedByUI() || c.IsInputCapturedByTeleport() || c.IsPlayerImmovable())
            return false;
        var a = USWAbilitySystemComponent.GetASC(p);
        var m = p.CharacterMovement;
        return Valid(a) && Valid(m) && !a.GetUserAbilityActivationInhibited() && a.GetGameplayTagCount(G("SW.State.Life.Alive")) > 0 && a.GetGameplayTagCount(G("SW.State.Stunned")) == 0 && a.GetGameplayTagCount(G("SW.State.Immobile")) == 0 && (m.MovementMode == EMovementMode.MOVE_Walking || m.MovementMode == EMovementMode.MOVE_NavWalking) && m.CurrentFloor.bBlockingHit && m.CurrentFloor.bWalkableFloor && m.CurrentFloor.FloorDist >= -5 && m.CurrentFloor.FloorDist <= 10;
    }

    // 检查玩家与敌人的原生距离，不能用扩大工具范围代替游戏攻击范围。
    bool Near(FVector a, FVector b, double range, double height)
    {
        double x = a.X - b.X, y = a.Y - b.Y, z = a.Z - b.Z;
        return x * x + y * y <= range * range && z >= -height && z <= height;
    }

    // 从玩家已授予的能力定义中找出唯一匹配项。
    bool Ability(APlayerCharacter p, string callsign, out FGameplayAbilitySpecHandle h)
    {
        h = new FGameplayAbilitySpecHandle();
        int count = 0;
        var a = USWAbilitySystemComponent.GetASC(p);
        foreach (var s in a.SWAbilitySpecs.Items)
        {
            if (!Valid(s.Definition))
                continue;
            bool match = kind == "melee" ? s.Definition.AbilityClass == Unreal.ClassOf<UGA_PlayerMeleeAttack>() : Tag(s.Definition.Definition.AbilityCallsign) == callsign && (kind == "roll" ? s.Definition.AbilityClass == Unreal.ClassOf<UGA_Roll>() : s.Definition.AbilityClass == Unreal.ClassOf<UGA_Artifact>());
            if (match)
            {
                h = s.Handle;
                count++;
            }
        }

        if (count != 1)
            return false;
        int nativeMatches = 0;
        foreach (var s in a.ActivatableAbilities.Items)
            if (s.Handle.Handle == h.Handle && !s.PendingRemove && Valid(s.Ability) && s.ActiveCount == 0)
                nativeMatches++;
        return nativeMatches == 1;
    }

    // 读取目标关联的能力任务归属，拒绝不确定的原生上下文。
    UGameplayAbility TaskOwner(APlayerCharacter p)
    {
        var a = USWAbilitySystemComponent.GetASC(p);
        FGameplayAbilitySpecHandle pickup = new FGameplayAbilitySpecHandle();
        int defs = 0;
        foreach (var s in a.SWAbilitySpecs.Items)
            if (Valid(s.Definition) && s.Definition.AbilityClass == Unreal.ClassOf<UGA_ItemPickup>() && Tag(s.Definition.Definition.AbilityCallsign) == "SW.AbilityCallsign.ItemPickup")
            {
                pickup = s.Handle;
                defs++;
            }

        if (defs != 1)
            return null;
        UGameplayAbility result = null;
        int count = 0;
        foreach (var s in a.ActivatableAbilities.Items)
            if (s.Handle.Handle == pickup.Handle && !s.PendingRemove)
            {
                foreach (var b in s.NonReplicatedInstances)
                    if (Valid(b) && b is UGA_ItemPickup && b.GetAvatarActorFromActorInfo() == p && b.GetAbilitySystemComponentFromActorInfo() == a)
                    {
                        result = b;
                        count++;
                    }

                foreach (var b in s.ReplicatedInstances)
                    if (Valid(b) && b is UGA_ItemPickup && b.GetAvatarActorFromActorInfo() == p && b.GetAbilitySystemComponentFromActorInfo() == a)
                    {
                        result = b;
                        count++;
                    }
            }

        return count == 1 ? result : null;
    }

    // 构造并发送原生近战事件；身上 TNT 等投掷状态继续按实现中的限制处理。
    bool MeleeEvent(APlayerCharacter p, FGameplayAbilitySpecHandle h, out FGameplayTag tag)
    {
        tag = new FGameplayTag();
        int matches = 0;
        foreach (var s in USWAbilitySystemComponent.GetASC(p).ActivatableAbilities.Items)
            if (s.Handle.Handle == h.Handle && s.Ability is UGA_PlayerMeleeAttack)
                foreach (var t in s.Ability.AbilityTriggers)
                    if (t.TriggerSource == EGameplayAbilityTriggerSource.GameplayEvent && Tag(t.TriggerTag) == "SW.GameplayEvent.PlayerMelee")
                    {
                        tag = t.TriggerTag;
                        matches++;
                    }

        return matches == 1;
    }

    // 以原生前滚能力执行一次已校验方向的闪避，记录前后位置以观察结果。
    bool ForwardRoll(APlayerCharacter p, FGameplayAbilitySpecHandle h, double envelope)
    {
        foreach (var s in USWAbilitySystemComponent.GetASC(p).SWAbilitySpecs.Items)
            if (s.Handle.Handle == h.Handle && Valid(s.Definition))
            {
                int directional = 0, strength = 0, durationCount = 0;
                double speed = 0, duration = 0;
                foreach (var value in s.Definition.Definition.SpecificKeyValues)
                {
                    if (Tag(value.TypeTag) == "SW.KeyValue.IsDirectional" && value.Type == EKeyValueType.Bool && !value.@bool)
                        directional++;
                    if (Tag(value.TypeTag) == "SW.KeyValue.RollStrength" && value.Type == EKeyValueType.Float)
                    {
                        speed = value.@float;
                        strength++;
                    }

                    if (Tag(value.TypeTag) == "SW.KeyValue.RollDuration" && value.Type == EKeyValueType.Float)
                    {
                        duration = value.@float;
                        durationCount++;
                    }
                }

                return directional == 1 && strength == 1 && durationCount == 1 && speed >= 100 && speed <= 2000 && duration >= .1 && duration <= 1 && envelope >= 800 && envelope <= 2500 && envelope >= speed * duration * 2 + 100;
            }

        return false;
    }

    // 轮询本次请求的能力、按住/释放和动作证据，超时不能当成成功。
    void Observe()
    {
        if (!waiting || !Valid(pendingPlayer))
            return;
        var a = USWAbilitySystemComponent.GetASC(pendingPlayer);
        if (!Valid(a))
            return;
        foreach (var s in a.ActivatableAbilities.Items)
            if (!s.PendingRemove && s.ActiveCount > 0)
            {
                if (s.Handle.Handle == handle)
                    abilityObserved = true;
                foreach (var b in s.NonReplicatedInstances)
                    if (Valid(b) && b.GetAvatarActorFromActorInfo() == pendingPlayer && b.GetAbilitySystemComponentFromActorInfo() == a)
                    {
                        if (s.Handle.Handle == handle && b is UGA_Artifact && !Valid(activeArtifact))
                        {
                            activeArtifact = b as UGA_Artifact;
                            artifactKey = b.CurrentActivationInfo.PredictionKeyWhenActivated;
                        }

                        if (s.Handle.Handle == heldHandle && !Valid(activeHeld))
                        {
                            activeHeld = b;
                            heldKey = b.CurrentActivationInfo.PredictionKeyWhenActivated;
                            heldObserved = true;
                        }
                    }

                foreach (var b in s.ReplicatedInstances)
                    if (Valid(b) && b.GetAvatarActorFromActorInfo() == pendingPlayer && b.GetAbilitySystemComponentFromActorInfo() == a)
                    {
                        if (s.Handle.Handle == handle && b is UGA_Artifact && !Valid(activeArtifact))
                        {
                            activeArtifact = b as UGA_Artifact;
                            artifactKey = b.CurrentActivationInfo.PredictionKeyWhenActivated;
                        }

                        if (s.Handle.Handle == heldHandle && !Valid(activeHeld))
                        {
                            activeHeld = b;
                            heldKey = b.CurrentActivationInfo.PredictionKeyWhenActivated;
                            heldObserved = true;
                        }
                    }
            }
    }

    // 读取固定双份信封，先处理 OFF/probe，再校验协议、场景、目标和能力并分派动作。
    void Poll()
    {
        var r = UGameplayStatics.LoadGameFromSlot("MCD2CombatRequest", 0) as Request;
        if (r == null)
        {
            Abort("Disabled");
            Write();
            return;
        }

        var copies = UKismetStringLibrary.ParseIntoArray(UKismetStringLibrary.TrimTrailing(r.Command), "#", false);
        if (r.Command.Length != 2048 || copies.Count != 2 || copies[0] != copies[1])
        {
            Abort("InvalidRequest");
            Write();
            return;
        }

        string command = copies[0];
        if (command == "OFF")
        {
            Abort("Disabled");
            Write();
            return;
        }

        var f = UKismetStringLibrary.ParseIntoArray(command, "|", false);
        if (f.Count != 20 || f[0] != "4" || f[1] != instance || f[2].Length != 32)
        {
            Abort("InvalidRequest");
            Write();
            return;
        }

        int next = UKismetStringLibrary.Conv_StringToInt(f[3]), requestedSlot = UKismetStringLibrary.Conv_StringToInt(f[9]);
        double px = UKismetStringLibrary.Conv_StringToDouble(f[10]), py = UKismetStringLibrary.Conv_StringToDouble(f[11]), pz = UKismetStringLibrary.Conv_StringToDouble(f[12]), tx = UKismetStringLibrary.Conv_StringToDouble(f[13]), ty = UKismetStringLibrary.Conv_StringToDouble(f[14]), tz = UKismetStringLibrary.Conv_StringToDouble(f[15]), dx = UKismetStringLibrary.Conv_StringToDouble(f[16]), dy = UKismetStringLibrary.Conv_StringToDouble(f[17]), range = UKismetStringLibrary.Conv_StringToDouble(f[18]), expires = UKismetStringLibrary.Conv_StringToDouble(f[19]);
        if (next <= 0)
        {
            Abort("InvalidRequest");
            Write();
            return;
        }

        if (f[4] == "probe")
        {
            if (waiting || f[8] != "1" || !(expires >= World.RealTime(this) && expires <= World.RealTime(this) + 2))
            {
                status = "InvalidProbe";
                Write();
                return;
            }

            epoch = f[2];
            sequence = next;
            status = "TransportReady";
            Write();
            return;
        }

        var p = UGameplayStatics.GetPlayerCharacter(this, 0) as APlayerCharacter;
        var c = UGameplayStatics.GetPlayerController(this, 0) as AGameplayPlayerController;
        if (f[2] == epoch && next == sequence)
        {
            if (waiting)
            {
                if (!Valid(p) || p != pendingPlayer)
                {
                    Abort("PlayerChanged");
                }
                else if (Identity(f) != requestIdentity)
                {
                    Abort("RequestChanged");
                }
                else if (kind == "artifact" && (!(expires >= World.RealTime(this) && expires <= World.RealTime(this) + .7) || !ArtifactStillValid(p, c)))
                {
                    Abort("LeaseOrTargetLost");
                }
                else
                {
                    Observe();
                    var a = USWAbilitySystemComponent.GetASC(p);
                    if (kind == "roll")
                    {
                        var after = p.K2_GetActorLocation();
                        double x = after.X - playerBefore.X, y = after.Y - playerBefore.Y;
                        if (abilityObserved && a.GetGameplayAttributeCurrentValue(Unreal.ClassOf<UATR_Movement>(), "RollCharges") < rollBefore && x * rollDirection.X + y * rollDirection.Y > 3)
                        {
                            EndTask();
                            status = "RollObserved";
                        }
                    }
                    else if (kind == "artifact" && (holdSeconds > 0 || aimed))
                    {
                        bool parentActive = false, childActive = false;
                        foreach (var s in a.ActivatableAbilities.Items)
                        {
                            if (s.Handle.Handle == handle && s.ActiveCount > 0)
                                parentActive = true;
                            if (s.Handle.Handle == heldHandle && s.ActiveCount > 0)
                                childActive = true;
                        }

                        if (abilityObserved && !parentActive && !childActive && (!aimed || targetDelivered) && (holdSeconds == 0 || heldObserved))
                        {
                            EndTask();
                            status = "ArtifactLifecycleEnded";
                        }
                        else
                        {
                            double tick = World.RealTime(this);
                            DriveArtifact(c, tick);
                            if (waiting && released)
                                status = "ReleaseRequested";
                            else if (waiting && aimed && targetDelivered && holdSeconds == 0)
                                status = "TargetSubmitted";
                        }
                    }
                    else if (abilityObserved)
                    {
                        EndTask();
                        status = "AbilityObserved";
                    }

                    if (waiting && World.RealTime(this) > deadline)
                    {
                        Abort("Unconfirmed");
                    }
                }
            }

            Write();
            return;
        }

        if (waiting)
        {
            Abort("RequestReplaced");
            Write();
            return;
        }

        EndTask();
        epoch = f[2];
        sequence = next;
        encounter = f[4] == "encounter";
        kind = encounter ? "melee" : f[4];
        slot = requestedSlot;
        abilityObserved = false;
        heldObserved = false;
        targetDelivered = false;
        released = false;
        aimed = false;
        holdSeconds = 0;
        heldHandle = 0;
        releaseAt = 0;
        requestIdentity = Identity(f);
        if (kind != "melee" && kind != "artifact" && kind != "roll")
        {
            status = "InvalidKind";
            Write();
            return;
        }

        if (!Ready(p, c) || UKismetSystemLibrary.GetObjectName(p) != f[5])
        {
            status = "PlayerBlocked";
            Write();
            return;
        }

        var here = p.K2_GetActorLocation();
        var expected = new FVector
        {
            X = px,
            Y = py,
            Z = pz
        };
        var there = new FVector
        {
            X = tx,
            Y = ty,
            Z = tz
        };
        double now = World.RealTime(this);
        if (!(expires >= now && expires <= now + .7))
        {
            status = "RequestExpired";
            Write();
            return;
        }

        if (!Near(here, expected, encounter ? 200 : kind == "artifact" ? 100 : 3, encounter || kind == "artifact" ? 30 : 3))
        {
            status = "PlayerPositionChanged";
            Write();
            return;
        }

        if (!(range >= 80 && range <= 2500) || !(dx * dx + dy * dy >= .999 && dx * dx + dy * dy <= 1.001))
        {
            status = "InvalidDirectionOrRange";
            Write();
            return;
        }

        var aPlayer = USWAbilitySystemComponent.GetASC(p);
        AActor target = null;
        if (kind != "roll")
        {
            int matches = 0;
            foreach (var actor in World.FindAll(this, Unreal.ClassOf<AMobCharacter>()))
                if (UKismetSystemLibrary.GetObjectName(actor) == f[6])
                {
                    target = actor;
                    matches++;
                }

            var mob = target as AMobCharacter;
            var a = Valid(mob) ? USWAbilitySystemComponent.GetASC(mob) : null;
            if (matches != 1 || !Valid(mob) || mob.IsActorBeingDestroyed() || mob.bHidden || Tag(mob.TypeInfo.TypeTag) != f[7] || !mob.IsHostileTowards(p) || !mob.IsTargetable() || !Valid(a) || a.GetGameplayTagCount(G("SW.State.Life.Alive")) <= 0 || a.GetGameplayTagCount(G("SW.Property.Attackable")) <= 0 || !Near(mob.K2_GetActorLocation(), there, 30, 30) || !Near(here, mob.K2_GetActorLocation(), kind == "melee" ? range : 800, kind == "melee" ? 100 : 250))
            {
                status = "TargetBlocked";
                Write();
                return;
            }

            if (kind == "melee")
            {
                double nativeRange = aPlayer.GetGameplayAttributeCurrentValue(Unreal.ClassOf<UATR_MeleeAttack>(), "MeleeAttackRange");
                if (!(range <= 500 && nativeRange >= 20 && nativeRange <= 1000))
                {
                    status = "InvalidMeleeRange";
                    Write();
                    return;
                }

                if (aPlayer.GetGameplayAttributeCurrentValue(Unreal.ClassOf<UATR_Throwable>(), "ThrowableHeldCount") != 0)
                {
                    status = "ThrowableHeld";
                    Write();
                    return;
                }
            // range 是用户限幅后的触发距离，不改 nativeRange；最终近战范围和伤害由原生形状/能力决定，不额外强加中心距限制。
            }
        }

        FGameplayAbilitySpecHandle ability;
        string callsign = kind == "artifact" ? "SW.AbilityCallsign.Artifact" + slot : "SW.AbilityCallsign.ForwardRoll";
        if (!Ability(p, callsign, out ability))
        {
            status = "AbilityUnavailable";
            Write();
            return;
        }

        FGameplayTag eventTag = new FGameplayTag();
        UGameplayAbility owner = null;
        if (kind == "melee" && !MeleeEvent(p, ability, out eventTag))
        {
            status = "EventUnavailable";
            Write();
            return;
        }

        if (kind != "melee")
        {
            owner = TaskOwner(p);
            if (!Valid(owner))
            {
                status = "ActivationContextUnavailable";
                Write();
                return;
            }

            if (kind == "artifact")
            {
                if (slot < 1 || slot > 3)
                {
                    status = "SlotBlocked";
                    Write();
                    return;
                }

                var equipped = UInventoryHelperLibrary.GetEquippedItemsInSlot(p, G("SW.ItemSlot.Equipment.Artifact.Slot" + slot));
                if (equipped.Count != 1 || !UInventoryHelperLibrary.CanActivateArtifact(p, equipped[0].ItemData))
                {
                    status = "ArtifactUnavailable";
                    Write();
                    return;
                }

                var types = USWTypeSystem.GetSWTypeSystemFromActor(p);
                if (!Valid(types))
                {
                    status = "ArtifactTypeUnavailable";
                    Write();
                    return;
                }

                artifactType = Tag(equipped[0].ItemData.TypeTag);
                artifactUID = equipped[0].ItemData.SessionUID;
                bool oneShot = false, charging = false, channeling = false;
                foreach (var trait in types.AS_GetType(equipped[0].ItemData.TypeTag).FinalizedTypeset.GameplayTags)
                {
                    if (Tag(trait) == "SW.Artifact.InputType.OneShot")
                        oneShot = true;
                    if (Tag(trait) == "SW.Artifact.Property.Targeting")
                        aimed = true;
                    if (Tag(trait) == "SW.Artifact.InputType.Charging")
                        charging = true;
                    if (Tag(trait) == "SW.Artifact.InputType.Channeling")
                        channeling = true;
                }

                if ((oneShot ? 1 : 0) + (charging ? 1 : 0) + (channeling ? 1 : 0) != 1)
                {
                    status = "ArtifactInputAmbiguous";
                    Write();
                    return;
                }

                // 持续时长依据原生最大蓄力期限限幅；成本、蓄力层级与耗尽由游戏处理。
                if (charging)
                {
                    if (artifactType == "SW.Item.Artifact.SoulHarvester")
                        holdSeconds = 6.2;
                    else if (artifactType == "SW.Item.Artifact.FlameSceptre")
                        holdSeconds = 4.2;
                    else
                    {
                        status = "ChargeDefinitionUnsupported";
                        Write();
                        return;
                    }
                }

                if (channeling)
                {
                    if (artifactType != "SW.Item.Artifact.CorruptedBeacon" && artifactType != "SW.Item.Artifact.LightningRod" && artifactType != "SW.Item.Artifact.BlizzardStaff")
                    {
                        status = "ChannelDefinitionUnsupported";
                        Write();
                        return;
                    }

                    holdSeconds = 2.2;
                }

                if ((charging || channeling) && (!HeldContext(p, charging) || artifactUID.UID == 0))
                {
                    status = "HeldContextUnavailable";
                    Write();
                    return;
                }

                if ((aimed || holdSeconds > 0) && !c.LineOfSightTo(target, here, true))
                {
                    status = "AimObstructed";
                    Write();
                    return;
                }
            }
            else if (!ForwardRoll(p, ability, range) || aPlayer.GetGameplayAttributeCurrentValue(Unreal.ClassOf<UATR_Movement>(), "RollCharges") < 1)
            {
                status = "RollUnavailable";
                Write();
                return;
            }
        }

        if (f[8] == "1")
        {
            status = "PreviewReady";
            Write();
            return;
        }

        if (f[8] != "0")
        {
            status = "InvalidRequest";
            Write();
            return;
        }

        double last = kind == "melee" ? lastMelee : kind == "artifact" ? lastArtifact : lastRoll;
        if (now - last < (kind == "melee" ? .35 : kind == "artifact" ? .25 : .8))
        {
            status = "Throttled";
            Write();
            return;
        }

        if (kind == "melee")
            lastMelee = now;
        else if (kind == "artifact")
            lastArtifact = now;
        else
            lastRoll = now;
        var data = new FGameplayEventData
        {
            Instigator = p,
            Target = target,
            EventTag = eventTag
        };
        var direction = kind == "roll" ? new FVector
        {
            X = here.X + dx * 300,
            Y = here.Y + dy * 300,
            Z = here.Z
        }

        : target.K2_GetActorLocation();
        var rot = UKismetMathLibrary.FindLookAtRotation(here, direction);
        rot.Pitch = 0;
        rot.Roll = 0;
        if (kind == "melee")
            data.TargetData = UAbilitySystemBlueprintLibrary.AbilityTargetDataFromActor(target);
        else if (kind == "artifact")
            data.TargetData = TargetData(p, target as AMobCharacter);
        else
        {
            // 原生 GA_Roll 的 IsDirectional=false 读取 RootComponent 朝向；只选择已校验方向，移动由 GA_Roll 实现。
            if (!p.K2_SetActorRotation(rot, false))
            {
                status = "FacingUnavailable";
                Write();
                return;
            }
        }

        c.SetControlRotation(rot);
        playerBefore = here;
        rollDirection = new FVector
        {
            X = dx,
            Y = dy,
            Z = 0
        };
        rollBefore = aPlayer.GetGameplayAttributeCurrentValue(Unreal.ClassOf<UATR_Movement>(), "RollCharges");
        handle = ability.Handle;
        pendingPlayer = p;
        pendingTarget = target as AMobCharacter;
        waiting = true;
        startedAt = now;
        deadline = now + (kind == "artifact" && (holdSeconds > 0 || aimed) ? 9 : .6);
        if (kind == "melee")
            UAbilitySystemBlueprintLibrary.SendGameplayEventToActor(p, eventTag, data);
        else
        {
            task = USWAbilityTask_TryActivateAbilityAndWait.TryActivate(owner, ability, data, aPlayer);
            if (!Valid(task))
            {
                EndTask();
                status = "ActivationUnavailable";
                Write();
                return;
            }

            task.ReadyForActivation();
        }

        Observe();
        status = "Dispatched";
        Write();
    }
}

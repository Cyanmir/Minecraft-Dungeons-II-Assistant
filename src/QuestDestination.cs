// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 只读定位已适配任务目标与关联导航状态。
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

sealed class QuestDestination
{
    public string Session, ActorClass, TypeTag, QuestId, BehaviorName;
    public int ActorIndex, ActorSerial;
    public ThreatVector Position;
    public bool Hidden, CollisionEnabled, Interactable;
    public Dictionary<string, object> Behavior;
}

sealed partial class HealthReader
{
    const string CopperReplayType = "SW.QuestActor.CA07.BR.QuestGiver";
    int activeBehaviorOffset = -1;
    // 解析已验证任务行为访问布局。
    int ActiveBehaviorOffset()
    {
        if (activeBehaviorOffset >= 0)
            return activeBehaviorOffset;
        long image = process.MainModule.BaseAddress.ToInt64(), body = 0;
        string wrapperPattern = "48 8D 54 24 ?? E8 ?? ?? ?? ?? 4C 8B F0", getterPattern = "48 8B 91 ?? ?? ?? ?? 48 85 D2";
        using (var symbols = new LocalSymbols(M.Handle, process.MainModule.FileName, image, process.MainModule.ModuleMemorySize))
            foreach (string name in new[]
            {
                "UQuestActorBehaviourComponent::GetActiveBehaviorConfig",
                "QuestActorBehaviourComponent::GetActiveBehaviorConfig"
            }

            )
            {
                var symbol = symbols.Find(name);
                if (symbol != null && executable(symbol.Address, 1024))
                {
                    body = symbol.Address;
                    break;
                }
            }

        if (body == 0)
        {
            long wrapper = NativeWrapper(NamedFunction("QuestActorBehaviourComponent", "GetActiveBehaviorConfig"), 1024);
            byte[] code = M.Read(wrapper, 1024);
            int at = SingleMatch(code, wrapperPattern);
            body = RelativeCall(wrapper, code, at + 5);
            AdaptationRecord.Set("function.QuestActorBehaviourComponent.GetActiveBehaviorConfig.wrapper", "RVA=0x" + (wrapper - image).ToString("X") + "; bytes=" + BitConverter.ToString(code).Replace('-', ' '));
            AdaptationRecord.Set("questDestination.lookup", "native symbols unavailable -> reflected function name -> verified instruction patterns; no function invocation");
        }
        else
            AdaptationRecord.Set("questDestination.lookup", "native symbol -> verified instruction pattern; no function invocation");
        byte[] bytes = M.Read(body, 1024);
        int match = SingleMatch(bytes.Take(128).ToArray(), getterPattern), offset = BitConverter.ToInt32(bytes, match + 3);
        if (offset < 128 || offset > 4096 || offset % 8 != 0)
            throw new Exception("Quest behavior layout requires adaptation");
        AdaptationRecord.Set("signature.catalog.questDestination.wrapper", wrapperPattern);
        AdaptationRecord.Set("signature.catalog.questDestination.activeConfig", getterPattern);
        AdaptationRecord.Set("function.QuestActorBehaviourComponent.GetActiveBehaviorConfig.body", "RVA=0x" + (body - image).ToString("X") + "; activeConfigOffset=0x" + offset.ToString("X") + "; bytes=" + BitConverter.ToString(bytes).Replace('-', ' '));
        activeBehaviorOffset = offset;
        return offset;
    }

    // 唯一查找目标任务配置的关联条目。
    static bool QuestConfigIndex(long array, int count, int stride, long active, out int index)
    {
        index = -1;
        if (count < 1 || count > 128 || stride != 208 || array <= 0 || active < array)
            return false;
        long difference = active - array;
        if (difference % stride != 0 || difference / stride >= count)
            return false;
        index = (int)(difference / stride);
        return true;
    }

    public QuestDestination ReadCopperReplayDestination()
    {
        Identity pawn = Pawn(), playerLevel = Token(M.Q(pawn.Address + 32)), world = Token(M.Q(playerLevel.Address + Offset(playerLevel, "OwningWorld", 8)));
        long type = Struct("SWTypeInfo", 16), configType = Struct("QuestActorBehaviourConfig", 208);
        int typeTag = Prop(type, "TypeTag", 8).Offset;
        var found = new List<Identity>();
        foreach (Identity actor in AllTokens())
            try
            {
                if (!IsA(actor.Class, "QuestASCActor") || Name(actor.Name).StartsWith("Default__"))
                    continue;
                if (ReadBool(actor, "bActorIsBeingDestroyed"))
                    continue;
                if (Name(M.I(actor.Address + Offset(actor, "TypeInfo", 16) + typeTag)) != CopperReplayType)
                    continue;
                Identity level = Token(M.Q(actor.Address + 32));
                if (!IsA(level.Class, "Level") || M.Q(level.Address + Offset(level, "OwningWorld", 8)) != world.Address)
                    continue;
                found.Add(actor);
            }
            catch
            {
            }

        if (found.Count != 1)
            throw new Exception("Copper replay pedestal is not uniquely loaded in the player's world");
        Identity target = found[0], component = Token(M.Q(target.Address + Offset(target, "QuestActorBehaviourComponent", 8)));
        if (!IsA(component.Class, "QuestActorBehaviourComponent") || M.Q(component.Address + 32) != target.Address)
            throw new Exception("Quest behavior owner mismatch");
        long header = component.Address + Offset(component, "BehaviourConfigs", 16), data = M.Q(header);
        int count = M.I(header + 8), capacity = M.I(header + 12);
        long active = M.Q(component.Address + ActiveBehaviorOffset());
        int configIndex;
        if (capacity < count || capacity > 512 || !QuestConfigIndex(data, count, 208, active, out configIndex))
            throw new Exception("Active quest behavior does not belong to the reflected config array");
        byte[] before = M.Read(active, 208);
        var config = ReadItemStruct(configType, active, 0);
        var task = (Dictionary<string, object>)config["TaskStateTrigger"];
        string quest = Convert.ToString(task["QuestName"]);
        // 只读任务配置；可用与活动配置必须归属同一任务，两者均不能证明 Boss 当前存活。
        if (quest != "CA07_BR" || !Convert.ToBoolean(task["bUseQuestState"]))
            throw new Exception("Quest does not match the active behavior");
        var result = new QuestDestination
        {
            Session = InventorySession(),
            ActorClass = ClassName(target.Class),
            TypeTag = CopperReplayType,
            QuestId = quest,
            BehaviorName = Convert.ToString(config["Name"]),
            ActorIndex = target.Index,
            ActorSerial = target.Serial,
            Position = Position(target),
            Hidden = ReadBool(target, "bHidden"),
            CollisionEnabled = ReadBool(target, "bActorEnableCollision"),
            Interactable = Convert.ToBoolean(config["bIsInteractable"]),
            Behavior = config
        };
        if (!Valid(pawn) || !Valid(target) || !Valid(component) || !Valid(world) || ReadBool(target, "bActorIsBeingDestroyed") || M.Q(header) != data || M.I(header + 8) != count || M.Q(component.Address + activeBehaviorOffset) != active || !M.Read(active, 208).SequenceEqual(before) || InventorySession() != result.Session)
            throw new Exception("Quest destination changed during capture");
        AdaptationRecord.Set("questDestination.identity", "SWTypeInfo.TypeTag=" + CopperReplayType + "; active config verified against BehaviourConfigs; quest=" + quest + "; no fixed heap pointer or hard-coded destination coordinates");
        return result;
    }

}

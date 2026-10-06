// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 读取玩家菜单、生命和战斗可用性状态。
using System;
using System.Collections.Generic;
using System.Linq;

// CombatReadiness 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
sealed class CombatReadiness
{
    public bool Known, Alive, HasRollCharge, GameplayUiOnly, AbilityEligibilityVerified, CanExecute;
    public double RollCharges, RollChargesMax, RollBaseCooldown;
    public string Reason;
    public string[] ActivePanels, PlayerTags;
}

// 跨文件的只读游戏读取器；各 partial 文件共同持有同一连接/对象身份缓存。
sealed partial class HealthReader
{
    readonly Dictionary<long, Identity> combatWidgets = new Dictionary<long, Identity>();
    // 范围仅作为说明，每秒刷新一次，避免在 75ms 战斗轮询中重复枚举属性集。
    Identity meleeRangeOwner;
    double? cachedMeleeRange;
    int meleeRangeSampled;
    // UI 可在战斗开关关闭时读取原生范围，仍通过 Pawn 身份校验。
    public double? MeleeRangeForUi()
    {
        try
        {
            return ReadMeleeRange(Pawn());
        }
        catch
        {
            return null;
        }
    }

    double? ReadMeleeRange(Identity player)
    {
        int now = Environment.TickCount;
        if (meleeRangeOwner != null && meleeRangeOwner.Address == player.Address && meleeRangeOwner.Serial == player.Serial && (uint)(now - meleeRangeSampled) < 1000 && Valid(player))
            return cachedMeleeRange;
        meleeRangeOwner = player;
        meleeRangeSampled = now;
        cachedMeleeRange = null;
        try
        {
            Identity asc = Token(M.Q(player.Address + Offset(player, "AbilitySystemComponent", 8)));
            long header = asc.Address + Offset(asc, "SpawnedAttributes", 16), data = M.Q(header);
            int count = M.I(header + 8), capacity = M.I(header + 12);
            if (count < 1 || count > 128 || capacity < count || capacity > 512)
                return null;
            int current = Prop(Struct("GameplayAttributeData", 16), "CurrentValue", 4).Offset;
            if (current != 12)
                return null;
            Identity melee = null;
            for (int i = 0; i < count; i++)
            {
                Identity candidate = Token(M.Q(data + i * 8));
                if (ClassName(candidate.Class) != "ATR_MeleeAttack")
                    continue;
                if (melee != null || (M.Q(candidate.Address + 32) != player.Address && M.Q(candidate.Address + 32) != asc.Address))
                    return null;
                melee = candidate;
            }

            if (melee == null)
                return null;
            double range = M.F(melee.Address + Offset(melee, "MeleeAttackRange", 16) + current);
            if (!CombatNumber(range) || range < 20 || range > 1000 || !Valid(player) || !Valid(asc) || !Valid(melee) || M.Q(header) != data || M.I(header + 8) != count || M.Q(asc.Address + Offset(asc, "AvatarActor", 8)) != player.Address)
                return null;
            cachedMeleeRange = range;
            return cachedMeleeRange;
        }
        catch (Exception e)
        {
            AdaptationRecord.Set("combat.melee.range.read", e.Message);
            return null;
        }
    }

    // 读取生命、菜单和原生可操作状态，任何未知前提继续阻止动作。
    CombatReadiness ReadCombatReadiness(Identity player)
    {
        var result = new CombatReadiness();
        try
        {
            var tags = OwnedTags(player);
            result.PlayerTags = tags.ToArray();
            result.Alive = tags.Contains("SW.State.Life.Alive");
            Identity asc = Token(M.Q(player.Address + Offset(player, "AbilitySystemComponent", 8)));
            long h = asc.Address + Offset(asc, "SpawnedAttributes", 16), data = M.Q(h);
            int n = M.I(h + 8), capacity = M.I(h + 12);
            if (n < 1 || n > 128 || capacity < n || capacity > 512)
                throw new Exception("Movement attribute bounds invalid");
            long attribute = Struct("GameplayAttributeData", 16);
            int current = Prop(attribute, "CurrentValue", 4).Offset;
            if (current != 12)
                throw new Exception("Movement attribute value schema changed");
            Identity movement = null;
            for (int i = 0; i < n; i++)
            {
                Identity a = Token(M.Q(data + i * 8));
                if (ClassName(a.Class) != "ATR_Movement")
                    continue;
                if (movement != null || M.Q(a.Address + 32) != player.Address && M.Q(a.Address + 32) != asc.Address)
                    throw new Exception("Movement attribute ownership invalid");
                movement = a;
            }

            if (movement == null)
                throw new Exception("Movement attributes unavailable");
            result.RollCharges = M.F(movement.Address + Offset(movement, "RollCharges", 16) + current);
            result.RollChargesMax = M.F(movement.Address + Offset(movement, "RollChargesMax", 16) + current);
            result.RollBaseCooldown = M.F(movement.Address + Offset(movement, "RollCooldown", 16) + current);
            if (!CombatNumber(result.RollCharges) || !CombatNumber(result.RollChargesMax) || !CombatNumber(result.RollBaseCooldown) || result.RollCharges < 0 || result.RollChargesMax < 1 || result.RollChargesMax > 100 || result.RollCharges > result.RollChargesMax + .001 || result.RollBaseCooldown < 0 || result.RollBaseCooldown > 60)
                throw new Exception("Roll attribute values invalid");
            result.HasRollCharge = result.RollCharges >= 1;
            var active = new List<string>();
            foreach (Identity widget in combatWidgets.Values.ToArray())
            {
                if (!Valid(widget))
                {
                    combatWidgets.Remove(widget.Address);
                    continue;
                }

                if (ReadBool(widget, "bIsActive"))
                    active.Add(ClassName(widget.Class));
            }

            result.ActivePanels = active.ToArray();
            result.GameplayUiOnly = active.Contains("W_PlayerHUD_Activatable_C") && active.All(name => new[] { "W_HotbarContainer_Activatable_C", "W_PlayerHUD_Activatable_C", "W_ActivityLayer_Activatable_C" }.Contains(name));
            if (!Valid(player) || !Valid(asc) || !Valid(movement) || M.Q(h) != data || M.I(h + 8) != n || M.Q(asc.Address + Offset(asc, "AvatarActor", 8)) != player.Address)
                throw new Exception("Player state changed during read");
            result.Known = true;
            result.Reason = "live charges and UI only; native dodge activation requirements and safe destination are not validated";
        }
        catch (Exception e)
        {
            result.Known = false;
            result.HasRollCharge = false;
            result.Reason = e.Message;
        }

        // 只有充能不能证明游戏会接受闪避；此处不启用输入。
        result.AbilityEligibilityVerified = false;
        result.CanExecute = false;
        return result;
    }
}

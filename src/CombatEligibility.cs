// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 中文维护说明：读取玩家菜单、生命和战斗可用性状态。这里决定是否可以尝试动作；新增状态应从已验证反射属性读取，未知数据应阻止动作。
// 源码导航和常改参数见 docs/maintenance.md；协议、反射标识及类型白名单修改前先核对两端。
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

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 导出自动化相关的反射对象与键位。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

// 跨文件的只读游戏读取器；各 partial 文件共同持有同一连接/对象身份缓存。
sealed partial class HealthReader
{
    const string AutomationNames = "Food|Consumable|Carrot|Melon|Potato|Apple|Pumpkin|DataTable|CommonInput|Map|Door|Guidance|Spline|Waypoint|Boss|Copper|Inventory|Equipment|SlotEntry|Item|GameplayTag|SWSessionUID|MinimalViewInfo|CameraCacheEntry|Salvage|Sell|Loot|Pickup|Treasure|Chest|Interact|Navigation|Nav|Gear|Quest|UIAction|Widget|Viewport|Button|PathFollowing|Cinematic|Cutscene|SequencePlayer|Dialogue|Mission|LevelProgress|Dungeon|MapTravel|Objective|Rarity|Enchantment|Bookmark";
    // 枚举并核对当前已加载 UObject 身份，不信任无归属地址。
    List<Identity> AllTokens()
    {
        var tokens = new List<Identity>();
        int count = M.I(objects + 20);
        long table = M.Q(objects);
        for (int ci = 0; ci * 65536 < count; ci++)
        {
            byte[] chunk = M.Read(M.Q(table + ci * 8), Math.Min(65536, count - ci * 65536) * 24);
            for (int at = 0; at < chunk.Length; at += 24)
                try
                {
                    long a = BitConverter.ToInt64(chunk, at);
                    if (a != 0)
                        tokens.Add(Token(a));
                }
                catch
                {
                }
        }

        return tokens;
    }

    // 读取完整键盘绑定表，供工具热键冲突检查。
    public Dictionary<string, string> AllKeyboardBindings()
    {
        var bindings = new Dictionary<string, string>();
        long mapping = Struct("PlayerKeyMapping", 168), rowStruct = Struct("KeyMappingRow", 80);
        int action = Prop(mapping, "MappingName", 8).Offset, current = Prop(mapping, "CurrentKey", 24).Offset;
        foreach (Identity profile in keyProfiles)
        {
            if (!Valid(profile))
                continue;
            Identity owner = Token(M.Q(profile.Address + 32));
            if (ClassName(owner.Class) != "SWEnhancedInputUserSettings" || M.I(owner.Address + Offset(owner, "OwningLocalPlayer", 8)) != local.Index || M.I(owner.Address + Offset(owner, "OwningLocalPlayer", 8) + 4) != local.Serial)
                continue;
            long id = profile.Address + Offset(profile, "ProfileIdentifierString", 16);
            int len = M.I(id + 8);
            if (len < 1 || len > 256)
                continue;
            if (Encoding.Unicode.GetString(M.Read(M.Q(id), len * 2)).TrimEnd('\0') != "SW.Input.Profile.InputType.Keyboard")
                continue;
            foreach (long row in SparseElements(profile.Address + Offset(profile, "PlayerMappedKeys", 80), 96, 512))
            {
                long h = row + 8 + Prop(rowStruct, "Mappings", 80).Offset;
                foreach (long key in SparseElements(h, 176, 32))
                    if (M.Read(key + Prop(mapping, "Slot", 1).Offset, 1)[0] == 0)
                        bindings[Name(M.I(key + action))] = Name(M.I(key + current));
            }
        }

        return bindings;
    }

}

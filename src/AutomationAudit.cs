// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 中文维护说明：导出自动化相关的反射对象与键位。该文件是只读诊断入口；新增采集项应复用 HealthReader 的身份和大小检查，不能在采集过程中触发游戏动作。
// 源码导航和常改参数见 docs/maintenance.md；协议、反射标识及类型白名单修改前先核对两端。
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

    // 导出玩家绑定与自动化相关反射信息，不执行动作。
    public void AuditAutomation(string path)
    {
        var tokens = AllTokens();
        var types = new List<object>();
        var live = new List<object>();
        var functions = new List<object>();
        var enums = new List<object>();
        var selected = new HashSet<long>();
        Identity pawn = Pawn();
        foreach (Identity t in tokens)
            try
            {
                string kind = ClassName(t.Class), name = Name(t.Name);
                if ((kind.EndsWith("Class") || IsA(t.Class, "ScriptStruct")) && (Regex.IsMatch(name, AutomationNames, RegexOptions.IgnoreCase) || name == "World" || name == "Level" || name == "PlayerController" || name == "PlayerCharacter" || name == "PlayerCameraManager"))
                {
                    selected.Add(t.Address);
                    types.Add(new { name = name, kind = kind, lineage = AuditLineage(t.Address), structSize = IsA(t.Class, "ScriptStruct") ? M.I(t.Address + 88) : 0, properties = AuditProperties(t.Address) });
                }

                if (kind == "Enum" && Regex.IsMatch(name, AutomationNames, RegexOptions.IgnoreCase))
                {
                    var candidates = new List<object>();
                    for (int off = 48; off <= 112; off += 8)
                        try
                        {
                            long data = M.Q(t.Address + off);
                            int n = M.I(t.Address + off + 8), cap = M.I(t.Address + off + 12);
                            if (n < 1 || n > 128 || cap < n || cap > 256)
                                continue;
                            var values = new List<object>();
                            for (int i = 0; i < n; i++)
                            {
                                string en = Name(M.I(data + i * 16));
                                if (!en.Contains("::"))
                                    throw new Exception();
                                values.Add(new { name = en, value = M.Q(data + i * 16 + 8) });
                            }

                            candidates.Add(new { arrayOffset = off, values = values });
                        }
                        catch
                        {
                        }

                    enums.Add(new { name = name, candidates = candidates });
                }
            }
            catch
            {
            }

        foreach (Identity t in tokens)
            try
            {
                string cn = ClassName(t.Class);
                if (!Valid(t) || Name(t.Name).StartsWith("Default__") || !Regex.IsMatch(cn, AutomationNames + "|PlayerCameraManager|PlayerController", RegexOptions.IgnoreCase) || cn == "Function" || cn == "Class" || cn == "BlueprintGeneratedClass" || cn == "ScriptStruct")
                    continue;
                var values = new Dictionary<string, object>();
                foreach (var field in AuditProperties(t.Class))
                {
                    var json = new JavaScriptSerializer();
                    var p = json.Deserialize<Dictionary<string, object>>(json.Serialize(field));
                    string pn = (string)p["name"];
                    if (pn == "OwningPlayerID" || pn == "LastEquippedItem")
                        continue;
                    if (!Regex.IsMatch(pn, "Current|Marker|Door|Guidance|Map|SkipInput|Nav|Selected|Active|State|Inventory|Equip|Item|Entry|Rarity|Power|Lock|Bookmark|Enchant|Salvage|Reward|Interact|Open|Skip|Playing|Progress|Path|Camera|Sequence|Target|Owner|Level|Objective|Count|Version|Complete", RegexOptions.IgnoreCase))
                        continue;
                    int offset = Convert.ToInt32(p["offset"]), size = Convert.ToInt32(p["size"]);
                    if (offset < 0 || offset > 0x20000)
                        continue;
                    try
                    {
                        object value = null;
                        if (size == 1)
                            value = (string)p["propertyKind"] == "BoolProperty" ? (object)ReadBool(t, pn) : M.Read(t.Address + offset, 1)[0];
                        else if (size == 4)
                        {
                            int integer = M.I(t.Address + offset);
                            value = new
                            {
                                integer = integer,
                                number = BitConverter.ToSingle(BitConverter.GetBytes(integer), 0).ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                            };
                        }
                        else if (size == 8)
                        {
                            long ptr = M.Q(t.Address + offset);
                            try
                            {
                                Identity target = Token(ptr);
                                value = new
                                {
                                    objectName = Name(target.Name),
                                    className = ClassName(target.Class),
                                    isPawn = ptr == pawn.Address
                                };
                            }
                            catch
                            {
                                value = new
                                {
                                    name = Name(M.I(t.Address + offset)),
                                    number = M.I(t.Address + offset + 4)
                                };
                            }
                        }
                        else if (size == 16)
                        {
                            value = new
                            {
                                count = M.I(t.Address + offset + 8),
                                capacity = M.I(t.Address + offset + 12)
                            };
                        }
                        else
                            value = new
                            {
                                size = size
                            };
                        values[pn] = value;
                    }
                    catch
                    {
                    }
                }

                string outer = "";
                try
                {
                    outer = ClassName(Token(M.Q(t.Address + 32)).Class);
                }
                catch
                {
                }

                object position = null;
                if (IsA(t.Class, "Actor"))
                    try
                    {
                        position = Position(t);
                    }
                    catch
                    {
                    }

                live.Add(new { name = Name(t.Name), className = cn, objectIndex = t.Index, serial = t.Serial, outerClass = outer, position = position, fields = values });
            }
            catch
            {
            }

        long image = process.MainModule.BaseAddress.ToInt64();
        using (var symbols = new LocalSymbols(M.Handle, process.MainModule.FileName, image, process.MainModule.ModuleMemorySize))
            foreach (Identity t in tokens)
                try
                {
                    if (ClassName(t.Class) != "Function")
                        continue;
                    Identity owner = Token(M.Q(t.Address + 32));
                    string name = Name(t.Name);
                    if (!selected.Contains(owner.Address) && !Regex.IsMatch(name, "Salvage|SellItem|Pickup|Chest|Skip.*(Cine|Scene|Movie|Sequence|Dialogue)|Travel|Restart|PathTo|MoveToLocation|ProjectWorld", RegexOptions.IgnoreCase))
                        continue;
                    string qualified = Name(owner.Name) + "::" + name;
                    var symbol = symbols.Find(qualified);
                    var wrappers = new List<object>();
                    if (symbol != null && executable(symbol.Address, 128))
                        wrappers.Add(new { source = "native-symbol", rva = "0x" + (symbol.Address - image).ToString("X"), bytes = BitConverter.ToString(M.Read(symbol.Address, 128)).Replace('-', ' ') });
                    if (symbol == null)
                        for (int slot = 120; slot <= 240; slot += 8)
                        {
                            long fn = M.Q(t.Address + slot);
                            if (executable(fn, 128))
                                wrappers.Add(new { source = "reflected-function-pointer", rva = "0x" + (fn - image).ToString("X"), bytes = BitConverter.ToString(M.Read(fn, 128)).Replace('-', ' ') });
                        }

                    functions.Add(new { name = qualified, nativeSymbolAvailable = symbol != null, wrappers = wrappers });
                }
                catch
                {
                }

        var report = new
        {
            status = "local read-only automation research; no game input or game function invocation",
            scope = "Loaded runtime metadata only",
            objectCount = tokens.Count,
            types = types,
            enums = enums,
            live = live,
            functions = functions,
            keyboardBindings = AllKeyboardBindings(),
            lookupRecord = AdaptationRecord.Contents()
        };
        File.WriteAllText(path, new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Serialize(report), new UTF8Encoding(true));
    }
}

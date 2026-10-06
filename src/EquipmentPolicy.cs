// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 中文维护说明：解码背包装备并生成带保护原因的整理计划。Normal/Storm 按七个类别索引，品质枚举值参与阈值比较；未知字段、重复 UID、装备中、锁定及附魔等保护不能因显示变化被绕过。
// 源码导航和常改参数见 docs/maintenance.md；协议、反射标识及类型白名单修改前先核对两端。
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

// 七类装备的固定索引及 Unsupported；顺序与 UI/保存数组对应，不能随意重排。
enum EquipmentCategory
{
    Melee,
    Ranged,
    Helmet,
    Chest,
    Leggings,
    Boots,
    Artifact,
    Unsupported
}

// 品质阈值枚举；Off 表示关闭该类回收，其他值按品质顺序比较。
enum EquipmentRarity
{
    Off,
    Common,
    Rare,
    Special,
    Unique
}

// 已解码装备及保护标记；Id 为 SessionUID，Known=false 一律不纳入回收。
sealed class EquipmentItem
{
    public string Id, Type, Container, Source, Quest;
    public EquipmentCategory Category = EquipmentCategory.Unsupported;
    public EquipmentRarity Rarity;
    public int Power;
    public bool Known, Equipped, Locked, Enchanted, SoulStorm, SourceProtected, OtherProtected;
    // 按原生装备/背包槽标签映射七个类别，未知槽保持 Unsupported。
    public static EquipmentCategory CategoryFor(string slot)
    {
        string p = "SW.ItemSlot.Inventory.", q = "SW.ItemSlot.Equipment.";
        if (slot.StartsWith(p))
            slot = slot.Substring(p.Length);
        else if (slot.StartsWith(q))
            slot = slot.Substring(q.Length);
        else
            return EquipmentCategory.Unsupported;
        switch (slot)
        {
            case "MeleeWeapon":
                return EquipmentCategory.Melee;
            case "RangedWeapon":
                return EquipmentCategory.Ranged;
            case "Armor.Helmet":
                return EquipmentCategory.Helmet;
            case "Armor.Chest":
                return EquipmentCategory.Chest;
            case "Armor.Leggings":
                return EquipmentCategory.Leggings;
            case "Armor.Boots":
                return EquipmentCategory.Boots;
            case "Artifact":
            case "Artifact.Slot1":
            case "Artifact.Slot2":
            case "Artifact.Slot3":
                return EquipmentCategory.Artifact;
        }

        return EquipmentCategory.Unsupported;
    }

    // 要求字段为可读结构字典，拒绝 unread/unsupported 占位。
    static Dictionary<string, object> Obj(object value)
    {
        var o = value as Dictionary<string, object>;
        if (o == null || o.ContainsKey("unread") || o.ContainsKey("unsupported"))
            throw new Exception("Unverified item field");
        return o;
    }

    // 要求字段为实际数组，拒绝字符串/字典冒充列表。
    static object[] Arr(object value)
    {
        var a = value as IEnumerable;
        if (a == null || value is string || value is IDictionary)
            throw new Exception("Unverified item array");
        return a.Cast<object>().ToArray();
    }

    // 要求有界字符串，防止未知值转换后被误认成有效标签。
    static string Str(object value)
    {
        string s = value as string;
        if (s == null || s.Length > 256)
            throw new Exception("Unverified item name");
        return s;
    }

    // 读取 SessionUID、品质、力量、收藏/附魔/来源保护；任何未知结构保持 Known=false。
    public static EquipmentItem Decode(Dictionary<string, object> row)
    {
        var item = new EquipmentItem();
        try
        {
            var d = Obj(row["ItemData"]);
            item.Id = Str(d["SessionUID"]);
            item.Type = Str(d["TypeTag"]);
            item.Container = Str(row["Container"]);
            item.Category = CategoryFor(item.Container);
            string eq = Str(row["EquippedSlot"]);
            item.Equipped = eq != "None" || item.Container.StartsWith("SW.ItemSlot.Equipment.");
            var tags = Arr(Obj(d["DynamicPropertyTags"])["GameplayTags"]).Select(Str).ToArray();
            item.Locked = tags.Contains("SW.Item.Property.Dynamic.Favourite");
            // 此标签已对照 InventoryHelperLibrary::IsSoulStormGearItem 及原生标签全局核验。
            item.SoulStorm = tags.Contains("SW.Item.Property.StorminatorReward");
            item.OtherProtected = tags.Any(t => t != "SW.Item.Property.Dynamic.Favourite" && t != "SW.Item.Property.Dynamic.Unseen" && t != "SW.Item.Property.StorminatorReward");
            string rarity = Str(d["RarityTag"]);
            EquipmentRarity r;
            if (!rarity.StartsWith("SW.Rarity.") || !Enum.TryParse(rarity.Substring(10), false, out r) || r < EquipmentRarity.Common || r > EquipmentRarity.Unique)
                throw new Exception("Unverified rarity");
            item.Rarity = r;
            item.Power = Convert.ToInt32(Obj(Obj(d["GeneratorData"])["PowerGeneratorValues"])["ItemPower"]);
            if (item.Power < 0 || item.Power > 100000)
                throw new Exception("Unverified power");
            foreach (object batch in Arr(d["Effects"]))
            {
                var b = Obj(batch);
                string type = Str(b["TypeTag"]);
                if (type == "SW.Item.Effect.Enchantment")
                    item.Enchanted = true;
                if (type != "SW.Item.Effect.Enchantment" && type != "SW.Item.Effect.Static" && type != "SW.Item.Effect.Rerollable" && type != "SW.Item.Effect.Upgradable")
                    item.OtherProtected = true;
                foreach (object effect in Arr(b["EffectsInThisBatch"]))
                    if (Convert.ToInt32(Obj(effect)["EnchantmentPointsInvested"]) > 0)
                        item.Enchanted = true;
            }

            item.Source = Str(d["DropSource"]);
            item.Quest = Str(d["QuestSource"]);
            // 只放行已验证敌人掉落来源；未知来源和任务来源继续受保护。
            item.SourceProtected = (item.Source != "None" && !EquipmentDropOrigins.Known(item.Source)) || item.Quest != "None";
            item.OtherProtected |= Arr(row["MetaData"]).Length > 0;
            item.Known = item.Id.Length == 20 && item.Type.StartsWith("SW.Item.") && Convert.ToInt32(row["StackCount"]) == 1;
        }
        catch
        {
            item.Known = false;
        }

        return item;
    }
}

// 七类装备的阈值与保护开关；所有执行计划必须经过 Plan，不能直接根据 UI 行出售。
sealed class EquipmentPolicy
{
    // 七类普通/风暴装备品质阈值数组；索引与 EquipmentCategory 对应，Off 表示该类不回收。
    public EquipmentRarity[] Normal = Enumerable.Repeat(EquipmentRarity.Common, 7).ToArray(), Storm = new EquipmentRarity[7];
    public int ToggleKey = 0x76;
    // 重复装备处理与升级/商人/任务来源保护开关，未知来源仍由 Plan 单独保护。
    public bool Duplicates, KeepUpgrades = true, KeepMerchant = true, KeepQuest = true;
    // 装备保护规则文件路径，与主设置目录保持一致。
    public static string PathName
    {
        get
        {
            return Path.Combine(Path.GetDirectoryName(ToolboxSettings.ConfigPath), "equipment-policy.json");
        }
    }

    // 加载本模块的配置或内嵌目录，并使用实现中的校验/回退规则。
    public static EquipmentPolicy Load()
    {
        try
        {
            var p = new JavaScriptSerializer().Deserialize<EquipmentPolicy>(File.ReadAllText(PathName));
            if (p.Normal == null || p.Storm == null || p.Normal.Length != 7 || p.Storm.Length != 7 || p.Normal.Concat(p.Storm).Any(v => v < EquipmentRarity.Off || v > EquipmentRarity.Unique))
                throw new Exception();
            if (!ToolboxInput.Allowed(p.ToggleKey))
                p.ToggleKey = 0x76;
            return p;
        }
        catch
        {
            return new EquipmentPolicy();
        }
    }

    // 持久化本模块设置；先写临时文件，再替换原文件，避免留下半份配置。
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathName));
        string tmp = PathName + ".tmp";
        File.WriteAllText(tmp, new JavaScriptSerializer().Serialize(this), new UTF8Encoding(true));
        if (File.Exists(PathName))
            File.Replace(tmp, PathName, null);
        else
            File.Move(tmp, PathName);
    }

    // 按类别品质阈值和逐项保护规则生成计划，未知或重复 UID 一律保留。
    public List<EquipmentDecision> Plan(IEnumerable<EquipmentItem> source, ISet<string> existing, bool all)
    {
        var items = source.ToList();
        var result = new List<EquipmentDecision>();
        bool duplicateIds = items.Where(i => i.Category != EquipmentCategory.Unsupported).GroupBy(i => i.Id).Any(g => String.IsNullOrEmpty(g.Key) || g.Count() > 1);
        foreach (var item in items)
        {
            string reason = null;
            bool sell = false;
            if (duplicateIds || !item.Known)
                reason = "DataUnknown";
            else if (item.Category == EquipmentCategory.Unsupported)
                reason = "Unsupported";
            else if (item.Equipped)
                reason = "Equipped";
            else if (item.Locked)
                reason = "Locked";
            else if (item.Enchanted)
                reason = "Enchanted";
            else if (!all && (existing == null || existing.Contains(item.Id)))
                reason = "Existing";
            else if (item.OtherProtected)
                reason = "OtherProtection";
            else if (item.SourceProtected && (KeepMerchant || KeepQuest))
                reason = "SourceProtection";
            else if (KeepUpgrades && !items.Any(i => i.Known && i.Equipped && i.Category == item.Category && i.SoulStorm == item.SoulStorm))
                reason = "UpgradeUnknown";
            else if (KeepUpgrades && item.Power > items.Where(i => i.Known && i.Equipped && i.Category == item.Category && i.SoulStorm == item.SoulStorm).Max(i => i.Power))
                reason = "Upgrade";
            else
            {
                // 整个背包共同决定保留哪一份；自动模式不删除旧有副本。
                var copies = items.Where(i => i.Known && i.Category == item.Category && i.Type == item.Type && i.SoulStorm == item.SoulStorm).ToList();
                var best = copies.OrderByDescending(i => i.Rarity).ThenByDescending(i => i.Power).ThenByDescending(i => i.Equipped || i.Locked || i.Enchanted).ThenByDescending(i => existing != null && existing.Contains(i.Id)).ThenBy(i => i.Id, StringComparer.Ordinal).First();
                bool duplicate = Duplicates && copies.Count > 1 && best.Id != item.Id;
                var limit = (item.SoulStorm ? Storm : Normal)[(int)item.Category];
                if (duplicate)
                {
                    sell = true;
                    reason = "Duplicate";
                }
                else if (Duplicates && copies.Count > 1)
                {
                    reason = "BestDuplicate";
                }
                else if (limit != EquipmentRarity.Off && item.Rarity <= limit)
                {
                    sell = true;
                    reason = "Rarity";
                }
                else
                    reason = item.SoulStorm ? "StormKept" : "RarityKept";
            }

            result.Add(new EquipmentDecision { Item = item, Sell = sell, Reason = reason });
        }

        return result;
    }
}

// EquipmentDecision 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
sealed class EquipmentDecision
{
    public EquipmentItem Item;
    public bool Sell;
    public string Reason;
}

// InventoryBaseline 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
sealed class InventoryBaseline
{
    string session;
    HashSet<string> ids;
    // 启用整理时记录的已有 UID 集合，用于区分后续新物品。
    public ISet<string> Existing
    {
        get
        {
            return ids;
        }
    }

    // 记录启用时当前关卡/角色与已有 UID 基线。
    public void Capture(string currentSession, IEnumerable<EquipmentItem> items)
    {
        var list = items.Where(i => i.Category != EquipmentCategory.Unsupported).ToList();
        if (list.Count == 0 || list.Any(i => !i.Known || String.IsNullOrEmpty(i.Id)) || list.Select(i => i.Id).Distinct().Count() != list.Count)
            throw new Exception("Inventory baseline is incomplete");
        session = currentSession;
        ids = new HashSet<string>(list.Select(i => i.Id));
    }

    // 验证当前身份、序号或基线是否仍属于同一次操作。
    public bool Matches(string currentSession)
    {
        return ids != null && session == currentSession;
    }

    // 清除原基线，换角色或场景后必须重新采集。
    public void Reset()
    {
        session = null;
        ids = null;
    }
}

// EquipmentChecks 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
static class EquipmentChecks
{
    // 校验调用前提，失败立即终止当前操作。
    static void Require(bool value, string name)
    {
        if (!value)
            throw new Exception(name);
    }

    // 创建离线装备样例，用于整理规则测试。
    static EquipmentItem Item(string id, int power, EquipmentRarity rarity = EquipmentRarity.Common)
    {
        return new EquipmentItem
        {
            Id = id,
            Type = "SW.Item.Sword",
            Category = EquipmentCategory.Melee,
            Power = power,
            Rarity = rarity,
            Known = true
        };
    }

    // 核对合成计划中的出售状态，不操作真实物品。
    static bool Sold(EquipmentPolicy p, IEnumerable<EquipmentItem> items, string id, ISet<string> old = null, bool all = true)
    {
        return p.Plan(items, old, all).Single(i => i.Item.Id == id).Sell;
    }

    // 运行本模块的离线规则自检；返回 PASS 摘要，失败抛出异常供命令行报告。
    public static string Test()
    {
        var p = new EquipmentPolicy();
        var equipped = Item("equipped", 100);
        equipped.Equipped = true;
        var gear = Item("new", 50);
        var rows = new[]
        {
            equipped,
            gear
        };
        Require(Sold(p, rows, "new"), "Common default filter");
        Require(!Sold(p, rows, "equipped"), "Equipped protection");
        gear.Locked = true;
        Require(!Sold(p, rows, "new"), "Favourite lock protection");
        gear.Locked = false;
        gear.Enchanted = true;
        Require(!Sold(p, rows, "new"), "Enchantment protection");
        gear.Enchanted = false;
        gear.Known = false;
        Require(!Sold(p, rows, "new"), "Unknown metadata protection");
        gear.Known = true;
        gear.Power = 101;
        Require(!Sold(p, rows, "new"), "Upgrade protection");
        gear.Power = 50;
        gear.SourceProtected = true;
        Require(!Sold(p, rows, "new"), "Merchant and quest origin protection");
        gear.SourceProtected = false;
        gear.OtherProtected = true;
        Require(!Sold(p, rows, "new"), "Unknown dynamic tags protection");
        gear.OtherProtected = false;
        Require(!Sold(p, rows, "new", new HashSet<string> { "new" }, false), "Existing inventory untouched");
        Require(!Sold(p, rows, "new", null, false), "Missing baseline cannot sell");
        Require(Sold(p, rows, "new", new HashSet<string> { "equipped" }, false), "New pickup matching rules");
        p.KeepUpgrades = false;
        gear.SoulStorm = true;
        Require(!Sold(p, rows, "new"), "Storm default kept");
        p.Storm[0] = EquipmentRarity.Common;
        Require(Sold(p, rows, "new"), "Separate storm threshold");
        gear.SoulStorm = false;
        p.Normal[0] = EquipmentRarity.Off;
        p.Duplicates = true;
        gear.Rarity = EquipmentRarity.Rare;
        Require(!Sold(p, rows, "new"), "Best rarity wins duplicate comparison");
        equipped.Rarity = EquipmentRarity.Unique;
        Require(Sold(p, rows, "new"), "Duplicate independent of rarity threshold");
        var old = Item("old", 100, EquipmentRarity.Rare);
        var better = Item("better", 120, EquipmentRarity.Rare);
        var baseline = new HashSet<string>
        {
            "old"
        };
        Require(!Sold(p, new[] { old, better }, "old", baseline, false) && !Sold(p, new[] { old, better }, "better", baseline, false), "Better new duplicate never automatically sells old copy");
        Require(Sold(p, new[] { old, better }, "old", baseline, true) && !Sold(p, new[] { old, better }, "better", baseline, true), "Manual plan keeps best duplicate");
        better.Power = 100;
        Require(!Sold(p, new[] { old, better }, "old", baseline, true) && Sold(p, new[] { old, better }, "better", baseline, true), "Tie keeps old copy");
        better.SoulStorm = true;
        Require(!Sold(p, new[] { old, better }, "better", baseline, true), "Storm duplicate grouping separate");
        var duplicate = Item("old", 40);
        Require(p.Plan(new[] { old, duplicate }, baseline, true).All(i => !i.Sell), "Duplicate identity blocks all sales");
        var tracker = new InventoryBaseline();
        tracker.Capture("level1", new[] { old });
        Require(tracker.Matches("level1") && !tracker.Matches("level2"), "World baseline isolation");
        tracker.Reset();
        Require(!tracker.Matches("level1"), "Disconnect clears baseline");
        tracker.Capture("gear", new[] { old, new EquipmentItem() });
        Require(tracker.Matches("gear") && tracker.Existing.Count == 1, "Unsupported inventory records do not poison equipment baseline");
        old.Known = false;
        bool incomplete = false;
        try
        {
            tracker.Capture("invalid", new[] { old });
        }
        catch
        {
            incomplete = true;
        }

        Require(incomplete && !tracker.Matches("invalid"), "Unknown supported gear blocks incomplete baseline");
        old.Known = true;
        Require(new EquipmentPolicy().ToggleKey == 0x76 && GameActionBindings.KeyboardCode("Enter") == 13 && GameActionBindings.KeyboardCode("Tab") == 9, "Default F7 and game menu binding decoding");
        Require(EquipmentItem.CategoryFor("SW.ItemSlot.Inventory.VillageMerchant.Tier0") == EquipmentCategory.Unsupported && EquipmentItem.CategoryFor("SW.ItemSlot.Inventory.Cosmetic.Pets") == EquipmentCategory.Unsupported, "Merchant stock and cosmetics excluded");
        return "PASS: equipment policy defaults, equipped/locked/enchanted/unknown protections, source and upgrade protection, existing/new distinction, independent storm thresholds, duplicates by rarity then power, ties, better new items, manual preview, identity and session guards. No game input sent.";
    }

    // 读取背包捕获报告并生成纯预览，不调用回收组件。
    public static void PreviewCapture(string source, string destination)
    {
        var json = new JavaScriptSerializer
        {
            MaxJsonLength = Int32.MaxValue
        };
        var data = (Dictionary<string, object>)json.DeserializeObject(File.ReadAllText(source, Encoding.UTF8));
        var items = ((object[])data["items"]).Cast<Dictionary<string, object>>().Select(EquipmentItem.Decode).ToList();
        var plan = new EquipmentPolicy().Plan(items, null, true);
        File.WriteAllText(destination, json.Serialize(new { status = "Preview only; no items sold", itemCount = items.Count, equipmentCount = items.Count(i => i.Category != EquipmentCategory.Unsupported), knownEquipment = items.Count(i => i.Known && i.Category != EquipmentCategory.Unsupported), eligible = plan.Count(i => i.Sell), decisions = plan }), new UTF8Encoding(true));
    }
}

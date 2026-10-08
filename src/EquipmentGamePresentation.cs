// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 装备译名、图标、品质纹理展示。
// 游戏译文与图像继续属于原权利人。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;

static class EquipmentGamePresentation
{
    static Dictionary<string, object> items, terms;
    static readonly Dictionary<string, byte[]> icons = new Dictionary<string, byte[]>();
    static readonly Dictionary<string, Bitmap> thumbnails = new Dictionary<string, Bitmap>();
    // 原始 UI 纹理保留尺寸，不能用装备 64×64 缩略图替代品质标记。
    static readonly Dictionary<string, Bitmap> textures = new Dictionary<string, Bitmap>();
    static Dictionary<string, string> uiTextures = new Dictionary<string, string>();
    static readonly string[] categories =
    {
        "Equipment_Melee",
        "Equipment_Ranged",
        "Equipment_Helmet",
        "Equipment_Chest",
        "Equipment_Leggings",
        "Equipment_Boots",
        "Equipment_Artifact"
    };
    static EquipmentGamePresentation()
    {
        items = new Dictionary<string, object>();
        terms = InterfaceTerms();
        if (File.Exists(GameResources.CachePath))
            try
            {
                using (var stream = File.OpenRead(GameResources.CachePath)) Apply(GameResourceCatalog.Read(stream));
                return;
            }
            catch (Exception error) { ToolboxLog.Error("Resources.Cache", error); }
        try
        {
            using (var source = PresentationSource())
                if (source != null) Apply(GameResourceCatalog.Read(source));
        }
        catch (Exception error) { ToolboxLog.Error("Resources.Local", error); }
    }

    internal static string Revision = "";
    // 只在 UI 线程切换已完整校验的展示数据，释放旧缩略图；UID、回收规则、词条目录均不受影响。
    internal static void Apply(GameResourceCatalog catalog)
    {
        foreach (var image in thumbnails.Values) image.Dispose();
        thumbnails.Clear();
        foreach (var texture in textures.Values) texture.Dispose();
        textures.Clear();
        uiTextures = catalog.UiTextures;
        items = catalog.Items;
        var merged = InterfaceTerms();
        foreach (var term in catalog.Terms) merged[term.Key] = term.Value;
        terms = merged;
        icons.Clear();
        foreach (var icon in catalog.Icons) icons.Add(icon.Key, icon.Value);
        Revision = catalog.Revision;
    }

    // 仅接受四张实际装备定义表的名称来源；SW.Item 还包含任务占位、披风、食物和 TNT，不能直接当作装备。
    // 目录只负责搜索和展示，不声称每件装备都可刷新；没有来源的旧包需更新资源后再参与搜索。
    static bool SearchableEquipment(string tag)
    {
        if (!tag.StartsWith("SW.Item.", StringComparison.Ordinal)) return false;
        var row = (Dictionary<string, object>)items[tag];
        object source;
        if (!row.TryGetValue("namespace", out source)) return false;
        switch (source as string)
        {
            case "Text/Release/DT_ItemDefinitionMelee.csv":
            case "Text/Release/DT_ItemDefinitionRanged.csv":
            case "Text/Release/DT_ItemDefinitionArmor.csv":
            case "Text/Release/DT_ItemDefinitionArtifact.csv": return true;
            default: return false;
        }
    }

    // 用六语言游戏名称或精确标签搜索真实装备来源；宠物、护符、附魔书等也不混入铁匠装备目录。
    internal static string[] SearchItems(string query)
    {
        query = (query ?? "").Trim();
        return items.Keys.Where(tag => SearchableEquipment(tag) &&
            (query.Length == 0 || tag.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
             ((object[])((Dictionary<string, object>)items[tag])["names"]).Cast<string>().Any(name => name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)))
            .OrderBy(tag => Name(tag, L10n.Language)).Take(100).ToArray();
    }

    // 这里只提供原始效果名称供保存目标，不声称该名称在当前装备候选池内。
    internal static string[] EffectNames()
    {
        return items.Keys.Where(tag => tag.StartsWith("SW.Effect.", StringComparison.Ordinal) &&
            ((Dictionary<string, object>)items[tag]).ContainsKey("namespace") &&
            (string)((Dictionary<string, object>)items[tag])["namespace"] == "Text/Release/DT_EffectDefinition.csv" &&
            HasGameName(tag, L10n.Language))
            .OrderBy(tag => Name(tag, L10n.Language)).ToArray();
    }

    // 图标类型标签可以是装备或效果；多图标层级仍保持原有不猜测约束。
    internal static Bitmap Icon(string tag) { return Icon(new EquipmentItem { Type = tag }); }

    // 只读已校验的图标数据；不存在时用边框颜色回退，不能伪造已下载纹理。
    internal static Bitmap Texture(string key)
    {
        string name;
        byte[] bytes;
        if (!uiTextures.TryGetValue(key, out name) || !icons.TryGetValue(name, out bytes)) return null;
        Bitmap texture;
        if (textures.TryGetValue(key, out texture)) return texture;
        using (var stream = new MemoryStream(bytes, false))
        using (var image = Image.FromStream(stream)) texture = new Bitmap(image);
        textures[key] = texture;
        return texture;
    }

    // 优先读取可选内嵌展示包，否则仅从 EXE 同目录读取本地图标包。
    static Stream PresentationSource()
    {
        var embedded = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("equipment-presentation.bin.gz");
        if (embedded != null)
            return embedded;
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "equipment-presentation.bin.gz");
        if (!File.Exists(path))
            return null;
        long size = new FileInfo(path).Length;
        if (size < 8 || size > 16777216)
            throw new Exception("Local equipment presentation size invalid");
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    // 构造无需拆包资源的六语言类别/品质/力量词条。
    static Dictionary<string, object> InterfaceTerms()
    {
        return new Dictionary<string, object>
        {
            // 原始 OnboardingLabels 的 namespace/key/sourceHash 在资源目录记录；离线首启仍可显示游戏原译名。
            { "Currency_Emerald", new object[] { "绿宝石", "Emeralds", "エメラルド", "에메랄드", "綠寶石", "綠寶石" } },
            { "Currency_SpringStone", new object[] { "回响碎片", "Echo Shards", "残響のかけら", "에코 조각", "回聲碎片", "回聲碎片" } },
            { "Currency_EnchantmentPoint", new object[] { "附魔点数", "Enchantment points", "エンチャントポイント", "마법 부여 포인트", "附魔點數", "附魔點數" } },
            {
                "Equipment_Melee",
                new object[]
                {
                    "近战武器",
                    "Melee weapon",
                    "近接武器",
                    "근접 무기",
                    "近戰武器",
                    "近戰武器"
                }
            },
            {
                "Equipment_Ranged",
                new object[]
                {
                    "远程武器",
                    "Ranged weapon",
                    "遠距離武器",
                    "원거리 무기",
                    "遠程武器",
                    "遠程武器"
                }
            },
            {
                "Equipment_Helmet",
                new object[]
                {
                    "头盔",
                    "Helmet",
                    "ヘルメット",
                    "투구",
                    "頭盔",
                    "頭盔"
                }
            },
            {
                "Equipment_Chest",
                new object[]
                {
                    "胸甲",
                    "Chest armor",
                    "胴防具",
                    "흉갑",
                    "胸甲",
                    "胸甲"
                }
            },
            {
                "Equipment_Leggings",
                new object[]
                {
                    "护腿",
                    "Leg armor",
                    "脚防具",
                    "각반",
                    "護腿",
                    "護腿"
                }
            },
            {
                "Equipment_Boots",
                new object[]
                {
                    "靴子",
                    "Boots",
                    "ブーツ",
                    "장화",
                    "靴子",
                    "靴子"
                }
            },
            {
                "Equipment_Artifact",
                new object[]
                {
                    "法器",
                    "Artifact",
                    "アーティファクト",
                    "유물",
                    "法器",
                    "法器"
                }
            },
            {
                "SW_Rarity_Common",
                new object[]
                {
                    "普通",
                    "Common",
                    "コモン",
                    "일반",
                    "普通",
                    "普通"
                }
            },
            {
                "SW_Rarity_Rare",
                new object[]
                {
                    "稀有",
                    "Rare",
                    "レア",
                    "희귀",
                    "稀有",
                    "稀有"
                }
            },
            {
                "SW_Rarity_Special",
                new object[]
                {
                    "特殊",
                    "Special",
                    "スペシャル",
                    "특수",
                    "特殊",
                    "特殊"
                }
            },
            {
                "SW_Rarity_Unique",
                new object[]
                {
                    "独特",
                    "Unique",
                    "ユニーク",
                    "고유",
                    "獨特",
                    "獨特"
                }
            },
            {
                "header_power",
                new object[]
                {
                    "战力",
                    "Power",
                    "パワー",
                    "위력",
                    "戰力",
                    "戰力"
                }
            }
        };
    }

    // 按语言索引读取词条，缺失时使用已定义回退。
    static string Localized(object value, int language)
    {
        var rows = value as object[];
        if (rows == null || rows.Length != 6 || language < 0 || language > 5)
            throw new Exception("Equipment language resource invalid");
        return (string)rows[language];
    }

    // 取得指定展示词条，缺失时按参数使用稳定回退。
    public static string Term(string key, int language)
    {
        object value;
        return terms.TryGetValue(key, out value) ? Localized(value, language) : key;
    }

    // 取得指定展示词条，缺失时按参数使用稳定回退。
    public static string Term(string key)
    {
        return Term(key, L10n.Language);
    }

    // 将装备类别转换为当前语言显示名称。
    public static string Category(EquipmentCategory category)
    {
        return category >= EquipmentCategory.Melee && category <= EquipmentCategory.Artifact ? Term(categories[(int)category]) : "?";
    }

    // 将品质枚举转换为当前语言显示名称。
    public static string Rarity(EquipmentRarity rarity)
    {
        return rarity >= EquipmentRarity.Common && rarity <= EquipmentRarity.Unique ? Term("SW_Rarity_" + rarity) : L10n.T("关闭");
    }

    public static string Name(string tag, int language)
    {
        object value;
        if (tag != null && items.TryGetValue(tag, out value))
        {
            var row = (Dictionary<string, object>)value;
            string name = Localized(row["names"], language).Trim();
            if (!DisplayName(row, name)) return tag;
            return name;
        }
        if (tag != null && NearbyLootCatalog.Contains(tag))
        {
            string name = tag.Substring(tag.LastIndexOf('.') + 1);
            return Regex.Replace(name, @"(?<=[a-z0-9])(?=[A-Z])", " ");
        }

        return tag ?? "?";
    }

    // 过滤语言表中的 DropChance_TalismanEffect 等占位名。
    // 它与本地化 key 不同，不能只用“等于 key”识别；原始资源保持原样，本体拒绝将其当正式译名。
    static bool DisplayName(Dictionary<string, object> row, string name)
    {
        object key;
        return !String.IsNullOrWhiteSpace(name) &&
            !(row.TryGetValue("key", out key) && key is string &&
                (name == (string)key || name.EndsWith("," + (string)key, StringComparison.Ordinal))) &&
            !Regex.IsMatch(name, @"^(?:SW[._]|DT_|Text/Release/|LOCTABLE\s*\()") &&
            !(name.Contains("_") && Regex.IsMatch(name, @"^[A-Za-z][A-Za-z0-9_]*$"));
    }

    // 默认效果名称列表只列当前语言可用的原始译名；未知效果仍可按保存/实际读取的精确身份保留。
    internal static bool HasGameName(string tag, int language)
    {
        object value;
        if (tag == null || !items.TryGetValue(tag, out value)) return false;
        var row = (Dictionary<string, object>)value;
        return DisplayName(row, Localized(row["names"], language).Trim());
    }

    public static string Name(EquipmentItem item)
    {
        return Name(item.Type, L10n.Language);
    }

    public static Bitmap Icon(EquipmentItem item)
    {
        object value;
        if (item.Type == null || !items.TryGetValue(item.Type, out value))
            return null;
        var names = (object[])((Dictionary<string, object>)value)["icons"];
        // 层级法器图标需要单独验证层级，不能猜测。
        if (names.Length != 1)
            return null;
        string key = (string)names[0];
        Bitmap image;
        if (thumbnails.TryGetValue(key, out image))
            return image;
        byte[] bytes;
        if (!icons.TryGetValue(key, out bytes))
            return null;
        using (var stream = new MemoryStream(bytes, false))
        using (var original = Image.FromStream(stream))
        {
            image = new Bitmap(64, 64);
            using (var g = Graphics.FromImage(image))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                float scale = Math.Min(64f / original.Width, 64f / original.Height);
                int w = (int)(original.Width * scale), h = (int)(original.Height * scale);
                g.DrawImage(original, new Rectangle((64 - w) / 2, (64 - h) / 2, w, h));
            }
        }

        thumbnails[key] = image;
        return image;
    }

}

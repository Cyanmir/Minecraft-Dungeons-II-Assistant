// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 装备名称、分类、稀有度和可选本地图标展示。
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
    static readonly Dictionary<string, object> items, terms;
    static readonly Dictionary<string, byte[]> icons = new Dictionary<string, byte[]>();
    static readonly Dictionary<string, Bitmap> thumbnails = new Dictionary<string, Bitmap>();
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
        using (var source = PresentationSource())
        {
            if (source == null)
                return;
            using (var gz = new GZipStream(source, CompressionMode.Decompress))
            using (var r = new BinaryReader(gz, Encoding.UTF8))
            {
                if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "M2EQ")
                    throw new Exception("Invalid equipment resource");
                int size = r.ReadInt32();
                if (size < 1 || size > 4000000)
                    throw new Exception("Equipment manifest size invalid");
                byte[] raw = r.ReadBytes(size);
                if (raw.Length != size)
                    throw new EndOfStreamException();
                var data = (Dictionary<string, object>)new JavaScriptSerializer
                {
                    MaxJsonLength = 4000000
                }.DeserializeObject(Encoding.UTF8.GetString(raw));
                if ((string)data["format"] != "MCD2.EquipmentPresentation.v1")
                    throw new Exception("Equipment manifest version invalid");
                items = (Dictionary<string, object>)data["items"];
                terms = (Dictionary<string, object>)data["terms"];
                var hashes = (Dictionary<string, object>)data["iconSHA256"];
                int count = r.ReadInt32();
                if (count < 1 || count > 512)
                    throw new Exception("Equipment icon count invalid");
                for (int i = 0; i < count; i++)
                {
                    int length = r.ReadUInt16();
                    if (length < 1 || length > 128)
                        throw new Exception("Equipment icon name invalid");
                    string key = Encoding.UTF8.GetString(r.ReadBytes(length));
                    int bytes = r.ReadInt32();
                    if (bytes < 1 || bytes > 2000000)
                        throw new Exception("Equipment icon size invalid");
                    byte[] png = r.ReadBytes(bytes);
                    if (png.Length != bytes)
                        throw new EndOfStreamException();
                    using (var sha = System.Security.Cryptography.SHA256.Create())
                        if (BitConverter.ToString(sha.ComputeHash(png)).Replace("-", "").ToLowerInvariant() != (string)hashes[key])
                            throw new Exception("Equipment icon hash mismatch");
                    icons.Add(key, png);
                }

                if (r.Read() != -1)
                    throw new Exception("Trailing equipment resource data");
                    }
        }
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

    // 读取本地目录名称，缺失时由已核实类型标签生成名称，未知类型保留原标识。
    public static string Name(string tag, int language)
    {
        object value;
        if (tag != null && items.TryGetValue(tag, out value))
            return Localized(((Dictionary<string, object>)value)["names"], language);
        if (tag != null && NearbyLootCatalog.Contains(tag))
        {
            string name = tag.Substring(tag.LastIndexOf('.') + 1);
            return Regex.Replace(name, @"(?<=[a-z0-9])(?=[A-Z])", " ");
        }

        return tag ?? "?";
    }

    // 读取本地目录名称，缺失时由已核实类型标签生成名称，未知类型保留原标识。
    public static string Name(EquipmentItem item)
    {
        return Name(item.Type, L10n.Language);
    }

    // 返回本地包已有图标；公开构建无图标时返回空值供 UI 回退。
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

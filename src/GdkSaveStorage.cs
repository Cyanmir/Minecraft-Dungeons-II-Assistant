// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 只读解析 GDK WGS 索引、容器描述符与 blob 关联。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

// 只读发现 WGS 结构，不提供 blob 或云元数据写入入口；布局参考：https://github.com/Z1ni/XGP-save-extractor/blob/main/main.py 。
static class GdkSaveStorage
{
    const string Family = "Microsoft.MinecraftDungeons2_8wekyb3d8bbwe";
    internal static readonly string[] Slots =
    {
        "MCD2NearbyLootRequest",
        "MCD2NearbyLootReceipt",
        "MCD2CombatRequest",
        "MCD2CombatReceipt",
        "MCD2EquipmentRequest",
        "MCD2EquipmentReceipt"
    };
    // Entry 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
    internal sealed class Entry
    {
        internal string Name;
        internal Guid Folder;
        internal byte Revision;
    }

    // Index 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
    internal sealed class Index
    {
        internal uint Version;
        internal string Package;
        internal int Count;
        internal List<Entry> Entries = new List<Entry>();
    }

    // Blob 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
    internal sealed class Blob
    {
        internal string Name;
        internal Guid Cloud, Local;
    }

    // Budget 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。

    // 规范化私有槽标识，用于索引和描述符关联。
    internal static string Slot(string name)
    {
        return Slots.FirstOrDefault(s => String.Equals(name, s, StringComparison.Ordinal) || String.Equals(name, s + ".sav", StringComparison.Ordinal));
    }

    // 检查有界文本或标识是否精确匹配，拒绝模糊候选。
    static byte[] Exact(BinaryReader reader, int count)
    {
        byte[] bytes = reader.ReadBytes(count);
        if (bytes.Length != count)
            throw new EndOfStreamException("Truncated storage metadata");
        return bytes;
    }

    // 解码受长度限制的元数据字符串。
    static string Text(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > 2048)
            throw new Exception("Storage string length rejected");
        return new UnicodeEncoding(false, false, true).GetString(Exact(reader, count * 2)).TrimEnd('\0');
    }

    // 读取 WGS 索引条目，校验容器数量和字节边界。
    internal static Index ParseIndex(byte[] bytes)
    {
        if (bytes.Length < 32 || bytes.Length > 4 * 1024 * 1024)
            throw new Exception("Storage index size rejected");
        using (var stream = new MemoryStream(bytes))
        using (var reader = new BinaryReader(stream))
        {
            var result = new Index
            {
                Version = reader.ReadUInt32(),
                Count = reader.ReadInt32()
            };
            if (result.Count < 0 || result.Count > 10000)
                throw new Exception("Storage container count rejected");
            Text(reader);
            result.Package = Text(reader).Split('!')[0];
            if (result.Package != "Microsoft.MinecraftDungeons2" && result.Package != Family && !result.Package.StartsWith("Microsoft.MinecraftDungeons2_", StringComparison.Ordinal))
                throw new Exception("Storage index belongs to a different package");
            Exact(reader, 8);
            Exact(reader, 4);
            Text(reader);
            Exact(reader, 8);
            for (int i = 0; i < result.Count; i++)
            {
                string name = Text(reader);
                Text(reader);
                Text(reader);
                byte revision = reader.ReadByte();
                Exact(reader, 4);
                Guid guid = new Guid(Exact(reader, 16));
                Exact(reader, 8);
                Exact(reader, 16);
                if (guid == Guid.Empty)
                    throw new Exception("Empty storage container GUID");
                result.Entries.Add(new Entry { Name = name, Folder = guid, Revision = revision });
            }

            if (stream.Position != stream.Length)
                throw new Exception("Unrecognized trailing storage index metadata");
            return result;
        }
    }

    // 解析容器描述符中的 blob 关联，拒绝截断或歧义结构。
    internal static List<Blob> ParseDescriptor(byte[] bytes)
    {
        using (var stream = new MemoryStream(bytes))
        using (var reader = new BinaryReader(stream))
        {
            if (reader.ReadUInt32() != 4)
                throw new Exception("Unsupported storage descriptor header");
            int count = reader.ReadInt32();
            if (count < 0 || count > 128 || bytes.Length != 8 + count * 160)
                throw new Exception("Storage descriptor length rejected");
            var result = new List<Blob>();
            for (int i = 0; i < count; i++)
            {
                string name = new UnicodeEncoding(false, false, true).GetString(Exact(reader, 128)).TrimEnd('\0');
                if (name.Contains("\0"))
                    throw new Exception("Invalid storage blob name");
                result.Add(new Blob { Name = name, Cloud = new Guid(Exact(reader, 16)), Local = new Guid(Exact(reader, 16)) });
            }

            return result;
        }
    }

    // 核对 blob GUID 是否属于指定描述符条目。
    static bool Linked(string path)
    {
        return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    }

    // 验证解析后的路径仍在指定 WGS 根目录内。
    internal static void RequireContained(string path, string root)
    {
        string full = Path.GetFullPath(path), basePath = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Storage path escaped its package root");
        string current = full;
        for (int i = 0; i < 64 && current != null; i++, current = Path.GetDirectoryName(current))
        {
            if ((Directory.Exists(current) || File.Exists(current)) && Linked(current))
                throw new Exception("Linked storage paths are not followed");
            if (String.Equals(current, basePath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                return;
        }

        throw new Exception("Storage ancestor validation failed");
    }

    internal static string ResolveGuid(string parent, Guid id, string root, bool folder)
    {
        if (id == Guid.Empty)
            return null;
        var candidates = new[]
        {
            id.ToString("N").ToUpperInvariant(),
            id.ToString("N"),
            id.ToString("D"),
            id.ToString("D").ToUpperInvariant()
        };
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in candidates)
        {
            string path = Path.Combine(parent, name);
            if (folder ? Directory.Exists(path) : File.Exists(path))
            {
                RequireContained(path, root);
                found.Add(path);
            }
        }

        if (found.Count > 1)
            throw new Exception("Ambiguous storage GUID paths");
        return found.Count == 1 ? found.First() : null;
    }

}

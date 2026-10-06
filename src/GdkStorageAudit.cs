// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 中文维护说明：只读解析 GDK WGS 索引、容器描述符与 blob 关联。描述符可滚动更换，读取应稳定且有预算/范围限制；输出诊断只证明容器关联，不证明组件交互已成功。
// 源码导航和常改参数见 docs/maintenance.md；协议、反射标识及类型白名单修改前先核对两端。
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
// GdkStorageAudit 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
static class GdkStorageAudit
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
    sealed class Budget
    {
        internal int Descriptors;
        internal long MetadataBytes;
    }

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

    // 限次读取正在滚动的文件，避免混合两次版本的数据。
    static byte[] ReadStable(string path, int max, Func<string, byte[]> read)
    {
        var before = new FileInfo(path);
        long size = before.Length;
        DateTime touched = before.LastWriteTimeUtc;
        if (size < 0 || size > max)
            throw new Exception("Storage file size rejected");
        byte[] bytes = read(path);
        var after = new FileInfo(path);
        if (bytes.Length != size || after.Length != size || after.LastWriteTimeUtc != touched)
            throw new Exception("Storage changed during read; retry while standing still");
        return bytes;
    }

    // 计算文件 SHA256，供安装校验及备份归属检查。
    static string Hash(byte[] bytes)
    {
        using (var hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
    }

    // 将已验证 GUID 解析为当前容器中的文件关联。
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

    // 只读探测 WGS 容器结构，不发送组件请求。
    static Dictionary<string, object> Probe(string path, string slot, string storage, string locator, string root, string output, int sample, Func<string, byte[]> read)
    {
        var result = new Dictionary<string, object>
        {
            {
                "slot",
                slot
            },
            {
                "storage",
                storage
            },
            {
                "locator",
                locator
            },
            {
                "validated",
                false
            }
        };
        try
        {
            if (Slot(slot) == null)
                throw new Exception("Component slot not allowed");
            RequireContained(path, root);
            byte[] bytes = ReadStable(path, 65536, read);
            bool request = slot.EndsWith("Request", StringComparison.Ordinal);
            string mod = slot.StartsWith("MCD2NearbyLoot") ? "MCD2NearbyLootBridge" : slot.StartsWith("MCD2Combat") ? "MCD2CombatBridge" : "MCD2EquipmentBridge";
            string value = EquipmentSaveCodec.Parse(bytes, request ? "Request" : "Receipt", request ? "Command" : "Status", mod).Value;
            result["validated"] = true;
            result["bytes"] = bytes.Length;
            result["sha256"] = Hash(bytes);
            result["lastWriteUtc"] = File.GetLastWriteTimeUtc(path);
            result["value"] = value;
            result["component"] = mod;
            string relative = "component-slots/sample-" + sample + "/" + Guid.NewGuid().ToString("N").Substring(0, 8) + "-" + slot + ".sav";
            string destination = Path.Combine(output, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.WriteAllBytes(destination, bytes);
            result["copiedTo"] = relative;
        }
        catch (Exception e)
        {
            result["error"] = e.Message;
        }

        return result;
    }

    // 采集一个受预算限制的结构快照，保留不可读原因。
    internal static object Sample(string root, string output, int sample, Func<string, byte[]> read)
    {
        var records = new List<Dictionary<string, object>>();
        var profiles = new List<object>();
        var errors = new List<string>();
        var hints = new HashSet<string>();
        var budget = new Budget();
        foreach (string format in new[]
        {
            "wgs",
            "xgs"
        }

        )
        {
            string storage = Path.Combine(root, "SystemAppData", format);
            if (!Directory.Exists(storage))
                continue;
            try
            {
                RequireContained(storage, root);
                string[] users = Directory.GetDirectories(storage);
                if (users.Length > 64)
                    throw new Exception("Storage profile directory limit reached");
                int profileIndex = 0;
                foreach (string user in users.OrderBy(p => p, StringComparer.Ordinal))
                {
                    profileIndex++;
                    string label = format + "/profile-" + profileIndex;
                    if (Path.GetFileName(user) == "t" || Path.GetFileName(user).IndexOf("backup", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    try
                    {
                        RequireContained(user, root);
                        if (format == "xgs")
                        {
                            foreach (string slot in Slots)
                            {
                                foreach (string name in new[]
                                {
                                    slot,
                                    slot + ".sav"
                                }

                                )
                                {
                                    string candidate = Path.Combine(user, name);
                                    if (File.Exists(candidate))
                                        records.Add(Probe(candidate, slot, format, label + "/" + name, root, output, sample, read));
                                    if (!Directory.Exists(candidate))
                                        continue;
                                    RequireContained(candidate, root);
                                    string data = Path.Combine(candidate, "data");
                                    if (File.Exists(data))
                                        records.Add(Probe(data, slot, format, label + "/" + name + "/data", root, output, sample, read));
                                }
                            }

                            profiles.Add(new { profile = label, indexPresent = false });
                            continue;
                        }

                        string indexPath = Path.Combine(user, "containers.index");
                        if (!File.Exists(indexPath))
                            continue;
                        RequireContained(indexPath, root);
                        byte[] indexBytes = ReadStable(indexPath, 4 * 1024 * 1024, read);
                        budget.MetadataBytes += indexBytes.Length;
                        Index index = ParseIndex(indexBytes);
                        int matched = 0, failed = 0, containerIndex = 0;
                        foreach (Entry entry in index.Entries.OrderBy(e => Slot(e.Name) == null ? 1 : 0))
                        {
                            containerIndex++;
                            if (entry.Name.IndexOf("MCD2", StringComparison.OrdinalIgnoreCase) >= 0)
                                hints.Add(entry.Name);
                            if (budget.Descriptors >= 512 || budget.MetadataBytes > 16 * 1024 * 1024)
                                throw new Exception("Storage metadata scan limit reached");
                            try
                            {
                                string directory = ResolveGuid(user, entry.Folder, root, true);
                                if (directory == null)
                                {
                                    failed++;
                                    continue;
                                }

                                string descriptor = Path.Combine(directory, "container." + entry.Revision);
                                if (!File.Exists(descriptor))
                                {
                                    failed++;
                                    continue;
                                }

                                RequireContained(descriptor, root);
                                byte[] metadata = ReadStable(descriptor, 8 + 128 * 160, read);
                                budget.Descriptors++;
                                budget.MetadataBytes += metadata.Length;
                                var blobs = ParseDescriptor(metadata);
                                string containerSlot = Slot(entry.Name);
                                foreach (Blob blob in blobs)
                                {
                                    if (blob.Name.IndexOf("MCD2", StringComparison.OrdinalIgnoreCase) >= 0)
                                        hints.Add(blob.Name);
                                    string slot = Slot(blob.Name);
                                    if (slot == null && containerSlot != null && (blobs.Count == 1 || blob.Name == "Data" || blob.Name == "data"))
                                        slot = containerSlot;
                                    if (slot == null)
                                        continue;
                                    matched++;
                                    string local = ResolveGuid(directory, blob.Local, root, false), cloud = ResolveGuid(directory, blob.Cloud, root, false);
                                    if (local != null && cloud != null && !String.Equals(local, cloud, StringComparison.OrdinalIgnoreCase))
                                        throw new Exception("Ambiguous local/cloud blob; no payload read");
                                    string payload = local ?? cloud;
                                    if (payload == null)
                                        throw new Exception("Component blob missing; no payload read");
                                    records.Add(Probe(payload, slot, format, label + "/container-" + containerIndex + "/" + blob.Name, root, output, sample, read));
                                }
                            }
                            catch (Exception e)
                            {
                                failed++;
                                if (errors.Count < 12)
                                    errors.Add(label + ": " + e.Message);
                            }
                        }

                        profiles.Add(new { profile = label, indexPresent = true, indexVersion = index.Version, containerCount = index.Count, matchedComponentEntries = matched, descriptorErrors = failed, indexSHA256 = Hash(indexBytes) });
                    }
                    catch (Exception e)
                    {
                        if (errors.Count < 12)
                            errors.Add(label + ": " + e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                if (errors.Count < 12)
                    errors.Add(format + ": " + e.Message);
            }
        }

        return new
        {
            sample = sample,
            utc = DateTime.UtcNow,
            profiles = profiles,
            componentEntries = records,
            mcd2NameHints = hints.Take(32).ToArray(),
            metadataDescriptorsRead = budget.Descriptors,
            metadataBytesRead = budget.MetadataBytes,
            errors = errors
        };
    }

    // 生成只读存储报告，报告不自动上传。
    public static void Run(string report)
    {
        string output = Path.GetDirectoryName(Path.GetFullPath(report));
        Directory.CreateDirectory(output);
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", Family);
        var samples = new List<object>();
        samples.Add(Sample(root, output, 1, File.ReadAllBytes));
        Thread.Sleep(1200);
        samples.Add(Sample(root, output, 2, File.ReadAllBytes));
        var processes = Process.GetProcessesByName("Dungeons-WinGDK-Shipping");
        var ids = processes.Select(p => p.Id).ToArray();
        foreach (var process in processes)
            process.Dispose();
        var data = new
        {
            schema = 1,
            version = "1.0.0",
            readOnly = true,
            gameInput = false,
            gameWrites = false,
            saveWrites = false,
            githubUpload = false,
            packageFamily = Family,
            packageStorageExists = Directory.Exists(root),
            gamePids = ids,
            scope = "Reads storage index/descriptor metadata. Payloads only for six exact MCD2 slots; exports only after exact Unreal component class/property validation. No character payloads, cloud/account IDs or raw indexes exported. No write-channel or gameplay compatibility claim.",
            samples = samples
        };
        string json = new JavaScriptSerializer
        {
            MaxJsonLength = Int32.MaxValue
        }.Serialize(data);
        json = json.Replace(root.Replace("\\", "\\\\"), "%PACKAGE_STORAGE%");
        File.WriteAllText(report, json, new UTF8Encoding(true));
    }
}

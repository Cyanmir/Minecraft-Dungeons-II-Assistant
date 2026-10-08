// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 独立图标/译名仓库的有界下载、哈希校验与原子缓存替换。
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

// 先解析完整的新目录再交给 UI 切换；不可让损坏下载改变正在显示的目录。
sealed class GameResourceCatalog
{
    public Dictionary<string, object> Items, Terms;
    public Dictionary<string, byte[]> Icons = new Dictionary<string, byte[]>();
    // 旧个人包没有此字段；新包仅允许有界的展示纹理名和已验证图标引用。
    public Dictionary<string, string> UiTextures = new Dictionary<string, string>();
    public string Revision;
    // 名称六列对应工具语言索引；缺失译文回退由资源构建器明确记录，不在客户端编造翻译。
    static void Names(object value)
    {
        var names = value as object[];
        if (names == null || names.Length != 6 || names.Any(x => !(x is string) || String.IsNullOrWhiteSpace((string)x) || ((string)x).Length > 4096))
            throw new InvalidDataException("Invalid resource localization");
    }
    // M2EQ 沿用已有个人展示包格式，增加 revision 但不改变旧包兼容性；最多解压 64 MiB。
    public static GameResourceCatalog Read(Stream source)
    {
        var result = new GameResourceCatalog();
        using (var gzip = new GZipStream(source, CompressionMode.Decompress))
        using (var reader = new BinaryReader(gzip, Encoding.UTF8))
        {
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "M2EQ") throw new InvalidDataException("Invalid resource header");
            int length = reader.ReadInt32();
            if (length < 1 || length > 4000000) throw new InvalidDataException("Invalid resource manifest size");
            byte[] json = reader.ReadBytes(length);
            if (json.Length != length) throw new EndOfStreamException();
            var data = new JavaScriptSerializer { MaxJsonLength = 4000000, RecursionLimit = 32 }.DeserializeObject(Encoding.UTF8.GetString(json)) as Dictionary<string, object>;
            if (data == null || (string)data["format"] != "MCD2.EquipmentPresentation.v1") throw new InvalidDataException("Unsupported resource format");
            result.Revision = data.ContainsKey("revision") ? (string)data["revision"] : "local";
            result.Items = (Dictionary<string, object>)data["items"];
            result.Terms = (Dictionary<string, object>)data["terms"];
            var hashes = (Dictionary<string, object>)data["iconSHA256"];
            if (data.ContainsKey("uiTextures"))
            {
                var textures = data["uiTextures"] as Dictionary<string, object>;
                if (textures == null || textures.Count > 16) throw new InvalidDataException("Invalid UI texture map");
                foreach (var texture in textures)
                {
                    var name = texture.Value as string;
                    if (!Regex.IsMatch(texture.Key, @"^[A-Za-z]{1,40}$") || name == null || !hashes.ContainsKey(name)) throw new InvalidDataException("Invalid UI texture reference");
                    result.UiTextures.Add(texture.Key, name);
                }
            }
            if (result.Items.Count > 4096 || result.Terms.Count > 512 || hashes.Count > 2048) throw new InvalidDataException("Resource entry limit");
            foreach (var term in result.Terms.Values) Names(term);
            foreach (var item in result.Items)
            {
                if (!Regex.IsMatch(item.Key, @"^SW\.(Item|Effect|Enchantment)\.[A-Za-z0-9_.]+$")) throw new InvalidDataException("Invalid resource type");
                var row = item.Value as Dictionary<string, object>;
                if (row == null) throw new InvalidDataException("Invalid resource row");
                Names(row["names"]);
                var icons = row["icons"] as object[];
                if (icons == null || icons.Length > 16 || icons.Any(x => !(x is string) || !hashes.ContainsKey((string)x))) throw new InvalidDataException("Invalid resource icon reference");
            }
            int count = reader.ReadInt32();
            if (count < 1 || count != hashes.Count || count > 2048) throw new InvalidDataException("Resource icon count mismatch");
            long total = length;
            for (int i = 0; i < count; i++)
            {
                int nameLength = reader.ReadUInt16();
                if (nameLength < 1 || nameLength > 128) throw new InvalidDataException("Invalid resource filename");
                string name = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
                if (!Regex.IsMatch(name, @"^[a-z0-9_-]+\.png$")) throw new InvalidDataException("Invalid icon filename");
                int size = reader.ReadInt32();
                total += size;
                if (size < 8 || size > 2000000 || total > 64L * 1024 * 1024) throw new InvalidDataException("Resource expansion limit");
                byte[] png = reader.ReadBytes(size);
                if (png.Length != size || !png.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) || !hashes.ContainsKey(name) || GameResources.Hash(png) != (string)hashes[name]) throw new InvalidDataException("Icon integrity mismatch");
                // 解码前按 PNG IHDR 限制尺寸，防止超大图像占用 UI 内存；构建器输出原有 UI 图标。
                if (size < 24 || Encoding.ASCII.GetString(png, 12, 4) != "IHDR" || Width(png, 16) < 1 || Width(png, 16) > 4096 || Width(png, 20) < 1 || Width(png, 20) > 4096) throw new InvalidDataException("Icon dimensions rejected");
                using (var memory = new MemoryStream(png, false))
                using (var image = System.Drawing.Image.FromStream(memory, false, true)) { }
                result.Icons.Add(name, png);
            }
            if (reader.Read() != -1) throw new InvalidDataException("Unexpected resource suffix");
        }
        return result;
    }
    // PNG 的宽高以网络字节序保存，不能用本机 Little Endian 解码。
    static int Width(byte[] png, int at) { return (png[at] << 24) | (png[at + 1] << 16) | (png[at + 2] << 8) | png[at + 3]; }
}

static class GameResources
{
    internal const string Repository = "Cyanmir/Minecraft-Dungeons-II-Assistant-Resources";
    const string RootUrl = "https://raw.githubusercontent.com/" + Repository + "/main/";
    internal static string CachePath { get { return Path.Combine(Path.GetDirectoryName(ToolboxSettings.ConfigPath), "resources", "equipment-presentation.bin.gz"); } }
    internal static string Hash(byte[] bytes)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
    // 只从固定公开仓库下载；远端不能指定执行代码、任意 URL 或落盘路径。
    static byte[] Download(string relative, int maximum, CancellationToken token)
    {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        var request = (HttpWebRequest)WebRequest.Create(RootUrl + relative);
        request.UserAgent = "MCD2A-Resources/1";
        request.Timeout = request.ReadWriteTimeout = 15000;
        request.AllowAutoRedirect = false;
        request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
        using (token.Register(request.Abort))
        using (var response = (HttpWebResponse)request.GetResponse())
        using (var input = response.GetResponseStream())
        using (var output = new MemoryStream())
        {
            if (response.StatusCode != HttpStatusCode.OK || response.ContentLength > maximum) throw new InvalidDataException("Resource download rejected");
            var buffer = new byte[16384];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
            {
                token.ThrowIfCancellationRequested();
                if (output.Length + read > maximum) throw new InvalidDataException("Resource download limit");
                output.Write(buffer, 0, read);
            }
            return output.ToArray();
        }
    }
    // 返回校验过的新目录；安装失败时旧文件保留。资源更新不会更改 EXE 或游戏组件。
    internal static GameResourceCatalog Update(CancellationToken token)
    {
        var info = new JavaScriptSerializer { MaxJsonLength = 65536 }.DeserializeObject(Encoding.UTF8.GetString(Download("manifest.json", 65536, token))) as Dictionary<string, object>;
        if (info == null || (string)info["format"] != "MCD2A.Resources.v1") throw new InvalidDataException("Unsupported resource update manifest");
        string hash = (string)info["sha256"], revision = (string)info["revision"];
        long size = Convert.ToInt64(info["bytes"]);
        if (!Regex.IsMatch(hash, @"^[0-9a-f]{64}$") || !Regex.IsMatch(revision, @"^[0-9.]{1,40}$") || size < 1 || size > 16777216 || (string)info["package"] != "packages/" + hash + ".bin.gz") throw new InvalidDataException("Invalid resource update manifest");
        byte[] bytes = null;
        if (File.Exists(CachePath) && new FileInfo(CachePath).Length == size)
        {
            var cached = File.ReadAllBytes(CachePath);
            if (Hash(cached) == hash) bytes = cached;
        }
        if (bytes == null) bytes = Download("packages/" + hash + ".bin.gz", 16777216, token);
        if (bytes.LongLength != size || Hash(bytes) != hash) throw new InvalidDataException("Resource package integrity mismatch");
        GameResourceCatalog catalog;
        using (var stream = new MemoryStream(bytes, false)) catalog = GameResourceCatalog.Read(stream);
        if (catalog.Revision != revision) throw new InvalidDataException("Resource revision mismatch");
        token.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(CachePath));
        string temp = CachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temp, bytes);
            token.ThrowIfCancellationRequested();
            if (File.Exists(CachePath)) File.Replace(temp, CachePath, null);
            else File.Move(temp, CachePath);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return catalog;
    }
}

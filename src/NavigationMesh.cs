// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 只读读取地面多边形、连通性并提供几何规划算法。
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Globalization;
using System.Web.Script.Serialization;

sealed class NavigationPolygon
{
    public int Id, Tile;
    public ThreatVector[] Vertices;
    public ThreatVector Center;
    [ScriptIgnore]
    public Dictionary<int, ThreatVector> Portals = new Dictionary<int, ThreatVector>();
    // 此地面多边形的连通邻居集合，只用于几何规划。
    public Dictionary<string, ThreatVector> Connections
    {
        get
        {
            return Portals.ToDictionary(p => p.Key.ToString(CultureInfo.InvariantCulture), p => p.Value);
        }
    }
}

// NavigationCapture 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
sealed class NavigationCapture
{
    public string Session;
    public ThreatVector Player;
    public int LoadedTiles;
    public Dictionary<string, int> AreaFlags = new Dictionary<string, int>();
    [ScriptIgnore]
    public Dictionary<int, string> AllowedAreas = new Dictionary<int, string>();
    // 当前任务/地面快照的原生区域名称，用于诊断归属。
    public Dictionary<string, string> GroundAreaNames
    {
        get
        {
            return AllowedAreas.ToDictionary(p => p.Key.ToString(CultureInfo.InvariantCulture), p => p.Value);
        }
    }

    public List<NavigationPolygon> Polygons = new List<NavigationPolygon>();
}

// 只读地面图算法；结果不直接控制角色，也不启用自动寻路。
static class NavigationPlanner
{
    // 把地面坐标生成可比较索引，按实现保留量化精度。
    static string Key(ThreatVector p)
    {
        return Math.Round(p.X, 1).ToString("R", CultureInfo.InvariantCulture) + "," + Math.Round(p.Y, 1).ToString("R", CultureInfo.InvariantCulture) + "," + Math.Round(p.Z, 1).ToString("R", CultureInfo.InvariantCulture);
    }

    // 建立共享边的地面多边形连通关系。
    public static void Connect(NavigationCapture mesh)
    {
        var edges = new Dictionary<string, List<KeyValuePair<NavigationPolygon, ThreatVector>>>();
        foreach (var poly in mesh.Polygons)
        {
            poly.Portals.Clear();
            poly.Center = new ThreatVector();
            foreach (var v in poly.Vertices)
                poly.Center = poly.Center + v;
            poly.Center = poly.Center * (1.0 / poly.Vertices.Length);
            for (int i = 0; i < poly.Vertices.Length; i++)
            {
                ThreatVector a = poly.Vertices[i], b = poly.Vertices[(i + 1) % poly.Vertices.Length];
                string ka = Key(a), kb = Key(b), key = String.CompareOrdinal(ka, kb) < 0 ? ka + "/" + kb : kb + "/" + ka;
                List<KeyValuePair<NavigationPolygon, ThreatVector>> pairs;
                if (!edges.TryGetValue(key, out pairs))
                    edges[key] = pairs = new List<KeyValuePair<NavigationPolygon, ThreatVector>>();
                pairs.Add(new KeyValuePair<NavigationPolygon, ThreatVector>(poly, (a + b) * .5));
            }
        }

        foreach (var entries in edges.Values)
        {
            if (entries.Count != 2)
                continue;
            var a = entries[0];
            var b = entries[1];
            if (a.Key.Id == b.Key.Id)
                continue;
            a.Key.Portals[b.Key.Id] = a.Value;
            b.Key.Portals[a.Key.Id] = b.Value;
        }
    }

    // 查找包含给定世界点的已读取地面多边形。
    public static NavigationPolygon Locate(NavigationCapture mesh, ThreatVector point)
    {
        if (!point.Valid)
            return null;
        NavigationPolygon best = null;
        double distance = Double.MaxValue;
        foreach (var p in mesh.Polygons)
        {
            bool inside = false;
            var v = p.Vertices;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
                if ((v[i].Y > point.Y) != (v[j].Y > point.Y) && point.X < (v[j].X - v[i].X) * (point.Y - v[i].Y) / (v[j].Y - v[i].Y) + v[i].X)
                    inside = !inside;
            double height = Math.Abs(point.Z - p.Center.Z);
            if (inside && height <= 250 && height < distance)
            {
                best = p;
                distance = height;
            }
        }

        return best;
    }

}

// 跨文件的只读游戏读取器；各 partial 文件共同持有同一连接/对象身份缓存。
sealed partial class HealthReader
{
    // 读取当前玩家附近已加载地面区域。
    Dictionary<int, string> ReadPlayerGroundAreas(Identity nav)
    {
        long st = Struct("SupportedAreaData", 32), h = nav.Address + Offset(nav, "SupportedAreas", 16), data = M.Q(h);
        int n = M.I(h + 8), cap = M.I(h + 12);
        if (n < 1 || n > 64 || cap < n || cap > 128)
            throw new Exception("Navigation area table invalid");
        int idOffset = Prop(st, "AreaID", 4).Offset, classOffset = Prop(st, "AreaClass", 8).Offset;
        var ids = new HashSet<int>();
        var allowed = new Dictionary<int, string>();
        for (int i = 0; i < n; i++)
        {
            int id = M.I(data + i * 32 + idOffset);
            Identity cl = Token(M.Q(data + i * 32 + classOffset));
            string name = Name(cl.Name);
            if (id < 0 || id > 63 || !ids.Add(id) || !IsA(cl.Class, "Class"))
                throw new Exception("Navigation area identity invalid");
            AdaptationRecord.Set("navigation.area." + id, name);
            if (name == "NavArea_Default" || name == "NavArea_PlayerOnly")
                allowed[id] = name;
        }

        if (allowed.Count < 1 || M.Q(h) != data || M.I(h + 8) != n || !Valid(nav))
            throw new Exception("Navigation area table changed");
        return allowed;
    }

    // 按实际反射函数名查找可验证原生入口。
    long NamedFunction(string owner, string name)
    {
        foreach (var t in AllTokens())
            try
            {
                if (ClassName(t.Class) == "Function" && Name(t.Name) == name && Name(Token(M.Q(t.Address + 32)).Name) == owner)
                    return t.Address;
            }
            catch
            {
            }

        throw new Exception("Named function unavailable: " + owner + "::" + name);
    }

    // 核对反射包装函数与目标调用的关系。
    long NativeWrapper(long function, int size)
    {
        var list = new HashSet<long>();
        for (int off = 120; off <= 240; off += 8)
            try
            {
                long p = M.Q(function + off);
                if (executable(p, size))
                    list.Add(p);
            }
            catch
            {
            }

        if (list.Count != 1)
            throw new Exception("Native function pointer is ambiguous");
        return list.First();
    }

    // 解析限定代码段中的相对调用目标。
    long RelativeCall(long address, byte[] bytes, int offset)
    {
        if (bytes[offset] != 0xe8)
            throw new Exception("Expected native call");
        long result = address + offset + 5 + BitConverter.ToInt32(bytes, offset + 1);
        if (!executable(result, 32))
            throw new Exception("Native call target invalid");
        return result;
    }

    // 取得背包所属玩家与关卡会话标识，防止跨场景沿用基线。
    public string InventorySession()
    {
        Identity pawn = Pawn(), level = Token(M.Q(pawn.Address + 32));
        if (!IsA(level.Class, "Level"))
            throw new Exception("Player level unavailable");
        return Pid + ":" + pawn.Index + ":" + pawn.Serial + ":" + level.Index + ":" + level.Serial;
    }

    public NavigationCapture ReadNavigation(bool requirePlayerAgreement = true)
    {
        Identity pawn = Pawn(), level = Token(M.Q(pawn.Address + 32)), world = Token(M.Q(level.Address + Offset(level, "OwningWorld", 8))), system = Token(M.Q(world.Address + Offset(world, "NavigationSystem", 8))), actor = Token(M.Q(system.Address + Offset(system, "MainNavData", 8)));
        if (!IsA(level.Class, "Level") || !IsA(world.Class, "World") || !IsA(system.Class, "NavigationSystemV1") || !IsA(actor.Class, "RecastNavMesh"))
            throw new Exception("Active world navigation owner mismatch");
        long image = process.MainModule.BaseAddress.ToInt64(), body = 0;
        using (var symbols = new LocalSymbols(M.Handle, process.MainModule.FileName, image, process.MainModule.ModuleMemorySize))
            foreach (string name in new[]
            {
                "ARecastNavMesh::K2_ReplaceAreaInTileBounds",
                "RecastNavMesh::K2_ReplaceAreaInTileBounds"
            }

            )
            {
                var symbol = symbols.Find(name);
                if (symbol != null && executable(symbol.Address, 1024))
                {
                    body = symbol.Address;
                    break;
                }
            }

        string wrapperPattern = "49 8B CF E8 ?? ?? ?? ?? 85 C0 0F 9F C0";
        if (body == 0)
        {
            long wrapper = NativeWrapper(NamedFunction("RecastNavMesh", "K2_ReplaceAreaInTileBounds"), 1024);
            byte[] code = M.Read(wrapper, 1024);
            int at = SingleMatch(code, wrapperPattern);
            body = RelativeCall(wrapper, code, at + 3);
            AdaptationRecord.Set("navigation.lookup", "native symbols unavailable -> reflected RecastNavMesh.K2_ReplaceAreaInTileBounds -> verified instruction patterns");
        }
        else
            AdaptationRecord.Set("navigation.lookup", "native symbol -> verified instruction patterns");
        byte[] b = M.Read(body, 1536);
        int implAt = SingleMatch(b, "48 8B 81 ?? ?? ?? ?? 48 89 9C 24 ?? ?? ?? ?? 33 DB 48 85 C0"), implOffset = BitConverter.ToInt32(b, implAt + 3);
        int tileAt = SingleMatch(b, "49 8B CF FF C8 23 D0 E8 ?? ?? ?? ?? 48 8B C8 48 85 C0");
        long getter = RelativeCall(body, b, tileAt + 7);
        byte[] get = M.Read(getter, 1024);
        int getAt = SingleMatch(get, "48 63 C2 48 69 C0 ?? ?? ?? ?? 48 03 81 ?? ?? ?? ?? C3");
        int stride = BitConverter.ToInt32(get, getAt + 6), tilesOffset = BitConverter.ToInt32(get, getAt + 13);
        int maxAt = SingleMatch(get, "45 3B 83 ?? ?? ?? ?? 73 ??"), countOffset = BitConverter.ToInt32(get, maxAt + 3);
        if (implOffset < 512 || implOffset > 0x4000 || stride != 168 || tilesOffset < 16 || tilesOffset > 512 || countOffset < 16 || countOffset > 512)
            throw new Exception("Navigation layout requires adaptation");
        long impl = M.Q(actor.Address + implOffset), nav = M.Q(impl + 8), tiles = M.Q(nav + tilesOffset);
        int count = M.I(nav + countOffset);
        if (count < 1 || count > 100000)
            throw new Exception("Navigation tile count invalid");
        AdaptationRecord.Set("function.RecastNavMesh.navigation-body", "RVA=0x" + (body - image).ToString("X") + "; impl=0x" + implOffset.ToString("X") + "; tileGetterRVA=0x" + (getter - image).ToString("X") + "; tileStride=" + stride + "; tilesOffset=0x" + tilesOffset.ToString("X") + "; maxTilesOffset=0x" + countOffset.ToString("X") + "; bytes=" + BitConverter.ToString(b).Replace('-', ' '));
        AdaptationRecord.Set("signature.catalog.navigation.wrapper", wrapperPattern);
        AdaptationRecord.Set("navigation.tile-layout", "Version 7 packed UE tile; ushort polygon/vertex counts +4/+6, 32-byte polygons, double vertices. Only ground polygons with flags=1 and area IDs resolved from native SupportedAreas as NavArea_Default or NavArea_PlayerOnly; exact shared-edge adjacency. Water, lava, unsafe, obstacle, low-height and off-mesh links are excluded.");
        ThreatVector origin = Vector(actor.Address + Offset(actor, "NavMeshOriginOffset", 24));
        var mesh = new NavigationCapture
        {
            Session = InventorySession(),
            Player = Position(pawn),
            AllowedAreas = ReadPlayerGroundAreas(actor)
        };
        Identity capsule = Token(M.Q(pawn.Address + Offset(pawn, "CapsuleComponent", 8)));
        float height = M.F(capsule.Address + Offset(capsule, "CapsuleHalfHeight", 4));
        if (height < 10 || height > 400)
            throw new Exception("Player capsule height invalid");
        mesh.Player.Z -= height;
        for (int first = 0; first < count; first += 4096)
        {
            int take = Math.Min(4096, count - first);
            byte[] chunk = M.Read(tiles + first * stride, take * stride);
            for (int i = 0; i < take; i++)
            {
                long tile = tiles + (first + i) * stride, header = BitConverter.ToInt64(chunk, i * stride + 8);
                if (header == 0)
                    continue;
                byte[] h = M.Read(header, 40);
                if (BitConverter.ToUInt16(h, 0) != 7)
                    throw new Exception("Navigation tile version changed");
                int np = BitConverter.ToUInt16(h, 4), nv = BitConverter.ToUInt16(h, 6);
                if (np < 1 || np > 32768 || nv < 3 || nv > 32768)
                    throw new Exception("Navigation tile counts invalid");
                long polygons = M.Q(tile + 16), vertices = M.Q(tile + 24);
                byte[] p = M.Read(polygons, np * 32), v = M.Read(vertices, nv * 24);
                mesh.LoadedTiles++;
                for (int j = 0; j < np; j++)
                {
                    int at = j * 32, n = p[at + 30];
                    string areaKey = "area=" + (p[at + 31] & 63) + " flags=" + BitConverter.ToUInt16(p, at + 28) + " type=" + (p[at + 31] >> 6);
                    int seen;
                    mesh.AreaFlags.TryGetValue(areaKey, out seen);
                    mesh.AreaFlags[areaKey] = seen + 1;
                    if (n < 3 || n > 6 || (p[at + 31] >> 6) != 0 || BitConverter.ToUInt16(p, at + 28) != 1 || !mesh.AllowedAreas.ContainsKey(p[at + 31] & 63))
                        continue;
                    var points = new ThreatVector[n];
                    for (int k = 0; k < n; k++)
                    {
                        int vi = BitConverter.ToUInt16(p, at + 4 + k * 2);
                        if (vi >= nv)
                            throw new Exception("Navigation vertex index invalid");
                        var recast = new ThreatVector(BitConverter.ToDouble(v, vi * 24), BitConverter.ToDouble(v, vi * 24 + 8), BitConverter.ToDouble(v, vi * 24 + 16));
                        if (!recast.Valid)
                            throw new Exception("Navigation coordinate invalid");
                        points[k] = new ThreatVector(-recast.X + origin.X, -recast.Z + origin.Y, recast.Y + origin.Z);
                    }

                    mesh.Polygons.Add(new NavigationPolygon { Id = mesh.Polygons.Count, Tile = first + i, Vertices = points });
                    if (mesh.Polygons.Count > 50000)
                        throw new Exception("Navigation polygon budget exceeded");
                }

                if (M.Q(tile + 8) != header || M.Q(tile + 16) != polygons || M.Q(tile + 24) != vertices)
                    throw new Exception("Navigation tile changed");
            }
        }

        if (!Valid(pawn) || !Valid(actor) || M.Q(actor.Address + implOffset) != impl || M.Q(impl + 8) != nav || M.Q(nav + tilesOffset) != tiles || M.I(nav + countOffset) != count)
            throw new Exception("Navigation world changed");
        NavigationPlanner.Connect(mesh);
        if (requirePlayerAgreement && NavigationPlanner.Locate(mesh, mesh.Player) == null)
            throw new Exception("Player position does not agree with navigation coordinates");
        return mesh;
    }

}

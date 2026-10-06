// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 中文维护说明：解析骨骼布局和接触几何，为近战威胁提供空间证据。距离与骨骼坐标使用游戏世界单位；仅有动画时序不足以证明会命中，必须保留几何可用性检查。
// 源码导航和常改参数见 docs/maintenance.md；协议、反射标识及类型白名单修改前先核对两端。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

// CombatGeometry 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
sealed class CombatGeometry
{
    public bool Known, ActiveWindow, CurrentOverlap;
    public string Reason, StartBone, EndBone;
    public double Radius, Distance;
    public ThreatVector Start, End, Away;
    public long PoseAgeFrames;
}

// ContactGeometry 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
static class ContactGeometry
{
    // 排除 NaN 和无穷大，避免无效遥测参与距离或时间比较。
    static bool Finite(double value)
    {
        return !Double.IsNaN(value) && !Double.IsInfinity(value);
    }

    // 计算向量点积，用于最近点和接触距离。
    static double Dot(ThreatVector a, ThreatVector b)
    {
        return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    }

    // 将比例或距离限制在给定边界内。
    static double Clamp(double x)
    {
        return Math.Max(0, Math.Min(1, x));
    }

    // 计算点/线段间最近距离，处理退化线段。
    public static double Distance(ThreatVector a, ThreatVector b, ThreatVector c, ThreatVector d, out ThreatVector attackPoint)
    {
        attackPoint = new ThreatVector();
        if (!a.Valid || !b.Valid || !c.Valid || !d.Valid)
            return Double.NaN;
        ThreatVector u = b - a, v = d - c, w = a - c;
        double aa = Dot(u, u), bb = Dot(u, v), cc = Dot(v, v), dd = Dot(u, w), ee = Dot(v, w), s = 0, t = 0;
        if (aa < 1e-12)
        {
            if (cc >= 1e-12)
                t = Clamp(ee / cc);
        }
        else if (cc < 1e-12)
            s = Clamp(-dd / aa);
        else
        {
            double denominator = aa * cc - bb * bb;
            s = denominator > 1e-12 ? Clamp((bb * ee - cc * dd) / denominator) : 0;
            t = (bb * s + ee) / cc;
            if (t < 0)
            {
                t = 0;
                s = Clamp(-dd / aa);
            }
            else if (t > 1)
            {
                t = 1;
                s = Clamp((bb - dd) / aa);
            }
        }

        attackPoint = a + u * s;
        return (attackPoint - (c + v * t)).Length;
    }

    // 按胶囊几何计算接触范围，保留角色/攻击半径。
    public static CombatGeometry Capsule(ThreatVector start, ThreatVector end, double attackRadius, ThreatVector center, double playerRadius, double halfHeight)
    {
        var g = new CombatGeometry
        {
            Start = start,
            End = end,
            Radius = attackRadius
        };
        if (!start.Valid || !end.Valid || !center.Valid || !Finite(attackRadius) || !Finite(playerRadius) || !Finite(halfHeight) || attackRadius <= 0 || attackRadius > 2000 || playerRadius <= 0 || halfHeight < playerRadius || halfHeight > 400)
        {
            g.Reason = "invalid collision geometry";
            return g;
        }

        double cylinder = halfHeight - playerRadius;
        ThreatVector p;
        g.Distance = Distance(start, end, center - new ThreatVector(0, 0, cylinder), center + new ThreatVector(0, 0, cylinder), out p);
        if (Double.IsNaN(g.Distance))
            return g;
        g.Known = true;
        g.CurrentOverlap = g.Distance <= attackRadius + playerRadius;
        var away = center - p;
        away.Z = 0;
        double length = away.Length;
        if (length > 1e-6)
            g.Away = away * (1 / length);
        return g;
    }

    // 将局部骨骼点按原生变换转换为世界坐标。
    public static ThreatVector Transform(ThreatVector p, double[] world)
    {
        if (world == null || world.Length != 12 || world.Any(value => Double.IsNaN(value) || Double.IsInfinity(value)))
            throw new Exception("Invalid bone transform");
        double x = world[0], y = world[1], z = world[2], w = world[3], norm = x * x + y * y + z * z + w * w;
        if (norm < .99 || norm > 1.01 || new[]
        {
            world[8],
            world[9],
            world[10]
        }.Any(s => s <= .001 || s > 100))
            throw new Exception("Invalid bone rotation/scale");
        ThreatVector v = new ThreatVector(p.X * world[8], p.Y * world[9], p.Z * world[10]);
        ThreatVector t = new ThreatVector(2 * (y * v.Z - z * v.Y), 2 * (z * v.X - x * v.Z), 2 * (x * v.Y - y * v.X));
        return v + t * w + new ThreatVector(y * t.Z - z * t.Y, z * t.X - x * t.Z, x * t.Y - y * t.X) + new ThreatVector(world[4], world[5], world[6]);
    }

    // 运行本模块的离线规则自检；返回 PASS 摘要，失败抛出异常供命令行报告。
    public static string Test()
    {
        var sphere = Capsule(new ThreatVector(20, 0, 0), new ThreatVector(20, 0, 0), 10, new ThreatVector(), 15, 35);
        var above = Capsule(new ThreatVector(0, 0, 100), new ThreatVector(0, 0, 100), 10, new ThreatVector(), 15, 35);
        var crossing = Capsule(new ThreatVector(-100, 0, 0), new ThreatVector(100, 0, 0), 5, new ThreatVector(), 15, 35);
        var parallel = Capsule(new ThreatVector(100, 0, -100), new ThreatVector(100, 0, 100), 5, new ThreatVector(), 15, 35);
        if (!sphere.Known || !sphere.CurrentOverlap || above.CurrentOverlap || !crossing.CurrentOverlap || parallel.CurrentOverlap || Capsule(new ThreatVector(), new ThreatVector(), Double.NaN, new ThreatVector(), 15, 35).Known || Capsule(new ThreatVector(), new ThreatVector(), 10, new ThreatVector(), Double.NaN, 35).Known || Capsule(new ThreatVector(), new ThreatVector(), 10, new ThreatVector(), 15, Double.PositiveInfinity).Known)
            throw new Exception("Sphere/segment/capsule geometry failed");
        var point = Transform(new ThreatVector(1, 0, 0), new[] { 0.0, 0, Math.Sqrt(.5), Math.Sqrt(.5), 10, 20, 30, 0, 2, 2, 2, 0 });
        if ((point - new ThreatVector(10, 22, 30)).Length > 1e-6 || sphere.Away.X >= 0)
            throw new Exception("Bone rotation/scale or escape vector failed");
        return "PASS: swept-segment candidate versus player capsule, height/parallel/degenerate/invalid guards, bone quaternion/scale and away direction. Geometry does not enable input.\r\n";
    }
}

// 跨文件的只读游戏读取器；各 partial 文件共同持有同一连接/对象身份缓存。
sealed partial class HealthReader
{
    readonly Dictionary<string, Identity> geometryFunctions = new Dictionary<string, Identity>();
    // BoneLayout 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
    sealed class BoneLayout
    {
        public int World, PoseIndex, PoseArray, ReferenceSkeleton, NameMap, SocketSlot, ReferenceSlot;
        public long SocketFunction, ReferenceFunction;
    }

    readonly Dictionary<string, BoneLayout> boneLayouts = new Dictionary<string, BoneLayout>();
    long frameCounter;
    // 按可核对函数名定位骨骼读取原生代码。
    List<NamedCode> NamedGeometryCode(LocalSymbols symbols, string owner, string name, int length)
    {
        var list = new List<NamedCode>();
        var seen = new HashSet<long>();
        foreach (string qualified in new[]
        {
            "U" + owner + "::" + name,
            owner + "::" + name,
            "U" + owner + "::exec" + name
        }

        )
        {
            var symbol = symbols.Find(qualified);
            if (symbol != null && executable(symbol.Address, length) && seen.Add(symbol.Address))
                list.Add(new NamedCode { Address = symbol.Address, Bytes = M.Read(symbol.Address, length), Source = "native symbol " + qualified });
        }

        Identity f;
        if (geometryFunctions.TryGetValue(owner + "::" + name, out f) && Valid(f))
            for (int slot = 120; slot <= 240; slot += 8)
            {
                long fn = M.Q(f.Address + slot);
                if (executable(fn, length) && seen.Add(fn))
                    list.Add(new NamedCode { Address = fn, Bytes = M.Read(fn, length), Source = "reflected " + owner + "::" + name });
            }

        return list;
    }

    // 解析并核对相对调用目标，而非直接执行代码地址。
    long CallTarget(long address, byte[] code, int at)
    {
        long fn = address + at + 5 + BitConverter.ToInt32(code, at + 1);
        if (!executable(fn, 64))
            throw new Exception("Bone helper outside executable section");
        return fn;
    }

    // 记录骨骼函数的来源与模块相对地址。
    void RecordGeometryCode(string name, long fn, byte[] code, string source)
    {
        AdaptationRecord.Set("function.geometry." + name, source + "; RVA=0x" + (fn - process.MainModule.BaseAddress.ToInt64()).ToString("X") + "; bytes=" + BitConverter.ToString(code).Replace('-', ' '));
    }

    // 读取动画帧标记，避免拼接不同帧的骨骼数据。
    void FrameCounter(LocalSymbols symbols)
    {
        if (frameCounter != 0)
            return;
        var native = symbols.Find("GFrameCounter");
        if (native != null)
        {
            long value = M.Q(native.Address);
            if (value > 0 && value < 1000000000000L)
            {
                frameCounter = native.Address;
                return;
            }
        }

        var found = new HashSet<long>();
        foreach (var candidate in NamedGeometryCode(symbols, "KismetSystemLibrary", "GetFrameCount", 64))
            try
            {
                int at = MontageCodeLayout.Unique(candidate.Bytes, "48 8B 05 ?? ?? ?? ?? 49 89 00 C3");
                long pointer = candidate.Address + at + 7 + BitConverter.ToInt32(candidate.Bytes, at + 3);
                long value = M.Q(pointer);
                if (value <= 0 || value >= 1000000000000L)
                    continue;
                found.Add(pointer);
                RecordGeometryCode("KismetSystemLibrary.GetFrameCount", candidate.Address, candidate.Bytes, candidate.Source);
            }
            catch
            {
            }

        if (found.Count != 1)
            throw new Exception("Frame counter signature unavailable/ambiguous");
        frameCounter = found.First();
    }

    // 唯一解析并缓存已验证骨骼容器布局。
    BoneLayout GetBoneLayout(Identity mesh, Identity asset)
    {
        string key = mesh.Class + ":" + asset.Class;
        BoneLayout cached;
        if (boneLayouts.TryGetValue(key, out cached))
            return cached;
        long image = process.MainModule.BaseAddress.ToInt64();
        var found = new List<BoneLayout>();
        string[] patterns =
        {
            "49 8B 06 FF 90 ?? ?? ?? ??",
            "4C 8D B1 ?? ?? ?? ?? 33 C0 41 0F 10 26",
            "4D 8B CE 48 8D 55 ?? 48 8B CE E8 ?? ?? ?? ??",
            "49 63 8E ?? ?? ?? ??",
            "49 03 94 CE ?? ?? ?? ??",
            "48 81 C1 ?? ?? ?? ?? 49 8B F9",
            "E8 ?? ?? ?? ?? 48 8B 5C 24 ?? 48 8B 6C 24 ?? 89 06",
            "48 8B 89 ?? ?? ?? ?? 0F B6 15",
            "48 8B 03 48 8B CB FF 90 ?? ?? ?? ?? 48 8B C8",
            "48 8D 83 ?? ?? ?? ?? 48 83 C4 20 5B C3",
            "48 8B C8 48 8D 54 24 ?? E8 ?? ?? ?? ??",
            "4C 8B 8F ?? ?? ?? ??",
            "48 8D 14 89 48 8D 14 95 00 00 00 00",
            "48 8B 05 ?? ?? ?? ?? 49 89 00 C3"
        };
        for (int i = 0; i < patterns.Length; i++)
            AdaptationRecord.Set("signature.catalog.geometry." + i, patterns[i]);
        using (var symbols = new LocalSymbols(M.Handle, process.MainModule.FileName, image, process.MainModule.ModuleMemorySize))
        {
            FrameCounter(symbols);
            foreach (var socket in NamedGeometryCode(symbols, "SceneComponent", "GetSocketTransform", 700))
                foreach (var index in NamedGeometryCode(symbols, "SkinnedMeshComponent", "GetBoneIndex", 180))
                    try
                    {
                        int call = MontageCodeLayout.Unique(socket.Bytes, "49 8B 06 FF 90 ?? ?? ?? ??"), slot = BitConverter.ToInt32(socket.Bytes, call + 5);
                        if (slot < 0 || slot > 0x2000 || slot % 8 != 0)
                            continue;
                        long socketFn = M.Q(M.Q(mesh.Address) + slot);
                        if (!executable(socketFn, 512))
                            continue;
                        byte[] socketBody = M.Read(socketFn, 512);
                        int world = BitConverter.ToInt32(socketBody, MontageCodeLayout.Unique(socketBody, "4C 8D B1 ?? ?? ?? ?? 33 C0 41 0F 10 26") + 3);
                        int poseCall = MontageCodeLayout.Unique(socketBody, "4D 8B CE 48 8D 55 ?? 48 8B CE E8 ?? ?? ?? ??") + 10;
                        long poseFn = CallTarget(socketFn, socketBody, poseCall);
                        if (!executable(poseFn, 0x3400))
                            continue;
                        byte[] poseBody = M.Read(poseFn, 0x3400);
                        int pi = BitConverter.ToInt32(poseBody, MontageCodeLayout.Unique(poseBody, "49 63 8E ?? ?? ?? ??") + 3), pa = BitConverter.ToInt32(poseBody, MontageCodeLayout.Unique(poseBody, "49 03 94 CE ?? ?? ?? ??") + 4);
                        // 原生辅助函数必须使用当前组件反射出来的 leader-pose 字段。
                        int leader = BitConverter.ToInt32(poseBody, MontageCodeLayout.Unique(poseBody, "48 81 C1 ?? ?? ?? ?? 49 8B F9") + 3);
                        if (leader != Offset(mesh, "LeaderPoseComponent", 8))
                            continue;
                        int indexCall = MontageCodeLayout.Unique(index.Bytes, "E8 ?? ?? ?? ?? 48 8B 5C 24 ?? 48 8B 6C 24 ?? 89 06");
                        long indexFn = CallTarget(index.Address, index.Bytes, indexCall);
                        byte[] indexBody = M.Read(indexFn, 256);
                        int sk = BitConverter.ToInt32(indexBody, MontageCodeLayout.Unique(indexBody, "48 8B 89 ?? ?? ?? ?? 0F B6 15") + 3);
                        if (sk != Offset(mesh, "SkeletalMesh", 8))
                            continue;
                        int referenceAt = MontageCodeLayout.Unique(indexBody, "48 8B 03 48 8B CB FF 90 ?? ?? ?? ?? 48 8B C8"), rs = BitConverter.ToInt32(indexBody, referenceAt + 8);
                        if (rs < 0 || rs > 0x2000 || rs % 8 != 0)
                            continue;
                        long referenceFn = M.Q(M.Q(asset.Address) + rs);
                        if (!executable(referenceFn, 64))
                            continue;
                        byte[] referenceBody = M.Read(referenceFn, 64);
                        int reference = BitConverter.ToInt32(referenceBody, MontageCodeLayout.Unique(referenceBody, "48 8D 83 ?? ?? ?? ?? 48 83 C4 20 5B C3") + 3);
                        int mapCall = MontageCodeLayout.Unique(indexBody, "48 8B C8 48 8D 54 24 ?? E8 ?? ?? ?? ??") + 8;
                        long mapFn = CallTarget(indexFn, indexBody, mapCall);
                        byte[] mapBody = M.Read(mapFn, 240);
                        int map = BitConverter.ToInt32(mapBody, MontageCodeLayout.Unique(mapBody, "4C 8B 8F ?? ?? ?? ??") + 3);
                        MontageCodeLayout.Unique(mapBody, "48 8D 14 89 48 8D 14 95 00 00 00 00"); // 稀疏映射条目的原生步长为 20 字节，改变步长前需重新验证结构。
                        if (world < 40 || world > 4096 || pi < 40 || pi > 4096 || pa < 40 || pa > 4096 || reference < 40 || reference > 4096 || map < 16 || map > 1024)
                            continue;
                        var layout = new BoneLayout
                        {
                            World = world,
                            PoseIndex = pi,
                            PoseArray = pa,
                            ReferenceSkeleton = reference,
                            NameMap = map,
                            SocketSlot = slot,
                            ReferenceSlot = rs,
                            SocketFunction = socketFn,
                            ReferenceFunction = referenceFn
                        };
                        found.Add(layout);
                        RecordGeometryCode("SceneComponent.GetSocketTransform", socket.Address, socket.Bytes, socket.Source);
                        RecordGeometryCode("SkinnedMeshComponent.GetBoneIndex", index.Address, index.Bytes, index.Source);
                        RecordGeometryCode("socketOverride", socketFn, socketBody, "validated live mesh virtual slot");
                        RecordGeometryCode("boneTransformHelper", poseFn, poseBody, "validated socket override call");
                        RecordGeometryCode("boneIndexHelper", indexFn, indexBody, "validated named wrapper call");
                        RecordGeometryCode("referenceSkeleton", referenceFn, referenceBody, "validated skeletal asset virtual slot");
                        RecordGeometryCode("boneNameMap", mapFn, mapBody, "validated bone-index helper call");
                    }
                    catch (Exception e)
                    {
                        AdaptationRecord.Set("geometry.layout.rejection", e.Message);
                    }
        }

        if (found.Select(l => l.World + ":" + l.PoseIndex + ":" + l.PoseArray + ":" + l.ReferenceSkeleton + ":" + l.NameMap + ":" + l.SocketFunction + ":" + l.ReferenceFunction).Distinct().Count() != 1)
            throw new Exception("Live bone layout unavailable/ambiguous");
        cached = found[0];
        boneLayouts[key] = cached;
        AdaptationRecord.Set("geometry.bone.layout", "world=0x" + cached.World.ToString("X") + " poseIndex=0x" + cached.PoseIndex.ToString("X") + " poseArray=0x" + cached.PoseArray.ToString("X") + " reference=0x" + cached.ReferenceSkeleton.ToString("X") + " nameMap=0x" + cached.NameMap.ToString("X") + "; symbols first, named reflection and validated signatures; no invocation");
        return cached;
    }

    // 稳定读取骨骼坐标，必要时重试帧间变化。
    ThreatVector[] BonePoints(Identity mesh, string[] boneNames, out long age)
    {
        age = 0;
        for (int attempt = 0; attempt < 3; attempt++)
            try
            {
                return BonePointsOnce(mesh, boneNames, out age);
            }
            catch (Exception e)
            {
                if (e.Message != "Skeletal pose changed during read" || attempt == 2)
                    throw;
            }

        throw new Exception("Skeletal pose changed during read");
    }

    // 有界读取单次骨骼坐标并验证数组布局。
    ThreatVector[] BonePointsOnce(Identity mesh, string[] boneNames, out long age)
    {
        age = 0;
        if (!IsA(mesh.Class, "SkeletalMeshComponent") || Weak(mesh.Address + Offset(mesh, "LeaderPoseComponent", 8)) != null)
            throw new Exception("Follower or unsupported skeletal pose");
        Identity asset = Token(M.Q(mesh.Address + Offset(mesh, "SkeletalMesh", 8)));
        if (!IsA(asset.Class, "SkeletalMesh"))
            throw new Exception("Invalid skeletal asset");
        BoneLayout layout = GetBoneLayout(mesh, asset);
        if (M.Q(M.Q(mesh.Address) + layout.SocketSlot) != layout.SocketFunction || M.Q(M.Q(asset.Address) + layout.ReferenceSlot) != layout.ReferenceFunction)
            throw new Exception("Bone virtual implementation changed");
        long transform = Struct("Transform", 96);
        if (Prop(transform, "Translation", 24).Offset != 32 || Prop(transform, "Rotation", 32).Offset != 0 || Prop(transform, "Scale3D", 24).Offset != 64)
            throw new Exception("Bone transform schema mismatch");
        int pi = M.I(mesh.Address + layout.PoseIndex);
        if (pi < 0 || pi > 1)
            throw new Exception("Bone buffer index invalid");
        long header = mesh.Address + layout.PoseArray + pi * 16, array = M.Q(header);
        int n = M.I(header + 8), cap = M.I(header + 12);
        if (n < 1 || n > 2048 || cap < n || cap > 4096)
            throw new Exception("Bone buffer bounds invalid");
        long map = asset.Address + layout.ReferenceSkeleton + layout.NameMap;
        var indices = new Dictionary<string, int>();
        foreach (long row in SparseElements(map, 20, 2048))
        {
            if (M.I(row + 4) != 0)
                throw new Exception("Numbered bone names unsupported");
            string name = Name(M.I(row));
            int index = M.I(row + 8);
            if (index < 0 || index >= n || indices.ContainsKey(name))
                throw new Exception("Bone index map invalid");
            indices[name] = index;
        }

        int frameOffset = Offset(mesh, "LastPoseTickFrame", 4);
        long poseFrame = unchecked((uint)M.I(mesh.Address + frameOffset)), frame = unchecked((uint)M.Q(frameCounter));
        age = unchecked((uint)(frame - poseFrame));
        if (age > 4)
            throw new Exception("Skeletal pose is stale");
        byte[] worldBytes = M.Read(mesh.Address + layout.World, 96), poses = M.Read(array, n * 96);
        if (M.I(mesh.Address + frameOffset) != unchecked((int)poseFrame) || M.I(mesh.Address + layout.PoseIndex) != pi || M.Q(header) != array || M.I(header + 8) != n)
            throw new Exception("Skeletal pose changed during read");
        double[] world = Enumerable.Range(0, 12).Select(i => BitConverter.ToDouble(worldBytes, i * 8)).ToArray();
        var points = new List<ThreatVector>();
        foreach (string name in boneNames)
        {
            int index;
            if (String.IsNullOrEmpty(name) || name == "None" || !indices.TryGetValue(name, out index))
                throw new Exception("Named bone unavailable; custom/empty sockets not inferred");
            int at = index * 96 + 32;
            var p = new ThreatVector(BitConverter.ToDouble(poses, at), BitConverter.ToDouble(poses, at + 8), BitConverter.ToDouble(poses, at + 16));
            if (!p.Valid || p.Length > 20000)
                throw new Exception("Bone position invalid");
            var point = ContactGeometry.Transform(p, world);
            if (!point.Valid)
                throw new Exception("Bone world position invalid");
            points.Add(point);
        }

        if (!Valid(mesh) || !Valid(asset) || M.Q(mesh.Address + Offset(mesh, "SkeletalMesh", 8)) != asset.Address)
            throw new Exception("Skeletal asset changed during read");
        return points.ToArray();
    }

    // 结合攻击骨骼和双方体积生成接触几何证据。
    CombatGeometry ReadContactGeometry(Identity asset, CombatDamageWindow window, double position, Identity mesh, ThreatVector playerPosition, double playerRadius, double halfHeight)
    {
        var g = new CombatGeometry();
        try
        {
            if (window == null || window.Kind != "ANS_AnimationBasedComplexCollisionMeleeAttack")
            {
                g.Reason = "ability-driven attack geometry not resolved";
                return g;
            }

            long notify = Struct("AnimNotifyEvent", 184), header = asset.Address + Offset(asset, "Notifies", 16), array = M.Q(header);
            int count = M.I(header + 8);
            if (window.Index < 0 || window.Index >= count || count > 512)
                throw new Exception("Collision notify bounds invalid");
            Identity obj = Token(M.Q(array + window.Index * 184 + Prop(notify, "NotifyStateClass", 8).Offset));
            if (ClassName(obj.Class) != window.Kind)
                throw new Exception("Collision notify class mismatch");
            string start = Name(M.I(obj.Address + Offset(obj, "StartSocket", 8))), end = Name(M.I(obj.Address + Offset(obj, "EndSocket", 8)));
            double radius = M.F(obj.Address + Offset(obj, "Radius", 4));
            if (M.F(obj.Address + Offset(obj, "ConeOffsetUnits", 4)) != 0 || ReadBool(obj, "bIsWorldActor"))
                throw new Exception("Offset/world-actor collision model not supported");
            long age;
            var points = BonePoints(mesh, new[] { start, end }, out age);
            g = ContactGeometry.Capsule(points[0], points[1], radius, playerPosition, playerRadius, halfHeight);
            g.StartBone = start;
            g.EndBone = end;
            g.PoseAgeFrames = age;
            g.ActiveWindow = position >= window.Start && position <= window.End;
            g.Reason = "current named-bone segment only; future pose, swept history and damage eligibility not confirmed";
            if (!Valid(asset) || !Valid(obj) || M.Q(header) != array || M.I(header + 8) != count)
                throw new Exception("Collision notify changed");
        }
        catch (Exception e)
        {
            g.Known = false;
            g.CurrentOverlap = false;
            g.Reason = e.Message;
        }

        return g;
    }

    // 只读检查实际游戏骨骼帧与布局，属于现场诊断而非离线规则自检。
    public string BoneLiveTest()
    {
        Identity player = Pawn(), mesh = Token(M.Q(player.Address + Offset(player, "Mesh", 8)));
        long age;
        var points = BonePoints(mesh, new[] { "J_Root", "J_Head" }, out age);
        double distance = (points[0] - Position(player)).Length;
        if (distance > 200 || (points[1] - points[0]).Length < 10 || (points[1] - points[0]).Length > 500)
            throw new Exception("Player bone positions fail root/head cross-check");
        return "PASS: live named root/head bones, stable pose buffer, frame freshness, quaternion world transform and player-root spatial cross-check; root distance=" + distance.ToString("0.0") + " age=" + age + ". No game input.\r\n" + AdaptationRecord.Contents();
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 中文维护说明：跟踪敌人和弹道，结合标签、动画与接触几何生成威胁帧。位置/半径使用世界单位，预计接触时间使用秒；忽略友方、死者、菜单和缺乏空间证据的目标。
// 源码导航和常改参数见 docs/maintenance.md；协议、反射标识及类型白名单修改前先核对两端。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

// 游戏世界坐标向量；分量使用世界单位，距离与 UI 像素没有换算关系。
struct ThreatVector
{
    public double X, Y, Z;
    // 初始化 ThreatVector 的本地状态、依赖和必要绑定；实例释放时使用对应清理流程。
    public ThreatVector(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    // Valid 的只读/受控访问入口；使用该属性而不绕过访问器中的校验和更新逻辑。
    public bool Valid
    {
        get
        {
            return Finite(X) && Finite(Y) && Finite(Z) && Math.Abs(X) < 1e8 && Math.Abs(Y) < 1e8 && Math.Abs(Z) < 1e8;
        }
    }

    // 排除 NaN 和无穷大，避免无效遥测参与距离或时间比较。
    static bool Finite(double x)
    {
        return !Double.IsNaN(x) && !Double.IsInfinity(x);
    }

    // Length 的只读/受控访问入口；使用该属性而不绕过访问器中的校验和更新逻辑。
    public double Length
    {
        get
        {
            return Math.Sqrt(X * X + Y * Y + Z * Z);
        }
    }

    // 提供游戏世界向量的基本运算，保留分量精度与世界单位。
    public static ThreatVector operator -(ThreatVector a, ThreatVector b)
    {
        return new ThreatVector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    }

    // 提供游戏世界向量的基本运算，保留分量精度与世界单位。
    public static ThreatVector operator +(ThreatVector a, ThreatVector b)
    {
        return new ThreatVector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }

    // 提供游戏世界向量的基本运算，保留分量精度与世界单位。
    public static ThreatVector operator *(ThreatVector a, double b)
    {
        return new ThreatVector(a.X * b, a.Y * b, a.Z * b);
    }
}

// EnemyThreat 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
sealed class EnemyThreat
{
    public string Id, Kind, Detail;
    public double Seconds, Radius;
    public bool RadiusKnown;
    public ThreatVector Position, Velocity;
    public MeleeTrajectory Melee;
}

// CombatExclusion 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
sealed class CombatExclusion
{
    public string ActorId, ActorClass, Stage, Reason, ComponentClass, MeshClass, AnimClass, LeaderPoseClass;
    public bool? AvatarMatches;
}

// ThreatFrame 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
sealed class ThreatFrame
{
    public bool Known;
    public string Reason;
    public List<EnemyThreat> Threats = new List<EnemyThreat>();
    public List<EnemyThreat> ProjectilePaths = new List<EnemyThreat>();
    public List<CombatObservation> Observations = new List<CombatObservation>();
    public List<CombatExclusion> Exclusions = new List<CombatExclusion>();
    public int Enemies, Projectiles, UnvalidatedAttacks;
    public CombatReadiness Readiness;
}

// ThreatRule 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
static class ThreatRule
{
    // 计算相对线性运动与膨胀角色胶囊的交点，包含高度约束。
    // 根据弹道/双方空间状态估算接触时间，未知几何不产生有效预测。
    public static double Impact(ThreatVector offset, ThreatVector relativeVelocity, double radius, double halfHeight, double horizon)
    {
        if (!offset.Valid || !relativeVelocity.Valid || relativeVelocity.Length < 1 || radius <= 0 || halfHeight < radius || horizon <= 0 || horizon > 2)
            return Double.NaN;
        double vv = relativeVelocity.X * relativeVelocity.X + relativeVelocity.Y * relativeVelocity.Y, dot = offset.X * relativeVelocity.X + offset.Y * relativeVelocity.Y;
        double c = offset.X * offset.X + offset.Y * offset.Y - radius * radius, lo = 0, hi = horizon;
        if (vv < 1e-8)
        {
            if (c > 0)
                return Double.NaN;
        }
        else
        {
            double discriminant = dot * dot - vv * c;
            if (discriminant < 0)
                return Double.NaN;
            double root = Math.Sqrt(discriminant);
            lo = Math.Max(lo, (-dot - root) / vv);
            hi = Math.Min(hi, (-dot + root) / vv);
        }

        if (Math.Abs(relativeVelocity.Z) < 1e-8)
        {
            if (Math.Abs(offset.Z) > halfHeight)
                return Double.NaN;
        }
        else
        {
            double a = (-halfHeight - offset.Z) / relativeVelocity.Z, b = (halfHeight - offset.Z) / relativeVelocity.Z;
            lo = Math.Max(lo, Math.Min(a, b));
            hi = Math.Min(hi, Math.Max(a, b));
        }

        return hi >= lo && hi >= 0 ? lo : Double.NaN;
    }

    // 匹配已核实的攻击状态名称，保留定义范围。
    public static bool AttackName(string name)
    {
        if (String.IsNullOrEmpty(name))
            return false;
        string n = name.ToLowerInvariant();
        if (new[]
        {
            "spawn",
            "death",
            "dead",
            "idle",
            "walk",
            "hurt",
            "hitreact",
            "victory"
        }.Any(n.Contains))
            return false;
        return new[]
        {
            "attack",
            "melee",
            "ranged",
            "shoot"
        }.Any(n.Contains);
    }

    // 依据原生队伍标签确认敌对，未知队伍不算敌人。
    public static bool HostileTeam(IEnumerable<string> values)
    {
        if (values == null)
            return false;
        var tags = values.ToArray();
        Func<string, bool> mob = t => t == "SW.Team.Mob" || t.StartsWith("SW.Team.Mob.", StringComparison.Ordinal);
        return tags.Any(mob) && tags.Contains("SW.Mob.Default.Hostile") && !tags.Any(t => t.StartsWith("SW.Team.", StringComparison.Ordinal) && !mob(t));
    }

    // 从威胁帧选择满足攻击时限、几何及玩家状态的预警对象。
    public static int[] Select(ToolboxSettings settings, CooldownInfo[] cooldowns, float souls, float[] costs, float hp, float max)
    {
        if (!(hp > 0 && max >= hp) || Single.IsInfinity(hp) || Single.IsInfinity(max))
            return new int[0];
        bool potion;
        int[] keys = RecoveryRule.Select(settings, cooldowns, souls, costs, out potion);
        return hp < max * settings.Threshold / 100f ? keys : keys.Where(k => k != settings.PotionKey).ToArray();
    }

    // 运行本模块的离线规则自检；返回 PASS 摘要，失败抛出异常供命令行报告。
    public static string Test()
    {
        Func<ThreatVector, ThreatVector, double> impact = (p, v) => Impact(p, v, 50, 80, .7);
        if (Math.Abs(impact(new ThreatVector(500, 0, 0), new ThreatVector(-1000, 0, 0)) - .45) > 1e-6)
            throw new Exception("incoming trajectory failed");
        if (!Double.IsNaN(impact(new ThreatVector(500, 0, 0), new ThreatVector(1000, 0, 0))) || !Double.IsNaN(impact(new ThreatVector(500, 100, 0), new ThreatVector(-1000, 0, 0))) || !Double.IsNaN(impact(new ThreatVector(500, 0, 200), new ThreatVector(-1000, 0, 0))))
            throw new Exception("outgoing/miss/height guard failed");
        if (!Double.IsNaN(impact(new ThreatVector(1000, 0, 0), new ThreatVector(-1000, 0, 0))) || !Double.IsNaN(impact(new ThreatVector(Double.NaN, 0, 0), new ThreatVector(-1000, 0, 0))))
            throw new Exception("horizon/invalid trajectory guard failed");
        if (!AttackName("AM_Zombie_Attack") || !AttackName("AM_Skeleton_RangedAttack") || AttackName("AM_Slime_Spawn_Small") || AttackName("AM_Zombie_Death") || AttackName("AM_Walk"))
            throw new Exception("attack animation filtering failed");
        if (HostileTeam(new[] { "SW.State.Life.Alive" }) || HostileTeam(new[] { "SW.Team.Mob" }) || HostileTeam(new[] { "SW.Team.Player", "SW.Team.Mob", "SW.Mob.Default.Hostile" }) || HostileTeam(new[] { "SW.Team.Neutral", "SW.Mob.Default.Hostile" }) || !HostileTeam(new[] { "SW.Team.Mob", "SW.Mob.Default.Hostile", "SW.State.Life.Alive" }))
            throw new Exception("neutral/pet/unconfirmed hostility filtering failed");
        var s = new ToolboxSettings
        {
            AutoSlots = new[]
            {
                true,
                false,
                false
            },
            PotionFallback = true,
            PotionSlot = 0
        };
        var cd = new[]
        {
            new CooldownInfo
            {
                Known = true
            },
            new CooldownInfo(),
            new CooldownInfo(),
            new CooldownInfo
            {
                Known = true,
                Charges = 1
            }
        };
        if (!Select(s, cd, 100, new[] { 20f, 0, 0 }, 100, 100).SequenceEqual(new[] { s.Slots[0] }))
            throw new Exception("early artifact selection failed");
        cd[0].Start = 1;
        if (Select(s, cd, 100, new[] { 20f, 0, 0 }, 100, 100).Length != 0 || !Select(s, cd, 100, new[] { 20f, 0, 0 }, 30, 100).SequenceEqual(new[] { s.PotionKey }))
            throw new Exception("potion health threshold failed");
        s.AutoSlots = new bool[3];
        if (Select(s, cd, Single.NaN, null, 100, 100).Length != 0 || !Select(s, cd, Single.NaN, null, 30, 100).SequenceEqual(new[] { s.PotionKey }))
            throw new Exception("potion-only threat rule failed");
        if (Select(s, cd, 0, null, 0, 100).Length != 0 || Select(s, cd, 0, null, Single.NaN, 100).Length != 0)
            throw new Exception("dead/unknown health guard failed");
        return "PASS: incoming/outgoing/missing/high/invalid trajectories, forecast horizon, attack/spawn/death filtering, early artifacts, low-health-only potion and potion-only mode.\r\n";
    }
}

// 跨文件的只读游戏读取器；各 partial 文件共同持有同一连接/对象身份缓存。
sealed partial class HealthReader
{
    // ThreatSlot 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
    sealed class ThreatSlot
    {
        public long Address;
        public int Serial;
    }

    readonly Dictionary<int, ThreatSlot> threatSlots = new Dictionary<int, ThreatSlot>();
    readonly Dictionary<long, int> threatClasses = new Dictionary<long, int>();
    readonly Dictionary<long, Identity> threatActors = new Dictionary<long, Identity>();
    readonly Dictionary<long, Identity> attackMeshes = new Dictionary<long, Identity>();
    readonly Dictionary<long, bool> attackMeshClasses = new Dictionary<long, bool>();
    readonly Dictionary<long, long> attackMeshOwners = new Dictionary<long, long>();
    MeleeThreatTracker meleeTracker = new MeleeThreatTracker();
    string meleeSession;
    Identity montageFunction, hostilityFunction;
    int montageArray = -1, montageWeight = -1, ownedTagsOffset = -1;
    long lastThreatScan = -1000;
    string montageFailure;
    // 核对 Actor 类是否属于已识别敌人或弹道。
    int ThreatClass(long cl)
    {
        int kind;
        if (!threatClasses.TryGetValue(cl, out kind))
        {
            kind = IsA(cl, "MobCharacter") ? 1 : IsA(cl, "BaseProjectile") ? 2 : 0;
            threatClasses[cl] = kind;
        }

        return kind;
    }

    // 把具有稳定 UObject 身份的 Actor 加入威胁跟踪。
    void TrackThreat(long a, long cl, int index, int serial)
    {
        TrackNearbyLoot(a, cl);
        string widgetClass = ClassName(cl);
        if (widgetClass == "GA_Roll" && !Name(M.I(a + 24)).StartsWith("Default__"))
            rollAbilities[a] = Token(a);
        if (widgetClass == "W_ArtifactSlot_C" || widgetClass == "W_PotionSlot_C")
        {
            var widgets = widgetClass == "W_ArtifactSlot_C" ? artifactWidgets : potionWidgets;
            widgets.RemoveAll(w => w.Address == a);
            widgets.Add(Token(a));
        }

        bool skeletal;
        if (!attackMeshClasses.TryGetValue(cl, out skeletal))
        {
            skeletal = IsA(cl, "SkeletalMeshComponent");
            attackMeshClasses[cl] = skeletal;
        }

        if (skeletal && !Name(M.I(a + 24)).StartsWith("Default__"))
        {
            attackMeshes[a] = Token(a);
            attackMeshOwners[a] = M.Q(a + 32);
        }

        if (widgetClass.StartsWith("W_") && IsA(cl, "CommonActivatableWidget") && !Name(M.I(a + 24)).StartsWith("Default__"))
            combatWidgets[a] = Token(a);
        if (ThreatClass(cl) != 0 && !Name(M.I(a + 24)).StartsWith("Default__"))
            threatActors[a] = Token(a);
        if (ClassName(cl) == "Function")
        {
            Identity owner = Token(M.Q(a + 32));
            string ownerName = Name(owner.Name), functionName = Name(M.I(a + 24));
            if (ownerName == "AnimInstance")
                combatFunctions[functionName] = Token(a);
            if (ownerName == "SceneComponent" && functionName == "GetSocketTransform" || ownerName == "SkinnedMeshComponent" && functionName == "GetBoneIndex" || ownerName == "KismetSystemLibrary" && functionName == "GetFrameCount")
                geometryFunctions[ownerName + "::" + functionName] = Token(a);
        }

        if (ClassName(cl) == "Function" && Name(M.I(a + 24)) == "GetCurrentActiveMontage")
        {
            Identity owner = Token(M.Q(a + 32));
            if (Name(owner.Name) == "AnimInstance")
                montageFunction = Token(a);
        }

        if (ClassName(cl) == "Function" && Name(M.I(a + 24)) == "IsHostileTowards")
        {
            Identity owner = Token(M.Q(a + 32));
            if (Name(owner.Name) == "BaseCharacter")
                hostilityFunction = Token(a);
        }
    }

    // 更新候选列表并移除已失效或换身份的 Actor。
    void RefreshThreatActors(long now)
    {
        // 所有调用使用同一单调时钟，包含威胁观察以外的冷却读取。
        now = (long)(Stopwatch.GetTimestamp() * (1000.0 / Stopwatch.Frequency));
        if (now - lastThreatScan < 250)
            return;
        lastThreatScan = now;
        int count = M.I(objects + 20);
        if (count < 1000 || count > 2000000)
            throw new Exception("对象表验证失败。");
        long table = M.Q(objects);
        for (int ci = 0; ci * 65536 < count; ci++)
        {
            byte[] chunk = M.Read(M.Q(table + ci * 8), Math.Min(65536, count - ci * 65536) * 24);
            for (int at = 0; at < chunk.Length; at += 24)
            {
                int index = ci * 65536 + at / 24;
                long a = BitConverter.ToInt64(chunk, at);
                int serial = BitConverter.ToInt32(chunk, at + 16);
                ThreatSlot previous;
                if (threatSlots.TryGetValue(index, out previous) && previous.Address == a && previous.Serial == serial)
                    continue;
                if (previous != null)
                    threatActors.Remove(previous.Address);
                threatSlots[index] = new ThreatSlot
                {
                    Address = a,
                    Serial = serial
                };
                if (a == 0)
                    continue;
                try
                {
                    Identity t = Token(a);
                    TrackThreat(a, t.Class, index, serial);
                }
                catch
                {
                }
            }
        }

        artifactWidgets.RemoveAll(w => !Valid(w));
        potionWidgets.RemoveAll(w => !Valid(w));
    }

    // 读取已验证反射布尔字段，处理其掩码布局。
    bool ReadBool(Identity o, string name)
    {
        Property p = Prop(o.Class, name, 1);
        byte[] meta = M.Read(p.Field + 120, 4);
        if (meta[0] != 1 || meta[1] > 8 || meta[2] == 0 || meta[3] == 0)
            throw new Exception("布尔属性布局不匹配。");
        AdaptationRecord.Set("boolean." + ClassName(o.Class) + "." + name, "offset=0x" + p.Offset.ToString("X") + " byteOffset=" + meta[1] + " mask=0x" + meta[3].ToString("X"));
        return (M.Read(o.Address + p.Offset + meta[1], 1)[0] & meta[3]) != 0;
    }

    // 按已核实布局读取原生空间向量。
    ThreatVector Vector(long at)
    {
        byte[] b = M.Read(at, 24);
        var v = new ThreatVector(BitConverter.ToDouble(b, 0), BitConverter.ToDouble(b, 8), BitConverter.ToDouble(b, 16));
        if (!v.Valid)
            throw new Exception("坐标验证失败。");
        return v;
    }

    // 读取 Actor 的有效世界位置。
    ThreatVector Position(Identity actor)
    {
        Identity root = Token(M.Q(actor.Address + Offset(actor, "RootComponent", 8)));
        if (!Valid(actor) || !IsA(root.Class, "SceneComponent") || M.Q(root.Address + Offset(root, "AttachParent", 8)) != 0)
            throw new Exception("位置读数不可用。");
        return Vector(root.Address + Offset(root, "RelativeLocation", 24));
    }

    // 读取指定原生 GameplayTag 容器。
    List<string> Tags(Identity actor)
    {
        Identity asc = Token(M.Q(actor.Address + Offset(actor, "AbilitySystemComponent", 8)));
        if (!IsA(asc.Class, "SWAbilitySystemComponent") || M.Q(asc.Address + Offset(asc, "AvatarActor", 8)) != actor.Address)
            throw new Exception("敌人状态不可用。");
        long header = asc.Address + Offset(asc, "SWReplicatedTags", 32), data = M.Q(header);
        int n = M.I(header + 8), cap = M.I(header + 12);
        if (n < 0 || n > 256 || cap < n || cap > 1024)
            throw new Exception("敌人标签无效。");
        byte[] bytes = M.Read(data, n * 8);
        var tags = new List<string>();
        for (int i = 0; i < n; i++)
            tags.Add(Name(BitConverter.ToInt32(bytes, i * 8)));
        if (M.Q(header) != data || M.I(header + 8) != n)
            throw new Exception("敌人状态正在变化。");
        return tags;
    }

    // 核对弱引用的索引/序列，避免使用已复用对象地址。
    Identity Weak(long at)
    {
        int index = M.I(at), serial = M.I(at + 4), count = M.I(objects + 20);
        if (index < 0 || index >= count || serial == 0)
            return null;
        long slot = M.Q(M.Q(objects) + (index / 65536) * 8) + (index % 65536) * 24;
        if (M.I(slot + 16) != serial)
            return null;
        return Token(M.Q(slot));
    }

    // 核对攻击蒙太奇访问布局。
    void MontageLayout()
    {
        if (montageArray >= 0)
            return;
        if (montageFailure != null)
            throw new Exception(montageFailure);
        try
        {
            AdaptationRecord.Set("function.AnimInstance.GetCurrentActiveMontage", "query pending / unavailable; reflected owner and function name");
            AdaptationRecord.Set("signature.catalog.montage.count", "8B 81 ?? ?? ?? ?? 83 E8 01; offset at +2");
            AdaptationRecord.Set("signature.catalog.montage.array", "49 8B 82 ?? ?? ?? ??; offset at +3");
            AdaptationRecord.Set("signature.catalog.montage.weight", "0F 2E 80 ?? ?? ?? ?? or 0F 2F 80 ?? ?? ?? ??; offset at +3");
            var layouts = new HashSet<string>();
            using (var symbols = new LocalSymbols(M.Handle, process.MainModule.FileName, process.MainModule.BaseAddress.ToInt64(), process.MainModule.ModuleMemorySize))
            {
                foreach (var code in NamedAnimationCode(symbols, "GetCurrentActiveMontage", 100))
                    try
                    {
                        var layout = MontageCodeLayout.Active(code.Bytes);
                        layouts.Add(layout.Array + ":" + layout.Weight);
                        RecordAnimationCode("GetCurrentActiveMontage", code);
                    }
                    catch
                    {
                    }
            }

            if (layouts.Count != 1)
                throw new Exception("攻击动画函数布局未识别。");
            string[] offsets = layouts.First().Split(':');
            montageArray = Int32.Parse(offsets[0]);
            montageWeight = Int32.Parse(offsets[1]);
            ToolboxLog.Write("Threat.Layout", "symbol-first / reflected AnimInstance.GetCurrentActiveMontage signature validated; no function call");
        }
        catch (Exception e)
        {
            montageFailure = e.Message;
            throw;
        }
    }

    // 确定实际攻击用 Mesh，排除无归属预览对象。
    Identity AttackMesh(Identity actor)
    {
        var candidates = new List<Identity>();
        foreach (var mesh in attackMeshes.Values.ToArray())
            try
            {
                long owner;
                if (!attackMeshOwners.TryGetValue(mesh.Address, out owner) || owner != actor.Address)
                    continue;
                if (!Valid(mesh))
                {
                    attackMeshes.Remove(mesh.Address);
                    attackMeshOwners.Remove(mesh.Address);
                    continue;
                }

                if (M.Q(mesh.Address + 32) != actor.Address)
                    continue;
                long address = M.Q(mesh.Address + Offset(mesh, "AnimScriptInstance", 8));
                if (address == 0)
                    continue;
                Identity anim = Token(address);
                if (!IsA(anim.Class, "AnimInstance") || M.Q(anim.Address + 32) != mesh.Address || !Valid(anim))
                    continue;
                candidates.Add(mesh);
            }
            catch
            {
            }

        if (candidates.Count != 1)
            throw new Exception("攻击骨骼动画组件不唯一或不可用。");
        return candidates[0];
    }

    // 生成当前敌人、弹道、排除原因与攻击证据帧。
    public ThreatFrame Threats(long now)
    {
        var frame = new ThreatFrame();
        now = (long)(Stopwatch.GetTimestamp() * (1000.0 / Stopwatch.Frequency));
        try
        {
            Identity player = Pawn();
            ThreatVector playerPosition = Position(player);
            RefreshThreatActors(now);
            MontageLayout();
            OwnedTagsLayout(player);
            Identity playerLevel = Token(M.Q(player.Address + 32)), world = Token(M.Q(playerLevel.Address + Offset(playerLevel, "OwningWorld", 8)));
            if (!IsA(playerLevel.Class, "Level") || !IsA(world.Class, "World"))
                throw new Exception("当前场景不可用。");
            string gameHash = AdaptationRecord.Get("game.sha256");
            AdaptationRecord.Set("combat.catalog", CombatDefinitions.MatchesVersion(gameHash) ? "matching game build; active montage observations only; no new automatic execution" : "build mismatch; static combat definitions disabled");
            Identity capsule = Token(M.Q(player.Address + Offset(player, "CapsuleComponent", 8)));
            double radius = M.F(capsule.Address + Offset(capsule, "CapsuleRadius", 4)), height = M.F(capsule.Address + Offset(capsule, "CapsuleHalfHeight", 4));
            if (radius < 5 || radius > 200 || height < radius || height > 400)
                throw new Exception("玩家碰撞范围无效。");
            Identity movement = Token(M.Q(player.Address + Offset(player, "CharacterMovement", 8)));
            ThreatVector playerVelocity = Vector(movement.Address + Offset(movement, "Velocity", 24));
            if (playerVelocity.Length > 5000)
                throw new Exception("玩家速度不可用。");
            frame.Readiness = ReadCombatReadiness(player);
            string session = InventorySession();
            if (session != meleeSession)
            {
                meleeSession = session;
                meleeTracker = new MeleeThreatTracker();
            }

            meleeTracker.Prune(now);
            foreach (Identity actor in threatActors.Values.ToArray())
            {
                string stage = "actor/world";
                try
                {
                    if (!Valid(actor) || ReadBool(actor, "bHidden") || !ReadBool(actor, "bActorEnableCollision") || ReadBool(actor, "bActorIsBeingDestroyed"))
                        continue;
                    Identity level = Token(M.Q(actor.Address + 32));
                    if (!IsA(level.Class, "Level") || M.Q(level.Address + Offset(level, "OwningWorld", 8)) != world.Address)
                        continue;
                    ThreatVector pos = Position(actor);
                    double distance = (pos - playerPosition).Length;
                    if (distance > 2500)
                        continue;
                    int kind = ThreatClass(actor.Class);
                    if (kind == 1)
                    {
                        stage = "owned enemy tags";
                        List<string> tags = OwnedTags(actor);
                        if (!ThreatRule.HostileTeam(tags) || !tags.Contains("SW.State.Life.Alive"))
                            continue;
                        frame.Enemies++;
                        stage = "target/animation";
                        string actorClass = ClassName(actor.Class);
                        CombatProfile profile = CombatDefinitions.Identify(gameHash, actorClass, tags);
                        bool defined = CombatDefinitions.DefinedActor(actorClass) || tags.Contains("SW.Trait.Boss");
                        Identity target = Weak(actor.Address + Offset(actor, "ReplicatedCurrentTargetActor", 8));
                        if (target == null || target.Address != player.Address || !Valid(target))
                            continue;
                        Identity mesh = AttackMesh(actor), anim = Token(M.Q(mesh.Address + Offset(mesh, "AnimScriptInstance", 8)));
                        if (!IsA(anim.Class, "AnimInstance"))
                            continue;
                        long h = anim.Address + montageArray, data = M.Q(h);
                        int n = M.I(h + 8), cap = M.I(h + 12);
                        if (n < 0 || n > 64 || cap < n || cap > 256)
                            continue;
                        byte[] entries = M.Read(data, n * 8);
                        for (int i = 0; i < n; i++)
                        {
                            long instance = BitConverter.ToInt64(entries, i * 8);
                            if (instance == 0)
                                continue;
                            Identity asset = Token(M.Q(instance));
                            if (!IsA(asset.Class, "AnimMontage"))
                                continue;
                            float weight = M.F(instance + montageWeight);
                            if (weight <= 0 || weight > 1.1f)
                                continue;
                            string name = Name(asset.Name);
                            if (!Valid(actor) || !Valid(asset) || M.Q(h) != data || M.I(h + 8) != n || M.Q(instance) != asset.Address)
                                continue;
                            if (defined)
                            {
                                CombatMotion motion = CombatDefinitions.Motion(profile, name);
                                if (motion != null)
                                    frame.Observations.Add(ReadCombatObservation(actor.Index + ":" + actor.Serial + ":" + instance + ":" + asset.Index + ":" + asset.Serial, profile, motion, asset, instance, now, mesh, playerPosition, radius, height));
                                frame.UnvalidatedAttacks++;
                                continue;
                            }

                            // 普通敌人也要核对真实通知元数据；动画名称和任意距离不能证明即将命中玩家。
                            CombatMotion liveMotion = ReadLiveMotion(asset, now);
                            if (liveMotion.Windows.Length == 0)
                                continue;
                            var liveProfile = new CombatProfile
                            {
                                Name = actorClass,
                                TypeTag = tags.Where(t => t.StartsWith("SW.Mob.") && !t.StartsWith("SW.Mob.Default.")).OrderByDescending(t => t.Length).FirstOrDefault(),
                                ActorClass = actorClass
                            };
                            var observed = ReadCombatObservation(actor.Index + ":" + actor.Serial + ":" + instance + ":" + asset.Index + ":" + asset.Serial, liveProfile, liveMotion, asset, instance, now, mesh, playerPosition, radius, height);
                            observed.DefinitionSource = "live reflected notify metadata";
                            frame.Observations.Add(observed);
                            frame.UnvalidatedAttacks++;
                        }
                    }
                    else if (kind == 2)
                    {
                        Identity source = Token(M.Q(actor.Address + Offset(actor, "Instigator", 8)));
                        if (source.Address == player.Address || !IsA(source.Class, "MobCharacter"))
                            continue;
                        List<string> tags = OwnedTags(source);
                        if (!ThreatRule.HostileTeam(tags))
                            continue;
                        Identity sourceLevel = Token(M.Q(source.Address + 32));
                        if (!IsA(sourceLevel.Class, "Level") || M.Q(sourceLevel.Address + Offset(sourceLevel, "OwningWorld", 8)) != world.Address || ReadBool(source, "bActorIsBeingDestroyed"))
                            continue;
                        Identity motion = Token(M.Q(actor.Address + Offset(actor, "ProjectileMovementComponent", 8)));
                        if (!IsA(motion.Class, "ProjectileMovementComponent") || !ReadBool(motion, "bSimulationEnabled"))
                            continue;
                        // 曲线/追踪弹道需要独立验证模型，不能标成线性运动。
                        if (Math.Abs(M.F(motion.Address + Offset(motion, "ProjectileGravityScale", 4))) > .001 || ReadBool(motion, "bIsHomingProjectile"))
                            continue;
                        ThreatVector velocity = Vector(motion.Address + Offset(motion, "Velocity", 24));
                        if (velocity.Length < 10 || velocity.Length > 50000)
                            continue;
                        frame.Projectiles++;
                        double bound;
                        try
                        {
                            bound = ProjectileBoundRadius(actor);
                        }
                        catch
                        {
                            bound = Double.NaN;
                        }

                        bool boundKnown = !Double.IsNaN(bound);
                        double padding = boundKnown ? bound : 25;
                        double impact = ThreatRule.Impact(pos - playerPosition, velocity - playerVelocity, radius + padding, height + padding, .7);
                        if (Valid(actor) && Valid(source))
                        {
                            var path = new EnemyThreat
                            {
                                Id = actor.Index + ":" + actor.Serial + ":projectile",
                                Kind = "projectile",
                                Detail = Name(actor.Name),
                                Seconds = Double.IsNaN(impact) ? 1 : impact,
                                Position = pos,
                                Velocity = velocity,
                                Radius = boundKnown ? bound : 25,
                                RadiusKnown = boundKnown
                            };
                            if (boundKnown)
                                frame.ProjectilePaths.Add(path);
                            if (!Double.IsNaN(impact))
                                frame.Threats.Add(path);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (frame.Exclusions.Count < 64)
                    {
                        var excluded = new CombatExclusion
                        {
                            ActorId = actor.Index + ":" + actor.Serial,
                            ActorClass = ClassName(actor.Class),
                            Stage = stage,
                            Reason = e.Message
                        };
                        if (stage == "owned enemy tags")
                            try
                            {
                                Identity component = Token(M.Q(actor.Address + Offset(actor, "AbilitySystemComponent", 8)));
                                excluded.ComponentClass = ClassName(component.Class);
                                excluded.AvatarMatches = M.Q(component.Address + Offset(component, "AvatarActor", 8)) == actor.Address;
                            }
                            catch
                            {
                            }

                        if (stage == "target/animation")
                            try
                            {
                                Identity mesh = Token(M.Q(actor.Address + Offset(actor, "Mesh", 8)));
                                excluded.MeshClass = ClassName(mesh.Class);
                                long animAddress = M.Q(mesh.Address + Offset(mesh, "AnimScriptInstance", 8));
                                excluded.AnimClass = animAddress == 0 ? "none" : ClassName(Token(animAddress).Class);
                                Identity leader = Weak(mesh.Address + Offset(mesh, "LeaderPoseComponent", 8));
                                excluded.LeaderPoseClass = leader == null ? "none" : ClassName(leader.Class);
                            }
                            catch
                            {
                            }

                        frame.Exclusions.Add(excluded);
                    }
                }
            }

            foreach (var observation in frame.Observations)
            {
                var approaching = meleeTracker.Observe(observation, now, playerPosition, playerVelocity, radius, height);
                if (approaching != null)
                    frame.Threats.Add(approaching);
            }

            if (!Valid(player) || !Valid(world) || M.Q(player.Address + 32) != playerLevel.Address || M.Q(playerLevel.Address + Offset(playerLevel, "OwningWorld", 8)) != world.Address || InventorySession() != session)
                throw new Exception("角色对象或场景已变化。");
            frame.Known = true;
        }
        catch (Exception e)
        {
            frame.Reason = e.Message;
        }

        return frame;
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 附近目标识别、类型白名单、原生范围、交互节流与 UI 调度。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

static class NearbyLootCatalog
{
    static readonly HashSet<string> tags = Load();
    static HashSet<string> Load()
    {
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("loot-catalog.json"))
        using (var reader = new StreamReader(stream))
        {
            var root = new JavaScriptSerializer
            {
                MaxJsonLength = 1000000
            }.Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
            return new HashSet<string>(((System.Collections.IEnumerable)root["entries"]).Cast<Dictionary<string, object>>().Select(row => (string)row["typeTag"]));
        }
    }

    public static bool Contains(string tag)
    {
        return tag != null && tags.Contains(tag);
    }

    // 按已核实附魔书类型分类，不依赖屏幕拾取提示。
    public static bool Book(string tag)
    {
        return Contains(tag) && tag.StartsWith("SW.Item.EnchantmentBook.", StringComparison.Ordinal) && tag != "SW.Item.EnchantmentBook.Base";
    }

    // 按已核实食物类型分类，未知食物不自动尝试。
    public static bool Food(string tag)
    {
        return Contains(tag) && tag.StartsWith("SW.Item.Consumable.Food.", StringComparison.Ordinal);
    }

    // 识别地上可拾取 TNT；背负/投掷状态由原生游戏处理。
    public static bool Tnt(string tag)
    {
        return Contains(tag) && tag == "SW.Item.Consumable.Throwable.TNT";
    }

    // 区分已核实小型/大型绿宝石罐。
    public static bool EmeraldPot(string tag)
    {
        return Contains(tag) && (tag == "SW.LootActor.Pot.Small.Emerald" || tag == "SW.LootActor.Pot.Large.Emerald");
    }

    // 识别已掉落绿宝石，仅用于结果观察，实际拾取由游戏负责。
    public static bool EmeraldCurrency(string tag)
    {
        return Contains(tag) && (tag == "SW.Item.Currency.Emerald" || tag == "SW.Item.Currency.EmeraldBlock");
    }
}

static class LootIndicatorLayout
{
    public const string ComponentPattern = "48 8B 81 ?? ?? ?? ?? 49 89 00 C3";
    public const string VisiblePattern = "48 8B 81 ?? ?? ?? ?? 48 85 C0 74 ?? 8B 40 08 C1 E8 1E F6 D0 A8 01 74 ?? 80 79 ?? 00 74 ?? 41 C6 00 01 C3 41 C6 00 00 C3";
    // 核对原生交互提示访问布局，拒绝不唯一或失配候选。
    public static void Validate(byte[] visible, byte[] component, int componentOffset, int visibleOffset)
    {
        var a = new GameLocator.Pattern(ComponentPattern).Matches(component).ToArray();
        var b = new GameLocator.Pattern(VisiblePattern).Matches(visible).ToArray();
        if (a.Length != 1 || b.Length != 1 || BitConverter.ToInt32(component, a[0] + 3) != componentOffset || BitConverter.ToInt32(visible, b[0] + 3) != componentOffset || visible[b[0] + 26] != visibleOffset)
            throw new Exception("Interaction indicator getter signature / reflected field mismatch");
    }

}

// 真实附近 Actor 的快照；对象名、类型、位置与归属进入原生请求，不能仅凭提示文字选中。
sealed class NearbyLootTarget
{
    public string Id, Kind, Type, ActorType, ActorName, ItemId, Reason;
    public ThreatVector Position, AimPosition;
    public double Distance, PotRadius;
    public bool InRange, LocalOwner, AimKnown;
    public bool? Opened;
    public string[] Tags;
}

sealed class NearbyLootState
{
    public bool Known;
    public string Reason, PawnName;
    public CombatState Combat;
    public double InteractionRange;
    public StationaryFloor Standing = new StationaryFloor();
    public List<NearbyLootTarget> Targets = new List<NearbyLootTarget>();
    public List<string> Exclusions = new List<string>();
}

sealed class StationaryFloor
{
    public bool Known, Blocking, Walkable;
    public int MovementMode;
    public double Distance;
    public string Reason;
    // 当前地面/移动状态是否有充分证据支持本模块动作。
    public bool Supported
    {
        get
        {
            return Known && (MovementMode == 1 || MovementMode == 2) && Blocking && Walkable && !Double.IsNaN(Distance) && !Double.IsInfinity(Distance) && Distance >= -5 && Distance <= 10;
        }
    }
}

static class LootSceneTransform
{
    // 按真实原生变换把目标点转换成世界坐标。
    public static ThreatVector Apply(ThreatVector point, ThreatVector location, ThreatVector rotation, ThreatVector scale)
    {
        if (!point.Valid || !location.Valid || !rotation.Valid || !scale.Valid || Math.Abs(rotation.X) > 720 || Math.Abs(rotation.Y) > 720 || Math.Abs(rotation.Z) > 720 || scale.X <= 0 || scale.Y <= 0 || scale.Z <= 0 || scale.X > 20 || scale.Y > 20 || scale.Z > 20)
            throw new Exception("Pot scene transform invalid");
        double p = rotation.X * Math.PI / 180, y = rotation.Y * Math.PI / 180, r = rotation.Z * Math.PI / 180;
        var forward = new ThreatVector(Math.Cos(p) * Math.Cos(y), Math.Cos(p) * Math.Sin(y), Math.Sin(p));
        var right0 = new ThreatVector(-Math.Sin(y), Math.Cos(y), 0);
        var up0 = new ThreatVector(-Math.Sin(p) * Math.Cos(y), -Math.Sin(p) * Math.Sin(y), Math.Cos(p));
        var right = right0 * Math.Cos(r) - up0 * Math.Sin(r);
        var up = up0 * Math.Cos(r) + right0 * Math.Sin(r);
        return location + forward * (point.X * scale.X) + right * (point.Y * scale.Y) + up * (point.Z * scale.Z);
    }

}

sealed class NearbyLootTiming
{
    public const int Minimum = 100, Maximum = 30000;
    long lastAny = -1000000, lastOther = -1000000, lastFood = -1000000;
    // 把交互间隔限制到 100–30000 毫秒，UI/配置均应复用此边界。
    public static int Clamp(int value)
    {
        return Math.Max(Minimum, Math.Min(Maximum, value));
    }

    // 检查通用/食物间隔是否已到，避免不同目标绕过节流。
    public bool Ready(string kind, long now, int interval, int foodInterval)
    {
        return now - lastAny >= Minimum && now - (kind == "food" ? lastFood : lastOther) >= (kind == "food" ? Clamp(foodInterval) : Clamp(interval));
    }

    public void Attempt(string kind, long now)
    {
        lastAny = now;
        if (kind == "food")
            lastFood = now;
        else
            lastOther = now;
    }

}

static class NearbyIntervalScale
{
    // 把毫秒间隔映射为滑块位置，保持数字输入可往返。
    public static int Position(int milliseconds)
    {
        return (int)Math.Round(Math.Log(NearbyLootTiming.Clamp(milliseconds) / (double)NearbyLootTiming.Minimum) / Math.Log(NearbyLootTiming.Maximum / (double)NearbyLootTiming.Minimum) * 1000);
    }

    // 把滑块位置映射回毫秒，不能误当成秒使用。
    public static int Milliseconds(int position)
    {
        return NearbyLootTiming.Clamp((int)Math.Round(NearbyLootTiming.Minimum * Math.Pow(NearbyLootTiming.Maximum / (double)NearbyLootTiming.Minimum, Math.Max(0, Math.Min(1000, position)) / 1000.0)));
    }

}

sealed class NearbyLootRule
{
    readonly HashSet<string> attempted = new HashSet<string>();
    readonly Dictionary<string, long> deferred = new Dictionary<string, long>();
    string session;
    // 切换关卡/角色会话时重置单目标尝试记录。
    public void Session(string value)
    {
        if (value != session)
        {
            attempted.Clear();
            deferred.Clear();
            session = value;
        }
    }

    // 把目标原生身份构造为稳定尝试键，避免只用位置区分目标。
    static string Key(NearbyLootTarget target)
    {
        return (target.Kind == "item" || target.Kind == "book") && !String.IsNullOrEmpty(target.ItemId) ? "item:" + target.ItemId : target.Id;
    }

    public void Attempt(NearbyLootTarget target)
    {
        attempted.Add(Key(target));
    }

    // 暂时推迟当前目标，避免拒绝目标每帧重复占用通道。
    public void Defer(NearbyLootTarget target, long now, int milliseconds = 2000)
    {
        deferred[Key(target)] = now + Math.Max(100, milliseconds);
    }

    bool Deferred(NearbyLootTarget target, long now)
    {
        long until;
        string key = Key(target);
        if (!deferred.TryGetValue(key, out until))
            return false;
        if (now < until)
            return true;
        deferred.Remove(key);
        return false;
    }

    // 检查附近战斗状态；原生模式和旧点击模式的规则分别处理。
    public static bool NearbyCombat(NearbyLootState state)
    {
        return state.Combat.Targets.Any(t => t.TargetsPlayer || t.Distance <= Math.Max(300, state.InteractionRange));
    }

    // 要求罐子存活、可攻击且可目标化，并校验其原生类型标签。
    public static bool ReadyPot(NearbyLootTarget t)
    {
        return t != null && t.Kind == "pot" && t.AimKnown && t.AimPosition.Valid && NearbyLootCatalog.EmeraldPot(t.Type) && t.Tags != null && new[]
        {
            t.Type,
            "SW.TargetingProperties.BreakablePot",
            "SW.State.Life.Alive",
            "SW.Property.Attackable",
            "SW.Property.Targetable",
            "SW.Tracked.Damageable"
        }.All(t.Tags.Contains) && !t.Tags.Contains("SW.State.Untargetable");
    }

    // 校验世界距离、高度、碰撞半径和原生范围，保留近战保守限制。
    public static bool WithinRange(CombatState state, ThreatVector position, double interactionRange, bool stationaryPot, double potRadius = 0)
    {
        if (state == null || !state.Player.Valid || !position.Valid || Double.IsNaN(interactionRange) || Double.IsInfinity(interactionRange) || interactionRange < 20 || interactionRange > 600)
            return false;
        if (Double.IsNaN(potRadius) || Double.IsInfinity(potRadius) || potRadius < 0 || potRadius > 500)
            return false;
        var delta = position - state.Player;
        double height = Math.Abs(delta.Z);
        delta.Z = 0;
        return delta.Length <= (stationaryPot ? Math.Min(160 + Math.Min(potRadius, 80), interactionRange * .8) : interactionRange) && height <= state.HalfHeight + 120;
    }

    // 使用原生交互范围减去安全边界，防止游戏开启路径接近目标。
    static bool DirectRange(NearbyLootState state, NearbyLootTarget t)
    {
        double range = state.InteractionRange;
        if (range < 20 || range > 600 || Double.IsNaN(range) || !t.Position.Valid || !state.Combat.Player.Valid)
            return false;
        var d = t.Position - state.Combat.Player;
        double maximum = t.Kind == "pot" ? Math.Min(150, range - 10) : range - 10;
        return d.X * d.X + d.Y * d.Y <= maximum * maximum && Math.Abs(d.Z) <= 150;
    }

    // 按用户类型开关、目标归属、范围和一次尝试规则选出一个现有目标。
    public NearbyLootTarget Select(NearbyLootState state, bool chests, bool items, bool pots = false, long now = 0, bool foods = false, bool direct = false, bool allowMoving = false, Func<NearbyLootTarget, bool> eligible = null)
    {
        if (state == null || !state.Known || state.Combat == null || !state.Combat.CanAttempt || (!(direct && allowMoving) && state.Combat.PlayerVelocity.Length > 30) || (!direct && NearbyCombat(state)))
            return null;
        Session(state.Combat.Session);
        return state.Targets.Where(t => (direct ? DirectRange(state, t) : state.Combat.CanAim && state.Combat.LeftMousePrimary) && t.InRange && !String.IsNullOrEmpty(t.Id) && !attempted.Contains(Key(t)) && !Deferred(t, now) && (eligible == null || eligible(t)) && (items && (t.Kind == "item" || direct && t.Kind == "book" && NearbyLootCatalog.Book(t.Type)) && t.LocalOwner && NearbyLootCatalog.Contains(t.Type) && !NearbyLootCatalog.Food(t.Type) && !NearbyLootCatalog.Tnt(t.Type) || direct && items && t.Kind == "tnt" && NearbyLootCatalog.Tnt(t.Type) || foods && t.Kind == "food" && NearbyLootCatalog.Food(t.Type) || chests && t.Kind == "chest" && t.Opened != true && NearbyLootCatalog.Contains(t.Type) || pots && (direct || state.Combat.RootKey != 0 && state.Combat.RootKey != state.Combat.DodgeKey) && ReadyPot(t))).OrderBy(t => t.Distance).FirstOrDefault();
    }

}

sealed partial class HealthReader
{
    // 读取玩家原生投掷物数量，供 TNT 拾取结果比较。
    public double ReadThrowableCount()
    {
        Identity player = Pawn(), asc = Token(M.Q(player.Address + Offset(player, "AbilitySystemComponent", 8)));
        if (M.Q(asc.Address + Offset(asc, "AvatarActor", 8)) != player.Address)
            throw new Exception("Throwable attribute owner mismatch");
        long h = asc.Address + Offset(asc, "SpawnedAttributes", 16), data = M.Q(h);
        int n = M.I(h + 8), cap = M.I(h + 12), matches = 0;
        double result = 0;
        if (n < 1 || n > 128 || cap < n || cap > 512)
            throw new Exception("Throwable attribute array invalid");
        int current = Prop(Struct("GameplayAttributeData", 16), "CurrentValue", 4).Offset;
        for (int i = 0; i < n; i++)
        {
            Identity a = Token(M.Q(data + i * 8));
            if (ClassName(a.Class) != "ATR_Throwable")
                continue;
            long owner = M.Q(a.Address + 32);
            if (owner != player.Address && owner != asc.Address)
                continue;
            result = M.F(a.Address + Offset(a, "ThrowableHeldCount", 16) + current);
            matches++;
        }

        if (matches != 1 || Double.IsNaN(result) || Double.IsInfinity(result) || result < 0 || result > 100 || !Valid(player) || !Valid(asc) || M.Q(h) != data || M.I(h + 8) != n)
            throw new Exception("Throwable attribute unavailable or changed");
        return result;
    }

    string LootObjectName(Identity actor)
    {
        return Name(actor.Name) + (actor.Number > 0 ? "_" + (actor.Number - 1) : "");
    }

    bool stationaryModesVerified;
    // 验证地面/移动模式布局，禁止用猜测偏移放行。
    void VerifyStationaryModes()
    {
        if (stationaryModesVerified)
            return;
        var modes = AllTokens().Where(t => ClassName(t.Class) == "Enum" && Name(t.Name) == "EMovementMode").ToArray();
        if (modes.Length != 1)
            throw new Exception("Movement mode enum unavailable");
        var mode = modes[0];
        int matches = 0;
        for (int off = 48; off <= 112; off += 8)
            try
            {
                long data = M.Q(mode.Address + off);
                int count = M.I(mode.Address + off + 8), capacity = M.I(mode.Address + off + 12);
                if (count < 3 || count > 32 || capacity < count || capacity > 64)
                    continue;
                var values = new Dictionary<string, long>();
                for (int i = 0; i < count; i++)
                {
                    string name = Name(M.I(data + i * 16));
                    if (!name.StartsWith("EMovementMode::", StringComparison.Ordinal) && !name.StartsWith("MOVE_", StringComparison.Ordinal))
                        throw new Exception();
                    values.Add(name.Split(':').Last(), M.Q(data + i * 16 + 8));
                }

                AdaptationRecord.Set("loot.stationary.enumCandidate." + off, String.Join(",", values.Select(v => v.Key + "=" + v.Value)));
                if (values["MOVE_Walking"] == 1 && values["MOVE_NavWalking"] == 2 && values["MOVE_Falling"] == 3)
                    matches++;
            }
            catch
            {
            }

        if (matches != 1 || !Valid(mode))
            throw new Exception("Movement mode enum ambiguous or changed");
        stationaryModesVerified = true;
        AdaptationRecord.Set("loot.stationary.movementModes", "reflected EMovementMode: Walking=1 NavWalking=2 Falling=3; native floor support only, no forward movement");
    }

    bool FloorFlag(long type, long address, string name)
    {
        Property p = Prop(type, name, 1);
        byte[] meta = M.Read(p.Field + 120, 4);
        if (meta[0] != 1 || meta[1] > 8 || meta[2] == 0 || meta[3] == 0)
            throw new Exception("Floor boolean schema changed");
        return (M.Read(address + p.Offset + meta[1], 1)[0] & meta[3]) != 0;
    }

    // 组合地面、移动模式和支撑状态，拒绝空中/不确定地形。
    StationaryFloor ReadStationaryFloor(Identity player)
    {
        var result = new StationaryFloor();
        try
        {
            VerifyStationaryModes();
            Identity movement = Token(M.Q(player.Address + Offset(player, "CharacterMovement", 8)));
            if (!IsA(movement.Class, "CharacterMovementComponent") || M.Q(movement.Address + Offset(movement, "CharacterOwner", 8)) != player.Address)
                throw new Exception("Stationary movement owner mismatch");
            long type = Struct("FindFloorResult", 272), floor = movement.Address + Offset(movement, "CurrentFloor", 272);
            int mode = Offset(movement, "MovementMode", 1);
            result.MovementMode = M.Read(movement.Address + mode, 1)[0];
            result.Blocking = FloorFlag(type, floor, "bBlockingHit");
            result.Walkable = FloorFlag(type, floor, "bWalkableFloor");
            result.Distance = M.F(floor + Prop(type, "FloorDist", 4).Offset);
            if (!Valid(player) || !Valid(movement) || M.Q(player.Address + Offset(player, "CharacterMovement", 8)) != movement.Address || M.Read(movement.Address + mode, 1)[0] != result.MovementMode)
                throw new Exception("Stationary floor changed");
            result.Known = true;
        }
        catch (Exception e)
        {
            result.Reason = e.Message;
        }

        return result;
    }

    readonly Dictionary<long, Identity> nearbyLootActors = new Dictionary<long, Identity>();
    readonly Dictionary<long, int> nearbyLootClasses = new Dictionary<long, int>();
    readonly Dictionary<long, Identity> lootIndicators = new Dictionary<long, Identity>();
    readonly Dictionary<long, bool> lootIndicatorClasses = new Dictionary<long, bool>();
    readonly Dictionary<long, Identity> lootPromptComponents = new Dictionary<long, Identity>(), lootPromptWidgets = new Dictionary<long, Identity>();
    readonly Dictionary<long, int> lootPromptClasses = new Dictionary<long, int>();
    readonly Dictionary<string, Identity> lootFunctions = new Dictionary<string, Identity>();
    bool lootGetterTried, lootGetterKnown;
    string lootGetterReason;
    // 记录已验证目标 UObject 身份，避免对象地址复用。
    void TrackNearbyLoot(long address, long cl)
    {
        if (ClassName(cl) == "Function")
        {
            Identity owner = Token(M.Q(address + 32));
            string ownerName = Name(owner.Name), functionName = Name(M.I(address + 24));
            if (new[]
            {
                "IndicatorDescriptor",
                "LootActor",
                "ChestActor",
                "SWItemActor",
                "GASItemComponent"
            }.Contains(ownerName) || ownerName == "GameplayPlayerController" && functionName == "OnActorClicked" || ownerName == "AbilitySystemComponent" && functionName == "ServerTryActivateAbilityWithEventData" || ownerName == "AbilitySystemBlueprintLibrary" && new[]
            {
                "GetGameplayAbilityFromSpecHandle",
                "AbilityTargetDataFromActor"
            }.Contains(functionName))
                lootFunctions[ownerName + "::" + functionName] = Token(address);
        }

        bool indicator;
        if (!lootIndicatorClasses.TryGetValue(cl, out indicator))
        {
            indicator = IsA(cl, "IndicatorDescriptor");
            lootIndicatorClasses[cl] = indicator;
        }

        if (indicator && !Name(M.I(address + 24)).StartsWith("Default__"))
            lootIndicators[address] = Token(address);
        int prompt;
        if (!lootPromptClasses.TryGetValue(cl, out prompt))
        {
            prompt = IsA(cl, "AS_InWorldItemTooltipComponent") || IsA(cl, "AS_InWorldPromptWidgetComponent") ? 1 : IsA(cl, "AS_InWorldItemTooltipWidget") || IsA(cl, "AS_InWorldPromptWidget") ? 2 : 0;
            lootPromptClasses[cl] = prompt;
        }

        if (prompt != 0 && !Name(M.I(address + 24)).StartsWith("Default__"))
            (prompt == 1 ? lootPromptComponents : lootPromptWidgets)[address] = Token(address);
        int kind;
        if (!nearbyLootClasses.TryGetValue(cl, out kind))
        {
            kind = IsA(cl, "SWItemActor") ? 1 : IsA(cl, "SWASCChestActor") ? 2 : IsA(cl, "SWChestActor") ? 3 : IsA(cl, "ChestActor") ? 4 : IsA(cl, "LootActor") ? 5 : IsA(cl, "ItemActor") ? 6 : IsA(cl, "SWActor") ? 7 : 0;
            nearbyLootClasses[cl] = kind;
        }

        if (kind != 0 && !Name(M.I(address + 24)).StartsWith("Default__"))
            nearbyLootActors[address] = Token(address);
    }

    // 记录交互 getter 的原生查找来源和相对地址。
    object LootFunctionRecord()
    {
        var records = new List<object>();
        long image = process.MainModule.BaseAddress.ToInt64();
        using (var symbols = new LocalSymbols(M.Handle, process.MainModule.FileName, image, process.MainModule.ModuleMemorySize))
            foreach (var pair in lootFunctions)
                try
                {
                    if (!Valid(pair.Value))
                        continue;
                    var candidates = new List<object>();
                    LocalSymbols.Symbol symbol = null;
                    string qualified = null;
                    foreach (string name in new[]
                    {
                        pair.Key,
                        "U" + pair.Key,
                        "A" + pair.Key
                    }

                    )
                    {
                        symbol = symbols.Find(name);
                        if (symbol != null)
                        {
                            qualified = name;
                            break;
                        }
                    }

                    if (symbol != null && executable(symbol.Address, 128))
                        candidates.Add(new { source = "native-symbol " + qualified, rva = "0x" + (symbol.Address - image).ToString("X"), bytes = BitConverter.ToString(M.Read(symbol.Address, 128)).Replace('-', ' ') });
                    else
                        for (int slot = 120; slot <= 240; slot += 8)
                        {
                            long pointer = M.Q(pair.Value.Address + slot);
                            if (executable(pointer, 128))
                                candidates.Add(new { source = "reflected-function-pointer candidate; not invoked", rva = "0x" + (pointer - image).ToString("X"), bytes = BitConverter.ToString(M.Read(pointer, 128)).Replace('-', ' ') });
                        }

                    records.Add(new { name = pair.Key, nativeSymbolAvailable = symbol != null, candidates = candidates });
                }
                catch
                {
                }

        return records;
    }

    sealed class LootCode
    {
        public long Address;
        public byte[] Bytes;
        public string Source;
    }

    // 按函数名/特征取得可验证交互代码候选。
    List<LootCode> LootCodeCandidates(LocalSymbols symbols, string name)
    {
        var result = new List<LootCode>();
        foreach (string qualified in new[]
        {
            "UIndicatorDescriptor::" + name,
            "IndicatorDescriptor::" + name
        }

        )
        {
            var symbol = symbols.Find(qualified);
            if (symbol != null && executable(symbol.Address, 64))
                result.Add(new LootCode { Address = symbol.Address, Bytes = M.Read(symbol.Address, 64), Source = "native symbol " + qualified });
        }

        // 反射定位所属函数，代码特征核对实现和偏移，不保留某版本的固定模块地址。
        if (result.Count > 0)
            return result;
        Identity function;
        if (!lootFunctions.TryGetValue("IndicatorDescriptor::" + name, out function) || !Valid(function))
            return result;
        for (int slot = 120; slot <= 240; slot += 8)
        {
            long fn = M.Q(function.Address + slot);
            if (executable(fn, 64))
                result.Add(new LootCode { Address = fn, Bytes = M.Read(fn, 64), Source = "reflected IndicatorDescriptor::" + name + " + verified wildcard signature" });
        }

        return result;
    }

    // 核对交互 getter 与结构偏移关系，不只凭名称放行。
    bool VerifyLootGetters(Identity descriptor)
    {
        if (lootGetterTried)
            return lootGetterKnown;
        lootGetterTried = true;
        try
        {
            int component = Offset(descriptor, "Component", 8);
            Property visible = Prop(descriptor.Class, "bVisible", 1);
            byte[] meta = M.Read(visible.Field + 120, 4);
            if (meta[0] != 1 || meta[1] != 0 || meta[3] != 255)
                throw new Exception("Indicator native boolean layout changed");
            var matches = new List<Tuple<LootCode, LootCode>>();
            using (var symbols = new LocalSymbols(M.Handle, process.MainModule.FileName, process.MainModule.BaseAddress.ToInt64(), process.MainModule.ModuleMemorySize))
            {
                foreach (var a in LootCodeCandidates(symbols, "GetIsVisible"))
                    foreach (var b in LootCodeCandidates(symbols, "GetSceneComponent"))
                        try
                        {
                            LootIndicatorLayout.Validate(a.Bytes, b.Bytes, component, visible.Offset);
                            matches.Add(Tuple.Create(a, b));
                        }
                        catch
                        {
                        }
            }

            if (matches.Count != 1)
                throw new Exception("Interaction getter signatures absent or ambiguous");
            long image = process.MainModule.BaseAddress.ToInt64();
            var found = matches[0];
            AdaptationRecord.Set("signature.catalog.loot.component", LootIndicatorLayout.ComponentPattern);
            AdaptationRecord.Set("signature.catalog.loot.visible", LootIndicatorLayout.VisiblePattern);
            foreach (var code in new[]
            {
                found.Item1,
                found.Item2
            }

            )
                AdaptationRecord.Set("function.loot." + (code == found.Item1 ? "GetIsVisible" : "GetSceneComponent"), code.Source + " RVA=0x" + (code.Address - image).ToString("X") + " bytes=" + BitConverter.ToString(code.Bytes).Replace('-', ' '));
            lootGetterKnown = true;
        }
        catch (Exception e)
        {
            lootGetterReason = e.Message;
            AdaptationRecord.Set("loot.getter.unavailable", lootGetterReason);
        }

        return lootGetterKnown;
    }

    // 读取原生提示与目标的关联，仅作旧输入/诊断证据。
    public bool NearbyPromptMatches(string id)
    {
        // 屏幕投影不能识别游戏鼠标目标；旧路径需要属于同一 Actor 的实际可见世界提示。
        Identity player = Pawn();
        foreach (var prompt in lootPromptComponents.Values.ToArray())
            try
            {
                if (!Valid(prompt))
                {
                    lootPromptComponents.Remove(prompt.Address);
                    continue;
                }

                if (M.Q(prompt.Address + 32) != player.Address)
                    continue;
                Identity descriptor = Token(M.Q(prompt.Address + Offset(prompt, "IndicatorDescriptor", 8)));
                if (!VerifyLootGetters(descriptor) || !ReadBool(descriptor, "bVisible") || ReadBool(descriptor, "bOverrideScreenPosition"))
                    continue;
                Identity source = Token(M.Q(descriptor.Address + Offset(descriptor, "DataObject", 8))), component = Token(M.Q(descriptor.Address + Offset(descriptor, "Component", 8)));
                // 原生根提示组件以本地 Pawn 为 DataObject，其 Component 则重新绑定到当前高亮目标。
                if (source.Address != player.Address || !LootPromptWidgetVisible(descriptor))
                    continue;
                if ((M.I(component.Address + 8) & 0x40000000) != 0)
                    continue;
                Identity owner = Token(M.Q(component.Address + 32));
                if (!IsA(component.Class, "SceneComponent") || !IsA(owner.Class, "Actor"))
                    continue;
                bool matches = owner.Index + ":" + owner.Serial == id;
                if (!matches && IsA(owner.Class, "ItemActor"))
                {
                    Identity parent = Token(M.Q(owner.Address + Offset(owner, "Owner", 8)));
                    matches = parent.Index + ":" + parent.Serial == id;
                }

                if (matches && Valid(owner) && Valid(source) && Valid(component) && Valid(descriptor) && Valid(prompt) && Valid(player))
                {
                    AdaptationRecord.Set("loot.prompt.guard", "local AS_InWorldItemTooltipComponent / AS_InWorldPromptWidgetComponent owns IndicatorDescriptor; matching visible native prompt widget; DataObject local pawn; Component owning actor identity; ordinary primary action only after game prompt matches");
                    return true;
                }
            }
            catch
            {
            }

        return false;
    }

    // 检查目标提示是否真实可见，不能由文字截图代替原生关联。
    bool LootPromptWidgetVisible(Identity descriptor)
    {
        foreach (var widget in lootPromptWidgets.Values.ToArray())
            try
            {
                if (!Valid(widget))
                {
                    lootPromptWidgets.Remove(widget.Address);
                    continue;
                }

                if (M.Q(widget.Address + Offset(widget, "IndicatorDescriptor", 8)) != descriptor.Address)
                    continue;
                byte visibility = M.Read(widget.Address + Offset(widget, "Visibility", 1), 1)[0];
                if (visibility != 0 && visibility != 3 && visibility != 4)
                    continue;
                float opacity = M.F(widget.Address + Offset(widget, "RenderOpacity", 4));
                if (opacity < .95f || opacity > 1.01f)
                    continue;
                if (Valid(widget) && Valid(descriptor))
                    return true;
            }
            catch
            {
            }

        return false;
    }

    // 读取原生罐子可攻击位置与碰撞几何，原生路径不移动鼠标。
    ThreatVector PotAim(Identity actor, out double potRadius)
    {
        potRadius = 0;
        Identity root = Token(M.Q(actor.Address + Offset(actor, "RootComponent", 8))), component = Token(M.Q(actor.Address + Offset(actor, "NearMissTargetSelect", 8)));
        if (!IsA(component.Class, "SphereComponent"))
            throw new Exception("Native pot targeting sphere unavailable");
        double radius = M.F(component.Address + Offset(component, "SphereRadius", 4));
        if (Double.IsNaN(radius) || radius < 10 || radius > 500)
            throw new Exception("Native pot targeting sphere invalid");
        Identity capsule = Token(M.Q(actor.Address + Offset(actor, "Capsule", 8)));
        if (!IsA(capsule.Class, "CapsuleComponent") || M.Q(component.Address + Offset(component, "AttachParent", 8)) != capsule.Address)
            throw new Exception("Pot target selection capsule unavailable");
        double capsuleRadius = M.F(capsule.Address + Offset(capsule, "CapsuleRadius", 4));
        if (Double.IsNaN(capsuleRadius) || capsuleRadius < 1 || capsuleRadius > 500)
            throw new Exception("Pot capsule radius invalid");
        var chain = new List<Identity>();
        var seen = new HashSet<long>();
        var point = new ThreatVector(0, 0, 0);
        bool rooted = false, scaleCapsule = false;
        double scaleX = 1, scaleY = 1;
        for (int i = 0; i < 8; i++)
        {
            if (!seen.Add(component.Address) || !IsA(component.Class, "SceneComponent") || M.Q(component.Address + 32) != actor.Address || ReadBool(component, "bAbsoluteLocation") || ReadBool(component, "bAbsoluteRotation") || ReadBool(component, "bAbsoluteScale"))
                throw new Exception("Pot targeting attachment unsupported");
            long socket = component.Address + Offset(component, "AttachSocketName", 8);
            if (Name(M.I(socket)) != "None" || M.I(socket + 4) != 0)
                throw new Exception("Pot targeting socket unsupported");
            var scale = Vector(component.Address + Offset(component, "RelativeScale3D", 24));
            if (component.Address == capsule.Address)
                scaleCapsule = true;
            if (scaleCapsule)
            {
                scaleX *= scale.X;
                scaleY *= scale.Y;
            }

            chain.Add(component);
            point = LootSceneTransform.Apply(point, Vector(component.Address + Offset(component, "RelativeLocation", 24)), Vector(component.Address + Offset(component, "RelativeRotation", 24)), scale);
            long parent = M.Q(component.Address + Offset(component, "AttachParent", 8));
            if (parent == 0)
            {
                rooted = component.Address == root.Address;
                break;
            }

            component = Token(parent);
        }

        if (!rooted || !point.Valid || (point - Position(actor)).Length > 300 || !Valid(actor) || chain.Any(c => !Valid(c)) || M.Q(actor.Address + Offset(actor, "RootComponent", 8)) != root.Address)
            throw new Exception("Pot targeting identity changed");
        potRadius = capsuleRadius * Math.Min(scaleX, scaleY);
        if (!scaleCapsule || Double.IsNaN(potRadius) || Double.IsInfinity(potRadius) || potRadius <= 0 || potRadius > 500)
            throw new Exception("Pot world capsule radius invalid");
        AdaptationRecord.Set("loot.pot.aim", "reflected NearMissTargetSelect.SphereRadius and Capsule.CapsuleRadius; owner-validated SceneComponent attachment chain, RelativeLocation/RelativeRotation/RelativeScale3D, no sockets or absolute transforms; native target-selection center instead of actor root");
        return point;
    }

    // 读取当前玩家原生交互范围，不写入或扩大该游戏参数。
    double ReadInteractionRange(Identity player)
    {
        Identity asc = Token(M.Q(player.Address + Offset(player, "AbilitySystemComponent", 8)));
        long h = asc.Address + Offset(asc, "SpawnedAttributes", 16), data = M.Q(h);
        int n = M.I(h + 8), cap = M.I(h + 12);
        if (n < 1 || n > 128 || cap < n || cap > 512)
            throw new Exception("Interaction attribute array invalid");
        var ranges = new List<double>();
        int current = Prop(Struct("GameplayAttributeData", 16), "CurrentValue", 4).Offset;
        for (int i = 0; i < n; i++)
        {
            Identity a = Token(M.Q(data + i * 8));
            if (ClassName(a.Class) != "ATR_Movement")
                continue;
            if (M.Q(a.Address + 32) != player.Address && M.Q(a.Address + 32) != asc.Address)
                throw new Exception("Interaction attribute owner changed");
            ranges.Add(M.F(a.Address + Offset(a, "InteractionRange", 16) + current));
        }

        if (ranges.Count != 1 || Double.IsNaN(ranges[0]) || Double.IsInfinity(ranges[0]) || ranges[0] < 20 || ranges[0] > 600 || !Valid(player) || !Valid(asc) || M.Q(h) != data || M.I(h + 8) != n)
            throw new Exception("Interaction range unavailable");
        return ranges[0];
    }

    // 解析装备 Mesh 的实际交互点，仅用于已适配定位。
    ThreatVector ItemMeshAim(Identity actor, Identity itemData)
    {
        Identity mesh = Token(M.Q(itemData.Address + Offset(itemData, "CachedItemMeshComponent", 8)));
        if (!IsA(mesh.Class, "StaticMeshComponent"))
            throw new Exception("Item mesh unavailable");
        Identity asset = Token(M.Q(mesh.Address + Offset(mesh, "StaticMesh", 8)));
        if (!IsA(asset.Class, "StaticMesh"))
            throw new Exception("Item mesh asset unavailable");
        long bounds = asset.Address + Offset(asset, "ExtendedBounds", 56);
        var point = Vector(bounds + Prop(Struct("BoxSphereBounds", 56), "Origin", 24).Offset);
        if (point.Length > 500)
            throw new Exception("Item mesh bounds invalid");
        Identity root = Token(M.Q(actor.Address + Offset(actor, "RootComponent", 8))), component = mesh;
        var chain = new List<Identity>();
        var seen = new HashSet<long>();
        bool rooted = false;
        for (int depth = 0; depth < 8; depth++)
        {
            if (!seen.Add(component.Address) || !IsA(component.Class, "SceneComponent") || ReadBool(component, "bAbsoluteLocation") || ReadBool(component, "bAbsoluteRotation") || ReadBool(component, "bAbsoluteScale"))
                throw new Exception("Item mesh attachment unsupported");
            Identity owner = Token(M.Q(component.Address + 32));
            if (owner.Address != actor.Address && (!IsA(owner.Class, "ItemActor") || M.Q(owner.Address + Offset(owner, "Owner", 8)) != actor.Address))
                throw new Exception("Item mesh owner changed");
            long socket = component.Address + Offset(component, "AttachSocketName", 8);
            if (Name(M.I(socket)) != "None" || M.I(socket + 4) != 0)
                throw new Exception("Item mesh socket unsupported");
            chain.Add(component);
            point = LootSceneTransform.Apply(point, Vector(component.Address + Offset(component, "RelativeLocation", 24)), Vector(component.Address + Offset(component, "RelativeRotation", 24)), Vector(component.Address + Offset(component, "RelativeScale3D", 24)));
            long parent = M.Q(component.Address + Offset(component, "AttachParent", 8));
            if (parent == 0)
            {
                rooted = component.Address == root.Address;
                break;
            }

            component = Token(parent);
        }

        if (!rooted || !point.Valid || (point - Position(actor)).Length > 300 || !Valid(actor) || !Valid(itemData) || !Valid(asset) || chain.Any(c => !Valid(c)) || M.Q(actor.Address + Offset(actor, "RootComponent", 8)) != root.Address)
            throw new Exception("Item mesh identity or transform changed");
        return point;
    }

    // 只读采集玩家状态和附近已有目标，保留未知/不可作用原因。
    public NearbyLootState ReadNearbyLoot(double diagnosticRadius = 1000)
    {
        var state = new NearbyLootState();
        try
        {
            if (Double.IsNaN(diagnosticRadius) || Double.IsInfinity(diagnosticRadius) || diagnosticRadius < 1000 || diagnosticRadius > 300000)
                throw new Exception("Loot diagnostic radius invalid");
            state.Combat = ReadCombatState();
            if (!state.Combat.Known)
                throw new Exception(state.Combat.Reason);
            Identity player = Pawn();
            state.PawnName = LootObjectName(player);
            Identity level = Token(M.Q(player.Address + 32)), world = Token(M.Q(level.Address + Offset(level, "OwningWorld", 8)));
            state.InteractionRange = ReadInteractionRange(player);
            state.Standing = ReadStationaryFloor(player);
            long type = Struct("SWTypeInfo", 16);
            int typeTag = Prop(type, "TypeTag", 8).Offset;
            foreach (var actor in nearbyLootActors.Values.ToArray())
                try
                {
                    if (!Valid(actor))
                    {
                        nearbyLootActors.Remove(actor.Address);
                        continue;
                    }

                    int kind = nearbyLootClasses[actor.Class];
                    string actorType = kind <= 3 || kind == 7 ? Name(M.I(actor.Address + Offset(actor, "TypeInfo", 16) + typeTag)) : ClassName(actor.Class);
                    if (kind == 7 && (!NearbyLootCatalog.Contains(actorType) || !actorType.StartsWith("SW.Item.", StringComparison.Ordinal) && !NearbyLootCatalog.EmeraldPot(actorType)))
                        continue;
                    if (ReadBool(actor, "bHidden") || ReadBool(actor, "bActorIsBeingDestroyed"))
                        continue;
                    Identity actorLevel = Token(M.Q(actor.Address + 32));
                    if (!IsA(actorLevel.Class, "Level") || M.Q(actorLevel.Address + Offset(actorLevel, "OwningWorld", 8)) != world.Address)
                        continue;
                    var position = Position(actor);
                    double distance = (position - state.Combat.Player).Length;
                    if (distance > diagnosticRadius)
                        continue;
                    var target = new NearbyLootTarget
                    {
                        Id = actor.Index + ":" + actor.Serial,
                        ActorName = LootObjectName(actor),
                        Kind = kind == 1 || kind == 6 ? "item" : kind == 5 ? "legacy-loot" : kind == 7 ? "pickup" : "chest",
                        ActorType = actorType,
                        Position = position,
                        Distance = distance
                    };
                    target.Type = target.ActorType;
                    target.InRange = NearbyLootRule.WithinRange(state.Combat, position, state.InteractionRange, NearbyLootCatalog.EmeraldPot(actorType));
                    if (NearbyLootCatalog.EmeraldCurrency(actorType))
                    {
                        target.Kind = "currency";
                        target.Reason = "native emerald currency; game pickup/pull range applies; no equipment UID or mouse-click pickup assumed";
                    }
                    else if (target.Kind == "item")
                    {
                        Identity itemActor = actor;
                        if (kind == 6)
                        {
                            long owner = M.Q(actor.Address + Offset(actor, "Owner", 8));
                            if (owner == 0)
                                continue;
                            itemActor = Token(owner);
                            if (!IsA(itemActor.Class, "SWItemActor"))
                                continue;
                        }

                        Identity component = Token(M.Q(itemActor.Address + Offset(itemActor, "GASItemComponent", 8)));
                        if (!IsA(component.Class, "GASItemComponent") || M.Q(component.Address + 32) != itemActor.Address || !ReadBool(component, "bAssetsLoadComplete"))
                            continue;
                        long item = Struct("ItemData", 192), data = component.Address + Offset(component, "ItemData", 192);
                        string tag = Name(M.I(data + Prop(item, "TypeTag", 8).Offset));
                        if (!tag.StartsWith("SW.Item.", StringComparison.Ordinal))
                            throw new Exception("Dropped item TypeTag not recognized: " + tag);
                        target.Type = tag;
                        if (NearbyLootCatalog.Book(tag))
                            target.Kind = "book";
                        // 消耗品由原生类型和现场 Actor 序列识别，不要求拥有装备背包 UID。
                        byte[] uid = M.Read(data + Prop(item, "SessionUID", 8).Offset, 8);
                        if (NearbyLootCatalog.Food(tag))
                        {
                            target.Kind = "food";
                            target.Reason = "verified native food type; direct eating prototype requires consumption and effect confirmation";
                        }
                        else if (NearbyLootCatalog.Tnt(tag))
                        {
                            target.Kind = "tnt";
                            target.Reason = "native throwable TNT definition and live actor; direct pickup/carry not implemented or verified; no automatic throw";
                        }
                        else
                        {
                            if (!uid.All(b => b == 0))
                                target.ItemId = InventoryIdentity(uid);
                        }

                        long itemOwner = M.Q(itemActor.Address + Offset(itemActor, "Owner", 8));
                        target.LocalOwner = itemOwner == player.Address || target.Kind == "book" && itemOwner == 0;
                        if (target.Kind == "item")
                            target.Reason = target.LocalOwner ? "live dropped item; pickup not yet confirmed" : "item owner not confirmed as local player";
                        target.Id = itemActor.Index + ":" + itemActor.Serial;
                        target.ActorName = LootObjectName(itemActor);
                        try
                        {
                            target.AimPosition = ItemMeshAim(itemActor, component);
                            target.AimKnown = true;
                        }
                        catch (Exception e)
                        {
                            AdaptationRecord.Set("loot.item.aim.fallback", e.Message + "; actor position with same-target prompt required");
                        }

                        if (!Valid(component) || !Valid(itemActor))
                            continue;
                    }
                    else if (kind == 7 && NearbyLootCatalog.EmeraldPot(actorType))
                    {
                        target.Kind = "pot";
                        if (!IsA(actor.Class, "SWASCActor"))
                            continue;
                        target.Tags = OwnedTags(actor).ToArray();
                        target.AimPosition = PotAim(actor, out target.PotRadius);
                        target.AimKnown = true;
                        target.InRange = NearbyLootRule.WithinRange(state.Combat, position, state.InteractionRange, true, target.PotRadius);
                        target.Reason = "native emerald pot definition, target-selection component and ability-system tags; ordinary stationary attack only";
                    }
                    else if (kind == 7)
                    {
                        target.Reason = "native typed world item; gear ItemData and pickup eligibility not established";
                    }
                    else if (kind == 2)
                    {
                        target.Tags = OwnedTags(actor).ToArray();
                        if (target.Tags.Contains("SW.State.Access.Open"))
                        {
                            target.Opened = true;
                            target.Reason = "native chest access-open tag; excluded from new interaction";
                        }
                        else
                            target.Reason = "existing ability-system chest; actual prompt required before interaction";
                    }
                    else if (kind == 4)
                    {
                        target.Opened = ReadBool(actor, "bOpened");
                        target.Reason = "legacy ChestActor; explicit opened state; ability-system reward flow not assumed";
                    }
                    else if (kind == 5)
                    {
                        target.Reason = ReadBool(actor, "bLootUnlocked") ? "legacy unlocked loot; pickup outcome not yet verified" : "legacy locked loot";
                    }
                    else
                    {
                        target.Reason = "visual SWChestActor; authoritative opened state not exposed";
                    }

                    if (Valid(actor) && !state.Targets.Any(t => t.Id == target.Id))
                        state.Targets.Add(target);
                }
                catch (Exception e)
                {
                    if (state.Exclusions.Count < 32)
                        state.Exclusions.Add(actor.Index + ":" + actor.Serial + " " + e.Message);
                }

            if (!Valid(player) || !Valid(world) || InventorySession() != state.Combat.Session)
                throw new Exception("Nearby loot session changed");
            state.Known = true;
            AdaptationRecord.Set("loot.read.source", "reflected SWItemActor.GASItemComponent.ItemData.SessionUID/TypeTag, SWASCChestActor, SWTypeInfo.TypeTag, ATR_Movement.InteractionRange; object serial/world/owner/load guards; no travel or spawning");
        }
        catch (Exception e)
        {
            state.Reason = e.Message;
        }

        return state;
    }

    object NearbyActorDiagnostic(CombatState state)
    {
        var rows = new List<object>();
        foreach (var actor in nearbyLootActors.Values.ToArray())
            try
            {
                if (!Valid(actor) || !IsA(actor.Class, "SWItemActor"))
                    continue;
                var position = Position(actor);
                if ((position - state.Player).Length > 1000)
                    continue;
                Identity component = Token(M.Q(actor.Address + Offset(actor, "GASItemComponent", 8)));
                long data = component.Address + Offset(component, "ItemData", 192);
                string tag = Name(M.I(data + Prop(Struct("ItemData", 192), "TypeTag", 8).Offset));
                rows.Add(new { id = actor.Index + ":" + actor.Serial, type = tag, sessionUidPresent = M.Read(data + Prop(Struct("ItemData", 192), "SessionUID", 8).Offset, 8).Any(b => b != 0), hidden = ReadBool(actor, "bHidden"), destroying = ReadBool(actor, "bActorIsBeingDestroyed"), assetsComplete = ReadBool(component, "bAssetsLoadComplete"), position = position, localOwner = M.Q(actor.Address + Offset(actor, "Owner", 8)) == Pawn().Address });
            }
            catch (Exception e)
            {
                rows.Add(new { id = actor.Index + ":" + actor.Serial, error = e.Message });
            }

        return rows;
    }

    object NearbyPromptDiagnostic()
    {
        var rows = new List<object>();
        foreach (var d in lootIndicators.Values.ToArray())
            try
            {
                if (!Valid(d))
                    continue;
                Identity component = Token(M.Q(d.Address + Offset(d, "Component", 8))), owner = Token(M.Q(component.Address + 32));
                string source = "null";
                try
                {
                    source = ClassName(Token(M.Q(d.Address + Offset(d, "DataObject", 8))).Class);
                }
                catch
                {
                }

                string actorRead = null;
                double? distance = null;
                try
                {
                    distance = (Position(owner) - Position(Pawn())).Length;
                }
                catch (Exception e)
                {
                    actorRead = e.Message;
                }

                rows.Add(new { id = d.Index + ":" + d.Serial, visible = ReadBool(d, "bVisible"), screenOverride = ReadBool(d, "bOverrideScreenPosition"), componentClass = ClassName(component.Class), ownerClass = ClassName(owner.Class), ownerName = Name(owner.Name), tracked = nearbyLootActors.ContainsKey(owner.Address), ownerId = owner.Index + ":" + owner.Serial, dataClass = source, distance = distance, actorRead = actorRead });
            }
            catch
            {
            }

        return rows;
    }

    object NearbyPromptBindingDiagnostic()
    {
        var rows = new List<object>();
        foreach (var o in lootPromptComponents.Values.Concat(lootPromptWidgets.Values))
            try
            {
                if (!Valid(o))
                    continue;
                Identity d = Token(M.Q(o.Address + Offset(o, "IndicatorDescriptor", 8)));
                int visibility = -1;
                float opacity = -1;
                if (IsA(o.Class, "Widget"))
                {
                    visibility = M.Read(o.Address + Offset(o, "Visibility", 1), 1)[0];
                    opacity = M.F(o.Address + Offset(o, "RenderOpacity", 4));
                }

                rows.Add(new { id = o.Index + ":" + o.Serial, type = ClassName(o.Class), descriptorId = d.Index + ":" + d.Serial, widgetVisible = LootPromptWidgetVisible(d), visibility = visibility, opacity = opacity });
            }
            catch
            {
            }

        return rows;
    }

    public void AuditNearbyLoot(string path)
    {
        var state = ReadNearbyLoot();
        var prompts = state.Targets.Select(t => new { t.Id, matches = NearbyPromptMatches(t.Id) }).ToArray();
        File.WriteAllText(path, new JavaScriptSerializer { MaxJsonLength = 4000000 }.Serialize(new { readOnly = true, gameInputs = false, gameWrites = false, githubUpload = false, state = state, prompts = prompts, actors = NearbyActorDiagnostic(state.Combat), indicators = NearbyPromptDiagnostic(), promptBindings = NearbyPromptBindingDiagnostic(), getterVerified = lootGetterKnown, getterReason = lootGetterReason, functions = LootFunctionRecord(), lookupRecord = AdaptationRecord.Contents() }), new UTF8Encoding(true));
        if (!state.Known)
            throw new Exception(state.Reason);
    }

    // 只读采集大型罐子的类型和几何信息。
    public void AuditLargePots(string path)
    {
        var state = ReadNearbyLoot(300000);
        File.WriteAllText(path, new JavaScriptSerializer { MaxJsonLength = 4000000 }.Serialize(new { readOnly = true, gameInputs = false, gameWrites = false, scope = "loaded actors in current world only; no travel", largePots = state.Targets.Where(t => t.Type == "SW.LootActor.Pot.Large.Emerald").OrderBy(t => t.Distance).ToArray(), smallPotCount = state.Targets.Count(t => t.Type == "SW.LootActor.Pot.Small.Emerald"), exclusions = state.Exclusions, reason = state.Reason, known = state.Known, lookupRecord = AdaptationRecord.Contents() }), new UTF8Encoding(true));
        if (!state.Known)
            throw new Exception(state.Reason);
    }
}

// 主窗口的一个 partial 部分；事件处理与异步任务共用主窗口状态，退出时统一清理。
sealed partial class ToolboxForm
{
    NearbyLootState lastNearbyLootState;
    // 汇总启用开关、节流和最近目标状态供日志使用。
    string NearbyLootDiagnosticState()
    {
        return lastNearbyLootState == null ? "nearbyRead=not sampled" : "nearbyRead known=" + lastNearbyLootState.Known + " ageMs=" + (clock.ElapsedMilliseconds - lastNearbyLootRead) + " reason=" + lastNearbyLootState.Reason + " camera=" + (lastNearbyLootState.Combat != null && lastNearbyLootState.Combat.CanAim) + "; " + String.Join("; ", lastNearbyLootState.Targets.Take(12).Select(t => t.Kind + ":" + t.Id + " distance=" + t.Distance.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " inRange=" + t.InRange + " opened=" + t.Opened));
    }

    CheckBox nearbyChestsEnabled, nearbyItemsEnabled, nearbyPotsEnabled, nearbyFoodEnabled, nearbyDirectEnabled, nearbyMovingEnabled;
    OreNumber nearbyInterval, nearbyFoodInterval;
    OreSlider nearbyIntervalSlider, nearbyFoodIntervalSlider;
    bool nearbyTimingUpdating;
    PixelLabel nearbyLootNote;
    long lastNearbyLootRead = -10000;
    readonly NearbyLootRule nearbyLootRule = new NearbyLootRule();
    readonly NearbyLootTiming nearbyLootTiming = new NearbyLootTiming();
    // 同步滑块/数字框与配置的毫秒间隔，避免递归重绘。
    void NearbyTimingChanged(bool food, bool fromSlider)
    {
        if (nearbyTimingUpdating)
            return;
        nearbyTimingUpdating = true;
        try
        {
            var number = food ? nearbyFoodInterval : nearbyInterval;
            var slider = food ? nearbyFoodIntervalSlider : nearbyIntervalSlider;
            if (fromSlider)
                number.Value = NearbyIntervalScale.Milliseconds(slider.Value);
            else
                slider.Value = NearbyIntervalScale.Position((int)number.Value);
        }
        finally
        {
            nearbyTimingUpdating = false;
        }

        Changed();
    }

    // 原生模式允许手动跑动但保留攻击/技能冲突；旧输入路径有独立条件。
    bool NearbyInputReady(long now)
    {
        return settings.NearbyAllowMoving || CombatUserIdle(now);
    }

    // 判断当前目标类型对应的交互间隔是否已到。
    bool NearbyDue(NearbyLootTarget t)
    {
        return nearbyLootTiming.Ready(t.Kind, clock.ElapsedMilliseconds, settings.NearbyIntervalMs, settings.NearbyFoodIntervalMs);
    }

    // 从只读快照选一个目标，满足开关、输入和节流后排队一次请求。
    void PollNearbyLoot(bool active, long now)
    {
        if (!settings.NearbyDirect)
        {
            if (active && (settings.NearbyChests || settings.NearbyItems || settings.NearbyPots || settings.NearbyFood))
                nearbyLootNote.Text = L10n.T("需要蓝图加载器及直接收集组件；不移动鼠标，不需选中");
            return;
        }

        if (!active || (!settings.NearbyChests && !settings.NearbyItems && !settings.NearbyPots && !settings.NearbyFood) || pressing || nearbyObservation.Busy || requests.Count > 0 || now - lastNearbyLootRead < 100)
            return;
        lastNearbyLootRead = now;
        var state = reader.ReadNearbyLoot();
        lastNearbyLootState = state;
        nearbyLootNote.Text = NearbyLootCaption(state);
        string blocked = !state.Known ? state.Reason : !settings.NearbyAllowMoving && state.Combat.PlayerVelocity.Length > 30 ? "player moving" : "ready; targets=" + state.Targets.Count + " inRange=" + state.Targets.Count(t => t.InRange);
        ToolboxLog.Limited("Loot.Status", blocked);
        var target = nearbyLootRule.Select(state, settings.NearbyChests, settings.NearbyItems, settings.NearbyPots, clock.ElapsedMilliseconds, settings.NearbyFood, settings.NearbyDirect, settings.NearbyAllowMoving, NearbyDue);
        string note = !state.Known ? L10n.T("附近目标读数不可用，暂停交互") : !settings.NearbyAllowMoving && state.Combat.PlayerVelocity.Length > 30 ? L10n.T("角色正在移动，站定后尝试附近交互") : null;
        if (note == null && target == null && nearbyLootRule.Select(state, settings.NearbyChests, settings.NearbyItems, settings.NearbyPots, now, settings.NearbyFood, settings.NearbyDirect, settings.NearbyAllowMoving) != null)
            note = L10n.T("等待交互间隔");
        if (note == null && target == null)
        {
            var nearest = state.Targets.Where(t => settings.NearbyChests && t.Kind == "chest" && t.Opened != true || settings.NearbyItems && (t.Kind == "item" || t.Kind == "book" || t.Kind == "tnt") || settings.NearbyFood && t.Kind == "food" || settings.NearbyPots && t.Kind == "pot").OrderBy(t => t.Distance).FirstOrDefault();
            note = nearest == null ? L10n.T("没有可执行的已启用目标") : !nearest.InRange ? String.Format(L10n.T("最近目标距离 {0}，超出当前交互范围"), nearest.Distance.ToString("0")) : L10n.T("目标已尝试或暂不符合交互条件");
        }

        if (note != null)
        {
            nearbyLootNote.Text = NearbyLootCaption(state) + "\n" + note;
            ToolboxLog.Limited("Loot.Selection", note);
            return;
        }

        if (!NearbyInputReady(now))
        {
            nearbyLootNote.Text = NearbyLootCaption(state) + "\n" + L10n.T("手动输入中，暂停附近交互");
            ToolboxLog.Limited("Loot.Blocked", "manual input; target=" + target.Id);
            return;
        }

        if (!LootGround(state, target, now))
        {
            ToolboxLog.Limited("Loot.Blocked", "stationary floor/range unavailable; target=" + target.Id);
            return;
        }

        requests.Enqueue(new InputRequest { Keys = new int[0], Description = L10n.T(target.Kind == "pot" ? "尝试击破绿宝石罐" : target.Kind == "food" ? "尝试食用地上食物" : target.Kind == "book" ? "尝试拾取地上附魔书" : target.Kind == "item" ? "尝试拾取地上装备" : "尝试交互附近宝箱"), NearbyLoot = true, TargetId = target.Id, CombatSession = state.Combat.Session, Generation = generation, Expires = clock.ElapsedMilliseconds + 1500 });
    }

    // 按目标类型生成界面状态说明。
    static string NearbyLootCaption(NearbyLootState state)
    {
        if (state == null || !state.Known)
            return L10n.T("附近目标读数不可用，暂停交互");
        string foods = String.Join(" / ", state.Targets.Where(t => t.Kind == "food").Select(t => EquipmentGamePresentation.Name(t.Type, L10n.Language)).Distinct().Take(3));
        int emeralds = state.Targets.Count(t => t.Kind == "currency");
        string detail = emeralds > 0 ? String.Format(L10n.T("绿宝石 {0}：需进入游戏原生拾取范围"), emeralds) : foods.Length == 0 ? L10n.T("只交互身边目标；不寻路") : foods;
        return String.Format(L10n.T("宝箱 {0} · 装备 {1} · 食物 {2} · 罐子 {3}\n{4}"), state.Targets.Count(t => t.Kind == "chest"), state.Targets.Count(t => t.Kind == "item"), state.Targets.Count(t => t.Kind == "food"), state.Targets.Count(t => t.Kind == "pot"), L10n.T("附魔书") + " " + state.Targets.Count(t => t.Kind == "book") + " · TNT " + state.Targets.Count(t => t.Kind == "tnt") + " · " + detail);
    }

    // 在执行前再次读取地面状态，避免使用过期快照。
    bool LootGround(NearbyLootState nearby, NearbyLootTarget target, long now)
    {
        // 此操作不走路；核对玩家原生地面状态，墙边或导航网格外宝箱还需自身提示证据。
        return nearby.Standing != null && nearby.Standing.Supported && NearbyLootRule.WithinRange(nearby.Combat, target.Position, nearby.InteractionRange, target.Kind == "pot", target.PotRadius);
    }

    // 执行已排队目标；原生模式走直接组件，旧点击路径保持独立。
    async Task RunNearbyLoot(InputRequest request, CancellationToken cancel)
    {
        if (!settings.NearbyDirect)
            return;
        if (!Active(request) || cancel.IsCancellationRequested || request.Generation != generation || clock.ElapsedMilliseconds > request.Expires || !NearbyInputReady(clock.ElapsedMilliseconds))
            return;
        var state = reader.ReadNearbyLoot();
        var target = nearbyLootRule.Select(state, settings.NearbyChests, settings.NearbyItems, settings.NearbyPots, clock.ElapsedMilliseconds, settings.NearbyFood, settings.NearbyDirect, settings.NearbyAllowMoving, NearbyDue);
        if (target == null || target.Id != request.TargetId || state.Combat.Session != request.CombatSession || !LootGround(state, target, clock.ElapsedMilliseconds))
            return;
        await RunDirectLoot(request, state, target, cancel);
    }
}

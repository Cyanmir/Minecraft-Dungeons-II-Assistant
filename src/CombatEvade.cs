// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 读取闪避能力/充能，检查地面走廊并规划一次原生闪避。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

sealed class RollEligibility
{
    public bool Known, TagsAllow;
    public double BaseSpeed, BaseDuration, GroundEnvelope;
    public string Reason;
    public string[] RequiredTags, BlockedTags;
}

sealed class EvadePlan
{
    public string ThreatId, Kind;
    public ThreatVector Direction, Aim;
}

static class EvadeGroundRule
{
    sealed class Interval
    {
        public double Start, End;
        public NavigationPolygon Polygon;
    }

    static double Cross(ThreatVector a, ThreatVector b)
    {
        return a.X * b.Y - a.Y * b.X;
    }

    static double SignedArea(IList<ThreatVector> polygon)
    {
        double area = 0;
        for (int i = 0; i < polygon.Count; i++)
            area += Cross(polygon[i], polygon[(i + 1) % polygon.Count]);
        return area * .5;
    }

    // 求线段交点并处理平行/边界情况。
    static List<ThreatVector> Intersect(IList<ThreatVector> polygon, IList<ThreatVector> clip)
    {
        var output = polygon.ToList();
        double sign = SignedArea(clip) > 0 ? 1 : -1;
        for (int i = 0; i < clip.Count && output.Count > 0; i++)
        {
            var a = clip[i];
            var edge = clip[(i + 1) % clip.Count] - a;
            var input = output;
            output = new List<ThreatVector>();
            var previous = input[input.Count - 1];
            double before = sign * Cross(edge, previous - a);
            foreach (var current in input)
            {
                double after = sign * Cross(edge, current - a);
                if ((before >= 0) != (after >= 0))
                {
                    double t = before / (before - after);
                    output.Add(previous + (current - previous) * t);
                }

                if (after >= 0)
                    output.Add(current);
                previous = current;
                before = after;
            }
        }

        return output;
    }

    // 核对通道宽度和覆盖，避免只检查一条无限细的中心线。
    static bool WideCoverage(NavigationCapture mesh, ThreatVector start, ThreatVector direction, double distance, double radius)
    {
        var side = new ThreatVector(-direction.Y, direction.X, 0) * (radius + 10);
        var end = direction * distance;
        var rectangle = new[]
        {
            side * -1,
            side,
            end + side,
            end - side
        };
        var pieces = new List<List<ThreatVector>>();
        foreach (var poly in mesh.Polygons)
        {
            if (poly.Vertices == null || poly.Vertices.Any(v => !v.Valid) || Math.Abs(poly.Center.Z - start.Z) > 45 || poly.Vertices.Max(v => v.Z) - poly.Vertices.Min(v => v.Z) > 50)
                continue;
            var piece = Intersect(poly.Vertices.Select(v => v - start).ToArray(), rectangle);
            if (piece.Count >= 3 && Math.Abs(SignedArea(piece)) > .000001)
                pieces.Add(piece);
            if (pieces.Count > 512)
                return false;
        }

        // Recast 多边形分割同一地面；多个重叠候选地面属于歧义情况。
        for (int i = 0; i < pieces.Count; i++)
            for (int j = i + 1; j < pieces.Count; j++)
            {
                var overlap = Intersect(pieces[i], pieces[j]);
                if (overlap.Count >= 3 && Math.Abs(SignedArea(overlap)) > .001)
                    return false;
            }

        double area = pieces.Sum(p => Math.Abs(SignedArea(p))), expected = distance * (radius + 10) * 2;
        return Math.Abs(area - expected) <= Math.Max(.001, expected * .0000001);
    }

    // 裁剪检查完整线段，不能只采几个点而漏掉地面缺口。
    // 将通道与地面多边形裁剪得到可用区域。
    static Interval Clip(NavigationPolygon polygon, ThreatVector start, ThreatVector end)
    {
        var vertices = polygon.Vertices;
        if (vertices == null || vertices.Length < 3 || vertices.Any(v => !v.Valid) || Math.Abs(polygon.Center.Z - start.Z) > 45 || vertices.Max(v => v.Z) - vertices.Min(v => v.Z) > 50)
            return null;
        double area = 0;
        for (int i = 0; i < vertices.Length; i++)
            area += Cross(vertices[i], vertices[(i + 1) % vertices.Length]);
        if (Math.Abs(area) < .01)
            return null;
        double sign = area > 0 ? 1 : -1, lo = 0, hi = 1;
        var direction = end - start;
        for (int i = 0; i < vertices.Length; i++)
        {
            var a = vertices[i];
            var edge = vertices[(i + 1) % vertices.Length] - a;
            double inside = sign * Cross(edge, start - a), slope = sign * Cross(edge, direction);
            if (Math.Abs(slope) < 1e-9)
            {
                if (inside < -.0001)
                    return null;
                continue;
            }

            double at = -inside / slope;
            if (slope > 0)
                lo = Math.Max(lo, at);
            else
                hi = Math.Min(hi, at);
            if (lo > hi + .0000001)
                return null;
        }

        return new Interval
        {
            Start = Math.Max(0, lo),
            End = Math.Min(1, hi),
            Polygon = polygon
        };
    }

    static bool GroundLine(NavigationCapture mesh, ThreatVector start, ThreatVector end)
    {
        var intervals = mesh.Polygons.Select(p => Clip(p, start, end)).Where(i => i != null).ToArray();
        var reached = new HashSet<int>();
        double covered = 0;
        foreach (var i in intervals.Where(i => i.Start <= .0000001 && i.End >= 0))
        {
            reached.Add(i.Polygon.Id);
            covered = Math.Max(covered, i.End);
        }

        if (reached.Count == 0)
            return false;
        bool changed = true;
        while (changed && covered < 1 - .0000001)
        {
            changed = false;
            foreach (var i in intervals)
            {
                if (reached.Contains(i.Polygon.Id) || i.Start > covered + .0000001 || !i.Polygon.Portals.Keys.Any(reached.Contains))
                    continue;
                // 相邻多边形必须沿该线段共享连续边界，只有公共顶点不能当成通道。
                if (!intervals.Any(j => reached.Contains(j.Polygon.Id) && i.Polygon.Portals.ContainsKey(j.Polygon.Id) && i.Start <= j.End + .0000001 && j.Start <= i.End + .0000001))
                    continue;
                reached.Add(i.Polygon.Id);
                covered = Math.Max(covered, i.End);
                changed = true;
            }
        }

        return covered >= 1 - .0000001;
    }

    // 核对整段闪避路径的宽度、地面和危险覆盖。
    public static bool Corridor(NavigationCapture mesh, ThreatVector start, ThreatVector direction, double distance, double radius)
    {
        if (mesh == null || !start.Valid || !direction.Valid || distance < 50 || distance > 2500 || radius < 5 || radius > 200 || Math.Abs(direction.Z) > .0001 || Math.Abs(direction.Length - 1) > .001)
            return false;
        var end = start + direction * distance;
        var side = new ThreatVector(-direction.Y, direction.X, 0) * (radius + 10);
        return GroundLine(mesh, start, end) && GroundLine(mesh, start + side, end + side) && GroundLine(mesh, start - side, end - side) && WideCoverage(mesh, start, direction, distance, radius);
    }

    // 在原生距离和当前充能限制下选择安全闪避方向。
    public static EvadePlan Plan(CombatState state, ThreatFrame threats, NavigationCapture mesh, HashSet<string> consumed, Func<ThreatVector, bool> aimAllowed = null, bool native = false)
    {
        if (state == null || state.PlayerVelocity.Length > 100 || !state.CanAttempt || state.Roll == null || !state.Roll.Known || !state.Roll.TagsAllow || !state.Readiness.HasRollCharge || (!native && state.DodgeKey == 0) || threats == null || !threats.Known || mesh == null || mesh.Session != state.Session)
            return null;
        var ground = state.Player;
        ground.Z -= state.HalfHeight;
        foreach (var threat in threats.Threats.Where(t => t.RadiusKnown && !consumed.Contains(t.Id) && (t.Kind == "projectile" && t.Seconds >= .08 && t.Seconds <= .45 || t.Kind == "melee" && t.Melee != null && t.Seconds >= .03 && t.Seconds <= .12)).OrderBy(t => t.Seconds))
        {
            ThreatVector[] directions;
            if (threat.Kind == "melee")
            {
                var away = threat.Melee.Geometry.Away;
                away.Z = 0;
                double length = away.Length;
                if (length < .001)
                    continue;
                away = away * (1 / length);
                var side = new ThreatVector(-away.Y, away.X, 0);
                directions = new[]
                {
                    away,
                    (away + side) * Math.Sqrt(.5),
                    (away - side) * Math.Sqrt(.5),
                    side,
                    side * -1
                };
            }
            else
            {
                double horizontal = Math.Sqrt(threat.Velocity.X * threat.Velocity.X + threat.Velocity.Y * threat.Velocity.Y);
                if (horizontal < 10)
                    continue;
                var incoming = new ThreatVector(threat.Velocity.X / horizontal, threat.Velocity.Y / horizontal, 0);
                var side = new ThreatVector(-incoming.Y, incoming.X, 0);
                directions = new[]
                {
                    side,
                    side * -1,
                    (side + incoming) * Math.Sqrt(.5),
                    (incoming - side) * Math.Sqrt(.5)
                };
            }

            foreach (var direction in directions)
            {
                if (!Corridor(mesh, ground, direction, state.Roll.GroundEnvelope, state.Radius) || aimAllowed != null && !aimAllowed(state.Player + direction * 300))
                    continue;
                // 保守线性预测仅检查候选路径，不宣称角色进入无敌状态。
                if (threats.ProjectilePaths.Any(path => !Double.IsNaN(ThreatRule.Impact(path.Position - state.Player, path.Velocity - direction * state.Roll.BaseSpeed, state.Radius + path.Radius, state.HalfHeight + path.Radius, .6))))
                    continue;
                if (threats.Threats.Any(path => path.Melee != null && !MeleeThreatTracker.EscapeClear(path, state, direction)))
                    continue;
                return new EvadePlan
                {
                    ThreatId = threat.Id,
                    Kind = threat.Kind,
                    Direction = direction,
                    Aim = state.Player + direction * 300
                };
            }
        }

        return null;
    }

}

sealed partial class HealthReader
{
    readonly Dictionary<long, Identity> rollAbilities = new Dictionary<long, Identity>();
    // 读取闪避能力所需的原生状态标签。
    List<string> AbilityTagContainer(Identity ability, string field)
    {
        long h = ability.Address + Offset(ability, field, 32), data = M.Q(h);
        int n = M.I(h + 8), cap = M.I(h + 12);
        if (n < 0 || n > 256 || cap < n || cap > 1024)
            throw new Exception("Roll tag container invalid");
        var tags = new List<string>();
        for (int i = 0; i < n; i++)
        {
            if (M.I(data + i * 8 + 4) != 0)
                throw new Exception("Numbered roll tag invalid");
            tags.Add(Name(M.I(data + i * 8)));
        }

        if (!Valid(ability) || M.Q(h) != data || M.I(h + 8) != n)
            throw new Exception("Roll tags changed");
        return tags;
    }

    double BaseRollFloat(Identity ability, string name)
    {
        long st = Struct("SWFloatAKV", 96), at = ability.Address + Offset(ability, name, 96);
        if (M.Read(at + Prop(st, "Type", 1).Offset, 1)[0] != 1)
            throw new Exception("Nonconstant base roll parameter unsupported");
        return M.F(at + Prop(st, "float", 4).Offset);
    }

    RollEligibility ReadRollEligibility(Identity player, CombatReadiness readiness)
    {
        var result = new RollEligibility();
        try
        {
            if (!CombatDefinitions.MatchesVersion(AdaptationRecord.Get("game.sha256")))
                throw new Exception("Roll base model not checked for this game build");
            var models = new List<RollEligibility>();
            foreach (var ability in rollAbilities.Values.ToArray())
            {
                if (!Valid(ability))
                {
                    rollAbilities.Remove(ability.Address);
                    continue;
                }

                if (M.Q(ability.Address + 32) != player.Address)
                    continue;
                var model = new RollEligibility
                {
                    BaseSpeed = BaseRollFloat(ability, "RollStrength"),
                    BaseDuration = BaseRollFloat(ability, "RollDuration"),
                    RequiredTags = AbilityTagContainer(ability, "ActivationRequiredTags").ToArray(),
                    BlockedTags = AbilityTagContainer(ability, "ActivationBlockedTags").ToArray()
                };
                if (model.BaseSpeed < 100 || model.BaseSpeed > 2000 || model.BaseDuration < .1 || model.BaseDuration > 1)
                    throw new Exception("Roll base envelope invalid");
                models.Add(model);
            }

            if (models.Count < 1 || models.Any(m => Math.Abs(m.BaseSpeed - models[0].BaseSpeed) > .001 || Math.Abs(m.BaseDuration - models[0].BaseDuration) > .001 || !m.RequiredTags.OrderBy(t => t).SequenceEqual(models[0].RequiredTags.OrderBy(t => t)) || !m.BlockedTags.OrderBy(t => t).SequenceEqual(models[0].BlockedTags.OrderBy(t => t))))
                throw new Exception("Ambiguous roll base definitions");
            result = models[0];
            result.GroundEnvelope = Math.Max(800, result.BaseSpeed * result.BaseDuration * 2 + 100);
            if (result.GroundEnvelope > 2500)
                throw new Exception("Roll envelope too large for local evasion");
            Func<string, bool> has = tag => readiness.PlayerTags != null && readiness.PlayerTags.Any(t => t == tag || t.StartsWith(tag + ".", StringComparison.Ordinal));
            result.TagsAllow = result.RequiredTags.All(has) && !result.BlockedTags.Any(has);
            result.Known = Valid(player) && readiness.Known;
            result.Reason = "Base roll constants and activation tags read; game resolves configured overrides and final activation. Ground corridor is a conservative candidate check, not guaranteed collision-free or invulnerability.";
            AdaptationRecord.Set("evade.roll.model", "GA_Roll reflected RollStrength/RollDuration (SWFloatAKV constant), required/blocked activation tags; speed=" + result.BaseSpeed + " duration=" + result.BaseDuration + " ground envelope=" + result.GroundEnvelope + "; ordinary DirectionalDodge input; native activation remains authoritative");
        }
        catch (Exception e)
        {
            result.Known = false;
            result.TagsAllow = false;
            result.Reason = e.Message;
        }

        return result;
    }

    // 建立用于只读地面检查的独立遥测读取器。
    public Func<HealthReader> NavigationWorker()
    {
        int pid = Pid;
        long started = process.StartTime.ToUniversalTime().Ticks, image = process.MainModule.BaseAddress.ToInt64(), knownPool = pool, knownObjects = objects;
        Identity knownLocal = local;
        var knownStructs = new Dictionary<string, long>(structs);
        var predicate = executable;
        return delegate
        {
            return new HealthReader(pid, started, image, knownPool, knownObjects, knownLocal, knownStructs, predicate);
        };
    }

    HealthReader(int pid, long started, long image, long knownPool, long knownObjects, Identity knownLocal, Dictionary<string, long> knownStructs, Func<long, int, bool> predicate)
    {
        process = System.Diagnostics.Process.GetProcessById(pid);
        try
        {
            if (process.StartTime.ToUniversalTime().Ticks != started || process.MainModule.BaseAddress.ToInt64() != image)
                throw new Exception("Navigation worker process changed");
            M = new Memory(pid);
            pool = knownPool;
            objects = knownObjects;
            local = knownLocal;
            structs = knownStructs;
            executable = predicate;
            if (Name(0) != "None" || !Valid(local))
                throw new Exception("Navigation worker identity invalid");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    double ProjectileBoundRadius(Identity projectile)
    {
        Identity root = Token(M.Q(projectile.Address + Offset(projectile, "RootComponent", 8)));
        var scale = Vector(root.Address + Offset(root, "RelativeScale3D", 24));
        double maximum = Math.Max(Math.Abs(scale.X), Math.Max(Math.Abs(scale.Y), Math.Abs(scale.Z))), radius;
        if (maximum <= 0 || maximum > 10)
            return Double.NaN;
        if (IsA(root.Class, "SphereComponent"))
            radius = M.F(root.Address + Offset(root, "SphereRadius", 4));
        else if (IsA(root.Class, "CapsuleComponent"))
            radius = Math.Max(M.F(root.Address + Offset(root, "CapsuleRadius", 4)), M.F(root.Address + Offset(root, "CapsuleHalfHeight", 4)));
        else if (IsA(root.Class, "BoxComponent"))
            radius = Vector(root.Address + Offset(root, "BoxExtent", 24)).Length;
        else
            return Double.NaN;
        radius *= maximum;
        return Valid(root) && Valid(projectile) && radius >= 1 && radius <= 200 ? radius : Double.NaN;
    }

}

// 主窗口的一个 partial 部分；事件处理与异步任务共用主窗口状态，退出时统一清理。
sealed partial class ToolboxForm
{
    CheckBox combatEvadeEnabled;
    PixelLabel evadeRuntimeNote;
    NavigationCapture evadeGround;
    bool evadeGroundBusy;
    long lastEvadeGround = -10000, lastEvadeGroundAttempt = -10000, lastEvadeRead = -10000, lastEvade = -10000;
    readonly HashSet<string> evadedThreats = new HashSet<string>();
    // 刷新地面快照，保持采样归属和刷新时间限制。
    async void RefreshEvadeGround(CombatState state, long now)
    {
        if (evadeGroundBusy || now - lastEvadeGroundAttempt < 5000)
            return;
        evadeGroundBusy = true;
        lastEvadeGroundAttempt = now;
        int capturedGeneration = generation, pid = reader.Pid;
        var factory = reader.NavigationWorker();
        try
        {
            var mesh = await Task.Run(delegate
            {
                using (var worker = factory())
                    return worker.ReadNavigation();
            });
            if (!closing && reader != null && reader.Pid == pid && generation == capturedGeneration && mesh.Session == state.Session)
            {
                evadeGround = mesh;
                lastEvadeGround = clock.ElapsedMilliseconds;
            }
        }
        catch (Exception e)
        {
            evadeGround = null;
            ToolboxLog.Limited("Evade.GroundUnavailable", e.Message);
        }
        finally
        {
            evadeGroundBusy = false;
        }
    }

    bool EvadeKeyConflict(int key)
    {
        return key == 0 || settings.ComboEnabled && key == settings.ComboTrigger || settings.JumpEnabled && key == settings.JumpTrigger;
    }

    // 依据当前威胁和地面证据决定是否提交一次闪避请求。
    void PollEvade(bool active, long now)
    {
        if (!settings.CombatEvade || !active || (!UseNativeCombat && XboxPad.ActiveMode) || pressing || now - lastEvadeRead < 75)
            return;
        lastEvadeRead = now;
        var state = reader.ReadCombatState();
        if (!(UseNativeCombat ? state.CanAttempt : state.CanAim) || state.Roll == null || !state.Roll.Known || !state.Roll.TagsAllow || !state.Readiness.HasRollCharge || (!UseNativeCombat && EvadeKeyConflict(state.DodgeKey)))
        {
            evadeRuntimeNote.Text = L10n.T("闪避等待充能、游戏键位和地面状态");
            return;
        }

        RefreshEvadeGround(state, now);
        if (evadeGround == null || evadeGround.Session != state.Session || now - lastEvadeGround > 8000)
        {
            evadeRuntimeNote.Text = L10n.T("正在读取附近可通行地面");
            return;
        }

        Func<ThreatVector, bool> aimAllowed = null;
        if (!UseNativeCombat)
        {
            Rectangle viewport;
            if (!ToolboxInput.ClientArea(reader.Window, reader.Pid, out viewport))
                return;
            aimAllowed = delegate (ThreatVector candidate)
            {
                Point screen;
                return state.Camera.Project(candidate, viewport, out screen);
            };
        }

        var frame = reader.Threats(now);
        var live = new HashSet<string>(frame.Threats.Select(t => t.Id));
        evadedThreats.RemoveWhere(id => !live.Contains(id));
        var plan = EvadeGroundRule.Plan(state, frame, evadeGround, evadedThreats, aimAllowed, UseNativeCombat);
        evadeRuntimeNote.Text = String.Format(L10n.T("闪避充能 {0} · 可核对危险 {1}"), state.Readiness.RollCharges, frame.Threats.Count(t => t.RadiusKnown));
        if (plan == null || now - lastEvade < 800 || requests.Any(r => r.CombatEvade) || (GameForeground() && ToolboxInput.Modifiers()) || new[]
        {
            1,
            2,
            4,
            5,
            6,
            65,
            68,
            83,
            87
        }.Any(GameInputHeld))
            return;
        // 排队攻击不能延迟危险响应；已有恢复请求继续保留。
        var response = new InputRequest
        {
            Keys = new int[0],
            Description = L10n.T(plan.Kind == "melee" ? "近战攻击自动闪避" : "直线弹道自动闪避"),
            CombatEvade = true,
            ThreatId = plan.ThreatId,
            CombatSession = state.Session,
            Generation = generation,
            Expires = clock.ElapsedMilliseconds + 100
        };
        requests = new Queue<InputRequest>(new[] { response }.Concat(requests.Where(r => !r.CombatAttack)));
    }

    // 执行已经校验的旧输入闪避请求并负责按键释放。
    async Task RunCombatEvade(InputRequest request, CancellationToken cancel)
    {
        if (UseNativeCombat)
        {
            await RunNativeEvade(request, cancel);
            return;
        }

        CombatState state;
        if (!settings.CombatEvade || XboxPad.ActiveMode || !ValidateCombatRequest(request, out state) || !state.CanAim || EvadeKeyConflict(state.DodgeKey) || evadeGround == null || clock.ElapsedMilliseconds - lastEvadeGround > 8000)
            return;
        Func<ThreatVector, bool> aimAllowed = null;
        if (!UseNativeCombat)
        {
            Rectangle viewport;
            if (!ToolboxInput.ClientArea(reader.Window, reader.Pid, out viewport))
                return;
            aimAllowed = delegate (ThreatVector candidate)
            {
                Point screen;
                return state.Camera.Project(candidate, viewport, out screen);
            };
        }

        var frame = reader.Threats(clock.ElapsedMilliseconds);
        var plan = EvadeGroundRule.Plan(state, frame, evadeGround, evadedThreats, aimAllowed, UseNativeCombat);
        if (plan == null || plan.ThreatId != request.ThreatId || cancel.IsCancellationRequested || !Active(request) || request.Generation != generation || clock.ElapsedMilliseconds > request.Expires || ToolboxInput.Busy(new[] { state.DodgeKey }) || new[]
        {
            1,
            2,
            4,
            5,
            6,
            65,
            68,
            83,
            87
        }.Any(GameInputHeld))
            return;
        Rectangle area;
        Point aim, previous;
        IntPtr window = reader.Window;
        int pid = reader.Pid;
        if (!ToolboxInput.ClientArea(window, pid, out area) || !state.Camera.Project(plan.Aim, area, out aim) || !ToolboxInput.CombatCursor(out previous))
            return;
        combatLease = new CombatPressLease(up =>
        {
            if (!up && (!ToolboxInput.Front(pid) || cancel.IsCancellationRequested))
                throw new OperationCanceledException();
            ToolboxInput.Send(new[] { state.DodgeKey }, up);
        }, up =>
        {
        }, () => ToolboxInput.RestoreAim(pid, aim, previous));
        ToolboxInput.Aim(window, pid, aim);
        combatLease.Press();
        lastEvade = clock.ElapsedMilliseconds;
        evadedThreats.Add(plan.ThreatId);
        ToolboxLog.Write("Combat.Evade", "directional normal input; threat=" + plan.ThreatId + " key=" + state.DodgeKey + " ground corridor checked; native activation authoritative");
        await Task.Delay(60, cancel);
    }
}

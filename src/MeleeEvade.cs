// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 按多帧接触几何跟踪近战运动并判断闪避方向是否安全。
using System;
using System.Collections.Generic;
using System.Linq;

// 短期估计来自实际命名骨骼姿态，不由静态动画名/半径触发；伤害和闪避接受条件仍由游戏决定。
// MeleeTrajectory 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
sealed class MeleeTrajectory
{
    public CombatGeometry Geometry;
    public ThreatVector StartVelocity, EndVelocity;
    public double Starts, Ends, Horizon = .12;
    // 按给定时刻估计已核实近战轨迹位置。
    public CombatGeometry At(double seconds, ThreatVector player, double radius, double height)
    {
        return ContactGeometry.Capsule(Geometry.Start + StartVelocity * seconds, Geometry.End + EndVelocity * seconds, Geometry.Radius, player, radius, height);
    }
}

sealed class MeleeThreatTracker
{
    // Sample 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
    sealed class Sample
    {
        public long Time;
        public double Position;
        public CombatGeometry Geometry;
        public ThreatVector StartVelocity, EndVelocity;
        public int Stable, Cycle;
    }

    readonly Dictionary<string, Sample> samples = new Dictionary<string, Sample>();
    // 删除过期或失去对象归属的轨迹样本。
    public void Prune(long now)
    {
        foreach (string key in samples.Where(p => now - p.Value.Time > 500).Select(p => p.Key).ToArray())
            samples.Remove(key);
    }

    // 把当前几何样本加入多帧跟踪，避免仅凭一次动画观察判为命中。
    public EnemyThreat Observe(CombatObservation observation, long now, ThreatVector player, ThreatVector playerVelocity, double radius, double height)
    {
        if (observation == null || !observation.TimingKnown || !observation.AssetTimelineMatched || !observation.Playing || !observation.Position.HasValue || !observation.EffectiveRate.HasValue || !observation.NextAttackEventSeconds.HasValue || observation.Geometry == null || !observation.Geometry.Known || observation.Geometry.PoseAgeFrames > 2 || observation.Geometry.PoseAgeFrames < 0)
            return null;
        var geometry = observation.Geometry;
        var window = observation.EventWindows == null ? null : observation.EventWindows.SingleOrDefault(w => w.Index == observation.ContactEvent);
        if (window == null || window.Kind != "ANS_AnimationBasedComplexCollisionMeleeAttack" || !window.FullyParsed || window.Chance != 1 || window.End <= window.Start)
            return null;
        double position = observation.Position.Value, rate = observation.EffectiveRate.Value;
        if (Double.IsNaN(position) || Double.IsInfinity(position) || Double.IsNaN(rate) || Double.IsInfinity(rate) || rate <= 0 || rate > 8 || String.IsNullOrEmpty(observation.ActorId))
            return null;
        string key = observation.ActorId + ":" + window.Index;
        Sample previous;
        samples.TryGetValue(key, out previous);
        var current = new Sample
        {
            Time = now,
            Position = position,
            Geometry = geometry,
            Cycle = previous == null ? 0 : previous.Cycle
        };
        if (previous == null)
        {
            samples[key] = current;
            return null;
        }

        double elapsed = (now - previous.Time) / 1000.0;
        bool sameShape = geometry.StartBone == previous.Geometry.StartBone && geometry.EndBone == previous.Geometry.EndBone && Math.Abs(geometry.Radius - previous.Geometry.Radius) <= .01;
        if (elapsed >= 0 && elapsed < .025 && position >= previous.Position && sameShape && (geometry.Start - previous.Geometry.Start - previous.StartVelocity * elapsed).Length <= 8 && (geometry.End - previous.Geometry.End - previous.EndVelocity * elapsed).Length <= 8)
        {
            // 同帧复查沿用上一次样本对，避免重置历史和除以极小时间差。
            if (previous.Stable < 2)
                return null;
            current.StartVelocity = previous.StartVelocity;
            current.EndVelocity = previous.EndVelocity;
            current.Stable = previous.Stable;
        }
        else
        {
            samples[key] = current;
            if (elapsed < .025 || elapsed > .16 || position <= previous.Position || !sameShape)
            {
                current.Cycle++;
                return null;
            }

            current.StartVelocity = (geometry.Start - previous.Geometry.Start) * (1 / elapsed);
            current.EndVelocity = (geometry.End - previous.Geometry.End) * (1 / elapsed);
            if (!current.StartVelocity.Valid || !current.EndVelocity.Valid || current.StartVelocity.Length > 12000 || current.EndVelocity.Length > 12000)
                return null;
            bool stable = previous.Stable > 0 && (current.StartVelocity - previous.StartVelocity).Length <= Math.Max(150, current.StartVelocity.Length * .25) && (current.EndVelocity - previous.EndVelocity).Length <= Math.Max(150, current.EndVelocity.Length * .25);
            current.Stable = stable ? previous.Stable + 1 : 1;
            if (current.Stable < 2)
                return null;
        }

        var path = new MeleeTrajectory
        {
            Geometry = geometry,
            StartVelocity = current.StartVelocity,
            EndVelocity = current.EndVelocity,
            Starts = Math.Max(0, (window.Start - position) / rate),
            Ends = Math.Min(.12, (window.End - position) / rate)
        };
        if (path.Starts > .12 || path.Ends < .03 || !player.Valid || !playerVelocity.Valid)
            return null;
        // 当前重叠不作为即将接触的威胁，避免重复消耗闪避充能。
        if (geometry.ActiveWindow && geometry.CurrentOverlap)
            return null;
        for (double t = Math.Max(.03, path.Starts); t <= path.Ends + .000001; t += .015)
        {
            var predicted = path.At(t, player + playerVelocity * t, radius, height);
            if (!predicted.Known || !predicted.CurrentOverlap)
                continue;
            return new EnemyThreat
            {
                Id = key + ":" + current.Cycle + ":melee",
                Kind = "melee",
                Detail = observation.Profile + " / " + observation.Montage,
                Seconds = t,
                Radius = geometry.Radius,
                RadiusKnown = true,
                Position = predicted.Start,
                Melee = path
            };
        }

        return null;
    }

    public static bool EscapeClear(EnemyThreat threat, CombatState state, ThreatVector direction)
    {
        var path = threat.Melee;
        if (path == null)
            return false;
        for (double t = Math.Max(.03, path.Starts); t <= path.Ends + .000001; t += .015)
            if (path.At(t, state.Player + direction * (state.Roll.BaseSpeed * t), state.Radius, state.HalfHeight).CurrentOverlap)
                return false;
        return true;
    }

}

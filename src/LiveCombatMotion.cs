// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
using System;
using System.Collections.Generic;
using System.Linq;

sealed partial class HealthReader
{
    sealed class ReflectedMotion
    {
        public CombatMotion Motion;
        public long Checked;
    }

    readonly Dictionary<string, ReflectedMotion> reflectedMotions = new Dictionary<string, ReflectedMotion>();
    // 核对现场动画数值范围，拒绝 NaN/无穷及失配布局。
    static bool CombatNumber(double value)
    {
        return !Double.IsNaN(value) && !Double.IsInfinity(value);
    }

    CombatMotion ReadLiveMotion(Identity asset, long now)
    {
        string key = asset.Index + ":" + asset.Serial;
        ReflectedMotion cached;
        if (reflectedMotions.TryGetValue(key, out cached) && now - cached.Checked < 1000 && Valid(asset))
            return cached.Motion;
        var motion = new CombatMotion
        {
            Name = Name(asset.Name)
        };
        long ns = Struct("AnimNotifyEvent", 184), ss = Struct("CompositeSection", 80);
        motion.Length = M.F(asset.Address + Offset(asset, "SequenceLength", 4));
        if (!CombatNumber(motion.Length) || motion.Length <= 0 || motion.Length > 300)
            throw new Exception("Live montage length invalid");
        int start = Prop(ns, "LinkValue", 4).Offset, method = Prop(ns, "LinkMethod", 1).Offset, duration = Prop(ns, "Duration", 4).Offset, chance = Prop(ns, "NotifyTriggerChance", 4).Offset;
        int notify = Prop(ns, "Notify", 8).Offset, state = Prop(ns, "NotifyStateClass", 8).Offset;
        long h = asset.Address + Offset(asset, "Notifies", 16), array = M.Q(h);
        int n = M.I(h + 8), cap = M.I(h + 12);
        if (n < 0 || n > 512 || cap < n || cap > 2048)
            throw new Exception("Live notify bounds invalid");
        byte[] bytes = M.Read(array, n * 184);
        var windows = new List<CombatDamageWindow>();
        for (int i = 0; i < n; i++)
        {
            int at = i * 184;
            long obj = BitConverter.ToInt64(bytes, at + notify);
            if (obj == 0)
                obj = BitConverter.ToInt64(bytes, at + state);
            if (obj == 0)
                continue;
            Identity instance = Token(obj);
            string kind = ClassName(instance.Class);
            if (!new[]
            {
                "ANS_AnimationBasedComplexCollisionMeleeAttack",
                "AN_MeleeAttack",
                "AN_RangedAttack",
                "AN_AreaAttack"
            }.Contains(kind))
                continue;
            double time = BitConverter.ToSingle(bytes, at + start), length = BitConverter.ToSingle(bytes, at + duration), probability = BitConverter.ToSingle(bytes, at + chance);
            if (bytes[at + method] != 0 || !CombatNumber(time) || !CombatNumber(length) || !CombatNumber(probability) || time < 0 || length < 0 || time + length > motion.Length + .001 || probability < 0 || probability > 1 || !Valid(instance))
                throw new Exception("Unsupported live attack notify timing");
            windows.Add(new CombatDamageWindow { Index = i, Kind = kind, Start = time, End = time + length, Chance = probability, FullyParsed = true });
        }

        int sectionName = Prop(ss, "SectionName", 8).Offset, sectionStart = Prop(ss, "LinkValue", 4).Offset, sectionMethod = Prop(ss, "LinkMethod", 1).Offset;
        long sh = asset.Address + Offset(asset, "CompositeSections", 16), sa = M.Q(sh);
        int sn = M.I(sh + 8), sc = M.I(sh + 12);
        if (sn < 0 || sn > 128 || sc < sn || sc > 512)
            throw new Exception("Live section bounds invalid");
        byte[] sections = M.Read(sa, sn * 80);
        var parsed = new List<CombatSection>();
        for (int i = 0; i < sn; i++)
        {
            int at = i * 80;
            double time = BitConverter.ToSingle(sections, at + sectionStart);
            string name = Name(BitConverter.ToInt32(sections, at + sectionName));
            if (sections[at + sectionMethod] != 0 || BitConverter.ToInt32(sections, at + sectionName + 4) != 0 || !CombatNumber(time) || time < 0 || time > motion.Length || parsed.Any(s => s.Name == name))
                throw new Exception("Unsupported live montage section");
            parsed.Add(new CombatSection { Name = name, Start = time });
        }

        if (!Valid(asset) || M.Q(h) != array || M.I(h + 8) != n || M.Q(sh) != sa || M.I(sh + 8) != sn)
            throw new Exception("Live montage changed during read");
        motion.Windows = windows.ToArray();
        motion.Sections = parsed.ToArray();
        if (reflectedMotions.Count > 2048)
            reflectedMotions.Clear();
        reflectedMotions[key] = new ReflectedMotion
        {
            Motion = motion,
            Checked = now
        };
        AdaptationRecord.Set("combat.live-notifies." + motion.Name, "reflected actual asset; length=" + motion.Length.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " damage events=" + windows.Count + "; geometry/eligibility separate; no name-only threat trigger");
        return motion;
    }
}

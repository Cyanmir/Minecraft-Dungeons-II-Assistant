// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 中文维护说明：读取 combat-catalog.json 中的敌人/动画定义并计算攻击窗口。目录 Format 和游戏 SHA 是数据兼容约束；预测秒数、动画窗口和几何命中证据分别核对，不能互相替代。
// 源码导航和常改参数见 docs/maintenance.md；协议、反射标识及类型白名单修改前先核对两端。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;

#pragma warning disable 0649 // 目录字段由反序列化填充，因此屏蔽编译器的未赋值字段提示。

// CombatDamageWindow 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
sealed class CombatDamageWindow
{
    public int Index;
    public string Kind;
    public double Start, End;
    public double? Chance;
    public bool FullyParsed;
}

// CombatSection 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
sealed class CombatSection
{
    public string Name;
    public double Start;
}

// CombatMotion 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
sealed class CombatMotion
{
    public string Name;
    public double Length;
    public CombatDamageWindow[] Windows;
    public CombatSection[] Sections;
}

// CombatProfile 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
sealed class CombatProfile
{
    public string Id, TypeTag, Name, Category, ActorClass;
    public string[] Montages;
}

// CombatCatalogData 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
sealed class CombatCatalogData
{
    public int Format;
    public string GameSha256;
    public CombatProfile[] Profiles;
    public CombatMotion[] Montages;
}

#pragma warning restore 0649
// CombatObservation 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
sealed class CombatObservation
{
    public string ActorId, Profile, TypeTag, Montage, Status;
    public int DamageWindows;
    public int ContactEvent = -1;
    // 仅有时间轴不能预测命中；方向、命中几何和界面可操作性分别核对。
    public bool TimingKnown, ExecutionValidated, AssetTimelineMatched;
    public double? Position, EffectiveRate, NextAttackEventSeconds;
    public double? ReportedRate;
    public bool Playing;
    public CombatGeometry Geometry;
    public string DefinitionSource;
    public CombatDamageWindow[] EventWindows;
}

// CombatForecast 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
sealed class CombatForecast
{
    public int Event;
    public double Seconds;
    public bool Active;
}

// CombatDefinitions 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
static class CombatDefinitions
{
    static readonly CombatCatalogData data = Load();
    static readonly Dictionary<string, CombatMotion> motions = data.Montages.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
    // 加载本模块的配置或内嵌目录，并使用实现中的校验/回退规则。
    static CombatCatalogData Load()
    {
        using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("combat-catalog.json"))
        {
            if (input == null)
                throw new Exception("Missing combat catalog");
            using (var reader = new StreamReader(input))
            {
                var result = new JavaScriptSerializer
                {
                    MaxJsonLength = 2000000
                }.Deserialize<CombatCatalogData>(reader.ReadToEnd());
                if (result.Format != 1 || result.Profiles == null || result.Montages == null || result.GameSha256 == null || result.GameSha256.Length != 64)
                    throw new Exception("Invalid combat catalog");
                if (result.Profiles.Select(p => p.TypeTag).Distinct().Count() != result.Profiles.Length || result.Montages.Select(m => m.Name.ToLowerInvariant()).Distinct().Count() != result.Montages.Length)
                    throw new Exception("Ambiguous combat catalog");
                foreach (var m in result.Montages)
                {
                    if (!Finite(m.Length) || m.Length <= 0 || m.Windows == null)
                        throw new Exception("Invalid montage timeline");
                    foreach (var w in m.Windows)
                        if (!Finite(w.Start) || !Finite(w.End) || w.Start < 0 || w.End < w.Start || w.End > m.Length + .05)
                            throw new Exception("Invalid damage window");
                }

                return result;
            }
        }
    }

    // 排除 NaN 和无穷大，避免无效遥测参与距离或时间比较。
    static bool Finite(double x)
    {
        return !Double.IsNaN(x) && !Double.IsInfinity(x);
    }

    // 把当前 EXE SHA 与资源目录绑定版本比较，目录失配不继续套用动画定义。
    public static bool MatchesVersion(string sha)
    {
        return String.Equals(sha, data.GameSha256, StringComparison.OrdinalIgnoreCase);
    }

    // 检查 Actor 类是否存在于已适配敌人目录。
    public static bool DefinedActor(string actorClass)
    {
        return data.Profiles.Any(p => p.ActorClass == actorClass);
    }

    // 按名称查找静态蒙太奇定义，不代表现场正在播放。
    public static CombatMotion CatalogMotion(string name)
    {
        CombatMotion motion;
        return motions.TryGetValue(name, out motion) ? motion : null;
    }

    // 根据版本、Actor 类、敌对/存活标签唯一识别最具体的敌人类型。
    public static CombatProfile Identify(string sha, string actorClass, IEnumerable<string> ownedTags)
    {
        if (!MatchesVersion(sha) || !ThreatRule.HostileTeam(ownedTags))
            return null;
        var tags = new HashSet<string>(ownedTags);
        if (!tags.Contains("SW.State.Life.Alive"))
            return null;
        // 精确标签优先匹配最具体类型，风暴变体/克隆不能静默降级为父类。
        var candidates = data.Profiles.Where(p => p.ActorClass == actorClass && tags.Contains(p.TypeTag)).OrderByDescending(p => p.TypeTag.Length).ToArray();
        if (candidates.Length == 0 || candidates.Length > 1 && candidates[0].TypeTag.Length == candidates[1].TypeTag.Length)
            return null;
        return candidates[0];
    }

    // 取得指定敌人允许的蒙太奇定义，拒绝无归属的动画。
    public static CombatMotion Motion(CombatProfile profile, string actualMontage)
    {
        if (profile == null || !profile.Montages.Contains(actualMontage, StringComparer.OrdinalIgnoreCase))
            return null;
        CombatMotion motion;
        return motions.TryGetValue(actualMontage, out motion) ? motion : null;
    }

    // 计算当前动画段的结束时刻，保持目录时间边界。
    public static double SectionEnd(CombatMotion motion, double position)
    {
        if (motion.Sections == null || motion.Sections.Length == 0)
            return motion.Length;
        return motion.Sections.Where(s => s.Start > position).Select(s => s.Start).DefaultIfEmpty(motion.Length).Min();
    }

    // 只读预测限制在当前动画段内，不能假设后续段一定会播放。
    // 按播放位置和倍率计算下一个攻击事件的预计秒数。
    public static CombatForecast Forecast(CombatMotion motion, double position, double effectiveRate, double sectionEnd, double horizon)
    {
        if (motion == null || !Finite(position) || !Finite(effectiveRate) || !Finite(sectionEnd) || !Finite(horizon) || position < 0 || position > motion.Length || effectiveRate <= 0 || effectiveRate > 8 || sectionEnd < position || sectionEnd > motion.Length + .01 || horizon <= 0 || horizon > 2)
            return null;
        foreach (var w in motion.Windows.OrderBy(w => w.Start))
        {
            if (!w.FullyParsed || w.Chance != 1 || w.Start >= sectionEnd || w.End < position)
                continue;
            // 瞬时事件一旦越过时间点，就不再属于待发生事件。
            double seconds = Math.Max(0, (w.Start - position) / effectiveRate);
            if (seconds <= horizon)
                return new CombatForecast
                {
                    Event = w.Index,
                    Seconds = seconds,
                    Active = position >= w.Start && position <= w.End
                };
        }

        return null;
    }

    // 把已验证现场动画与静态攻击窗口关联，保留时序/执行/几何证据的区分。
    public static CombatObservation Observe(string actorId, CombatProfile profile, CombatMotion motion)
    {
        return new CombatObservation
        {
            ActorId = actorId,
            Profile = profile.Name,
            TypeTag = profile.TypeTag,
            Montage = motion.Name,
            DamageWindows = motion.Windows.Length,
            Status = "active montage matched; live timing and hit geometry not validated",
            TimingKnown = false,
            ExecutionValidated = false
        };
    }

    // 运行本模块的离线规则自检；返回 PASS 摘要，失败抛出异常供命令行报告。
    public static string Test()
    {
        if (data.Profiles.Length != 27 || data.Montages.Length != 159)
            throw new Exception("Combat catalog incomplete");
        string[] tags =
        {
            "SW.Team.Mob",
            "SW.Mob.Default.Hostile",
            "SW.State.Life.Alive",
            "SW.Mob.Monstrosity.Copper",
            "SW.Mob.Monstrosity.Copper.Storm"
        };
        var p = Identify(data.GameSha256, "BP_Monstrosity_Copper_C", tags);
        if (p == null || p.TypeTag != "SW.Mob.Monstrosity.Copper.Storm")
            throw new Exception("Variant identity failed");
        if (Identify("changed-build", "BP_Monstrosity_Copper_C", tags) != null || Identify(data.GameSha256, "BP_CA07_InactiveMonstrosity_C", tags) != null || Identify(data.GameSha256, p.ActorClass, tags.Concat(new[] { "SW.Team.Player" })) != null || Identify(data.GameSha256, p.ActorClass, tags.Where(t => t != "SW.State.Life.Alive")) != null)
            throw new Exception("Combat identity guards failed");
        var motion = Motion(p, "AM_Monstrosity_Copper_BasicAttack");
        if (motion == null || motion.Windows.Length != 2 || Motion(p, "AM_Zombie_Attack") != null)
            throw new Exception("Actual montage matching failed");
        var before = Forecast(motion, .7, 1, 2, .7);
        var fast = Forecast(motion, .7, 2, 2, .7);
        if (before == null || Math.Abs(before.Seconds - .2078073501586914) > 1e-6 || fast == null || Math.Abs(fast.Seconds - before.Seconds / 2) > 1e-6)
            throw new Exception("Actual damage timing failed");
        if (Forecast(motion, .7, 0, 2, .7) != null || Forecast(motion, .7, -1, 2, .7) != null || Forecast(motion, Double.NaN, 1, 2, .7) != null || Forecast(motion, .7, 1, .8666666746139526, .7) != null || Forecast(motion, 2.4, 1, motion.Length, .7) != null)
            throw new Exception("Paused/reversed/section/recovery guards failed");
        if (CombatDefinitions.SectionEnd(motion, .86666) > .87)
            throw new Exception("Near-section-boundary guard failed");
        var active = Forecast(motion, 1.0, 1, 2, .7);
        if (active == null || !active.Active || active.Seconds != 0)
            throw new Exception("Active collision window failed");
        var uncertain = new CombatMotion
        {
            Length = 2,
            Windows = new[]
            {
                new CombatDamageWindow
                {
                    Start = 1,
                    End = 1,
                    Chance = .5,
                    FullyParsed = true
                }
            }
        };
        if (Forecast(uncertain, .9, 1, 2, .7) != null)
            throw new Exception("Probabilistic notify accepted");
        var observed = Observe("test", p, motion);
        if (observed.ExecutionValidated || observed.TimingKnown || observed.NextAttackEventSeconds.HasValue)
            throw new Exception("Static catalog enables input");
        return "PASS: 27 enemy/Boss profiles and 159 motions; exact hostile/live/variant identity; version and class guards; actual damage windows, play rate, section boundary, paused/reversed/recovery and probability guards. Static matches cannot trigger input.\r\n";
    }
}

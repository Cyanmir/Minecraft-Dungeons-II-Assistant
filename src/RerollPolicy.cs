// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 不同目标与实际可刷新词条的二分图匹配。
using System;
using System.Collections.Generic;
using System.Linq;

sealed class RerollGoal
{
    // Type 为不同词条身份，等级来自已确认的模板/原生等级；不能用装备力量代替。
    public string Type;
    public int MinimumLevel;
    public RerollGoal(string type, int minimumLevel)
    {
        Type = type;
        MinimumLevel = minimumLevel;
    }
}
sealed class RerollEffect
{
    public string Type, Template;
    public int Level = -1, Failures;
    public double Intensity;
    // 原生 GetTotalEffectValue 的格式化显示，只供人工查看；不能解析成最低值要求。
    public string NativeValue;
    // 按 ECurrency 的 Emerald / SpringStone / EnchantmentPoint 顺序保存当前原生费用。
    public int[] Cost = new int[3];
}
sealed class RerollMatch
{
    public int Required, Count;
    // 每槽对应目标索引；-1 为未分配。执行层只能保留分配成功的槽，不重复计算同一个目标。
    public int[] SlotToGoal;
    public bool Complete { get { return Required > 0 && Count >= Required; } }
}
static class RerollMatching
{
    // 达标只比较效果身份和最低等级；原始强度保留给执行确认，不作为隐藏的目标条件。
    static bool Meets(RerollEffect effect, RerollGoal goal)
    {
        return effect.Type == goal.Type && effect.Level >= goal.MinimumLevel;
    }
    // 增广路径允许重新分配先前匹配，不能固定目标前 N 项或假设槽位顺序等于目标顺序。
    static bool Assign(int goal, IList<RerollGoal> goals, IList<RerollEffect> effects, int[] owners, bool[] seen)
    {
        for (int slot = 0; slot < effects.Count; slot++)
        {
            if (seen[slot] || !Meets(effects[slot], goals[goal])) continue;
            seen[slot] = true;
            if (owners[slot] < 0 || Assign(owners[slot], goals, effects, owners, seen))
            {
                owners[slot] = goal;
                return true;
            }
        }
        return false;
    }
    // 实际槽数只能来自 GetRerollableEffectsFromItem；品质配置不参与这个停止数量公式。
    public static RerollMatch Match(IList<RerollGoal> goals, IList<RerollEffect> effects)
    {
        if (goals == null || effects == null || goals.Count > 64 || effects.Count > 16) throw new ArgumentException("Invalid reroll input");
        if (goals.Any(g => g == null || String.IsNullOrEmpty(g.Type) || g.MinimumLevel < 1) ||
            goals.Select(g => g.Type).Distinct(StringComparer.Ordinal).Count() != goals.Count) throw new ArgumentException("Targets must have different, readable identities");
        if (effects.Any(e => e == null || String.IsNullOrEmpty(e.Type) || Double.IsNaN(e.Intensity) || Double.IsInfinity(e.Intensity))) throw new ArgumentException("Unreadable effects");
        var result = new RerollMatch { Required = Math.Min(goals.Count, effects.Count), SlotToGoal = Enumerable.Repeat(-1, effects.Count).ToArray() };
        for (int i = 0; i < goals.Count; i++)
            if (Assign(i, goals, effects, result.SlotToGoal, new bool[effects.Count])) result.Count++;
        return result;
    }
}

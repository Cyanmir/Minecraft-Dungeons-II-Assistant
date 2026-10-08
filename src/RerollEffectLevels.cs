// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 最小索引来自核对过的普通模板数字 Tier 引用，独特固定模板不参与等级上限。
// 这里只限制输入不能超过游戏普通效果定义；不是当前装备/品质/铁匠条件下可达最高等级的证明。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

static class RerollEffectLevels
{
    static readonly Dictionary<string, int[]> levels = Read();
    static Dictionary<string, int[]> Read()
    {
        using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("reroll-effect-levels.json"))
        using (var reader = new StreamReader(stream))
        {
            var root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(reader.ReadToEnd());
            if (Convert.ToInt32(root["format"]) != 1) throw new InvalidDataException("Effect level index format");
            return ((Dictionary<string, object>)root["levels"]).ToDictionary(pair => pair.Key,
                pair => ((System.Collections.IEnumerable)pair.Value).Cast<object>().Select(Convert.ToInt32).ToArray(), StringComparer.Ordinal);
        }
    }
    internal static int Maximum(string type)
    {
        int[] value;
        return type != null && levels.TryGetValue(type, out value) ? value.Max() : 0;
    }
    internal static bool Supports(string type, int level)
    {
        int[] value;
        return type != null && levels.TryGetValue(type, out value) && value.Contains(level);
    }
}

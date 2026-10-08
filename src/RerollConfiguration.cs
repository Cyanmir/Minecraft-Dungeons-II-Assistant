// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 用户目标/次数/三类预算单独持久化，兼容没有此文件的旧配置。
// Level=0 表示要求实际可达的最高词条等级，绝不把未知最高值补为 3 或装备力量。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

sealed class RerollTargetSetting
{
    public string Type;
    public int Level;
    // 只持久化效果身份和最低等级；旧 JSON 的数值阈值/方向字段由反序列化忽略，重新保存时移除。
}
sealed class RerollConfiguration
{
    internal const int CountLimit = 1000;
    // QualityPreset 保留兼容旧配置，新的词条数筛选只按实际批次过滤显示。
    public int Format = 1, MaximumRerolls = 100, QualityPreset = 0;
    public int[] Budgets = new[] { 0, 100, 0 };
    public string EquipmentType = "";
    public List<RerollTargetSetting> Targets = new List<RerollTargetSetting>();
    // 按装备类型保存最近目标模板；运行中的具体装备仍按本次背包稳定 UID 单独绑定，不持久化 UID。
    public Dictionary<string, List<RerollTargetSetting>> GearProfiles = new Dictionary<string, List<RerollTargetSetting>>();
    public bool ManualUnverified = true;
    static string PathName { get { return Path.Combine(Path.GetDirectoryName(ToolboxSettings.ConfigPath), "reroll.json"); } }
    internal static RerollConfiguration Load()
    {
        if (!File.Exists(PathName)) return new RerollConfiguration();
        try
        {
            if (new FileInfo(PathName).Length > 1048576) throw new InvalidDataException("Reroll configuration size");
            var result = new JavaScriptSerializer { MaxJsonLength = 1048576 }.Deserialize<RerollConfiguration>(File.ReadAllText(PathName));
            if (result.GearProfiles == null) result.GearProfiles = new Dictionary<string, List<RerollTargetSetting>>();
            // 旧 UI 允许设置 1000，现按普通模板的已核实定义上限迁移，不能留下永远达不到的超限目标。
            foreach (var target in (result.Targets ?? new List<RerollTargetSetting>()).Concat(result.GearProfiles.Values.Where(v => v != null).SelectMany(v => v)))
            {
                if (target == null) continue;
                int limit = RerollEffectLevels.Maximum(target.Type);
                if (limit > 0 && target.Level > limit) target.Level = limit;
            }
            result.ManualUnverified = true;
            // 旧百万次上限缩小为实用整批上限，保留目标与其他旧配置。
            if (result.MaximumRerolls >= 1 && result.MaximumRerolls <= 1000000) result.MaximumRerolls = Math.Min(CountLimit, result.MaximumRerolls);
            result.Validate();
            return result;
        }
        catch (Exception error) { ToolboxLog.Error("Reroll.Configuration", error); return new RerollConfiguration(); }
    }
    internal void Validate()
    {
        if (Format != 1 || MaximumRerolls < 1 || MaximumRerolls > CountLimit || QualityPreset < 0 || QualityPreset > 2 ||
            EquipmentType == null || EquipmentType.Length > 160 || EquipmentType.Length > 0 && !Tag(EquipmentType) ||
            Budgets == null || Budgets.Length != 3 || Budgets.Any(v => v < 0) || Targets == null || Targets.Count > 64 ||
            Targets.Any(t => t == null || !Tag(t.Type) || t.Level < 0 || t.Level > 1000 || AboveDefinedLevel(t)) ||
            Targets.Select(t => t.Type).Distinct(StringComparer.Ordinal).Count() != Targets.Count)
            throw new InvalidDataException("Invalid reroll configuration");
        if (GearProfiles == null || GearProfiles.Count > 32) throw new InvalidDataException("Invalid gear profiles");
        foreach (var profile in GearProfiles)
        {
            if (!Tag(profile.Key)) throw new InvalidDataException("Invalid gear profile type");
            ValidateTargets(profile.Value);
        }
    }
    internal static void ValidateTargets(List<RerollTargetSetting> targets)
    {
        if (targets == null || targets.Count > 64 || targets.Any(t => t == null || !Tag(t.Type) || t.Level < 0 || t.Level > 1000 || AboveDefinedLevel(t)) || targets.Select(t => t.Type).Distinct(StringComparer.Ordinal).Count() != targets.Count)
            throw new InvalidDataException("Invalid gear targets");
    }
    // 存储兼容旧的未知条目，但已知效果不可越过定义上限；执行另要求已核实的数字等级。
    static bool AboveDefinedLevel(RerollTargetSetting target)
    {
        int maximum = RerollEffectLevels.Maximum(target.Type);
        return maximum > 0 && target.Level > maximum;
    }
    internal static List<RerollTargetSetting> CopyTargets(System.Collections.Generic.IEnumerable<RerollTargetSetting> targets)
    {
        return targets.Select(t => new RerollTargetSetting { Type = t.Type, Level = t.Level }).ToList();
    }
    internal void Save()
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(PathName));
        string temporary = PathName + ".tmp";
        File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(this), new UTF8Encoding(false));
        if (File.Exists(PathName)) File.Replace(temporary, PathName, null); else File.Move(temporary, PathName);
    }
    internal RerollConfiguration Copy()
    {
        return new JavaScriptSerializer().Deserialize<RerollConfiguration>(new JavaScriptSerializer().Serialize(this));
    }
    internal static bool Tag(string value) { return value != null && value.Length <= 160 && Regex.IsMatch(value, "^SW\\.[A-Za-z0-9_.]+$"); }
    internal static bool Finite(double value) { return !Double.IsInfinity(value) && !Double.IsNaN(value); }
}

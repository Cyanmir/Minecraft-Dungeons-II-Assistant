// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 验证动画函数布局并读取当前播放状态。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

// MontageCodeLayout 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
sealed class MontageCodeLayout
{
    public int Array, Weight, Position, Rate, Playing;
    public long InstanceHelper;
    // 从代码匹配候选中要求唯一布局，歧义不得默选第一项。
    public static int Unique(byte[] code, string pattern)
    {
        int[] matches = new GameLocator.Pattern(pattern).Matches(code).ToArray();
        if (matches.Length != 1)
            throw new Exception("Animation signature absent or ambiguous");
        return matches[0];
    }

    // 解析并验证原生当前动画实例访问布局。
    public static MontageCodeLayout Active(byte[] code)
    {
        int count = BitConverter.ToInt32(code, Unique(code, "8B 81 ?? ?? ?? ?? 83 E8 01") + 2);
        int array = BitConverter.ToInt32(code, Unique(code, "49 8B 82 ?? ?? ?? ??") + 3);
        int[] weights = new GameLocator.Pattern("0F 2E 80 ?? ?? ?? ??").Matches(code).Concat(new GameLocator.Pattern("0F 2F 80 ?? ?? ?? ??").Matches(code)).ToArray();
        if (weights.Length != 1)
            throw new Exception("Montage weight signature ambiguous");
        int weight = BitConverter.ToInt32(code, weights[0] + 3);
        if (array < 40 || array > 4096 || count != array + 8 || weight < 16 || weight > 4096)
            throw new Exception("Invalid montage array layout");
        return new MontageCodeLayout
        {
            Array = array,
            Weight = weight
        };
    }

    // 验证原生读取单精度字段的代码形态与偏移。
    public static int FloatField(byte[] code, long address, MontageCodeLayout active, out long helper)
    {
        int at = Unique(code, "E8 ?? ?? ?? ?? 48 85 C0 74 ?? F3 0F 10 80 ?? ?? ?? ?? F3 0F 11 45 00");
        helper = address + at + 5 + BitConverter.ToInt32(code, at + 1);
        int offset = BitConverter.ToInt32(code, at + 14);
        int count = BitConverter.ToInt32(code, Unique(code, "48 63 86 ?? ?? ?? ?? 85 C0") + 3);
        int array = BitConverter.ToInt32(code, Unique(code, "48 8B 8E ?? ?? ?? ?? 48 8B D0") + 3);
        if (count != active.Array + 8 || array != active.Array || offset < 16 || offset > 4096 || offset % 4 != 0)
            throw new Exception("Animation clock array mismatch");
        return offset;
    }

    // 验证播放状态字段的读取布局。
    public static int PlayingField(byte[] code, long address, MontageCodeLayout active, long helper)
    {
        int at = Unique(code, "E8 ?? ?? ?? ?? 48 85 C0 74 ?? 48 39 18 74 ?? 38 58 ?? 75 ??");
        if (address + at + 5 + BitConverter.ToInt32(code, at + 1) != helper)
            throw new Exception("Playing montage resolver mismatch");
        int field = code[at + 17];
        int second = Unique(code, "80 78 ?? 00 75 ?? 48 FF C3 48 83 C1 08");
        int count = BitConverter.ToInt32(code, Unique(code, "48 63 85 ?? ?? ?? ?? 85 C0") + 3), array = BitConverter.ToInt32(code, Unique(code, "48 8B 8D ?? ?? ?? ?? 48 8B D0") + 3);
        if (field != code[second + 2] || count != active.Array + 8 || array != active.Array || field < 8)
            throw new Exception("Playing montage layout mismatch");
        return field;
    }

}

sealed class CombatClock
{
    double previous, previousRate, previousReported, previousSection;
    long last = -1;
    double? confirmedRate;
    // 以连续读值估算真实速度，包含世界/角色时间缩放；需要三个稳定样本，暂停、跳变、切段或变速都会重置。
    // 读取并规范化一帧动画位置/速率，校验时间连续性。
    public double? Sample(long now, double position, double reported, double sectionEnd, bool playing)
    {
        double? result = null;
        double elapsed = (now - last) / 1000.0;
        if (last >= 0 && elapsed >= 0 && elapsed < .025 && playing && reported > 0 && reported <= 8 && reported == previousReported && sectionEnd == previousSection && position >= previous && position - previous <= Math.Max(reported, confirmedRate.GetValueOrDefault()) * elapsed * 4 + .025)
            return confirmedRate;
        if (last >= 0 && elapsed >= .025 && elapsed <= .35 && playing && reported > 0 && reported <= 8 && reported == previousReported && sectionEnd == previousSection)
        {
            double rate = (position - previous) / elapsed;
            if (rate > 0 && rate <= 8 && previousRate > 0 && Math.Abs(rate - previousRate) <= Math.Max(.15, previousRate * .2))
                result = rate;
            previousRate = rate > 0 && rate <= 8 ? rate : 0;
        }
        else
            previousRate = 0;
        previous = position;
        previousReported = reported;
        previousSection = sectionEnd;
        last = now;
        confirmedRate = result;
        return result;
    }

}

// 跨文件的只读游戏读取器；各 partial 文件共同持有同一连接/对象身份缓存。
sealed partial class HealthReader
{
    readonly Dictionary<string, Identity> combatFunctions = new Dictionary<string, Identity>();
    readonly Dictionary<string, CombatClock> combatClocks = new Dictionary<string, CombatClock>();
    // CheckedMotion 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
    sealed class CheckedMotion
    {
        public long Checked;
        public bool Matched;
    }

    readonly Dictionary<string, CheckedMotion> checkedMotions = new Dictionary<string, CheckedMotion>();
    MontageCodeLayout timingLayout;
    bool timingAttempted;
    string timingFailure;
    sealed class NamedCode
    {
        public long Address;
        public byte[] Bytes;
        public string Source;
    }

    // 通过可核对的函数名定位动画相关原生函数代码。
    List<NamedCode> NamedAnimationCode(LocalSymbols symbols, string name, int length)
    {
        var result = new List<NamedCode>();
        foreach (string qualified in new[]
        {
            "UAnimInstance::" + name,
            "AnimInstance::" + name,
            "UAnimInstance::exec" + name
        }

        )
        {
            var symbol = symbols.Find(qualified);
            if (symbol != null && executable(symbol.Address, length))
                result.Add(new NamedCode { Address = symbol.Address, Bytes = M.Read(symbol.Address, length), Source = "native symbol " + qualified });
        }

        Identity function;
        if (!combatFunctions.TryGetValue(name, out function) || !Valid(function))
            return result;
        var seen = new HashSet<long>(result.Select(c => c.Address));
        for (int slot = 120; slot <= 240; slot += 8)
        {
            long fn = M.Q(function.Address + slot);
            if (executable(fn, length) && seen.Add(fn))
                result.Add(new NamedCode { Address = fn, Bytes = M.Read(fn, length), Source = "reflected AnimInstance::" + name + " pointerField=" + slot });
        }

        return result;
    }

    void RecordAnimationCode(string name, NamedCode code)
    {
        AdaptationRecord.Set("function.AnimInstance." + name, code.Source + "; RVA=0x" + (code.Address - process.MainModule.BaseAddress.ToInt64()).ToString("X") + "; bytes=" + BitConverter.ToString(code.Bytes).Replace('-', ' '));
    }

    // 验证位置、长度和速率等动画时序读取布局。
    void TimingLayout()
    {
        if (timingAttempted)
            return;
        timingAttempted = true;
        AdaptationRecord.Set("signature.catalog.clock.float", "E8 ?? ?? ?? ?? 48 85 C0 74 ?? F3 0F 10 80 ?? ?? ?? ?? F3 0F 11 45 00");
        AdaptationRecord.Set("signature.catalog.clock.playing", "E8 ?? ?? ?? ?? 48 85 C0 74 ?? 48 39 18 74 ?? 38 58 ?? 75 ??; cross-check second comparison, array layout and shared resolver");
        try
        {
            using (var symbols = new LocalSymbols(M.Handle, process.MainModule.FileName, process.MainModule.BaseAddress.ToInt64(), process.MainModule.ModuleMemorySize))
            {
                var active = new MontageCodeLayout
                {
                    Array = montageArray,
                    Weight = montageWeight
                };
                var found = new List<MontageCodeLayout>();
                foreach (var position in NamedAnimationCode(symbols, "Montage_GetPosition", 320))
                    foreach (var rate in NamedAnimationCode(symbols, "Montage_GetPlayRate", 320))
                        foreach (var playing in NamedAnimationCode(symbols, "Montage_IsPlaying", 320))
                            try
                            {
                                long a, b;
                                int pos = MontageCodeLayout.FloatField(position.Bytes, position.Address, active, out a), speed = MontageCodeLayout.FloatField(rate.Bytes, rate.Address, active, out b);
                                if (a != b || !executable(a, 64) || speed != pos + 4)
                                    continue;
                                int flag = MontageCodeLayout.PlayingField(playing.Bytes, playing.Address, active, a);
                                found.Add(new MontageCodeLayout { Array = active.Array, Weight = active.Weight, Position = pos, Rate = speed, Playing = flag, InstanceHelper = a });
                                RecordAnimationCode("Montage_GetPosition", position);
                                RecordAnimationCode("Montage_GetPlayRate", rate);
                                RecordAnimationCode("Montage_IsPlaying", playing);
                            }
                            catch
                            {
                            }

                if (found.Select(l => l.Position + ":" + l.Rate + ":" + l.Playing + ":" + l.InstanceHelper).Distinct().Count() != 1)
                    throw new Exception("Animation clock absent or ambiguous");
                timingLayout = found[0];
                AdaptationRecord.Set("combat.clock.layout", "position=0x" + timingLayout.Position.ToString("X") + " rate=0x" + timingLayout.Rate.ToString("X") + " playing=0x" + timingLayout.Playing.ToString("X") + "; native symbols first, reflected names and validated signatures otherwise; no function call");
            }
        }
        catch (Exception e)
        {
            timingFailure = e.Message;
            AdaptationRecord.Set("combat.clock.layout", "unavailable: " + timingFailure);
        }
    }

    // 确认现场动画身份与已匹配静态目录一致。
    bool LiveMotionMatches(Identity asset, CombatMotion motion, long now)
    {
        string key = asset.Index + ":" + asset.Serial;
        CheckedMotion cached;
        if (checkedMotions.TryGetValue(key, out cached) && now - cached.Checked < 1000)
            return cached.Matched && Valid(asset);
        bool matched = false;
        try
        {
            long notifyStruct = Struct("AnimNotifyEvent", 184), sectionStruct = Struct("CompositeSection", 80);
            if (Math.Abs(M.F(asset.Address + Offset(asset, "SequenceLength", 4)) - motion.Length) > .0001)
                return false;
            long header = asset.Address + Offset(asset, "Notifies", 16), array = M.Q(header);
            int count = M.I(header + 8), capacity = M.I(header + 12);
            if (count < 0 || count > 512 || capacity < count || capacity > 2048)
                return false;
            int start = Prop(notifyStruct, "LinkValue", 4).Offset, method = Prop(notifyStruct, "LinkMethod", 1).Offset, duration = Prop(notifyStruct, "Duration", 4).Offset, chance = Prop(notifyStruct, "NotifyTriggerChance", 4).Offset;
            int notify = Prop(notifyStruct, "Notify", 8).Offset, state = Prop(notifyStruct, "NotifyStateClass", 8).Offset;
            int events = 0;
            for (int i = 0; i < count; i++)
            {
                long row = array + i * 184, instance = M.Q(row + notify);
                if (instance == 0)
                    instance = M.Q(row + state);
                if (instance == 0)
                    continue;
                Identity obj = Token(instance);
                string kind = ClassName(obj.Class);
                if (!new[]
                {
                    "ANS_AnimationBasedComplexCollisionMeleeAttack",
                    "AN_MeleeAttack",
                    "AN_RangedAttack",
                    "AN_AreaAttack"
                }.Contains(kind))
                    continue;
                CombatDamageWindow w = motion.Windows.FirstOrDefault(x => x.Index == i && x.Kind == kind);
                if (w == null || M.Read(row + method, 1)[0] != 0)
                    return false;
                if (Math.Abs(M.F(row + start) - w.Start) > .0001 || Math.Abs(M.F(row + duration) - (w.End - w.Start)) > .0001 || !w.Chance.HasValue || Math.Abs(M.F(row + chance) - w.Chance.Value) > .0001 || !Valid(obj))
                    return false;
                events++;
            }

            if (events != motion.Windows.Length || M.Q(header) != array || M.I(header + 8) != count)
                return false;
            long sh = asset.Address + Offset(asset, "CompositeSections", 16), sa = M.Q(sh);
            int sn = M.I(sh + 8), sc = M.I(sh + 12);
            if (sn < 0 || sn > 128 || sc < sn || sc > 512 || sn != motion.Sections.Length)
                return false;
            int sectionName = Prop(sectionStruct, "SectionName", 8).Offset, sectionStart = Prop(sectionStruct, "LinkValue", 4).Offset, sectionMethod = Prop(sectionStruct, "LinkMethod", 1).Offset;
            for (int i = 0; i < sn; i++)
            {
                long row = sa + i * 80;
                string name = Name(M.I(row + sectionName));
                var section = motion.Sections.SingleOrDefault(x => x.Name == name);
                if (section == null || M.Read(row + sectionMethod, 1)[0] != 0 || Math.Abs(M.F(row + sectionStart) - section.Start) > .0001)
                    return false;
            }

            matched = Valid(asset) && M.Q(sh) == sa && M.I(sh + 8) == sn;
        }
        catch
        {
        }
        finally
        {
            if (checkedMotions.Count > 2048)
                checkedMotions.Clear();
            checkedMotions[key] = new CheckedMotion
            {
                Checked = now,
                Matched = matched
            };
            AdaptationRecord.Set("combat.timeline." + motion.Name, matched ? "live sequence length, notify classes/times/chance and section names/times match static catalog" : "live asset differs or schema unavailable; timing forecast disabled");
        }

        return matched;
    }

    // 汇总现场播放、攻击窗口和几何信息，不把静态目录视为动作结果。
    CombatObservation ReadCombatObservation(string key, CombatProfile profile, CombatMotion motion, Identity asset, long instance, long now, Identity mesh, ThreatVector playerPosition, double playerRadius, double halfHeight)
    {
        var observation = CombatDefinitions.Observe(key, profile, motion);
        observation.DefinitionSource = "version-matched game-file catalog";
        observation.EventWindows = motion.Windows;
        TimingLayout();
        if (timingLayout == null)
        {
            observation.Status = timingFailure;
            return observation;
        }

        float pos = M.F(instance + timingLayout.Position), reported = M.F(instance + timingLayout.Rate);
        byte playing = M.Read(instance + timingLayout.Playing, 1)[0];
        if (pos < 0 || pos > motion.Length + .001 || Math.Abs(reported) > 8 || playing > 1)
        {
            observation.Status = "invalid animation clock values";
            return observation;
        }

        observation.Position = pos;
        observation.ReportedRate = reported;
        observation.Playing = playing == 1;
        observation.AssetTimelineMatched = LiveMotionMatches(asset, motion, now);
        if (!observation.AssetTimelineMatched)
        {
            observation.Status = "live asset differs or schema unavailable; timing forecast disabled";
            return observation;
        }

        CombatClock clock;
        if (!combatClocks.TryGetValue(key, out clock))
        {
            if (combatClocks.Count > 4096)
                combatClocks.Clear();
            clock = new CombatClock();
            combatClocks[key] = clock;
        }

        double end = CombatDefinitions.SectionEnd(motion, pos);
        var effective = clock.Sample(now, pos, reported, end, playing == 1);
        observation.EffectiveRate = effective;
        observation.TimingKnown = effective.HasValue;
        if (effective.HasValue)
        {
            var forecast = CombatDefinitions.Forecast(motion, pos, effective.Value, end, .7);
            if (forecast != null)
            {
                observation.NextAttackEventSeconds = forecast.Seconds;
                observation.ContactEvent = forecast.Event;
                observation.Geometry = ReadContactGeometry(asset, motion.Windows.Single(w => w.Index == forecast.Event), pos, mesh, playerPosition, playerRadius, halfHeight);
            }
        }

        observation.Status = observation.NextAttackEventSeconds.HasValue ? "attack event approaching; player impact not validated" : "clock read; no confirmed nearby attack event";
        return observation;
    }

    // 按限定样本数只读观察战斗时序并导出报告。
    public void ObserveCombat(string path, int seconds)
    {
        if (seconds < 1 || seconds > 120)
            throw new Exception("Observation duration out of bounds");
        var samples = new List<object>();
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < seconds * 1000)
        {
            var frame = Threats(watch.ElapsedMilliseconds);
            samples.Add(new { elapsedMs = watch.ElapsedMilliseconds, frame = frame });
            System.Threading.Thread.Sleep(75);
        }

        var report = new
        {
            capturedUtc = DateTime.UtcNow.ToString("o"),
            readOnly = true,
            gameInputs = false,
            gameWrites = false,
            githubUpload = false,
            samples = samples,
            lookupRecord = AdaptationRecord.Contents()
        };
        File.WriteAllText(path, new JavaScriptSerializer { MaxJsonLength = 20000000 }.Serialize(report), new UTF8Encoding(true));
    }

}

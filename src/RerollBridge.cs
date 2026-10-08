// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 协议 7 将观察编号与动作 epoch/sequence 分开。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// 协议过旧与游戏未适配分开提示；重复安装相同 EXE 内的组件不能解决版本准入。
sealed class RerollComponentVersionException : Exception { }

sealed class RerollSnapshot
{
    public string Instance, State, Type, Rarity, SelectedUid;
    public string Epoch, ActionStatus;
    public int ActionSequence;
    public int Sequence, Power, RequiredBlacksmithLevel, Failures, NativeRerolls;
    public double Clock;
    public double RawPower;
    public bool Storm;
    // 监听状态 0=未建立，1=建立，2=载荷异常；建立不代表已收到消息或验证完成语义。
    public int MessageObserver, MessageCount;
    public double MessageClock;
    public string MessageSelectedUid;
    public int[] Balance = new int[3];
    public List<RerollEffect> Effects = new List<RerollEffect>();
    // Selected 仅表示组件已交叉核对列表选择及原生 UID，不等于词条目录或刷新完成条件已验证。
    internal static RerollSnapshot Parse(string text)
    {
        string[] head = text.Split('|');
        if (head.Length == 6 && head[0] != "7") throw new RerollComponentVersionException();
        var result = new RerollSnapshot();
        if (head.Length != 6 || head[0] != "7" || head[1].Length < 1 || head[1].Length > 160 ||
            !(head[2] == "-" || Regex.IsMatch(head[2], "^[a-f0-9]{32}$")) ||
            !Int32.TryParse(head[3], out result.ActionSequence) || result.ActionSequence < 0 ||
            !Double.TryParse(head[5], NumberStyles.Float, CultureInfo.InvariantCulture, out result.Clock) || !Finite(result.Clock) || result.Clock < 0)
            throw new InvalidDataException("Invalid reroll observation header");
        result.Instance = head[1];
        result.Epoch = head[2];
        var payload = head[4].Split('~');
        if (payload.Length < 3 || !new[] { "Selecting", "SelectedItem", "SelectionUnavailable", "SelectionFailed", "Disabled", "TransportReady", "Queued", "Dispatched", "Confirmed", "Expired", "InvalidRequest", "ResultUncertain", "ResultTimedOut", "ConfirmationUnavailable", "BudgetLimit", "AmbiguousEffects", "NativeConditionsFailed", "CostChanged", "InsufficientFunds", "Cancelled", "NoPlayer", "MultiplePlayers", "Unreadable", "BlacksmithClosed", "NoSelection", "UnknownCost", "SelectionChanged", "SelectionUnverified" }.Contains(payload[0])) throw new InvalidDataException("Unknown reroll action outcome");
        result.ActionStatus = payload[0];
        result.Sequence = Integer(payload[1], 1, Int32.MaxValue);
        if (result.ActionSequence == 0 && (result.Epoch != "-" || result.ActionStatus != "Disabled") || result.ActionSequence > 0 && result.Epoch == "-") throw new InvalidDataException("Invalid reroll action identity");
        var data = payload.Skip(2).ToArray();
        result.State = data[0];
        if (result.State != "Selected")
        {
            if (data.Length != 1 || !new[] { "NoPlayer", "MultiplePlayers", "Unreadable", "BlacksmithClosed", "NoSelection", "UnknownCost", "SelectionChanged", "SelectionUnverified", "SelectionPending" }.Contains(result.State)) throw new InvalidDataException("Unknown reroll observation state");
            return result;
        }
        long identity;
        if (data.Length != 13 || !Int64.TryParse(data[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out identity) || identity == 0 ||
            !Tag(data[2]) || !Tag(data[3])) throw new InvalidDataException("Invalid reroll item identity");
        result.SelectedUid = data[1];
        result.Type = data[2];
        result.Rarity = data[3];
        result.Power = Integer(data[4], 0, 100000);
        result.RequiredBlacksmithLevel = Integer(data[5], 0, 100000);
        result.Failures = Integer(data[6], 0, 31);
        result.NativeRerolls = Integer(data[7], 0, 255);
        result.Storm = Integer(data[8], 0, 1) == 1;
        // 原始 ItemPower 是 float；它与 GetItemPowerLevel 的整数显示不得通过猜测取整混用。
        double rawPower;
        if (!Double.TryParse(data[12], NumberStyles.Float, CultureInfo.InvariantCulture, out rawPower) || !Finite(rawPower) || rawPower < 0 || rawPower > 100000)
            throw new InvalidDataException("Unreadable raw item power");
        result.RawPower = (double)(float)rawPower;
        var balance = data[9].Split(',');
        if (balance.Length != 3) throw new InvalidDataException("Unreadable native balance");
        result.Balance = balance.Select(value => Integer(value, 0, Int32.MaxValue)).ToArray();
        var messages = data[11].Split(',');
        long observedUid;
        if (messages.Length != 4 || !Double.TryParse(messages[2], NumberStyles.Float, CultureInfo.InvariantCulture, out result.MessageClock) ||
            !Finite(result.MessageClock) || result.MessageClock < 0 || result.MessageClock > result.Clock ||
            !Int64.TryParse(messages[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out observedUid)) throw new InvalidDataException("Unreadable reroll message evidence");
        result.MessageObserver = Integer(messages[0], 0, 2);
        result.MessageCount = Integer(messages[1], 0, Int32.MaxValue);
        result.MessageSelectedUid = messages[3];
        if (result.MessageCount == 0 && (result.MessageClock != 0 || observedUid != 0)) throw new InvalidDataException("Reroll message evidence has no callback");
        if (data[10].Length == 0) return result;
        foreach (var entry in data[10].Split(';'))
        {
            var fields = entry.Split(',');
            double intensity;
            if (fields.Length != 9 || !Tag(fields[0]) || !Tag(fields[2]) || !Double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out intensity) || !Finite(intensity) || Single.IsInfinity((float)intensity)) throw new InvalidDataException("Unreadable reroll effect");
            // 游戏字段本来是 float；先还原九位有效数字传输，再提升到 double 做精确阈值比较。
            // 等级比较使用原始强度，不使用展示舍入值或容差。
            intensity = (double)(float)intensity;
            result.Effects.Add(new RerollEffect { Type = fields[0], Template = fields[2], Intensity = intensity,
                Level = Integer(fields[3], -1, 1000), Failures = Integer(fields[4], 0, 31),
                Cost = new[] { Integer(fields[5], 0, Int32.MaxValue), Integer(fields[6], 0, Int32.MaxValue), Integer(fields[7], 0, Int32.MaxValue) }, NativeValue = DecodeDisplay(fields[8]) });
        }
        if (result.Effects.Count > 16) throw new InvalidDataException("Reroll effect count limit");
        return result;
    }
    static bool Finite(double value) { return !Double.IsNaN(value) && !Double.IsInfinity(value); }
    // 游戏格式化文本仅供显示；白名单转义还原后不当作数值、等级或可执行目标。
    static string DecodeDisplay(string text)
    {
        if (text.Length > 384 || Regex.IsMatch(text, @"%(?!25|7C|7E|3B|2C|0A|0D)")) throw new InvalidDataException("Invalid native display escape");
        text = text.Replace("%7C", "|").Replace("%7E", "~").Replace("%3B", ";").Replace("%2C", ",").Replace("%0A", "\n").Replace("%0D", "\r").Replace("%25", "%");
        if (text.Length > 128 || text.Any(c => Char.IsControl(c) && c != '\n' && c != '\r')) throw new InvalidDataException("Invalid native display text");
        return text;
    }
    static bool Tag(string text) { return text.Length <= 160 && (text == "None" || Regex.IsMatch(text, @"^SW\.[A-Za-z0-9_.]+$")); }
    static int Integer(string text, int min, int max)
    {
        int value;
        if (!Int32.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value < min || value > max) throw new InvalidDataException("Invalid native reroll number");
        return value;
    }
}
static class RerollObserver
{
    const string Mod = "MCD2RerollBridge";
    // WinGDK 通过既有受限 WGS 关联读回执；未经确认的平台/版本由原有平台门限拒绝。
    internal static RerollSnapshot Read(int pid)
    {
        using (var process = Process.GetProcessById(pid))
        {
            bool gdk = NativeBridgeProfile.Require(process);
            DateTime started = process.StartTime.ToUniversalTime();
            string path;
            if (gdk)
            {
                var pair = GdkBridgeTransport.Resolve(GdkBridgeTransport.Root, Mod, "7");
                GdkBridgeTransport.Fresh(pair, started);
                path = pair.Receipt;
            }
            else path = Path.Combine(EquipmentBridge.Root, "MCD2RerollReceipt.sav");
            DateTime touched = File.GetLastWriteTimeUtc(path);
            if (touched < started || DateTime.UtcNow - touched > TimeSpan.FromSeconds(1.5) || touched - DateTime.UtcNow > TimeSpan.FromSeconds(1)) throw new InvalidDataException("Reroll observation is stale");
            var snapshot = RerollSnapshot.Parse(EquipmentSaveCodec.Parse(GdkBridgeTransport.ReadReliable(path), "Receipt", "Status", Mod).Value);
            // 同一次文件读取后的时间也要新鲜，不能把刚刚退出的组件缓存显示为实时连接。
            if (DateTime.UtcNow - touched > TimeSpan.FromSeconds(1.5)) throw new InvalidDataException("Reroll observation expired");
            return snapshot;
        }
    }
}

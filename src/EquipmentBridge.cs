// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 装备回收的独立通信与 SaveGame 字段编解码。
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Diagnostics;

// 游戏创建自有 SaveGame 模板，只替换组件 Command 字段；不写进程内存、角色存档或远程调用游戏函数。
static class EquipmentSaveCodec
{
    public sealed class Field
    {
        public int SizeOffset, PayloadOffset, PayloadSize, End;
        public string Value;
    }

    // 解析 SaveGame 字符串的长度、编码和字节边界。
    static string FString(BinaryReader r)
    {
        int n = r.ReadInt32();
        if (n == 0)
            return "";
        if (n == Int32.MinValue || Math.Abs(n) > 16384)
            throw new Exception("Invalid mod string");
        int bytes = n > 0 ? n : -n * 2;
        byte[] b = r.ReadBytes(bytes);
        if (b.Length != bytes)
            throw new EndOfStreamException();
        string value = (n > 0 ? Encoding.UTF8 : Encoding.Unicode).GetString(b);
        if (value.Length == 0 || value[value.Length - 1] != '\0')
            throw new Exception("Invalid string terminator");
        return value.Substring(0, value.Length - 1);
    }

    // 解析外部数据并检查结构约束；格式或协议失配不能继续使用。
    public static Field Parse(byte[] bytes, string asset, string field, string mod = "MCD2EquipmentBridge")
    {
        if (bytes.Length < 64 || bytes.Length > 65536)
            throw new Exception("Invalid mod save size");
        using (var m = new MemoryStream(bytes))
        using (var r = new BinaryReader(m))
        {
            if (r.ReadUInt32() != 0x53415647 || r.ReadInt32() != 3)
                throw new Exception("Unsupported mod save format");
            r.ReadInt32();
            int ue5 = r.ReadInt32();
            if (ue5 < 1012 || ue5 > 1017)
                throw new Exception("Unsupported game save version");
            r.ReadUInt16();
            r.ReadUInt16();
            r.ReadUInt16();
            r.ReadUInt32();
            FString(r);
            if (r.ReadInt32() != 3)
                throw new Exception("Unsupported custom versions");
            int count = r.ReadInt32();
            if (count < 0 || count > 512)
                throw new Exception("Invalid custom versions");
            m.Position += count * 20L;
            if (FString(r) != "/Game/Mods/" + mod + "/" + asset + "." + asset + "_C")
                throw new Exception("Unexpected mod save class");
            if (r.ReadByte() != 0 || FString(r) != field || FString(r) != "StrProperty" || r.ReadInt32() != 0)
                throw new Exception("Unexpected mod save fields");
            var result = new Field
            {
                SizeOffset = (int)m.Position
            };
            result.PayloadSize = r.ReadInt32();
            if (result.PayloadSize < 4 || result.PayloadSize > 32768 || r.ReadByte() != 0)
                throw new Exception("Invalid mod property flags");
            result.PayloadOffset = (int)m.Position;
            result.Value = FString(r);
            result.End = (int)m.Position;
            if (result.End - result.PayloadOffset != result.PayloadSize || FString(r) != "None")
                throw new Exception("Mod payload length mismatch");
            // 保留 UObject 可选尾零，拒绝额外未知属性。
            if (m.Position != m.Length && (m.Length - m.Position != 4 || r.ReadInt32() != 0))
                throw new Exception("Unexpected mod save suffix");
            return result;
        }
    }

    // 替换已校验自有通信模板字段，不修改角色存档。
    public static byte[] Replace(byte[] template, string value, string mod = "MCD2EquipmentBridge")
    {
        if (value.Length > 512 || value.Any(c => c > 127 || c == '\0'))
            throw new Exception("Invalid organizer command");
        var f = Parse(template, "Request", "Command", mod);
        byte[] text = Encoding.UTF8.GetBytes(value + "\0");
        using (var m = new MemoryStream())
        using (var w = new BinaryWriter(m))
        {
            w.Write(template, 0, f.SizeOffset);
            w.Write(text.Length + 4);
            w.Write(template, f.SizeOffset + 4, f.PayloadOffset - f.SizeOffset - 4);
            w.Write(text.Length);
            w.Write(text);
            w.Write(template, f.End, template.Length - f.End);
            return m.ToArray();
        }
    }
}

sealed class EquipmentBridgeState
{
    public string Epoch, Status;
    public int Sequence, Sold, Eligible;
}

sealed class EquipmentBridge
{
    public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dungeons2", "Saved", "SaveGames");
    // 自有装备回收请求槽路径，不是角色存档。
    public static string RequestPath
    {
        get
        {
            return Path.Combine(Root, "MCD2EquipmentRequest.sav");
        }
    }

    // 自有装备回收回执槽路径，只接受匹配的保存类。
    public static string ReceiptPath
    {
        get
        {
            return Path.Combine(Root, "MCD2EquipmentReceipt.sav");
        }
    }

    string epoch;
    int sequence;
    DateTime started;
    EquipmentPolicy policy;
    bool existing;
    int limit;
    public EquipmentBridge(int pid, EquipmentPolicy rules, bool includeExisting = false, int saleLimit = 0)
    {
        // 只更新本体的 Steam 准入；出售仍沿用预编译组件与原协议，绝不重建旧 stub。
        using (var process = Process.GetProcessById(pid))
        {
            GameBuildCompatibility.RequireSteam(process);
            started = process.StartTime.ToUniversalTime();
        }
        policy = rules;
        epoch = Guid.NewGuid().ToString("N");
        existing = includeExisting;
        limit = saleLimit;
        if (!File.Exists(RequestPath) || !File.Exists(ReceiptPath) || File.GetLastWriteTimeUtc(ReceiptPath) < started)
            throw new Exception(L10n.T("未检测到装备回收组件，请安装蓝图加载器及组件后重启游戏"));
        Read();
    }

    public EquipmentBridgeState Read()
    {
        var text = EquipmentSaveCodec.Parse(File.ReadAllBytes(ReceiptPath), "Receipt", "Status").Value.Split('|');
        if (text.Length == 6 && text[0] != "2")
            throw new Exception(L10n.T("装备回收组件版本过旧，请更新后重启游戏"));
        int seq, sold, eligible;
        if (text.Length != 6 || text[0] != "2" || !Int32.TryParse(text[2], out seq) || !Int32.TryParse(text[4], out sold) || !Int32.TryParse(text[5], out eligible) || sold < 0 || eligible < 0)
            throw new Exception("Invalid equipment bridge receipt");
        return new EquipmentBridgeState
        {
            Epoch = text[1],
            Sequence = seq,
            Status = text[3],
            Sold = sold,
            Eligible = eligible
        };
    }

    // 确认回执属于当前命令/实例，避免旧回执推动 UI。
    public bool Acknowledged(EquipmentBridgeState state)
    {
        return state.Epoch == epoch && state.Sequence > 0 && state.Sequence <= sequence && File.GetLastWriteTimeUtc(ReceiptPath) >= started;
    }

    public void Pulse(bool dryRun)
    {
        sequence++;
        string normal = String.Concat(policy.Normal.Select(v => ((int)v).ToString()).ToArray()), storm = String.Concat(policy.Storm.Select(v => ((int)v).ToString()).ToArray());
        string flags = (policy.Duplicates ? "1" : "0") + (policy.KeepUpgrades ? "1" : "0") + (policy.KeepMerchant ? "1" : "0") + (policy.KeepQuest ? "1" : "0");
        Write("2|" + epoch + "|" + sequence + "|1|" + normal + "|" + storm + "|" + flags + "|" + (dryRun ? "1" : "0") + "|" + (existing ? "1" : "0") + "|" + limit);
    }

    static void Write(string command)
    {
        byte[] bytes = EquipmentSaveCodec.Replace(File.ReadAllBytes(RequestPath), command);
        string temp = RequestPath + ".toolbox.tmp";
        File.WriteAllBytes(temp, bytes);
        File.Replace(temp, RequestPath, null);
    }

    // 终止本工具拥有的请求并恢复禁用命令，清理未完成状态。
    public static void Stop()
    {
        if (File.Exists(RequestPath))
            Write("OFF");
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 统一 Steam 自有槽和 WinGDK 私有 WGS 通道。
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

// 收集、战斗和铁匠使用同一固定信封；出售存档格式保持原样。
// 固定 2048 字符双份 ASCII 信封编码器；协议与槽模板修改必须两端一致。
static class NativeMailboxCodec
{
    public const int Width = 2048;
    // 将命令复制两份并补空格至 2048 字符；内容必须是有界 ASCII。
    public static string Encode(string command)
    {
        if (String.IsNullOrEmpty(command) || command.Length > 1023 || command.Any(c => c < ' ' || c > 126 || c == '#') || command.TrimEnd() != command)
            throw new Exception("Invalid native mailbox command");
        return (command + "#" + command).PadRight(Width, ' ');
    }

    public static string Decode(string encoded)
    {
        if (encoded == null || encoded.Length != Width || encoded.Any(c => c < ' ' || c > 126))
            throw new Exception("Native mailbox requires updated components");
        var copies = encoded.TrimEnd(' ').Split('#');
        if (copies.Length != 2 || copies[0].Length == 0 || copies[0] != copies[1] || Encode(copies[0]) != encoded)
            throw new Exception("Incomplete native mailbox command");
        return copies[0];
    }

    // 仅替换已校验请求模板内的固定 Command 字节区，不改变文件布局。
    public static byte[] Replace(byte[] template, string encoded, string mod)
    {
        if (mod != "MCD2CombatBridge" && mod != "MCD2NearbyLootBridge" && mod != "MCD2RerollBridge")
            throw new Exception("Unexpected native mailbox class");
        if (encoded == null || encoded.Length != Width || encoded.Any(c => c < ' ' || c > 126))
            throw new Exception("Invalid native mailbox payload");
        var f = EquipmentSaveCodec.Parse(template, "Request", "Command", mod);
        if (f.Value.Length != Width || f.PayloadSize != Width + 5 || BitConverter.ToInt32(template, f.PayloadOffset) != Width + 1)
            throw new Exception("Native mailbox template requires updated components");
        var bytes = (byte[])template.Clone();
        Encoding.ASCII.GetBytes(encoded).CopyTo(bytes, f.PayloadOffset + 4);
        return bytes;
    }

    public static string Probe(string protocol, string instance, string epoch, double clock)
    {
        string expiry = (clock + 1).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        if (protocol == "6")
            return "6|" + instance + "|" + epoch + "|1|probe|-|-|-|0|0|0|" + expiry + "|1|0|500|1000";
        if (protocol == "5")
            return String.Join("|", new[] { "5", instance, epoch, "1", "probe", "-", "-", "-", "1", "0", "0", "0", "0", "0", "0", "0", "1", "0", "80", expiry });
        if (protocol == "7")
            return String.Join("|", new[] { "7", instance, epoch, "1", "probe", expiry });
        throw new Exception("Unsupported native transport probe");
    }
}

// 平台准入检查；GDK 包、模块和运行结构必须属于已验证范围。
static class NativeBridgeProfile
{
    internal const string GdkPackage = "Microsoft.MinecraftDungeons2_1.1.1.0_x64__8wekyb3d8bbwe";
    internal const int GdkImageSize = 197652480;
    // Windows 进程句柄入口；访问权限由调用方传入，句柄需配对关闭。
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    // 关闭 Windows 原生句柄，与成功打开的句柄配对。
    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr handle);
    // 查询进程的包身份，供限定已验证 WinGDK 包使用。
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern int GetPackageFullName(IntPtr process, ref uint length, StringBuilder name);
    // 对照已验证包身份、模块大小及运行结构，拒绝未适配 GDK 版本。
    internal static bool MatchesGdk(string name, string package, int imageSize, bool layoutValidated)
    {
        return name == "Dungeons-WinGDK-Shipping" && package == GdkPackage && imageSize == GdkImageSize && layoutValidated;
    }

    // 检查当前游戏平台是否满足原生组件通道的验证前提。
    internal static bool Require(Process process)
    {
        if (process.ProcessName == "Dungeons-Win64-Shipping")
        {
            // Steam 新旧版本共用精确准入；不能因安装成功而绕过未知游戏版本。
            GameBuildCompatibility.RequireSteam(process);
            return false;
        }

        if (process.ProcessName != "Dungeons-WinGDK-Shipping")
            throw new Exception("Unsupported native bridge process");
        IntPtr handle = OpenProcess(0x1000, false, process.Id);
        if (handle == IntPtr.Zero)
            throw new Exception("Cannot query GDK package identity");
        string package;
        try
        {
            uint count = 0;
            if (GetPackageFullName(handle, ref count, null) != 122 || count < 2 || count > 512)
                throw new Exception("GDK package identity unavailable");
            var name = new StringBuilder((int)count);
            if (GetPackageFullName(handle, ref count, name) != 0)
                throw new Exception("GDK package identity query failed");
            package = name.ToString();
        }
        finally
        {
            CloseHandle(handle);
        }

        bool layout = !String.IsNullOrEmpty(AdaptationRecord.Get("resolved.names")) && !String.IsNullOrEmpty(AdaptationRecord.Get("resolved.objects")) && AdaptationRecord.Get("game.executable") == "Dungeons-WinGDK-Shipping.exe";
        if (!MatchesGdk(process.ProcessName, package, process.MainModule.ModuleMemorySize, layout))
            throw new Exception(L10n.T("GDK 原生组件无法连接，请更新组件"));
        AdaptationRecord.Set("native.gdk.profile", package + "; experimental; fixed mailbox handshake required; gameplay not yet verified");
        return true;
    }
}

sealed class NativeBridgeChannel
{
    readonly string mod, protocol;
    readonly DateTime started;
    readonly bool gdk;
    readonly Func<string, NearbyLootReceipt> parse;
    string requestPath, receiptPath, instance, lastEncoded, previousEncoded, pendingEncoded;
    // 当前桥接组件实例标识，跨关卡/重启后必须重新握手。
    internal string Instance
    {
        get
        {
            return instance;
        }
    }

    public NativeBridgeChannel(int pid, string component, string version, Func<string, NearbyLootReceipt> parser)
    {
        if (component == "MCD2CombatBridge" ? version != "5" : component == "MCD2NearbyLootBridge" ? version != "6" : component == "MCD2RerollBridge" ? version != "7" : true)
            throw new Exception("Unknown native mailbox protocol");
        mod = component;
        protocol = version;
        parse = parser;
        using (var process = Process.GetProcessById(pid))
        {
            started = process.StartTime.ToUniversalTime();
            gdk = NativeBridgeProfile.Require(process);
        }

        string prefix = mod == "MCD2CombatBridge" ? "MCD2Combat" : mod == "MCD2NearbyLootBridge" ? "MCD2NearbyLoot" : "MCD2Reroll";
        if (!gdk)
        {
            requestPath = Path.Combine(EquipmentBridge.Root, prefix + "Request.sav");
            receiptPath = Path.Combine(EquipmentBridge.Root, prefix + "Receipt.sav");
        }

        Initialize();
    }

    // 建立自有请求/回执槽关联，GDK 路径还需要握手和 OFF 恢复确认。
    void Initialize()
    {
        var first = Read();
        instance = first.Instance;
        lastEncoded = EquipmentSaveCodec.Parse(ReadRequest(), "Request", "Command", mod).Value;
        if (NativeMailboxCodec.Decode(lastEncoded) != "OFF")
            throw new Exception(L10n.T("请先按 F9 停止旧工具，再连接原生组件"));
        // Steam 热修复也先执行无玩法动作的探针；文件存在或安装成功不等于当前组件能响应。
        // probe/OFF 属于既有协议，两端版本号及 Xbox 平台准入保持不变。
        Handshake();
    }

    // 取得当前场景已绑定的请求/回执关联，失效时不继续沿用旧文件。
    GdkBridgeTransport.Pair BoundPair(bool fresh)
    {
        var pair = GdkBridgeTransport.Resolve(GdkBridgeTransport.Root, mod, protocol);
        if (requestPath != null && !String.Equals(requestPath, pair.Request, StringComparison.OrdinalIgnoreCase))
            throw new Exception("GDK request container changed; reconnect component");
        var value = fresh ? GdkBridgeTransport.Fresh(pair, started) : GdkBridgeTransport.ReadReceipt(pair);
        if (instance != null && value.Instance != instance)
            throw new Exception(L10n.T("场景已变化，请重新连接原生组件"));
        requestPath = pair.Request;
        receiptPath = pair.Receipt;
        return pair;
    }

    public NearbyLootReceipt Read()
    {
        // 读取期间容器会轮换，需核对索引、描述符和内容的一致性。
        var watch = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                if (gdk)
                    BoundPair(true);
                var touched = File.GetLastWriteTimeUtc(receiptPath);
                double age = (DateTime.UtcNow - touched).TotalSeconds;
                if (touched < started || age > 1.5 || age < -1)
                    throw new Exception(L10n.T("原生组件未响应，暂停动作"));
                var r = parse(EquipmentSaveCodec.Parse(gdk ? GdkBridgeTransport.ReadReliable(receiptPath) : File.ReadAllBytes(receiptPath), "Receipt", "Status", mod).Value);
                if (instance != null && r.Instance != instance)
                    throw new Exception(L10n.T("场景已变化，请重新连接原生组件"));
                return r;
            }
            catch (IOException)
            {
                if (watch.ElapsedMilliseconds >= 1200)
                    throw;
                Thread.Sleep(20);
            }
        }
    }

    byte[] ReadRequest()
    {
        return gdk ? GdkBridgeTransport.ReadReliable(requestPath) : File.ReadAllBytes(requestPath);
    }

    // 执行 probe → TransportReady → OFF → Disabled，先验证通道和恢复能力再允许动作。
    void Handshake()
    {
        string token = Guid.NewGuid().ToString("N");
        double readyClock = 0;
        try
        {
            var first = Read();
            Write(NativeMailboxCodec.Probe(protocol, instance, token, first.Clock));
            var watch = Stopwatch.StartNew();
            bool ready = false;
            while (watch.ElapsedMilliseconds < 1800)
            {
                Thread.Sleep(20);
                var r = Read();
                if (r.Instance == instance && r.Epoch == token && r.Sequence == 1 && r.Status == "TransportReady" && r.Clock > first.Clock)
                {
                    ready = true;
                    readyClock = r.Clock;
                    break;
                }
            }

            if (!ready)
                throw new GameCompatibilityException("组件通信未确认，请重启游戏后重新连接；未发送游戏动作");
            ToolboxLog.Write("Native.TransportHandshake", mod + " platform=" + (gdk ? "WinGDK" : "Steam") + " protocol=" + protocol + " TransportReady; handshake completed");
        }
        finally
        {
            Stop();
        }

        var restored = Stopwatch.StartNew();
        while (restored.ElapsedMilliseconds < 1800)
        {
            Thread.Sleep(20);
            var r = Read();
            if (r.Instance == instance && r.Epoch == token && r.Sequence == 1 && r.Status == "Disabled" && r.Clock > readyClock)
                return;
        }

        throw new GameCompatibilityException("组件未确认停止状态，请重启游戏后重新连接；未发送游戏动作");
    }

    public void Write(string command)
    {
        string encoded = NativeMailboxCodec.Encode(command);
        if (gdk)
            BoundPair(true);
        byte[] before = ReadRequest();
        var f = EquipmentSaveCodec.Parse(before, "Request", "Command", mod);
        if (f.Value != lastEncoded || pendingEncoded != null)
            throw new Exception("Native mailbox changed or an earlier write was interrupted");
        previousEncoded = lastEncoded;
        pendingEncoded = encoded;
        WriteEncoded(before, encoded);
        lastEncoded = encoded;
        previousEncoded = null;
        pendingEncoded = null;
    }

    // 把已编码固定信封写入自有 Command 字段，保持其他字节不变。
    void WriteEncoded(byte[] before, string encoded)
    {
        byte[] after = NativeMailboxCodec.Replace(before, encoded, mod);
        if (!gdk)
        {
            string temp = requestPath + ".toolbox.tmp";
            File.WriteAllBytes(temp, after);
            File.Replace(temp, requestPath, null);
            return;
        }

        int offset = EquipmentSaveCodec.Parse(before, "Request", "Command", mod).PayloadOffset + 4;
        // 不能替换 WGS 文件、调整 blob 大小或更新云元数据。
        using (var stream = new FileStream(requestPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        {
            if (stream.Length != before.Length)
                throw new IOException("GDK request length changed before write");
            byte[] check = new byte[before.Length];
            int at = 0, n;
            while (at < check.Length && (n = stream.Read(check, at, check.Length - at)) > 0)
                at += n;
            if (at != check.Length || !check.SequenceEqual(before))
                throw new IOException("GDK request was replaced by another writer");
            stream.Position = offset;
            stream.Write(after, offset, NativeMailboxCodec.Width);
            stream.Flush(true);
        }

        if (!ReadRequest().SequenceEqual(after))
            throw new IOException("GDK fixed mailbox write could not be verified");
    }

    // 终止本工具拥有的请求并恢复禁用命令，清理未完成状态。
    public void Stop()
    {
        // 仅在相同场景恢复本通道自己写下的命令或部分写入。
        if (gdk)
            BoundPair(false);
        else if (Read().Instance != instance)
            return;
        byte[] before = ReadRequest();
        string current = EquipmentSaveCodec.Parse(before, "Request", "Command", mod).Value;
        string off = NativeMailboxCodec.Encode("OFF");
        if (current == off)
        {
            lastEncoded = off;
            previousEncoded = null;
            pendingEncoded = null;
            return;
        }

        bool owned = current == lastEncoded;
        if (!owned && previousEncoded != null && pendingEncoded != null && current.Length == NativeMailboxCodec.Width)
            owned = Enumerable.Range(0, current.Length).All(i => current[i] == previousEncoded[i] || current[i] == pendingEncoded[i]);
        if (!owned)
            throw new Exception("Another command replaced this native request; stop refused to overwrite it");
        previousEncoded = current;
        pendingEncoded = off;
        WriteEncoded(before, off);
        lastEncoded = off;
        previousEncoded = null;
        pendingEncoded = null;
    }
}

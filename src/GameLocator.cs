// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 识别游戏进程、PE 节和运行时名称/对象表，建立兼容布局。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Security.Cryptography;

// GameLayout 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
sealed class GameLayout
{
    public long Names, Objects;
    public string NameSource, ObjectSource;
    public Func<long, int, bool> Executable;
}

// 遥测和组件安装共用精确 EXE 名白名单。
static class GameProcess
{
    static readonly string[] names =
    {
        "Dungeons-Win64-Shipping",
        "Dungeons-WinGDK-Shipping"
    };
    // 按完整进程名白名单识别 Steam/WinGDK 客户端。
    internal static bool Supported(string name)
    {
        return names.Any(n => String.Equals(n, name, StringComparison.OrdinalIgnoreCase));
    }

    // 枚举支持的游戏进程，失败时释放已打开的进程对象。
    static Process[] Running()
    {
        var found = new List<Process>();
        try
        {
            foreach (string name in names)
                found.AddRange(Process.GetProcessesByName(name));
            return found.ToArray();
        }
        catch
        {
            foreach (Process p in found)
                p.Dispose();
            throw;
        }
    }

    // 仅检查游戏是否仍在运行，用于安装前阻断更新。
    internal static bool IsRunning()
    {
        Process[] found = Running();
        try
        {
            return found.Length > 0;
        }
        finally
        {
            foreach (Process p in found)
                p.Dispose();
        }
    }

    // 连接只允许一个游戏进程，零个或多个实例均报错。
    static void RequireSingle(int count)
    {
        if (count == 0)
            throw new Exception("请启动游戏并进入关卡，再连接。");
        if (count != 1)
            throw new Exception("检测到多个游戏进程，请只保留一个游戏实例后连接。");
    }

    // 取得唯一游戏进程并记住经校验的安装目录。
    internal static Process OpenSingle()
    {
        Process[] found = Running();
        try
        {
            RequireSingle(found.Length);
            GameInstallLocator.RememberProcess(found[0]);
            return found[0];
        }
        catch
        {
            foreach (Process p in found)
                p.Dispose();
            throw;
        }
    }

    // 接受游戏根目录，也接受 Xbox 带 Content 子目录的外层安装目录。
    // 确认游戏根目录或 Xbox 外层 Content 目录包含真实游戏布局。
    internal static string InstallRoot(string selected, Func<string, bool> directoryExists, Func<string, bool> fileExists)
    {
        string full = Path.GetFullPath(selected);
        var valid = new List<string>();
        foreach (string root in new[]
        {
            full,
            Path.Combine(full, "Content")
        }

        )
        {
            if (!directoryExists(Path.Combine(root, "Dungeons", "Content", "Paks")))
                continue;
            string binaries = Path.Combine(root, "Dungeons", "Binaries");
            if (fileExists(Path.Combine(binaries, "Win64", names[0] + ".exe")) || fileExists(Path.Combine(binaries, "WinGDK", names[1] + ".exe")) || fileExists(Path.Combine(binaries, "Win64", names[1] + ".exe")))
                valid.Add(root);
        }

        if (valid.Count != 1)
            throw new Exception(L10n.T("请选择包含 Dungeons 文件夹的游戏安装目录"));
        return valid[0];
    }

}

// 优先真实本地符号，特征仅用于定位数据，不调用游戏函数；所有来源的候选都经过同样名称/对象结构验证。
static class GameLocator
{
    // Section 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
    sealed class Section
    {
        public long Address;
        public int Size;
        public uint Flags;
    }

    internal sealed class Pattern
    {
        public byte[] Bytes;
        public bool[] Fixed;
        public string Text;
        public Pattern(string text)
        {
            Text = text;
            string[] parts = text.Split(' ');
            Bytes = new byte[parts.Length];
            Fixed = new bool[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                Fixed[i] = parts[i] != "??";
                if (Fixed[i])
                    Bytes[i] = Convert.ToByte(parts[i], 16);
            }
        }

        // 比较字节序列与带掩码特征，后续仍需验证候选用途。
        public IEnumerable<int> Matches(byte[] bytes)
        {
            for (int i = 0; i <= bytes.Length - Bytes.Length; i++)
            {
                if (bytes[i] != Bytes[0])
                    continue;
                int j = 1;
                for (; j < Bytes.Length; j++)
                    if (Fixed[j] && bytes[i + j] != Bytes[j])
                        break;
                if (j == Bytes.Length)
                    yield return i;
            }
        }
    }

    static readonly Pattern[] namePatterns =
    {
        new Pattern("48 8D 35 ?? ?? ?? ?? EB ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B F0 C6 05 ?? ?? ?? ?? 01 8B D3 0F B7 C3 C1 EA 10"),
        new Pattern("48 8D 05 ?? ?? ?? ?? 48 8B D1 48 8B C8 48 83 C4 20 5B E9 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 8B D3")
    };
    static readonly Pattern[] objectPatterns =
    {
        new Pattern("48 8B 05 ?? ?? ?? ?? 48 8B 0C C8 48 8D 0C D1"),
        new Pattern("48 8B 05 ?? ?? ?? ?? 48 8B 0C C8 48 8D 1C D1"),
        new Pattern("48 8B 05 ?? ?? ?? ?? 48 8B 0C C8 8B 44 D1 08")
    };
    // 计算 x64 指令的模块内相对引用地址，不直接调用该地址。
    static long Rip(byte[] code, int at, long address)
    {
        return checked(address + at + 7 + BitConverter.ToInt32(code, at + 3));
    }

    // 有界解析 PE 节表，区分可执行代码与数据区。
    static List<Section> Sections(Memory memory, long image, int imageSize)
    {
        byte[] dos = memory.Read(image, 64);
        if (BitConverter.ToUInt16(dos, 0) != 0x5a4d)
            throw new Exception("游戏映像验证失败。");
        int pe = BitConverter.ToInt32(dos, 60);
        if (pe < 64 || pe > 1048576)
            throw new Exception("游戏映像验证失败。");
        byte[] header = memory.Read(image + pe, 24);
        int count = BitConverter.ToUInt16(header, 6), optional = BitConverter.ToUInt16(header, 20);
        if (BitConverter.ToUInt32(header, 0) != 0x4550 || BitConverter.ToUInt16(header, 4) != 0x8664 || count < 1 || count > 96 || optional < 112 || optional > 4096)
            throw new Exception("游戏映像验证失败。");
        if (BitConverter.ToUInt16(memory.Read(image + pe + 24, 2), 0) != 0x20b)
            throw new Exception("游戏映像验证失败。");
        byte[] table = memory.Read(image + pe + 24 + optional, count * 40);
        var result = new List<Section>();
        for (int i = 0; i < count; i++)
        {
            int at = i * 40, size = BitConverter.ToInt32(table, at + 8), rva = BitConverter.ToInt32(table, at + 12);
            if (size < 0 || rva < 0 || (long)rva + size > imageSize)
                throw new Exception("游戏映像区段无效。");
            result.Add(new Section { Address = image + rva, Size = size, Flags = BitConverter.ToUInt32(table, at + 36) });
        }

        return result;
    }

    // 校验候选地址是否落在合适的 PE 数据区。
    static bool Data(List<Section> sections, long address, int size)
    {
        return sections.Any(s => (s.Flags & 0x80000000) != 0 && address >= s.Address && address + size >= address && address + size <= s.Address + s.Size);
    }

    // 校验候选地址是否为本模块可执行代码范围。
    static bool Code(List<Section> sections, long address, int size)
    {
        return sections.Any(s => (s.Flags & 0x20000000) != 0 && address >= s.Address && address + size >= address && address + size <= s.Address + s.Size);
    }

    // 解析候选名称池条目，用于验证布局而非直接信任特征命中。
    static string Name(Memory m, long pool, int index)
    {
        uint n = unchecked((uint)index);
        if ((n >> 16) > 8191)
            throw new Exception("名称索引无效。");
        long block = m.Q(pool + 16 + (n >> 16) * 8), address = block + (n & 65535) * 2;
        ushort h = BitConverter.ToUInt16(m.Read(address, 2), 0);
        int length = h >> 6;
        if (length < 1 || length > 1024)
            throw new Exception("名称长度无效。");
        return ((h & 1) != 0 ? Encoding.Unicode : Encoding.UTF8).GetString(m.Read(address + 2, length * ((h & 1) != 0 ? 2 : 1)));
    }

    // 通过已知名称及布局约束验证名称池候选。
    static bool NamesValid(Memory m, List<Section> sections, long pool)
    {
        try
        {
            return Data(sections, pool, 24) && Name(m, pool, 0) == "None";
        }
        catch
        {
            return false;
        }
    }

    // 通过对象槽、数量和类身份验证对象表候选。
    static bool ObjectsValid(Memory m, List<Section> sections, long array, long pool)
    {
        try
        {
            if (!Data(sections, array, 32))
                return false;
            long chunks = m.Q(array);
            int maximum = m.I(array + 16), count = m.I(array + 20), maxChunks = m.I(array + 24), numChunks = m.I(array + 28);
            if (count < 1000 || count > 2000000 || maximum < count || maximum > 4000000 || numChunks != (count + 65535) / 65536 || maxChunks < numChunks || maxChunks > 128)
                return false;
            int valid = 0;
            var classes = new HashSet<string>();
            byte[] first = m.Read(m.Q(chunks), Math.Min(count, 128) * 24);
            for (int i = 0; i < first.Length; i += 24)
            {
                long obj = BitConverter.ToInt64(first, i);
                if (obj == 0)
                    continue;
                byte[] b = m.Read(obj, 40);
                if (BitConverter.ToInt32(b, 12) != i / 24)
                    return false;
                long cl = BitConverter.ToInt64(b, 16);
                string cn = Name(m, pool, m.I(cl + 24)), on = Name(m, pool, BitConverter.ToInt32(b, 24));
                if (cn.Length == 0 || on.Length == 0 || cn.IndexOf('\0') >= 0 || on.IndexOf('\0') >= 0)
                    return false;
                int classIndex = m.I(cl + 12);
                if (classIndex < 0 || classIndex >= count)
                    return false;
                long classSlot = m.Q(chunks + (classIndex / 65536) * 8) + (classIndex % 65536) * 24;
                if (m.Q(classSlot) != cl)
                    return false;
                classes.Add(cn);
                if (++valid >= 16)
                    break;
            }

            return valid >= 8 && classes.Contains("Class");
        }
        catch
        {
            return false;
        }
    }

    // 要求经过结构验证后只剩一个候选地址。
    static long Unique(IEnumerable<long> candidates, Func<long, bool> validate, string what)
    {
        long[] valid = candidates.Distinct().Where(validate).ToArray();
        if (valid.Length > 1)
            throw new Exception("游戏定位结果不唯一：" + what);
        return valid.Length == 1 ? valid[0] : 0;
    }

    // 优先使用可核对的符号来源，并验证候选数据结构。
    static long Preferred(IEnumerable<long> symbols, Func<IEnumerable<long>> signatures, Func<long, bool> validate, string what, out string source)
    {
        long result = Unique(symbols, validate, what);
        source = "symbols";
        if (result != 0)
            return result;
        source = "signatures";
        return Unique(signatures(), validate, what);
    }

    // 在限定模块代码范围内扫描特征，保留多匹配拒绝规则。
    static IEnumerable<long> Scan(Memory m, List<Section> sections, Pattern[] patterns, long image)
    {
        const int step = 1024 * 1024, overlap = 64;
        var found = new HashSet<long>();
        foreach (Section s in sections.Where(x => (x.Flags & 0x20000000) != 0))
            for (int off = 0; off < s.Size; off += step)
            {
                byte[] bytes = m.Read(s.Address + off, Math.Min(step + overlap, s.Size - off));
                foreach (Pattern p in patterns)
                    foreach (int at in p.Matches(bytes))
                    {
                        long target = Rip(bytes, at, s.Address + off);
                        AdaptationRecord.Set("signature.match.rva.0x" + (s.Address + off + at - image).ToString("X"), p.Text + " -> candidate RVA=0x" + (target - image).ToString("X"));
                        if (Data(sections, target, 32) && found.Add(target))
                            yield return target;
                    }
            }
    }

    // 解析函数中的相对引用，取得可校验的全局数据候选。
    static IEnumerable<long> FunctionReferences(Memory m, List<Section> sections, LocalSymbols.Symbol symbol)
    {
        if (symbol == null || symbol.Size < 7 || symbol.Size > 8192 || !Code(sections, symbol.Address, symbol.Size))
            yield break;
        byte[] b = m.Read(symbol.Address, symbol.Size);
        for (int i = 0; i <= b.Length - 7; i++)
            if ((b[i] == 0x48 || b[i] == 0x4c) && (b[i + 1] == 0x8d || b[i + 1] == 0x8b) && (b[i + 2] & 0xc7) == 5)
            {
                long a = Rip(b, i, symbol.Address);
                if (Data(sections, a, 32))
                    yield return a;
            }
    }

    // 基础遥测可在文件指纹不可用时继续；不能把内存镜像哈希冒充 EXE 哈希或视为版本已核实。
    // 计算当前游戏 EXE 指纹，供目录和适配范围校验。
    static string FileFingerprint(Func<Stream> open, out string status)
    {
        try
        {
            using (Stream file = open())
            using (var sha = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
                status = "verified file SHA-256";
                return hash;
            }
        }
        catch (UnauthorizedAccessException)
        {
            status = "unavailable: file access denied; version-gated features disabled";
        }
        catch (System.Security.SecurityException)
        {
            status = "unavailable: file security restriction; version-gated features disabled";
        }
        catch (IOException)
        {
            status = "unavailable: file cannot be read; version-gated features disabled";
        }

        return "unavailable";
    }

    // 结合本地符号、代码特征和运行结构建立名称池/对象表布局。
    internal static GameLayout Resolve(Memory m, Process process)
    {
        AdaptationRecord.Begin();
        ProcessModule module = process.MainModule;
        long image = module.BaseAddress.ToInt64();
        AdaptationRecord.Set("lookup.order", "local symbols / named functions -> signatures -> live structure validation -> reflected property names");
        AdaptationRecord.Set("game.executable", Path.GetFileName(module.FileName));
        AdaptationRecord.Set("game.distribution", String.Equals(Path.GetFileNameWithoutExtension(module.FileName), "Dungeons-WinGDK-Shipping", StringComparison.OrdinalIgnoreCase) ? "Xbox PC / Microsoft Store GDK; runtime compatibility requires validation" : "Steam Win64");
        var sections = Sections(m, image, module.ModuleMemorySize);
        var layout = new GameLayout
        {
            Executable = (address, size) => Code(sections, address, size)
        };
        string fingerprintStatus;
        string fingerprint = FileFingerprint(() => File.OpenRead(module.FileName), out fingerprintStatus);
        AdaptationRecord.Set("game.sha256", fingerprint);
        AdaptationRecord.Set("game.sha256.status", fingerprintStatus);
        if (fingerprint == "unavailable")
            ToolboxLog.Write("Reader.Fingerprint", fingerprintStatus + "; continuing validated read-only lookup");
        foreach (Pattern pattern in namePatterns)
            AdaptationRecord.Set("signature.catalog.names." + Array.IndexOf(namePatterns, pattern), pattern.Text);
        foreach (Pattern pattern in objectPatterns)
            AdaptationRecord.Set("signature.catalog.objects." + Array.IndexOf(objectPatterns, pattern), pattern.Text);
        using (var symbols = new LocalSymbols(m.Handle, module.FileName, image, module.ModuleMemorySize))
        {
            var pools = new List<long>();
            foreach (string name in new[]
            {
                "NamePoolData",
                "GNamePool",
                "FName::NamePoolData"
            }

            )
            {
                var s = symbols.Find(name);
                if (s != null)
                    pools.Add(s.Address);
            }

            foreach (string name in new[]
            {
                "FName::GetPlainNameString",
                "FName::ToString",
                "FName::GetDisplayNameEntry"
            }

            )
                pools.AddRange(FunctionReferences(m, sections, symbols.Find(name)));
            layout.Names = Preferred(pools, () => Scan(m, sections, namePatterns, image), a => NamesValid(m, sections, a), "名称表", out layout.NameSource);
            if (layout.Names == 0)
                throw new Exception("找不到经过验证的游戏名称表，需要更新适配。");
            var objects = new List<long>();
            foreach (string name in new[]
            {
                "GUObjectArray",
                "GObjectArray"
            }

            )
            {
                var s = symbols.Find(name);
                if (s != null)
                    for (int off = 0; off <= 64; off += 8)
                        objects.Add(s.Address + off);
            }

            foreach (string name in new[]
            {
                "FUObjectArray::IndexToObject",
                "FWeakObjectPtr::Get",
                "FWeakObjectPtr::IsValid"
            }

            )
                objects.AddRange(FunctionReferences(m, sections, symbols.Find(name)));
            layout.Objects = Preferred(objects, () => Scan(m, sections, objectPatterns, image), a => ObjectsValid(m, sections, a, layout.Names), "对象表", out layout.ObjectSource);
            if (layout.Objects == 0)
                throw new Exception("找不到经过验证的游戏对象表，需要更新适配。");
        }

        AdaptationRecord.Set("resolved.names", layout.NameSource + " RVA=0x" + (layout.Names - image).ToString("X"));
        AdaptationRecord.Set("resolved.objects", layout.ObjectSource + " RVA=0x" + (layout.Objects - image).ToString("X"));
        ToolboxLog.Write("Reader.Locator", "names=" + layout.NameSource + " objects=" + layout.ObjectSource + "; validated live tables; properties resolved by reflected names");
        return layout;
    }

}

// DbgHelp 使用串行本地匹配符号上下文，不访问符号服务器。
sealed class LocalSymbols : IDisposable
{
    // 与原生 ABI 对应的数据结构；字段顺序、类型及 StructLayout 决定字节布局，不能仅为美观调整。
    internal sealed class Symbol
    {
        public long Address;
        public int Size;
    }

    // 与原生 ABI 对应的数据结构；字段顺序、类型及 StructLayout 决定字节布局，不能仅为美观调整。
    [StructLayout(LayoutKind.Sequential)]
    struct SymbolInfo
    {
        public uint SizeOfStruct, TypeIndex;
        public ulong Reserved1, Reserved2;
        public uint Index, Size;
        public ulong ModBase;
        public uint Flags;
        public ulong Value, Address;
        public uint Register, Scope, Tag, NameLen, MaxNameLen;
        public byte Name;
    }

    [DllImport("dbghelp.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern bool SymInitialize(IntPtr process, string path, bool invade);
    // 登记当前模块以便解析本地符号。
    [DllImport("dbghelp.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern ulong SymLoadModuleEx(IntPtr process, IntPtr file, string image, string module, ulong address, uint size, IntPtr data, uint flags);
    // 按名称查询真实存在的本地调试符号。
    [DllImport("dbghelp.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern bool SymFromName(IntPtr process, string name, IntPtr info);
    // 设置符号解析选项，调用结束应恢复上下文。
    [DllImport("dbghelp.dll")]
    static extern uint SymSetOptions(uint options);
    // 读取当前符号解析选项。
    [DllImport("dbghelp.dll")]
    static extern uint SymGetOptions();
    // 释放本对象的符号查询上下文。
    [DllImport("dbghelp.dll")]
    static extern bool SymCleanup(IntPtr process);
    static readonly object gate = new object ();
    IntPtr handle;
    bool active, locked;
    uint oldOptions;
    long image;
    int imageSize;
    public LocalSymbols(IntPtr process, string path, long address, int size)
    {
        System.Threading.Monitor.Enter(gate);
        locked = true;
        handle = process;
        image = address;
        imageSize = size;
        try
        {
            oldOptions = SymGetOptions();
            SymSetOptions(0x2 | 0x4 | 0x80 | 0x200 | 0x1000 | 0x80000 | 0x02000000);
            active = SymInitialize(handle, Path.GetDirectoryName(path) + ";" + AppDomain.CurrentDomain.BaseDirectory, false);
            AdaptationRecord.Set("symbols.initialize", active ? "local search only; matching symbols required" : "unavailable; win32=" + Marshal.GetLastWin32Error());
            if (active)
            {
                ulong loaded = SymLoadModuleEx(handle, IntPtr.Zero, path, "MCD2Game", unchecked((ulong)address), (uint)size, IntPtr.Zero, 0);
                AdaptationRecord.Set("symbols.module", loaded != 0 ? "loaded; local symbols only" : "unavailable; win32=" + Marshal.GetLastWin32Error());
            }
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    // 从当前本地符号上下文查找真实可用符号；反射名不算恢复的 PDB。
    public Symbol Find(string name)
    {
        AdaptationRecord.Set("symbol." + name, "unavailable");
        if (!active)
            return null;
        int size = Marshal.SizeOf(typeof(SymbolInfo));
        IntPtr buffer = Marshal.AllocHGlobal(size + 1024);
        try
        {
            byte[] empty = new byte[size + 1024];
            Marshal.Copy(empty, 0, buffer, empty.Length);
            Marshal.StructureToPtr(new SymbolInfo { SizeOfStruct = (uint)size, MaxNameLen = 1024 }, buffer, false);
            if (!SymFromName(handle, "MCD2Game!" + name, buffer))
            {
                AdaptationRecord.Set("symbol." + name, "unavailable; win32=" + Marshal.GetLastWin32Error());
                return null;
            }

            var result = (SymbolInfo)Marshal.PtrToStructure(buffer, typeof(SymbolInfo));
            long a = unchecked((long)result.Address);
            if (unchecked((long)result.ModBase) != image || a < image || a >= image + imageSize || (result.Flags & (0x100 | 0x400 | 0x40 | 0x4000)) != 0 || result.Size > Int32.MaxValue)
                return null;
            AdaptationRecord.Set("symbol." + name, "RVA=0x" + (a - image).ToString("X") + " size=" + result.Size);
            return new Symbol
            {
                Address = a,
                Size = (int)result.Size
            };
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    // 释放本对象拥有的句柄、绘图对象或监听资源，避免退出后继续占用。
    public void Dispose()
    {
        try
        {
            if (active)
            {
                SymCleanup(handle);
                active = false;
            }

            SymSetOptions(oldOptions);
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
        finally
        {
            if (locked)
            {
                locked = false;
                System.Threading.Monitor.Exit(gate);
            }
        }
    }
}

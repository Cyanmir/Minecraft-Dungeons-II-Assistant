// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 基础遥测层：只读进程内存、验证 UObject 身份、解析反射字段及法器冷却。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

// 游戏遥测只读，输入使用普通键盘消息；不写游戏进程内存，不发送网络请求。
// 只读进程句柄封装；所有读取限制长度/地址，Dispose 负责关闭句柄。
sealed class Memory : IDisposable
{
    // Windows 进程句柄入口；访问权限由调用方传入，句柄需配对关闭。
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr OpenProcess(uint a, bool inherit, int pid);
    // Windows 只读进程内存入口；调用方检查地址、长度和实际读取字节数。
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool ReadProcessMemory(IntPtr h, IntPtr a, byte[] b, UIntPtr size, out UIntPtr got);
    // 关闭 Windows 原生句柄，与成功打开的句柄配对。
    [DllImport("kernel32.dll")]
    static extern bool CloseHandle(IntPtr h);
    IntPtr handle;
    // 本读取器拥有的进程句柄；只能在对象有效期间使用。
    internal IntPtr Handle
    {
        get
        {
            return handle;
        }
    }

    public Memory(int pid)
    {
        handle = OpenProcess(0x410, false, pid);
        if (handle == IntPtr.Zero)
            throw new Exception("无法连接游戏进程。");
    }

    public byte[] Read(long a, int n)
    {
        if (a < 65536 || a > 0x7fffffffffffL || n < 0 || n > 1600000)
            throw new Exception("读取地址验证失败。");
        byte[] b = new byte[n];
        UIntPtr got;
        if (!ReadProcessMemory(handle, (IntPtr)a, b, (UIntPtr)n, out got) || got.ToUInt64() != (ulong)n)
            throw new Exception("游戏正在切换场景或已退出，请重新连接。");
        return b;
    }

    // 按小端布局读取 64 位整数/指针；上层仍需验证指针和 UObject 身份。
    public long Q(long a)
    {
        return BitConverter.ToInt64(Read(a, 8), 0);
    }

    // 按小端布局读取 32 位整数，长度校验由 Read 统一执行。
    public int I(long a)
    {
        return BitConverter.ToInt32(Read(a, 4), 0);
    }

    // 读取单精度浮点数并拒绝 NaN/无穷值，防止坏遥测进入判断。
    public float F(long a)
    {
        float f = BitConverter.ToSingle(Read(a, 4), 0);
        if (float.IsNaN(f) || float.IsInfinity(f))
            throw new Exception("游戏数值验证失败。");
        return f;
    }

    // 释放本对象拥有的句柄、绘图对象或监听资源，避免退出后继续占用。
    public void Dispose()
    {
        if (handle != IntPtr.Zero)
        {
            CloseHandle(handle);
            handle = IntPtr.Zero;
        }
    }
}

// UObject 身份令牌；Address 不能独立证明身份，Index/Serial 用于识别地址复用。
sealed class Identity
{
    public long Address, Class, Slot;
    public int Index, Serial, Name, Number;
}

// 从真实游戏键位配置取得 Xbox 操作对应的键盘动作；工具不创建虚拟手柄或驱动。
sealed class GameActionBindings
{
    public int[] Keyboard = new int[4], Gamepad = new int[4];
    // 把游戏反射键名转换为 Windows 虚拟键；未知键返回 0。
    public static int KeyboardCode(string name)
    {
        string[] digits =
        {
            "Zero",
            "One",
            "Two",
            "Three",
            "Four",
            "Five",
            "Six",
            "Seven",
            "Eight",
            "Nine"
        };
        int digit = Array.IndexOf(digits, name);
        if (digit >= 0)
            return 0x30 + digit;
        if (name.Length == 1 && name[0] >= 'A' && name[0] <= 'Z')
            return name[0];
        if (name == "SpaceBar")
            return 32;
        switch (name)
        {
            case "BackSpace":
                return 8;
            case "Tab":
                return 9;
            case "Enter":
                return 13;
            case "PageUp":
                return 33;
            case "PageDown":
                return 34;
            case "End":
                return 35;
            case "Home":
                return 36;
            case "Left":
                return 37;
            case "Up":
                return 38;
            case "Right":
                return 39;
            case "Down":
                return 40;
            case "Insert":
                return 45;
            case "Delete":
                return 46;
            case "Multiply":
                return 106;
            case "Add":
                return 107;
            case "Subtract":
                return 109;
            case "Decimal":
                return 110;
            case "Divide":
                return 111;
            case "Semicolon":
                return 186;
            case "Equals":
                return 187;
            case "Comma":
                return 188;
            case "Hyphen":
                return 189;
            case "Period":
                return 190;
            case "Slash":
                return 191;
            case "Tilde":
                return 192;
            case "LeftBracket":
                return 219;
            case "Backslash":
                return 220;
            case "RightBracket":
                return 221;
            case "Apostrophe":
                return 222;
        }

        if (name.StartsWith("NumPad"))
        {
            int num = Array.IndexOf(digits, name.Substring(6));
            if (num >= 0)
                return 96 + num;
        }

        int number;
        if (name.StartsWith("F") && Int32.TryParse(name.Substring(1), out number) && number >= 1 && number <= 24)
        {
            int key = 0x6f + number;
            return ToolboxInput.Allowed(key) ? key : 0;
        }

        return 0;
    }

    // 把原生手柄键名转换为工具内部按钮/触发器标记。
    public static int GamepadCode(string name)
    {
        string[] names =
        {
            "Gamepad_FaceButton_Bottom",
            "Gamepad_FaceButton_Right",
            "Gamepad_FaceButton_Left",
            "Gamepad_FaceButton_Top",
            "Gamepad_LeftShoulder",
            "Gamepad_RightShoulder",
            "Gamepad_LeftTrigger",
            "Gamepad_LeftTriggerAxis",
            "Gamepad_RightTrigger",
            "Gamepad_RightTriggerAxis",
            "Gamepad_DPad_Up",
            "Gamepad_DPad_Down",
            "Gamepad_DPad_Left",
            "Gamepad_DPad_Right",
            "Gamepad_LeftThumbstick",
            "Gamepad_RightThumbstick",
            "Gamepad_Special_Left",
            "Gamepad_Special_Right"
        };
        int[] keys =
        {
            4096,
            8192,
            16384,
            32768,
            256,
            512,
            65536,
            65536,
            131072,
            131072,
            1,
            2,
            4,
            8,
            64,
            128,
            32,
            16
        };
        int i = Array.IndexOf(names, name);
        return i >= 0 ? keys[i] : 0;
    }

    // 把选中的槽位操作路由到游戏已读取的键位，拒绝缺失绑定及组合键冲突。
    public int[] Route(ToolboxSettings settings, int[] keys)
    {
        return RouteKeys(settings, keys, true);
    }

    // 自动恢复的键鼠路径也读取当前游戏键位，不要求已知手柄绑定。
    public int[] RouteKeyboard(ToolboxSettings settings, int[] keys)
    {
        return RouteKeys(settings, keys, false);
    }

    // 两种路由共用动作身份、缺失绑定和触发键冲突检查。
    int[] RouteKeys(ToolboxSettings settings, int[] keys, bool controller)
    {
        var result = new List<int>();
        foreach (int key in keys)
        {
            int slot = Array.IndexOf(settings.Slots, key);
            int action = slot >= 0 ? slot : key == settings.PotionKey ? 3 : -1;
            if (action < 0 || Keyboard[action] == 0 || controller && Gamepad[action] == 0)
                throw new Exception("游戏手柄键位未读取，请检查游戏内绑定。");
            int mapped = Keyboard[action];
            if ((settings.ComboEnabled && mapped == settings.ComboTrigger) || (settings.JumpEnabled && mapped == settings.JumpTrigger))
                throw new Exception("游戏键位与工具箱触发键冲突，请更换触发键。");
            if (!result.Contains(mapped))
                result.Add(mapped);
        }

        return result.ToArray();
    }
}

// ArtifactInfo 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
sealed class ArtifactInfo
{
    public string Name;
    public float Cost;
    public int Slot;
}

// 快捷槽冷却快照；Known=false 时不能把零值或缺少开始时间解释为就绪。
sealed class CooldownInfo
{
    public bool Known, Locked, Unavailable;
    public float Start, Duration, Charges = float.NaN;
    // 只有数据已知且原生冷却开始值有效才判为冷却中。
    public bool Cooling
    {
        get
        {
            return Known && Start > 0;
        }
    }

    // 数据已知、未锁定且充能/冷却满足条件才判为就绪。
    public bool Ready
    {
        get
        {
            return Known && !Locked && (!float.IsNaN(Charges) ? Charges >= 1 : Start == 0);
        }
    }
}

static class CooldownRefreshRule
{
    // 只要求已勾选法器与药水的必要冷却数据已知，避免未使用槽位拖慢刷新。
    public static bool RequiredKnown(CooldownInfo[] values, bool[] selected)
    {
        return values != null && values.Length == 4 && selected != null && selected.Length == 3 && values[3] != null && values[3].Known && Enumerable.Range(0, 3).All(i => !selected[i] || values[i] != null && values[i].Known);
    }

}

// 已校验反射字段布局：Offset/Size 为字节，Field 为原生字段元数据地址。
sealed class Property
{
    public int Offset, Size;
    public long Field;
}

// 跨文件的只读游戏读取器；各 partial 文件共同持有同一连接/对象身份缓存。
sealed partial class HealthReader : IDisposable
{
    Memory M;
    Process process;
    long pool, objects;
    Identity local;
    Func<long, int, bool> executable;
    Dictionary<int, string> names = new Dictionary<int, string>();
    Dictionary<long, string> classNames = new Dictionary<long, string>();
    Dictionary<string, Property> properties = new Dictionary<string, Property>();
    List<Identity> keyProfiles = new List<Identity>();
    List<Identity> artifactWidgets = new List<Identity>(), potionWidgets = new List<Identity>();
    Dictionary<string, long> structs = new Dictionary<string, long>();
    // 当前已连接游戏 PID，不代表窗口永远仍属于该进程。
    public int Pid
    {
        get
        {
            return process.Id;
        }
    }

    // 检查连接进程是否仍存活。
    public bool Alive
    {
        get
        {
            try
            {
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }
    }

    // 当前游戏窗口句柄；发送消息前仍要核对有效性与 PID。
    public IntPtr Window
    {
        get
        {
            process.Refresh();
            return process.HasExited ? IntPtr.Zero : process.MainWindowHandle;
        }
    }

    public HealthReader()
    {
        process = GameProcess.OpenSingle();
        try
        {
            M = new Memory(process.Id);
            GameLayout layout = GameLocator.Resolve(M, process);
            pool = layout.Names;
            objects = layout.Objects;
            executable = layout.Executable;
            if (Name(0) != "None")
                throw new Exception("游戏结构校验失败。");
            DiscoverLocal();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    string Name(int id)
    {
        string s;
        if (names.TryGetValue(id, out s))
            return s;
        uint n = unchecked((uint)id);
        if ((n >> 16) > 8191)
            throw new Exception("名称索引无效。");
        long a = M.Q(pool + 16 + (n >> 16) * 8) + (n & 65535) * 2;
        ushort h = BitConverter.ToUInt16(M.Read(a, 2), 0);
        int len = h >> 6;
        if (len > 1024)
            throw new Exception("名称长度无效。");
        s = ((h & 1) != 0 ? Encoding.Unicode : Encoding.UTF8).GetString(M.Read(a + 2, len * ((h & 1) != 0 ? 2 : 1)));
        names[id] = s;
        return s;
    }

    // 解析 UObject 类名；类地址仍需通过对象身份校验。
    string ClassName(long c)
    {
        string n;
        if (!classNames.TryGetValue(c, out n))
        {
            n = Name(M.I(c + 24));
            classNames[c] = n;
        }

        return n;
    }

    // 沿原生继承关系确认对象是否属于指定类型。
    bool IsA(long c, string expected)
    {
        for (int i = 0; c != 0 && i < 32; i++, c = M.Q(c + 64))
            if (ClassName(c) == expected)
                return true;
        return false;
    }

    // 查找并验证反射字段的类型、尺寸和偏移，按类型缓存结果。
    Property Prop(long c, string name, int size)
    {
        string key = c + ":" + name;
        string ownerClass = ClassName(c);
        Property p;
        if (properties.TryGetValue(key, out p))
            return p;
        AdaptationRecord.Set("property." + ownerClass + "." + name, "query pending / unavailable; expected size=" + size);
        for (int i = 0; c != 0 && i < 32; i++, c = M.Q(c + 64))
        {
            long f = M.Q(c + 80);
            for (int j = 0; f != 0 && j < 512; j++, f = M.Q(f + 24))
            {
                if (Name(M.I(f + 32)) != name)
                    continue;
                p = new Property
                {
                    Offset = M.I(f + 72),
                    Size = M.I(f + 52),
                    Field = f
                };
                if (p.Size != size || p.Offset < 0 || p.Offset > 0x20000)
                    throw new Exception("属性布局不匹配：" + name);
                AdaptationRecord.Set("property." + ownerClass + "." + name, "offset=0x" + p.Offset.ToString("X") + " size=" + p.Size + " declaredBy=" + ClassName(c));
                properties[key] = p;
                return p;
            }
        }

        throw new Exception("找不到游戏属性：" + name);
    }

    // 返回已校验反射属性的偏移，未知字段不能用猜测常量代替。
    int Offset(Identity o, string n, int size)
    {
        return Prop(o.Class, n, size).Offset;
    }

    // 读取 UObject 的地址/类/索引/序列等身份信息，用于抵抗对象地址复用。
    Identity Token(long a)
    {
        byte[] b = M.Read(a, 40);
        int ix = BitConverter.ToInt32(b, 12);
        int count = M.I(objects + 20);
        if (ix < 0 || ix >= count || count > 2000000 || (BitConverter.ToInt32(b, 8) & 0x18030) != 0)
            throw new Exception("角色对象不可用。");
        long slot = M.Q(M.Q(objects) + (ix / 65536) * 8) + (ix % 65536) * 24;
        if (M.Q(slot) != a)
            throw new Exception("角色对象已变化。");
        return new Identity
        {
            Address = a,
            Class = BitConverter.ToInt64(b, 16),
            Index = ix,
            Slot = slot,
            Serial = M.I(slot + 16),
            Name = BitConverter.ToInt32(b, 24),
            Number = BitConverter.ToInt32(b, 28)
        };
    }

    // 校验对象或数据是否满足当前模块的使用前提。
    public bool Valid(Identity t)
    {
        if (t == null)
            return false;
        try
        {
            return M.Q(t.Slot) == t.Address && M.I(t.Slot + 16) == t.Serial && M.I(t.Address + 12) == t.Index && M.Q(t.Address + 16) == t.Class && M.I(t.Address + 24) == t.Name && M.I(t.Address + 28) == t.Number && (M.I(t.Address + 8) & 0x18030) == 0;
        }
        catch
        {
            return false;
        }
    }

    // 唯一定位本地玩家及其控制对象，拒绝缺失或多个候选。
    void DiscoverLocal()
    {
        int count = M.I(objects + 20);
        if (count < 1000 || count > 2000000)
            throw new Exception("对象表验证失败。");
        long arr = M.Q(objects);
        List<Identity> found = new List<Identity>();
        for (int ci = 0; ci * 65536 < count; ci++)
        {
            byte[] chunk = M.Read(M.Q(arr + ci * 8), Math.Min(65536, count - ci * 65536) * 24);
            for (int j = 0; j < chunk.Length; j += 24)
            {
                long a = BitConverter.ToInt64(chunk, j);
                if (a == 0)
                    continue;
                try
                {
                    byte[] b = M.Read(a, 40);
                    if ((BitConverter.ToInt32(b, 8) & 0x18030) != 0)
                        continue;
                    long c = BitConverter.ToInt64(b, 16);
                    // 这是当前版本的原生本地玩家类；通过它解析控制器和 Pawn。
                    string cn = ClassName(c);
                    int ix = ci * 65536 + j / 24, serial = BitConverter.ToInt32(chunk, j + 16);
                    threatSlots[ix] = new ThreatSlot
                    {
                        Address = a,
                        Serial = serial
                    };
                    TrackThreat(a, c, ix, serial);
                    if (cn == "DungeonsLocalPlayer")
                        found.Add(Token(a));
                    if (cn == "SWEnhancedPlayerMappableKeyProfile" && !Name(BitConverter.ToInt32(b, 24)).StartsWith("Default__"))
                        keyProfiles.Add(Token(a));
                    if (cn == "ScriptStruct")
                    {
                        string sn = Name(BitConverter.ToInt32(b, 24));
                        structs[sn] = a;
                    }
                }
                catch
                {
                }
            }
        }

        if (found.Count != 1)
            throw new Exception("未找到唯一的本地玩家，请进入单人关卡后重新连接。");
        local = found[0];
    }

    List<long> SparseElements(long header, int stride, int limit)
    {
        long data = M.Q(header);
        int count = M.I(header + 8), capacity = M.I(header + 12), bits = M.I(header + 40), bitCapacity = M.I(header + 44), free = M.I(header + 52);
        if (count < 0 || count > limit || capacity < count || capacity > limit * 4 || bits != count || bitCapacity < bits || bitCapacity > limit * 4 || free < 0 || free > count)
            throw new Exception("游戏键位列表无效。");
        long flags = bits <= 128 ? header + 16 : M.Q(header + 32);
        var rows = new List<long>();
        for (int i = 0; i < count; i++)
            if ((unchecked((uint)M.I(flags + (i / 32) * 4)) & (1u << (i % 32))) != 0)
                rows.Add(data + i * stride);
        if (rows.Count != count - free || M.Q(header) != data || M.I(header + 8) != count)
            throw new Exception("游戏键位正在变化。");
        return rows;
    }

    // 读取实际游戏键位配置，支持键盘与手柄路由而不创建虚拟手柄。
    public GameActionBindings GameBindings()
    {
        if (!Valid(local))
            throw new Exception("游戏手柄键位未读取，请检查游戏内绑定。");
        var result = new GameActionBindings();
        bool keyboard = false, pad = false;
        long mapping = Struct("PlayerKeyMapping", 168), rowStruct = Struct("KeyMappingRow", 80);
        int mappingName = Prop(mapping, "MappingName", 8).Offset, current = Prop(mapping, "CurrentKey", 24).Offset, slot = Prop(mapping, "Slot", 1).Offset;
        int rowMappings = Prop(rowStruct, "Mappings", 80).Offset;
        string[] actions =
        {
            "Artifact1",
            "Artifact2",
            "Artifact3",
            "HealthPotion"
        };
        foreach (Identity profile in keyProfiles)
        {
            if (!Valid(profile))
                continue;
            Identity owner = Token(M.Q(profile.Address + 32));
            if (ClassName(owner.Class) != "SWEnhancedInputUserSettings" || (M.I(owner.Address + Offset(owner, "OwningLocalPlayer", 8)) != local.Index || M.I(owner.Address + Offset(owner, "OwningLocalPlayer", 8) + 4) != local.Serial))
                continue;
            long identifier = profile.Address + Offset(profile, "ProfileIdentifierString", 16);
            int length = M.I(identifier + 8);
            if (length < 1 || length > 256)
                throw new Exception("游戏键位标识无效。");
            string id = Encoding.Unicode.GetString(M.Read(M.Q(identifier), length * 2)).TrimEnd('\0');
            bool isPad = id == "SW.Input.Profile.InputType.Gamepad";
            if (!isPad && id != "SW.Input.Profile.InputType.Keyboard")
                continue;
            if (isPad ? pad : keyboard)
                throw new Exception("游戏键位配置不唯一。");
            if (isPad)
                pad = true;
            else
                keyboard = true;
            long table = profile.Address + Offset(profile, "PlayerMappedKeys", 80);
            foreach (long row in SparseElements(table, 96, 512))
            {
                string name = Name(M.I(row));
                int action = Array.IndexOf(actions, name);
                if (action < 0)
                    continue;
                int primary = 0, alternative = 0;
                foreach (long key in SparseElements(row + 8 + rowMappings, 176, 32))
                {
                    if (Name(M.I(key + mappingName)) != name)
                        throw new Exception("游戏键位归属无效。");
                    string keyName = Name(M.I(key + current));
                    int code = isPad ? GameActionBindings.GamepadCode(keyName) : GameActionBindings.KeyboardCode(keyName);
                    if (code == 0)
                        continue;
                    if (M.Read(key + slot, 1)[0] == 0)
                        primary = code;
                    else if (alternative == 0)
                        alternative = code;
                }

                (isPad ? result.Gamepad : result.Keyboard)[action] = primary != 0 ? primary : alternative;
            }

            if (!Valid(profile) || !Valid(owner) || (M.I(owner.Address + Offset(owner, "OwningLocalPlayer", 8)) != local.Index || M.I(owner.Address + Offset(owner, "OwningLocalPlayer", 8) + 4) != local.Serial))
                throw new Exception("游戏键位正在变化。");
        }

        if (!keyboard || !pad)
            throw new Exception("游戏手柄键位未读取，请检查游戏内绑定。");
        return result;
    }

    // 返回当前本地玩家 Pawn，并重新验证关卡/对象归属。
    Identity Pawn()
    {
        if (process.HasExited || !Valid(local))
            throw new Exception("游戏状态已变化，请重新连接。");
        Identity controller = Token(M.Q(local.Address + Offset(local, "PlayerController", 8)));
        Identity pawn = Token(M.Q(controller.Address + Offset(controller, "Pawn", 8)));
        if (!IsA(pawn.Class, "PlayerCharacter") || M.Q(pawn.Address + Offset(pawn, "Controller", 8)) != controller.Address)
            throw new Exception("等待玩家进入关卡。");
        return pawn;
    }

    // 读取生命和灵魂遥测快照，读取失败由上层暂停动作。
    public float[] Snapshot()
    {
        Identity pawn = Pawn();
        Identity asc = Token(M.Q(pawn.Address + Offset(pawn, "AbilitySystemComponent", 8)));
        if (M.Q(asc.Address + Offset(asc, "AvatarActor", 8)) != pawn.Address)
            throw new Exception("玩家属性归属校验失败。");
        long list = asc.Address + Offset(asc, "SpawnedAttributes", 16);
        int count = M.I(list + 8);
        long data = M.Q(list);
        if (count < 1 || count > 128)
            throw new Exception("玩家属性列表无效。");
        float h = float.NaN, max = float.NaN, souls = float.NaN, soulsMax = float.NaN;
        for (int i = 0; i < count; i++)
        {
            Identity a = Token(M.Q(data + i * 8));
            string cn = ClassName(a.Class);
            if (cn != "ATR_Health" && cn != "ATR_Soul")
                continue;
            long outer = M.Q(a.Address + 32);
            if (outer != pawn.Address && outer != asc.Address)
                continue;
            if (cn == "ATR_Health")
            {
                h = M.F(a.Address + Offset(a, "Health", 16) + 12);
                max = M.F(a.Address + Offset(a, "HealthMax", 16) + 12);
            }
            else
            {
                float sp = M.F(a.Address + Offset(a, "Souls", 16) + 12), sm = M.F(a.Address + Offset(a, "SoulsMax", 16) + 12);
                if (sm > 0 && sm < 10000000 && sp >= 0 && sp <= sm * 1.1f)
                {
                    souls = sp;
                    soulsMax = sm;
                }
            }
        }

        if (float.IsNaN(h) || float.IsNaN(max) || max <= 0 || max > 10000000 || h < 0 || h > max * 1.1f)
            throw new Exception("血量读数暂不可用。");
        return new float[]
        {
            h,
            max,
            souls,
            soulsMax
        };
    }

    // 读取已反射验证的结构字段，不对未知布局直接转换。
    long Struct(string name, int size)
    {
        long c;
        if (!structs.TryGetValue(name, out c) || ClassName(M.Q(c + 16)) != "ScriptStruct" || M.I(c + 88) != size)
            throw new Exception("法器布局验证失败。");
        return c;
    }

    // 读取本地玩家实际快捷槽，避免误读预览控件或其他玩家。
    bool Hotbar(Identity widget)
    {
        long outer = M.Q(widget.Address + 32);
        bool singlePlayer = false;
        for (int depth = 0; outer != 0 && depth < 16; depth++)
        {
            Identity parent = Token(outer);
            string name = ClassName(parent.Class);
            if (name == "W_SinglePlayerHotbar_C")
                singlePlayer = true;
            if (name == "W_HotbarContainer_Activatable_C")
                return singlePlayer && ReadBool(parent, "bIsActive");
            outer = M.Q(outer + 32);
        }

        return false;
    }

    // 读取药水充能值，未知/无效状态不能认定药水已就绪。
    float PotionCharges()
    {
        Identity pawn = Pawn(), asc = Token(M.Q(pawn.Address + Offset(pawn, "AbilitySystemComponent", 8)));
        if (M.Q(asc.Address + Offset(asc, "AvatarActor", 8)) != pawn.Address)
            throw new Exception("玩家属性归属校验失败。");
        long list = asc.Address + Offset(asc, "SpawnedAttributes", 16), data = M.Q(list);
        int count = M.I(list + 8);
        if (count < 1 || count > 128)
            throw new Exception("玩家属性列表无效。");
        for (int i = 0; i < count; i++)
        {
            Identity a = Token(M.Q(data + i * 8));
            if (ClassName(a.Class) != "ATR_Health")
                continue;
            long outer = M.Q(a.Address + 32);
            if (outer != pawn.Address && outer != asc.Address)
                continue;
            float charges = M.F(a.Address + Offset(a, "HealthPotionCharges", 16) + 12);
            if (charges < 0 || charges > 100)
                throw new Exception("药水次数无效。");
            return charges;
        }

        throw new Exception("药水次数未读取。");
    }

    // 读取指定快捷槽的冷却和可用性，药水槽与法器槽按原生结构区分。
    public CooldownInfo Cooldown(int slot)
    {
        try
        {
            if (slot < 0 || slot > 3)
                throw new Exception("槽位无效。");
            Pawn();
            RefreshThreatActors(0);
            Identity chosen = null;
            foreach (Identity widget in slot == 3 ? potionWidgets : artifactWidgets)
            {
                if (!Valid(widget))
                    continue;
                try
                {
                    if (!Hotbar(widget))
                        continue;
                    if (slot < 3 && Name(M.I(widget.Address + Offset(widget, "InventorySlotTag", 8))) != "SW.ItemSlot.Equipment.Artifact.Slot" + (slot + 1))
                        continue;
                    if (M.Q(widget.Address + Offset(widget, "StateImage", 8)) == 0)
                        continue;
                    if (chosen != null)
                        throw new InvalidOperationException("冷却槽位不唯一。");
                    chosen = widget;
                }
                catch (InvalidOperationException)
                {
                    throw;
                }
                catch
                {
                }
            }

            if (chosen == null)
                throw new Exception("冷却槽位未读取。");
            Identity image = Token(M.Q(chosen.Address + Offset(chosen, "StateImage", 8)));
            Identity material = Token(M.Q(image.Address + Offset(image, "Brush", 176) + Prop(Struct("SlateBrush", 176), "ResourceObject", 8).Offset));
            if (!IsA(image.Class, "Image") || ClassName(material.Class) != "MaterialInstanceDynamic" || M.Q(material.Address + 32) != image.Address)
                throw new Exception("冷却条归属无效。");
            long array = material.Address + Offset(material, "ScalarParameterValues", 16), data = M.Q(array);
            int count = M.I(array + 8), cap = M.I(array + 12);
            if (count < 0 || count > 64 || cap < count || cap > 256)
                throw new Exception("冷却参数无效。");
            long valueStruct = Struct("ScalarParameterValue", 36), paramStruct = Struct("MaterialParameterInfo", 16);
            int infoOff = Prop(valueStruct, "ParameterInfo", 16).Offset, nameOff = Prop(paramStruct, "Name", 8).Offset, valueOff = Prop(valueStruct, "ParameterValue", 4).Offset;
            var values = new Dictionary<string, float>();
            for (int i = 0; i < count; i++)
                values[Name(M.I(data + i * 36 + infoOff + nameOff))] = M.F(data + i * 36 + valueOff);
            if (!Valid(chosen) || !Valid(image) || !Valid(material) || M.Q(array) != data || M.I(array + 8) != count)
                throw new Exception("冷却条已变化。");
            return DecodeCooldown(values, slot == 3 ? PotionCharges() : float.NaN);
        }
        catch
        {
            return new CooldownInfo();
        }
    }

    // 把原生冷却字段转换为 Known/Locked/Cooling/Ready 等工具状态。
    public static CooldownInfo DecodeCooldown(IDictionary<string, float> values, float charges)
    {
        float start, duration, icon, locked, unavailable;
        bool hasStart = values.TryGetValue("StartTime_Cooldown", out start), hasDuration = values.TryGetValue("Duration_Cooldown", out duration);
        bool hasIcon = values.TryGetValue("State_Icon", out icon), hasLocked = values.TryGetValue("State_Locked", out locked), hasUnavailable = values.TryGetValue("State_Unavailable", out unavailable);
        // 冷却覆盖字段未创建时，使用图标状态或药水充能判断。
        bool idle = hasIcon && icon == 1 && hasLocked && hasUnavailable && locked >= 0 && locked <= 1 && unavailable >= 0 && unavailable <= 1;
        bool potion = !float.IsNaN(charges) && !float.IsInfinity(charges) && charges >= 0 && charges <= 100;
        if (hasStart != hasDuration || (!hasStart && !idle && !potion) || float.IsNaN(start) || float.IsInfinity(start) || start < 0 || float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0 || duration > 3600)
            throw new Exception("冷却数值无效。");
        return new CooldownInfo
        {
            Known = true,
            Start = start,
            Duration = duration,
            Locked = locked > 0,
            Unavailable = unavailable > 0,
            Charges = charges
        };
    }

    // 读取法器身份和灵魂消耗，用于预算及槽位显示。
    public ArtifactInfo Artifact(int slot)
    {
        if (slot < 0 || slot > 2)
            throw new Exception("自定义键无法确定槽位，请关闭自动读取并填写消耗。");
        RefreshThreatActors(0);
        Identity pawn = Pawn(), inv = Token(M.Q(pawn.Address + Offset(pawn, "InventoryManagerComponent", 8)));
        if (!IsA(inv.Class, "InventoryManagerComponent") || M.Q(inv.Address + 32) != pawn.Address)
            throw new Exception("法器归属验证失败。");
        long container = Struct("SlotEntryContainerReplicated", 280), entry = Struct("SlotEntry", 80), item = Struct("InventoryEntry", 232);
        long array = inv.Address + Offset(inv, "ReplicatedItems", 280) + Prop(container, "Items", 16).Offset;
        int count = M.I(array + 8);
        long data = M.Q(array);
        if (count < 1 || count > 128)
            throw new Exception("法器列表暂不可用。");
        string tag = "SW.ItemSlot.Equipment.Artifact.Slot" + (slot + 1);
        string itemName = null;
        int tagId = 0;
        long chosen = 0;
        for (int i = 0; i < count; i++)
        {
            long a = data + i * 80;
            int id = M.I(a + Prop(entry, "TypeTag", 8).Offset);
            if (Name(id) != tag)
                continue;
            long items = a + Prop(entry, "ItemInventoryEntries", 16).Offset;
            int n = M.I(items + 8);
            if (n != 1)
                throw new Exception("所选法器槽位为空或暂不可用。");
            long equipped = M.Q(items);
            if (Name(M.I(equipped + Prop(item, "EquippedSlot", 8).Offset)) != tag)
                throw new Exception("法器槽位验证失败。");
            itemName = Name(M.I(equipped + Prop(item, "ItemData", 192).Offset));
            tagId = id;
            chosen = equipped;
            break;
        }

        if (itemName == null)
            throw new Exception("没有找到所选法器。");
        float cost = float.NaN;
        int matched = 0;
        foreach (var widget in artifactWidgets)
        {
            if (!Valid(widget))
                continue;
            try
            {
                if (M.I(widget.Address + Offset(widget, "InventorySlotTag", 8)) != tagId)
                    continue;
                // 只有实际本地玩家快捷槽拥有文字子控件；预览控件的对应子项为空。
                if (!Hotbar(widget))
                    continue;
                Identity text = Token(M.Q(widget.Address + Offset(widget, "SoulCostTextBlock", 8)));
                if (!IsA(text.Class, "TextBlock"))
                    continue;
                long address = text.Address + Offset(text, "Text", 16), textData = M.Q(address);
                long str = M.Q(textData + 24);
                int n = M.I(textData + 32), capacity = M.I(textData + 36);
                if (n < 2 || n > 12 || capacity < n || capacity > 128)
                    continue;
                string s = Encoding.Unicode.GetString(M.Read(str, n * 2)).TrimEnd('\0');
                float value;
                if (!float.TryParse(s, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value) || value < 0 || value > 1000000)
                    continue;
                if (M.Q(address) != textData || M.Q(textData + 24) != str || M.I(textData + 32) != n)
                    continue;
                if (!Valid(widget) || !Valid(text))
                    continue;
                cost = value;
                matched++;
            }
            catch
            {
            }
        }

        if (matched != 1 || float.IsNaN(cost) || !Valid(pawn) || !Valid(inv) || Name(M.I(chosen)) != itemName || Name(M.I(chosen + Prop(item, "EquippedSlot", 8).Offset)) != tag)
            throw new Exception("法器消耗暂不可读，请重新连接或关闭自动读取并填写消耗。");
        return new ArtifactInfo
        {
            Name = FriendlyArtifact(itemName),
            Cost = cost,
            Slot = slot
        };
    }

    // 将已知法器类型转换为可读名称；不影响原生类型标识。
    static string FriendlyArtifact(string name)
    {
        if (name == "SW.Item.Artifact.TotemOfRegeneration")
            return "回复图腾";
        if (name == "SW.Item.Artifact.SoulHealer")
            return "灵魂治疗器";
        if (name == "SW.Item.Artifact.SoulHarvester")
            return "灵魂收割器";
        if (name == "SW.Item.Artifact.CorruptedSeeds")
            return "腐化种子";
        return name.Replace("SW.Item.Artifact.", "");
    }

    // 释放本对象拥有的句柄、绘图对象或监听资源，避免退出后继续占用。
    public void Dispose()
    {
        if (M != null)
        {
            M.Dispose();
            M = null;
        }

        if (process != null)
            process.Dispose();
    }
}

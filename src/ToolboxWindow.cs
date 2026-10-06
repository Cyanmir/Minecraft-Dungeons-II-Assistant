// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 主窗口、设置迁移、自动恢复规则、输入协调、连接与命令行入口。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

// 可持久化的默认设置；改默认值还要检查 Parse 限幅、Save 键名和对应 UI 控件。
sealed class ToolboxSettings
{
    // 默认低血量阈值 45%；重试为秒；槽位从 0 开始；键码为 Windows 虚拟键。改值还需核对配置 Parse 与 UI。
    public int Threshold = 45, RetrySeconds = 5, HealSlot = 2, HealKey = 0x33;
    // 三个法器槽的默认键为 1/2/3；数组索引与游戏槽位、自动/组合选择一致。
    public int[] Slots = new int[]
    {
        0x31,
        0x32,
        0x33
    };
    public bool HealEnabled = true, ComboEnabled = true, JumpEnabled = true, DodgeEnabled = true;
    public bool ThreatEnabled;
    // 原生战斗/主动遇敌/法器/闪避及各收集类型独立开关；直接组件默认不开启，不能用一个开关替代目标类型选择。
    public bool CombatNative, CombatAttack, CombatEncounter, CombatArtifacts, CombatEvade, NearbyChests, NearbyItems, NearbyPots, NearbyFood, NearbyDirect;
    // 工具触发范围默认 220 游戏单位；不是对游戏原生攻击范围的修改。
    public int CombatRange = 220;
    // 允许玩家手动跑动时原生交互；没有自动寻路或自动移动含义。
    public bool NearbyAllowMoving = true;
    // 通用交互默认 500 毫秒，食物默认 1000 毫秒；配置和滑块共同限幅到 100–30000 毫秒。
    public int NearbyIntervalMs = 500, NearbyFoodIntervalMs = 1000;
    // 手动组合的槽位选择，独立于自动恢复的 AutoSlots。
    public bool[] ComboSlots = new bool[]
    {
        true,
        true,
        true
    };
    // 自动恢复/战斗提前法器使用的槽选择，默认只选第三槽；不能和 ComboSlots 共用数组。
    public bool[] AutoSlots = new bool[]
    {
        false,
        false,
        true
    };
    public int AttackTrigger = 1;
    // 组合默认 G，跳劈触发空格、补按 Q、延迟 30 毫秒；右键闪避使用 R。
    public int ComboTrigger = 0x47, JumpTrigger = 0x20, JumpKey = 0x51, JumpDelay = 30, DodgeKey = 0x52;
    public bool GamepadEnabled = true;
    public int PadStart, PadStop, PadCombo;
    // 药水备用、原生灵魂成本读取和统一后台控制；辅助快捷操作有独立输入冲突检查。
    public bool PotionFallback = true, AutoCost = true, BackgroundAuto = true;
    // 药水默认 E；手动灵魂成本默认 65；成本参考槽从 0 开始，默认第三槽。
    public int PotionKey = 0x45, SoulCost = 65, PotionSlot = 2;
    // 语言索引：0 简中、1 英语、2 日语、3 韩语、4 香港繁中、5 台湾繁中。
    public int Language = SystemLanguage(System.Globalization.CultureInfo.CurrentUICulture.Name);
    // 将系统语言映射到稳定 0–5 索引，默认英语。
    public static int SystemLanguage(string name)
    {
        name = (name ?? "").ToLowerInvariant();
        if (name.StartsWith("ja"))
            return 2;
        if (name.StartsWith("ko"))
            return 3;
        if (name.StartsWith("zh"))
        {
            if (name.Contains("hk") || name.Contains("mo"))
                return 4;
            if (name.Contains("tw") || name.Contains("hant"))
                return 5;
            return 0;
        }

        return 1;
    }

    // 用户设置文件路径；保留旧目录名以兼容已安装版本，不能写死开发者路径。
    public static string ConfigPath
    {
        get
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MinecraftDungeons2Toolbox", "settings.txt");
        }
    }

    public static ToolboxSettings Load()
    {
        try
        {
            return Parse(File.ReadAllLines(ConfigPath));
        }
        catch
        {
            return new ToolboxSettings();
        }
    }

    // 读取 key=value 配置，忽略退役键，并限幅数值和迁移旧法器选择。
    public static ToolboxSettings Parse(IEnumerable<string> lines)
    {
        var s = new ToolboxSettings();
        bool group = false;
        try
        {
            foreach (string line in lines)
            {
                string[] pair = line.Split('=');
                int value;
                if (pair.Length != 2 || !int.TryParse(pair[1], out value))
                    continue;
                switch (pair[0])
                {
                    case "nearbyChests":
                        s.NearbyChests = value != 0;
                        break;
                    case "nearbyItems":
                        s.NearbyItems = value != 0;
                        break;
                    case "nearbyFood":
                        s.NearbyFood = value != 0;
                        break;
                    case "nearbyDirect":
                        s.NearbyDirect = value != 0;
                        break;
                    case "nearbyAllowMoving":
                        s.NearbyAllowMoving = value != 0;
                        break;
                    case "nearbyIntervalMs":
                        s.NearbyIntervalMs = NearbyLootTiming.Clamp(value);
                        break;
                    case "nearbyFoodIntervalMs":
                        s.NearbyFoodIntervalMs = NearbyLootTiming.Clamp(value);
                        break;
                    case "nearbyPots":
                        s.NearbyPots = value != 0;
                        break;
                    case "combatEvade":
                        s.CombatEvade = value != 0;
                        break;
                    case "combatNative":
                        s.CombatNative = value != 0;
                        break;
                    case "combatAttack":
                        s.CombatAttack = value != 0;
                        break;
                    case "combatEncounter":
                        s.CombatEncounter = value != 0;
                        break;
                    case "combatArtifacts":
                        s.CombatArtifacts = value != 0;
                        break;
                    case "combatRange":
                        s.CombatRange = Math.Max(80, Math.Min(500, value));
                        break;
                    case "threatEnabled":
                        s.ThreatEnabled = value != 0;
                        break;
                    case "gamepadEnabled":
                        s.GamepadEnabled = value != 0;
                        break;
                    case "padStart":
                        if (XboxPad.ValidBinding(value))
                            s.PadStart = value;
                        break;
                    case "padStop":
                        if (XboxPad.ValidBinding(value))
                            s.PadStop = value;
                        break;
                    case "padCombo":
                        if (XboxPad.ValidBinding(value))
                            s.PadCombo = value;
                        break;
                    case "padJump":
                        break; // 退役配置不再恢复；跳劈只使用键盘/鼠标。
                    case "padAttack":
                        break;
                    case "auto1":
                    case "auto2":
                    case "auto3":
                        s.AutoSlots[pair[0][4] - '1'] = value != 0;
                        group = true;
                        break;
                    case "autoExtra":
                        break; // 退役的额外按键设置，兼容读取时忽略。
                    case "language":
                        s.Language = Math.Max(0, Math.Min(5, value));
                        break;
                    case "potionFallback":
                        s.PotionFallback = value != 0;
                        break;
                    case "backgroundAuto":
                        s.BackgroundAuto = value != 0;
                        break;
                    case "autoCost":
                        s.AutoCost = value != 0;
                        break;
                    case "potionSlot":
                        s.PotionSlot = Math.Max(0, Math.Min(2, value));
                        break;
                    case "potionKey":
                        if (ToolboxInput.Allowed(value))
                            s.PotionKey = value;
                        break;
                    case "soulCost":
                        s.SoulCost = Math.Max(0, Math.Min(100000, value));
                        break;
                    case "threshold":
                        s.Threshold = Math.Max(1, Math.Min(99, value));
                        break;
                    case "retry":
                        s.RetrySeconds = Math.Max(1, Math.Min(120, value));
                        break;
                    case "healSlot":
                        s.HealSlot = Math.Max(0, Math.Min(5, value));
                        break;
                    case "healKey":
                        if (ToolboxInput.Allowed(value))
                            s.HealKey = value;
                        break;
                    case "slot1":
                    case "slot2":
                    case "slot3":
                        if (ToolboxInput.Allowed(value))
                            s.Slots[pair[0][4] - '1'] = value;
                        break;
                    case "healEnabled":
                        s.HealEnabled = value != 0;
                        break;
                    case "comboEnabled":
                        s.ComboEnabled = value != 0;
                        break;
                    case "jumpEnabled":
                        s.JumpEnabled = value != 0;
                        break;
                    case "dodgeEnabled":
                        s.DodgeEnabled = value != 0;
                        break;
                    case "dodgeKey":
                        break; // 退役的自定义闪避设置；当前闪避固定使用 R。
                    case "extraEnabled":
                        break;
                    case "combo1":
                    case "combo2":
                    case "combo3":
                        s.ComboSlots[pair[0][5] - '1'] = value != 0;
                        break;
                    case "comboTrigger":
                        if (ToolboxInput.Allowed(value))
                            s.ComboTrigger = value;
                        break;
                    case "extraKey":
                        break;
                    case "jumpTrigger":
                        if (ToolboxInput.Allowed(value))
                            s.JumpTrigger = value;
                        break;
                    case "attackTrigger":
                        if (new[]
                        {
                            1,
                            2,
                            4,
                            5,
                            6
                        }.Contains(value) || ToolboxInput.Allowed(value))
                            s.AttackTrigger = value;
                        break;
                    case "jumpKey":
                        if (ToolboxInput.Allowed(value))
                            s.JumpKey = value;
                        break;
                    case "jumpDelay":
                        s.JumpDelay = Math.Max(0, Math.Min(500, value));
                        break;
                }
            }
        }
        catch
        {
        }

        if (!group)
        {
            s.AutoSlots = new bool[3];
            if (s.HealSlot < 3)
                s.AutoSlots[s.HealSlot] = true;
            else
            {
                int slot = Array.IndexOf(s.Slots, s.HealKey);
                if (slot >= 0)
                    s.AutoSlots[slot] = true;
            }
        }

        return s;
    }

    // 持久化本模块设置；先写临时文件，再替换原文件，避免留下半份配置。
    public void Save()
    {
        var rows = new List<string>
        {
            "gamepadEnabled=" + (GamepadEnabled ? 1 : 0),
            "padStart=" + PadStart,
            "padStop=" + PadStop,
            "padCombo=" + PadCombo,
            "backgroundAuto=" + (BackgroundAuto ? 1 : 0),
            "language=" + Language,
            "potionFallback=" + (PotionFallback ? 1 : 0),
            "autoCost=" + (AutoCost ? 1 : 0),
            "potionKey=" + PotionKey,
            "potionSlot=" + PotionSlot,
            "soulCost=" + SoulCost,
            "threshold=" + Threshold,
            "retry=" + RetrySeconds,
            "healSlot=" + HealSlot,
            "healKey=" + HealKey,
            "healEnabled=" + (HealEnabled ? 1 : 0),
            "comboEnabled=" + (ComboEnabled ? 1 : 0),
            "jumpEnabled=" + (JumpEnabled ? 1 : 0),
            "attackTrigger=" + AttackTrigger,
            "comboTrigger=" + ComboTrigger,
            "jumpTrigger=" + JumpTrigger,
            "jumpKey=" + JumpKey,
            "jumpDelay=" + JumpDelay,
            "dodgeEnabled=" + (DodgeEnabled ? 1 : 0),
            "dodgeKey=" + DodgeKey
        };
        for (int i = 0; i < 3; i++)
        {
            rows.Add("slot" + (i + 1) + "=" + Slots[i]);
            rows.Add("combo" + (i + 1) + "=" + (ComboSlots[i] ? 1 : 0));
            rows.Add("auto" + (i + 1) + "=" + (AutoSlots[i] ? 1 : 0));
        }

        rows.Add("nearbyChests=" + (NearbyChests ? 1 : 0));
        rows.Add("nearbyItems=" + (NearbyItems ? 1 : 0));
        rows.Add("nearbyFood=" + (NearbyFood ? 1 : 0));
        rows.Add("nearbyDirect=" + (NearbyDirect ? 1 : 0));
        rows.Add("nearbyPots=" + (NearbyPots ? 1 : 0));
        rows.Add("threatEnabled=" + (ThreatEnabled ? 1 : 0));
        rows.Add("combatEvade=" + (CombatEvade ? 1 : 0));
        rows.Add("combatNative=" + (CombatNative ? 1 : 0));
        rows.Add("combatAttack=" + (CombatAttack ? 1 : 0));
        rows.Add("combatEncounter=" + (CombatEncounter ? 1 : 0));
        rows.Add("combatArtifacts=" + (CombatArtifacts ? 1 : 0));
        rows.Add("combatRange=" + CombatRange);
        rows.Add("nearbyAllowMoving=" + (NearbyAllowMoving ? 1 : 0));
        rows.Add("nearbyIntervalMs=" + NearbyIntervalMs);
        rows.Add("nearbyFoodIntervalMs=" + NearbyFoodIntervalMs);
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
        string temp = ConfigPath + ".tmp";
        File.WriteAllLines(temp, rows, Encoding.UTF8);
        if (File.Exists(ConfigPath))
            File.Replace(temp, ConfigPath, null);
        else
            File.Move(temp, ConfigPath);
    }

    // 取得自动恢复选中的去重槽位键，与组合键选择独立。
    public int[] AutoKeys()
    {
        var result = new List<int>();
        for (int i = 0; i < 3; i++)
            if (AutoSlots[i])
                result.Add(Slots[i]);
        return result.Distinct().ToArray();
    }

    // 读取参考槽原生灵魂消耗，关闭自动成本时采用用户值。
    public float AutomaticCost(Func<int, float> readCost)
    {
        if (!AutoCost)
            return SoulCost;
        float cost = readCost(PotionSlot);
        if (float.IsNaN(cost) || float.IsInfinity(cost) || cost < 0 || cost > 1000000)
            throw new Exception(L10n.T("法器列表暂不可用。"));
        return cost;
    }

    // 没有选中的自动法器槽时进入仅药水模式。
    public bool PotionOnly
    {
        get
        {
            return AutoKeys().Length == 0;
        }
    }

    // 仅药水模式或启用药水备用时才参与药水逻辑。
    public bool UsesPotion
    {
        get
        {
            return PotionOnly || PotionFallback;
        }
    }

    // 取得手动组合选中的槽位键，不改变自动恢复选择。
    public int[] ComboKeys()
    {
        var result = new List<int>();
        for (int i = 0; i < 3; i++)
            if (ComboSlots[i])
                result.Add(Slots[i]);
        return result.Distinct().ToArray();
    }

    // 检查启用功能与键位冲突，返回可显示错误，不发送游戏输入。
    public string Validate()
    {
        if (GamepadEnabled)
        {
            var bindings = new[]
            {
                PadStart,
                PadStop,
                PadCombo
            }.Where(v => v != 0).ToArray();
            if (bindings.Distinct().Count() != bindings.Length)
                return L10n.T("手柄操作请使用不同的按钮组合。");
            foreach (int control in bindings)
                foreach (int action in bindings.Where(v => v != control))
                    if ((action & control) == control || (action & control) == action)
                        return L10n.T("手柄操作请使用不同的按钮组合。");
        }

        if (!HealEnabled && !ComboEnabled && !JumpEnabled && !DodgeEnabled && !CombatAttack && !CombatEncounter && !CombatArtifacts && !CombatEvade && !NearbyChests && !NearbyItems && !NearbyPots && !NearbyFood)
            return L10n.T("请至少启用一项功能。");
        if (Slots.Distinct().Count() != 3)
            return L10n.T("三个法器槽位请使用不同的键。");
        if ((HealEnabled && PotionFallback || CombatArtifacts) && AutoKeys().Contains(PotionKey))
            return L10n.T("药水键不能与自动使用的法器键相同。");
        if (ComboEnabled && ComboKeys().Length == 0)
            return L10n.T("请勾选需要同时使用的法器槽位。");
        if (ComboEnabled && ComboKeys().Contains(ComboTrigger))
            return L10n.T("法器连发的触发键不能与选中的法器键相同。");
        if (JumpEnabled && (JumpTrigger == JumpKey || JumpTrigger == AttackTrigger))
            return L10n.T("跳劈的检测键与补按键请使用不同的键。");
        if (ComboEnabled && JumpEnabled && ComboTrigger == JumpTrigger)
            return L10n.T("法器连发与跳劈请使用不同的触发键。");
        var outputs = new List<int>();
        if (HealEnabled || CombatArtifacts)
        {
            outputs.AddRange(AutoKeys());
            if (HealEnabled && UsesPotion)
                outputs.Add(PotionKey);
        }

        if (ComboEnabled)
            outputs.AddRange(ComboKeys());
        if (JumpEnabled)
            outputs.Add(JumpKey);
        if (DodgeEnabled)
            outputs.Add(DodgeKey);
        if (ComboEnabled && outputs.Contains(ComboTrigger))
            return L10n.T("法器连发的触发键不能与自动发送的按键相同。");
        if (JumpEnabled && outputs.Contains(JumpTrigger))
            return L10n.T("跳劈检测键不能与自动发送的按键相同。");
        return null;
    }
}

sealed class HealRule
{
    public long Last = -1000000;
    // 按当前血量/资源/冷却或读失败门限判断是否已满足本类型前提。
    public bool Ready(float hp, float max, int threshold, long now, int seconds)
    {
        return max > 0 && hp > 0 && !float.IsNaN(hp) && !float.IsNaN(max) && !float.IsInfinity(hp) && !float.IsInfinity(max) && hp < max * threshold / 100f && now - Last >= seconds * 1000L;
    }

    // 决定是否启用药水备用，继续遵守药水冷却和血量条件。
    public static bool UsePotion(bool enabled, float souls, float cost)
    {
        return enabled && !float.IsNaN(souls) && !float.IsInfinity(souls) && souls >= 0 && !float.IsNaN(cost) && !float.IsInfinity(cost) && cost > 0 && souls < cost;
    }
}

static class RecoveryRule
{
    // 把恢复选择转换为可读动作说明。
    public static string Description(ToolboxSettings settings, bool potion)
    {
        return L10n.T(potion ? (settings.PotionOnly ? "低血量 → 药水回血" : "法器不可用 → 药水回血") : "低血量法器组合");
    }

    // 检查恢复所需的冷却数据已知，未勾选槽位不参与阻断。
    static bool Known(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
    }

    // 优先按独立自动槽选择可用法器，再按条件采用药水备用/仅药水。
    public static int[] Select(ToolboxSettings settings, CooldownInfo[] cooldowns, float souls, float[] costs, out bool potion)
    {
        potion = false;
        // 自动法器全不选表示仅药水恢复，无需灵魂或法器数据。
        if (settings.PotionOnly)
        {
            if (!cooldowns[3].Ready)
                return new int[0];
            potion = true;
            return new[]
            {
                settings.PotionKey
            };
        }

        if (!Known(souls))
            return new int[0];
        int reference = settings.PotionSlot;
        bool referenceUnavailable = cooldowns[reference].Known && (!cooldowns[reference].Ready || (Known(costs[reference]) && souls < costs[reference]));
        // 先预留选定参考法器的成本，再接纳其他单独就绪的槽。
        var keys = new List<int>();
        float available = souls;
        foreach (int slot in Enumerable.Range(0, 3).OrderBy(i => i == reference ? 0 : 1))
        {
            if (!settings.AutoSlots[slot] || !cooldowns[slot].Ready || !Known(costs[slot]) || costs[slot] > available)
                continue;
            keys.Add(settings.Slots[slot]);
            available -= costs[slot];
        }

        if (settings.PotionFallback && referenceUnavailable && cooldowns[3].Ready)
        {
            potion = true;
            keys.Add(settings.PotionKey);
        }

        return keys.Distinct().ToArray();
    }
}

sealed class JumpGesture
{
    bool keyWas, mouseWas;
    long keyAt = -1000000, mouseAt = -1000000, last = -1000000;
    public bool Observe(bool key, bool mouse, bool active, long now)
    {
        if (!active)
        {
            keyWas = key;
            mouseWas = mouse;
            keyAt = mouseAt = last = -1000000;
            return false;
        }

        if (key && !keyWas)
            keyAt = now;
        if (mouse && !mouseWas)
            mouseAt = now;
        keyWas = key;
        mouseWas = mouse;
        if (keyAt > last && mouseAt > last && now - keyAt <= 350 && now - mouseAt <= 350)
        {
            last = now;
            return true;
        }

        return false;
    }
}

static partial class ToolboxInput
{
    // 与原生 ABI 对应的数据结构；字段顺序、类型及 StructLayout 决定字节布局，不能仅为美观调整。
    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT
    {
        public ushort vk, scan;
        public uint flags, time;
        public UIntPtr extra;
    }

    // 与原生 ABI 对应的数据结构；字段顺序、类型及 StructLayout 决定字节布局，不能仅为美观调整。
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    struct UNION
    {
        [FieldOffset(0)]
        public KEYBDINPUT keyboard;
    }

    // 与原生 ABI 对应的数据结构；字段顺序、类型及 StructLayout 决定字节布局，不能仅为美观调整。
    [StructLayout(LayoutKind.Sequential)]
    struct INPUT
    {
        public uint type;
        public UNION value;
    }

    // Windows 原生输入入口；调用者必须校验前台/输入归属并负责释放。
    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint count, INPUT[] inputs, int size);
    // 转换 Windows 虚拟键和扫描码，保持普通键与扩展键的区分。
    [DllImport("user32.dll")]
    static extern uint MapVirtualKey(uint code, uint mapType);
    // 取得当前前台窗口，以判断是否允许前台输入。
    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();
    // 读取窗口所属 PID，避免把输入发到其他程序。
    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    // 读取物理按键状态，检测人工操作和按键冲突。
    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int key);
    // 读取屏幕鼠标位置，供输入条件及“不移动鼠标”的结果核对使用。
    [DllImport("user32.dll")]
    static extern bool GetCursorPos(out Point point);
    static readonly bool[] previousKeys = new bool[255];
    static Point previousCursor;
    static bool cursorKnown;
    // 汇总实际键盘操作，供键鼠/手柄模式判断。
    public static bool KeyboardActivity()
    {
        bool activity = false;
        for (int i = 1; i < 255; i++)
        {
            if (i >= 0xc3 && i <= 0xda)
                continue;
            bool held = Held(i);
            if (held && !previousKeys[i])
                activity = true;
            previousKeys[i] = held;
        }

        Point cursor;
        if (GetCursorPos(out cursor))
        {
            if (cursorKnown && cursor != previousCursor)
                activity = true;
            previousCursor = cursor;
            cursorKnown = true;
        }

        return activity;
    }

    // 向窗口投递普通键消息；发送前核对窗口存活和目标 PID。
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool PostMessage(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
    // 检查窗口句柄是否仍有效。
    [DllImport("user32.dll")]
    static extern bool IsWindow(IntPtr window);
    // 向 Windows 注册快捷键；组合冲突应反馈用户，不静默覆盖。
    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(IntPtr w, int id, uint mods, uint key);
    // 移除本窗口注册的快捷键，与注册动作配对。
    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(IntPtr w, int id);
    // 核对前台窗口是否属于指定游戏 PID。
    public static bool Front(int id)
    {
        uint pid;
        GetWindowThreadProcessId(GetForegroundWindow(), out pid);
        return pid == (uint)id;
    }

    public static IntPtr Foreground
    {
        get
        {
            return GetForegroundWindow();
        }
    }

    // 读取物理按键是否仍按下，避免与人工攻击/技能竞争。
    public static bool Held(int k)
    {
        return (GetAsyncKeyState(k) & 0x8000) != 0;
    }

    // 读取 Ctrl/Alt/Shift/Windows 等修饰键状态。
    public static bool Modifiers()
    {
        return Held(16) || Held(17) || Held(18) || Held(91) || Held(92);
    }

    // 检查额外修饰键，避免误触组合热键。
    public static bool OtherModifiers()
    {
        return Held(17) || Held(18) || Held(91) || Held(92);
    }

    // 检测人工输入占用，决定是否允许当前键消息。
    public static bool Busy(int[] keys)
    {
        return Modifiers() || keys.Any(Held);
    }

    // 检查键码是否在可配置白名单中，排除保留/危险输入。
    public static bool Allowed(int key)
    {
        return key >= 8 && key <= 254 && key != 0x77 && key != 0x78 && key != 0x1b && key != 16 && key != 17 && key != 18 && key != 91 && key != 92 && key != 93 && key != 0x5f && key != 0xe7 && key != 0xe5 && key != 0x14 && key != 0x90 && key != 0x91 && !(key >= 0xa0 && key <= 0xa5);
    }

    // 把键码或按钮组合转为可读名称，未知值按现有回退显示。
    public static string Name(int key)
    {
        if (key == 1)
            return L10n.T("鼠标左键");
        if (key == 2)
            return L10n.T("鼠标右键");
        if (key == 0x20)
            return L10n.T("空格");
        if (key >= 0x30 && key <= 0x39)
            return ((char)key).ToString();
        return ((Keys)key).ToString();
    }

    // 计算 Windows 键输入结构实际字节大小，避免 x64 ABI 失配。
    public static int InputSize()
    {
        return Marshal.SizeOf(typeof(INPUT));
    }

    // 校验窗口有效且所属 PID 与目标一致。
    public static bool OwnWindow(IntPtr window, int id)
    {
        uint pid;
        return window != IntPtr.Zero && IsWindow(window) && GetWindowThreadProcessId(window, out pid) != 0 && pid == (uint)id;
    }

    // 向经过 PID 验证的游戏窗口发送普通键消息，按下/松开必须配对。
    public static void SendWindow(IntPtr window, int id, int[] keys, bool up)
    {
        if (!OwnWindow(window, id))
            throw new Exception(L10n.T("游戏窗口已变化，等待重新连接。"));
        var sent = new List<int>();
        try
        {
            foreach (int key in keys)
            {
                uint scan = MapVirtualKey((uint)key, 4);
                if (scan == 0)
                    throw new Exception(L10n.T("这个按键无法发送，请换一个键。"));
                uint bits = 1u | ((scan & 255) << 16) | ((scan & 0xff00) != 0 ? 1u << 24 : 0) | (up ? 3u << 30 : 0);
                if (!PostMessage(window, up ? 0x101u : 0x100u, (IntPtr)key, (IntPtr)(long)bits))
                    throw new Exception(L10n.T("后台按键发送失败，请确认运行权限一致。"));
                sent.Add(key);
            }
        }
        catch
        {
            if (!up && sent.Count > 0)
                try
                {
                    SendWindow(window, id, sent.ToArray(), true);
                }
                catch
                {
                }

            throw;
        }
    }

    // 发送本工具允许的键输入，保持已持有输入的释放归属。
    public static void Send(int[] keys, bool up)
    {
        INPUT[] data = new INPUT[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            uint scan = MapVirtualKey((uint)keys[i], 4);
            uint extended = ((scan & 0xff00) != 0) ? 1u : 0u;
            if (scan == 0)
                throw new Exception(L10n.T("这个按键无法发送，请换一个键。"));
            data[i] = new INPUT
            {
                type = 1,
                value = new UNION
                {
                    keyboard = new KEYBDINPUT
                    {
                        scan = (ushort)(scan & 255),
                        flags = 8u | extended | (up ? 2u : 0u)
                    }
                }
            };
        }

        if (SendInput((uint)data.Length, data, InputSize()) != (uint)data.Length)
            throw new Exception(L10n.T("按键发送失败，已暂停。请确认工具箱与游戏的运行权限一致。"));
    }
}

// PadSample 的数据/状态结构；字段由本文件解析或计算，下游应保留未知值和身份有效性检查。
struct PadSample
{
    public int Index, Buttons;
    public short LX, LY, RX, RY;
}

static class XboxPad
{
    public static bool ActiveMode;
    public const int LT = 0x10000, RT = 0x20000, Mask = 0x3f3ff;
    // 与原生 ABI 对应的数据结构；字段顺序、类型及 StructLayout 决定字节布局，不能仅为美观调整。
    [StructLayout(LayoutKind.Sequential)]
    struct Gamepad
    {
        public ushort Buttons;
        public byte LeftTrigger, RightTrigger;
        public short LX, LY, RX, RY;
    }

    // 与原生 ABI 对应的数据结构；字段顺序、类型及 StructLayout 决定字节布局，不能仅为美观调整。
    [StructLayout(LayoutKind.Sequential)]
    struct State
    {
        public uint Packet;
        public Gamepad Gamepad;
    }

    // XInput 1.4 入口，读取设备按钮/摇杆/触发器状态。
    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    static extern uint Read14(uint index, out State state);
    // XInput 9.1.0 兼容入口，按实现回退而非创建虚拟手柄。
    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
    static extern uint Read91(uint index, out State state);
    static bool legacy;
    public static PadSample Read()
    {
        for (uint i = 0; i < 4; i++)
        {
            State state;
            uint result;
            try
            {
                result = legacy ? Read91(i, out state) : Read14(i, out state);
            }
            catch (DllNotFoundException)
            {
                legacy = true;
                try
                {
                    result = Read91(i, out state);
                }
                catch
                {
                    return new PadSample
                    {
                        Index = -1
                    };
                }
            }
            catch
            {
                return new PadSample
                {
                    Index = -1
                };
            }

            if (result == 0)
                return new PadSample
                {
                    Index = (int)i,
                    Buttons = Decode(state.Gamepad.Buttons, state.Gamepad.LeftTrigger, state.Gamepad.RightTrigger),
                    LX = state.Gamepad.LX,
                    LY = state.Gamepad.LY,
                    RX = state.Gamepad.RX,
                    RY = state.Gamepad.RY
                };
        }

        return new PadSample
        {
            Index = -1
        };
    }

    // 把 XInput 原生状态转换成工具按钮/摇杆标记。
    public static int Decode(int buttons, int left, int right)
    {
        return (buttons & 0xf3ff) | (left > 30 ? LT : 0) | (right > 30 ? RT : 0);
    }

    // 验证手柄快捷组合仅含受支持按钮/触发器位。
    public static bool ValidBinding(int buttons)
    {
        return buttons >= 0 && (buttons & ~Mask) == 0;
    }

    // 把键码或按钮组合转为可读名称，未知值按现有回退显示。
    public static string Name(int buttons)
    {
        if (buttons == 0)
            return L10n.T("未绑定");
        var names = new List<string>();
        int[] masks =
        {
            1,
            2,
            4,
            8,
            16,
            32,
            64,
            128,
            256,
            512,
            4096,
            8192,
            16384,
            32768,
            LT,
            RT
        };
        string[] labels =
        {
            "↑",
            "↓",
            "←",
            "→",
            "Menu",
            "View",
            "LS",
            "RS",
            "LB",
            "RB",
            "A",
            "B",
            "X",
            "Y",
            "LT",
            "RT"
        };
        for (int i = 0; i < masks.Length; i++)
            if ((buttons & masks[i]) != 0)
                names.Add(labels[i]);
        return String.Join(" + ", names);
    }
}

sealed class InputModeTracker
{
    // 最近检测的活动输入模式是否为手柄，不创建虚拟控制器。
    public bool Gamepad { get; private set; }

    public void Observe(bool connected, bool padActivity, bool keyboardActivity)
    {
        if (!connected || keyboardActivity)
            Gamepad = false;
        else if (padActivity)
            Gamepad = true;
    }
}

sealed class PadEdges
{
    int index = -1, previous, current;
    bool neutral, active;
    // 保存当前/前一帧手柄状态，供按下边缘触发。
    public void Update(PadSample sample, bool scope)
    {
        bool same = sample.Index == index;
        index = sample.Index;
        previous = current;
        current = sample.Buttons;
        if (!same || !scope || index < 0)
        {
            neutral = false;
            active = false;
            previous = current;
            return;
        }

        if (current == 0)
            neutral = true;
        active = neutral;
    }

    // 检查按钮或组合当前是否处于按下状态。
    public bool Down(int mask)
    {
        return active && mask != 0 && (current & mask) == mask;
    }

    // 检查本帧新按下边缘，避免持续按住反复触发快捷动作。
    public bool Pressed(int mask)
    {
        return Down(mask) && (previous & mask) != mask;
    }
}

sealed class KeyButton : OreButton
{
    public int KeyCode, PadBinding;
    public bool AllowGamepad, AllowMouse, FixedKeyboard, GameOutput;
    public int MappedKeyboard;
    public Action BeforeChange;
    public event EventHandler Changed;
    public KeyButton(int key)
    {
        KeyBinding = true;
        KeyCode = key;
        RefreshText();
        Width = 94;
        Height = 29;
        Click += delegate
        {
            if (BeforeChange != null)
                BeforeChange();
            int picked = KeyCode, pickedPad = PadBinding;
            using (var f = new Form
            {
                Text = L10n.T("设置键位"),
                ClientSize = new Size(500, 250),
                BackColor = OreTheme.Card,
                FormBorderStyle = FormBorderStyle.None,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent,
                KeyPreview = true,
                Font = Font
            }

            )
            using (var timer = new System.Windows.Forms.Timer
            {
                Interval = 25
            }

            )
            {
                f.Controls.Add(new PixelLabel { Text = L10n.T("设置键位"), PixelScale = 1.5f, EnglishHeading = true, Location = new Point(20, 15), Size = new Size(460, 35) });
                var device = new OreSelect
                {
                    Location = new Point(20, 58),
                    Size = new Size(460, 34),
                    FixedTypography = true
                };
                device.Items.Add(L10n.T("键盘 / 鼠标"));
                if (AllowGamepad)
                    device.Items.Add("Xbox");
                device.SelectedIndex = AllowGamepad && XboxPad.ActiveMode ? 1 : 0;
                f.Controls.Add(device);
                var prompt = new PixelLabel
                {
                    Location = new Point(20, 105),
                    Size = new Size(460, 65)
                };
                f.Controls.Add(prompt);
                Action instructions = delegate
                {
                    prompt.Text = device.SelectedIndex == 1 && GameOutput ? OutputPrompt() : device.SelectedIndex == 1 ? L10n.T("先松开按钮，再按下要绑定的按钮或组合，松开后确认。") : FixedKeyboard ? L10n.T("键盘快捷键保持 F8 / F9；可选择 Xbox 绑定手柄") : L10n.T("请按下要绑定的键；鼠标绑定请点击此提示区。Esc 取消");
                };
                bool neutral = false;
                int captured = 0, index = -1;
                device.SelectedIndexChanged += delegate
                {
                    neutral = false;
                    captured = 0;
                    index = -1;
                    instructions();
                };
                instructions();
                var clear = new OreButton
                {
                    Text = L10n.T("清除手柄绑定"),
                    Location = new Point(20, 195),
                    Size = new Size(180, 40),
                    Visible = AllowGamepad && !GameOutput
                };
                f.Controls.Add(clear);
                clear.Click += delegate
                {
                    pickedPad = 0;
                    f.DialogResult = DialogResult.OK;
                };
                var cancel = new OreButton
                {
                    Text = L10n.T("取消"),
                    Location = new Point(300, 195),
                    Size = new Size(180, 40)
                };
                f.Controls.Add(cancel);
                cancel.Click += delegate
                {
                    f.DialogResult = DialogResult.Cancel;
                };
                f.Paint += delegate (object sender, PaintEventArgs e)
                {
                    OreTheme.Border(e.Graphics, f.ClientRectangle, OreTheme.Line, 2);
                };
                f.KeyDown += delegate (object sender, KeyEventArgs e)
                {
                    e.SuppressKeyPress = true;
                    if (e.KeyCode == Keys.Escape)
                    {
                        f.DialogResult = DialogResult.Cancel;
                        return;
                    }

                    int k = (int)e.KeyCode;
                    if (device.SelectedIndex == 0 && !FixedKeyboard && e.Modifiers == Keys.None && ToolboxInput.Allowed(k))
                    {
                        picked = k;
                        f.DialogResult = DialogResult.OK;
                    }
                };
                prompt.MouseDown += delegate (object sender, MouseEventArgs e)
                {
                    if (AllowMouse && device.SelectedIndex == 0 && !FixedKeyboard)
                    {
                        picked = e.Button == MouseButtons.Left ? 1 : e.Button == MouseButtons.Right ? 2 : e.Button == MouseButtons.Middle ? 4 : e.Button == MouseButtons.XButton1 ? 5 : 6;
                        f.DialogResult = DialogResult.OK;
                    }
                };
                timer.Tick += delegate
                {
                    if (device.SelectedIndex != 1 || GameOutput)
                        return;
                    var sample = XboxPad.Read();
                    if (sample.Index < 0)
                    {
                        neutral = false;
                        captured = 0;
                        index = -1;
                        prompt.Text = L10n.T("等待 Xbox 手柄连接");
                        return;
                    }

                    if (index != sample.Index)
                    {
                        index = sample.Index;
                        neutral = false;
                        captured = 0;
                    }

                    if (!neutral)
                    {
                        if (sample.Buttons == 0)
                        {
                            neutral = true;
                            instructions();
                        }

                        return;
                    }

                    if (sample.Buttons != 0)
                    {
                        captured |= sample.Buttons;
                        prompt.Text = XboxPad.Name(captured);
                    }
                    else if (captured != 0)
                    {
                        pickedPad = captured;
                        f.DialogResult = DialogResult.OK;
                    }
                };
                timer.Start();
                if (f.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    KeyCode = picked;
                    PadBinding = pickedPad;
                    RefreshText();
                    if (Changed != null)
                        Changed(this, EventArgs.Empty);
                }
            }
        };
    }

    // 生成当前待绑定键的 UI 提示。
    public string OutputPrompt()
    {
        return PadBinding == 0 || MappedKeyboard == 0 ? L10n.T("游戏手柄键位未读取，请检查游戏内绑定。") : String.Format(L10n.T("跟随游戏键位：{0} → {1}\n使用对应的键盘操作；手柄按钮请在游戏内修改。"), XboxPad.Name(PadBinding), ToolboxInput.Name(MappedKeyboard));
    }

    // 刷新键码显示，不改变绑定值。
    public void RefreshText()
    {
        Text = AllowGamepad && XboxPad.ActiveMode ? XboxPad.Name(PadBinding).Replace(" + ", "+") : ToolboxInput.Name(KeyCode);
    }

    // 还原已接受的键位并刷新显示。
    public void ResetKey(int key)
    {
        if (BeforeChange != null)
            BeforeChange();
        KeyCode = key;
        if (!GameOutput)
            PadBinding = 0;
        RefreshText();
        if (Changed != null)
            Changed(this, EventArgs.Empty);
    }
}

// 完整物理右键周期按最初修饰键路由；已吞掉按下后，即使焦点、Shift 或开关改变也要吞掉对应松开。
sealed class RightDodgeRule
{
    bool down, blocked;
    // 输入钩子条件不确定时放行人工鼠标操作，不吞掉正常游戏输入。
    public void FailOpen()
    {
        blocked = false;
    }

    // 判断右键闪避钩子是否应处理此次事件，保留 Shift+右键远程攻击。
    public bool Handle(bool up, bool injected, bool active, bool shift, bool otherModifiers, out bool dodge)
    {
        dodge = false;
        if (injected)
            return false;
        if (up)
        {
            bool result = down && blocked;
            down = blocked = false;
            return result;
        }

        if (down)
            return blocked;
        down = true;
        blocked = active && !shift && !otherModifiers;
        dodge = blocked;
        return blocked;
    }
}

sealed class RightMouseHook : IDisposable
{
    // 与原生 ABI 对应的数据结构；字段顺序、类型及 StructLayout 决定字节布局，不能仅为美观调整。
    [StructLayout(LayoutKind.Sequential)]
    struct DATA
    {
        public int x, y;
        public uint mouseData, flags, time;
        public UIntPtr extra;
    }

    delegate IntPtr Procedure(int code, IntPtr message, IntPtr data);
    // 安装本进程监听的 Windows 低级鼠标钩子。
    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetWindowsHookEx(int type, Procedure callback, IntPtr module, uint thread);
    // 移除本对象拥有的鼠标钩子。
    [DllImport("user32.dll")]
    static extern bool UnhookWindowsHookEx(IntPtr hook);
    // 把未处理的事件传给后续钩子，保持正常人工操作。
    [DllImport("user32.dll")]
    static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    // 取得本模块句柄供鼠标钩子安装。
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr GetModuleHandle(string name);
    readonly Procedure callback;
    readonly Func<bool> active;
    readonly Action dodge;
    readonly Func<bool> consumeInput;
    bool passThroughPair;
    readonly RightDodgeRule rule = new RightDodgeRule();
    IntPtr handle;
    // 当前鼠标钩子是否成功安装。
    public bool Installed
    {
        get
        {
            return handle != IntPtr.Zero;
        }
    }

    public RightMouseHook(Func<bool> isActive, Action onDodge, Func<bool> shouldConsume = null)
    {
        active = isActive;
        dodge = onDodge;
        consumeInput = shouldConsume ?? (() => true);
        callback = Observe;
        handle = SetWindowsHookEx(14, callback, GetModuleHandle(null), 0);
    }

    IntPtr Observe(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && (message.ToInt64() == 0x204 || message.ToInt64() == 0x205))
        {
            try
            {
                var input = (DATA)Marshal.PtrToStructure(data, typeof(DATA));
                bool fire;
                if (message.ToInt64() == 0x204)
                    passThroughPair = !consumeInput();
                bool consume = rule.Handle(message.ToInt64() == 0x205, (input.flags & 3) != 0, active(), ToolboxInput.Held(16), ToolboxInput.OtherModifiers(), out fire);
                if (fire)
                    dodge();
                if (consume && !passThroughPair)
                    return (IntPtr)1;
            }
            catch
            {
                rule.FailOpen();
            } // 异常后仍要把已放行的按下与对应松开配对，避免吞掉正常人工操作。
        }

        return CallNextHookEx(handle, code, message, data);
    }

    // 释放本对象拥有的句柄、绘图对象或监听资源，避免退出后继续占用。
    public void Dispose()
    {
        if (handle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(handle);
            handle = IntPtr.Zero;
        }
    }
}

// 一次排队动作的数据；保存来源/目标/会话，执行前仍需重新校验。
sealed class InputRequest
{
    public int[] Keys;
    public string Description, ThreatId, TargetId, CombatSession;
    public int Delay, Generation;
    public long Expires;
    public bool Healing, Controller, Threat, CombatAttack, CombatArtifacts, CombatEvade, NearbyLoot;
}

// 主窗口的一个 partial 部分；事件处理与异步任务共用主窗口状态，退出时统一清理。
sealed partial class ToolboxForm : Form
{
    OreButton exportLogsButton;
    bool exportingLogs;
    long lastDiagnostic = -30000;
    CheckBox threatEnabled;
    PixelLabel threatNote;
    long lastThreatRead = -10000;
    ThreatFrame threatFrame = new ThreatFrame();
    readonly HashSet<string> consumedThreats = new HashSet<string>();
    readonly bool interactive;
    ToolboxSettings settings;
    HealthReader reader;
    System.Windows.Forms.Timer poll = new System.Windows.Forms.Timer();
    Stopwatch clock = Stopwatch.StartNew();
    HealRule healRule = new HealRule();
    JumpGesture gesture = new JumpGesture();
    Queue<InputRequest> requests = new Queue<InputRequest>();
    InputRequest currentAction;
    CancellationTokenSource actionCancel;
    int[] held = new int[0];
    IntPtr heldWindow;
    int heldPid;
    bool armed, connecting, pressing, wasFront, comboWas, conflict, closing, monitorRequested, resumeOnReconnect;
    int generation, lowSamples;
    readonly ConnectionReadGate readGate = new ConnectionReadGate();
    bool hudRefreshing;
    long lastHudRefresh = -15000;
    long nextReconnect, lastAllCooldownKnown;
    CooldownInfo[] cooldowns = new[]
    {
        new CooldownInfo(),
        new CooldownInfo(),
        new CooldownInfo(),
        new CooldownInfo()
    };
    CheckBox backgroundAuto;
    PixelLabel[] cooldownLabels = new PixelLabel[4], cooldownNames = new PixelLabel[4];
    long focusSince, lastHealth = -10000, lastConflict = -10000, suppressUntil, lastArtifact = -10000;
    float hp, max, souls = float.NaN, soulsMax = float.NaN, artifactCost = float.NaN;
    float[] artifactCosts = new[]
    {
        float.NaN,
        float.NaN,
        float.NaN
    };
    Label status, health, connectionBadge;
    Button connect;
    OreNumber threshold, retry, jumpDelay;
    KeyButton[] slotKeys = new KeyButton[3];
    KeyButton comboTrigger, jumpTrigger, jumpKey;
    CheckBox[] autoSlots = new CheckBox[3];
    CheckBox healEnabled, comboEnabled, jumpEnabled, dodgeEnabled, potionEnabled, autoCost;
    OreNumber soulCost;
    KeyButton potionKey;
    OreSelect potionSlot;
    PixelLabel artifactNote, autoCostHint;
    CheckBox[] comboSlots = new CheckBox[3];
    RightMouseHook mouseHook;
    KeyButton padStartButton, padStopButton, jumpAttack;
    PadEdges padEdges = new PadEdges();
    InputModeTracker inputMode = new InputModeTracker();
    PadSample lastPad = new PadSample
    {
        Index = -1
    };
    readonly System.Windows.Forms.Timer padPoll = new System.Windows.Forms.Timer
    {
        Interval = 25
    };
    Panel[] pages;
    OreButton[] navigation;
    PixelLabel pageTitle, pageDescription;
    OreHealthBar healthBar;
    OreSelect languageButton;
    OreButton maximizeButton;
    bool languageUpdating;
    int pageIndex;
    readonly Dictionary<Control, Rectangle> designBounds = new Dictionary<Control, Rectangle>();
    readonly Dictionary<Control, float> designTextScale = new Dictionary<Control, float>();
    bool layoutReady, fullScreen;
    Rectangle restoreBounds;
    FormWindowState restoreState;
    readonly System.Windows.Forms.Timer windowMotion = new System.Windows.Forms.Timer
    {
        Interval = 16
    }, pageMotion = new System.Windows.Forms.Timer
    {
        Interval = 16
    };
    long windowMotionStart, pageMotionStart;
    bool exitAnimation, exitReady;
    Panel movingPage;
    Rectangle movingBounds;
    // 读取 Windows 动效偏好，用于窗口页面动画。
    [DllImport("user32.dll")]
    static extern bool SystemParametersInfo(uint action, uint parameter, out bool value, uint flags);
    // 按系统偏好和窗口状态判断是否启用页面动效。
    static bool MotionAllowed()
    {
        bool enabled;
        return !SystemParametersInfo(0x1042, 0, out enabled, 0) || enabled;
    }

    // 应用窗口样式参数，保持原生 WinForms 句柄创建约束。
    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            if (interactive)
                parameters.Style |= 0x00C00000 | 0x00040000 | 0x00020000 | 0x00010000 | 0x00080000;
            return parameters;
        }
    }

    // 取消旧页面动画，避免快速切页时继续绘制过期页面。
    void CancelPageMotion()
    {
        pageMotion.Stop();
        if (movingPage != null && !movingPage.IsDisposed)
            movingPage.Bounds = movingBounds;
        movingPage = null;
    }

    // 按受限帧序推进页面切换动画，不参与游戏动作时序。
    void AnimatePage(int index)
    {
        CancelPageMotion();
        bool changed = index != pageIndex;
        ShowPage(index);
        if (!interactive || !changed || !MotionAllowed())
            return;
        movingPage = pages[index];
        movingBounds = movingPage.Bounds;
        pageMotionStart = clock.ElapsedMilliseconds;
        movingPage.Top = movingBounds.Top + 10;
        pageMotion.Start();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (interactive && MotionAllowed())
        {
            Opacity = 0;
            exitAnimation = false;
            windowMotionStart = clock.ElapsedMilliseconds;
            windowMotion.Start();
        }
    }

    // 退出前暂停任务、释放输入、停止组件并清理读取器。
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (interactive && !exitReady && Visible && MotionAllowed() && e.CloseReason == CloseReason.UserClosing)
        {
            Arm(false);
            closing = true;
            e.Cancel = true;
            if (!exitAnimation)
            {
                exitAnimation = true;
                windowMotionStart = clock.ElapsedMilliseconds;
                windowMotion.Start();
            }
        }

        base.OnFormClosing(e);
    }

    public ToolboxForm(bool run)
    {
        windowMotion.Tick += delegate
        {
            double progress = Math.Min(1, (clock.ElapsedMilliseconds - windowMotionStart) / 160.0);
            double ease = 1 - Math.Pow(1 - progress, 3);
            Opacity = exitAnimation ? 1 - ease : ease;
            if (progress >= 1)
            {
                windowMotion.Stop();
                if (exitAnimation)
                {
                    exitReady = true;
                    Close();
                }
            }
        };
        pageMotion.Tick += delegate
        {
            if (movingPage == null || closing)
            {
                CancelPageMotion();
                return;
            }

            double progress = Math.Min(1, (clock.ElapsedMilliseconds - pageMotionStart) / 150.0);
            movingPage.Top = movingBounds.Top + (int)Math.Round(10 * Math.Pow(1 - progress, 3));
            if (progress >= 1)
                CancelPageMotion();
        };
        FormClosed += delegate
        {
            CancelPageMotion();
            windowMotion.Stop();
            windowMotion.Dispose();
            pageMotion.Dispose();
            padPoll.Stop();
            padPoll.Dispose();
        };
        interactive = run;
        settings = run ? ToolboxSettings.Load() : new ToolboxSettings();
        L10n.Language = settings.Language;
        if (run)
            ToolboxLog.Start();
        Text = "Minecraft Dungeons II Assistant";
        float dpi = run ? OreDpi.Scale(IntPtr.Zero) : 1;
        Rectangle available = Screen.FromPoint(Cursor.Position).WorkingArea;
        ClientSize = new Size(Math.Min(available.Width - 24, (int)(OreMetrics.DesignWidth * dpi)), Math.Min(available.Height - 24, (int)(OreMetrics.DesignHeight * dpi)));
        MinimumSize = new Size(900, 700);
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        KeyPreview = true;
        Font = new Font("Microsoft YaHei UI", 11);
        BackColor = OreTheme.Background;
        DoubleBuffered = true;
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        using (var stream = assembly.GetManifestResourceStream("toolbox.ico"))
        {
            if (stream != null)
                using (var source = new Icon(stream))
                    Icon = (Icon)source.Clone();
        }

        var header = new Panel
        {
            Location = Point.Empty,
            Size = new Size(OreMetrics.DesignWidth, OreMetrics.HeaderHeight),
            BackColor = OreTheme.Field
        };
        Controls.Add(header);
        using (var stream = assembly.GetManifestResourceStream("toolbox.png"))
        {
            if (stream != null)
                using (var source = new Bitmap(stream))
                {
                    var picture = new PictureBox
                    {
                        Location = new Point(17, 11),
                        Size = new Size(40, 40),
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Image = new Bitmap(source)
                    };
                    header.Controls.Add(picture);
                }
        }

        var title = LabelAt(header, "Minecraft Dungeons II Assistant", 73, 11, 470, 40);
        ((PixelLabel)title).PixelScale = 1.15f;
        ((PixelLabel)title).Shadow = false;
        ((PixelLabel)title).VerticalCenter = true;
        ((PixelLabel)title).BrandHeading = true;
        ((PixelLabel)title).EnglishScale = 1;
        languageButton = new OreSelect
        {
            FixedTypography = true,
            Location = new Point(665, 14),
            Size = new Size(197, 34)
        };
        languageButton.Items.AddRange(new object[] { "简体中文", "English", "日本語", "한국어", "繁體中文（香港）", "繁體中文（台灣）" });
        languageButton.SelectedIndex = settings.Language;
        header.Controls.Add(languageButton);
        languageButton.SelectedIndexChanged += delegate
        {
            if (!languageUpdating)
                SetLanguage(languageButton.SelectedIndex);
        };
        var minimize = new OreButton
        {
            Text = "−",
            Location = new Point(920, 14),
            Size = new Size(38, 34),
            PixelScale = 1.3f
        };
        var close = new OreButton
        {
            Text = "×",
            Location = new Point(969, 14),
            Size = new Size(38, 34),
            Destructive = true,
            PixelScale = 1.3f
        };
        header.Controls.Add(minimize);
        header.Controls.Add(close);
        minimize.Click += delegate
        {
            WindowState = FormWindowState.Minimized;
        };
        close.Click += delegate
        {
            Close();
        };
        maximizeButton = new OreButton
        {
            Text = "□",
            Location = new Point(871, 14),
            Size = new Size(38, 34),
            PixelScale = 1.3f
        };
        header.Controls.Add(maximizeButton);
        maximizeButton.Click += delegate
        {
            ToggleMaximize();
        };
        Point drag = Point.Empty;
        bool dragging = false;
        MouseEventHandler down = delegate (object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && WindowState == FormWindowState.Normal && !fullScreen)
            {
                drag = PointToClient(Cursor.Position);
                dragging = true;
            }
        };
        MouseEventHandler move = delegate (object sender, MouseEventArgs e)
        {
            if (dragging && e.Button == MouseButtons.Left)
                Location = new Point(Cursor.Position.X - drag.X, Cursor.Position.Y - drag.Y);
        };
        MouseEventHandler up = delegate
        {
            dragging = false;
        };
        header.MouseDown += down;
        header.MouseMove += move;
        header.MouseUp += up;
        title.MouseDown += down;
        title.MouseMove += move;
        title.MouseUp += up;
        header.DoubleClick += delegate
        {
            ToggleMaximize();
        };
        title.DoubleClick += delegate
        {
            ToggleMaximize();
        };
        KeyDown += delegate (object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F11)
            {
                ToggleFullscreen();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape && fullScreen)
            {
                ToggleFullscreen();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };
        var sidebar = new OreSidebar
        {
            Location = new Point(0, OreMetrics.HeaderHeight),
            Size = new Size(OreMetrics.NavWidth, 636)
        };
        Controls.Add(sidebar);
        LabelAt(sidebar, L10n.T("功能设置"), 24, 20, 190, 28);
        var note = LabelAt(sidebar, L10n.T("按你的游玩习惯配置"), 24, 52, 190, 32);
        note.ForeColor = OreTheme.Muted;
        ((PixelLabel)note).PixelScale = 1;
        pages = new Panel[7];
        navigation = new OreButton[7];
        string[] names =
        {
            L10n.T("自动恢复"),
            L10n.T("操作辅助"),
            L10n.T("附近交互"),
            L10n.T("设置"),
            L10n.T("自动战斗"),
            L10n.T("装备整理"),
            L10n.T("首页")
        };
        for (int i = 0; i < pages.Length; i++)
        {
            int n = i;
            navigation[i] = new OreNavItem
            {
                Text = names[i],
                Tag = L10n.Canonical(names[i]),
                Navigation = true,
                IconIndex = Array.IndexOf(new[] { 6, 0, 2, 4, 5, 3 }, i),
                Location = new Point(8, 96 + Math.Max(0, Array.IndexOf(new[] { 6, 0, 2, 4, 5, 3 }, i)) * (OreMetrics.NavItemHeight + 4)),
                Size = new Size(OreMetrics.NavWidth - 32, OreMetrics.NavItemHeight),
                Visible = i != 1,
                PixelScale = 2
            };
            sidebar.Controls.Add(navigation[i]);
            navigation[i].Click += delegate
            {
                AnimatePage(n);
            };
            pages[i] = new OreScrollPanel();
            pages[i].Location = new Point(OreMetrics.ContentX, 171);
            pages[i].Size = new Size(OreMetrics.ContentWidth, 516);
            pages[i].BackColor = OreTheme.Background;
            Controls.Add(pages[i]);
        }

        LabelAt(sidebar, L10n.T("快捷操作"), 24, 421, 187, 25).ForeColor = OreTheme.Muted;
        var startCaption = (PixelLabel)LabelAt(sidebar, L10n.T("开始 / 暂停"), 18, 452, 124, 32);
        startCaption.Wrap = false;
        startCaption.VerticalCenter = true;
        startCaption.PixelScale = 0.85f;
        startCaption.EnglishScale = 0.7f;
        padStartButton = KeyAt(sidebar, 0x77, 146, 452, 61);
        padStartButton.Height = 32;
        padStartButton.PixelScale = 0.75f;
        padStartButton.AllowGamepad = true;
        padStartButton.FixedKeyboard = true;
        padStartButton.PadBinding = settings.PadStart;
        var stopCaption = (PixelLabel)LabelAt(sidebar, L10n.T("立即停止"), 18, 488, 124, 32);
        stopCaption.Wrap = false;
        stopCaption.VerticalCenter = true;
        stopCaption.PixelScale = 0.85f;
        stopCaption.EnglishScale = 0.7f;
        padStopButton = KeyAt(sidebar, 0x78, 146, 488, 61);
        padStopButton.Height = 32;
        padStopButton.PixelScale = 0.75f;
        padStopButton.AllowGamepad = true;
        padStopButton.FixedKeyboard = true;
        padStopButton.PadBinding = settings.PadStop;
        exportLogsButton = new OreButton
        {
            Text = L10n.T("导出日志"),
            Tag = "导出日志",
            Location = new Point(18, 538),
            Size = new Size(188, 42),
            PixelScale = 1
        };
        sidebar.Controls.Add(exportLogsButton);
        exportLogsButton.Click += delegate
        {
            ExportLogs();
        };
        var licenses = new OreButton
        {
            Text = L10n.T("许可与致谢"),
            Tag = "许可与致谢",
            Location = new Point(18, 590),
            Size = new Size(188, 34),
            PixelScale = 0.8f
        };
        sidebar.Controls.Add(licenses);
        licenses.Click += delegate
        {
            ShowLicenses();
        };
        pageTitle = (PixelLabel)LabelAt(this, "", 264, 85, 744, 42);
        pageTitle.PixelScale = 2;
        pageTitle.Shadow = false;
        pageTitle.EnglishHeading = true;
        pageTitle.EnglishScale = 0.85f;
        pageDescription = (PixelLabel)LabelAt(this, "", 264, 132, 744, 30);
        pageDescription.ForeColor = OreTheme.Muted;
        pageDescription.PixelScale = 1;
        for (int i = 0; i < 3; i++)
            slotKeys[i] = new KeyButton(settings.Slots[i]);
        var heal = pages[0];
        var enabled = Card(heal, 0, 76);
        LabelAt(enabled, L10n.T("低血量时自动使用法器"), 20, 15, 580, 25);
        LabelAt(enabled, L10n.T("生命低于设定比例时，同时使用已选法器。"), 20, 45, 590, 24).ForeColor = OreTheme.Muted;
        healEnabled = CheckAt(enabled, "", 660, 20, 64, settings.HealEnabled);
        var limit = Card(heal, 88, 120);
        LabelAt(limit, L10n.T("血量阈值"), 20, 16, 350, 26);
        LabelAt(limit, L10n.T("低于此百分比时触发"), 20, 46, 470, 24).ForeColor = OreTheme.Muted;
        threshold = Number(limit, 523, 12, 1, 99, settings.Threshold, 201);
        threshold.Suffix = "%";
        var slider = new OreSlider
        {
            Location = new Point(20, 78),
            Size = new Size(704, 34),
            Minimum = 1,
            Maximum = 99,
            Value = settings.Threshold
        };
        limit.Controls.Add(slider);
        slider.ValueChanged += delegate
        {
            if (threshold.Value != slider.Value)
                threshold.Value = slider.Value;
        };
        threshold.ValueChanged += delegate
        {
            slider.Value = (int)threshold.Value;
        };
        var artifacts = Card(heal, 220, 116);
        LabelAt(artifacts, L10n.T("使用法器（不勾选时仅使用药水）"), 20, 16, 690, 26);
        for (int i = 0; i < 3; i++)
        {
            autoSlots[i] = CheckAt(artifacts, L10n.T((i + 1) + " 槽位"), 20 + i * 237, 57, 230, settings.AutoSlots[i]);
            ((OreToggle)autoSlots[i]).Tile = true;
            autoSlots[i].Height = 44;
        }

        var fallback = Card(heal, 348, 224);
        LabelAt(fallback, L10n.T("法器不可用时使用药水"), 20, 24, 470, 26);
        potionEnabled = CheckAt(fallback, "", 523, 16, 64, settings.PotionFallback);
        potionKey = KeyAt(fallback, settings.PotionKey, 621, 12, 103);
        potionKey.AllowGamepad = true;
        potionKey.GameOutput = true;
        LabelAt(fallback, L10n.T("药水备用判断槽位"), 20, 68, 470, 26);
        potionSlot = new OreSelect
        {
            Location = new Point(523, 57),
            Size = new Size(201, 44)
        };
        for (int i = 0; i < 3; i++)
            potionSlot.Items.Add(L10n.T((i + 1) + " 槽位"));
        potionSlot.SelectedIndex = settings.PotionSlot;
        fallback.Controls.Add(potionSlot);
        artifactNote = (PixelLabel)LabelAt(fallback, L10n.T("法器：等待连接 · 消耗：等待读取"), 20, 111, 704, 26);
        artifactNote.ForeColor = OreTheme.Muted;
        artifactNote.PixelScale = 1;
        autoCost = CheckAt(fallback, L10n.T("自动读取灵魂消耗"), 20, 166, 476, settings.AutoCost);
        soulCost = Number(fallback, 523, 162, 0, 100000, settings.SoulCost, 201);
        soulCost.Visible = !settings.AutoCost;
        autoCostHint = (PixelLabel)LabelAt(fallback, L10n.T("只看指定槽位"), 523, 178, 201, 26);
        autoCostHint.ForeColor = OreTheme.Muted;
        autoCostHint.Visible = settings.AutoCost;
        potionSlot.SelectedIndexChanged += delegate
        {
            Changed();
        };
        var timing = Card(heal, 584, 80);
        LabelAt(timing, L10n.T("重试间隔"), 20, 16, 445, 26);
        var intervalNote = LabelAt(timing, L10n.T("法器和药水共用此尝试间隔"), 20, 46, 464, 26);
        ((PixelLabel)intervalNote).PixelScale = 1;
        intervalNote.ForeColor = OreTheme.Muted;
        retry = Number(timing, 523, 18, 1, 120, settings.RetrySeconds, 201);
        retry.Suffix = L10n.T("秒");
        var background = Card(pages[3], 0, 116);
        LabelAt(background, L10n.T("游戏后台运行"), 20, 17, 580, 26);
        LabelAt(background, L10n.T("统一控制已选功能，F9 停止所有操作。"), 20, 58, 704, 30).ForeColor = OreTheme.Muted;
        backgroundAuto = CheckAt(background, "", 660, 16, 64, settings.BackgroundAuto);
        var cooling = StatusCard(heal, 676, 222);
        LabelAt(cooling, L10n.T("法器与药水冷却"), 20, 17, 690, 26);
        for (int i = 0; i < 4; i++)
        {
            cooldownNames[i] = (PixelLabel)LabelAt(cooling, L10n.T(i == 3 ? "药水" : (i + 1) + " 槽位") + ":", 20, 57 + 39 * i, 145, 30);
            cooldownNames[i].Tag = i == 3 ? "药水" : (i + 1) + " 槽位";
            cooldownLabels[i] = (PixelLabel)LabelAt(cooling, L10n.T("等待冷却数据"), 170, 57 + 39 * i, 554, 30);
            cooldownLabels[i].ForeColor = OreTheme.Muted;
        }

        var combo = pages[1];
        var ce = Card(combo, 0, 76);
        LabelAt(ce, L10n.T("同时释放多个法器"), 20, 15, 580, 29);
        LabelAt(ce, L10n.T("组合使用你选中的法器槽位。"), 20, 45, 590, 24).ForeColor = OreTheme.Muted;
        comboEnabled = CheckAt(ce, "", 660, 20, 64, settings.ComboEnabled);
        var ct = Card(combo, 88, 160);
        LabelAt(ct, L10n.T("法器组合宏"), 20, 17, 660, 26);
        LabelAt(ct, L10n.T("为一个按键分配组合，按下时使用已选法器。"), 20, 51, 700, 27).ForeColor = OreTheme.Muted;
        var macroRow = new Panel
        {
            Location = new Point(3, 86),
            Size = new Size(738, 71),
            BackColor = OreTheme.Card
        };
        ct.Controls.Add(macroRow);
        LabelAt(macroRow, L10n.T("触发键"), 17, 22, 360, 30);
        comboTrigger = KeyAt(macroRow, settings.ComboTrigger, 520, 13, 201);
        comboTrigger.AllowGamepad = true;
        comboTrigger.PadBinding = settings.PadCombo;
        var reset = new OreButton
        {
            Text = "↶",
            Location = new Point(458, 13),
            Size = new Size(50, 44),
            PixelScale = 1.7f
        };
        macroRow.Controls.Add(reset);
        reset.Click += delegate
        {
            comboTrigger.ResetKey(0x47);
        };
        var choose = Card(combo, 260, 128);
        LabelAt(choose, L10n.T("选择要同时使用的法器"), 20, 17, 690, 28);
        for (int i = 0; i < 3; i++)
        {
            comboSlots[i] = CheckAt(choose, L10n.T((i + 1) + " 槽位"), 20 + i * 237, 62, 230, settings.ComboSlots[i]);
            ((OreToggle)comboSlots[i]).Tile = true;
            comboSlots[i].Height = 44;
        }

        var jump = pages[1];
        var je = Card(jump, 400, 76);
        LabelAt(je, L10n.T("启用跳劈辅助"), 20, 15, 580, 29);
        LabelAt(je, L10n.T("检测跳跃和左键操作，再补按指定按键。"), 20, 45, 610, 24).ForeColor = OreTheme.Muted;
        jumpEnabled = CheckAt(je, "", 660, 20, 64, settings.JumpEnabled);
        var flow = Card(jump, 488, 116);
        LabelAt(flow, L10n.T("检测组合 → 自动补按"), 20, 17, 700, 27);
        jumpTrigger = KeyAt(flow, settings.JumpTrigger, 20, 57, 145);
        LabelAt(flow, "+", 179, 69, 25, 27);
        jumpAttack = KeyAt(flow, settings.AttackTrigger, 220, 57, 158);
        jumpAttack.AllowMouse = true;
        LabelAt(flow, "→", 438, 68, 40, 29);
        jumpKey = KeyAt(flow, settings.JumpKey, 523, 57, 201);
        var jd = Card(jump, 616, 93);
        LabelAt(jd, L10n.T("补按延迟"), 20, 17, 435, 28);
        LabelAt(jd, L10n.T("检测到组合操作后，再等待此时长。"), 20, 52, 475, 28).ForeColor = OreTheme.Muted;
        jumpDelay = Number(jd, 523, 25, 0, 500, settings.JumpDelay, 201);
        jumpDelay.Suffix = "ms";
        var dodgePage = pages[1];
        var de = Card(dodgePage, 721, 76);
        LabelAt(de, L10n.T("启用右键闪避"), 20, 15, 580, 29);
        LabelAt(de, L10n.T("右键触发闪避；Shift + 右键保留远程攻击。"), 20, 45, 610, 24).ForeColor = OreTheme.Muted;
        dodgeEnabled = CheckAt(de, "", 660, 20, 64, settings.DodgeEnabled);
        var ranged = Card(dodgePage, 809, 93);
        LabelAt(ranged, L10n.T("远程攻击"), 20, 17, 690, 28);
        LabelAt(ranged, L10n.T("先按住 Shift，再按鼠标右键；可持续蓄力。"), 20, 52, 704, 28).ForeColor = OreTheme.Muted;
        BuildAutomationPages();
        var footer = new OreCard
        {
            Location = new Point(16, 700),
            Size = new Size(992, OreMetrics.FooterHeight),
            BackColor = OreTheme.Card
        };
        Controls.Add(footer);
        health = LabelAt(footer, L10n.T("生命：-- / --\n灵魂：-- / --"), 16, 10, 422, 43);
        ((PixelLabel)health).PixelScale = 1;
        health.ForeColor = OreTheme.Accent;
        healthBar = new OreHealthBar
        {
            Location = new Point(17, 55),
            Size = new Size(389, 9)
        };
        footer.Controls.Add(healthBar);
        connect = ButtonAt(footer, L10n.T("连接游戏"), 443, 13, 350);
        ((OreButton)connect).Primary = true;
        Button stop = ButtonAt(footer, L10n.T("停止（F9）"), 804, 13, 171);
        ((OreButton)stop).Destructive = true;
        status = LabelAt(footer, L10n.T("连接后自动开始并持续检测；进菜单前按 F9 停止。"), 16, 78, 960, 38);
        ((PixelLabel)status).PixelScale = 1;
        connectionBadge = LabelAt(footer, L10n.T("正在等待连接"), 443, 53, 350, 27);
        connectionBadge.ForeColor = OreTheme.Muted;
        ((PixelLabel)connectionBadge).Center = true;
        var version = LabelAt(footer, "MCD2A  1.1.0", 804, 53, 171, 27);
        version.ForeColor = OreTheme.Muted;
        ((PixelLabel)version).Center = true;
        ((PixelLabel)version).PixelScale = 0.8f;
        connect.Click += delegate
        {
            ToggleMain();
        };
        stop.Click += delegate
        {
            Arm(false);
        };
        foreach (var b in new KeyButton[]
        {
            slotKeys[0],
            slotKeys[1],
            slotKeys[2],
            comboTrigger,
            jumpTrigger,
            jumpAttack,
            jumpKey,
            potionKey,
            padStartButton,
            padStopButton
        }

        )
        {
            b.BeforeChange = delegate
            {
                Arm(false);
            };
            b.Changed += delegate
            {
                Changed();
            };
        }

        foreach (var c in new CheckBox[]
        {
            backgroundAuto,
            healEnabled,
            comboEnabled,
            jumpEnabled,
            dodgeEnabled,
            potionEnabled,
            autoCost,
            comboSlots[0],
            comboSlots[1],
            comboSlots[2],
            autoSlots[0],
            autoSlots[1],
            autoSlots[2]
        }

        )
            c.CheckedChanged += delegate
            {
                Changed();
            };
        threatEnabled.CheckedChanged += delegate
        {
            Changed();
        };
        foreach (var n in new OreNumber[]
        {
            threshold,
            retry,
            jumpDelay,
            soulCost
        }

        )
            n.ValueChanged += delegate
            {
                Changed();
            };
        BuildDashboard();
        ApplyLanguage();
        RefreshEquipmentLanguage();
        RefreshRecoveryMode();
        ShowPage(0);
        padPoll.Tick += delegate
        {
            PollGamepad();
        };
        if (interactive)
            padPoll.Start();
        poll.Interval = 15;
        poll.Tick += delegate
        {
            Poll();
        };
        FormClosing += delegate
        {
            closing = true;
            Arm(false);
            poll.Stop();
            padPoll.Stop();
            if (interactive)
                try
                {
                    ReadSettings();
                    settings.Save();
                }
                catch (Exception e)
                {
                    ToolboxLog.Error("Settings.Save", e);
                }

            if (reader != null)
            {
                reader.Dispose();
                reader = null;
            }

            if (interactive)
                ToolboxLog.Stop();
        };
        RecordLayout(this);
        layoutReady = true;
        Resize += delegate
        {
            LayoutWindow();
        };
        LayoutWindow();
        if (interactive)
            ToolboxLog.Write("Startup", DiagnosticState());
    }

    // 记录控件基准位置，供窗口尺寸变化布局使用。
    void RecordLayout(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            if (c is OreScrollBar)
                continue;
            designBounds[c] = c.Bounds;
            var label = c as PixelLabel;
            var button = c as OreButton;
            if (label != null)
                designTextScale[c] = label.PixelScale;
            else if (button != null)
                designTextScale[c] = button.PixelScale;
            RecordLayout(c);
        }
    }

    // 在普通/最大化窗口状态间切换并重排控件。
    void ToggleMaximize()
    {
        if (fullScreen)
        {
            ToggleFullscreen();
            return;
        }

        MaximizedBounds = Screen.FromControl(this).WorkingArea;
        WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        LayoutWindow();
    }

    // 切换全屏并保存/恢复原窗口边界。
    void ToggleFullscreen()
    {
        if (!fullScreen)
        {
            restoreState = WindowState;
            restoreBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            WindowState = FormWindowState.Normal;
            fullScreen = true;
            Bounds = Screen.FromControl(this).Bounds;
        }
        else
        {
            fullScreen = false;
            WindowState = FormWindowState.Normal;
            Bounds = restoreBounds;
            if (restoreState == FormWindowState.Maximized)
            {
                MaximizedBounds = Screen.FromControl(this).WorkingArea;
                WindowState = FormWindowState.Maximized;
            }
        }

        LayoutWindow();
    }

    // 切换导航页面，取消旧动画并刷新当前语言/选中状态。
    void ShowPage(int index)
    {
        CancelPageMotion();
        pageIndex = index;
        string[] titles =
        {
            L10n.T("自动恢复"),
            L10n.T("操作辅助"),
            L10n.T("附近交互"),
            L10n.T("设置"),
            L10n.T("附近敌人自动战斗"),
            L10n.T("装备整理"),
            L10n.T("首页")
        };
        string[] descriptions =
        {
            L10n.T("低于设定血量时，自动组合使用已选法器。"),
            L10n.T("法器组合、跳劈和右键闪避"),
            L10n.T("自动拾取、开箱、食用和破罐；可调整交互间隔"),
            L10n.T("一次安装收集、战斗和装备回收组件"),
            L10n.T("原地近战与提前使用法器；药水遵循血量阈值"),
            L10n.T("自定义快捷键整理新拾取装备，保留受保护物品"),
            L10n.T("连接游戏，选择功能，再按 F8 开始")
        };
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].Visible = i == index;
            navigation[i].Selected = i == index || i == 3 && index == 1;
            navigation[i].Invalidate();
        }

        pageTitle.Text = titles[index];
        pageDescription.Text = UiCaption(descriptions[index]);
        RefreshDashboard();
    }

    // 创建页面卡片容器并设定布局尺寸。
    static OreCard Card(Control parent, int y, int height)
    {
        var p = new OreSection
        {
            Location = new Point(0, y),
            Size = new Size(OreMetrics.ContentWidth, height)
        };
        parent.Controls.Add(p);
        return p;
    }

    // 把规范标题转换为当前语言的界面文字。
    static string UiCaption(string text)
    {
        return text.TrimEnd('。', '.');
    }

    // 在指定像素坐标创建文本标签。
    static Label LabelAt(Control parent, string text, int x, int y, int w, int h)
    {
        string original = L10n.Canonical(text);
        bool group = new[]
        {
            "MCD2A",
            "功能设置",
            "快捷操作"
        }.Contains(original);
        bool setting = new[]
        {
            "低血量时自动使用法器",
            "血量阈值",
            "使用法器",
            "使用法器（不勾选时仅使用药水）",
            "法器不可用时使用药水",
            "后台自动使用",
            "法器与药水冷却",
            "重试间隔",
            "同时释放多个法器",
            "法器组合宏",
            "触发键",
            "选择要同时使用的法器",
            "启用跳劈辅助",
            "检测组合 → 自动补按",
            "补按延迟"
        }.Contains(original);
        var l = new PixelLabel
        {
            Text = UiCaption(text),
            Tag = original,
            Location = new Point(x, y),
            Size = new Size(w, h),
            EnglishHeading = group,
            EnglishScale = setting ? 1.125f : 1
        };
        parent.Controls.Add(l);
        return l;
    }

    // 在指定位置创建开关，并绑定统一设置变化回调。
    static CheckBox CheckAt(Control parent, string text, int x, int y, int w, bool value)
    {
        OreToggle c = String.IsNullOrEmpty(text) ? (OreToggle)new OreSwitch() : new OreCheckbox();
        c.Text = text;
        c.Tag = L10n.Canonical(text);
        c.Location = new Point(x, y);
        c.Size = new Size(w, 36);
        c.Checked = value;
        parent.Controls.Add(c);
        return c;
    }

    // 创建有界数字输入；边界应与配置 Parse 和对应滑块一致。
    static OreNumber Number(Control parent, int x, int y, int min, int max, int value, int w)
    {
        var n = new OreNumber
        {
            Location = new Point(x, y),
            Size = new Size(w, 44),
            Minimum = min,
            Maximum = max,
            Value = value
        };
        parent.Controls.Add(n);
        return n;
    }

    // 创建可重绑定按键控件并绑定配置回调。
    static KeyButton KeyAt(Control parent, int key, int x, int y, int w)
    {
        var b = new KeyButton(key)
        {
            Location = new Point(x, y),
            Size = new Size(w, 44),
            PixelScale = 1.15f
        };
        parent.Controls.Add(b);
        return b;
    }

    // 创建操作按钮并连接既有行为入口。
    static Button ButtonAt(Control parent, string text, int x, int y, int w)
    {
        var b = new OreButton
        {
            Text = text,
            Tag = L10n.Canonical(text),
            Location = new Point(x, y),
            Size = new Size(w, 43),
            PixelScale = 1.05f
        };
        parent.Controls.Add(b);
        return b;
    }

    // 把控件值采集到统一设置对象，避免页面间保留过期配置。
    void ReadSettings()
    {
        ReadCombatSettings();
        settings.ThreatEnabled = threatEnabled.Checked;
        settings.GamepadEnabled = true;
        settings.PadStart = padStartButton.PadBinding;
        settings.PadStop = padStopButton.PadBinding;
        settings.PadCombo = comboTrigger.PadBinding;
        settings.AttackTrigger = jumpAttack.KeyCode;
        settings.PotionSlot = potionSlot.SelectedIndex;
        settings.BackgroundAuto = backgroundAuto.Checked;
        settings.Language = L10n.Language;
        settings.Threshold = (int)threshold.Value;
        settings.RetrySeconds = (int)retry.Value;
        for (int i = 0; i < 3; i++)
            settings.AutoSlots[i] = autoSlots[i].Checked;
        settings.HealSlot = settings.AutoSlots.Any(v => v) ? Array.FindIndex(settings.AutoSlots, v => v) : 3;
        settings.HealEnabled = healEnabled.Checked;
        settings.ComboEnabled = comboEnabled.Checked;
        settings.JumpEnabled = jumpEnabled.Checked;
        settings.DodgeEnabled = dodgeEnabled.Checked;
        settings.DodgeKey = 0x52;
        settings.ComboTrigger = comboTrigger.KeyCode;
        settings.JumpTrigger = jumpTrigger.KeyCode;
        settings.JumpKey = jumpKey.KeyCode;
        settings.JumpDelay = (int)jumpDelay.Value;
        settings.PotionFallback = potionEnabled.Checked;
        settings.AutoCost = autoCost.Checked;
        settings.PotionKey = potionKey.KeyCode;
        settings.SoulCost = (int)soulCost.Value;
        for (int i = 0; i < 3; i++)
        {
            settings.Slots[i] = slotKeys[i].KeyCode;
            settings.ComboSlots[i] = comboSlots[i].Checked;
        }
    }

    // 配置改变后刷新/验证并持久化；演示窗口不保存用户设置。
    void Changed()
    {
        Arm(false);
        ReadSettings();
        RefreshRecoveryMode();
        soulCost.Visible = !settings.AutoCost;
        autoCostHint.Visible = settings.AutoCost;
        lastArtifact = -10000;
        artifactCost = float.NaN;
        artifactCosts = new[]
        {
            float.NaN,
            float.NaN,
            float.NaN
        };
        if (interactive)
            try
            {
                settings.Save();
                ToolboxLog.Change("Settings", DiagnosticState());
            }
            catch (Exception e)
            {
                ToolboxLog.Error("Settings.Save", e);
            }

        status.Text = L10n.T("设置已更新，按 F8 开始。");
    }

    // 遍历控件翻译显示文字，保持配置值和规范键不变。
    static void TranslateControls(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            var original = c.Tag as string;
            if (!String.IsNullOrEmpty(original))
            {
                var label = c as PixelLabel;
                var button = c as OreButton;
                c.Text = label != null ? UiCaption(L10n.T(original)) : L10n.T(original);
            }

            TranslateControls(c);
        }
    }

    // 按新语言更新页面、字体、下拉项与状态说明。
    void ApplyLanguage()
    {
        SuspendLayout();
        TranslateControls(this);
        Text = "Minecraft Dungeons II Assistant";
        languageUpdating = true;
        languageButton.SelectedIndex = L10n.Language;
        languageButton.Invalidate();
        languageUpdating = false;
        foreach (var nav in navigation)
            nav.PixelScale = 1.15f;
        foreach (var b in new KeyButton[]
        {
            slotKeys[0],
            slotKeys[1],
            slotKeys[2],
            comboTrigger,
            jumpTrigger,
            jumpAttack,
            jumpKey,
            potionKey,
            padStartButton,
            padStopButton
        }

        )
            b.RefreshText();
        for (int i = 0; i < 3; i++)
            potionSlot.Items[i] = L10n.T((i + 1) + " 槽位");
        potionSlot.Invalidate();
        RefreshEquipmentLanguage();
        RefreshActionLabels();
        retry.Suffix = L10n.T("秒");
        retry.Invalidate();
        RefreshRecoveryMode();
        ShowPage(pageIndex);
        RefreshMainButton();
        if (max > 0)
            RenderHealth();
        for (int i = 0; i < 4; i++)
            cooldownNames[i].Text = L10n.T(i == 3 ? "药水" : (i + 1) + " 槽位") + ":";
        if (reader != null)
        {
            UpdateArtifact();
            UpdateCooldowns();
        }

        ResumeLayout();
        LayoutWindow();
        Invalidate(true);
    }

    // 设置稳定语言索引并刷新 UI；参数决定是否保存用户选择。
    public void SetLanguage(bool english)
    {
        SetLanguage(english ? 1 : 0);
    }

    // 设置稳定语言索引并刷新 UI；参数决定是否保存用户选择。
    public void SetLanguage(int language)
    {
        Arm(false);
        L10n.Language = Math.Max(0, Math.Min(5, language));
        ReadSettings();
        ApplyLanguage();
        status.Text = L10n.T("设置已更新，按 F8 开始。");
        if (interactive)
            try
            {
                settings.Save();
                ToolboxLog.Change("Settings", DiagnosticState());
            }
            catch (Exception e)
            {
                ToolboxLog.Error("Settings.Save", e);
            }
    }

    // 把当前生命值绘制到状态区，未知数据使用等待状态。
    void RenderHealth()
    {
        health.Text = String.Format(L10n.T("生命：{0:0.#} / {1:0.#}（{2:0.#}%）\n灵魂：{3} / {4}"), hp, max, 100 * hp / max, float.IsNaN(souls) ? L10n.T("未读取") : souls.ToString("0.#"), float.IsNaN(soulsMax) ? "?" : soulsMax.ToString("0.#"));
        healthBar.Percent = 100 * hp / max;
    }

    // 根据自动槽选择刷新法器/仅药水模式说明。
    void RefreshRecoveryMode()
    {
        bool artifacts = !settings.PotionOnly;
        potionEnabled.Enabled = artifacts;
        potionSlot.Enabled = artifacts;
        autoCost.Enabled = artifacts;
        soulCost.Enabled = artifacts;
        if (!artifacts)
            artifactNote.Text = L10n.T("未选择法器，仅按血量与药水冷却自动使用药水。");
    }

    // 更新单个法器槽名称、成本和可用性显示。
    void UpdateArtifact()
    {
        artifactCosts = new[]
        {
            float.NaN,
            float.NaN,
            float.NaN
        };
        ArtifactInfo reference = null;
        if (settings.PotionOnly)
        {
            artifactCost = float.NaN;
            artifactNote.Text = L10n.T("未选择法器，仅按血量与药水冷却自动使用药水。");
            return;
        }

        for (int i = 0; i < 3; i++)
        {
            if (!settings.AutoSlots[i] && i != settings.PotionSlot)
                continue;
            try
            {
                var a = reader.Artifact(i);
                artifactCosts[i] = a.Cost;
                if (i == settings.PotionSlot)
                    reference = a;
            }
            catch (Exception e)
            {
                ToolboxLog.Error("Artifact." + (i + 1), e);
            }
        }

        if (!settings.AutoCost)
            artifactCosts[settings.PotionSlot] = settings.SoulCost;
        artifactCost = artifactCosts[settings.PotionSlot];
        if (reference != null)
            artifactNote.Text = String.Format(L10n.T("{0} 槽位 · {1} · 消耗 {2} 灵魂"), settings.PotionSlot + 1, L10n.T(reference.Name), float.IsNaN(artifactCost) ? L10n.T("未读取") : artifactCost.ToString("0"));
        else
            artifactNote.Text = String.Format(L10n.T("{0} 槽位 · 灵魂消耗：{1}"), settings.PotionSlot + 1, float.IsNaN(artifactCost) ? L10n.T("未读取") : artifactCost.ToString("0"));
    }

    // 窗口句柄建立后注册当前需要的全局热键。
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!interactive)
            return;
        bool a = ToolboxInput.RegisterHotKey(Handle, 8, 0x4000, 0x77), b = ToolboxInput.RegisterHotKey(Handle, 9, 0x4000, 0x78);
        RegisterEquipmentHotkey();
        mouseHook = new RightMouseHook(DodgeActive, QueueDodge, GameForeground);
        ToolboxLog.Write("Input.Hooks", "F8=" + a + " F9=" + b + " rightMouse=" + mouseHook.Installed);
        if (!a || !b)
            status.Text = L10n.T("F8 / F9 被占用，请关闭旧工具后重新打开，或使用窗口按钮。");
    }

    // 窗口句柄销毁时移除本窗口注册的热键。
    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (mouseHook != null)
        {
            mouseHook.Dispose();
            mouseHook = null;
        }

        if (interactive)
        {
            ToolboxInput.UnregisterHotKey(Handle, 8);
            ToolboxInput.UnregisterHotKey(Handle, 9);
            ToolboxInput.UnregisterHotKey(Handle, 10);
        }

        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (interactive && m.Msg == 0x02E0)
        {
            // 跨显示器 DPI 变化遵循 Windows 推荐矩形，布局随后按逻辑尺寸重排。
            var next = (OreDpi.NativeRect)Marshal.PtrToStructure(m.LParam, typeof(OreDpi.NativeRect));
            Bounds = Rectangle.FromLTRB(next.Left, next.Top, next.Right, next.Bottom);
            LayoutWindow();
            m.Result = IntPtr.Zero;
            return;
        }

        if (interactive && m.Msg == 0x83 && m.WParam != IntPtr.Zero)
        {
            m.Result = IntPtr.Zero;
            return;
        }

        if (interactive && m.Msg == 0x312)
        {
            if (Form.ActiveForm != null && Form.ActiveForm != this)
            {
                m.Result = IntPtr.Zero;
                return;
            }

            if (m.WParam.ToInt32() == 8)
            {
                if (!exportingLogs)
                    ToggleMain();
            }
            else if (m.WParam.ToInt32() == 9)
                Arm(false);
            else if (m.WParam.ToInt32() == 10 && !exportingLogs && !closing && !connecting)
                ToggleEquipment();
        }

        base.WndProc(ref m);
        if (m.Msg == 0x84 && WindowState == FormWindowState.Normal && !fullScreen)
        {
            Point p = PointToClient(new Point(unchecked((short)(m.LParam.ToInt64() & 65535)), unchecked((short)((m.LParam.ToInt64() >> 16) & 65535))));
            bool left = p.X < 6, right = p.X >= ClientSize.Width - 6, top = p.Y < 6, bottom = p.Y >= ClientSize.Height - 6;
            if (left || right || top || bottom)
                m.Result = (IntPtr)(top ? (left ? 13 : right ? 14 : 12) : bottom ? (left ? 16 : right ? 17 : 15) : left ? 10 : 11);
        }
    }

    // 检查右键闪避是否启用且满足前台/玩家等前置条件。
    bool DodgeActive()
    {
        return !exportingLogs && !closing && !connecting && armed && settings.DodgeEnabled && reader != null && readGate.Ready && !conflict && hp > 0 && ShortcutScope() && (!GameForeground() || clock.ElapsedMilliseconds - focusSince >= 600);
    }

    // 排队一次右键闪避动作，保留人工按键与队列冲突检查。
    void QueueDodge()
    {
        int captured = generation;
        try
        {
            BeginInvoke((Action)delegate
            {
                if (captured != generation || !DodgeActive())
                    return;
                if (pressing && currentHealing)
                {
                    if (actionCancel != null)
                        actionCancel.Cancel();
                    Release();
                }

                var request = new InputRequest
                {
                    Keys = new[]
                    {
                        settings.DodgeKey
                    },
                    Description = L10n.T("右键闪避"),
                    Generation = generation,
                    Expires = clock.ElapsedMilliseconds + 600,
                    Healing = false
                };
                var pending = new Queue<InputRequest>();
                pending.Enqueue(request);
                foreach (var r in requests.Take(3))
                    pending.Enqueue(r);
                requests = pending;
            });
        }
        catch
        {
        } // 调度异常不能越过原生鼠标钩子回调边界。
    }

    // 切换整体开始/暂停状态，连接流程按现有门限处理。
    void ToggleMain()
    {
        if (connecting || closing)
            return;
        if (reader == null)
            Connect(false);
        else
            Arm(!armed);
    }

    // 确认连接和必要遥测都就绪后才允许启动。
    bool CanStartAfterConnect(int capturedGeneration)
    {
        return generation == capturedGeneration && !closing;
    }

    // 刷新连接/开始/暂停按钮标题和可用状态。
    void RefreshMainButton()
    {
        if (connectionBadge != null)
            connectionBadge.Text = connecting ? L10n.T("正在连接…") : reader == null ? L10n.T("正在等待连接") : L10n.T("已连接");
        connect.Text = connecting ? L10n.T("正在连接…") : (reader == null ? L10n.T("连接游戏") : (armed ? L10n.T("暂停（F8）") : L10n.T("开始（F8）")));
        connect.Enabled = !connecting && !closing;
    }

    // 统一开启或停止监测；停止时必须释放输入和组件请求。
    void Arm(bool on)
    {
        if (!on)
        {
            StopEquipment();
            StopDirectLoot();
            StopNativeCombat();
        }

        ReadSettings();
        string invalid = on ? settings.Validate() : null;
        if (on && interactive && settings.DodgeEnabled && (mouseHook == null || !mouseHook.Installed))
            invalid = L10n.T("右键监听失败，请重新打开工具箱。");
        if (on && invalid != null)
        {
            ToolboxLog.Limited("Automation.Rejected", invalid);
            status.Text = invalid;
            return;
        }

        resumeOnReconnect = on;
        armed = on && reader != null && !connecting && !closing;
        generation++;
        ToolboxLog.Change("Automation", "armed=" + armed + " requested=" + on + " connected=" + (reader != null));
        lowSamples = 0;
        wasFront = false;
        requests.Clear();
        gesture.Observe(false, false, false, clock.ElapsedMilliseconds);
        comboWas = false;
        if (actionCancel != null)
            actionCancel.Cancel();
        Release();
        RefreshMainButton();
        status.Text = armed ? L10n.T("已开启，持续检测血量与冷却。进菜单前按 F9 停止。") : L10n.T("已停止，按 F8 开始。");
    }

    // 释放当前工具拥有的键和请求状态，不释放玩家自行按下的输入。
    void Release()
    {
        ReleaseCombat();
        var keys = held;
        var window = heldWindow;
        int pid = heldPid;
        held = new int[0];
        heldWindow = IntPtr.Zero;
        heldPid = 0;
        if (keys.Length > 0)
            try
            {
                if (window != IntPtr.Zero)
                    ToolboxInput.SendWindow(window, pid, keys, true);
                else
                    ToolboxInput.Send(keys, true);
            }
            catch (Exception e)
            {
                ToolboxLog.Error("Input.Release", e);
            }
    }

    // 建立唯一游戏连接、读取布局与键位，失败保持暂停状态。
    async void Connect(bool recovery)
    {
        if (connecting || closing)
            return;
        ToolboxLog.Write("Connection.Attempt", recovery ? "automatic reconnect" : "user connect");
        bool resume = recovery ? resumeOnReconnect : true;
        Arm(false);
        monitorRequested = true;
        int connectGeneration = generation;
        connecting = true;
        poll.Stop();
        RefreshMainButton();
        status.Text = L10n.T("正在读取游戏血量…");
        try
        {
            if (reader != null)
            {
                reader.Dispose();
                reader = null;
            }

            HealthReader r = await Task.Run(delegate
            {
                var created = new HealthReader();
                try
                {
                    created.Snapshot();
                    return created;
                }
                catch
                {
                    created.Dispose();
                    throw;
                }
            });
            if (closing || IsDisposed)
            {
                r.Dispose();
                return;
            }

            reader = r;
            InitializeEquipmentBaseline();
            ToolboxLog.Write("Connection.Success", "pid=" + r.Pid);
            readGate.Success();
            lastAllCooldownKnown = clock.ElapsedMilliseconds;
            cooldowns = new[]
            {
                new CooldownInfo(),
                new CooldownInfo(),
                new CooldownInfo(),
                new CooldownInfo()
            };
            lastHealth = -10000;
            lastConflict = -10000;
            lastArtifact = -10000;
            connecting = false;
            poll.Start();
            Arm(resume && CanStartAfterConnect(connectGeneration));
        }
        catch (Exception e)
        {
            ToolboxLog.Error("Connection.Error", e);
            if (!IsDisposed)
            {
                resumeOnReconnect = resume && CanStartAfterConnect(connectGeneration);
                nextReconnect = clock.ElapsedMilliseconds + 3000;
                poll.Start();
                health.Text = L10n.T("生命：-- / --\n灵魂：-- / --");
                status.Text = L10n.T("持续等待角色进入关卡，自动重新连接。") + " " + L10n.T(e.Message);
            }
        }
        finally
        {
            connecting = false;
            if (!IsDisposed)
                RefreshMainButton();
        }
    }

    // 必要遥测未知时暂停动作并记录原因。
    void PauseUnreadTelemetry()
    {
        lowSamples = 0;
        wasFront = false;
        requests.Clear();
        nearbyObservation.Cancel();
        if (actionCancel != null)
            actionCancel.Cancel();
        Release();
        cooldowns = new[]
        {
            new CooldownInfo(),
            new CooldownInfo(),
            new CooldownInfo(),
            new CooldownInfo()
        };
        foreach (var label in cooldownLabels)
            label.Text = L10n.T("等待冷却数据");
    }

    // 在明确需要时重建冷却读取上下文，避免每帧全量重连。
    async void RefreshCooldownReader()
    {
        long now = clock.ElapsedMilliseconds;
        if (hudRefreshing || now - lastHudRefresh < 15000 || reader == null || closing)
            return;
        hudRefreshing = true;
        lastHudRefresh = now;
        HealthReader previous = reader, candidate = null;
        try
        {
            candidate = await Task.Run(delegate
            {
                var fresh = new HealthReader();
                try
                {
                    fresh.Snapshot();
                    bool known = false;
                    for (int i = 0; i < 4; i++)
                        known |= fresh.Cooldown(i).Known;
                    if (!known)
                    {
                        fresh.Dispose();
                        return null;
                    }

                    return fresh;
                }
                catch
                {
                    fresh.Dispose();
                    throw;
                }
            });
            if (candidate != null && !closing && !IsDisposed && reader == previous)
            {
                reader = candidate;
                candidate = null;
                previous.Dispose();
                ToolboxLog.Write("Connection.HudRefresh", "Cooldown reader refreshed");
                lastArtifact = -10000;
                lastAllCooldownKnown = clock.ElapsedMilliseconds;
            }
        }
        catch (Exception e)
        {
            ToolboxLog.Error("Connection.HudRefreshError", e);
        }
        finally
        {
            if (candidate != null)
                candidate.Dispose();
            hudRefreshing = false;
        }
    }

    // 读取必要槽冷却并刷新 UI；未知未使用槽位不阻断自动恢复。
    void UpdateCooldowns()
    {
        long now = clock.ElapsedMilliseconds;
        bool finished = false;
        for (int i = 0; i < 4; i++)
        {
            var value = reader.Cooldown(i);
            if (cooldowns[i].Known && !cooldowns[i].Ready && value.Ready)
                finished = true;
            cooldowns[i] = value;
            ToolboxLog.Change("Cooldown." + (i + 1), CooldownState(value));
            string label = i == 3 ? L10n.T("药水") : L10n.T((i + 1) + " 槽位");
            string state = !value.Known ? L10n.T("未读取") : value.Ready ? L10n.T("已就绪") : value.Cooling ? L10n.T("冷却中") : L10n.T("暂不可用");
            cooldownNames[i].Text = label + ":";
            cooldownLabels[i].Text = state + (value.Cooling ? String.Format(L10n.T("（冷却时长 {0:0.#} 秒）"), value.Duration) : "");
            cooldownLabels[i].ForeColor = value.Ready ? OreTheme.Accent : OreTheme.Muted;
        }

        // 原生冷却真正完成后解除重试计时，同时保留最小防抖间隔。
        if (finished && now - healRule.Last >= 1000)
            healRule.Last = now - settings.RetrySeconds * 1000L;
        if (CooldownRefreshRule.RequiredKnown(cooldowns, settings.AutoSlots))
            lastAllCooldownKnown = now;
    }

    // 检查已知输入冲突来源，按当前规则决定是否暂停。
    static bool OtherTool()
    {
        foreach (Process p in Process.GetProcesses())
        {
            try
            {
                string n = p.ProcessName;
                if (n.Equals("AutoHeal", StringComparison.OrdinalIgnoreCase) || n.StartsWith("Dungeons2Trainer", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch
            {
            }
            finally
            {
                p.Dispose();
            }
        }

        return false;
    }

    GameActionBindings gameBindings;
    long lastBindings = -10000;
    // 刷新实际游戏键位，检测缺失及冲突。
    void RefreshGameBindings()
    {
        try
        {
            gameBindings = reader.GameBindings();
            ToolboxLog.Change("Game.Bindings", "keyboard=" + KeyList(gameBindings.Keyboard) + " xbox=" + KeyList(gameBindings.Gamepad));
        }
        catch (Exception e)
        {
            gameBindings = null;
            ToolboxLog.Error("Game.BindingsError", e);
        }

        potionKey.PadBinding = gameBindings == null ? 0 : gameBindings.Gamepad[3];
        potionKey.MappedKeyboard = gameBindings == null ? 0 : gameBindings.Keyboard[3];
        potionKey.RefreshText();
        RefreshActionLabels();
    }

    // 更新已启用动作的键位/手柄显示。
    void RefreshActionLabels()
    {
        for (int i = 0; i < 3; i++)
        {
            string text = L10n.T((i + 1) + " 槽位");
            if (XboxPad.ActiveMode && gameBindings != null && gameBindings.Gamepad[i] != 0)
                text = XboxPad.Name(gameBindings.Gamepad[i]) + (L10n.English ? " / " : " · ") + text;
            autoSlots[i].Text = comboSlots[i].Text = text;
        }
    }

    // 将已校验输入请求交给执行通道，避免重入或覆盖运行中的请求。
    void Queue(int[] keys, string name, int delay, bool healing, long now, bool controller = false)
    {
        if (keys.Length == 0 || requests.Count >= 4)
            return;
        requests.Enqueue(new InputRequest { Keys = keys, Description = name, Delay = delay, Generation = generation, Expires = now + 600, Healing = healing, Controller = controller });
    }

    // 检查输入请求是否仍属于当前会话及启用状态。
    bool Active(InputRequest request)
    {
        return !exportingLogs && !closing && armed && reader != null && readGate.Ready && !conflict && GameScope();
    }

    bool currentHealing;
    // 检测人工摇杆移动，避免自动动作与手柄控制竞争。
    static bool StickMoved(short current, short previous, int deadzone)
    {
        return Math.Abs((int)current) > deadzone && Math.Abs((int)current - previous) > 2000;
    }

    // 读取手柄边缘触发并处理启停/组合输入。
    void PollGamepad()
    {
        if (closing)
            return;
        var sample = XboxPad.Read();
        long now = clock.ElapsedMilliseconds;
        bool padActivity = sample.Index == lastPad.Index && sample.Index >= 0 && ((sample.Buttons & ~lastPad.Buttons) != 0 || StickMoved(sample.LX, lastPad.LX, 7849) || StickMoved(sample.LY, lastPad.LY, 7849) || StickMoved(sample.RX, lastPad.RX, 8689) || StickMoved(sample.RY, lastPad.RY, 8689));
        bool keyboardActivity = ToolboxInput.KeyboardActivity() && (reader != null && ToolboxInput.Front(reader.Pid) || Form.ActiveForm == this);
        inputMode.Observe(sample.Index >= 0, padActivity, keyboardActivity && !pressing && now >= suppressUntil);
        lastPad = sample;
        if (XboxPad.ActiveMode != inputMode.Gamepad)
        {
            XboxPad.ActiveMode = inputMode.Gamepad;
            ToolboxLog.Write("Input.Mode", XboxPad.ActiveMode ? "Xbox" : "Keyboard/mouse");
            foreach (var b in new[]
            {
                comboTrigger,
                potionKey,
                padStartButton,
                padStopButton
            }

            )
                b.RefreshText();
            RefreshActionLabels();
        }

        bool gameFront = reader != null && ToolboxInput.Front(reader.Pid);
        bool scope = !exportingLogs && settings.GamepadEnabled && (GameScope() || Form.ActiveForm == this);
        padEdges.Update(sample, scope);
        if (padEdges.Pressed(settings.PadStop))
        {
            Arm(false);
            return;
        }

        if (padEdges.Pressed(settings.PadStart))
        {
            ToggleMain();
            return;
        }

        bool active = scope && GameScope() && Form.ActiveForm == null && armed && readGate.Ready && !conflict && hp > 0 && (!gameFront || now - focusSince >= 600) && now >= suppressUntil;
        if (active && settings.ComboEnabled && padEdges.Pressed(settings.PadCombo))
            Queue(settings.ComboKeys(), L10n.T("法器连发"), 0, false, now, true);
    }

    // 主轮询入口：刷新遥测，再分别判断恢复、战斗、收集和整理请求。
    void Poll()
    {
        if (closing)
            return;
        if (reader == null)
        {
            if (monitorRequested && !connecting && clock.ElapsedMilliseconds >= nextReconnect)
                Connect(true);
            return;
        }

        try
        {
            long now = clock.ElapsedMilliseconds;
            PollEquipment(now);
            if (pageIndex == 4 && now - lastMeleeRangeDisplay >= 1000)
            {
                lastMeleeRangeDisplay = now;
                RefreshMeleeRangeNote(reader.MeleeRangeForUi());
            }

            if (now - lastBindings >= 2000)
            {
                lastBindings = now;
                RefreshGameBindings();
            }

            if (now - lastConflict >= 1000)
            {
                lastConflict = now;
                conflict = OtherTool();
            }

            if (conflict)
            {
                Arm(false);
                status.Text = L10n.T("请关闭旧 AutoHeal 或修改器，再按 F8 开始。");
                return;
            }

            if (now - lastHealth >= 100)
            {
                lastHealth = now;
                float[] sample = reader.Snapshot();
                readGate.Success();
                hp = sample[0];
                max = sample[1];
                souls = sample[2];
                soulsMax = sample[3];
                RenderHealth();
                if (hp > 0 && hp < max * settings.Threshold / 100f)
                    lowSamples++;
                else
                    lowSamples = 0;
            }

            if (now - lastArtifact >= 250)
            {
                lastArtifact = now;
                UpdateArtifact();
                UpdateCooldowns();
                if (now - lastAllCooldownKnown > 5000)
                    RefreshCooldownReader();
            }

            bool front = ToolboxInput.Front(reader.Pid);
            if (front && !wasFront)
                focusSince = now;
            wasFront = front;
            bool active = !exportingLogs && armed && readGate.Ready && hp > 0 && GameScope() && (!front || now - focusSince >= 600) && now >= suppressUntil && (!front || !ToolboxInput.Modifiers());
            bool shortcuts = active && ShortcutScope();
            bool comboDown = ToolboxInput.Held(settings.ComboTrigger);
            if (shortcuts && settings.ComboEnabled && comboDown && !comboWas)
                Queue(settings.ComboKeys(), L10n.T("法器连发"), 0, false, now);
            comboWas = comboDown;
            bool jump = gesture.Observe(ToolboxInput.Held(settings.JumpTrigger), ToolboxInput.Held(settings.AttackTrigger), shortcuts, now);
            if (jump && settings.JumpEnabled)
                Queue(new int[] { settings.JumpKey }, L10n.T("跳劈补按"), settings.JumpDelay, false, now);
            bool automatic = active;
            // 持续按住的原生法器要让位给当前确实可用的低血量恢复。
            if (automatic && settings.HealEnabled && lowSamples >= 2 && healRule.Ready(hp, max, settings.Threshold, now, settings.RetrySeconds) && UseNativeCombat && currentAction != null && currentAction.CombatArtifacts && actionCancel != null && !actionCancel.IsCancellationRequested)
            {
                bool potion;
                if (RecoveryRule.Select(settings, cooldowns, souls, artifactCosts, out potion).Length > 0)
                {
                    ToolboxLog.Write("Combat.Preempted", "native artifact yields to low-health recovery");
                    actionCancel.Cancel();
                }
            }

            if (!active || !settings.NearbyDirect)
                nearbyObservation.Cancel();
            if (settings.ThreatEnabled && settings.HealEnabled && active && now - lastThreatRead >= 75)
            {
                lastThreatRead = now;
                threatFrame = reader.Threats(now);
                var ids = new HashSet<string>(threatFrame.Threats.Select(t => t.Id));
                consumedThreats.RemoveWhere(id => !ids.Contains(id));
                threatNote.Text = threatFrame.Known ? String.Format(L10n.T("附近敌人 {0} · 弹道 {1} · 危险 {2}"), threatFrame.Enemies, threatFrame.Projectiles, threatFrame.Threats.Count) : L10n.T("预警读数不可用，保持原有血量规则");
                if (!threatFrame.Known)
                    ToolboxLog.Limited("Threat.Unavailable", threatFrame.Reason);
                var danger = threatFrame.Threats.FirstOrDefault(t => !consumedThreats.Contains(t.Id));
                if (danger != null && now - healRule.Last >= settings.RetrySeconds * 1000L && !pressing && !requests.Any(r => r.Healing || r.Threat))
                {
                    var keys = ThreatRule.Select(settings, cooldowns, souls, artifactCosts, hp, max);
                    if (keys.Length > 0)
                    {
                        requests.Enqueue(new InputRequest { Keys = keys, Description = L10n.T("攻击预警自动使用"), Threat = true, ThreatId = danger.Id, Generation = generation, Expires = now + 250, Controller = XboxPad.ActiveMode });
                        ToolboxLog.Write("Threat.Detected", danger.Kind + " " + danger.Detail + " impactSeconds=" + danger.Seconds.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
                    }
                }
            }

            if (automatic && settings.HealEnabled && lowSamples >= 2 && healRule.Ready(hp, max, settings.Threshold, now, settings.RetrySeconds) && !requests.Any(r => r.Healing || r.Threat) && !pressing)
            {
                bool potion;
                var keys = RecoveryRule.Select(settings, cooldowns, souls, artifactCosts, out potion);
                if (keys.Length == 0)
                    ToolboxLog.Limited("Recovery.Wait", "No available artifact/potion output. " + CooldownSnapshot());
                Queue(keys, RecoveryRule.Description(settings, potion), 0, true, now, XboxPad.ActiveMode);
            }

            PollEvade(active, clock.ElapsedMilliseconds);
            PollCombat(active, clock.ElapsedMilliseconds);
            PollNearbyLoot(active, clock.ElapsedMilliseconds);
            if (!armed)
                status.Text = L10n.T("已连接，按 F8 开始。");
            else
                status.Text = front ? L10n.T("工具箱已开启，正在监测。进菜单前按 F9 停止。") : settings.BackgroundAuto ? L10n.T("正在运行，F9 停止所有功能。") : L10n.T("等待返回游戏。");
            if (exportingLogs || !armed || (!front && !settings.BackgroundAuto))
            {
                requests.Clear();
                if (actionCancel != null)
                    actionCancel.Cancel();
                Release();
            }

            if (now - lastDiagnostic >= 30000)
            {
                lastDiagnostic = now;
                ToolboxLog.Write("Telemetry", TelemetrySnapshot());
            }

            if (!pressing && requests.Count > 0)
            {
                var request = requests.Dequeue();
                if (request.Generation == generation && now <= request.Expires && Active(request))
                    RunRequest(request);
            }
        }
        catch (Exception e)
        {
            ToolboxLog.Error("Telemetry.Error", e);
            PauseUnreadTelemetry();
            if (!readGate.Failure(clock.ElapsedMilliseconds, !reader.Alive))
            {
                status.Text = L10n.T("血量读数暂不可用。");
                return;
            }

            bool resume = armed;
            ToolboxLog.Write("Connection.Lost", "Resume after reconnect=" + resume);
            Arm(false);
            resumeOnReconnect = resume;
            nextReconnect = clock.ElapsedMilliseconds + 3000;
            if (reader != null)
            {
                reader.Dispose();
                reader = null;
            }

            cooldowns = new[]
            {
                new CooldownInfo(),
                new CooldownInfo(),
                new CooldownInfo(),
                new CooldownInfo()
            };
            foreach (var label in cooldownLabels)
                label.Text = L10n.T("等待冷却数据");
            RefreshMainButton();
            health.Text = L10n.T("生命：-- / --\n灵魂：-- / --");
            status.Text = L10n.T("持续等待角色进入关卡，自动重新连接。") + " " + L10n.T(e.Message);
        }
    }

    // 执行排队输入，支持取消；finally 中释放本工具拥有的输入。
    async void RunRequest(InputRequest request)
    {
        if (request.NearbyLoot)
        {
            RunNearbyObservation(request);
            return;
        }

        pressing = true;
        currentAction = request;
        currentHealing = request.Healing;
        var cancel = new CancellationTokenSource();
        actionCancel = cancel;
        try
        {
            if (request.Delay > 0)
                await Task.Delay(request.Delay, cancel.Token);
            if (request.CombatEvade)
            {
                await RunCombatEvade(request, cancel.Token);
                return;
            }

            if (request.CombatAttack)
            {
                await RunCombatAttack(request, cancel.Token);
                return;
            }

            if (request.CombatArtifacts && UseNativeCombat)
            {
                await RunNativeArtifacts(request, cancel.Token);
                return;
            }

            if (request.CombatArtifacts && !PrepareCombatArtifacts(request))
                return;
            if (request.Healing)
            {
                float[] sample;
                try
                {
                    sample = reader.Snapshot();
                }
                catch (Exception e)
                {
                    ToolboxLog.Error("Recovery.TelemetryError", e);
                    PauseUnreadTelemetry();
                    readGate.Failure(clock.ElapsedMilliseconds, false);
                    return;
                }

                if (!healRule.Ready(sample[0], sample[1], settings.Threshold, clock.ElapsedMilliseconds, settings.RetrySeconds))
                    return;
                UpdateArtifact();
                UpdateCooldowns();
                bool potion;
                request.Keys = RecoveryRule.Select(settings, cooldowns, sample[2], artifactCosts, out potion);
                request.Description = RecoveryRule.Description(settings, potion);
            }

            if (request.Threat)
            {
                if (!settings.ThreatEnabled || !settings.HealEnabled || reader == null || !GameScope() || clock.ElapsedMilliseconds - healRule.Last < settings.RetrySeconds * 1000L)
                    return;
                var current = reader.Threats(clock.ElapsedMilliseconds);
                if (!current.Known || !current.Threats.Any(t => t.Id == request.ThreatId) || consumedThreats.Contains(request.ThreatId))
                    return;
                var sample = reader.Snapshot();
                UpdateArtifact();
                UpdateCooldowns();
                request.Keys = ThreatRule.Select(settings, cooldowns, sample[2], artifactCosts, sample[0], sample[1]);
            }

            if (request.Controller)
            {
                if (reader == null)
                    return;
                request.Keys = reader.GameBindings().Route(settings, request.Keys);
            }

            bool background = reader != null && !ToolboxInput.Front(reader.Pid);
            if (request.Keys.Length == 0 || cancel.IsCancellationRequested || request.Generation != generation || !Active(request) || (!background && ToolboxInput.Busy(request.Keys)) || clock.ElapsedMilliseconds > request.Expires)
            {
                ToolboxLog.Limited("Action.Skipped", L10n.Canonical(request.Description) + " keys=" + KeyList(request.Keys) + " cancelled=" + cancel.IsCancellationRequested + " stale=" + (request.Generation != generation) + " active=" + Active(request) + " expired=" + (clock.ElapsedMilliseconds > request.Expires) + " background=" + background);
                return;
            }

            held = request.Keys;
            heldWindow = background ? reader.Window : IntPtr.Zero;
            heldPid = reader.Pid;
            if (background)
            {
                if (heldWindow == IntPtr.Zero)
                    throw new Exception(L10n.T("游戏窗口已变化，等待重新连接。"));
                ToolboxInput.SendWindow(heldWindow, heldPid, request.Keys, false);
            }
            else
                ToolboxInput.Send(request.Keys, false);
            ToolboxLog.Write("Action.Sent", L10n.Canonical(request.Description) + " keys=" + KeyList(request.Keys) + " background=" + background + " controller=" + request.Controller + " " + TelemetrySnapshot());
            if (request.CombatArtifacts)
                lastCombatArtifact = clock.ElapsedMilliseconds;
            if (request.Healing || request.Threat)
                healRule.Last = clock.ElapsedMilliseconds;
            if (request.Threat)
                consumedThreats.Add(request.ThreatId);
            await Task.Delay(background ? (request.Healing || request.Threat ? 800 : 100) : 60, cancel.Token);
        }
        catch (OperationCanceledException)
        {
            ToolboxLog.Write("Action.Cancelled", L10n.Canonical(request.Description));
        }
        catch (Exception e)
        {
            ToolboxLog.Error("Action.Error", e);
            if (!closing)
            {
                Arm(false);
                status.Text = L10n.T(e.Message);
            }
        }
        finally
        {
            Release();
            suppressUntil = clock.ElapsedMilliseconds + (UseNativeCombat && (request.CombatAttack || request.CombatArtifacts || request.CombatEvade) ? 0 : 70);
            pressing = false;
            currentAction = null;
            currentHealing = false;
            if (actionCancel == cancel)
                actionCancel = null;
            cancel.Dispose();
        }
    }

    // 格式化遥测值；非有限数值显示 unknown。
    static string Metric(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? "unknown" : value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    // 生成去重键位列表的诊断名称。
    static string KeyList(int[] keys)
    {
        return String.Join(",", keys.Select(k => "0x" + k.ToString("X")));
    }

    // 把冷却 Known/Locked/Cooling 等状态转换为报告字段。
    static string CooldownState(CooldownInfo value)
    {
        return "known=" + value.Known + " ready=" + value.Ready + " locked=" + value.Locked + " cooling=" + value.Cooling + " duration=" + Metric(value.Duration) + " charges=" + Metric(value.Charges);
    }

    // 组织各槽冷却诊断，不执行任何技能。
    string CooldownSnapshot()
    {
        return String.Join("; ", cooldowns.Select((c, i) => (i == 3 ? "potion" : "slot" + (i + 1)) + " " + CooldownState(c) + " cost=" + (i < 3 ? Metric(artifactCosts[i]) : "n/a")));
    }

    // 组织生命、资源与模式的当前只读诊断快照。
    string TelemetrySnapshot()
    {
        return "connected=" + (reader != null) + " armed=" + armed + " readingsReady=" + readGate.Ready + " foreground=" + wasFront + " hp=" + Metric(hp) + "/" + Metric(max) + " souls=" + Metric(souls) + "/" + Metric(soulsMax) + " healthAgeMs=" + (lastHealth < 0 ? -1 : clock.ElapsedMilliseconds - lastHealth) + "; " + CooldownSnapshot();
    }

    // 汇总设置、连接和当前通道状态，先脱敏再导出。
    string DiagnosticState()
    {
        return "language=" + L10n.Language + " inputMode=" + (XboxPad.ActiveMode ? "Xbox" : "keyboard/mouse") + " connecting=" + connecting + " monitoring=" + monitorRequested + " conflict=" + conflict + " queued=" + requests.Count + " exporting=" + exportingLogs + "\r\n" + TelemetrySnapshot() + "\r\n" + "combatNative=" + settings.CombatNative + " combatEvade=" + settings.CombatEvade + " combatEncounter=" + settings.CombatEncounter + " combatAttack=" + settings.CombatAttack + " combatArtifacts=" + settings.CombatArtifacts + " combatRange=" + settings.CombatRange + " nearbyChests=" + settings.NearbyChests + " nearbyItems=" + settings.NearbyItems + " nearbyPots=" + settings.NearbyPots + " nearbyFood=" + settings.NearbyFood + " nearbyDirect=" + settings.NearbyDirect + " nearbyAllowMoving=" + settings.NearbyAllowMoving + " nearbyIntervalMs=" + settings.NearbyIntervalMs + " nearbyFoodIntervalMs=" + settings.NearbyFoodIntervalMs + "\r\n" + "threat=" + settings.ThreatEnabled + " heal=" + settings.HealEnabled + " threshold=" + settings.Threshold + " retrySeconds=" + settings.RetrySeconds + " autoSlots=" + String.Join(",", settings.AutoSlots.Select(v => v ? "1" : "0")) + " slotKeys=" + KeyList(settings.Slots) + "\r\n" + "potionOnly=" + settings.PotionOnly + " effectivePotion=" + settings.UsesPotion + " potionFallback=" + settings.PotionFallback + " referenceSlot=" + (settings.PotionSlot + 1) + " potionKey=" + KeyList(new[] { settings.PotionKey }) + " autoCost=" + settings.AutoCost + " manualCost=" + settings.SoulCost + " backgroundAuto=" + settings.BackgroundAuto + "\r\n" + "combo=" + settings.ComboEnabled + " comboSlots=" + String.Join(",", settings.ComboSlots.Select(v => v ? "1" : "0")) + " comboKey=" + KeyList(new[] { settings.ComboTrigger }) + " xboxStartStopCombo=" + KeyList(new[] { settings.PadStart, settings.PadStop, settings.PadCombo }) + "\r\n" + "jump=" + settings.JumpEnabled + " jumpDelayMs=" + settings.JumpDelay + " jumpKeys=" + KeyList(new[] { settings.JumpTrigger, settings.AttackTrigger, settings.JumpKey }) + " dodge=" + settings.DodgeEnabled + "\r\n" + "gameBindings=" + (gameBindings == null ? "unknown" : "keyboard=" + KeyList(gameBindings.Keyboard) + " xbox=" + KeyList(gameBindings.Gamepad)) + "\r\n" + CombatDiagnosticState() + "\r\n" + NearbyLootDiagnosticState() + "\r\n" + AdaptationRecord.Contents();
    }

    // 把日志和快照导出到用户选定文件，不自动上传。
    void ExportLogs()
    {
        if (exportingLogs || closing)
            return;
        exportingLogs = true;
        exportLogsButton.Enabled = false;
        requests.Clear();
        if (actionCancel != null)
            actionCancel.Cancel();
        Release();
        try
        {
            using (var dialog = new SaveFileDialog
            {
                Title = L10n.T("导出诊断日志"),
                Filter = L10n.T("文本文件 (*.txt)|*.txt"),
                DefaultExt = "txt",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = "MCD2A_log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt"
            }

            )
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                ToolboxLog.Write("Export", "Requested by user");
                ToolboxLog.Export(dialog.FileName, DiagnosticState());
                ToolboxLog.Write("Export", "Completed");
                MessageBox.Show(this, L10n.T("日志已导出，可附在群内或 GitHub Issues 反馈中。"), L10n.T("导出日志"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception e)
        {
            ToolboxLog.Error("Export.Error", e);
            MessageBox.Show(this, String.Format(L10n.T("导出失败，请选择其他保存位置。\n{0}"), DiagnosticLog.Redact(e.Message)), L10n.T("导出日志"), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            exportingLogs = false;
            exportLogsButton.Enabled = true;
            lowSamples = 0;
            wasFront = false;
            comboWas = ToolboxInput.Held(settings.ComboTrigger);
            gesture.Observe(false, false, false, clock.ElapsedMilliseconds);
        }
    }

    // 打开包含 MIT 及第三方许可的只读对话框。
    void ShowLicenses()
    {
        if (exportingLogs || closing)
            return;
        exportingLogs = true;
        requests.Clear();
        if (actionCancel != null)
            actionCancel.Cancel();
        Release();
        try
        {
            LicenseViewer.Show(this);
        }
        finally
        {
            exportingLogs = false;
            lowSamples = 0;
            wasFront = false;
            comboWas = ToolboxInput.Held(settings.ComboTrigger);
            gesture.Observe(false, false, false, clock.ElapsedMilliseconds);
        }
    }

    // 记录未处理异常并停止自动动作，保留用户可见错误状态。
    public void ReportUnhandledError(Exception e)
    {
        ToolboxLog.Error("Unhandled.UI", e);
        Arm(false);
        status.Text = L10n.T("发生错误，已停止。请导出日志反馈。");
    }

    // 在连接/遥测条件满足后恢复监测，仍受启动校验约束。
    public void ResumeMonitoring()
    {
        if (interactive)
            Connect(false);
    }

}

sealed class ConnectionReadGate
{
    long failedSince = -1;
    // 数据已知、未锁定且充能/冷却满足条件才判为就绪。
    public bool Ready { get; private set; }

    // 清除连接成功/失败计数，开始新的遥测可用性判断。
    public void Reset()
    {
        Ready = false;
        failedSince = -1;
    }

    // 记录一次必要遥测成功读取。
    public void Success()
    {
        Ready = true;
        failedSince = -1;
    }

    // 记录读取失败并推进暂停/重试门限。
    public bool Failure(long now, bool exited)
    {
        Ready = false;
        if (failedSince < 0)
            failedSince = now;
        return exited || now - failedSince >= 1500;
    }
}

static class ToolboxProgram
{

    [STAThread]
    // 程序启动和诊断参数入口；版本展示与报告同步，诊断分支不会自动进入正常窗口。
    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        L10n.Language = ToolboxSettings.Load().Language;
        bool made;
        using (var mutex = new Mutex(true, "Local\\MinecraftDungeons2Toolbox", out made))
        {
            if (!made)
            {
                MessageBox.Show(L10n.T("MCD2A已在运行。"));
                return 1;
            }

            using (var form = new ToolboxForm(true))
            {
                if (args.Length == 1 && args[0] == "--resume-monitor")
                    form.Shown += delegate
                    {
                        form.ResumeMonitoring();
                    };
                Application.ThreadException += delegate (object sender, ThreadExceptionEventArgs e)
                {
                    form.ReportUnhandledError(e.Exception);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e)
                {
                    ToolboxLog.Write("Unhandled.Fatal", DiagnosticLog.Redact(Convert.ToString(e.ExceptionObject)));
                    ToolboxLog.Stop();
                };
                try
                {
                    Application.Run(form);
                }
                finally
                {
                    ToolboxLog.Stop();
                }
            }

            return 0;
        }
    }
}

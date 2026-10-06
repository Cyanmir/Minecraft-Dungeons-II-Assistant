// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 中文维护说明：UI 侧战斗请求调度、按键租约与旧输入路径。原生模式应走 CombatBridge；本文件仍含旧鼠标瞄准实现，不能把它接回“不移动鼠标”的原生交互链。
// 源码导航和常改参数见 docs/maintenance.md；协议、反射标识及类型白名单修改前先核对两端。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

// 即使一个释放动作失败，按键租约也会尝试释放两组输入。
// CombatPressLease 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
sealed class CombatPressLease : IDisposable
{
    readonly Action<bool> root, mouse;
    readonly Action restore;
    bool rootHeld, mouseHeld, disposed;
    // 初始化 CombatPressLease 的本地状态、依赖和必要绑定；实例释放时使用对应清理流程。
    public CombatPressLease(Action<bool> root, Action<bool> mouse, Action restore)
    {
        this.root = root;
        this.mouse = mouse;
        this.restore = restore;
    }

    // 在旧按键路径准备原地攻击键，记录由本工具拥有的按键租约。
    public void PrepareRoot()
    {
        if (disposed)
            throw new ObjectDisposedException("CombatPressLease");
        if (rootHeld)
            return;
        rootHeld = true;
        root(false);
    }

    // 按下本工具当前租约持有的键，清理时必须释放。
    public void Press()
    {
        PrepareRoot();
        mouseHeld = true;
        mouse(false);
    }

    // 释放本对象拥有的句柄、绘图对象或监听资源，避免退出后继续占用。
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        try
        {
            if (mouseHeld)
                mouse(true);
        }
        finally
        {
            try
            {
                if (rootHeld)
                    root(true);
            }
            finally
            {
                restore();
            }
        }
    }

    // 运行本模块的离线规则自检；返回 PASS 摘要，失败抛出异常供命令行报告。
    public static string Test()
    {
        var log = new List<string>();
        var lease = new CombatPressLease(up => log.Add(up ? "root up" : "root down"), up => log.Add(up ? "mouse up" : "mouse down"), () => log.Add("restore"));
        lease.Press();
        lease.Dispose();
        lease.Dispose();
        if (!log.SequenceEqual(new[] { "root down", "mouse down", "mouse up", "root up", "restore" }))
            throw new Exception("Stop/cancel paired combat release failed");
        log.Clear();
        lease = new CombatPressLease(up => log.Add(up ? "root up" : "root down"), up =>
        {
            log.Add(up ? "mouse up" : "mouse down");
            throw new Exception("simulated partial send failure");
        }, () => log.Add("restore"));
        try
        {
            lease.Press();
        }
        catch
        {
        }

        try
        {
            lease.Dispose();
        }
        catch
        {
        }

        if (!log.SequenceEqual(new[] { "root down", "mouse down", "mouse up", "root up", "restore" }))
            throw new Exception("Partial input failure leaked rooted key");
        log.Clear();
        lease = new CombatPressLease(up => log.Add(up ? "root up" : "root down"), up => log.Add(up ? "mouse up" : "mouse down"), () => log.Add("restore"));
        lease.PrepareRoot();
        lease.Dispose();
        if (!log.SequenceEqual(new[] { "root down", "root up", "restore" }))
            throw new Exception("Cancellation during root lead-in leaked input");
        log.Clear();
        lease = new CombatPressLease(up => log.Add(up ? "root up" : "root down"), up => log.Add(up ? "mouse up" : "mouse down"), () => log.Add("restore"));
        lease.PrepareRoot();
        lease.Press();
        lease.Dispose();
        if (!log.SequenceEqual(new[] { "root down", "mouse down", "mouse up", "root up", "restore" }))
            throw new Exception("Root lead-in sent duplicate key down");
        return "PASS: combat stop/cancel paired mouse/key releases, root lead-in cancellation, idempotent cleanup and release-error recovery; fake input only.\r\n";
    }
}

// ToolboxInput 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
static partial class ToolboxInput
{
    // 与原生 ABI 对应的数据结构；字段顺序、类型及 StructLayout 决定字节布局，不能仅为美观调整。
    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT
    {
        public int x, y;
        public uint data, flags, time;
        public UIntPtr extra;
    }

    // 与原生 ABI 对应的数据结构；字段顺序、类型及 StructLayout 决定字节布局，不能仅为美观调整。
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    struct MOUSEUNION
    {
        [FieldOffset(0)]
        public MOUSEINPUT mouse;
    }

    // 与原生 ABI 对应的数据结构；字段顺序、类型及 StructLayout 决定字节布局，不能仅为美观调整。
    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEPACKET
    {
        public uint type;
        public MOUSEUNION value;
    }

    // 与原生 ABI 对应的数据结构；字段顺序、类型及 StructLayout 决定字节布局，不能仅为美观调整。
    [StructLayout(LayoutKind.Sequential)]
    struct CLIENTRECT
    {
        public int left, top, right, bottom;
    }

    // 旧 Windows 鼠标输入入口，原生模式不调用此方法。
    [DllImport("user32.dll", EntryPoint = "SendInput", SetLastError = true)]
    static extern uint SendMouseInput(uint count, MOUSEPACKET[] packets, int size);
    // 取得窗口客户区尺寸，用于坐标和可见范围校验。
    [DllImport("user32.dll")]
    static extern bool GetClientRect(IntPtr window, out CLIENTRECT rectangle);
    // 把客户区坐标转换成屏幕坐标，供旧输入路径使用。
    [DllImport("user32.dll")]
    static extern bool ClientToScreen(IntPtr window, ref Point point);
    // Windows 鼠标定位入口，仅属于旧输入路径；原生交互不能调用。
    [DllImport("user32.dll")]
    static extern bool SetCursorPos(int x, int y);
    // 读取游戏窗口客户区并验证显示区域。
    public static bool ClientArea(IntPtr window, int pid, out Rectangle area)
    {
        area = Rectangle.Empty;
        CLIENTRECT r;
        Point origin = Point.Empty;
        if (!OwnWindow(window, pid) || !GetClientRect(window, out r) || !ClientToScreen(window, ref origin) || r.right < 300 || r.bottom < 300)
            return false;
        area = new Rectangle(origin.X, origin.Y, r.right, r.bottom);
        return true;
    }

    // 核对旧瞄准路径的坐标与视口范围。
    public static bool CombatCursor(out Point p)
    {
        return GetCursorPos(out p);
    }

    // 旧输入模式暂时设置瞄准位置；不能用于“不移动鼠标”的原生模式。
    public static void Aim(IntPtr window, int pid, Point target)
    {
        if (!OwnWindow(window, pid) || !Front(pid) || !SetCursorPos(target.X, target.Y))
            throw new Exception("Combat aim unavailable");
    }

    // 还原旧输入路径前的鼠标位置。
    public static void RestoreAim(int pid, Point aim, Point previous)
    {
        Point current;
        if (Front(pid) && GetCursorPos(out current) && current == aim)
            SetCursorPos(previous.X, previous.Y);
    }

    // 旧输入路径发送左键动作并配对释放。
    public static void LeftClick(bool up)
    {
        var input = new MOUSEPACKET
        {
            value = new MOUSEUNION
            {
                mouse = new MOUSEINPUT
                {
                    flags = up ? 4u : 2u
                }
            }
        };
        if (SendMouseInput(1, new[] { input }, Marshal.SizeOf(typeof(MOUSEPACKET))) != 1)
            throw new Exception("Combat mouse input rejected");
    }

    // 计算旧鼠标输入结构的原生字节尺寸，供 ABI 自检。
    public static int MouseInputSize()
    {
        return Marshal.SizeOf(typeof(MOUSEPACKET));
    }
}

// 主窗口的一个 partial 部分；事件处理与异步任务共用主窗口状态，退出时统一清理。
sealed partial class ToolboxForm
{
    CheckBox combatAttackEnabled, combatEncounterEnabled, combatArtifactsEnabled;
    OreNumber combatRange;
    PixelLabel combatRuntimeNote;
    CombatPressLease combatLease;
    long lastCombatRead = -10000, lastCombatAttack = -10000, lastCombatArtifact = -10000, cursorMovedAt, lastCombatStatusLog = -10000;
    Point combatPreviousCursor;
    bool combatCursorKnown;
    CombatState combatState;
    // 汇总战斗触发/模式和当前状态，供脱敏日志使用。
    string CombatDiagnosticState()
    {
        return combatState == null ? "combatRead=not sampled" : "combatRead known=" + combatState.Known + " canAttempt=" + combatState.CanAttempt + " canAim=" + combatState.CanAim + " ageMs=" + (clock.ElapsedMilliseconds - lastCombatRead) + " enemies=" + combatState.Targets.Count + " reason=" + combatState.Reason + " cameraReason=" + combatState.CameraReason;
    }

    // 把战斗页控件采集到统一设置对象。
    void ReadCombatSettings()
    {
        settings.CombatNative = combatNativeEnabled.Checked;
        settings.NearbyAllowMoving = nearbyMovingEnabled.Checked;
        settings.NearbyIntervalMs = (int)nearbyInterval.Value;
        settings.NearbyFoodIntervalMs = (int)nearbyFoodInterval.Value;
        settings.NearbyDirect = nearbyDirectEnabled.Checked;
        settings.NearbyChests = nearbyChestsEnabled.Checked;
        settings.NearbyItems = nearbyItemsEnabled.Checked;
        settings.NearbyPots = nearbyPotsEnabled.Checked;
        settings.NearbyFood = nearbyFoodEnabled.Checked;
        settings.CombatEvade = combatEvadeEnabled.Checked;
        settings.CombatAttack = combatAttackEnabled.Checked;
        settings.CombatEncounter = combatEncounterEnabled.Checked;
        settings.CombatArtifacts = combatArtifactsEnabled.Checked;
        settings.CombatRange = (int)combatRange.Value;
    }

    // 释放当前战斗按键租约和输入状态。
    void ReleaseCombat()
    {
        var lease = combatLease;
        combatLease = null;
        if (lease != null)
            try
            {
                lease.Dispose();
            }
            catch (Exception e)
            {
                ToolboxLog.Error("Combat.Release", e);
            }
    }

    // 检测旧输入路径的人工操作与稳定状态。
    bool CombatUserIdle(long now)
    {
        Point point;
        if (!ToolboxInput.CombatCursor(out point))
            return false;
        if (!combatCursorKnown || point != combatPreviousCursor)
        {
            cursorMovedAt = now;
            combatPreviousCursor = point;
            combatCursorKnown = true;
        }

        bool held = Enumerable.Range(8, 247).Any(ToolboxInput.Held) || new[]
        {
            1,
            2,
            4,
            5,
            6
        }.Any(ToolboxInput.Held);
        if (held)
            cursorMovedAt = now;
        return now - cursorMovedAt >= 300 && !held;
    }

    // 检查攻击/技能键与工具触发键是否冲突。
    bool CombatBindingConflict(int root)
    {
        return root == 0 || root == settings.ComboTrigger && settings.ComboEnabled || root == settings.JumpTrigger && settings.JumpEnabled;
    }

    // 在轮询中选择原生或旧输入模式，校验目标、手动输入和执行占用。
    void PollCombat(bool active, long now)
    {
        if (!active || (!settings.CombatAttack && !settings.CombatEncounter && !settings.CombatArtifacts) || pressing || requests.Count > 0 || now - lastCombatRead < 75)
            return;
        lastCombatRead = now;
        combatState = reader.ReadCombatState();
        bool reportStatus = now - lastCombatStatusLog >= 1000;
        if (reportStatus)
            lastCombatStatusLog = now;
        if (!settings.CombatNative && combatState.CanAttempt && !combatState.CanAim)
            ToolboxLog.Limited("Combat.CameraUnavailable", combatState.CameraReason);
        combatRuntimeNote.Text = !combatState.CanAttempt ? L10n.T("等待战斗场景；菜单或状态不明时暂停") : String.Format(L10n.T("附近有效敌人 {0} · 原地近战范围 {1}"), combatState.Targets.Count, settings.CombatRange);
        if (settings.CombatEncounter && !settings.CombatNative)
        {
            combatRuntimeNote.Text = L10n.T("遇敌自动攻击需要开启原生模式");
            ToolboxLog.Limited("Combat.EncounterBlocked", "native mode is off");
        }

        if (!settings.CombatNative && settings.CombatAttack && combatState.CanAttempt && (!combatState.LeftMousePrimary || combatState.RootKey == 0 || CombatBindingConflict(combatState.RootKey)))
            combatRuntimeNote.Text = L10n.T("检查游戏左键主操作、原地攻击键及触发键冲突");
        if (!combatState.CanAttempt)
        {
            ToolboxLog.Limited("Combat.Unavailable", !combatState.Known ? combatState.Reason : combatState.Readiness == null ? "readiness unavailable" : !combatState.Readiness.Known ? combatState.Readiness.Reason : !combatState.Readiness.Alive ? "player not alive" : "gameplay UI blocked; panels=" + String.Join(",", combatState.Readiness.ActivePanels ?? new string[0]));
            return;
        }

        // 低血量恢复优先；本轮询位于恢复动作排队之后。
        var artifact = NativeCombatRule.ArtifactTarget(combatState, settings.CombatNative && settings.CombatEncounter);
        if (reportStatus && settings.CombatNative && settings.CombatArtifacts)
            ToolboxLog.Limited("Combat.NativeArtifactStatus", artifact == null ? "no eligible enemy targeting player; candidates=" + combatState.Targets.Count : !settings.AutoSlots.Any(v => v) ? "no selected artifact slots" : now - lastCombatArtifact < settings.RetrySeconds * 1000L ? "artifact retry interval" : NativeCombatRule.ArtifactSlots(settings, cooldowns, souls, artifactCosts).Length == 0 ? "no ready slot within soul budget" : "ready; target=" + artifact.Id);
        if (settings.CombatArtifacts && !settings.AutoSlots.Any(v => v))
            ToolboxLog.Limited("Combat.ArtifactsBlocked", "no artifact slots selected on Auto artifacts page; potion-only health mode; no artifact input requested");
        if (settings.CombatArtifacts && artifact != null && now - lastCombatArtifact >= settings.RetrySeconds * 1000L)
        {
            var keys = settings.CombatNative ? NativeCombatRule.ArtifactSlots(settings, cooldowns, souls, artifactCosts) : CombatActionRule.ArtifactKeys(settings, cooldowns, souls, artifactCosts);
            if (keys.Length > 0)
            {
                requests.Enqueue(new InputRequest { Keys = keys, Description = L10n.T("战斗提前使用法器"), CombatArtifacts = true, TargetId = artifact.Id, CombatSession = combatState.Session, Generation = generation, Expires = clock.ElapsedMilliseconds + 1200, Controller = XboxPad.ActiveMode });
                return;
            }
        }

        var target = settings.CombatNative ? NativeCombatRule.AttackTarget(combatState, settings.CombatRange, settings.CombatEncounter) : CombatActionRule.AttackTarget(combatState, settings.CombatRange);
        bool idle = settings.CombatNative ? CombatScheduling.NativeInputReady(combatState, settings, ToolboxInput.Held, settings.CombatEncounter) : CombatUserIdle(now);
        if (reportStatus && settings.CombatNative && (settings.CombatAttack || settings.CombatEncounter))
            ToolboxLog.Limited("Combat.NativeStatus", !combatState.PlayerVelocity.Valid ? "velocity unavailable" : !settings.CombatEncounter && combatState.PlayerVelocity.Length > 30 ? "player moving; speed=" + combatState.PlayerVelocity.Length.ToString("0.0") : target == null ? "no valid enemy inside melee range=" + settings.CombatRange + "; candidates=" + combatState.Targets.Count + " nearest=" + (combatState.Targets.Count == 0 ? "none" : combatState.Targets.Min(t => t.Distance).ToString("0.0")) : !idle ? "manual command held" : now - lastCombatAttack < 350 ? "native melee interval" : "ready; target=" + target.Id);
        if (settings.CombatAttack && !settings.CombatNative)
            ToolboxLog.Limited("Combat.Status", !combatState.CanAim ? "camera unavailable" : XboxPad.ActiveMode ? "controller mode; automatic melee is keyboard/mouse only" : CombatBindingConflict(combatState.RootKey) ? "root/trigger binding conflict" : target == null ? "no valid enemy inside melee range=" + settings.CombatRange + "; nearby candidates=" + combatState.Targets.Count : !idle ? "manual input; automatic melee paused" : "ready; target=" + target.Id);
        if ((settings.CombatAttack || settings.CombatNative && settings.CombatEncounter) && (settings.CombatNative || !XboxPad.ActiveMode) && target != null && (settings.CombatNative || !CombatBindingConflict(combatState.RootKey)) && now - lastCombatAttack >= 350 && idle)
        {
            if (!settings.CombatNative)
            {
                Rectangle area;
                Point point;
                if (!ToolboxInput.ClientArea(reader.Window, reader.Pid, out area) || !combatState.Camera.Project(target.Position, area, out point))
                    return;
            }

            requests.Enqueue(new InputRequest { Keys = new int[0], Description = L10n.T("附近敌人原地近战"), CombatAttack = true, TargetId = target.Id, CombatSession = combatState.Session, Generation = generation, Expires = clock.ElapsedMilliseconds + 300 });
        }
    }

    // 执行前再次验证会话、玩家和目标，拒绝过期排队请求。
    bool ValidateCombatRequest(InputRequest request, out CombatState current)
    {
        current = null;
        if (!Active(request) || clock.ElapsedMilliseconds > request.Expires || request.Generation != generation)
            return false;
        current = reader.ReadCombatState();
        return current.CanAttempt && current.Session == request.CombatSession && ToolboxInput.Front(reader.Pid) && !ToolboxInput.Modifiers() && Active(request) && request.Generation == generation && clock.ElapsedMilliseconds <= request.Expires;
    }

    // 执行已校验近战请求，按设置路由到原生/旧输入模式。
    async Task RunCombatAttack(InputRequest request, CancellationToken cancel)
    {
        if (settings.CombatNative)
        {
            await RunNativeMelee(request, cancel);
            return;
        }

        CombatState current;
        if (!settings.CombatAttack || XboxPad.ActiveMode || !ValidateCombatRequest(request, out current) || cancel.IsCancellationRequested || CombatBindingConflict(current.RootKey) || !CombatUserIdle(clock.ElapsedMilliseconds))
            return;
        // 再次核对同一目标身份，不能执行旧指针或旧缓存目标。
        var target = CombatActionRule.AttackTarget(current, settings.CombatRange);
        if (target == null || target.Id != request.TargetId || ToolboxInput.Held(current.RootKey))
            return;
        Rectangle area;
        Point aim, previous;
        IntPtr window = reader.Window;
        int pid = reader.Pid;
        if (!ToolboxInput.ClientArea(window, pid, out area) || !current.Camera.Project(target.Position, area, out aim) || !ToolboxInput.CombatCursor(out previous))
            return;
        combatLease = new CombatPressLease(up =>
        {
            if (!up && (!ToolboxInput.Front(pid) || cancel.IsCancellationRequested))
                throw new OperationCanceledException();
            ToolboxInput.Send(new[] { current.RootKey }, up);
        }, up =>
        {
            if (!up && (!ToolboxInput.Front(pid) || cancel.IsCancellationRequested))
                throw new OperationCanceledException();
            ToolboxInput.LeftClick(up);
        }, () => ToolboxInput.RestoreAim(pid, aim, previous));
        ToolboxInput.Aim(window, pid, aim);
        combatLease.Press();
        lastCombatAttack = clock.ElapsedMilliseconds;
        ToolboxLog.Write("Combat.Attack", "stationary primary attack; target=" + target.Id + " " + target.Name + " distance=" + target.Distance.ToString("0.0") + "; game activation remains authoritative");
        await Task.Delay(60, cancel);
    }

    // 根据勾选槽位、冷却和灵魂预算准备法器请求。
    bool PrepareCombatArtifacts(InputRequest request)
    {
        CombatState current;
        if (!settings.CombatArtifacts || !ValidateCombatRequest(request, out current) || clock.ElapsedMilliseconds - lastCombatArtifact < settings.RetrySeconds * 1000L)
            return false;
        var target = CombatActionRule.ArtifactTarget(current);
        if (target == null || target.Id != request.TargetId)
            return false;
        var sample = reader.Snapshot();
        UpdateArtifact();
        UpdateCooldowns();
        request.Keys = CombatActionRule.ArtifactKeys(settings, cooldowns, sample[2], artifactCosts);
        return request.Keys.Length > 0;
    }

    // 离线检查战斗页面开关和配置绑定。
    void TestCombatUi()
    {
        if (nearbyChestsEnabled.Checked || nearbyItemsEnabled.Checked || nearbyPotsEnabled.Checked || nearbyFoodEnabled.Checked || nearbyDirectEnabled.Checked || settings.NearbyDirect || settings.NearbyChests || settings.NearbyItems || settings.NearbyPots || settings.NearbyFood)
            throw new Exception("Nearby interactions must default off");
        nearbyChestsEnabled.Checked = true;
        nearbyItemsEnabled.Checked = true;
        nearbyPotsEnabled.Checked = true;
        nearbyFoodEnabled.Checked = true;
        nearbyDirectEnabled.Checked = true;
        ReadSettings();
        if (!settings.NearbyChests || !settings.NearbyItems || !settings.NearbyPots || !settings.NearbyFood || !settings.NearbyDirect || armed)
            throw new Exception("Nearby settings binding failed");
        if (combatEncounterEnabled.Checked || settings.CombatEncounter || combatNativeEnabled.Checked || settings.CombatNative || combatEvadeEnabled.Checked || settings.CombatEvade || combatAttackEnabled.Checked || combatArtifactsEnabled.Checked || settings.CombatAttack || settings.CombatArtifacts || combatRange.Value != 220)
            throw new Exception("Combat opt-in defaults failed");
        combatNativeEnabled.Checked = true;
        combatEncounterEnabled.Checked = true;
        combatEvadeEnabled.Checked = true;
        combatAttackEnabled.Checked = true;
        combatArtifactsEnabled.Checked = true;
        combatRange.Value = 300;
        ReadSettings();
        if (!settings.CombatEncounter || !settings.CombatNative || !settings.CombatEvade || !settings.CombatAttack || !settings.CombatArtifacts || settings.CombatRange != 300 || armed)
            throw new Exception("Combat settings binding failed");
        for (int language = 0; language < 6; language++)
        {
            SetLanguage(language);
            if (!settings.CombatEncounter || !settings.CombatNative || !settings.CombatEvade || !settings.CombatAttack || !settings.CombatArtifacts || !settings.NearbyChests || !settings.NearbyItems || !settings.NearbyPots || !settings.NearbyFood || !settings.NearbyDirect || settings.CombatRange != 300 || armed)
                throw new Exception("Combat / nearby language preservation failed");
        }

        var receipt = new TaskCompletionSource<bool>();
        bool observationStopped = false;
        var observation = nearbyObservation.Run(token =>
        {
            token.Register(() =>
            {
                observationStopped = true;
                receipt.TrySetResult(true);
            });
            return receipt.Task;
        });
        var log = new List<string>();
        combatLease = new CombatPressLease(up => log.Add(up ? "key up" : "key down"), up => log.Add(up ? "mouse up" : "mouse down"), () => log.Add("restore"));
        combatLease.Press();
        Arm(false);
        if (!observationStopped || combatLease != null || !log.Contains("mouse up") || !log.Contains("key up") || requests.Count != 0)
            throw new Exception("F9 combat release / collection observation cancellation failed");
        nearbyChestsEnabled.Checked = false;
        nearbyItemsEnabled.Checked = false;
        nearbyPotsEnabled.Checked = false;
        nearbyFoodEnabled.Checked = false;
        nearbyDirectEnabled.Checked = false;
        combatEvadeEnabled.Checked = false;
        combatAttackEnabled.Checked = false;
        combatArtifactsEnabled.Checked = false;
        combatNativeEnabled.Checked = false;
        combatEncounterEnabled.Checked = false;
        combatRange.Value = 220;
        SetLanguage(0);
    }
}

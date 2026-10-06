// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 中文维护说明：分离收集回执观察与战斗调度，避免等待拾取结果时阻塞攻击。收集通道一次只允许一个未完成请求；取消信号发出后仍要等待拥有者清理，不能提前复用通道。
// 源码导航和常改参数见 docs/maintenance.md；协议、反射标识及类型白名单修改前先核对两端。
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// 收集请求完成前始终拥有其回执；等待独立组件不能占用键盘/原生战斗通道。
// 独立收集回执观察通道；等待结果不阻塞战斗执行，未清理前禁止第二个请求。
sealed class NearbyObservationLane
{
    CancellationTokenSource current;
    // 当前观察任务尚未完成清理；取消不等于立即释放通道。
    public bool Busy
    {
        get
        {
            return current != null;
        }
    }

    // 发送当前观察任务的取消信号，任务完成清理前仍保留通道归属。
    public void Cancel()
    {
        if (current != null)
            current.Cancel();
    }

    // 在独立收集观察通道运行一个任务，拒绝重入并在 finally 中归还通道。
    public async Task Run(Func<CancellationToken, Task> observe)
    {
        if (Busy)
            throw new InvalidOperationException("A collection receipt is already pending");
        var owner = new CancellationTokenSource();
        current = owner;
        try
        {
            await observe(owner.Token);
        }
        finally
        {
            current = null;
            owner.Dispose();
        }
    }
}

// CombatScheduling 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
static class CombatScheduling
{
    // 检查人工攻击/技能等按键；可选允许玩家移动，不要求鼠标静止等待。
    public static bool NativeInputReady(CombatState state, ToolboxSettings settings, Func<int, bool> held, bool allowMovement = false)
    {
        if (state == null || settings == null || held == null)
            return false;
        // 原生路径不采鼠标静止时间，也不发送鼠标输入；仍尊重人工攻击、移动和技能键。
        var keys = new[]
        {
            1,
            2,
            4,
            5,
            6,
            state.DodgeKey,
            settings.PotionKey,
            settings.AttackTrigger
        }.Concat(allowMovement ? new int[0] : new[] { 65, 68, 83, 87 }).Concat(settings.Slots).Concat(settings.ComboEnabled ? new[] { settings.ComboTrigger } : new int[0]).Concat(settings.JumpEnabled ? new[] { settings.JumpTrigger } : new int[0]);
        return !keys.Where(k => k > 0 && k < 255).Distinct().Any(held);
    }

    // 运行本模块的离线规则自检；返回 PASS 摘要，失败抛出异常供命令行报告。
    public static string Test()
    {
        return Check().GetAwaiter().GetResult();
    }

    // 离线验证输入排除、收集通道重入、取消、异常清理与复用规则。
    static async Task<string> Check()
    {
        var state = new CombatState
        {
            DodgeKey = 82
        };
        var settings = new ToolboxSettings();
        if (!NativeInputReady(state, settings, k => k == 160 || k == 20))
            throw new Exception("Root/irrelevant key blocked native melee");
        foreach (int key in new[]
        {
            1,
            2,
            87,
            82,
            settings.Slots[0],
            settings.PotionKey,
            settings.ComboTrigger,
            settings.JumpTrigger
        }

        )
            if (NativeInputReady(state, settings, k => k == key))
                throw new Exception("Native melee interferes with manual command " + key);
        if (!NativeInputReady(state, settings, k => false))
            throw new Exception("Native melee requires a quiet period");
        if (!NativeInputReady(state, settings, k => k == 87, true) || NativeInputReady(state, settings, k => k == 1, true) || NativeInputReady(state, settings, k => k == settings.Slots[1], true))
            throw new Exception("Encounter walk opt-in bypassed manual attack/ability guard");
        var lane = new NearbyObservationLane();
        var receipt = new TaskCompletionSource<bool>();
        bool cancelled = false;
        Task pending = lane.Run(token =>
        {
            token.Register(() => cancelled = true);
            return receipt.Task;
        });
        if (!lane.Busy || pending.IsCompleted)
            throw new Exception("Collection receipt ownership ended prematurely");
        bool rejected = false;
        try
        {
            await lane.Run(token => Task.FromResult(true));
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        if (!rejected)
            throw new Exception("Two collection requests can replace a pending receipt");
        lane.Cancel();
        if (!cancelled || !lane.Busy)
            throw new Exception("Stop failed to cancel or released ownership before cleanup");
        receipt.SetResult(true);
        await pending;
        if (lane.Busy)
            throw new Exception("Collection lane leaked after completion");
        try
        {
            await lane.Run(token =>
            {
                throw new Exception("simulated receipt failure");
            });
        }
        catch
        {
        }

        if (lane.Busy)
            throw new Exception("Receipt failure leaked lane");
        var interrupted = new TaskCompletionSource<bool>();
        var stopped = lane.Run(token =>
        {
            token.Register(() => interrupted.TrySetCanceled());
            return interrupted.Task;
        });
        lane.Cancel();
        try
        {
            await stopped;
            throw new Exception("Cancelled observation completed as success");
        }
        catch (OperationCanceledException)
        {
        }

        if (lane.Busy)
            throw new Exception("Cancellation leaked lane");
        await lane.Run(token => Task.FromResult(true));
        return "PASS: native input has no cursor quiet-period delay; manual attack/movement/ability commands respected. Independent collection receipt ownership, duplicate rejection, cancellation, fault cleanup and reuse; no game input.\r\n";
    }
}

// 主窗口的一个 partial 部分；事件处理与异步任务共用主窗口状态，退出时统一清理。
sealed partial class ToolboxForm
{
    readonly NearbyObservationLane nearbyObservation = new NearbyObservationLane();
    // 观察单个收集回执而不占用战斗按键通道；异常按现有规则暂停。
    async void RunNearbyObservation(InputRequest request)
    {
        if (nearbyObservation.Busy)
            return;
        try
        {
            await nearbyObservation.Run(token => RunNearbyLoot(request, token));
        }
        catch (OperationCanceledException)
        {
            ToolboxLog.Write("Loot.ObservationCancelled", L10n.Canonical(request.Description));
        }
        catch (Exception e)
        {
            ToolboxLog.Error("Loot.ObservationError", e);
            if (!closing)
                Arm(false);
        }
    // 此清理不释放战斗通道的按键，也不改变其按下/抑制状态。
    }
}

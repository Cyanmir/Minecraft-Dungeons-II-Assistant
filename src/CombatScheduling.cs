// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 分离收集回执观察与战斗调度，避免等待拾取结果时阻塞攻击。
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// 收集请求完成前始终拥有其回执；等待独立组件不能占用键盘/原生战斗通道。
// 独立收集回执观察通道；等待结果不阻塞战斗执行，未清理前禁止第二个请求。
sealed class NearbyObservationLane
{
    CancellationTokenSource current;
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

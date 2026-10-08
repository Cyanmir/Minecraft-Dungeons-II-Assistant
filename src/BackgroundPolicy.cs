// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
using System;
using System.Windows.Forms;

// 后台作用域统一入口；关闭后台开关后仍保留前台行为。
static class BackgroundPolicy
{
    // 仅决定窗口作用域；玩家、菜单、会话和暂停由各执行入口继续校验。
    public static bool Allows(bool foreground, bool enabled)
    {
        return foreground || enabled;
    }

    // 其他应用的人工键鼠操作不能阻挡游戏端的自动动作。
    public static bool Held(bool foreground, int key, Func<int, bool> read)
    {
        return foreground && read(key);
    }

    // 后台战斗强制走原生通道，绝不向其他窗口注入鼠标动作。
    public static bool NativeCombat(bool foreground, bool selected)
    {
        return selected || !foreground;
    }

}

// 运行时窗口作用域适配；原生通道不需要抢占窗口焦点。
sealed partial class ToolboxForm
{
    // 每次发送前重查游戏前台状态，避免使用轮询缓存误投递。
    bool GameForeground()
    {
        return reader != null && ToolboxInput.Front(reader.Pid);
    }

    // 后台总开关覆盖自动功能和辅助快捷操作，仍受统一 F9 停止控制。
    bool GameScope()
    {
        return reader != null && BackgroundPolicy.Allows(GameForeground(), settings.BackgroundAuto);
    }

    // 原生输入竞争检查只考虑游戏前台的人工操作。
    bool GameInputHeld(int key)
    {
        return BackgroundPolicy.Held(GameForeground(), key, ToolboxInput.Held);
    }

    // 后台自动采用组件；前台保留用户选择的兼容按键模式。
    bool UseNativeCombat
    {
        get
        {
            return BackgroundPolicy.NativeCombat(GameForeground(), settings.CombatNative);
        }
    }

    // 修改工具设置时不触发组合、跳劈或右键操作；其他应用内快捷键仍可触发后台游戏动作。
    bool ShortcutScope()
    {
        // 鼠标监听有独立消息线程，不能用线程局部的 ActiveForm 判定工具是否在前台。
        // 直接核对窗口所属 PID，也覆盖本工具的详情、设置及对话框。
        return GameScope() && !ToolboxInput.Front(ToolboxInput.ToolProcessId) && !ToolboxInput.Modifiers();
    }
}

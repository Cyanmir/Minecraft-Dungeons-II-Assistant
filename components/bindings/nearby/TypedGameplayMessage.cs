// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// NeoRune 编译期绑定：将原生玩法消息的 wildcard payload 声明为已核实食物消息类型。
using NeoRune;
using UE.GameplayMessageRuntime;
using UE.SpicewoodGAS;

namespace MCD2NativeBindings;
// 绑定已有原生 wildcard 读取函数及真实 payload 结构/大小；只接收消息，不广播或自行应用效果。
[UClass("/Script/GameplayMessageRuntime.AsyncAction_ListenForGameplayMessage")]
public class UTypedGameplayMessageListener : UAsyncAction_ListenForGameplayMessage
{
    // 声明已有原生消息 payload 读取入口，输出经核实的食物消息结构。
    [UFunction("GetPayload", 71435265u, "OutPayload:struct(/Script/SpicewoodGAS.ConsumableUsedPlayerMessage,240):18001048000380;" + "ReturnValue:bool:18001040000780")]
    public bool GetFoodPayload(out FConsumableUsedPlayerMessage payload)
    {
        payload = default;
        return false;
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 铁匠 reducer 注册的完成消息使用 FActionActor，SDK 给出的原生大小为 8 字节。
// 载荷只有 Actor，没有装备 UID 或工具请求 ID；不可把任意回调当成自动刷新完成。
using NeoRune;
using UE.GameplayMessageRuntime;
using UE.SWCorePayloads;

namespace MCD2RerollNativeBindings;
[UClass("/Script/GameplayMessageRuntime.AsyncAction_ListenForGameplayMessage")]
public class UTypedRerollMessage : UAsyncAction_ListenForGameplayMessage
{
    [UFunction("GetPayload", 71435265u,
        "OutPayload:struct(/Script/SWCorePayloads.ActionActor,8):18001048000380;" +
        "ReturnValue:bool:18001040000780")]
    public bool ReadActor(out FActionActor payload)
    {
        // NeoRune 据属性输出已有原生 custom thunk；此 C# 方法体不作为游戏执行逻辑。
        payload = default;
        return false;
    }
}

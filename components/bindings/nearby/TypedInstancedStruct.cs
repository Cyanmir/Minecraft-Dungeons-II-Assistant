// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// NeoRune 编译期绑定：读取 InstancedStruct 中的原生交互重定向数据。不能凭猜测换成另一结构类型；绑定只描述已有原生 thunk，不注入新的 C# 游戏运行时。
using NeoRune;
using UE.CoreUObject;
using UE.Engine;
using UE.SWCoreGameplay;

namespace MCD2NativeBindings;
// 编译期绑定，NeoRune 输出已有原生 custom thunk；wildcard 输出要求真实 FStructProperty 而非 SDK 占位 int，本文件不在游戏运行 C# 或改状态。
[UClass("/Script/Engine.BlueprintInstancedStructLibrary")]
public class UTypedInstancedStructLibrary : UObject
{
    // 声明原生 InstancedStruct 读取入口，输出真实交互重定向结构。
    [UFunction("GetInstancedStructValue", 71443459u, "ExecResult:enum(/Script/Engine.EStructUtilsResult):18001040000380;" + "InstancedStruct:struct(/Script/CoreUObject.InstancedStruct,16):10000008000182;" + "Value:struct(/Script/SWCoreGameplay.InteractionRedirection,8):10000000000380")]
    public static void GetInteractionRedirection(out EStructUtilsResult result, FInstancedStruct source, out FInteractionRedirection value)
    {
        result = default;
        value = default;
    }
}

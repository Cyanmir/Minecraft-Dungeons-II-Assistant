// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// SDK 0.3.1 把 wildcard 输出写成 int；这里按 SDK 的原生结构属性声明真实类型。
// 只读 GetInstancedStructValue，不能用 SetInstancedStructValue 写入玩家或商人状态。
using NeoRune;
using UE.CoreUObject;
using UE.Engine;
using UE.Angelscript;

namespace MCD2RerollNativeBindings;
[UClass("/Script/Engine.BlueprintInstancedStructLibrary")]
public class UTypedUiState : UObject
{
    // 8304 字节和结构路径来自当前 SDK 的 UStructAttribute；游戏更新后必须重新核对。
    [UFunction("GetInstancedStructValue", 71443459u,
        "ExecResult:enum(/Script/Engine.EStructUtilsResult):18001040000380;" +
        "InstancedStruct:struct(/Script/CoreUObject.InstancedStruct,16):10000008000182;" +
        "Value:struct(/Script/Angelscript.AS_OverallUIState,8304):18001040000380")]
    public static void Read(out EStructUtilsResult result, FInstancedStruct source, out FAS_OverallUIState value)
    {
        // 与其他绑定一样，这个方法体只服务于 C# 编译；NeoRune 输出原生 custom thunk。
        result = default;
        value = default;
    }
}

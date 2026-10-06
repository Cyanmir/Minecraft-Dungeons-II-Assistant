// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// NeoRune 编译期绑定：调用原生数组比较 thunk 比较完整预测键。
using System.Collections.Generic;
using NeoRune;
using UE.CoreUObject;
using UE.GameplayAbilities;

namespace MCD2CombatNativeBindings;
// 编译期类型绑定只调用现有原生 wildcard thunk，不添加 C# 运行时/游戏补丁；比较完整预测键，不截断 Int16 字段。
[UClass("/Script/Engine.KismetArrayLibrary")]
public class UPredictionArrayLibrary : UObject
{
    // 声明已有原生 wildcard 数组比较入口，用完整预测键结构判定同一激活。
    [UFunction("Array_Identical", 339878915u, "ArrayA:array(struct(/Script/GameplayAbilities.PredictionKey,16)):10000008000382;" + "ArrayB:array(struct(/Script/GameplayAbilities.PredictionKey,16)):10000008000382;" + "ReturnValue:bool:18001040000780")]
    public static bool SameKeys(List<FPredictionKey> a, List<FPredictionKey> b)
    {
        return false;
    }
}

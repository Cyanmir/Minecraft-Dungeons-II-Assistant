// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 中文维护说明：NeoRune 编译期绑定：调用原生数组比较 thunk 比较完整预测键。绑定路径、函数名与形参对应真实反射声明；本文件不在游戏里运行 C#，不能把声明误当作自行实现的游戏逻辑。
// 源码导航和常改参数见 docs/maintenance.md；协议、反射标识及类型白名单修改前先核对两端。
using System.Collections.Generic;
using NeoRune;
using UE.CoreUObject;
using UE.GameplayAbilities;

namespace MCD2CombatNativeBindings;
// 编译期类型绑定只调用现有原生 wildcard thunk，不添加 C# 运行时/游戏补丁；比较完整预测键，不截断 Int16 字段。
// UPredictionArrayLibrary 封装本文件的一组职责；修改调用顺序时同时检查初始化、失败和释放路径。
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

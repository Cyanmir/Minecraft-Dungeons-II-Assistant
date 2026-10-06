# 构建 MCD2A

应用版本：**1.0.0**。组件通信协议与依赖版本独立维护，不作为应用版本号。

人工修改前可查阅 [中文源码维护导航](maintenance.md)，定位设置、交互、战斗、装备保护及翻译入口。

## 工具端

在 Windows x64 上，从仓库根目录运行：

```powershell
& .\build.ps1
```

脚本调用 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`，输出 `dist/MCD2A.exe`。字体、项目图标、组件与许可说明内嵌到程序，不依赖本机私有路径或原开发环境。

指定输出位置：

```powershell
& .\build.ps1 -Output (Join-Path $PWD 'dist/MCD2A.exe')
```

游戏拆包展示资源不属于公共构建。可选本地资源放在 EXE 同级目录；也可用 `-EquipmentPresentationPath` 在个人构建中嵌入，但不要提交或分发该本地资源。

## 收集与战斗组件

重建需要 **.NET 10 SDK** 和 **NeoRune SDK 0.3.1**。SDK 通过 NuGet 解析，仓库不存放 SDK、游戏程序集或开发运行环境。

```powershell
dotnet build .\components\nearby\MCD2NearbyLootBridge.csproj
dotnet build .\components\combat\MCD2CombatBridge.csproj
```

构建输出位于组件目录的 `bin/NeoRune/Pak`。更新时，将三个 `MCD2NearbyLootBridge_P` 文件复制到 `src/assets/nearby-component`，将三个 `MCD2CombatBridge_P` 文件复制到 `src/assets/combat-component`，再构建工具。

`components/bindings` 只声明现有原生反射函数的类型信息，供 NeoRune 编译使用，不在游戏中运行 C#。

**装备回收组件沿用已验证的预编译文件。** 本仓库没有它的完整可重建源码，不从历史 stub 重建或替换。

## 自检

构建后可对独立临时输出目录运行：

```powershell
& .\dist\MCD2A.exe --selftest "$env:TEMP\MCD2A-selftest.txt"
& .\dist\MCD2A.exe --interaction-policy-selftest "$env:TEMP\MCD2A-policy.txt"
& .\dist\MCD2A.exe --native-transport-selftest "$env:TEMP\MCD2A-transport.txt"
```

这些离线自检不等于游戏实测。程序中的自检实现属于源码，生成的报告、合成存储目录、截图及诊断环境都不提交。

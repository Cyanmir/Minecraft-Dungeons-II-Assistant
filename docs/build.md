# 构建

## 工具端

环境：Windows x64，自带的 .NET Framework C# 编译器。

```powershell
.\build.ps1
```

输出 `dist/MCD2A.exe`。指定输出路径：

```powershell
.\build.ps1 -Output (Join-Path $PWD 'dist/MCD2A.exe')
```

源码位于 `src`，编译文件和内嵌资源清单位于 `src/build.ps1`。字体、项目图标、组件和许可内嵌到 EXE。游戏图标和译名从独立资源仓库下载；本地展示包可通过 `-EquipmentPresentationPath` 嵌入。

## 游戏组件

需要 .NET 10 SDK 和 NeoRune SDK 0.3.1。

```powershell
dotnet build .\components\nearby\MCD2NearbyLootBridge.csproj
dotnet build .\components\combat\MCD2CombatBridge.csproj
dotnet build .\components\reroll\MCD2RerollBridge.csproj
```

生成的 `.pak`、`.utoc`、`.ucas` 位于各组件的 `bin/NeoRune/Pak`。更新后复制到 `src/assets` 中对应的组件目录，再构建工具。组件构建不自动安装到游戏。

`components/bindings` 声明 NeoRune 编译需要的原生反射信息。收集协议 6、战斗协议 5、铁匠协议 7，与应用版本分别维护。

装备回收沿用 `src/assets/equipment-component` 的预编译文件。本仓库不提供它的完整构建源码。

## 效果索引

`tools` 中的生成器从对应游戏版本的本地数据表导出分类和数字等级索引：

```powershell
python tools/build-reroll-categories.py DT_EffectTemplateDefinition.json src/assets/reroll-display-categories.json --game-build 1.1.2.0
python tools/build-reroll-levels.py DT_EffectTemplateDefinition.json src/assets/reroll-effect-levels.json --game-build 1.1.2.0
```

分类根据模板 Traits，数字等级根据普通模板的 Tier 引用。生成结果保留来源哈希；输入表不提交到仓库。

## 发行包

发行包包含 `MCD2A.exe`、README、两份项目许可、第三方声明和 `docs` 中的使用与构建说明。构建输出、SDK、个人资源、配置和日志由 `.gitignore` 排除。

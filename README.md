<div align="center">

<img src="src/assets/toolbox.png" alt="MCD2A" width="96" />

# Minecraft Dungeons II Assistant

**MCD2A · Minecraft Dungeons II 游戏助手**
 
自动恢复 · 附近交互 · 原生战斗 · 装备整理

[![Version](https://img.shields.io/badge/version-1.0.0-4bb5c5)](https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant/releases/tag/1.0.0)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078d4)](#平台支持)
[![License](https://img.shields.io/badge/license-MIT-6b8e9e)](LICENSE)

[下载](https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant/releases/latest) · [使用说明](docs/usage.md) · [问题反馈](https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant/issues) · [参与开发](#参与开发)

</div>

## 下载与安装

前往 [Releases](https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant/releases/latest)，下载 **MCD2A-1.0.0-win-x64.zip**，解压后运行 `MCD2A.exe`。

1. 启动游戏，进入关卡后点击 **连接游戏**。
2. 按自己的玩法开启需要的功能，配置槽位、范围与交互间隔。
3. **F8** 开始 / 暂停，**F9** 立即停止。

程序内置界面字体、图标与许可说明。原生交互、原生战斗和装备回收需要游戏端组件；可在界面安装，工具会识别游戏目录并检查 Blueprint Loader。安装或更新前，请保存进度并完全退出游戏。

## 亮点功能

| 功能 | 说明 |
| --- | --- |
| 自动恢复 | 低血量时使用已选法器，判断冷却与灵魂；支持药水备用及仅药水模式 |
| 法器组合 | 一个触发键使用选中的多个槽位，与自动恢复选择独立保存 |
| 附近原生交互 | 交互附近已有宝箱、装备、附魔书、食物、TNT，以及小型 / 大型绿宝石罐 |
| 快速交互 | 通用与食物间隔分别设置，支持滑块和数字输入；允许手动跑动时交互 |
| 自动战斗 | 遇敌主动近战、攻击预警、提前使用法器与实验性闪避；支持已适配的蓄力、引导和瞄准法器 |
| 装备整理 | 按类别、品质与保护规则预览待整理清单，使用独立组件执行原生回收 |
| 操作辅助 | 跳劈辅助与右键定向闪避；保留 Shift + 右键的远程攻击操作 |
| 目录与依赖识别 | 自动发现游戏安装目录，缺少加载器时接入作者官网下载与安装流程 |
| 日志反馈 | 导出当前配置及最近动作记录，方便排查连接、触发与组件通信问题 |

附近原生交互无需手动选中目标，不移动鼠标，也不自动走向目标。食物走原生食用逻辑，绿宝石罐走原生攻击 / 破坏逻辑，掉落绿宝石由游戏自身拾取。

界面支持简体中文、English、日本語、한국어、繁體中文（香港）和繁體中文（台灣）。

## 平台支持

| 平台 | 当前范围 |
| --- | --- |
| Steam / Win64 | 全部适配 |
| Xbox PC / Microsoft Store / WinGDK | 待测试 |

GDK 实验配置仅接受已采集的 `Microsoft.MinecraftDungeons2_1.1.1.0_x64__8wekyb3d8bbwe` 包及匹配的运行时结构。包版本不等于游戏 EXE 的文件版本或 SHA256。GDK 自动出售尚未验证。

当前收集组件协议为 **6**，战斗组件协议为 **4**。从旧版升级时，需要同时更新两个组件并重启游戏；通信握手通过只代表通道可用，不能当作拾取、回血、伤害或掉落已成功。

## 使用说明

### 原生组件

在 **自动战斗** 页面找到 **安装 / 更新直接收集组件** 与 **安装 / 更新原生战斗组件**。安装器检查游戏进程、加载器和旧文件归属，并备份被替换的组件。

Blueprint Loader 来自 [ewanhowell5195 的 Nexus Mods 页面](https://www.nexusmods.com/minecraftdungeons2/mods/2)。加载器不随本项目分发；首次官网登录与下载确认由用户完成。完整下载后可由工具接管安装，也可选择已有 ZIP。

### 常用设置

- **附近原生交互**：开启目标类型，再开启原生交互。仅处理原生交互范围内的目标。
- **交互间隔**：通用默认 500 ms，食物默认 1000 ms，范围 100–30000 ms；实际频率受游戏能力和通信响应影响。
- **遇到敌人自动攻击**：需要原生战斗模式，只攻击附近敌人，不自动追赶远处目标。
- **攻击预警**：位于自动战斗页；药水继续受血量阈值限制。
- **自动恢复**：法器槽位全部取消勾选时，只使用已就绪的药水。

更多配置、手柄支持与升级步骤见 [使用说明](docs/usage.md)。

## 问题反馈

遇到异常时，点击 **导出日志**，在 [Issues](https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant/issues) 中说明平台、游戏版本、启用功能、预期行为及复现步骤。日志由用户主动导出，不自动上传。

游戏更新后，未经验证的版本、结构、组件协议或过期请求会被拒绝执行。

## 参与开发

欢迎提交问题反馈、翻译和代码改进。界面与文档以简体中文为主要语言。

在 Windows x64 上构建工具：

```powershell
& .\build.ps1
```

生成 `dist/MCD2A.exe`，使用 Windows 自带的 .NET Framework C# 编译器。工具端源码位于 `src`，收集 / 战斗组件源码位于 `components`；组件重建需 .NET 10 与 NeoRune SDK 0.3.1，详见 [构建说明](docs/build.md)。

源码已补充中文模块、类型、方法及关键参数注释，便于人工修改；常见改动位置、调用流程和单位/协议约束见 [中文源码维护导航](docs/maintenance.md)。

仓库只包含产品源码、必要资源及文档。游戏本体、SDK 运行环境、个人配置、存档、诊断报告、截图和历史构建包均不进入仓库。装备原版图标包可在本地独立加载，不随公开发行包分发。装备回收沿用已验证的预编译组件。

## Contributors

感谢参与开发与反馈的贡献者：

| 贡献者 | 参与方式 |
| --- | --- |
| [Cyanmir](https://github.com/Cyanmir) | 项目维护、功能规划与实机验证 |
| [3035936740](https://github.com/3035936740) | 测试与反馈支持 |
| [OpenAI Codex](https://openai.com/codex/) | AI 辅助开发、排查与文档整理 |

## 致谢与许可

项目原创代码、图标与文档采用 [MIT License](LICENSE)，中文说明见 [LICENSE.zh-CN.md](LICENSE.zh-CN.md)。字体、NeoRune 及其他第三方资源保留各自许可，见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

MCD2A 是社区项目，与 Mojang、Microsoft 或 Minecraft 官方没有隶属关系。

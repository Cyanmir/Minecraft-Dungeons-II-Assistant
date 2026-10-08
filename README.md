<div align="center">

<img src="src/assets/toolbox.png" alt="MCD2A" width="96" />

# Minecraft Dungeons II Assistant

**MCD2A · Minecraft Dungeons II 游戏助手 · 1.2.0**

[![Version](https://img.shields.io/badge/version-1.2.0-4bb5c5)](https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant/releases/tag/1.2.0)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078d4)](#系统要求)
[![License](https://img.shields.io/badge/license-MIT-6b8e9e)](LICENSE)

[下载](https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant/releases) · [使用说明](docs/usage.md) · [构建](docs/build.md) · [反馈](https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant/issues)

</div>

## 系统要求

Windows x64，支持 Steam / Win64 和 Xbox PC / Microsoft Store / WinGDK。
界面支持简体中文、English、日本語、한국어、繁體中文（香港）和繁體中文（台灣）。

## 开始使用

从 Releases 下载 Windows x64 发行包，解压后运行 `MCD2A.exe`。

1. 启动游戏并进入关卡，点击 **连接游戏**。
2. 在设置页安装所需组件，开启功能并配置键位。
3. **F8** 开始或暂停，**F9** 停止所有功能。

安装或更新游戏组件前，保存进度并退出游戏。组件依赖 [Blueprint Loader](https://www.nexusmods.com/minecraftdungeons2/mods/2)，首次安装按程序提示下载。

## 功能

| 功能 | 用途 |
| --- | --- |
| 自动恢复 | 低血量时使用法器或药水，检查冷却、充能与灵魂 |
| 附近交互 | 拾取装备、附魔书和 TNT，开箱、食用和破罐；可设置交互间隔 |
| 自动战斗 | 攻击附近敌人，提前使用法器，辅助闪避 |
| 装备整理（实验） | 按保护规则整理装备，保留锁定、收藏和受保护物品 |
| 自动刷词条 | 为每件装备设置效果和等级，按队列依次刷新，限制次数与花费 |
| 操作辅助 | 一键法器组合、跳劈辅助、右键定向闪避 |

## 构建

在 Windows x64 上运行：

```powershell
.\build.ps1
```

生成 `dist/MCD2A.exe`。组件构建和资源索引生成见 [构建说明](docs/build.md)。

## 反馈与许可

遇到异常可在程序中导出日志，并在 Issues 提供游戏版本、启用功能和复现步骤。日志不会自动上传。

项目代码和原创图标采用 [MIT License](LICENSE)，[中文许可说明](LICENSE.zh-CN.md)。第三方字体、组件依赖和游戏资源的许可见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

## Contributors

| 贡献者 | 参与方式 |
| --- | --- |
| [Cyanmir](https://github.com/Cyanmir) | 项目维护与功能开发 |
| [3035936740](https://github.com/3035936740) | 测试与反馈 |
| [Jackdwh](https://github.com/Jackdwh) | Xbox PC / Microsoft Store / WinGDK 平台测试与反馈 |
| [OpenAI Codex](https://openai.com/codex/) | AI 辅助开发、排查与文档整理 |

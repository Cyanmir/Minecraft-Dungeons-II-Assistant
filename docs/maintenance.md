# 中文源码维护导航

C# 文件头说明模块职责，类型与方法前有中文说明，关键配置字段注明单位和联动位置。Windows/Unreal 函数名、协议标识及许可证标识保留原文，便于与原生定义核对。

## 常见修改入口

| 想调整的内容 | 主要文件与入口 | 同步核对 |
| --- | --- | --- |
| 默认开关、低血量阈值、槽位键、语言 | `src/ToolboxWindow.cs` → `ToolboxSettings` | `Parse` 限幅、`Save` 键名、`ReadSettings` 和控件初值；已有配置不会因默认值修改自动重置 |
| 恢复与药水备用 | `ToolboxWindow.cs` → `HealRule`、`RecoveryRule`、`Poll` | `AutoHeal.cs` 冷却/充能/灵魂数据、人工输入冲突及仅药水模式 |
| 战斗开关与布局 | `AutomationUi.cs`、`CombatActions.cs` | `ReadCombatSettings`、翻译字典和页面滚动范围 |
| 遇敌反应和调度 | `CombatActions.cs` → `PollCombat`、`CombatScheduling.cs` | 恢复优先、人工按键、独立收集观察通道；取消后等待清理 |
| 原生近战/法器/闪避 | `CombatBridge.cs`、`components/combat/ModActor.cs` | 协议、场景、目标、槽位 UID、真实预测键、蓄力/引导释放与超时 |
| 交互间隔与滑块 | `NearbyLoot.cs` → `NearbyLootTiming`、`NearbyIntervalScale`、`NearbyTimingChanged` | 配置解析、数字输入、组件 `intervalMs`/`foodIntervalMs`；单位为毫秒 |
| 新交互类型与结果 | `NearbyLoot.cs`、`NearbyLootBridge.cs`、`components/nearby/ModActor.cs` | 识别、请求、原生入口和结果证据全部适配，不能仅放宽白名单 |
| 回收保护与阈值 | `EquipmentPolicy.cs` → `Decode`、`Plan` | 七类索引、品质枚举、基线 UID、锁定/附魔/来源保护和回执计数 |
| 装备展示 | `EquipmentGamePresentation.cs`、`EquipmentVisualization.cs` | 不改变身份或回收规则；公开构建不含拆包图像 |
| 目录与组件安装 | `GameInstallLocator.cs`、`BlueprintLoaderInstall.cs`、各 `*BridgeInstall.cs` | 游戏已退出、真实目录、下载包完整性、三件套哈希与备份 |
| 版本/反射适配 | `GameLocator.cs`、`AutoHeal.cs`、相关 `HealthReader` partial | 字段类型/尺寸、唯一特征候选、对象身份；不要盲用固定地址 |
| Xbox/GDK 通信 | `NativeBridgeChannel.cs`、`GdkBridgeTransport.cs`、`GdkStorageAudit.cs` | 已验证包/结构、私有容器关联、描述符轮换、握手及 OFF 恢复 |
| 自绘控件与数字输入 | `OreUi.cs` | 像素尺寸、共用限幅、重绘、语言字体及释放 |
| 翻译 | `Localization.cs`、`AutomationLocalization.cs` | 六语言索引、规范中文键与 `Canonical` 回查；不要翻译原生标签 |
| 日志 | `Diagnostics.cs`、`AdaptationRecord.cs` | 脱敏、节流和锁；日志只由用户主动导出 |
| 版本与编译资源 | `ToolboxAssembly.cs`、`src/build.ps1` | README、资源名、许可清单和下载包；版本与协议分别维护 |

## 动作调用流程

1. 设置层提供开关与参数，`HealthReader` 的各 partial 模块读取玩家、对象、距离、冷却和状态。
2. 主窗口 `Poll` 及各功能 `Poll*` 检查资格、人工输入、频率和通道。执行前再次核对可能过期的排队快照。
3. 原生收集/战斗通过各自独立请求槽调用游戏组件；收集回执等待不占用战斗输入通道。
4. 游戏组件调用已有且已授予的原生能力，再观察结果。通信成功、能力激活与最终结果分别处理。
5. F9、停止、换场景、取消或异常统一释放本工具拥有的输入/任务，并恢复请求 OFF。

“允许移动时交互”只指玩家手动跑动，原生路径不移动鼠标或自动追赶目标。`CombatActions.cs` 保留的旧鼠标路径不能接回原生交互。`NavigationAudit.cs` 的路径算法只用于诊断/地面检查，当前不自动寻路。

## 单位和标识

- `*Ms` 与交互间隔为毫秒；组件 `World.RealTime`、能力保持时间及动画攻击窗口为秒。
- 世界坐标、交互范围和碰撞半径为游戏世界单位；控件尺寸和字体宽高为像素。
- 槽位/语言/类别数组从零索引；Windows 键码 `0x45` 是 E，而 `1` 为鼠标左键。
- 收集协议 **6**、战斗协议 **4**、固定双份 ASCII 槽容量 **2048** 字符；应用 **1.0.0**、SDK **0.3.1** 与这些通信数字独立。
- `Request`/`Receipt` 类、字段、槽名、GameplayTag 和完整 Actor 名是通信/反射标识；改名需同步两端和嵌入资源。
- `Instance`、`Epoch`、`Sequence` 同时匹配，防止旧回执误算。对象地址可能复用，还需类、索引、序列和会话。
- 装备使用 `SessionUID`。食物 UID 为零时必须额外核对本地玩家、物品上下文和原生效果，不能把零当唯一身份。

## JSON 目录的说明

严格 JSON 不支持 `//`，插入行内注释会破坏运行时解析；字段说明放在这里，读取逻辑有中文注释。

| 文件 | 维护约束 |
| --- | --- |
| `src/assets/combat-catalog.json` | `Format` 约束目录版本，`GameSha256` 绑定游戏；`Profiles` 为敌人/Actor/蒙太奇关联，`Montages` 窗口 `Start/End` 为动画秒数，`FullyParsed` 区分解析证据 |
| `src/assets/loot-catalog.json` | 已核实目标目录；遵守消费者的格式和白名单校验，新条目不等于原生执行已适配 |
| `src/assets/native-combat-artifacts.json` | 已适配法器类型/生命周期；新增前核对蓄力、引导、瞄准、费用及释放 |

第三方字体、许可原文及预编译组件保留原文件。装备出售组件没有完整可重建源码，继续使用已验证三件套。

## 验证与构建

按 [构建说明](build.md) 编译。改组件逻辑时单独编译并更新对应三件套，再构建工具；纯注释/排版改动不需要更新游戏安装组件。

按修改范围运行已有离线自检。改游戏入口、识别或结果条件后仍需实机验证，编译/握手通过不等于拾取、回血或伤害成功。测试报告、截图、SDK 和私有拆包环境不提交。

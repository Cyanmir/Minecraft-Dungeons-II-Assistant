// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 主界面翻译及中文规范键映射。
using System;
using System.Collections.Generic;

// 六语言规范键与显示字典；翻译是 UI 层转换，不能改动原生类型/协议标识。
static partial class L10n
{
    public static int Language;
    public static bool English
    {
        get
        {
            return Language == 1;
        }

        set
        {
            Language = value ? 1 : 0;
        }
    }

    public static bool Cjk
    {
        get
        {
            return Language == 2 || Language == 3;
        }
    }

    static readonly Dictionary<string, string> En = new Dictionary<string, string>
    {
        {
            "绿宝石 {0}：需进入游戏原生拾取范围",
            "Emeralds {0}: move into the game's pickup range"
        },
        {
            "检测到多个游戏进程，请只保留一个游戏实例后连接。",
            "Multiple game processes detected. Keep one game instance open, then connect."
        },
        {
            "攻击预警（实验）",
            "Attack warning (experimental)"
        },
        {
            "攻击动作或直线弹道接近时，提前使用已选法器",
            "Incoming attack or arrow: use selected artifacts early"
        },
        {
            "药水仍受血量阈值限制；仅在游戏前台生效",
            "Potions require low health; game must be in foreground"
        },
        {
            "等待攻击预警数据",
            "Waiting for threat data"
        },
        {
            "附近敌人 {0} · 弹道 {1} · 危险 {2}",
            "Enemies {0} · Projectiles {1} · Threats {2}"
        },
        {
            "预警读数不可用，保持原有血量规则",
            "Threat data unavailable; health rules continue"
        },
        {
            "攻击预警自动使用",
            "Threat > use artifacts/potion"
        },
        {
            "游戏映像验证失败。",
            "Game image validation failed."
        },
        {
            "游戏映像区段无效。",
            "Invalid game image sections."
        },
        {
            "找不到经过验证的游戏名称表，需要更新适配。",
            "Verified game name table unavailable. Update required."
        },
        {
            "找不到经过验证的游戏对象表，需要更新适配。",
            "Verified game object table unavailable. Update required."
        },
        {
            "使用法器（不勾选时仅使用药水）",
            "Use artifacts (none selected: potion only)"
        },
        {
            "未选择法器，仅按血量与药水冷却自动使用药水。",
            "Potion only: health + potion cooldown"
        },
        {
            "低血量 → 药水回血",
            "Low health > use potion"
        },
        {
            "许可与致谢",
            "Licenses & Credits"
        },
        {
            "关闭",
            "Close"
        },
        {
            "导出日志",
            "Export Logs"
        },
        {
            "导出诊断日志",
            "Export diagnostic log"
        },
        {
            "文本文件 (*.txt)|*.txt",
            "Text files (*.txt)|*.txt"
        },
        {
            "日志已导出，可附在群内或 GitHub Issues 反馈中。",
            "Log exported. Attach it to your group or GitHub Issues report."
        },
        {
            "导出失败，请选择其他保存位置。\n{0}",
            "Export failed. Choose another location.\n{0}"
        },
        {
            "发生错误，已停止。请导出日志反馈。",
            "An error occurred. Stopped. Export the log to report it."
        },
        {
            "游戏手柄键位未读取，请检查游戏内绑定。",
            "Gamepad bindings unavailable; check your in-game bindings."
        },
        {
            "游戏键位与工具箱触发键冲突，请更换触发键。",
            "The game binding conflicts with a toolbox trigger. Change the trigger."
        },
        {
            "跟随游戏键位：{0} → {1}\n使用对应的键盘操作；手柄按钮请在游戏内修改。",
            "Game binding: {0} → {1}\nUses the matching keyboard action. Change gamepad bindings in game."
        },
        {
            "键盘 / 鼠标",
            "Keyboard / Mouse"
        },
        {
            "键盘快捷键保持 F8 / F9；可选择 Xbox 绑定手柄",
            "Keyboard shortcuts stay F8 / F9; select Xbox to bind a gamepad"
        },
        {
            "请按下要绑定的键；鼠标绑定请点击此提示区。Esc 取消",
            "Press a key; click this area for a mouse binding. Esc cancels"
        },
        {
            "清除手柄绑定",
            "Clear gamepad binding"
        },
        {
            "鼠标右键",
            "Right Mouse"
        },
        {
            "手柄",
            "Gamepad"
        },
        {
            "Xbox 手柄绑定",
            "Xbox Gamepad Bindings"
        },
        {
            "用手柄按钮或组合触发工具箱功能",
            "Use gamepad buttons or chords to activate features"
        },
        {
            "启用手柄触发",
            "Enable gamepad input"
        },
        {
            "自动识别键鼠与手柄；默认 Xbox 布局",
            "Detect keyboard, mouse and gamepad; Xbox labels"
        },
        {
            "跳跃检测按钮",
            "Jump detection button"
        },
        {
            "攻击检测按钮",
            "Attack detection button"
        },
        {
            "等待 Xbox 手柄连接",
            "Waiting for an Xbox gamepad"
        },
        {
            "Xbox 手柄已连接",
            "Xbox gamepad connected"
        },
        {
            "跳劈：先按跳跃，再按攻击，或同时按下",
            "Jump assist: jump, then attack, or press together"
        },
        {
            "按钮保留游戏原操作；请使用未占用的组合",
            "Game actions stay active; choose unused chords"
        },
        {
            "设置手柄按钮",
            "Bind Gamepad Buttons"
        },
        {
            "先松开按钮，再按下要绑定的按钮或组合，松开后确认。",
            "Release buttons, press a button or chord, then release to confirm"
        },
        {
            "清除绑定",
            "Clear binding"
        },
        {
            "取消",
            "Cancel"
        },
        {
            "未绑定",
            "Unbound"
        },
        {
            "手柄操作请使用不同的按钮组合。",
            "Choose different, non-overlapping gamepad chords"
        },
        {
            "自动识别：Xbox 手柄",
            "Detected: Xbox gamepad"
        },
        {
            "自动识别：键盘 / 鼠标",
            "Detected: keyboard / mouse"
        },
        {
            "正在等待连接",
            "Waiting for connection"
        },
        {
            "已连接",
            "Connected"
        },
        {
            "生命：-- / --\n灵魂：-- / --",
            "Health: -- / --\nSouls: -- / --"
        },
        {
            "右键闪避",
            "RMB Dodge"
        },
        {
            "启用右键闪避",
            "Enable right-click dodge"
        },
        {
            "仅在游戏前台生效；暂停后恢复普通右键。",
            "Game focus only. Pause to restore normal right-click."
        },
        {
            "闪避键",
            "Dodge key"
        },
        {
            "与游戏内的定向闪避绑定保持一致。",
            "Match the directional dodge key in game."
        },
        {
            "远程攻击",
            "Ranged attack"
        },
        {
            "先按住 Shift，再按鼠标右键；可持续蓄力。",
            "Hold Shift before right-click. Hold to charge your shot."
        },
        {
            "右键闪避，Shift + 右键远程攻击。",
            "Right-click to dodge. Shift + right-click to shoot."
        },
        {
            "右键监听失败，请重新打开工具箱。",
            "Mouse hook failed. Restart the toolbox."
        },
        {
            "药水备用判断槽位",
            "Potion fallback slot"
        },
        {
            "只看指定槽位",
            "Use specified slot"
        },
        {
            "{0} 槽位 · 灵魂消耗：{1}",
            "Slot {0} / soul cost: {1}"
        },
        {
            "法器不可用时使用药水",
            "Use potion if artifacts are unavailable"
        },
        {
            "后台自动使用",
            "Use in background"
        },
        {
            "切出游戏继续检测和使用；组合宏、跳劈仅在前台触发。",
            "Keep monitoring and using. Macros and jump assist need game focus."
        },
        {
            "法器与药水冷却",
            "Artifact and potion cooldowns"
        },
        {
            "等待冷却数据",
            "Waiting for cooldown data"
        },
        {
            "已就绪",
            "Ready"
        },
        {
            "冷却中",
            "Cooling down"
        },
        {
            "暂不可用",
            "Unavailable"
        },
        {
            "药水",
            "Potion"
        },
        {
            "（冷却时长 {0:0.#} 秒）",
            " (cooldown duration: {0:0.#} s)"
        },
        {
            "游戏窗口已变化，等待重新连接。",
            "Game window changed. Waiting to reconnect."
        },
        {
            "后台按键发送失败，请确认运行权限一致。",
            "Background input failed. Check matching permissions."
        },
        {
            "连接后自动开始并持续检测；进菜单前按 F9 停止。",
            "Connect to start continuous monitoring. Press F9 before menus."
        },
        {
            "已开启，持续检测血量与冷却。进菜单前按 F9 停止。",
            "Monitoring health and cooldowns. Press F9 before menus."
        },
        {
            "持续等待角色进入关卡，自动重新连接。",
            "Waiting for a level. Reconnecting automatically."
        },
        {
            "后台持续检测，低血量时自动使用；F9 停止。",
            "Monitoring and using in background. F9 stops."
        },
        {
            "后台持续检测，返回游戏后自动使用。",
            "Monitoring in background. Use resumes with game focus."
        },
        {
            "法器不可用 → 药水回血",
            "Artifacts unavailable > use potion"
        },
        {
            "冷却界面已变化，重新读取。",
            "Cooldown UI changed. Reading again."
        },
        {
            "自动法器",
            "Auto Use"
        },
        {
            "低血量时自动使用法器",
            "Enable automatic artifacts"
        },
        {
            "生命低于设定比例时，同时使用已选法器。",
            "Use selected artifacts together when health is low."
        },
        {
            "低于设定血量时，自动组合使用已选法器。",
            "Below the threshold, use your selected artifact combination."
        },
        {
            "使用法器",
            "Use artifacts"
        },
        {
            "按组合计算总消耗",
            "Combined soul cost"
        },
        {
            "手动组合总消耗：{0} 灵魂",
            "Manual total cost: {0} souls"
        },
        {
            "组合：{0} · 总消耗 {1} 灵魂",
            "Combination: {0} / total cost: {1} souls"
        },
        {
            "低血量法器组合",
            "Low-health artifact combination"
        },
        {
            "请勾选低血量时使用的法器。",
            "Select artifacts to use when health is low."
        },
        {
            "药水键不能与自动使用的法器键相同。",
            "Potion key must differ from the automatic artifact keys."
        },
        {
            "连接成功后自动开始；运行时点击此按钮暂停或继续。",
            "Connect to start automatically. Use the same button to pause or resume."
        },
        {
            "连接后自动开始。切出游戏暂停按键；进菜单前按 F9 停止。",
            "Connect to start. Inputs pause outside the game. Press F9 before menus."
        },
        {
            "MCD2A",
            "MCD2A"
        },
        {
            "MCD2A已在运行。",
            "MCD2A is already running."
        },
        {
            "功能设置",
            "Settings"
        },
        {
            "按你的游玩习惯配置",
            "Your play style"
        },
        {
            "自动回血",
            "Auto Heal"
        },
        {
            "法器组合",
            "Artifacts"
        },
        {
            "跳劈辅助",
            "Jump Assist"
        },
        {
            "快捷操作",
            "Hotkeys"
        },
        {
            "F8  开始 / 暂停",
            "F8  Start / Pause"
        },
        {
            "F9  立即停止",
            "F9  Stop immediately"
        },
        {
            "设置血量时自动使用法器",
            "Auto-use Artifacts"
        },
        {
            "正在连接…",
            "Connecting..."
        },
        {
            "F11 全屏 / 还原",
            "F11 Full screen"
        },
        {
            "开始 / 暂停",
            "Start/Pause"
        },
        {
            "立即停止",
            "Stop"
        },
        {
            "全屏 / 还原",
            "Full screen"
        },
        {
            "按血量比例自动使用回血法器。",
            "Use a healing artifact when health is low."
        },
        {
            "自动使用回血法器",
            "Enable automatic healing"
        },
        {
            "生命低于设定比例时，使用指定法器。",
            "Use the selected artifact below your health threshold."
        },
        {
            "血量阈值",
            "Health threshold"
        },
        {
            "低于此百分比时触发",
            "Trigger below this percentage"
        },
        {
            "回血法器",
            "Healing artifact"
        },
        {
            "请将回血法器装备到自定义键对应槽位。",
            "Equip healing artifact in the selected key's slot."
        },
        {
            "1 槽位",
            "Slot 1"
        },
        {
            "2 槽位",
            "Slot 2"
        },
        {
            "3 槽位",
            "Slot 3"
        },
        {
            "自定义键",
            "Custom key"
        },
        {
            "灵魂不足时使用药水",
            "Use potion if souls are insufficient"
        },
        {
            "法器：等待连接 · 消耗：等待读取",
            "Artifact: waiting for connection / soul cost"
        },
        {
            "自动读取灵魂消耗",
            "Read soul cost automatically"
        },
        {
            "跟随所选槽位",
            "Use selected slot"
        },
        {
            "重试间隔",
            "Retry interval"
        },
        {
            "法器和药水共用此尝试间隔",
            "Shared artifact and potion retry interval"
        },
        {
            "秒",
            "s"
        },
        {
            "空格",
            "Space"
        },
        {
            "同时释放多个法器",
            "Use Multiple Artifacts"
        },
        {
            "勾选槽位，使用一个触发键组合释放。",
            "Select slots and activate them with one key."
        },
        {
            "组合使用你选中的法器槽位。",
            "Use the selected artifact slots together."
        },
        {
            "法器组合宏",
            "Artifact macro"
        },
        {
            "为一个按键分配组合，按下时使用已选法器。",
            "Bind a key to activate your selected artifacts."
        },
        {
            "触发键",
            "Trigger key"
        },
        {
            "选择要同时使用的法器",
            "Select artifacts to use together"
        },
        {
            "自定义法器键",
            "Extra artifact key"
        },
        {
            "启用跳劈辅助",
            "Enable jump attack assistance"
        },
        {
            "检测跳跃和左键操作，再补按指定按键。",
            "Detect jump + left click, then send the follow-up key."
        },
        {
            "跳跃 + 鼠标左键 → 自动补按 Q。",
            "Jump + left click → send the follow-up key."
        },
        {
            "检测组合 → 自动补按",
            "Input combination → follow-up key"
        },
        {
            "鼠标左键",
            "Left click"
        },
        {
            "补按延迟",
            "Follow-up delay"
        },
        {
            "检测到组合操作后，再等待此时长。",
            "Wait this long after detecting the combination."
        },
        {
            "先跳跃，再点击左键，或同时按下。",
            "Jump, then left-click, or press both together."
        },
        {
            "间隔需在 0.35 秒内；单独按键不会触发。",
            "Complete both within 0.35s. Either input alone does nothing."
        },
        {
            "生命：等待连接",
            "Health: waiting for connection"
        },
        {
            "生命：等待重新连接",
            "Health: reconnect required"
        },
        {
            "连接游戏",
            "Connect"
        },
        {
            "开始（F8）",
            "Start (F8)"
        },
        {
            "暂停（F8）",
            "Pause (F8)"
        },
        {
            "停止（F9）",
            "Stop (F9)"
        },
        {
            "等待连接。切出游戏暂停按键；进菜单前按 F9 停止。",
            "Waiting to connect. Inputs pause outside the game. Press F9 before menus."
        },
        {
            "法器释放仍受游戏冷却、灵魂和施法动作限制。",
            "Artifact use is subject to in-game cooldowns, souls and casting animations."
        },
        {
            "设置已更新，按 F8 开始。",
            "Settings updated. Press F8 to start."
        },
        {
            "已连接，按 F8 开始。",
            "Connected. Press F8 to start."
        },
        {
            "已停止，按 F8 开始。",
            "Stopped. Press F8 to start."
        },
        {
            "已开启。切出游戏暂停按键；进菜单前按 F9 停止。",
            "Active. Inputs pause outside the game. Press F9 before menus."
        },
        {
            "工具箱已开启，正在监测。进菜单前按 F9 停止。",
            "Active and monitoring. Press F9 before opening menus."
        },
        {
            "游戏不在前台，已暂停按键。",
            "Game is not in the foreground. Inputs paused."
        },
        {
            "正在读取游戏血量…",
            "Reading player health..."
        },
        {
            "未读取",
            "Unknown"
        },
        {
            "F8 / F9 被占用，请关闭旧工具后重新打开，或使用窗口按钮。",
            "F8/F9 are in use. Close the old tool and reopen, or use the buttons."
        },
        {
            "请关闭旧 AutoHeal 或修改器，再按 F8 开始。",
            "Close the old AutoHeal or trainer, then press F8."
        },
        {
            "设置键位",
            "Set Key Binding"
        },
        {
            "请按下要绑定的键。\nF8、F9 保留；Esc 取消。",
            "Press the key to bind.\nF8/F9 are reserved. Esc cancels."
        },
        {
            "选择槽位",
            "Select slot"
        },
        {
            " 槽位",
            " slot"
        },
        {
            "回复图腾",
            "Totem of Regeneration"
        },
        {
            "灵魂治疗器",
            "Soul Healer"
        },
        {
            "灵魂收割器",
            "Soul Harvester"
        },
        {
            "腐化种子",
            "Corrupted Seeds"
        },
        {
            "手动消耗：{0} 灵魂 · 灵魂不足时改用药水",
            "Manual cost: {0} souls / potion when insufficient"
        },
        {
            "{0} 槽位 · {1} · 消耗 {2} 灵魂",
            "Slot {0} / {1} / cost: {2} souls"
        },
        {
            "生命：{0:0.#} / {1:0.#}（{2:0.#}%）\n灵魂：{3} / {4}",
            "Health: {0:0.#} / {1:0.#} ({2:0.#}%)\nSouls: {3} / {4}"
        },
        {
            "生命：{0:0.#} / {1:0.#}（{2:0.#}%）\n灵魂：{3:0.#} / {4:0.#}",
            "Health: {0:0.#} / {1:0.#} ({2:0.#}%)\nSouls: {3:0.#} / {4:0.#}"
        },
        {
            "{0:HH:mm:ss} {1}：{2} · 共 {3} 次",
            "{0:HH:mm:ss} {1}: {2} / {3} attempts"
        },
        {
            "法器连发",
            "Artifact macro"
        },
        {
            "跳劈补按",
            "Jump follow-up"
        },
        {
            "法器回血",
            "Artifact healing"
        },
        {
            "灵魂不足 → 药水回血",
            "Insufficient souls → potion healing"
        },
        {
            "请至少启用一项功能。",
            "Enable at least one feature."
        },
        {
            "三个法器槽位请使用不同的键。",
            "Use different keys for the three artifact slots."
        },
        {
            "请勾选需要同时使用的法器槽位。",
            "Select artifact slots to activate together."
        },
        {
            "法器连发的触发键不能与选中的法器键相同。",
            "The macro trigger cannot match a selected artifact key."
        },
        {
            "跳劈的检测键与补按键请使用不同的键。",
            "Use different detection and follow-up keys for jump attacks."
        },
        {
            "法器连发与跳劈请使用不同的触发键。",
            "Use different trigger keys for the artifact macro and jump attacks."
        },
        {
            "法器连发的触发键不能与自动发送的按键相同。",
            "The macro trigger cannot match an automatically sent key."
        },
        {
            "跳劈检测键不能与自动发送的按键相同。",
            "The jump detection key cannot match an automatically sent key."
        },
        {
            "这个按键无法发送，请换一个键。",
            "Cannot send this key. Choose another key."
        },
        {
            "按键发送失败，已暂停。请确认工具箱与游戏的运行权限一致。",
            "Input failed. Paused. Run the toolbox and game with matching privileges."
        },
        {
            "无法连接游戏进程。",
            "Cannot connect to the game process."
        },
        {
            "读取地址验证失败。",
            "Memory address validation failed. Reconnect in a level."
        },
        {
            "游戏正在切换场景或已退出，请重新连接。",
            "The game is changing levels or has exited. Reconnect."
        },
        {
            "游戏数值验证失败。",
            "Game value validation failed."
        },
        {
            "请启动游戏并进入关卡，再连接。",
            "Start the game and enter a level, then connect."
        },
        {
            "游戏版本不匹配，需要重新适配。",
            "Unsupported game version. A compatibility update is required."
        },
        {
            "游戏结构校验失败。",
            "Game structure validation failed."
        },
        {
            "名称索引无效。",
            "Invalid name index."
        },
        {
            "名称长度无效。",
            "Invalid name length."
        },
        {
            "属性布局不匹配：",
            "Property layout mismatch: "
        },
        {
            "找不到游戏属性：",
            "Game property not found: "
        },
        {
            "角色对象不可用。",
            "Player object is unavailable."
        },
        {
            "角色对象已变化。",
            "Player object has changed."
        },
        {
            "对象表验证失败。",
            "Object table validation failed."
        },
        {
            "未找到唯一的本地玩家，请进入单人关卡后重新连接。",
            "Cannot identify the local player. Enter a level and reconnect."
        },
        {
            "游戏状态已变化，请重新连接。",
            "Game state has changed. Reconnect."
        },
        {
            "等待玩家进入关卡。",
            "Waiting for the player to enter a level."
        },
        {
            "玩家属性归属校验失败。",
            "Player attribute ownership validation failed."
        },
        {
            "玩家属性列表无效。",
            "Invalid player attribute list."
        },
        {
            "血量读数暂不可用。",
            "Health reading is temporarily unavailable."
        },
        {
            "法器布局验证失败。",
            "Artifact layout validation failed."
        },
        {
            "自定义键无法确定槽位，请关闭自动读取并填写消耗。",
            "Custom key has no known slot. Disable auto-read and enter the cost."
        },
        {
            "法器归属验证失败。",
            "Artifact ownership validation failed."
        },
        {
            "法器列表暂不可用。",
            "Artifact list is temporarily unavailable."
        },
        {
            "所选法器槽位为空或暂不可用。",
            "Selected artifact slot is empty or unavailable."
        },
        {
            "法器槽位验证失败。",
            "Artifact slot validation failed."
        },
        {
            "没有找到所选法器。",
            "Selected artifact was not found."
        },
        {
            "法器消耗暂不可读，请重新连接或关闭自动读取并填写消耗。",
            "Cannot read soul cost. Reconnect or enter the cost manually."
        }
    };
    static readonly Dictionary<string, string> Ja = new Dictionary<string, string>
    {
        {
            "绿宝石 {0}：需进入游戏原生拾取范围",
            "エメラルド {0}：ゲームの回収範囲に入る必要があります"
        },
        {
            "检测到多个游戏进程，请只保留一个游戏实例后连接。",
            "複数のゲームプロセスを検出しました。ゲームを1つだけ起動して接続してください。"
        },
        {
            "攻击预警（实验）",
            "攻撃警戒（試験機能）"
        },
        {
            "攻击动作或直线弹道接近时，提前使用已选法器",
            "攻撃動作・直線弾道の接近時に選択した法器を先行使用"
        },
        {
            "药水仍受血量阈值限制；仅在游戏前台生效",
            "ポーションは低 HP 時のみ使用・ゲーム前面時に有効"
        },
        {
            "等待攻击预警数据",
            "攻撃警戒データを待機中"
        },
        {
            "附近敌人 {0} · 弹道 {1} · 危险 {2}",
            "付近の敵 {0} · 弾道 {1} · 危険 {2}"
        },
        {
            "预警读数不可用，保持原有血量规则",
            "警戒データ未取得・従来の HP ルールを継続"
        },
        {
            "攻击预警自动使用",
            "攻撃警戒による自動使用"
        },
        {
            "游戏映像验证失败。",
            "ゲーム映像の検証に失敗しました。"
        },
        {
            "游戏映像区段无效。",
            "ゲーム映像のセクションが無効です。"
        },
        {
            "找不到经过验证的游戏名称表，需要更新适配。",
            "ゲーム名テーブルを検証できません。更新が必要です。"
        },
        {
            "找不到经过验证的游戏对象表，需要更新适配。",
            "ゲームオブジェクトテーブルを検証できません。更新が必要です。"
        },
        {
            "使用法器（不勾选时仅使用药水）",
            "使用するアーティファクト（未選択：ポーションのみ）"
        },
        {
            "未选择法器，仅按血量与药水冷却自动使用药水。",
            "未選択のため、HP とポーションのクールダウンのみで自動使用します。"
        },
        {
            "低血量 → 药水回血",
            "低 HP → ポーション使用"
        },
        {
            "许可与致谢",
            "ライセンスと謝辞"
        },
        {
            "关闭",
            "閉じる"
        },
        {
            "导出日志",
            "ログ出力"
        },
        {
            "导出诊断日志",
            "診断ログを出力"
        },
        {
            "文本文件 (*.txt)|*.txt",
            "テキストファイル (*.txt)|*.txt"
        },
        {
            "日志已导出，可附在群内或 GitHub Issues 反馈中。",
            "ログを出力しました。グループまたは GitHub Issues での報告に添付できます。"
        },
        {
            "导出失败，请选择其他保存位置。\n{0}",
            "出力に失敗しました。別の保存先を選んでください。\n{0}"
        },
        {
            "发生错误，已停止。请导出日志反馈。",
            "エラーのため停止しました。ログを出力して報告してください。"
        },
        {
            "游戏手柄键位未读取，请检查游戏内绑定。",
            "ゲームパッド設定を取得できません。ゲーム内の設定を確認してください。"
        },
        {
            "游戏键位与工具箱触发键冲突，请更换触发键。",
            "ゲームのキーとツールの起動キーが重複しています。起動キーを変更してください。"
        },
        {
            "跟随游戏键位：{0} → {1}\n使用对应的键盘操作；手柄按钮请在游戏内修改。",
            "ゲーム設定：{0} → {1}\n対応するキーボード操作を送信します。パッドのボタンはゲーム内で変更してください。"
        },
        {
            "键盘 / 鼠标",
            "キーボード / マウス"
        },
        {
            "键盘快捷键保持 F8 / F9；可选择 Xbox 绑定手柄",
            "F8 / F9 は固定です。Xbox を選ぶとボタンを設定できます"
        },
        {
            "请按下要绑定的键；鼠标绑定请点击此提示区。Esc 取消",
            "キーを押してください。マウスはこの欄をクリック。Esc で取消"
        },
        {
            "清除手柄绑定",
            "ゲームパッド設定を解除"
        },
        {
            "鼠标右键",
            "マウス右ボタン"
        },
        {
            "手柄",
            "ゲームパッド"
        },
        {
            "Xbox 手柄绑定",
            "Xbox コントローラー割り当て"
        },
        {
            "用手柄按钮或组合触发工具箱功能",
            "ボタンや同時押しで機能を実行"
        },
        {
            "启用手柄触发",
            "ゲームパッド入力を有効化"
        },
        {
            "自动识别键鼠与手柄；默认 Xbox 布局",
            "入力機器を自動検出・Xbox 配置"
        },
        {
            "开始 / 暂停",
            "開始 / 一時停止"
        },
        {
            "立即停止",
            "すぐに停止"
        },
        {
            "跳跃检测按钮",
            "ジャンプ検出ボタン"
        },
        {
            "攻击检测按钮",
            "攻撃検出ボタン"
        },
        {
            "等待 Xbox 手柄连接",
            "Xbox コントローラー接続待機中"
        },
        {
            "Xbox 手柄已连接",
            "Xbox コントローラー接続済み"
        },
        {
            "跳劈：先按跳跃，再按攻击，或同时按下",
            "ジャンプ後に攻撃、または同時押し"
        },
        {
            "按钮保留游戏原操作；请使用未占用的组合",
            "ゲーム側の操作も有効です。未使用の組み合わせを選択"
        },
        {
            "设置手柄按钮",
            "ゲームパッドのボタン設定"
        },
        {
            "先松开按钮，再按下要绑定的按钮或组合，松开后确认。",
            "ボタンを離してから割り当てるボタンを押し、離すと確定"
        },
        {
            "清除绑定",
            "割り当てを解除"
        },
        {
            "取消",
            "キャンセル"
        },
        {
            "未绑定",
            "未設定"
        },
        {
            "手柄操作请使用不同的按钮组合。",
            "重複しないボタンの組み合わせを選択してください"
        },
        {
            "自动识别：Xbox 手柄",
            "自動検出：Xbox コントローラー"
        },
        {
            "自动识别：键盘 / 鼠标",
            "自動検出：キーボード / マウス"
        },
        {
            "正在等待连接",
            "接続を待機中"
        },
        {
            "已连接",
            "接続済み"
        },
        {
            "生命：-- / --\n灵魂：-- / --",
            "HP：-- / --\nソウル：-- / --"
        },
        {
            "右键闪避",
            "右クリック回避"
        },
        {
            "启用右键闪避",
            "右クリック回避を有効にする"
        },
        {
            "仅在游戏前台生效；暂停后恢复普通右键。",
            "ゲーム操作中のみ有効。一時停止で通常操作に戻ります。"
        },
        {
            "远程攻击",
            "遠距離攻撃"
        },
        {
            "先按住 Shift，再按鼠标右键；可持续蓄力。",
            "Shift を押して右クリック。長押しでチャージできます。"
        },
        {
            "右键闪避，Shift + 右键远程攻击。",
            "右クリックで回避、Shift + 右クリックで射撃。"
        },
        {
            "右键监听失败，请重新打开工具箱。",
            "マウスの監視に失敗しました。ツールを再起動してください。"
        },
        {
            "药水备用判断槽位",
            "ポーション判断用スロット"
        },
        {
            "只看指定槽位",
            "指定スロットのみ"
        },
        {
            "{0} 槽位 · 灵魂消耗：{1}",
            "スロット {0} · ソウル消費：{1}"
        },
        {
            "法器不可用时使用药水",
            "アーティファクト使用不可時にポーションを使う"
        },
        {
            "后台自动使用",
            "バックグラウンドで自動使用"
        },
        {
            "切出游戏继续检测和使用；组合宏、跳劈仅在前台触发。",
            "別のウィンドウでも監視・自動使用を継続。マクロはゲーム操作中のみ。"
        },
        {
            "法器与药水冷却",
            "アーティファクトとポーションのクールダウン"
        },
        {
            "等待冷却数据",
            "クールダウン情報を待機中"
        },
        {
            "已就绪",
            "使用可能"
        },
        {
            "冷却中",
            "クールダウン中"
        },
        {
            "暂不可用",
            "使用不可"
        },
        {
            "药水",
            "ポーション"
        },
        {
            "（冷却时长 {0:0.#} 秒）",
            "（クールダウン {0:0.#} 秒）"
        },
        {
            "游戏窗口已变化，等待重新连接。",
            "ゲームウィンドウが変わりました。再接続を待機中。"
        },
        {
            "后台按键发送失败，请确认运行权限一致。",
            "バックグラウンド入力に失敗。実行権限を確認してください。"
        },
        {
            "连接后自动开始并持续检测；进菜单前按 F9 停止。",
            "接続後に自動開始します。メニューを開く前に F9 で停止。"
        },
        {
            "已开启，持续检测血量与冷却。进菜单前按 F9 停止。",
            "HP とクールダウンを監視中。メニューを開く前に F9 で停止。"
        },
        {
            "持续等待角色进入关卡，自动重新连接。",
            "ステージへの入場を待機中。自動で再接続します。"
        },
        {
            "后台持续检测，低血量时自动使用；F9 停止。",
            "バックグラウンド監視中。低 HP 時に自動使用。F9 で停止。"
        },
        {
            "后台持续检测，返回游戏后自动使用。",
            "監視を継続中。ゲームに戻ると自動使用します。"
        },
        {
            "法器不可用 → 药水回血",
            "使用不可 → ポーションで回復"
        },
        {
            "冷却界面已变化，重新读取。",
            "クールダウン UI が変わりました。再取得します。"
        },
        {
            "自动法器",
            "自動使用"
        },
        {
            "低血量时自动使用法器",
            "低 HP 時にアーティファクトを自動使用"
        },
        {
            "生命低于设定比例时，同时使用已选法器。",
            "HP が設定値を下回ると、選択したスロットを使用します。"
        },
        {
            "低于设定血量时，自动组合使用已选法器。",
            "HP が設定値を下回ると、選択した組み合わせを使います。"
        },
        {
            "使用法器",
            "使用するアーティファクト"
        },
        {
            "低血量法器组合",
            "低 HP 時の組み合わせ"
        },
        {
            "请勾选低血量时使用的法器。",
            "低 HP 時に使うスロットを選択してください。"
        },
        {
            "药水键不能与自动使用的法器键相同。",
            "ポーションと自動使用のキーは別々にしてください。"
        },
        {
            "MCD2A已在运行。",
            "MCD2A は既に起動しています。"
        },
        {
            "功能设置",
            "機能設定"
        },
        {
            "按你的游玩习惯配置",
            "好みに合わせて設定"
        },
        {
            "法器组合",
            "組み合わせ"
        },
        {
            "跳劈辅助",
            "ジャンプ攻撃"
        },
        {
            "快捷操作",
            "ショートカット"
        },
        {
            "F8  开始 / 暂停",
            "F8  開始 / 一時停止"
        },
        {
            "F9  立即停止",
            "F9  即時停止"
        },
        {
            "F11 全屏 / 还原",
            "F11 全画面 / 復元"
        },
        {
            "设置血量时自动使用法器",
            "HP に応じた自動使用"
        },
        {
            "正在连接…",
            "接続中…"
        },
        {
            "血量阈值",
            "HP のしきい値"
        },
        {
            "低于此百分比时触发",
            "この割合を下回ると実行"
        },
        {
            "1 槽位",
            "スロット 1"
        },
        {
            "2 槽位",
            "スロット 2"
        },
        {
            "3 槽位",
            "スロット 3"
        },
        {
            "法器：等待连接 · 消耗：等待读取",
            "アーティファクト：接続待機 · 消費：取得待機"
        },
        {
            "自动读取灵魂消耗",
            "ソウル消費を自動取得"
        },
        {
            "重试间隔",
            "再試行間隔"
        },
        {
            "法器和药水共用此尝试间隔",
            "アーティファクトとポーションで共有"
        },
        {
            "秒",
            "秒"
        },
        {
            "空格",
            "スペース"
        },
        {
            "同时释放多个法器",
            "複数のアーティファクトを使用"
        },
        {
            "勾选槽位，使用一个触发键组合释放。",
            "スロットを選び、1 つのキーでまとめて使います。"
        },
        {
            "组合使用你选中的法器槽位。",
            "選択したスロットをまとめて使います。"
        },
        {
            "法器组合宏",
            "組み合わせマクロ"
        },
        {
            "为一个按键分配组合，按下时使用已选法器。",
            "キーを押すと、選択したアーティファクトを使います。"
        },
        {
            "触发键",
            "実行キー"
        },
        {
            "选择要同时使用的法器",
            "同時に使うスロットを選択"
        },
        {
            "启用跳劈辅助",
            "ジャンプ攻撃補助を有効にする"
        },
        {
            "检测跳跃和左键操作，再补按指定按键。",
            "ジャンプと左クリックを検出して追加入力します。"
        },
        {
            "跳跃 + 鼠标左键 → 自动补按 Q。",
            "ジャンプ + 左クリック → Q を追加入力。"
        },
        {
            "检测组合 → 自动补按",
            "入力の組み合わせ → 追加入力"
        },
        {
            "鼠标左键",
            "左クリック"
        },
        {
            "补按延迟",
            "追加入力の遅延"
        },
        {
            "检测到组合操作后，再等待此时长。",
            "組み合わせ検出後の待ち時間。"
        },
        {
            "生命：等待连接",
            "HP：接続待機中"
        },
        {
            "生命：等待重新连接",
            "HP：再接続待機中"
        },
        {
            "连接游戏",
            "ゲームに接続"
        },
        {
            "开始（F8）",
            "開始（F8）"
        },
        {
            "暂停（F8）",
            "一時停止（F8）"
        },
        {
            "停止（F9）",
            "停止（F9）"
        },
        {
            "设置已更新，按 F8 开始。",
            "設定を更新しました。F8 で開始。"
        },
        {
            "已连接，按 F8 开始。",
            "接続済み。F8 で開始。"
        },
        {
            "已停止，按 F8 开始。",
            "停止中。F8 で開始。"
        },
        {
            "工具箱已开启，正在监测。进菜单前按 F9 停止。",
            "監視中。メニューを開く前に F9 で停止。"
        },
        {
            "正在读取游戏血量…",
            "ゲームの HP を取得中…"
        },
        {
            "未读取",
            "未取得"
        },
        {
            "F8 / F9 被占用，请关闭旧工具后重新打开，或使用窗口按钮。",
            "F8 / F9 は使用中です。旧ツールを終了するかボタンを使ってください。"
        },
        {
            "请关闭旧 AutoHeal 或修改器，再按 F8 开始。",
            "旧 AutoHeal またはトレーナーを終了してから F8 で開始。"
        },
        {
            "设置键位",
            "キー設定"
        },
        {
            "请按下要绑定的键。\nF8、F9 保留；Esc 取消。",
            "設定するキーを押してください。\nF8、F9 は予約済み。Esc で取消。"
        },
        {
            "选择槽位",
            "スロットを選択"
        },
        {
            " 槽位",
            " スロット"
        },
        {
            "回复图腾",
            "再生のトーテム"
        },
        {
            "灵魂治疗器",
            "ソウルヒーラー"
        },
        {
            "灵魂收割器",
            "ソウルハーベスター"
        },
        {
            "腐化种子",
            "汚染された種"
        },
        {
            "{0} 槽位 · {1} · 消耗 {2} 灵魂",
            "スロット {0} · {1} · 消費 {2} ソウル"
        },
        {
            "生命：{0:0.#} / {1:0.#}（{2:0.#}%）\n灵魂：{3} / {4}",
            "HP：{0:0.#} / {1:0.#}（{2:0.#}%）\nソウル：{3} / {4}"
        },
        {
            "请至少启用一项功能。",
            "機能を 1 つ以上有効にしてください。"
        },
        {
            "三个法器槽位请使用不同的键。",
            "3 つのスロットには別々のキーを設定してください。"
        },
        {
            "请勾选需要同时使用的法器槽位。",
            "同時に使うスロットを選択してください。"
        },
        {
            "法器连发的触发键不能与选中的法器键相同。",
            "実行キーと選択したスロットのキーは別々にしてください。"
        },
        {
            "跳劈的检测键与补按键请使用不同的键。",
            "検出キーと追加入力キーは別々にしてください。"
        },
        {
            "法器连发与跳劈请使用不同的触发键。",
            "マクロとジャンプ攻撃の実行キーは別々にしてください。"
        },
        {
            "法器连发的触发键不能与自动发送的按键相同。",
            "実行キーと自動送信するキーは別々にしてください。"
        },
        {
            "跳劈检测键不能与自动发送的按键相同。",
            "検出キーと自動送信するキーは別々にしてください。"
        },
        {
            "这个按键无法发送，请换一个键。",
            "このキーは送信できません。別のキーを選んでください。"
        },
        {
            "按键发送失败，已暂停。请确认工具箱与游戏的运行权限一致。",
            "入力に失敗して停止しました。実行権限を確認してください。"
        },
        {
            "无法连接游戏进程。",
            "ゲームのプロセスに接続できません。"
        },
        {
            "读取地址验证失败。",
            "読み取りアドレスの検証に失敗しました。"
        },
        {
            "游戏正在切换场景或已退出，请重新连接。",
            "場面切替中、またはゲームが終了しました。再接続してください。"
        },
        {
            "游戏数值验证失败。",
            "ゲームの数値検証に失敗しました。"
        },
        {
            "请启动游戏并进入关卡，再连接。",
            "ゲームを起動してステージに入ってから接続してください。"
        },
        {
            "游戏版本不匹配，需要重新适配。",
            "未対応のゲームバージョンです。更新が必要です。"
        },
        {
            "游戏结构校验失败。",
            "ゲーム構造の検証に失敗しました。"
        },
        {
            "名称索引无效。",
            "名前インデックスが無効です。"
        },
        {
            "名称长度无效。",
            "名前の長さが無効です。"
        },
        {
            "属性布局不匹配：",
            "プロパティ構造が不一致："
        },
        {
            "找不到游戏属性：",
            "ゲームのプロパティが見つかりません："
        },
        {
            "角色对象不可用。",
            "プレイヤーオブジェクトが使用できません。"
        },
        {
            "角色对象已变化。",
            "プレイヤーオブジェクトが変わりました。"
        },
        {
            "对象表验证失败。",
            "オブジェクト表の検証に失敗しました。"
        },
        {
            "未找到唯一的本地玩家，请进入单人关卡后重新连接。",
            "ローカルプレイヤーを特定できません。ソロステージで再接続してください。"
        },
        {
            "游戏状态已变化，请重新连接。",
            "ゲームの状態が変わりました。再接続してください。"
        },
        {
            "等待玩家进入关卡。",
            "ステージへの入場待機中。"
        },
        {
            "玩家属性归属校验失败。",
            "プレイヤープロパティの所有者検証に失敗しました。"
        },
        {
            "玩家属性列表无效。",
            "プレイヤープロパティ一覧が無効です。"
        },
        {
            "血量读数暂不可用。",
            "HP の読み取りが一時的にできません。"
        },
        {
            "法器布局验证失败。",
            "アーティファクト構造の検証に失敗しました。"
        },
        {
            "法器归属验证失败。",
            "アーティファクトの所有者検証に失敗しました。"
        },
        {
            "法器列表暂不可用。",
            "アーティファクト一覧が一時的に取得できません。"
        },
        {
            "所选法器槽位为空或暂不可用。",
            "選択したスロットは空か使用できません。"
        },
        {
            "法器槽位验证失败。",
            "スロットの検証に失敗しました。"
        },
        {
            "没有找到所选法器。",
            "選択したアーティファクトが見つかりません。"
        },
        {
            "法器消耗暂不可读，请重新连接或关闭自动读取并填写消耗。",
            "消費を取得できません。再接続するか手動で設定してください。"
        },
        {
            "法器连发",
            "アーティファクトマクロ"
        },
        {
            "跳劈补按",
            "ジャンプ攻撃の追加入力"
        },
        {
            "闪避键",
            "回避キー"
        },
        {
            "与游戏内的定向闪避绑定保持一致。",
            "ゲームの方向回避キーと合わせてください。"
        },
    };
    static readonly Dictionary<string, string> Ko = new Dictionary<string, string>
    {
        {
            "绿宝石 {0}：需进入游戏原生拾取范围",
            "에메랄드 {0}: 게임 내 획득 범위로 들어가야 합니다"
        },
        {
            "检测到多个游戏进程，请只保留一个游戏实例后连接。",
            "게임 프로세스가 여러 개 감지되었습니다. 하나만 실행한 상태에서 연결하세요."
        },
        {
            "攻击预警（实验）",
            "공격 경고 (실험 기능)"
        },
        {
            "攻击动作或直线弹道接近时，提前使用已选法器",
            "공격 동작이나 직선 탄도 접근 시 선택한 아티팩트 사용"
        },
        {
            "药水仍受血量阈值限制；仅在游戏前台生效",
            "물약은 낮은 체력에서만 사용 · 게임이 전면일 때 작동"
        },
        {
            "等待攻击预警数据",
            "공격 경고 데이터 대기 중"
        },
        {
            "附近敌人 {0} · 弹道 {1} · 危险 {2}",
            "주변 적 {0} · 탄도 {1} · 위험 {2}"
        },
        {
            "预警读数不可用，保持原有血量规则",
            "경고 데이터 없음 · 기존 체력 규칙 유지"
        },
        {
            "攻击预警自动使用",
            "공격 경고 자동 사용"
        },
        {
            "游戏映像验证失败。",
            "게임 이미지 검증에 실패했습니다."
        },
        {
            "游戏映像区段无效。",
            "게임 이미지 섹션이 유효하지 않습니다."
        },
        {
            "找不到经过验证的游戏名称表，需要更新适配。",
            "게임 이름 테이블 검증 실패 · 호환성 업데이트 필요"
        },
        {
            "找不到经过验证的游戏对象表，需要更新适配。",
            "게임 객체 테이블 검증 실패 · 호환성 업데이트 필요"
        },
        {
            "使用法器（不勾选时仅使用药水）",
            "사용할 유물 (미선택 시 물약만 사용)"
        },
        {
            "未选择法器，仅按血量与药水冷却自动使用药水。",
            "유물 미선택: 체력과 물약 재사용 대기시간에 따라 물약만 자동 사용합니다."
        },
        {
            "低血量 → 药水回血",
            "낮은 체력 → 물약 사용"
        },
        {
            "许可与致谢",
            "라이선스 및 감사"
        },
        {
            "关闭",
            "닫기"
        },
        {
            "导出日志",
            "로그 내보내기"
        },
        {
            "导出诊断日志",
            "진단 로그 내보내기"
        },
        {
            "文本文件 (*.txt)|*.txt",
            "텍스트 파일 (*.txt)|*.txt"
        },
        {
            "日志已导出，可附在群内或 GitHub Issues 反馈中。",
            "로그를 내보냈습니다. 그룹 또는 GitHub Issues 제보에 첨부할 수 있습니다."
        },
        {
            "导出失败，请选择其他保存位置。\n{0}",
            "내보내기에 실패했습니다. 다른 저장 위치를 선택하세요.\n{0}"
        },
        {
            "发生错误，已停止。请导出日志反馈。",
            "오류로 중지했습니다. 로그를 내보내 제보해 주세요."
        },
        {
            "游戏手柄键位未读取，请检查游戏内绑定。",
            "게임패드 설정을 읽지 못했습니다. 게임 내 설정을 확인하세요."
        },
        {
            "游戏键位与工具箱触发键冲突，请更换触发键。",
            "게임 키와 도구 실행 키가 겹칩니다. 실행 키를 변경하세요."
        },
        {
            "跟随游戏键位：{0} → {1}\n使用对应的键盘操作；手柄按钮请在游戏内修改。",
            "게임 설정: {0} → {1}\n대응하는 키보드 입력을 보냅니다. 패드 버튼은 게임에서 변경하세요."
        },
        {
            "键盘 / 鼠标",
            "키보드 / 마우스"
        },
        {
            "键盘快捷键保持 F8 / F9；可选择 Xbox 绑定手柄",
            "키보드는 F8 / F9로 고정됩니다. Xbox를 선택해 버튼을 설정하세요"
        },
        {
            "请按下要绑定的键；鼠标绑定请点击此提示区。Esc 取消",
            "키를 누르세요. 마우스는 이 영역을 클릭하세요. Esc로 취소"
        },
        {
            "清除手柄绑定",
            "컨트롤러 설정 지우기"
        },
        {
            "鼠标右键",
            "마우스 오른쪽 버튼"
        },
        {
            "手柄",
            "게임패드"
        },
        {
            "Xbox 手柄绑定",
            "Xbox 컨트롤러 설정"
        },
        {
            "用手柄按钮或组合触发工具箱功能",
            "버튼 또는 조합으로 기능 실행"
        },
        {
            "启用手柄触发",
            "컨트롤러 입력 사용"
        },
        {
            "自动识别键鼠与手柄；默认 Xbox 布局",
            "입력 장치 자동 감지 · Xbox 배열"
        },
        {
            "开始 / 暂停",
            "시작 / 일시 정지"
        },
        {
            "立即停止",
            "즉시 중지"
        },
        {
            "跳跃检测按钮",
            "점프 감지 버튼"
        },
        {
            "攻击检测按钮",
            "공격 감지 버튼"
        },
        {
            "等待 Xbox 手柄连接",
            "Xbox 컨트롤러 연결 대기 중"
        },
        {
            "Xbox 手柄已连接",
            "Xbox 컨트롤러 연결됨"
        },
        {
            "跳劈：先按跳跃，再按攻击，或同时按下",
            "점프 후 공격 또는 동시에 누르기"
        },
        {
            "按钮保留游戏原操作；请使用未占用的组合",
            "게임 조작도 유지됩니다. 미사용 조합을 선택하세요"
        },
        {
            "设置手柄按钮",
            "컨트롤러 버튼 설정"
        },
        {
            "先松开按钮，再按下要绑定的按钮或组合，松开后确认。",
            "버튼을 놓고 원하는 버튼이나 조합을 누른 뒤 놓으면 확인됩니다"
        },
        {
            "清除绑定",
            "설정 지우기"
        },
        {
            "取消",
            "취소"
        },
        {
            "未绑定",
            "미설정"
        },
        {
            "手柄操作请使用不同的按钮组合。",
            "겹치지 않는 버튼 조합을 선택하세요"
        },
        {
            "自动识别：Xbox 手柄",
            "자동 감지: Xbox 컨트롤러"
        },
        {
            "自动识别：键盘 / 鼠标",
            "자동 감지: 키보드 / 마우스"
        },
        {
            "正在等待连接",
            "연결 대기 중"
        },
        {
            "已连接",
            "연결됨"
        },
        {
            "生命：-- / --\n灵魂：-- / --",
            "체력: -- / --\n영혼: -- / --"
        },
        {
            "右键闪避",
            "우클릭 회피"
        },
        {
            "启用右键闪避",
            "우클릭 회피 사용"
        },
        {
            "仅在游戏前台生效；暂停后恢复普通右键。",
            "게임이 활성화된 동안만 적용됩니다. 일시 정지하면 원래대로 돌아갑니다."
        },
        {
            "远程攻击",
            "원거리 공격"
        },
        {
            "先按住 Shift，再按鼠标右键；可持续蓄力。",
            "Shift를 누른 채 우클릭하세요. 길게 눌러 충전할 수 있습니다."
        },
        {
            "右键闪避，Shift + 右键远程攻击。",
            "우클릭으로 회피, Shift + 우클릭으로 사격합니다."
        },
        {
            "右键监听失败，请重新打开工具箱。",
            "마우스 감지에 실패했습니다. 도구를 다시 실행하세요."
        },
        {
            "药水备用判断槽位",
            "물약 판단 슬롯"
        },
        {
            "只看指定槽位",
            "지정 슬롯만 확인"
        },
        {
            "{0} 槽位 · 灵魂消耗：{1}",
            "슬롯 {0} · 영혼 소모: {1}"
        },
        {
            "法器不可用时使用药水",
            "유물 사용 불가 시 물약 사용"
        },
        {
            "后台自动使用",
            "백그라운드 자동 사용"
        },
        {
            "切出游戏继续检测和使用；组合宏、跳劈仅在前台触发。",
            "다른 창에서도 감지와 자동 사용을 계속합니다. 매크로는 게임 활성화 시에만 작동합니다."
        },
        {
            "法器与药水冷却",
            "유물 및 물약 재사용 대기시간"
        },
        {
            "等待冷却数据",
            "대기시간 정보 확인 중"
        },
        {
            "已就绪",
            "사용 가능"
        },
        {
            "冷却中",
            "재사용 대기 중"
        },
        {
            "暂不可用",
            "사용 불가"
        },
        {
            "药水",
            "물약"
        },
        {
            "（冷却时长 {0:0.#} 秒）",
            " (대기시간 {0:0.#}초)"
        },
        {
            "游戏窗口已变化，等待重新连接。",
            "게임 창이 변경되었습니다. 재연결을 기다립니다."
        },
        {
            "后台按键发送失败，请确认运行权限一致。",
            "백그라운드 입력 실패. 실행 권한이 같은지 확인하세요."
        },
        {
            "连接后自动开始并持续检测；进菜单前按 F9 停止。",
            "연결 후 자동으로 시작합니다. 메뉴를 열기 전 F9로 중지하세요."
        },
        {
            "已开启，持续检测血量与冷却。进菜单前按 F9 停止。",
            "체력과 대기시간 감지 중. 메뉴를 열기 전 F9로 중지하세요."
        },
        {
            "持续等待角色进入关卡，自动重新连接。",
            "스테이지 진입을 기다립니다. 자동으로 재연결합니다."
        },
        {
            "后台持续检测，低血量时自动使用；F9 停止。",
            "백그라운드 감지 중. 체력이 낮으면 자동 사용합니다. F9로 중지."
        },
        {
            "后台持续检测，返回游戏后自动使用。",
            "감지를 계속합니다. 게임으로 돌아가면 자동 사용합니다."
        },
        {
            "法器不可用 → 药水回血",
            "유물 사용 불가 → 물약 회복"
        },
        {
            "冷却界面已变化，重新读取。",
            "대기시간 화면이 변경되어 다시 읽습니다."
        },
        {
            "自动法器",
            "자동 유물"
        },
        {
            "低血量时自动使用法器",
            "체력이 낮으면 유물 자동 사용"
        },
        {
            "生命低于设定比例时，同时使用已选法器。",
            "체력이 설정 비율보다 낮으면 선택한 유물을 사용합니다."
        },
        {
            "低于设定血量时，自动组合使用已选法器。",
            "체력이 설정값보다 낮으면 선택한 유물 조합을 사용합니다."
        },
        {
            "使用法器",
            "사용할 유물"
        },
        {
            "低血量法器组合",
            "저체력 유물 조합"
        },
        {
            "请勾选低血量时使用的法器。",
            "체력이 낮을 때 사용할 유물을 선택하세요."
        },
        {
            "药水键不能与自动使用的法器键相同。",
            "물약 키와 자동 유물 키는 달라야 합니다."
        },
        {
            "MCD2A已在运行。",
            "MCD2A이 이미 실행 중입니다."
        },
        {
            "功能设置",
            "기능 설정"
        },
        {
            "按你的游玩习惯配置",
            "플레이 취향에 맞게 설정"
        },
        {
            "法器组合",
            "유물 조합"
        },
        {
            "跳劈辅助",
            "점프 공격"
        },
        {
            "快捷操作",
            "단축키"
        },
        {
            "F8  开始 / 暂停",
            "F8  시작 / 일시 정지"
        },
        {
            "F9  立即停止",
            "F9  즉시 중지"
        },
        {
            "F11 全屏 / 还原",
            "F11 전체 화면 / 복원"
        },
        {
            "设置血量时自动使用法器",
            "체력에 따른 자동 유물 사용"
        },
        {
            "正在连接…",
            "연결 중…"
        },
        {
            "血量阈值",
            "체력 기준값"
        },
        {
            "低于此百分比时触发",
            "이 비율보다 낮으면 실행"
        },
        {
            "1 槽位",
            "슬롯 1"
        },
        {
            "2 槽位",
            "슬롯 2"
        },
        {
            "3 槽位",
            "슬롯 3"
        },
        {
            "法器：等待连接 · 消耗：等待读取",
            "유물: 연결 대기 · 소모: 확인 대기"
        },
        {
            "自动读取灵魂消耗",
            "영혼 소모 자동 읽기"
        },
        {
            "重试间隔",
            "재시도 간격"
        },
        {
            "法器和药水共用此尝试间隔",
            "유물과 물약에 공통 적용"
        },
        {
            "秒",
            "초"
        },
        {
            "空格",
            "스페이스"
        },
        {
            "同时释放多个法器",
            "여러 유물 동시 사용"
        },
        {
            "勾选槽位，使用一个触发键组合释放。",
            "슬롯을 선택하고 하나의 키로 함께 사용합니다."
        },
        {
            "组合使用你选中的法器槽位。",
            "선택한 유물 슬롯을 함께 사용합니다."
        },
        {
            "法器组合宏",
            "유물 조합 매크로"
        },
        {
            "为一个按键分配组合，按下时使用已选法器。",
            "지정 키를 누르면 선택한 유물을 사용합니다."
        },
        {
            "触发键",
            "실행 키"
        },
        {
            "选择要同时使用的法器",
            "함께 사용할 유물 선택"
        },
        {
            "启用跳劈辅助",
            "점프 공격 보조 사용"
        },
        {
            "检测跳跃和左键操作，再补按指定按键。",
            "점프와 좌클릭을 감지한 후 지정 키를 입력합니다."
        },
        {
            "跳跃 + 鼠标左键 → 自动补按 Q。",
            "점프 + 좌클릭 → Q 자동 입력."
        },
        {
            "检测组合 → 自动补按",
            "입력 조합 → 자동 추가 입력"
        },
        {
            "鼠标左键",
            "좌클릭"
        },
        {
            "补按延迟",
            "추가 입력 지연"
        },
        {
            "检测到组合操作后，再等待此时长。",
            "입력 조합 감지 후 대기 시간입니다."
        },
        {
            "生命：等待连接",
            "체력: 연결 대기"
        },
        {
            "生命：等待重新连接",
            "체력: 재연결 대기"
        },
        {
            "连接游戏",
            "게임 연결"
        },
        {
            "开始（F8）",
            "시작 (F8)"
        },
        {
            "暂停（F8）",
            "일시 정지 (F8)"
        },
        {
            "停止（F9）",
            "중지 (F9)"
        },
        {
            "设置已更新，按 F8 开始。",
            "설정이 변경되었습니다. F8로 시작하세요."
        },
        {
            "已连接，按 F8 开始。",
            "연결되었습니다. F8로 시작하세요."
        },
        {
            "已停止，按 F8 开始。",
            "중지되었습니다. F8로 시작하세요."
        },
        {
            "工具箱已开启，正在监测。进菜单前按 F9 停止。",
            "감지 중입니다. 메뉴를 열기 전 F9로 중지하세요."
        },
        {
            "正在读取游戏血量…",
            "게임 체력 읽는 중…"
        },
        {
            "未读取",
            "확인 불가"
        },
        {
            "F8 / F9 被占用，请关闭旧工具后重新打开，或使用窗口按钮。",
            "F8 / F9 사용 중. 이전 도구를 종료하거나 화면 버튼을 사용하세요."
        },
        {
            "请关闭旧 AutoHeal 或修改器，再按 F8 开始。",
            "이전 AutoHeal 또는 트레이너를 종료한 후 F8로 시작하세요."
        },
        {
            "设置键位",
            "키 설정"
        },
        {
            "请按下要绑定的键。\nF8、F9 保留；Esc 取消。",
            "지정할 키를 누르세요.\nF8, F9는 예약 키입니다. Esc로 취소."
        },
        {
            "选择槽位",
            "슬롯 선택"
        },
        {
            " 槽位",
            " 슬롯"
        },
        {
            "回复图腾",
            "재생의 토템"
        },
        {
            "灵魂治疗器",
            "영혼 치유기"
        },
        {
            "灵魂收割器",
            "영혼 수확기"
        },
        {
            "腐化种子",
            "오염된 씨앗"
        },
        {
            "{0} 槽位 · {1} · 消耗 {2} 灵魂",
            "슬롯 {0} · {1} · 소모 {2} 영혼"
        },
        {
            "生命：{0:0.#} / {1:0.#}（{2:0.#}%）\n灵魂：{3} / {4}",
            "체력: {0:0.#} / {1:0.#} ({2:0.#}%)\n영혼: {3} / {4}"
        },
        {
            "请至少启用一项功能。",
            "기능을 하나 이상 활성화하세요."
        },
        {
            "三个法器槽位请使用不同的键。",
            "세 유물 슬롯에 서로 다른 키를 지정하세요."
        },
        {
            "请勾选需要同时使用的法器槽位。",
            "함께 사용할 유물 슬롯을 선택하세요."
        },
        {
            "法器连发的触发键不能与选中的法器键相同。",
            "실행 키와 선택한 유물 키는 달라야 합니다."
        },
        {
            "跳劈的检测键与补按键请使用不同的键。",
            "감지 키와 추가 입력 키는 달라야 합니다."
        },
        {
            "法器连发与跳劈请使用不同的触发键。",
            "유물 매크로와 점프 공격의 실행 키는 달라야 합니다."
        },
        {
            "法器连发的触发键不能与自动发送的按键相同。",
            "매크로 실행 키와 자동 입력 키는 달라야 합니다."
        },
        {
            "跳劈检测键不能与自动发送的按键相同。",
            "점프 감지 키와 자동 입력 키는 달라야 합니다."
        },
        {
            "这个按键无法发送，请换一个键。",
            "이 키는 입력할 수 없습니다. 다른 키를 선택하세요."
        },
        {
            "按键发送失败，已暂停。请确认工具箱与游戏的运行权限一致。",
            "입력에 실패하여 일시 정지했습니다. 실행 권한을 확인하세요."
        },
        {
            "无法连接游戏进程。",
            "게임 프로세스에 연결할 수 없습니다."
        },
        {
            "读取地址验证失败。",
            "읽기 주소 검증에 실패했습니다."
        },
        {
            "游戏正在切换场景或已退出，请重新连接。",
            "장면 전환 중이거나 게임이 종료되었습니다. 다시 연결하세요."
        },
        {
            "游戏数值验证失败。",
            "게임 수치 검증에 실패했습니다."
        },
        {
            "请启动游戏并进入关卡，再连接。",
            "게임을 실행하고 스테이지에 진입한 후 연결하세요."
        },
        {
            "游戏版本不匹配，需要重新适配。",
            "지원하지 않는 게임 버전입니다. 호환성 업데이트가 필요합니다."
        },
        {
            "游戏结构校验失败。",
            "게임 구조 검증에 실패했습니다."
        },
        {
            "名称索引无效。",
            "이름 인덱스가 잘못되었습니다."
        },
        {
            "名称长度无效。",
            "이름 길이가 잘못되었습니다."
        },
        {
            "属性布局不匹配：",
            "속성 구조 불일치: "
        },
        {
            "找不到游戏属性：",
            "게임 속성을 찾을 수 없음: "
        },
        {
            "角色对象不可用。",
            "플레이어 객체를 사용할 수 없습니다."
        },
        {
            "角色对象已变化。",
            "플레이어 객체가 변경되었습니다."
        },
        {
            "对象表验证失败。",
            "객체 테이블 검증에 실패했습니다."
        },
        {
            "未找到唯一的本地玩家，请进入单人关卡后重新连接。",
            "로컬 플레이어를 식별할 수 없습니다. 싱글 스테이지에서 다시 연결하세요."
        },
        {
            "游戏状态已变化，请重新连接。",
            "게임 상태가 변경되었습니다. 다시 연결하세요."
        },
        {
            "等待玩家进入关卡。",
            "플레이어의 스테이지 진입 대기 중."
        },
        {
            "玩家属性归属校验失败。",
            "플레이어 속성 소유자 검증에 실패했습니다."
        },
        {
            "玩家属性列表无效。",
            "플레이어 속성 목록이 잘못되었습니다."
        },
        {
            "血量读数暂不可用。",
            "체력 수치를 일시적으로 읽을 수 없습니다."
        },
        {
            "法器布局验证失败。",
            "유물 구조 검증에 실패했습니다."
        },
        {
            "法器归属验证失败。",
            "유물 소유자 검증에 실패했습니다."
        },
        {
            "法器列表暂不可用。",
            "유물 목록을 일시적으로 읽을 수 없습니다."
        },
        {
            "所选法器槽位为空或暂不可用。",
            "선택한 유물 슬롯이 비어 있거나 사용할 수 없습니다."
        },
        {
            "法器槽位验证失败。",
            "유물 슬롯 검증에 실패했습니다."
        },
        {
            "没有找到所选法器。",
            "선택한 유물을 찾을 수 없습니다."
        },
        {
            "法器消耗暂不可读，请重新连接或关闭自动读取并填写消耗。",
            "소모량을 읽을 수 없습니다. 다시 연결하거나 직접 입력하세요."
        },
        {
            "法器连发",
            "유물 매크로"
        },
        {
            "跳劈补按",
            "점프 공격 추가 입력"
        },
        {
            "闪避键",
            "회피 키"
        },
        {
            "与游戏内的定向闪避绑定保持一致。",
            "게임의 방향 회피 키와 일치시켜 주세요。"
        },
    };
    static readonly Dictionary<string, string> Hk = new Dictionary<string, string>
    {
        {
            "绿宝石 {0}：需进入游戏原生拾取范围",
            "綠寶石 {0}：需進入遊戲原生拾取範圍"
        },
        {
            "检测到多个游戏进程，请只保留一个游戏实例后连接。",
            "偵測到多個遊戲程序，請只保留一個遊戲實例後連接。"
        },
        {
            "攻击预警（实验）",
            "攻擊預警（實驗）"
        },
        {
            "攻击动作或直线弹道接近时，提前使用已选法器",
            "攻擊動作或直線彈道接近時，提前使用已選法器"
        },
        {
            "药水仍受血量阈值限制；仅在游戏前台生效",
            "藥水仍受血量閾值限制；僅在遊戲前台生效"
        },
        {
            "等待攻击预警数据",
            "等待攻擊預警數據"
        },
        {
            "附近敌人 {0} · 弹道 {1} · 危险 {2}",
            "附近敵人 {0} · 彈道 {1} · 危險 {2}"
        },
        {
            "预警读数不可用，保持原有血量规则",
            "預警讀數不可用，保持原有血量規則"
        },
        {
            "攻击预警自动使用",
            "攻擊預警自動使用"
        },
        {
            "游戏映像验证失败。",
            "遊戲映像驗證失敗。"
        },
        {
            "游戏映像区段无效。",
            "遊戲映像區段無效。"
        },
        {
            "找不到经过验证的游戏名称表，需要更新适配。",
            "找不到經過驗證的遊戲名稱表，需要更新適配。"
        },
        {
            "找不到经过验证的游戏对象表，需要更新适配。",
            "找不到經過驗證的遊戲對象表，需要更新適配。"
        },
        {
            "使用法器（不勾选时仅使用药水）",
            "使用法器（不勾選時只使用藥水）"
        },
        {
            "未选择法器，仅按血量与药水冷却自动使用药水。",
            "未選擇法器，只按血量與藥水冷卻自動使用藥水。"
        },
        {
            "低血量 → 药水回血",
            "低血量 → 藥水回血"
        },
        {
            "许可与致谢",
            "許可與鳴謝"
        },
        {
            "关闭",
            "關閉"
        },
        {
            "导出日志",
            "匯出日誌"
        },
        {
            "导出诊断日志",
            "匯出診斷日誌"
        },
        {
            "文本文件 (*.txt)|*.txt",
            "文字檔案 (*.txt)|*.txt"
        },
        {
            "日志已导出，可附在群内或 GitHub Issues 反馈中。",
            "日誌已匯出，可附於群組或 GitHub Issues 的問題回報。"
        },
        {
            "导出失败，请选择其他保存位置。\n{0}",
            "匯出失敗，請選擇其他儲存位置。\n{0}"
        },
        {
            "发生错误，已停止。请导出日志反馈。",
            "發生錯誤，已停止。請匯出日誌回報。"
        },
        {
            "游戏手柄键位未读取，请检查游戏内绑定。",
            "未能讀取遊戲手掣按鍵，請檢查遊戲內設定。"
        },
        {
            "游戏键位与工具箱触发键冲突，请更换触发键。",
            "遊戲按鍵與工具箱觸發鍵重複，請更換觸發鍵。"
        },
        {
            "跟随游戏键位：{0} → {1}\n使用对应的键盘操作；手柄按钮请在游戏内修改。",
            "跟隨遊戲按鍵：{0} → {1}\n使用對應鍵盤操作；手掣按鈕請在遊戲內修改。"
        },
        {
            "键盘 / 鼠标",
            "鍵盤 / 滑鼠"
        },
        {
            "键盘快捷键保持 F8 / F9；可选择 Xbox 绑定手柄",
            "鍵盤快捷鍵保持 F8 / F9；可選擇 Xbox 綁定手掣"
        },
        {
            "请按下要绑定的键；鼠标绑定请点击此提示区。Esc 取消",
            "請按下要綁定的鍵；滑鼠綁定請點此提示區。Esc 取消"
        },
        {
            "清除手柄绑定",
            "清除手掣綁定"
        },
        {
            "鼠标右键",
            "滑鼠右鍵"
        },
        {
            "手柄",
            "手掣"
        },
        {
            "Xbox 手柄绑定",
            "Xbox 手掣綁定"
        },
        {
            "用手柄按钮或组合触发工具箱功能",
            "用手掣按鈕或組合觸發工具箱功能"
        },
        {
            "启用手柄触发",
            "啟用手掣觸發"
        },
        {
            "自动识别键鼠与手柄；默认 Xbox 布局",
            "自動識別鍵鼠與手掣；預設 Xbox 佈局"
        },
        {
            "跳跃检测按钮",
            "跳躍偵測按鈕"
        },
        {
            "攻击检测按钮",
            "攻擊偵測按鈕"
        },
        {
            "等待 Xbox 手柄连接",
            "等待 Xbox 手掣連接"
        },
        {
            "Xbox 手柄已连接",
            "Xbox 手掣已連接"
        },
        {
            "跳劈：先按跳跃，再按攻击，或同时按下",
            "跳劈：先按跳躍，再按攻擊，或同時按下"
        },
        {
            "按钮保留游戏原操作；请使用未占用的组合",
            "按鈕保留遊戲原操作；請使用未佔用的組合"
        },
        {
            "设置手柄按钮",
            "設定手掣按鈕"
        },
        {
            "先松开按钮，再按下要绑定的按钮或组合，松开后确认。",
            "先放開按鈕，再按要綁定的按鈕或組合，放開後確認"
        },
        {
            "清除绑定",
            "清除綁定"
        },
        {
            "取消",
            "取消"
        },
        {
            "未绑定",
            "未綁定"
        },
        {
            "手柄操作请使用不同的按钮组合。",
            "手掣操作請使用不同且不重疊的按鈕組合"
        },
        {
            "自动识别：Xbox 手柄",
            "自動識別：Xbox 手掣"
        },
        {
            "自动识别：键盘 / 鼠标",
            "自動識別：鍵盤 / 滑鼠"
        },
        {
            "正在等待连接",
            "正在等待連接"
        },
        {
            "已连接",
            "已連接"
        },
        {
            "生命：-- / --\n灵魂：-- / --",
            "生命：-- / --\n靈魂：-- / --"
        },
        {
            "右键闪避",
            "右鍵閃避"
        },
        {
            "启用右键闪避",
            "啓用右鍵閃避"
        },
        {
            "仅在游戏前台生效；暂停后恢复普通右键。",
            "僅在遊戲前台生效；暫停後恢復普通右鍵。"
        },
        {
            "闪避键",
            "閃避鍵"
        },
        {
            "与游戏内的定向闪避绑定保持一致。",
            "與遊戲內的定向閃避綁定保持一致。"
        },
        {
            "远程攻击",
            "遠程攻擊"
        },
        {
            "先按住 Shift，再按鼠标右键；可持续蓄力。",
            "先按住 Shift，再按鼠標右鍵；可持續蓄力。"
        },
        {
            "右键闪避，Shift + 右键远程攻击。",
            "右鍵閃避，Shift + 右鍵遠程攻擊。"
        },
        {
            "右键监听失败，请重新打开工具箱。",
            "右鍵監聽失敗，請重新打開工具箱。"
        },
        {
            "药水备用判断槽位",
            "藥水備用判斷槽位"
        },
        {
            "只看指定槽位",
            "只看指定槽位"
        },
        {
            "{0} 槽位 · 灵魂消耗：{1}",
            "{0} 槽位 · 靈魂消耗：{1}"
        },
        {
            "法器不可用时使用药水",
            "法器不可用時使用藥水"
        },
        {
            "后台自动使用",
            "後台自動使用"
        },
        {
            "切出游戏继续检测和使用；组合宏、跳劈仅在前台触发。",
            "切出遊戲繼續檢測和使用；組合宏、跳劈僅在前台觸發。"
        },
        {
            "法器与药水冷却",
            "法器與藥水冷卻"
        },
        {
            "等待冷却数据",
            "等待冷卻數據"
        },
        {
            "已就绪",
            "已就緒"
        },
        {
            "冷却中",
            "冷卻中"
        },
        {
            "暂不可用",
            "暫不可用"
        },
        {
            "药水",
            "藥水"
        },
        {
            "（冷却时长 {0:0.#} 秒）",
            "（冷卻時長 {0:0.#} 秒）"
        },
        {
            "游戏窗口已变化，等待重新连接。",
            "遊戲窗口已變化，等待重新連接。"
        },
        {
            "后台按键发送失败，请确认运行权限一致。",
            "後台按鍵發送失敗，請確認運行權限一致。"
        },
        {
            "连接后自动开始并持续检测；进菜单前按 F9 停止。",
            "連接後自動開始並持續檢測；進菜單前按 F9 停止。"
        },
        {
            "已开启，持续检测血量与冷却。进菜单前按 F9 停止。",
            "已開啓，持續檢測血量與冷卻。進菜單前按 F9 停止。"
        },
        {
            "持续等待角色进入关卡，自动重新连接。",
            "持續等待角色進入關卡，自動重新連接。"
        },
        {
            "后台持续检测，低血量时自动使用；F9 停止。",
            "後台持續檢測，低血量時自動使用；F9 停止。"
        },
        {
            "后台持续检测，返回游戏后自动使用。",
            "後台持續檢測，返回遊戲後自動使用。"
        },
        {
            "法器不可用 → 药水回血",
            "法器不可用 → 藥水回血"
        },
        {
            "冷却界面已变化，重新读取。",
            "冷卻界面已變化，重新讀取。"
        },
        {
            "自动法器",
            "自動法器"
        },
        {
            "低血量时自动使用法器",
            "低血量時自動使用法器"
        },
        {
            "生命低于设定比例时，同时使用已选法器。",
            "生命低於設定比例時，同時使用已選法器。"
        },
        {
            "低于设定血量时，自动组合使用已选法器。",
            "低於設定血量時，自動組合使用已選法器。"
        },
        {
            "使用法器",
            "使用法器"
        },
        {
            "按组合计算总消耗",
            "按組合計算總消耗"
        },
        {
            "手动组合总消耗：{0} 灵魂",
            "手動組合總消耗：{0} 靈魂"
        },
        {
            "组合：{0} · 总消耗 {1} 灵魂",
            "組合：{0} · 總消耗 {1} 靈魂"
        },
        {
            "低血量法器组合",
            "低血量法器組合"
        },
        {
            "请勾选低血量时使用的法器。",
            "請勾選低血量時使用的法器。"
        },
        {
            "药水键不能与自动使用的法器键相同。",
            "藥水鍵不能與自動使用的法器鍵相同。"
        },
        {
            "连接成功后自动开始；运行时点击此按钮暂停或继续。",
            "連接成功後自動開始；運行時點擊此按鈕暫停或繼續。"
        },
        {
            "连接后自动开始。切出游戏暂停按键；进菜单前按 F9 停止。",
            "連接後自動開始。切出遊戲暫停按鍵；進菜單前按 F9 停止。"
        },
        {
            "MCD2A",
            "MCD2A"
        },
        {
            "MCD2A已在运行。",
            "MCD2A已在運行。"
        },
        {
            "功能设置",
            "功能設置"
        },
        {
            "按你的游玩习惯配置",
            "按你的遊玩習慣配置"
        },
        {
            "自动回血",
            "自動回血"
        },
        {
            "法器组合",
            "法器組合"
        },
        {
            "跳劈辅助",
            "跳劈輔助"
        },
        {
            "快捷操作",
            "快捷操作"
        },
        {
            "F8  开始 / 暂停",
            "F8  開始 / 暫停"
        },
        {
            "F9  立即停止",
            "F9  立即停止"
        },
        {
            "设置血量时自动使用法器",
            "設置血量時自動使用法器"
        },
        {
            "正在连接…",
            "正在連接…"
        },
        {
            "F11 全屏 / 还原",
            "F11 全屏 / 還原"
        },
        {
            "开始 / 暂停",
            "開始 / 暫停"
        },
        {
            "立即停止",
            "立即停止"
        },
        {
            "全屏 / 还原",
            "全屏 / 還原"
        },
        {
            "按血量比例自动使用回血法器。",
            "按血量比例自動使用回血法器。"
        },
        {
            "自动使用回血法器",
            "自動使用回血法器"
        },
        {
            "生命低于设定比例时，使用指定法器。",
            "生命低於設定比例時，使用指定法器。"
        },
        {
            "血量阈值",
            "血量閾值"
        },
        {
            "低于此百分比时触发",
            "低於此百分比時觸發"
        },
        {
            "回血法器",
            "回血法器"
        },
        {
            "请将回血法器装备到自定义键对应槽位。",
            "請將回血法器裝備到自定義鍵對應槽位。"
        },
        {
            "1 槽位",
            "1 槽位"
        },
        {
            "2 槽位",
            "2 槽位"
        },
        {
            "3 槽位",
            "3 槽位"
        },
        {
            "自定义键",
            "自定義鍵"
        },
        {
            "灵魂不足时使用药水",
            "靈魂不足時使用藥水"
        },
        {
            "法器：等待连接 · 消耗：等待读取",
            "法器：等待連接 · 消耗：等待讀取"
        },
        {
            "自动读取灵魂消耗",
            "自動讀取靈魂消耗"
        },
        {
            "跟随所选槽位",
            "跟隨所選槽位"
        },
        {
            "重试间隔",
            "重試間隔"
        },
        {
            "法器和药水共用此尝试间隔",
            "法器和藥水共用此嘗試間隔"
        },
        {
            "秒",
            "秒"
        },
        {
            "空格",
            "空格"
        },
        {
            "同时释放多个法器",
            "同時釋放多個法器"
        },
        {
            "勾选槽位，使用一个触发键组合释放。",
            "勾選槽位，使用一個觸發鍵組合釋放。"
        },
        {
            "组合使用你选中的法器槽位。",
            "組合使用你選中的法器槽位。"
        },
        {
            "法器组合宏",
            "法器組合宏"
        },
        {
            "为一个按键分配组合，按下时使用已选法器。",
            "為一個按鍵分配組合，按下時使用已選法器。"
        },
        {
            "触发键",
            "觸發鍵"
        },
        {
            "选择要同时使用的法器",
            "選擇要同時使用的法器"
        },
        {
            "自定义法器键",
            "自定義法器鍵"
        },
        {
            "启用跳劈辅助",
            "啓用跳劈輔助"
        },
        {
            "检测跳跃和左键操作，再补按指定按键。",
            "檢測跳躍和左鍵操作，再補按指定按鍵。"
        },
        {
            "跳跃 + 鼠标左键 → 自动补按 Q。",
            "跳躍 + 鼠標左鍵 → 自動補按 Q。"
        },
        {
            "检测组合 → 自动补按",
            "檢測組合 → 自動補按"
        },
        {
            "鼠标左键",
            "鼠標左鍵"
        },
        {
            "补按延迟",
            "補按延遲"
        },
        {
            "检测到组合操作后，再等待此时长。",
            "檢測到組合操作後，再等待此時長。"
        },
        {
            "先跳跃，再点击左键，或同时按下。",
            "先跳躍，再點擊左鍵，或同時按下。"
        },
        {
            "间隔需在 0.35 秒内；单独按键不会触发。",
            "間隔需在 0.35 秒內；單獨按鍵不會觸發。"
        },
        {
            "生命：等待连接",
            "生命：等待連接"
        },
        {
            "生命：等待重新连接",
            "生命：等待重新連接"
        },
        {
            "连接游戏",
            "連接遊戲"
        },
        {
            "开始（F8）",
            "開始（F8）"
        },
        {
            "暂停（F8）",
            "暫停（F8）"
        },
        {
            "停止（F9）",
            "停止（F9）"
        },
        {
            "等待连接。切出游戏暂停按键；进菜单前按 F9 停止。",
            "等待連接。切出遊戲暫停按鍵；進菜單前按 F9 停止。"
        },
        {
            "法器释放仍受游戏冷却、灵魂和施法动作限制。",
            "法器釋放仍受遊戲冷卻、靈魂和施法動作限制。"
        },
        {
            "设置已更新，按 F8 开始。",
            "設置已更新，按 F8 開始。"
        },
        {
            "已连接，按 F8 开始。",
            "已連接，按 F8 開始。"
        },
        {
            "已停止，按 F8 开始。",
            "已停止，按 F8 開始。"
        },
        {
            "已开启。切出游戏暂停按键；进菜单前按 F9 停止。",
            "已開啓。切出遊戲暫停按鍵；進菜單前按 F9 停止。"
        },
        {
            "工具箱已开启，正在监测。进菜单前按 F9 停止。",
            "工具箱已開啓，正在監測。進菜單前按 F9 停止。"
        },
        {
            "游戏不在前台，已暂停按键。",
            "遊戲不在前台，已暫停按鍵。"
        },
        {
            "正在读取游戏血量…",
            "正在讀取遊戲血量…"
        },
        {
            "未读取",
            "未讀取"
        },
        {
            "F8 / F9 被占用，请关闭旧工具后重新打开，或使用窗口按钮。",
            "F8 / F9 被佔用，請關閉舊工具後重新打開，或使用窗口按鈕。"
        },
        {
            "请关闭旧 AutoHeal 或修改器，再按 F8 开始。",
            "請關閉舊 AutoHeal 或修改器，再按 F8 開始。"
        },
        {
            "设置键位",
            "設置鍵位"
        },
        {
            "请按下要绑定的键。\nF8、F9 保留；Esc 取消。",
            "請按下要綁定的鍵。\nF8、F9 保留；Esc 取消。"
        },
        {
            "选择槽位",
            "選擇槽位"
        },
        {
            " 槽位",
            " 槽位"
        },
        {
            "回复图腾",
            "回覆圖騰"
        },
        {
            "灵魂治疗器",
            "靈魂治療器"
        },
        {
            "灵魂收割器",
            "靈魂收割器"
        },
        {
            "腐化种子",
            "腐化種子"
        },
        {
            "手动消耗：{0} 灵魂 · 灵魂不足时改用药水",
            "手動消耗：{0} 靈魂 · 靈魂不足時改用藥水"
        },
        {
            "{0} 槽位 · {1} · 消耗 {2} 灵魂",
            "{0} 槽位 · {1} · 消耗 {2} 靈魂"
        },
        {
            "生命：{0:0.#} / {1:0.#}（{2:0.#}%）\n灵魂：{3} / {4}",
            "生命：{0:0.#} / {1:0.#}（{2:0.#}%）\n靈魂：{3} / {4}"
        },
        {
            "生命：{0:0.#} / {1:0.#}（{2:0.#}%）\n灵魂：{3:0.#} / {4:0.#}",
            "生命：{0:0.#} / {1:0.#}（{2:0.#}%）\n靈魂：{3:0.#} / {4:0.#}"
        },
        {
            "{0:HH:mm:ss} {1}：{2} · 共 {3} 次",
            "{0:HH:mm:ss} {1}：{2} · 共 {3} 次"
        },
        {
            "法器连发",
            "法器連發"
        },
        {
            "跳劈补按",
            "跳劈補按"
        },
        {
            "法器回血",
            "法器回血"
        },
        {
            "灵魂不足 → 药水回血",
            "靈魂不足 → 藥水回血"
        },
        {
            "请至少启用一项功能。",
            "請至少啓用一項功能。"
        },
        {
            "三个法器槽位请使用不同的键。",
            "三個法器槽位請使用不同的鍵。"
        },
        {
            "请勾选需要同时使用的法器槽位。",
            "請勾選需要同時使用的法器槽位。"
        },
        {
            "法器连发的触发键不能与选中的法器键相同。",
            "法器連發的觸發鍵不能與選中的法器鍵相同。"
        },
        {
            "跳劈的检测键与补按键请使用不同的键。",
            "跳劈的檢測鍵與補按鍵請使用不同的鍵。"
        },
        {
            "法器连发与跳劈请使用不同的触发键。",
            "法器連發與跳劈請使用不同的觸發鍵。"
        },
        {
            "法器连发的触发键不能与自动发送的按键相同。",
            "法器連發的觸發鍵不能與自動發送的按鍵相同。"
        },
        {
            "跳劈检测键不能与自动发送的按键相同。",
            "跳劈檢測鍵不能與自動發送的按鍵相同。"
        },
        {
            "这个按键无法发送，请换一个键。",
            "這個按鍵無法發送，請換一個鍵。"
        },
        {
            "按键发送失败，已暂停。请确认工具箱与游戏的运行权限一致。",
            "按鍵發送失敗，已暫停。請確認工具箱與遊戲的運行權限一致。"
        },
        {
            "无法连接游戏进程。",
            "無法連接遊戲進程。"
        },
        {
            "读取地址验证失败。",
            "讀取地址驗證失敗。"
        },
        {
            "游戏正在切换场景或已退出，请重新连接。",
            "遊戲正在切換場景或已退出，請重新連接。"
        },
        {
            "游戏数值验证失败。",
            "遊戲數值驗證失敗。"
        },
        {
            "请启动游戏并进入关卡，再连接。",
            "請啓動遊戲並進入關卡，再連接。"
        },
        {
            "游戏版本不匹配，需要重新适配。",
            "遊戲版本不匹配，需要重新適配。"
        },
        {
            "游戏结构校验失败。",
            "遊戲結構校驗失敗。"
        },
        {
            "名称索引无效。",
            "名稱索引無效。"
        },
        {
            "名称长度无效。",
            "名稱長度無效。"
        },
        {
            "属性布局不匹配：",
            "屬性佈局不匹配："
        },
        {
            "找不到游戏属性：",
            "找不到遊戲屬性："
        },
        {
            "角色对象不可用。",
            "角色對象不可用。"
        },
        {
            "角色对象已变化。",
            "角色對象已變化。"
        },
        {
            "对象表验证失败。",
            "對象表驗證失敗。"
        },
        {
            "未找到唯一的本地玩家，请进入单人关卡后重新连接。",
            "未找到唯一的本地玩家，請進入單人關卡後重新連接。"
        },
        {
            "游戏状态已变化，请重新连接。",
            "遊戲狀態已變化，請重新連接。"
        },
        {
            "等待玩家进入关卡。",
            "等待玩家進入關卡。"
        },
        {
            "玩家属性归属校验失败。",
            "玩家屬性歸屬校驗失敗。"
        },
        {
            "玩家属性列表无效。",
            "玩家屬性列表無效。"
        },
        {
            "血量读数暂不可用。",
            "血量讀數暫不可用。"
        },
        {
            "法器布局验证失败。",
            "法器佈局驗證失敗。"
        },
        {
            "自定义键无法确定槽位，请关闭自动读取并填写消耗。",
            "自定義鍵無法確定槽位，請關閉自動讀取並填寫消耗。"
        },
        {
            "法器归属验证失败。",
            "法器歸屬驗證失敗。"
        },
        {
            "法器列表暂不可用。",
            "法器列表暫不可用。"
        },
        {
            "所选法器槽位为空或暂不可用。",
            "所選法器槽位為空或暫不可用。"
        },
        {
            "法器槽位验证失败。",
            "法器槽位驗證失敗。"
        },
        {
            "没有找到所选法器。",
            "沒有找到所選法器。"
        },
        {
            "法器消耗暂不可读，请重新连接或关闭自动读取并填写消耗。",
            "法器消耗暫不可讀，請重新連接或關閉自動讀取並填寫消耗。"
        },
    };
    static readonly Dictionary<string, string> Tw = new Dictionary<string, string>
    {
        {
            "绿宝石 {0}：需进入游戏原生拾取范围",
            "綠寶石 {0}：需進入遊戲原生拾取範圍"
        },
        {
            "检测到多个游戏进程，请只保留一个游戏实例后连接。",
            "偵測到多個遊戲處理程序，請只保留一個遊戲實例後連線。"
        },
        {
            "攻击预警（实验）",
            "攻擊預警（實驗）"
        },
        {
            "攻击动作或直线弹道接近时，提前使用已选法器",
            "攻擊動作或直線彈道接近時，提前使用已選法器"
        },
        {
            "药水仍受血量阈值限制；仅在游戏前台生效",
            "藥水仍受血量閾值限制；僅在遊戲前景生效"
        },
        {
            "等待攻击预警数据",
            "等待攻擊預警資料"
        },
        {
            "附近敌人 {0} · 弹道 {1} · 危险 {2}",
            "附近敵人 {0} · 彈道 {1} · 危險 {2}"
        },
        {
            "预警读数不可用，保持原有血量规则",
            "預警讀數不可用，維持原有血量規則"
        },
        {
            "攻击预警自动使用",
            "攻擊預警自動使用"
        },
        {
            "游戏映像验证失败。",
            "遊戲映像驗證失敗。"
        },
        {
            "游戏映像区段无效。",
            "遊戲映像區段無效。"
        },
        {
            "找不到经过验证的游戏名称表，需要更新适配。",
            "找不到經過驗證的遊戲名稱表，需要更新相容性。"
        },
        {
            "找不到经过验证的游戏对象表，需要更新适配。",
            "找不到經過驗證的遊戲物件表，需要更新相容性。"
        },
        {
            "使用法器（不勾选时仅使用药水）",
            "使用法器（不勾選時僅使用藥水）"
        },
        {
            "未选择法器，仅按血量与药水冷却自动使用药水。",
            "未選擇法器，僅按血量與藥水冷卻自動使用藥水。"
        },
        {
            "低血量 → 药水回血",
            "低血量 → 藥水回血"
        },
        {
            "许可与致谢",
            "授權與致謝"
        },
        {
            "关闭",
            "關閉"
        },
        {
            "导出日志",
            "匯出日誌"
        },
        {
            "导出诊断日志",
            "匯出診斷日誌"
        },
        {
            "文本文件 (*.txt)|*.txt",
            "文字檔案 (*.txt)|*.txt"
        },
        {
            "日志已导出，可附在群内或 GitHub Issues 反馈中。",
            "日誌已匯出，可附於群組或 GitHub Issues 的問題回報。"
        },
        {
            "导出失败，请选择其他保存位置。\n{0}",
            "匯出失敗，請選擇其他儲存位置。\n{0}"
        },
        {
            "发生错误，已停止。请导出日志反馈。",
            "發生錯誤，已停止。請匯出日誌回報。"
        },
        {
            "游戏手柄键位未读取，请检查游戏内绑定。",
            "未能讀取遊戲手把按鍵，請檢查遊戲內設定。"
        },
        {
            "游戏键位与工具箱触发键冲突，请更换触发键。",
            "遊戲按鍵與工具箱觸發鍵重複，請更換觸發鍵。"
        },
        {
            "跟随游戏键位：{0} → {1}\n使用对应的键盘操作；手柄按钮请在游戏内修改。",
            "跟隨遊戲按鍵：{0} → {1}\n使用對應鍵盤操作；手把按鈕請在遊戲內修改。"
        },
        {
            "键盘 / 鼠标",
            "鍵盤 / 滑鼠"
        },
        {
            "键盘快捷键保持 F8 / F9；可选择 Xbox 绑定手柄",
            "鍵盤快捷鍵維持 F8 / F9；可選擇 Xbox 綁定手把"
        },
        {
            "请按下要绑定的键；鼠标绑定请点击此提示区。Esc 取消",
            "請按下要綁定的鍵；滑鼠綁定請點此提示區。Esc 取消"
        },
        {
            "清除手柄绑定",
            "清除手把綁定"
        },
        {
            "鼠标右键",
            "滑鼠右鍵"
        },
        {
            "手柄",
            "手把"
        },
        {
            "Xbox 手柄绑定",
            "Xbox 手把綁定"
        },
        {
            "用手柄按钮或组合触发工具箱功能",
            "用手把按鈕或組合觸發工具箱功能"
        },
        {
            "启用手柄触发",
            "啟用手把觸發"
        },
        {
            "自动识别键鼠与手柄；默认 Xbox 布局",
            "自動辨識鍵鼠與手把；預設 Xbox 配置"
        },
        {
            "跳跃检测按钮",
            "跳躍偵測按鈕"
        },
        {
            "攻击检测按钮",
            "攻擊偵測按鈕"
        },
        {
            "等待 Xbox 手柄连接",
            "等待 Xbox 手把連線"
        },
        {
            "Xbox 手柄已连接",
            "Xbox 手把已連線"
        },
        {
            "跳劈：先按跳跃，再按攻击，或同时按下",
            "跳劈：先按跳躍，再按攻擊，或同時按下"
        },
        {
            "按钮保留游戏原操作；请使用未占用的组合",
            "按鈕保留遊戲原操作；請使用未占用的組合"
        },
        {
            "设置手柄按钮",
            "設定手把按鈕"
        },
        {
            "先松开按钮，再按下要绑定的按钮或组合，松开后确认。",
            "先放開按鈕，再按要綁定的按鈕或組合，放開後確認"
        },
        {
            "清除绑定",
            "清除綁定"
        },
        {
            "取消",
            "取消"
        },
        {
            "未绑定",
            "未綁定"
        },
        {
            "手柄操作请使用不同的按钮组合。",
            "手把操作請使用不同且不重疊的按鈕組合"
        },
        {
            "自动识别：Xbox 手柄",
            "自動辨識：Xbox 手把"
        },
        {
            "自动识别：键盘 / 鼠标",
            "自動辨識：鍵盤 / 滑鼠"
        },
        {
            "正在等待连接",
            "正在等待連線"
        },
        {
            "已连接",
            "已連線"
        },
        {
            "生命：-- / --\n灵魂：-- / --",
            "生命：-- / --\n靈魂：-- / --"
        },
        {
            "右键闪避",
            "右鍵閃避"
        },
        {
            "启用右键闪避",
            "啟用右鍵閃避"
        },
        {
            "仅在游戏前台生效；暂停后恢复普通右键。",
            "僅在遊戲前臺生效；暫停後恢復普通右鍵。"
        },
        {
            "闪避键",
            "閃避鍵"
        },
        {
            "与游戏内的定向闪避绑定保持一致。",
            "與遊戲內的定向閃避繫結保持一致。"
        },
        {
            "远程攻击",
            "遠端攻擊"
        },
        {
            "先按住 Shift，再按鼠标右键；可持续蓄力。",
            "先按住 Shift，再按滑鼠右鍵；可持續蓄力。"
        },
        {
            "右键闪避，Shift + 右键远程攻击。",
            "右鍵閃避，Shift + 右鍵遠端攻擊。"
        },
        {
            "右键监听失败，请重新打开工具箱。",
            "右鍵監聽失敗，請重新開啟工具箱。"
        },
        {
            "药水备用判断槽位",
            "藥水備用判斷槽位"
        },
        {
            "只看指定槽位",
            "只看指定槽位"
        },
        {
            "{0} 槽位 · 灵魂消耗：{1}",
            "{0} 槽位 · 靈魂消耗：{1}"
        },
        {
            "法器不可用时使用药水",
            "法器不可用時使用藥水"
        },
        {
            "后台自动使用",
            "後臺自動使用"
        },
        {
            "切出游戏继续检测和使用；组合宏、跳劈仅在前台触发。",
            "切出遊戲繼續檢測和使用；組合宏、跳劈僅在前臺觸發。"
        },
        {
            "法器与药水冷却",
            "法器與藥水冷卻"
        },
        {
            "等待冷却数据",
            "等待冷卻資料"
        },
        {
            "已就绪",
            "已就緒"
        },
        {
            "冷却中",
            "冷卻中"
        },
        {
            "暂不可用",
            "暫不可用"
        },
        {
            "药水",
            "藥水"
        },
        {
            "（冷却时长 {0:0.#} 秒）",
            "（冷卻時長 {0:0.#} 秒）"
        },
        {
            "游戏窗口已变化，等待重新连接。",
            "遊戲視窗已變化，等待重新連線。"
        },
        {
            "后台按键发送失败，请确认运行权限一致。",
            "後臺按鍵傳送失敗，請確認執行許可權一致。"
        },
        {
            "连接后自动开始并持续检测；进菜单前按 F9 停止。",
            "連線後自動開始並持續檢測；進選單前按 F9 停止。"
        },
        {
            "已开启，持续检测血量与冷却。进菜单前按 F9 停止。",
            "已開啟，持續檢測血量與冷卻。進選單前按 F9 停止。"
        },
        {
            "持续等待角色进入关卡，自动重新连接。",
            "持續等待角色進入關卡，自動重新連線。"
        },
        {
            "后台持续检测，低血量时自动使用；F9 停止。",
            "後臺持續檢測，低血量時自動使用；F9 停止。"
        },
        {
            "后台持续检测，返回游戏后自动使用。",
            "後臺持續檢測，返回遊戲後自動使用。"
        },
        {
            "法器不可用 → 药水回血",
            "法器不可用 → 藥水回血"
        },
        {
            "冷却界面已变化，重新读取。",
            "冷卻介面已變化，重新讀取。"
        },
        {
            "自动法器",
            "自動法器"
        },
        {
            "低血量时自动使用法器",
            "低血量時自動使用法器"
        },
        {
            "生命低于设定比例时，同时使用已选法器。",
            "生命低於設定比例時，同時使用已選法器。"
        },
        {
            "低于设定血量时，自动组合使用已选法器。",
            "低於設定血量時，自動組合使用已選法器。"
        },
        {
            "使用法器",
            "使用法器"
        },
        {
            "按组合计算总消耗",
            "按組合計算總消耗"
        },
        {
            "手动组合总消耗：{0} 灵魂",
            "手動組合總消耗：{0} 靈魂"
        },
        {
            "组合：{0} · 总消耗 {1} 灵魂",
            "組合：{0} · 總消耗 {1} 靈魂"
        },
        {
            "低血量法器组合",
            "低血量法器組合"
        },
        {
            "请勾选低血量时使用的法器。",
            "請勾選低血量時使用的法器。"
        },
        {
            "药水键不能与自动使用的法器键相同。",
            "藥水鍵不能與自動使用的法器鍵相同。"
        },
        {
            "连接成功后自动开始；运行时点击此按钮暂停或继续。",
            "連線成功後自動開始；執行時點選此按鈕暫停或繼續。"
        },
        {
            "连接后自动开始。切出游戏暂停按键；进菜单前按 F9 停止。",
            "連線後自動開始。切出遊戲暫停按鍵；進選單前按 F9 停止。"
        },
        {
            "MCD2A",
            "MCD2A"
        },
        {
            "MCD2A已在运行。",
            "MCD2A已在執行。"
        },
        {
            "功能设置",
            "功能設定"
        },
        {
            "按你的游玩习惯配置",
            "按你的遊玩習慣配置"
        },
        {
            "自动回血",
            "自動回血"
        },
        {
            "法器组合",
            "法器組合"
        },
        {
            "跳劈辅助",
            "跳劈輔助"
        },
        {
            "快捷操作",
            "快捷操作"
        },
        {
            "F8  开始 / 暂停",
            "F8  開始 / 暫停"
        },
        {
            "F9  立即停止",
            "F9  立即停止"
        },
        {
            "设置血量时自动使用法器",
            "設定血量時自動使用法器"
        },
        {
            "正在连接…",
            "正在連線…"
        },
        {
            "F11 全屏 / 还原",
            "F11 全屏 / 還原"
        },
        {
            "开始 / 暂停",
            "開始 / 暫停"
        },
        {
            "立即停止",
            "立即停止"
        },
        {
            "全屏 / 还原",
            "全屏 / 還原"
        },
        {
            "按血量比例自动使用回血法器。",
            "按血量比例自動使用回血法器。"
        },
        {
            "自动使用回血法器",
            "自動使用回血法器"
        },
        {
            "生命低于设定比例时，使用指定法器。",
            "生命低於設定比例時，使用指定法器。"
        },
        {
            "血量阈值",
            "血量閾值"
        },
        {
            "低于此百分比时触发",
            "低於此百分比時觸發"
        },
        {
            "回血法器",
            "回血法器"
        },
        {
            "请将回血法器装备到自定义键对应槽位。",
            "請將回血法器裝備到自定義鍵對應槽位。"
        },
        {
            "1 槽位",
            "1 槽位"
        },
        {
            "2 槽位",
            "2 槽位"
        },
        {
            "3 槽位",
            "3 槽位"
        },
        {
            "自定义键",
            "自定義鍵"
        },
        {
            "灵魂不足时使用药水",
            "靈魂不足時使用藥水"
        },
        {
            "法器：等待连接 · 消耗：等待读取",
            "法器：等待連線 · 消耗：等待讀取"
        },
        {
            "自动读取灵魂消耗",
            "自動讀取靈魂消耗"
        },
        {
            "跟随所选槽位",
            "跟隨所選槽位"
        },
        {
            "重试间隔",
            "重試間隔"
        },
        {
            "法器和药水共用此尝试间隔",
            "法器和藥水共用此嘗試間隔"
        },
        {
            "秒",
            "秒"
        },
        {
            "空格",
            "空格"
        },
        {
            "同时释放多个法器",
            "同時釋放多個法器"
        },
        {
            "勾选槽位，使用一个触发键组合释放。",
            "勾選槽位，使用一個觸發鍵組合釋放。"
        },
        {
            "组合使用你选中的法器槽位。",
            "組合使用你選中的法器槽位。"
        },
        {
            "法器组合宏",
            "法器組合宏"
        },
        {
            "为一个按键分配组合，按下时使用已选法器。",
            "為一個按鍵分配組合，按下時使用已選法器。"
        },
        {
            "触发键",
            "觸發鍵"
        },
        {
            "选择要同时使用的法器",
            "選擇要同時使用的法器"
        },
        {
            "自定义法器键",
            "自定義法器鍵"
        },
        {
            "启用跳劈辅助",
            "啟用跳劈輔助"
        },
        {
            "检测跳跃和左键操作，再补按指定按键。",
            "檢測跳躍和左鍵操作，再補按指定按鍵。"
        },
        {
            "跳跃 + 鼠标左键 → 自动补按 Q。",
            "跳躍 + 滑鼠左鍵 → 自動補按 Q。"
        },
        {
            "检测组合 → 自动补按",
            "檢測組合 → 自動補按"
        },
        {
            "鼠标左键",
            "滑鼠左鍵"
        },
        {
            "补按延迟",
            "補按延遲"
        },
        {
            "检测到组合操作后，再等待此时长。",
            "檢測到組合操作後，再等待此時長。"
        },
        {
            "先跳跃，再点击左键，或同时按下。",
            "先跳躍，再點選左鍵，或同時按下。"
        },
        {
            "间隔需在 0.35 秒内；单独按键不会触发。",
            "間隔需在 0.35 秒內；單獨按鍵不會觸發。"
        },
        {
            "生命：等待连接",
            "生命：等待連線"
        },
        {
            "生命：等待重新连接",
            "生命：等待重新連線"
        },
        {
            "连接游戏",
            "連線遊戲"
        },
        {
            "开始（F8）",
            "開始（F8）"
        },
        {
            "暂停（F8）",
            "暫停（F8）"
        },
        {
            "停止（F9）",
            "停止（F9）"
        },
        {
            "等待连接。切出游戏暂停按键；进菜单前按 F9 停止。",
            "等待連線。切出遊戲暫停按鍵；進選單前按 F9 停止。"
        },
        {
            "法器释放仍受游戏冷却、灵魂和施法动作限制。",
            "法器釋放仍受遊戲冷卻、靈魂和施法動作限制。"
        },
        {
            "设置已更新，按 F8 开始。",
            "設定已更新，按 F8 開始。"
        },
        {
            "已连接，按 F8 开始。",
            "已連線，按 F8 開始。"
        },
        {
            "已停止，按 F8 开始。",
            "已停止，按 F8 開始。"
        },
        {
            "已开启。切出游戏暂停按键；进菜单前按 F9 停止。",
            "已開啟。切出遊戲暫停按鍵；進選單前按 F9 停止。"
        },
        {
            "工具箱已开启，正在监测。进菜单前按 F9 停止。",
            "工具箱已開啟，正在監測。進選單前按 F9 停止。"
        },
        {
            "游戏不在前台，已暂停按键。",
            "遊戲不在前臺，已暫停按鍵。"
        },
        {
            "正在读取游戏血量…",
            "正在讀取遊戲血量…"
        },
        {
            "未读取",
            "未讀取"
        },
        {
            "F8 / F9 被占用，请关闭旧工具后重新打开，或使用窗口按钮。",
            "F8 / F9 被佔用，請關閉舊工具後重新開啟，或使用視窗按鈕。"
        },
        {
            "请关闭旧 AutoHeal 或修改器，再按 F8 开始。",
            "請關閉舊 AutoHeal 或修改器，再按 F8 開始。"
        },
        {
            "设置键位",
            "設定鍵位"
        },
        {
            "请按下要绑定的键。\nF8、F9 保留；Esc 取消。",
            "請按下要繫結的鍵。\nF8、F9 保留；Esc 取消。"
        },
        {
            "选择槽位",
            "選擇槽位"
        },
        {
            " 槽位",
            " 槽位"
        },
        {
            "回复图腾",
            "回覆圖騰"
        },
        {
            "灵魂治疗器",
            "靈魂治療器"
        },
        {
            "灵魂收割器",
            "靈魂收割器"
        },
        {
            "腐化种子",
            "腐化種子"
        },
        {
            "手动消耗：{0} 灵魂 · 灵魂不足时改用药水",
            "手動消耗：{0} 靈魂 · 靈魂不足時改用藥水"
        },
        {
            "{0} 槽位 · {1} · 消耗 {2} 灵魂",
            "{0} 槽位 · {1} · 消耗 {2} 靈魂"
        },
        {
            "生命：{0:0.#} / {1:0.#}（{2:0.#}%）\n灵魂：{3} / {4}",
            "生命：{0:0.#} / {1:0.#}（{2:0.#}%）\n靈魂：{3} / {4}"
        },
        {
            "生命：{0:0.#} / {1:0.#}（{2:0.#}%）\n灵魂：{3:0.#} / {4:0.#}",
            "生命：{0:0.#} / {1:0.#}（{2:0.#}%）\n靈魂：{3:0.#} / {4:0.#}"
        },
        {
            "{0:HH:mm:ss} {1}：{2} · 共 {3} 次",
            "{0:HH:mm:ss} {1}：{2} · 共 {3} 次"
        },
        {
            "法器连发",
            "法器連發"
        },
        {
            "跳劈补按",
            "跳劈補按"
        },
        {
            "法器回血",
            "法器回血"
        },
        {
            "灵魂不足 → 药水回血",
            "靈魂不足 → 藥水回血"
        },
        {
            "请至少启用一项功能。",
            "請至少啟用一項功能。"
        },
        {
            "三个法器槽位请使用不同的键。",
            "三個法器槽位請使用不同的鍵。"
        },
        {
            "请勾选需要同时使用的法器槽位。",
            "請勾選需要同時使用的法器槽位。"
        },
        {
            "法器连发的触发键不能与选中的法器键相同。",
            "法器連發的觸發鍵不能與選中的法器鍵相同。"
        },
        {
            "跳劈的检测键与补按键请使用不同的键。",
            "跳劈的檢測鍵與補按鍵請使用不同的鍵。"
        },
        {
            "法器连发与跳劈请使用不同的触发键。",
            "法器連發與跳劈請使用不同的觸發鍵。"
        },
        {
            "法器连发的触发键不能与自动发送的按键相同。",
            "法器連發的觸發鍵不能與自動傳送的按鍵相同。"
        },
        {
            "跳劈检测键不能与自动发送的按键相同。",
            "跳劈檢測鍵不能與自動傳送的按鍵相同。"
        },
        {
            "这个按键无法发送，请换一个键。",
            "這個按鍵無法傳送，請換一個鍵。"
        },
        {
            "按键发送失败，已暂停。请确认工具箱与游戏的运行权限一致。",
            "按鍵傳送失敗，已暫停。請確認工具箱與遊戲的執行許可權一致。"
        },
        {
            "无法连接游戏进程。",
            "無法連線遊戲程序。"
        },
        {
            "读取地址验证失败。",
            "讀取地址驗證失敗。"
        },
        {
            "游戏正在切换场景或已退出，请重新连接。",
            "遊戲正在切換場景或已退出，請重新連線。"
        },
        {
            "游戏数值验证失败。",
            "遊戲數值驗證失敗。"
        },
        {
            "请启动游戏并进入关卡，再连接。",
            "請啟動遊戲並進入關卡，再連線。"
        },
        {
            "游戏版本不匹配，需要重新适配。",
            "遊戲版本不匹配，需要重新適配。"
        },
        {
            "游戏结构校验失败。",
            "遊戲結構校驗失敗。"
        },
        {
            "名称索引无效。",
            "名稱索引無效。"
        },
        {
            "名称长度无效。",
            "名稱長度無效。"
        },
        {
            "属性布局不匹配：",
            "屬性佈局不匹配："
        },
        {
            "找不到游戏属性：",
            "找不到遊戲屬性："
        },
        {
            "角色对象不可用。",
            "角色物件不可用。"
        },
        {
            "角色对象已变化。",
            "角色物件已變化。"
        },
        {
            "对象表验证失败。",
            "物件表驗證失敗。"
        },
        {
            "未找到唯一的本地玩家，请进入单人关卡后重新连接。",
            "未找到唯一的本地玩家，請進入單人關卡後重新連線。"
        },
        {
            "游戏状态已变化，请重新连接。",
            "遊戲狀態已變化，請重新連線。"
        },
        {
            "等待玩家进入关卡。",
            "等待玩家進入關卡。"
        },
        {
            "玩家属性归属校验失败。",
            "玩家屬性歸屬校驗失敗。"
        },
        {
            "玩家属性列表无效。",
            "玩家屬性列表無效。"
        },
        {
            "血量读数暂不可用。",
            "血量讀數暫不可用。"
        },
        {
            "法器布局验证失败。",
            "法器佈局驗證失敗。"
        },
        {
            "自定义键无法确定槽位，请关闭自动读取并填写消耗。",
            "自定義鍵無法確定槽位，請關閉自動讀取並填寫消耗。"
        },
        {
            "法器归属验证失败。",
            "法器歸屬驗證失敗。"
        },
        {
            "法器列表暂不可用。",
            "法器列表暫不可用。"
        },
        {
            "所选法器槽位为空或暂不可用。",
            "所選法器槽位為空或暫不可用。"
        },
        {
            "法器槽位验证失败。",
            "法器槽位驗證失敗。"
        },
        {
            "没有找到所选法器。",
            "沒有找到所選法器。"
        },
        {
            "法器消耗暂不可读，请重新连接或关闭自动读取并填写消耗。",
            "法器消耗暫不可讀，請重新連線或關閉自動讀取並填寫消耗。"
        },
    };
    // 把已翻译显示文字还原到规范键，供日志与比较使用。
    public static string Canonical(string value)
    {
        if (value == null)
            return "";
        foreach (var map in new[]
        {
            En,
            Ja,
            Ko,
            Hk,
            Tw
        }

        )
            foreach (var pair in map)
                if (pair.Value == value)
                    return pair.Key;
        return value;
    }

    // 取得标题显示文字，保留语言和字体差异。
    public static string Heading(string value)
    {
        value = Canonical(value);
        string result;
        return Cjk && En.TryGetValue(value, out result) ? result : T(value);
    }

    // 统一界面翻译入口，按 Language 读取主字典及补充字典。
    public static string T(string value)
    {
        value = Canonical(value);
        if (Language == 0)
            return value;
        var map = Language == 2 ? Ja : Language == 3 ? Ko : Language == 4 ? Hk : Language == 5 ? Tw : En;
        string result;
        if (map.TryGetValue(value, out result))
            return result;
        if (Language > 1 && En.TryGetValue(value, out result))
            return result;
        foreach (string prefix in new[]
        {
            "属性布局不匹配：",
            "找不到游戏属性："
        }

        )
            if (value.StartsWith(prefix, StringComparison.Ordinal))
                return map[prefix] + value.Substring(prefix.Length);
        return value;
    }
}

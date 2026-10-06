// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
// 新视觉导航和首页文案；保持简中、英文、日文、韩文、港繁、台繁的既有索引。
static partial class L10n
{
    // 中文键是统一索引，后五列按英文、日文、韩文、港繁、台繁写入；新增文字必须同列补全。
    static void RegisterOreText()
    {
        string[][] rows =
        {
            new[]
            {
                "安装或更新请前往「设置」",
                "Install or update in Settings",
                "インストールと更新は設定から",
                "설정에서 설치 또는 업데이트",
                "安裝或更新請前往「設定」",
                "安裝或更新請前往「設定」"
            },
            new[]
            {
                "未连接",
                "Disconnected",
                "未接続",
                "연결 안 됨",
                "未連接",
                "未連線"
            },
            new[]
            {
                "使用自动恢复页已选槽位；药水遵循血量阈值",
                "Use Auto recovery slots; potions follow the health threshold",
                "自動回復で選択したスロットを使用。ポーションは体力基準に従います",
                "자동 회복에서 선택한 슬롯 사용. 물약은 체력 기준을 따름",
                "使用自動恢復頁已選槽位；藥水遵循血量閾值",
                "使用自動恢復頁已選槽位；藥水遵循血量閾值"
            },
            new[]
            {
                "首页",
                "Home",
                "ホーム",
                "홈",
                "首頁",
                "首頁"
            },
            new[]
            {
                "自动恢复",
                "Auto recovery",
                "自動回復",
                "자동 회복",
                "自動恢復",
                "自動恢復"
            },
            new[]
            {
                "附近交互",
                "Nearby interaction",
                "周辺の操作",
                "주변 상호작용",
                "附近互動",
                "附近互動"
            },
            new[]
            {
                "设置",
                "Settings",
                "設定",
                "설정",
                "設定",
                "設定"
            },
            new[]
            {
                "连接游戏，选择功能，再按 F8 开始",
                "Connect, choose your features, then press F8",
                "接続して機能を選び、F8 で開始",
                "연결 후 기능을 선택하고 F8로 시작",
                "連接遊戲，選擇功能，再按 F8 開始",
                "連接遊戲，選擇功能，再按 F8 開始"
            },
            new[]
            {
                "游戏连接",
                "Game connection",
                "ゲーム接続",
                "게임 연결",
                "遊戲連接",
                "遊戲連線"
            },
            new[]
            {
                "当前运行状态",
                "Current activity",
                "現在の動作状況",
                "현재 실행 상태",
                "目前執行狀態",
                "目前執行狀態"
            },
            new[]
            {
                "组件状态",
                "Components",
                "コンポーネント",
                "구성 요소 상태",
                "組件狀態",
                "元件狀態"
            },
            new[]
            {
                "附近交互组件",
                "Nearby interaction",
                "周辺操作コンポーネント",
                "주변 상호작용 구성 요소",
                "附近互動組件",
                "附近互動元件"
            },
            new[]
            {
                "战斗组件",
                "Combat",
                "戦闘コンポーネント",
                "전투 구성 요소",
                "戰鬥組件",
                "戰鬥元件"
            },
            new[]
            {
                "装备组件",
                "Equipment",
                "装備コンポーネント",
                "장비 구성 요소",
                "裝備組件",
                "裝備元件"
            },
            new[]
            {
                "运行中",
                "Running",
                "実行中",
                "실행 중",
                "執行中",
                "執行中"
            },
            new[]
            {
                "已暂停",
                "Paused",
                "一時停止中",
                "일시 정지됨",
                "已暫停",
                "已暫停"
            },
            new[]
            {
                "尚未检测；连接并开启对应功能后读取",
                "Not checked; connect and enable the feature",
                "未確認。接続して対象機能を有効にしてください",
                "확인 전. 연결 후 해당 기능을 켜세요",
                "尚未檢測；連接並開啟對應功能後讀取",
                "尚未檢測；連線並開啟對應功能後讀取"
            },
            new[]
            {
                "配置组合、跳劈和右键闪避",
                "Configure combos, jump attack and right-click dodge",
                "コンボ・ジャンプ攻撃・右クリック回避を設定",
                "콤보, 점프 공격 및 우클릭 회피 설정",
                "設定組合、跳劈和右鍵閃避",
                "設定組合、跳劈和右鍵閃避"
            },
            new[]
            {
                "返回设置",
                "Back to settings",
                "設定へ戻る",
                "설정으로 돌아가기",
                "返回設定",
                "返回設定"
            }
        };
        var maps = new[]
        {
            En,
            Ja,
            Ko,
            Hk,
            Tw
        };
        foreach (var row in rows)
            for (int i = 0; i < maps.Length; i++)
                maps[i][row[0]] = row[i + 1];
    }
}

// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir (https://github.com/Cyanmir/Minecraft-Dungeons-II-Assistant)
// 自动战斗、附近交互与装备页的补充翻译。
// 六语言规范键与显示字典；翻译是 UI 层转换，不能改动原生类型/协议标识。
static partial class L10n
{
    // 注册本文件的补充翻译字典；语言顺序保持与主字典及持久化索引一致。
    static L10n()
    {
        string[][] nearbyText =
        {
            new[]
            {
                "组件原生近战或按键模式，不追赶远处敌人",
                "Native melee or keyboard mode; no chasing distant enemies",
                "本来の近接攻撃またはキー操作。遠くの敵は追いません",
                "기본 근접 공격 또는 키 모드. 멀리 있는 적을 추격하지 않음",
                "組件原生近戰或按鍵模式，不追趕遠處敵人",
                "組件原生近戰或按鍵模式，不追趕遠處敵人"
            },
            new[]
            {
                "核对原生充能、危险和方向上的地面",
                "Check native charges, threats and the ground along the direction",
                "本来のチャージ・危険・回避方向の地面を確認",
                "기본 충전, 위험, 회피 방향의 지면 확인",
                "核對原生充能、危險和方向上的地面",
                "核對原生充能、危險和方向上的地面"
            },
            new[]
            {
                "原生战斗组件（实验）",
                "Native combat component (experimental)",
                "本来の戦闘コンポーネント（試験）",
                "기본 전투 구성 요소 (실험)",
                "原生戰鬥組件（實驗）",
                "原生戰鬥組件（實驗）"
            },
            new[]
            {
                "近战、各类法器与安全方向翻滚；鼠标不动",
                "Melee, artifacts and checked rolls; cursor stays put",
                "近接攻撃・各種アーティファクト・確認済み方向への回避。マウス移動なし",
                "근접 공격, 각종 유물, 검증한 방향 회피. 마우스 이동 없음",
                "近戰、各類法器與安全方向翻滾；滑鼠不動",
                "近戰、各類法器與安全方向翻滾；滑鼠不動"
            },
            new[]
            {
                "法器支持蓄力、限时引导和目标瞄准",
                "Charge, timed channel and aimed artifacts",
                "チャージ・時間制限付き持続・照準型に対応",
                "충전, 시간제 지속, 조준 지원",
                "法器支援蓄力、限時引導和目標瞄準",
                "法器支援蓄力、限時引導和目標瞄準"
            },
            new[]
            {
                "勾选原生模式后，组件缺失时暂停战斗动作",
                "In native mode, combat actions pause if the component is missing",
                "本来の操作モードではコンポーネント未導入時に停止",
                "기본 모드에서는 구성 요소 누락 시 전투 동작 중지",
                "勾選原生模式後，組件缺失時暫停戰鬥動作",
                "勾選原生模式後，組件缺失時暫停戰鬥動作"
            },
            new[]
            {
                "安装 / 更新原生战斗组件",
                "Install / update native combat component",
                "戦闘コンポーネントをインストール・更新",
                "기본 전투 구성 요소 설치 / 업데이트",
                "安裝 / 更新原生戰鬥組件",
                "安裝 / 更新原生戰鬥組件"
            },
            new[] { "原生战斗组件不支持此游戏版本", "Native combat does not support this game version", "このゲーム版はネイティブ戦闘に対応していません", "기본 전투 구성 요소가 이 게임 버전을 지원하지 않습니다", "原生戰鬥組件不支援此遊戲版本", "原生戰鬥元件不支援此遊戲版本" },
            new[]
            {
                "请安装原生战斗组件并重启游戏",
                "Install the native combat component and restart the game",
                "戦闘コンポーネントを導入してゲームを再起動",
                "기본 전투 구성 요소 설치 후 게임 재시작",
                "請安裝原生戰鬥組件並重啟遊戲",
                "請安裝原生戰鬥組件並重啟遊戲"
            },
            new[]
            {
                "原生战斗组件未响应",
                "Native combat component is not responding",
                "戦闘コンポーネントが応答していません",
                "기본 전투 구성 요소 응답 없음",
                "原生戰鬥組件未回應",
                "原生戰鬥組件未回應"
            },
            new[]
            {
                "场景已变化，请重新连接战斗组件",
                "Scene changed; reconnect the combat component",
                "シーンが変わりました。戦闘コンポーネントに再接続",
                "장면 변경됨. 전투 구성 요소 다시 연결",
                "場景已變化，請重新連接戰鬥組件",
                "場景已變化，請重新連接戰鬥組件"
            },
            new[]
            {
                "已请求原生战斗动作，等待游戏结果",
                "Native combat action requested; waiting for game evidence",
                "本来の戦闘動作を要求。ゲーム側の結果を待機",
                "기본 전투 동작 요청됨. 게임 결과 대기",
                "已請求原生戰鬥動作，等待遊戲結果",
                "已請求原生戰鬥動作，等待遊戲結果"
            },
            new[]
            {
                "已观察到原生能力激活；伤害由游戏结算",
                "Native ability activation observed; damage is resolved by the game",
                "本来の能力起動を観測。ダメージはゲーム側で処理",
                "기본 능력 활성화 관측됨. 피해는 게임에서 처리",
                "已觀察到原生能力啟動；傷害由遊戲結算",
                "已觀察到原生能力啟動；傷害由遊戲結算"
            },
            new[]
            {
                "已观察到闪避消耗充能并沿请求方向移动",
                "Roll charge spent and movement along requested direction observed",
                "回避チャージ消費と要求方向への移動を観測",
                "회피 충전 소모와 요청 방향 이동 관측됨",
                "已觀察到閃避消耗充能並沿請求方向移動",
                "已觀察到閃避消耗充能並沿請求方向移動"
            },
            new[]
            {
                "原生动作未确认，查看诊断日志",
                "Native action unconfirmed; see diagnostic log",
                "本来の動作は未確認。診断ログを確認",
                "기본 동작 미확인. 진단 로그 확인",
                "原生動作未確認，查看診斷日誌",
                "原生動作未確認，查看診斷日誌"
            },
            new[]
            {
                "法器蓄力／引导中；F9 可中止",
                "Artifact charging/channeling; F9 cancels",
                "チャージ・持続中。F9 で中止",
                "유물 충전/지속 중. F9로 중단",
                "法器蓄力／引導中；F9 可中止",
                "法器蓄力／引導中；F9 可中止"
            },
            new[]
            {
                "已请求法器释放，等待原生结束",
                "Artifact release requested; waiting for native end",
                "解放を要求。原生処理の終了待ち",
                "유물 해제 요청. 기본 처리 종료 대기",
                "已請求法器釋放，等待原生結束",
                "已請求法器釋放，等待原生結束"
            },
            new[]
            {
                "已提交原生瞄准目标，等待游戏结果",
                "Native aim target submitted; waiting for game result",
                "原生照準ターゲット送信。ゲーム結果待ち",
                "기본 조준 대상 전송. 게임 결과 대기",
                "已提交原生瞄準目標，等待遊戲結果",
                "已提交原生瞄準目標，等待遊戲結果"
            },
            new[] { "法器流程已结束", "Artifact lifecycle ended", "アーティファクトの処理が終了しました", "유물 실행이 종료되었습니다", "法器流程已結束", "法器流程已結束" },
            new[]
            {
                "没有可用的原生法器；检查槽位、冷却和灵魂",
                "No ready native artifact; check slots, cooldowns and souls",
                "使用可能なアーティファクトなし。スロット・クールダウン・ソウルを確認",
                "사용 가능한 유물 없음. 슬롯, 재사용 시간, 영혼 확인",
                "沒有可用的原生法器；檢查槽位、冷卻和靈魂",
                "沒有可用的原生法器；檢查槽位、冷卻和靈魂"
            },
            new[]
            {
                "请先退出游戏，再安装或更新原生战斗组件",
                "Exit the game before installing the native combat component",
                "ゲーム終了後に戦闘コンポーネントを導入",
                "게임 종료 후 기본 전투 구성 요소 설치",
                "請先退出遊戲，再安裝或更新原生戰鬥組件",
                "請先退出遊戲，再安裝或更新原生戰鬥組件"
            },
            new[]
            {
                "请先安装蓝图加载器，原生战斗功能需要此依赖",
                "Install Blueprint Loader first; native combat requires it",
                "本来の戦闘には Blueprint Loader が必要",
                "기본 전투에는 블루프린트 로더가 필요합니다",
                "請先安裝藍圖載入器，原生戰鬥功能需要此相依套件",
                "請先安裝藍圖載入器，原生戰鬥功能需要此相依套件"
            },
            new[]
            {
                "原生战斗组件已安装，请重启游戏后重新连接",
                "Native combat component installed; restart game and reconnect",
                "戦闘コンポーネント導入済み。再起動して再接続",
                "기본 전투 구성 요소 설치됨. 게임 재시작 후 다시 연결",
                "原生戰鬥組件已安裝，請重啟遊戲後重新連接",
                "原生戰鬥組件已安裝，請重啟遊戲後重新連接"
            },
            new[]
            {
                "拖动滑块或点击数字输入；1000 ms = 1 秒",
                "Drag a slider or enter a number; 1000 ms = 1 second",
                "スライダーか数値入力で設定。1000 ms = 1 秒",
                "슬라이더 또는 숫자 입력으로 설정. 1000 ms = 1초",
                "拖動滑桿或點選數字輸入；1000 ms = 1 秒",
                "拖動滑桿或點選數字輸入；1000 ms = 1 秒"
            },
            new[]
            {
                "移动交互和间隔可自定义；需更新直接收集组件",
                "Customize moving interactions and timing; update the collection component",
                "移動中の操作と間隔を設定可能。収集コンポーネントを更新",
                "이동 중 상호작용과 간격 설정 가능. 수집 구성 요소 업데이트 필요",
                "移動互動和間隔可自訂；需更新直接收集組件",
                "移動互動和間隔可自訂；需更新直接收集組件"
            },
            new[]
            {
                "允许手动移动时交互",
                "Interact while moving manually",
                "手動移動中も操作する",
                "수동 이동 중 상호작용 허용",
                "允許手動移動時互動",
                "允許手動移動時互動"
            },
            new[]
            {
                "通用交互间隔",
                "General interaction interval",
                "通常の操作間隔",
                "일반 상호작용 간격",
                "通用互動間隔",
                "通用互動間隔"
            },
            new[]
            {
                "食物食用间隔",
                "Food consumption interval",
                "食べ物の使用間隔",
                "음식 섭취 간격",
                "食物食用間隔",
                "食物食用間隔"
            },
            new[]
            {
                "最小尝试间隔；原生动作未结束时等待",
                "Minimum attempt interval; wait for native actions to finish",
                "最小試行間隔。本来の動作が終わるまで待機",
                "최소 시도 간격. 기본 동작이 끝날 때까지 대기",
                "最小嘗試間隔；原生動作未結束時等待",
                "最小嘗試間隔；原生動作未結束時等待"
            },
            new[]
            {
                "食物实体已消耗，原生效果未确认",
                "Food entity consumed; native effect unconfirmed",
                "食べ物は消費済み。本来の効果は未確認",
                "음식 개체 소모됨. 기본 효과 미확인",
                "食物實體已消耗，原生效果未確認",
                "食物實體已消耗，原生效果未確認"
            },
            new[]
            {
                "罐子已破坏，掉落结果未确认",
                "Pot broken; drops unconfirmed",
                "壺は破壊済み。ドロップは未確認",
                "항아리 파괴됨. 드롭 미확인",
                "罐子已破壞，掉落結果未確認",
                "罐子已破壞，掉落結果未確認"
            },
            new[]
            {
                "等待交互间隔",
                "Waiting for interaction interval",
                "操作間隔の待機中",
                "상호작용 간격 대기 중",
                "等待互動間隔",
                "等待互動間隔"
            },
            new[]
            {
                "检测到多个游戏目录，请选择要安装组件的目录",
                "Multiple game directories found; select the installation to update",
                "ゲームが複数見つかりました。更新するインストール先を選択",
                "여러 게임 폴더를 찾았습니다. 업데이트할 설치 폴더 선택",
                "偵測到多個遊戲目錄，請選擇要安裝組件的目錄",
                "偵測到多個遊戲目錄，請選擇要安裝組件的目錄"
            },
            new[]
            {
                "未自动找到游戏目录，请选择包含 Dungeons 文件夹的安装目录",
                "Game directory not found automatically; select the folder containing Dungeons",
                "自動検出できませんでした。Dungeons フォルダーを含むインストール先を選択",
                "게임 폴더를 자동으로 찾지 못했습니다. Dungeons 폴더가 있는 설치 경로 선택",
                "未自動找到遊戲目錄，請選擇包含 Dungeons 資料夾的安裝目錄",
                "未自動找到遊戲目錄，請選擇包含 Dungeons 資料夾的安裝目錄"
            },
            new[]
            {
                "游戏目录：{0}",
                "Game directory: {0}",
                "ゲームの場所：{0}",
                "게임 폴더: {0}",
                "遊戲目錄：{0}",
                "遊戲目錄：{0}"
            },
            new[]
            {
                "附近原生交互（实验）",
                "Native nearby interactions (experimental)",
                "近くの対象をゲーム本来の操作で処理（試験）",
                "주변 대상 기본 상호작용 (실험)",
                "附近原生互動（實驗）",
                "附近原生互動（實驗）"
            },
            new[]
            {
                "附近装备 / TNT 拾取（实验）",
                "Nearby gear / TNT pickup (experimental)",
                "近くの装備・TNTを拾う（試験）",
                "주변 장비 / TNT 획득 (실험)",
                "附近裝備 / TNT 拾取（實驗）",
                "附近裝備 / TNT 拾取（實驗）"
            },
            new[]
            {
                "已有宝箱使用原生交互；每个目标只请求一次",
                "Interact with existing chests; one native request per target",
                "既存の宝箱に本来の操作を要求。各対象は1回のみ",
                "기존 상자에 기본 상호작용 요청. 대상당 한 번만 실행",
                "已有寶箱使用原生互動；每個目標只請求一次",
                "已有寶箱使用原生互動；每個目標只請求一次"
            },
            new[]
            {
                "装备与 TNT 分别核对；TNT 仅拾取携带，不自动投掷",
                "Verify gear and TNT separately; pick up and carry TNT without throwing",
                "装備とTNTを別々に確認。TNTは拾って持ち、投げません",
                "장비와 TNT를 따로 확인. TNT는 획득 후 소지하며 던지지 않음",
                "裝備與 TNT 分別核對；TNT 僅拾取攜帶，不自動投擲",
                "裝備與 TNT 分別核對；TNT 僅拾取攜帶，不自動投擲"
            },
            new[]
            {
                "原生近战破坏大小罐；携带 TNT 时跳过；鼠标不动",
                "Native melee for both pot sizes; skip while carrying TNT; cursor unchanged",
                "大小の壺を本来の近接攻撃で破壊。TNT所持中はスキップ",
                "크기별 항아리를 기본 근접 공격으로 파괴. TNT 소지 시 건너뜀",
                "原生近戰破壞大小罐；攜帶 TNT 時跳過；滑鼠不動",
                "原生近戰破壞大小罐；攜帶 TNT 時跳過；滑鼠不動"
            },
            new[]
            {
                "只处理已核实食物，核对消耗与原生效果；鼠标不动",
                "Verified food only; check consumption and native effects; cursor unchanged",
                "確認済みの食べ物のみ。消費と本来の効果を確認",
                "확인된 음식만 처리. 소비와 기본 효과 확인. 마우스 이동 없음",
                "只處理已核實食物，核對消耗與原生效果；滑鼠不動",
                "只處理已核實食物，核對消耗與原生效果；滑鼠不動"
            },
            new[]
            {
                "配合上方各类型开关使用",
                "Use the switches above",
                "上の種類別設定と併用",
                "위 유형별 설정과 함께 사용",
                "配合上方各類型開關使用",
                "配合上方各類型開關使用"
            },
            new[]
            {
                "鼠标或角色位置发生变化，已停止附近交互",
                "Cursor or player position changed; nearby interactions stopped",
                "マウスまたはプレイヤーの位置が変わったため停止",
                "마우스 또는 플레이어 위치 변경으로 주변 상호작용 중지",
                "滑鼠或角色位置發生變化，已停止附近互動",
                "滑鼠或角色位置發生變化，已停止附近互動"
            },
            new[]
            {
                "游戏已确认食物消耗与原生效果",
                "Game confirmed food consumed and native effect",
                "食べ物の消費と本来の効果を確認",
                "게임에서 음식 소비와 기본 효과 확인",
                "遊戲已確認食物消耗與原生效果",
                "遊戲已確認食物消耗與原生效果"
            },
            new[]
            {
                "游戏已确认 TNT 拾取与携带",
                "Game confirmed TNT pickup and carrying",
                "TNTの取得と所持を確認",
                "게임에서 TNT 획득과 소지 확인",
                "遊戲已確認 TNT 拾取與攜帶",
                "遊戲已確認 TNT 拾取與攜帶"
            },
            new[]
            {
                "游戏已确认绿宝石罐破坏与掉落",
                "Game confirmed emerald pot broken and emerald drops",
                "エメラルドの壺の破壊とドロップを確認",
                "게임에서 에메랄드 항아리 파괴와 드롭 확인",
                "遊戲已確認綠寶石罐破壞與掉落",
                "遊戲已確認綠寶石罐破壞與掉落"
            },
            new[]
            {
                "直接收集组件需要更新，请退出游戏后重新安装组件",
                "Update collection component; exit game and reinstall component",
                "収集コンポーネントの更新が必要です。ゲーム終了後に再インストール",
                "수집 구성 요소 업데이트 필요. 게임 종료 후 다시 설치",
                "直接收集組件需要更新，請退出遊戲後重新安裝組件",
                "直接收集組件需要更新，請退出遊戲後重新安裝組件"
            },
            new[]
            {
                "直接收集装备与宝箱（实验）",
                "Direct gear and chest collection (experimental)",
                "装備と宝箱を直接収集（試験）",
                "장비와 상자 직접 수집 (실험)",
                "直接收集裝備與寶箱（實驗）",
                "直接收集裝備與寶箱（實驗）"
            },
            new[]
            {
                "需要蓝图加载器及直接收集组件；不移动鼠标，不需选中",
                "Requires Blueprint Loader and collection component; cursor stays in place",
                "Blueprint Loader と収集コンポーネントが必要。マウス移動や選択は不要",
                "블루프린트 로더와 수집 구성 요소 필요. 마우스 이동과 선택 불필요",
                "需要藍圖載入器及直接收集組件；不移動滑鼠，無須選中",
                "需要藍圖載入器及直接收集組件；不移動滑鼠，無須選中"
            },
            new[]
            {
                "配合上方装备、宝箱开关使用；只处理交互范围内目标",
                "Use with gear/chest switches above; targets within interaction range only",
                "上の装備・宝箱設定と併用。操作範囲内の対象のみ",
                "위 장비와 상자 설정과 함께 사용. 상호작용 범위 내 대상만 처리",
                "配合上方裝備、寶箱開關使用；只處理互動範圍內目標",
                "配合上方裝備、寶箱開關使用；只處理互動範圍內目標"
            },
            new[]
            {
                "安装 / 更新直接收集组件",
                "Install / update collection component",
                "収集コンポーネントをインストール・更新",
                "수집 구성 요소 설치 / 업데이트",
                "安裝 / 更新直接收集組件",
                "安裝 / 更新直接收集組件"
            },
            new[]
            {
                "请先退出游戏，再安装或更新直接收集组件",
                "Exit the game before installing the collection component",
                "ゲームを終了してから収集コンポーネントをインストール",
                "게임 종료 후 수집 구성 요소 설치",
                "請先退出遊戲，再安裝或更新直接收集組件",
                "請先退出遊戲，再安裝或更新直接收集組件"
            },
            new[]
            {
                "请先安装蓝图加载器，直接收集功能需要此依赖",
                "Install Blueprint Loader first; direct collection requires it",
                "直接収集には Blueprint Loader が必要です",
                "직접 수집에는 블루프린트 로더가 필요합니다",
                "請先安裝藍圖載入器，直接收集功能需要此依賴",
                "請先安裝藍圖載入器，直接收集功能需要此依賴"
            },
            new[]
            {
                "直接收集组件已安装，请重启游戏后重新连接",
                "Collection component installed; restart game and reconnect",
                "収集コンポーネント導入済み。再起動後に接続してください",
                "수집 구성 요소 설치 완료. 게임 재시작 후 다시 연결",
                "直接收集組件已安裝，請重啟遊戲後重新連接",
                "直接收集組件已安裝，請重啟遊戲後重新連接"
            },
            new[] { "收集组件不支持此游戏版本", "Collection component does not support this game version", "収集コンポーネントはこのゲーム版に対応していません", "수집 구성 요소가 이 게임 버전을 지원하지 않습니다", "收集組件不支援此遊戲版本", "收集元件不支援此遊戲版本" },
            new[]
            {
                "未检测到直接收集组件，请安装蓝图加载器及组件后重启游戏",
                "Collection component missing; install loader/component and restart game",
                "収集コンポーネント未検出。導入後にゲームを再起動してください",
                "수집 구성 요소 없음. 로더와 구성 요소 설치 후 게임 재시작",
                "未偵測到直接收集組件，請安裝藍圖載入器及組件後重啟遊戲",
                "未偵測到直接收集組件，請安裝藍圖載入器及組件後重啟遊戲"
            },
            new[]
            {
                "直接收集组件未响应，暂停收集",
                "Collection component unresponsive; paused",
                "収集コンポーネント応答なし。停止中",
                "수집 구성 요소 응답 없음. 수집 일시 중지",
                "直接收集組件未回應，暫停收集",
                "直接收集組件未回應，暫停收集"
            },
            new[]
            {
                "场景已变化，暂停直接收集",
                "Scene changed; direct collection paused",
                "シーン変更のため直接収集を停止",
                "장면 변경으로 직접 수집 일시 중지",
                "場景已變化，暫停直接收集",
                "場景已變化，暫停直接收集"
            },
            new[]
            {
                "正在直接收集附近目标，不移动鼠标",
                "Collecting nearby target; cursor unchanged",
                "マウスを動かさず近くの対象を収集中",
                "마우스 이동 없이 주변 대상 수집 중",
                "正在直接收集附近目標，不移動滑鼠",
                "正在直接收集附近目標，不移動滑鼠"
            },
            new[]
            {
                "游戏已确认装备拾取",
                "Game confirmed gear pickup",
                "装備の取得をゲームが確認",
                "게임에서 장비 획득 확인",
                "遊戲已確認裝備拾取",
                "遊戲已確認裝備拾取"
            },
            new[]
            {
                "游戏已确认宝箱开启",
                "Game confirmed chest opened",
                "宝箱の開封をゲームが確認",
                "게임에서 상자 열림 확인",
                "遊戲已確認寶箱開啟",
                "遊戲已確認寶箱開啟"
            },
            new[]
            {
                "直接收集未完成，详见日志",
                "Collection incomplete; see log",
                "直接収集未完了。ログを確認",
                "직접 수집 미완료. 로그 확인",
                "直接收集未完成，詳見日誌",
                "直接收集未完成，詳見日誌"
            },
            new[]
            {
                "附近食物自动食用（实验）",
                "Auto eat nearby food (experimental)",
                "近くの食べ物を自動で食べる（試験）",
                "주변 음식 자동 섭취 (실험)",
                "附近食物自動食用（實驗）",
                "附近食物自動食用（實驗）"
            },
            new[]
            {
                "核对同一食物的游戏食用提示后点击，不自动走过去",
                "Click after verifying the same food's game prompt; no automatic movement",
                "同じ食べ物のゲーム内表示を確認してクリック。移動はしません",
                "같은 음식의 게임 안내를 확인한 뒤 클릭하며 자동 이동하지 않습니다",
                "核對同一食物的遊戲食用提示後點擊，不自動走過去",
                "核對同一食物的遊戲食用提示後點擊，不自動走過去"
            },
            new[]
            {
                "尝试食用地上食物",
                "Attempt to eat dropped food",
                "落ちている食べ物を食べる",
                "바닥의 음식 섭취 시도",
                "嘗試食用地上食物",
                "嘗試食用地上食物"
            },
            new[]
            {
                "镜头未通过校验，暂停附近交互",
                "Camera unverified; nearby interaction paused",
                "カメラ未確認のため近くの操作を停止",
                "카메라 확인 실패로 주변 상호작용 일시 중지",
                "鏡頭未通過校驗，暫停附近互動",
                "鏡頭未通過校驗，暫停附近互動"
            },
            new[]
            {
                "附近正在战斗，暂停附近交互",
                "Nearby combat; interaction paused",
                "近くで戦闘中のため操作を停止",
                "주변 전투 중으로 상호작용 일시 중지",
                "附近正在戰鬥，暫停附近互動",
                "附近正在戰鬥，暫停附近互動"
            },
            new[]
            {
                "角色正在移动，站定后尝试附近交互",
                "Player moving; stand still to interact",
                "移動中。立ち止まると近くの操作を再開",
                "캐릭터 이동 중이며 멈추면 상호작용 시도",
                "角色正在移動，站定後嘗試附近互動",
                "角色正在移動，站定後嘗試附近互動"
            },
            new[]
            {
                "没有可执行的已启用目标",
                "No eligible target for enabled actions",
                "有効な操作の対象がありません",
                "활성화된 기능의 유효한 대상 없음",
                "沒有可執行的已啟用目標",
                "沒有可執行的已啟用目標"
            },
            new[]
            {
                "最近目标距离 {0}，超出当前交互范围",
                "Nearest target distance {0}; out of reach",
                "最も近い対象まで {0}。操作範囲外です",
                "가장 가까운 대상 거리 {0}, 상호작용 범위 밖",
                "最近目標距離 {0}，超出目前互動範圍",
                "最近目標距離 {0}，超出目前互動範圍"
            },
            new[]
            {
                "目标已尝试或暂不符合交互条件",
                "Target attempted or currently ineligible",
                "操作済み、または条件を満たさない対象",
                "이미 시도했거나 현재 조건에 맞지 않는 대상",
                "目標已嘗試或暫不符合互動條件",
                "目標已嘗試或暫不符合互動條件"
            },
            new[]
            {
                "原地攻击键与触发键冲突",
                "Stand-still key conflicts with trigger",
                "その場攻撃キーと起動キーが重複",
                "제자리 공격 키와 실행 키 충돌",
                "原地攻擊鍵與觸發鍵衝突",
                "原地攻擊鍵與觸發鍵衝突"
            },
            new[]
            {
                "手动输入中，暂停附近交互",
                "Manual input; nearby interaction paused",
                "手動操作中のため近くの操作を停止",
                "수동 입력 중으로 주변 상호작용 일시 중지",
                "手動輸入中，暫停附近互動",
                "手動輸入中，暫停附近互動"
            }
        };
        var nearbyMaps = new[]
        {
            En,
            Ja,
            Ko,
            Hk,
            Tw
        };
        foreach (var row in nearbyText)
            for (int i = 0; i < nearbyMaps.Length; i++)
                nearbyMaps[i][row[0]] = row[i + 1];
        En["附近绿宝石罐（实验）"] = "Nearby emerald pots (experimental)";
        En["核对大小绿宝石罐与可破坏状态，原地尝试攻击一次"] = "Check small / large pots and breakable state; attempt one stationary attack";
        En["尝试击破绿宝石罐"] = "Attempt to break emerald pot";
        En["宝箱 {0} · 装备 {1} · 食物 {2} · 罐子 {3}\n{4}"] = "Chests {0} / Gear {1} / Food {2} / Pots {3}\n{4}";
        En["宝箱 {0} · 装备 {1} · 食物 {2}\n{3}"] = "Chests {0} / Gear {1} / Food {2}\n{3}";
        En["只交互身边目标；不寻路"] = "Interact in reach only; no pathfinding";
        En["装备与食物分开识别；有敌人、菜单或手动输入时暂停拾取"] = "Identify gear and food separately; enemies, menus or manual input pause pickup";
        En["未核对到此目标的游戏交互提示，已跳过"] = "No matching game interaction prompt; target skipped";
        En["附近宝箱交互（实验）"] = "Nearby chest interaction (experimental)";
        En["只尝试交互范围内的已有宝箱，每个目标只点一次"] = "Existing chests in reach only; one click per target";
        En["附近装备拾取（实验）"] = "Nearby gear pickup (experimental)";
        En["识别原生物品类型和归属；有敌人、菜单或手动输入时暂停"] = "Read item type and owner; pause for enemies, menus or manual input";
        En["附近目标状态"] = "Nearby target status";
        En["勾选附近交互后开始检测\n不寻路；成功结果仍需游戏确认"] = "Enable nearby interaction to start monitoring\nNo pathfinding; game confirmation still required";
        En["F9 停止所有自动操作"] = "F9 stops all automatic actions";
        En["附近宝箱 {0} · 地上装备 {1}\n只交互身边目标；不寻路"] = "Nearby chests {0} / Dropped gear {1}\nInteract in reach only; no pathfinding";
        En["附近目标读数不可用，暂停交互"] = "Nearby targets unavailable; interaction paused";
        En["尝试拾取地上装备"] = "Attempt dropped gear pickup";
        En["尝试交互附近宝箱"] = "Attempt nearby chest interaction";
        En["已发送一次交互，结果由游戏确认\n未确认成功时不重复点击此目标"] = "One interaction sent; the game determines its outcome\nNo repeated clicks on unconfirmed targets";
        En["攻击自动闪避（实验）"] = "Attack evasion (experimental)";
        En["核对弹道与近战骨骼轨迹；未覆盖全部 Boss 攻击"] = "Check projectiles and melee bone motion; some boss attacks unsupported";
        En["近战攻击自动闪避"] = "Melee attack auto evade";
        En["Boss 技能范围、附近宝箱与拾取继续适配"] = "Boss abilities and nearby chest / loot support in development";
        En["只读背包预览，未出售任何物品"] = "Read-only inventory preview; nothing sold";
        En["符合回收规则"] = "Eligible under rules";
        En["回收清单与进度"] = "Salvage list and progress";
        En["全部装备"] = "All equipment";
        En["待回收"] = "Eligible";
        En["保留"] = "Kept";
        En["已离开背包"] = "Left inventory";
        En["待回收 {0} · 保留 {1} · 游戏确认回收 {2}"] = "Eligible {0} / Kept {1} / Game confirmed {2}";
        En["装备 / 稀有度 / 力量"] = "Gear / Rarity / Power";
        En["此分类暂无装备"] = "No equipment in this filter";
        En["生成预览或启用整理后显示装备"] = "Preview or enable organizing to display gear";
        En["仅预览，未出售任何物品"] = "Preview only; nothing sold";
        En["物品消失不代表已回收；回收数量以游戏回执为准"] = "Departures are not confirmed sales; receipts confirm totals";
        En["界面演示数据；未读取或出售游戏装备"] = "Demo data; no game items read or sold";
        En["清单暂未更新，显示上次读取结果"] = "List unavailable; displaying last snapshot";
        En["已有装备整理完成"] = "Existing equipment processing completed";
        En["正在整理"] = "Organizing";
        En["安装 / 更新装备回收组件"] = "Install / update equipment component";
        En["请先退出游戏，再安装或更新装备回收组件"] = "Exit the game before installing or updating the equipment component";
        En["请选择包含 Dungeons 文件夹的游戏安装目录"] = "Select the game installation folder containing Dungeons";
        En["请先安装 Blueprint Loader（蓝图加载器），此功能单独需要该依赖"] = "Install Blueprint Loader first; automatic selling requires it";
        En["组件目录已有其他文件，未覆盖；请保留原文件并检查安装目录"] = "Existing component folder is unmanaged; files kept. Check the installation";
        En["装备回收组件已安装，请启动游戏后重新连接"] = "Equipment component installed; start the game and reconnect";
        En["整理已停止"] = "Organizer stopped";
        En["装备回收组件版本过旧，请更新后重启游戏"] = "Equipment component outdated; update it and restart the game";
        En["回收符合规则的已有装备"] = "Salvage existing gear matching rules";
        En["持续自动出售只处理新拾取物品；F9 停止整理"] = "Continuous selling processes new pickups only; F9 stops organizing";
        En["已有装备整理完成 · 已回收 {0} 件"] = "Existing gear processed: {0} items salvaged";
        En["按当前规则回收已有装备和法器，待回收 {0} 件。已装备、锁定、附魔及受保护物品会保留。继续吗？"] = "Salvage existing equipment and artifacts matching the current rules: {0} candidates. Equipped, locked, enchanted and protected items are kept. Continue?";
        En["正在等待游戏端确认回收已有装备"] = "Waiting for the game to process existing gear";
        En["正在回收已有装备 · 已回收 {0} 件 · 待处理 {1} 件"] = "Processing existing gear: {0} salvaged, {1} pending";
        En["仅自动出售需 Blueprint Loader（蓝图加载器）和装备回收组件"] = "Automatic selling requires Blueprint Loader and the equipment component";
        En["只处理启用后拾取的装备；F9 停止整理"] = "Only items collected after enabling; F9 stops organizing";
        En["装备回收组件不支持此游戏版本"] = "Equipment component does not support this game version";
        En["未检测到装备回收组件，请安装蓝图加载器及组件后重启游戏"] = "Equipment component missing; install Blueprint Loader and the component, then restart";
        En["正在等待游戏端确认，已有装备保留"] = "Waiting for the game; existing equipment is kept";
        En["游戏端整理已停止，请重新启用"] = "Game component stopped; enable the organizer again";
        En["游戏端响应超时，整理已停止"] = "Game component timed out; organizer stopped";
        En["自动出售已启用 · 已回收 {0} 件 · 待处理 {1} 件"] = "Automatic selling active: {0} salvaged, {1} pending";
        En["游戏拒绝回收此物品，已保留并跳过"] = "Game rejected the sale; item kept and skipped";
        En["新拾取装备整理"] = "Organize newly collected gear";
        En["启停快捷键"] = "Toggle shortcut";
        En["整理已关闭；启用时记录已有物品"] = "Organizer off; existing items are recorded when enabled";
        En["自动出售等待游戏端组件；F9 停止整理"] = "Automatic selling awaits the game component; F9 stops organizing";
        En["自定义快捷键整理新拾取装备，保留受保护物品"] = "Use a custom shortcut to organize new gear and keep protected items";
        En["整理快捷键已被占用，可重新绑定或使用开关"] = "Shortcut unavailable; rebind it or use the toggle";
        En["整理快捷键与游戏或工具键位冲突，请重新绑定"] = "Shortcut conflicts with game or toolbox bindings; choose another key";
        En["整理键位设置失败"] = "Failed to set organizer shortcut";
        En["请先连接游戏，再启用整理"] = "Connect to the game before enabling the organizer";
        En["已开始检测新拾取装备；出售执行等待游戏端组件"] = "Monitoring new gear; selling awaits the game component";
        En["角色或场景已变化，请重新启用整理"] = "Player or scene changed; enable the organizer again";
        En["新拾取待卖 {0} 件；出售执行等待游戏端组件"] = "{0} new items eligible; selling awaits the game component";
        En["直线弹道自动闪避（实验）"] = "Linear projectile evasion (experimental)";
        En["读取游戏闪避键与充能，核对危险和方向上的地面"] = "Read game dodge key and charges; check threats and ground";
        En["只响应可核对的直线弹道；未覆盖全部 Boss 攻击"] = "Validated linear projectiles only; not all boss attacks";
        En["闪避等待充能、游戏键位和地面状态"] = "Waiting for dodge charge, game binding and grounded state";
        En["正在读取附近可通行地面"] = "Reading nearby walkable ground";
        En["闪避充能 {0} · 可核对危险 {1}"] = "Dodge charges {0} · Validated threat candidates {1}";
        En["直线弹道自动闪避"] = "Linear projectile auto evade";
        En["检查游戏左键主操作、原地攻击键及触发键冲突"] = "Check game left-click action, stand-still key and trigger conflicts";
        En["自动战斗"] = "Auto combat";
        En["附近敌人自动战斗"] = "Nearby auto combat";
        En["原地近战与提前使用法器；药水遵循血量阈值"] = "Stationary melee and combat artifacts; potion requires low health";
        En["遇敌自动攻击需要开启原生模式"] = "Enable native mode for attack on encounter";
        En["原生战斗组件需要更新，请退出游戏后重新安装组件"] = "Native combat component update required; exit game and reinstall";
        En["遇到敌人自动攻击"] = "Attack on enemy encounter";
        En["需原生模式；遇敌主动近战，允许手动行走，不寻路"] = "Native mode: proactive melee while walking; no navigation";
        En["法器仍由下方开关与勾选槽位控制，无需等敌人先出手"] = "Artifacts use the selected slots and switch below; no aggro required";
        En["附近装备 / 附魔书 / TNT 拾取（实验）"] = "Nearby gear / enchantment books / TNT (experimental)";
        En["拾取附近装备和附魔书；TNT 仅拾取携带"] = "Pick up nearby gear and books; TNT pickup only";
        En["遇敌攻击开启后主动使用，否则等待敌人攻击你"] = "Attack on encounter: proactive use; otherwise wait for enemy aggro";
        En["附魔书"] = "Enchantment books";
        En["尝试拾取地上附魔书"] = "Try picking up a dropped enchantment book";
        En["游戏已确认附魔书拾取"] = "Game confirmed enchantment book pickup";
        En["附近敌人原地近战"] = "Stationary melee against nearby enemies";
        En["按游戏的原地攻击键与左键攻击，不追赶远处敌人"] = "Use the game stand-still key and left click for nearby enemies";
        En["附近判定范围"] = "Nearby range";
        En["战斗提前使用法器"] = "Use artifacts during combat";
        En["敌人正在攻击你时，使用自动法器页勾选的可用槽位"] = "Use selected ready slots when a nearby enemy targets you";
        En["药水仍只在低血量时使用"] = "Potion use still requires low health";
        En["战斗状态"] = "Combat status";
        En["勾选功能后按 F8 开始，F9 随时停止"] = "Enable a feature, press F8 to start; F9 stops at any time";
        En["仅游戏前台执行；打开菜单或手动操作时暂停近战"] = "Foreground only; menus and manual input pause melee";
        En["Boss 精细闪避、附近宝箱与拾取仍在适配"] = "Precise boss evasion, nearby chests and loot are in development";
        En["未启用自动寻路"] = "Automatic navigation is disabled";
        En["等待战斗场景；菜单或状态不明时暂停"] = "Waiting for gameplay; menus or unknown state pause combat";
        En["附近有效敌人 {0} · 原地近战范围 {1}"] = "Nearby hostiles {0} · Stationary range {1}";
        En["战斗记录"] = "Combat";
        En["战斗识别（本地验证）"] = "Combat validation";
        En["记录敌人、攻击与弹道；新的自动执行尚未开放"] = "Record enemies, attacks and projectiles; new automation is not available";
        En["敌人和 Boss 行为记录"] = "Enemy and boss behavior records";
        En["已记录主要 Boss、变体和强力敌人的行为定义"] = "Boss and enemy behavior definitions recorded";
        En["读取动作预警与命中事件"] = "Read telegraph and hit events";
        En["等待读取敌人状态"] = "Waiting for enemy state capture";
        En["采集战斗记录"] = "Export combat capture";
        En["战斗与躲避 → 法器与药水 → 附近宝箱与战利品"] = "Combat / evade > Artifacts / potion > Nearby chests / loot";
        En["药水保持低血量条件"] = "Potion use requires low health";
        En["敌人状态与识别记录已导出；未操作角色"] = "Enemy detection capture exported; no character input sent";
        En["敌人状态读取失败，未执行操作"] = "Enemy state capture failed; no action performed";
        En["寻路验证"] = "Navigation";
        En["装备整理"] = "Equipment";
        En["寻路验证（本地开发）"] = "Navigation (local development)";
        En["装备整理（仅预览）"] = "Equipment (preview only)";
        En["先验证导航网格与目标；完整自动流程尚未开放"] = "Verify mesh and target first; full automation is not available";
        En["默认仅处理新拾取的普通装备；当前预览不出售"] = "New common gear only by default; preview does not sell";
        En["地图：至尊唤魔者塔楼"] = "Area: Supreme Evoker Tower";
        En["目标坐标尚未确认，移动与循环保持关闭"] = "Target coordinates unverified; movement and looping disabled";
        En["等待读取可通行区域"] = "Waiting for walkable regions";
        En["采集寻路记录"] = "Export navigation capture";
        En["自动流程适配进度"] = "Automation validation";
        En["战斗与躲避 → 宝箱 → 战利品 → 重进 → 循环"] = "Combat / evade > Chests > Loot > Re-enter > Loop";
        En["分类稀有度上限"] = "Rarity limits by category";
        En["装备类别"] = "Category";
        En["普通装备"] = "Normal gear";
        En["灵魂风暴装备"] = "Soul storm gear";
        En["近战武器"] = "Melee weapons";
        En["远程武器"] = "Ranged weapons";
        En["头盔"] = "Helmets";
        En["胸甲"] = "Chest armor";
        En["护腿"] = "Leggings";
        En["靴子"] = "Boots";
        En["法器"] = "Artifacts";
        En["普通"] = "Common";
        En["稀有"] = "Rare";
        En["特殊"] = "Special";
        En["独特"] = "Unique";
        En["已装备、锁定、附魔装备始终保留"] = "Equipped, locked and enchanted gear is always kept";
        En["保留更好的装备"] = "Keep upgrades";
        En["保留商店购买"] = "Keep merchant purchases";
        En["保留任务奖励"] = "Keep quest rewards";
        En["清理较差的重复装备"] = "Remove worse duplicates";
        En["未确认的属性与来源也会保留"] = "Unverified properties and origins are kept";
        En["待卖清单预览"] = "Salvage plan preview";
        En["新拾取模式忽略建立基线时已有的全部物品"] = "New-pickup mode skips everything present at baseline";
        En["预览新拾取"] = "Preview new pickups";
        En["预览全部已有"] = "Preview entire inventory";
        En["尚未生成清单；当前不会出售装备"] = "No preview yet; no gear will be sold";
        En["查看清单与保留原因"] = "View items and keep reasons";
        En["导出待卖清单"] = "Export salvage preview";
        En["此开发版仅验证规则，卖出执行尚未接入"] = "Development preview only; sale execution is not connected";
        En["规则已更新，请重新生成清单"] = "Rules updated; generate a new preview";
        En["读取 {0} 件装备 · 待卖 {1} 件 · 保留 {2} 件\n仅预览，未出售任何物品"] = "Read {0} gear items · Eligible {1} · Kept {2}\nPreview only; nothing was sold";
        En["清单读取失败，保持全部物品"] = "Inventory read failed; keep everything";
        En["请先生成待卖清单"] = "Generate a preview first";
        En["物品"] = "Item";
        En["稀有度"] = "Rarity";
        En["强度"] = "Power";
        En["判定"] = "Decision";
        En["符合规则（未出售）"] = "Eligible (not sold)";
        En["已装备"] = "Equipped";
        En["已锁定"] = "Locked";
        En["已附魔"] = "Enchanted";
        En["已有物品"] = "Existing item";
        En["优于已装备物品"] = "Better than equipped";
        En["缺少同类装备对照"] = "No equipped comparison";
        En["来源保护"] = "Origin protection";
        En["属性未确认，保留"] = "Unverified: keep";
        En["最佳重复装备"] = "Best duplicate";
        En["保留灵魂风暴装备"] = "Keep soul storm gear";
        En["超出稀有度上限"] = "Above rarity limit";
        En["寻路读取失败，移动保持关闭"] = "Navigation read failed; movement stays disabled";
        En["自动安装蓝图加载器"] = "Install Blueprint Loader automatically";
        En["未检测到完整蓝图加载器。\n请在官网登录并下载 Blueprint Loader（不是开发模板）。\n下载完成后自动识别、安装，再继续安装组件。"] = "Blueprint Loader is missing or incomplete.\nSign in on Nexus and download Blueprint Loader (not the template).\nThe completed ZIP will be detected and installed, then the component.";
        En["打开官方下载页"] = "Open official download page";
        En["选择已下载 ZIP"] = "Select downloaded ZIP";
        En["选择 Blueprint Loader 安装包"] = "Select the Blueprint Loader archive";
        En["正在监测系统下载文件夹；其他保存位置可点击选择 ZIP。"] = "Watching your Downloads folder; use Select ZIP for other locations.";
        En["无法打开浏览器，请手动访问 Nexus Mods 的 Blueprint Loader 页面，或选择已下载 ZIP。"] = "Browser unavailable. Open Blueprint Loader on Nexus Mods or select a downloaded ZIP.";
        En["请完全退出游戏和 Minecraft Launcher，再安装蓝图加载器"] = "Fully close the game and Minecraft Launcher before installing Blueprint Loader.";
        En["缺少蓝图加载器：请从官网下载安装包后重试"] = "Blueprint Loader missing: download the official archive and retry.";
        En["请选择 Blueprint Loader 的 ZIP，不是模组开发模板"] = "Select the Blueprint Loader ZIP, not the mod development template.";
        En["蓝图加载器文件不完整或过大，未安装"] = "Loader files are incomplete or too large; nothing installed.";
        En["蓝图加载器 UTOC 格式无效，未安装"] = "Invalid loader UTOC format; nothing installed.";
        En["蓝图加载器 PAK 格式无效，未安装"] = "Invalid loader PAK format; nothing installed.";
        En["安装包包含无效路径，未安装"] = "Invalid archive path; nothing installed.";
        En["安装包包含重复加载器，未安装"] = "Duplicate loader files in archive; nothing installed.";
        En["检测到多个不完整的蓝图加载器，请先检查模组目录"] = "Multiple incomplete loaders found; check the mod folders first.";
        En["模组目录过多，请检查蓝图加载器安装目录"] = "Too many mod folders; check the loader directory.";
        En["请先退出游戏，再安装组件"] = "Exit the game before installing components.";
    }
}

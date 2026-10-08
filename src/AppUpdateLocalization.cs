// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Cyanmir
static partial class L10n
{
    // 在主翻译字典完成后注册；新增格式参数时六列必须保持相同占位符。
    static void RegisterUpdateText()
    {
        string[][] rows =
        {
            new[] { "工具更新", "App updates", "ツールの更新", "도구 업데이트", "工具更新", "工具更新" },
            new[] { "启动时检查更新", "Check for updates on startup", "起動時に更新を確認", "시작 시 업데이트 확인", "啟動時檢查更新", "啟動時檢查更新" },
            new[] { "更新通道", "Update channel", "更新チャンネル", "업데이트 채널", "更新通道", "更新通道" },
            new[] { "正式版", "Stable", "正式版", "정식 버전", "正式版", "正式版" },
            new[] { "Dev 版", "Dev", "Dev 版", "Dev 버전", "Dev 版", "Dev 版" },
            new[] { "自动下载安装更新", "Download and install automatically", "更新を自動ダウンロード・インストール", "업데이트 자동 다운로드 및 설치", "自動下載並安裝更新", "自動下載並安裝更新" },
            new[] { "更新后重启工具，保留配置；Dev 版来自预发布。", "Restarts after updating; keeps settings. Dev uses prereleases.", "更新後に再起動。設定は保持。Dev はプレリリース。", "업데이트 후 재시작, 설정 유지. Dev는 사전 릴리스.", "更新後重啟工具並保留設定；Dev 版來自預發佈。", "更新後重啟工具並保留設定；Dev 版來自預發布。" },
            new[] { "检查更新", "Check for updates", "更新を確認", "업데이트 확인", "檢查更新", "檢查更新" },
            new[] { "下载并覆盖安装", "Download and replace", "ダウンロードして上書き", "다운로드 및 덮어쓰기", "下載並覆蓋安裝", "下載並覆蓋安裝" },
            new[] { "尚未检查更新", "Updates not checked yet", "更新未確認", "업데이트를 확인하지 않음", "尚未檢查更新", "尚未檢查更新" },
            new[] { "正在检查 GitHub 更新…", "Checking GitHub for updates…", "GitHub の更新を確認中…", "GitHub 업데이트 확인 중…", "正在檢查 GitHub 更新…", "正在檢查 GitHub 更新…" },
            new[] { "所选通道暂未发布可用版本", "No release available for this channel", "このチャンネルに公開版はありません", "선택한 채널에 배포된 버전 없음", "所選通道暫未發佈可用版本", "所選通道暫未發布可用版本" },
            new[] { "已是所选版本：{0}", "Selected version installed: {0}", "選択したバージョンは導入済み：{0}", "선택한 버전이 설치됨: {0}", "已是所選版本：{0}", "已是所選版本：{0}" },
            new[] { "所选版本 {0} 较旧，手动安装将降级", "{0} is older; manual installation will downgrade", "{0} は旧版です。手動導入でダウングレード", "{0}은 이전 버전. 수동 설치 시 다운그레이드", "所選版本 {0} 較舊，手動安裝將降級", "所選版本 {0} 較舊，手動安裝將降級" },
            new[] { "可安装 {0}，当前版本 {1}", "Available: {0}. Installed: {1}", "導入可能：{0}、現在：{1}", "설치 가능: {0}, 현재: {1}", "可安裝 {0}，目前版本 {1}", "可安裝 {0}，目前版本 {1}" },
            new[] { "更新检查失败，请稍后重试", "Update check failed; try again later", "更新確認に失敗。後でもう一度お試しください", "업데이트 확인 실패. 나중에 다시 시도", "更新檢查失敗，請稍後重試", "更新檢查失敗，請稍後重試" },
            new[] { "将安装较旧版本 {0}，是否继续？", "Install older version {0} and downgrade?", "旧バージョン {0} にダウングレードしますか？", "이전 버전 {0}(으)로 다운그레이드할까요?", "將安裝較舊版本 {0}，是否繼續？", "將安裝較舊版本 {0}，是否繼續？" },
            new[] { "正在下载更新：{0}%", "Downloading update: {0}%", "更新をダウンロード中：{0}%", "업데이트 다운로드 중: {0}%", "正在下載更新：{0}%", "正在下載更新：{0}%" },
            new[] { "下载校验完成，正在重启并安装…", "Download verified; restarting to install…", "ダウンロード確認済み。再起動して導入中…", "다운로드 검증 완료. 설치를 위해 재시작 중…", "下載校驗完成，正在重啟並安裝…", "下載校驗完成，正在重啟並安裝…" },
            new[] { "更新下载或安装失败，请重试", "Update download or installation failed; retry", "更新のダウンロードまたは導入に失敗。再試行してください", "업데이트 다운로드 또는 설치 실패. 다시 시도", "更新下載或安裝失敗，請重試", "更新下載或安裝失敗，請重試" },
            new[] { "更新下载地址无效", "Invalid update download URL", "更新のダウンロード URL が無効", "유효하지 않은 업데이트 다운로드 주소", "更新下載地址無效", "更新下載位址無效" },
            new[] { "更新文件大小或地址无效", "Invalid update file size or path", "更新ファイルのサイズまたはパスが無効", "유효하지 않은 업데이트 파일 크기 또는 경로", "更新檔案大小或路徑無效", "更新檔案大小或路徑無效" },
            new[] { "更新文件下载不完整", "Update download is incomplete", "更新のダウンロードが未完了", "업데이트 다운로드가 완료되지 않음", "更新檔案下載不完整", "更新檔案下載不完整" },
            new[] { "此版本没有可用的安装包", "No installable package in this release", "このリリースに導入可能なパッケージはありません", "이 릴리스에 설치 가능한 패키지 없음", "此版本沒有可用的安裝包", "此版本沒有可用的安裝套件" },
            new[] { "更新文件校验失败", "Update verification failed", "更新ファイルの検証に失敗", "업데이트 파일 검증 실패", "更新檔案校驗失敗", "更新檔案校驗失敗" },
            new[] { "此版本缺少更新校验文件", "Release is missing update checksums", "更新のチェックサムがありません", "이 버전에 업데이트 체크섬 없음", "此版本缺少更新校驗檔案", "此版本缺少更新校驗檔案" },
            new[] { "无法启动更新安装程序", "Cannot start the update installer", "更新インストーラーを起動できません", "업데이트 설치 프로그램을 시작할 수 없음", "無法啟動更新安裝程式", "無法啟動更新安裝程式" },
            new[] { "请先关闭正在运行的工具", "Close the running app first", "実行中のツールを終了してください", "실행 중인 도구를 먼저 종료하세요", "請先關閉正在執行的工具", "請先關閉正在執行的工具" },
            new[] { "更新安装失败，已保留旧版或备份", "Update failed; previous files or backups retained", "更新に失敗。旧版またはバックアップを保持", "업데이트 실패. 이전 버전 또는 백업 유지", "更新安裝失敗，已保留舊版或備份", "更新安裝失敗，已保留舊版或備份" }
        };
        var maps = new[] { En, Ja, Ko, Hk, Tw };
        foreach (var row in rows)
            for (int language = 0; language < maps.Length; language++)
                maps[language][row[0]] = row[language + 1];
    }
}

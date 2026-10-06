# 使用 Windows 自带 .NET Framework 编译器，构建 x64 WinForms 单文件 EXE。
# 新增源码/资源时更新下面的显式清单；资源名须与读取代码一致。
param([string]$Output=(Join-Path $PSScriptRoot '../dist/MCD2A.exe'), [string]$EquipmentPresentationPath='')
# 出错立即停止，避免把旧 EXE 误认为新构建。
$ErrorActionPreference='Stop'
# 确定绝对输出路径并创建生成目录；dist 由 .gitignore 排除。
$Output=[IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($Output)) -Force | Out-Null
# 公开构建不带拆包图像；显式提供个人图标包时才增加该资源。
$taskOptionalResource=@()
if($EquipmentPresentationPath){$taskAtlas=(Resolve-Path -LiteralPath $EquipmentPresentationPath).Path;$taskOptionalResource=@('/resource:'+ $taskAtlas + ',equipment-presentation.bin.gz')}
# 临时进入源码目录，让相对源码/资源路径始终相同；finally 恢复原目录。
Push-Location $PSScriptRoot
try {
    # Framework64 对应 x64 进程读取与输入 ABI，不能随意改为 x86。
    $compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    # 显式嵌入三种组件的完整 pak/utoc/ucas、产品字体和许可，不遍历 bin/obj 。
    # 出售组件沿用已验证预编译文件，不从历史 stub 重建。
    & $compiler `
        /nologo /target:winexe /platform:x64 /optimize+ `
        /win32icon:assets/toolbox.ico @taskOptionalResource /resource:assets/combat-catalog.json,combat-catalog.json /resource:assets/loot-catalog.json,loot-catalog.json `
        /resource:assets/nearby-component/MCD2NearbyLootBridge_P.pak,MCD2NearbyLootBridge_P.pak /resource:assets/nearby-component/MCD2NearbyLootBridge_P.utoc,MCD2NearbyLootBridge_P.utoc /resource:assets/nearby-component/MCD2NearbyLootBridge_P.ucas,MCD2NearbyLootBridge_P.ucas /resource:assets/equipment-component/MCD2EquipmentBridge_P.pak,MCD2EquipmentBridge_P.pak `
        /resource:assets/equipment-component/MCD2EquipmentBridge_P.utoc,MCD2EquipmentBridge_P.utoc /resource:assets/equipment-component/MCD2EquipmentBridge_P.ucas,MCD2EquipmentBridge_P.ucas /resource:assets/toolbox.ico,toolbox.ico /resource:assets/toolbox.png,toolbox.png `
        /resource:assets/unifont.hex.gz,unifont.hex.gz /resource:assets/minecraft-ten.ttf,minecraft-ten.ttf /resource:assets/minecraft-body.ttf,minecraft-body.ttf /resource:assets/source-han-jp.otf,source-han-jp.otf `
        /resource:assets/source-han-kr.otf,source-han-kr.otf /resource:assets/source-han-jp-heavy.otf,source-han-jp-heavy.otf /resource:assets/source-han-kr-heavy.otf,source-han-kr-heavy.otf /resource:assets/source-han-sc-regular.otf,source-han-sc-regular.otf `
        /resource:assets/source-han-sc-heavy.otf,source-han-sc-heavy.otf /resource:assets/source-han-hk-regular.otf,source-han-hk-regular.otf /resource:assets/source-han-hk-heavy.otf,source-han-hk-heavy.otf /resource:assets/source-han-tw-regular.otf,source-han-tw-regular.otf `
        /resource:assets/source-han-tw-heavy.otf,source-han-tw-heavy.otf /resource:../LICENSE,LICENSE /resource:../LICENSE.zh-CN.md,LICENSE.zh-CN.md /resource:../THIRD_PARTY_NOTICES.md,THIRD_PARTY_NOTICES.md `
        /resource:assets/Mojang-fonts-license.txt,Mojang-fonts-license.txt /resource:assets/Mojang-fonts-OFL.txt,Mojang-fonts-OFL.txt /resource:assets/OFL-1.1.txt,OFL-1.1.txt /resource:assets/SourceHanSans-LICENSE.txt,SourceHanSans-LICENSE.txt `
        /resource:assets/combat-component/MCD2CombatBridge_P.pak,MCD2CombatBridge_P.pak /resource:assets/combat-component/MCD2CombatBridge_P.utoc,MCD2CombatBridge_P.utoc /resource:assets/combat-component/MCD2CombatBridge_P.ucas,MCD2CombatBridge_P.ucas /resource:assets/native-combat-artifacts.json,native-combat-artifacts.json `
        /out:$Output /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll `
        /r:System.Web.Extensions.dll /r:System.IO.Compression.dll AutoHeal.cs BlueprintLoaderInstall.cs `
        GameLocator.cs GameInstallLocator.cs EnemyThreats.cs `
        CombatDefinitions.cs CombatTelemetry.cs BoneGeometry.cs LiveCombatMotion.cs `
        CombatEligibility.cs CombatRuntime.cs CombatActions.cs CombatScheduling.cs `
        CombatEvade.cs CombatBridge.cs CombatBridgeInstall.cs MeleeEvade.cs `
        NearbyLoot.cs NearbyLootBridge.cs NearbyLootBridgeInstall.cs EnemyHostility.cs `
        GameBindings.cs GdkSaveStorage.cs `
        GdkBridgeTransport.cs NativeBridgeChannel.cs InventoryReader.cs `
        EquipmentPolicy.cs EquipmentDropOrigins.cs EquipmentBridge.cs EquipmentBridgeInstall.cs `
        EquipmentActions.cs EquipmentVisualization.cs EquipmentGamePresentation.cs `
        NavigationMesh.cs QuestDestination.cs AutomationUi.cs AutomationLocalization.cs `
        AdaptationRecord.cs ToolboxWindow.cs ToolboxAssembly.cs OreUi.cs `
        Localization.cs Diagnostics.cs LicenseViewer.cs
    # 编译器失败时不继续打包；构建过程不修改游戏安装文件。
    if($LASTEXITCODE -ne 0){throw '编译失败'}
} finally {Pop-Location}

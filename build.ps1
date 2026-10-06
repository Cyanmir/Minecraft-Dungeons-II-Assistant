# 中文构建入口：只转发参数，实际编译和资源清单位于 src/build.ps1。
# Output 为最终 EXE 路径；EquipmentPresentationPath 为个人构建可选本地图标包，公开包不传此参数。
param([string]$Output=(Join-Path $PSScriptRoot 'dist/MCD2A.exe'), [string]$EquipmentPresentationPath='')
# 保持参数原样转交；此脚本不安装游戏组件或上传文件。
& (Join-Path $PSScriptRoot 'src/build.ps1') -Output $Output -EquipmentPresentationPath $EquipmentPresentationPath

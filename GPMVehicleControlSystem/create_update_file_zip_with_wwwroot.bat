@echo off
setlocal enabledelayedexpansion
REM 使用 PowerShell 獲取格式化的日期時間
for /f "delims=" %%a in ('powershell -Command "Get-Date -Format 'yyyyMMddHHmmss'"') do set datetime=%%a
set datetime=!datetime:~0,4!!datetime:~4,2!!datetime:~6,2!!datetime:~8,2!!datetime:~10,2!!datetime:~12,2!

REM 获取 .bat 文件所在目录
set "current_dir=%~dp0"

REM 定义要压缩的文件和目录
set files="%current_dir%GPM_VCS.deps.json","%current_dir%GPM_VCS.dll","%current_dir%GPM_VCS.pdb","%current_dir%GPM_VCS.xml","%current_dir%AGVSystemCommonNet6.dll","%current_dir%AGVSystemCommonNet6.pdb","%current_dir%RosBridgeClient.dll","%current_dir%RosBridgeClient.pdb","%current_dir%KGSWebAGVSystemAPI.dll","%current_dir%KGSWebAGVSystemAPI.pdb","%current_dir%EquipmentManagment.dll","%current_dir%EquipmentManagment.pdb","%current_dir%GPM_VCS.runtimeconfig.json","%current_dir%YamlDotNet.dll","%current_dir%Polly.Core.dll","%current_dir%Polly.dll","%current_dir%wwwroot"

REM 创建 zip 文件
set zipfile=%current_dir%VCS_Update_Files_Created_With_wwwroot_!datetime!.zip

REM 使用 PowerShell 压缩文件和目录
powershell Compress-Archive -Path %files% -DestinationPath %zipfile%

echo Files and directory have been compressed into %zipfile%

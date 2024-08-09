@echo off
REM 获取当前日期和时间，格式化为 YYYYMMDD_HHMMSS
setlocal enabledelayedexpansion
for /f "tokens=2 delims==" %%i in ('"wmic os get localdatetime /value"') do set datetime=%%i
set datetime=!datetime:~0,4!!datetime:~4,2!!datetime:~6,2!!datetime:~8,2!!datetime:~10,2!!datetime:~12,2!

REM 获取 .bat 文件所在目录
set "current_dir=%~dp0"

REM 定义要压缩的文件和目录
set files="%current_dir%GPM_VCS.deps.json","%current_dir%GPM_VCS.dll","%current_dir%GPM_VCS.pdb","%current_dir%GPM_VCS.xml","%current_dir%AGVSystemCommonNet6.dll","%current_dir%AGVSystemCommonNet6.pdb","%current_dir%RosBridgeClient.dll","%current_dir%RosBridgeClient.pdb","%current_dir%EquipmentManagment.dll","%current_dir%EquipmentManagment.pdb","%current_dir%GPM_VCS.runtimeconfig.json","%current_dir%wwwroot"

REM 创建 zip 文件
set zipfile=%current_dir%VCS_Update_Files_Created_With_wwwroot_!datetime!.zip

REM 使用 PowerShell 压缩文件和目录
powershell Compress-Archive -Path %files% -DestinationPath %zipfile%

echo Files and directory have been compressed into %zipfile%
pause

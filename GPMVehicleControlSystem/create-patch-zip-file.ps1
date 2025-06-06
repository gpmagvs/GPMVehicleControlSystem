param (
    [string]$outputPath = "C:\GPM Worksapce\AGV_Project\Codes\GPMVehicleControlSystem\GPMVehicleControlSystem\bin\Release\publish",
    [string]$includeswwwroot = "true"
)

# 將字串轉成布林
$includeswwwrootBool = $false
if ($includeswwwroot.ToLower() -eq "true") {
    $includeswwwrootBool = $true
}

Write-Host "壓縮 wwwroot 目錄 ? :$includeswwwrootBool"

# 確認路徑存在
if (-Not (Test-Path $outputPath)) {
    Write-Host "❌ 指定的資料夾不存在：$outputPath"
    exit 1
}

# 解析 GPM_VCS.dll 的版本號
$gpmDllPath = Join-Path $outputPath "GPM_VCS.dll"
$version = "0.0.0.0"
if (Test-Path $gpmDllPath) {
    $fileVersionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($gpmDllPath)
    $version = $fileVersionInfo.FileVersion
    Write-Host "🔍 GPM_VCS.dll 版本號：$version"
} else {
    Write-Host "⚠️ 找不到 GPM_VCS.dll，無法解析版本號"
    $version = "unknown"
}


# 要包含的檔案與資料夾（相對於 outputPath）
$includeItems = @(
    "version.json",
    "GPM_VCS.deps.json",
    "GPM_VCS",
    "GPM_VCS.dll",
    "GPM_VCS.pdb",
    "GPM_VCS.xml",
    "AGVSystemCommonNet6.dll",
    "AGVSystemCommonNet6.pdb",
    "KGSWebAGVSystemAPI.dll",
    "KGSWebAGVSystemAPI.pdb",
    "RosBridgeClient.dll",
    "RosBridgeClient.pdb",
    "EquipmentManagment.dll",
    "EquipmentManagment.pdb",
    "GPM_VCS.staticwebassets.endpoints.json",
    "GPM_VCS.runtimeconfig.json",
    "Polly.Core.dll",
    "Polly.dll",
    "INIFileParser.dll"
)


# 根據參數決定是否加入 wwwroot
if ($includeswwwrootBool) {
    $includeItems += "wwwroot"
}

# 濾出存在的項目
$existingItems = @()
foreach ($item in $includeItems) {
    $fullPath = Join-Path $outputPath $item
    if (Test-Path $fullPath) {
        $existingItems += $item
    } else {
        Write-Host "⚠️ 項目不存在，將略過：$item"
    }
}

if ($existingItems.Count -eq 0) {
    Write-Host "❌ 沒有任何可壓縮的項目，結束執行。"
    exit 1
}

# 建立壓縮檔名與路徑
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

if ($includeswwwrootBool){
    $zipFileName = "vcs_patch_with_wwwroot_v"+$version+"_$timestamp.zip"
}
else{
    $zipFileName = "vcs_patch_v"+$version+"_$timestamp.zip"
}

$zipFilePath = Join-Path -Path $outputPath -ChildPath $zipFileName

# 切換目錄後壓縮（保留相對結構）
Push-Location $outputPath
Compress-Archive -Path $existingItems -DestinationPath $zipFilePath
Pop-Location

Write-Host "✅ 壓縮完成：$zipFilePath"
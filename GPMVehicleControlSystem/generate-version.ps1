param(
    [string]$outputPath,
    [string]$version
)

Write-Host "Outout path :$outputPath"
Write-Host "Verstion    :$version"

if (-not (Test-Path $outputPath)) {
    New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
}

# 取得 git log 原始字串
$gitLogRaw = git -c i18n.logOutputEncoding=utf-8 log --max-count=10


# 把整個資料包成一個物件，加上版本號
$resultObject = [PSCustomObject]@{
    Version = $version
    Commits = $gitLogRaw
}

# 輸出 JSON 檔案路徑
$outputFile = Join-Path $outputPath "version.json"
$resultObject | ConvertTo-Json -Depth 5 | Set-Content -Path $outputFile -Encoding UTF8


Write-Host "version.json 已成功產生於 $outputFile"

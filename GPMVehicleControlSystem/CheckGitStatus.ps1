$branch = (git rev-parse --abbrev-ref HEAD).Trim()
Write-Host "🔍 當前分支：$branch"

if ($branch -eq 'develop' -or $branch -eq 'master') {
    # 只檢查工作目錄有無修改，忽略未追蹤檔案
    $diff = git diff --quiet HEAD; $diffExitCode = $LASTEXITCODE
    if ($diffExitCode -ne 0) {
        Write-Error "❌ 有未提交的修改，請先 commit。"
        exit 69
    }
} else {
    Write-Host "✅ 分支 $branch 無需檢查。"
}
exit 0
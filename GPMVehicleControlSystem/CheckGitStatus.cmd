@echo off
chcp 65001 >nul

setlocal enabledelayedexpansion

echo 檢查分支 commit 狀態

timeout /t 1

REM 取得目前分支
for /f "delims=" %%i in ('git rev-parse --abbrev-ref HEAD') do set CURRENT_BRANCH=%%i

echo ：%CURRENT_BRANCH%

if /I "%CURRENT_BRANCH%"=="develop" (
  git diff --quiet HEAD || (
    echo ❌ Git 有未提交的變更，請先 commit。
	timeout /t 1
    exit /b 1
  )
  
) else if /I "%CURRENT_BRANCH%"=="master" (
  git diff --quiet HEAD || (
    echo ❌ Git 有未提交的變更，請先 commit。
    timeout /t 1
	exit /b 1
  )
  
) else (
  echo ✅ 分支 %CURRENT_BRANCH% 無需檢查 Git 狀態。
)

timeout /t 1
exit /b 0

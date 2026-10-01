@echo off
rem ============================================================
rem  VirtualBT 一键启动
rem  双击本文件即可启动虚拟蓝牙键盘程序
rem ============================================================
setlocal
chcp 936 >nul 2>&1
title VirtualBT Launcher

set "ROOT=%~dp0"
set "PS=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"

echo ============================================
echo   VirtualBT 一键启动
echo   虚拟蓝牙键盘 / Virtual Bluetooth Keyboard
echo ============================================
echo.

if not exist "%PS%" (
    echo [错误] 未找到 PowerShell，无法继续。
    pause
    exit /b 1
)

if not exist "%ROOT%launch-virtualbt.ps1" (
    echo [错误] 未找到 launch-virtualbt.ps1，请确认文件完整。
    pause
    exit /b 1
)

"%PS%" -NoProfile -ExecutionPolicy Bypass -File "%ROOT%launch-virtualbt.ps1"
set "RC=%ERRORLEVEL%"

echo.
if "%RC%"=="0" (
    echo 启动完成。
    exit /b 0
) else (
    echo 启动过程中出现问题，错误码 %RC%。
    echo 请截图或记录以上信息以便排查。
    pause
    exit /b %RC%
)
endlocal

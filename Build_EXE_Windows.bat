@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build_EXE_Windows.ps1"
if errorlevel 1 (
    pause
    exit /b 1
)

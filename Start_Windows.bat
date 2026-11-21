@echo off
setlocal
if not exist "%~dp0dist\Rezak.exe" (
    echo Rezak.exe was not found. Run Build_EXE_Windows.bat first.
    pause
    exit /b 1
)

pushd "%~dp0dist" || exit /b 1
".\Rezak.exe"
set "posterMasterExitCode=%errorlevel%"
popd
if not "%posterMasterExitCode%"=="0" (
    pause
)
exit /b %posterMasterExitCode%

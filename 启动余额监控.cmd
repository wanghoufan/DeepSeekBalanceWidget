@echo off
setlocal

set "PROJECT_ROOT=%~dp0"
set "APP_PATH=%PROJECT_ROOT%release\DeepSeekBalanceWidget.exe"
set "PUBLISH_SCRIPT=%PROJECT_ROOT%scripts\publish.ps1"

if not exist "%APP_PATH%" (
    echo First run: publishing the application...
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PUBLISH_SCRIPT%"
    if errorlevel 1 (
        echo.
        echo Publish failed. Make sure the .NET 8 SDK is installed.
        pause
        exit /b 1
    )
)

start "" "%APP_PATH%"
endlocal

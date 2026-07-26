@echo off
rem ── Dragonfly launcher ─────────────────────────────────────
rem Opens the Dragonfly window. Builds it first.
rem Requires the .NET SDK (https://dotnet.microsoft.com/download).
cd /d "%~dp0"
set EXE=bin\Release\net10.0-windows\Dragonfly.exe
echo Building Dragonfly...
dotnet build -c Release
if errorlevel 1 (
    echo.
    echo Build failed. Make sure the .NET SDK is installed: https://dotnet.microsoft.com/download
    pause
    exit /b 1
)
start "" "%EXE%"

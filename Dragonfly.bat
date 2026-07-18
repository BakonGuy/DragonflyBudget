@echo off
rem ── Dragonfly launcher ─────────────────────────────────────
rem Opens the Dragonfly window. Builds it first if it hasn't been built yet.
rem Requires the .NET SDK (https://dotnet.microsoft.com/download).
cd /d "%~dp0"
set EXE=bin\Release\net9.0-windows\Dragonfly.exe
if not exist "%EXE%" (
    echo Building Dragonfly for the first time, please wait...
    dotnet build -c Release || (echo. & echo Build failed. Make sure the .NET SDK is installed: https://dotnet.microsoft.com/download & pause & exit /b 1)
)
start "" "%EXE%"

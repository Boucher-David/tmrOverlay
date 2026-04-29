@echo off
setlocal

set "ROOT=%~dp0"
set "APP=%ROOT%src\TmrOverlay.App\bin\Release\net8.0-windows10.0.19041.0\TmrOverlay.App.exe"

if not exist "%APP%" (
    echo Could not find the Release TmrOverlay.App.exe.
    echo Run this from the repo root first:
    echo dotnet build .\src\TmrOverlay.App\TmrOverlay.App.csproj -c Release
    pause
    exit /b 1
)

start "" "%APP%"

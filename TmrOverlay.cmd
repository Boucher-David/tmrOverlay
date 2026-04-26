@echo off
setlocal

set "ROOT=%~dp0"
set "APP="

if exist "%ROOT%src\TmrOverlay.App\bin\Release\net8.0-windows10.0.19041.0\TmrOverlay.App.exe" (
    set "APP=%ROOT%src\TmrOverlay.App\bin\Release\net8.0-windows10.0.19041.0\TmrOverlay.App.exe"
) else if exist "%ROOT%src\TmrOverlay.App\bin\Debug\net8.0-windows10.0.19041.0\TmrOverlay.App.exe" (
    set "APP=%ROOT%src\TmrOverlay.App\bin\Debug\net8.0-windows10.0.19041.0\TmrOverlay.App.exe"
)

if not defined APP (
    echo Could not find TmrOverlay.App.exe.
    echo Build the solution first, then run this launcher again.
    pause
    exit /b 1
)

start "" "%APP%"


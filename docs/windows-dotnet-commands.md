# Windows .NET Commands

Run these from the repository root in PowerShell:

```powershell
cd C:\Users\David\Desktop\Code\TMROverlay
```

## Common Checks

Restore packages:

```powershell
dotnet restore .\tmrOverlay.sln
```

Build the app in Debug:

```powershell
dotnet build .\src\TmrOverlay.App\TmrOverlay.App.csproj -c Debug
```

Clean and build the app in Release:

```powershell
dotnet clean .\tmrOverlay.sln -c Release; Remove-Item .\src\TmrOverlay.App\bin, .\src\TmrOverlay.App\obj, .\src\TmrOverlay.Core\bin, .\src\TmrOverlay.Core\obj, .\artifacts\TmrOverlay-win-x64, .\artifacts\TmrOverlay-win-x64.zip -Recurse -Force -ErrorAction SilentlyContinue; dotnet build .\src\TmrOverlay.App\TmrOverlay.App.csproj -c Release
```

Run tests:

```powershell
dotnet test .\tmrOverlay.sln
```

## Run Locally

Run the app from source:

```powershell
dotnet run --project .\src\TmrOverlay.App\TmrOverlay.App.csproj -c Debug
```

Run from source with raw capture enabled:

```powershell
$env:TMR_TelemetryCapture__RawCaptureEnabled = "true"
dotnet run --project .\src\TmrOverlay.App\TmrOverlay.App.csproj -c Debug
```

Run with repo-local storage enabled:

```powershell
$env:TMR_Storage__UseRepositoryLocalStorage = "true"
dotnet run --project .\src\TmrOverlay.App\TmrOverlay.App.csproj -c Debug
```

After the app has been built once, you can also start the built executable with:

```powershell
.\TmrOverlay.cmd
```

## Release Package

These are the same Windows build commands shown in the settings overlay.

Clean:

```powershell
dotnet clean .\tmrOverlay.sln -c Release; Remove-Item .\src\TmrOverlay.App\bin, .\src\TmrOverlay.App\obj, .\src\TmrOverlay.Core\bin, .\src\TmrOverlay.Core\obj, .\artifacts\TmrOverlay-win-x64, .\artifacts\TmrOverlay-win-x64.zip -Recurse -Force -ErrorAction SilentlyContinue
```

Clean + Build:

```powershell
dotnet clean .\tmrOverlay.sln -c Release; Remove-Item .\src\TmrOverlay.App\bin, .\src\TmrOverlay.App\obj, .\src\TmrOverlay.Core\bin, .\src\TmrOverlay.Core\obj, .\artifacts\TmrOverlay-win-x64, .\artifacts\TmrOverlay-win-x64.zip -Recurse -Force -ErrorAction SilentlyContinue; dotnet build .\src\TmrOverlay.App\TmrOverlay.App.csproj -c Release
```

Publish:

```powershell
dotnet publish .\src\TmrOverlay.App\TmrOverlay.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\TmrOverlay-win-x64
```

Zip:

```powershell
Compress-Archive -Path .\artifacts\TmrOverlay-win-x64\* -DestinationPath .\artifacts\TmrOverlay-win-x64.zip -Force
```

## Useful Paths

App data:

```powershell
%LOCALAPPDATA%\TmrOverlay
```

Logs:

```powershell
%LOCALAPPDATA%\TmrOverlay\logs
```

Diagnostics:

```powershell
%LOCALAPPDATA%\TmrOverlay\diagnostics
```

Captures:

```powershell
%LOCALAPPDATA%\TmrOverlay\captures
```

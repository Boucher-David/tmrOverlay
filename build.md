# Build Commands

Quick command reference for building and validating `TmrOverlay`.

## Windows PowerShell

Run from the repository root:

```powershell
cd C:\Users\David\Desktop\Code\TMROverlay
```

Restore packages:

```powershell
dotnet restore .\tmrOverlay.sln
```

Build Debug:

```powershell
dotnet build .\src\TmrOverlay.App\TmrOverlay.App.csproj -c Debug
```

Clean and build Release:

```powershell
dotnet clean .\tmrOverlay.sln -c Release; Remove-Item .\src\TmrOverlay.App\bin, .\src\TmrOverlay.App\obj, .\src\TmrOverlay.Core\bin, .\src\TmrOverlay.Core\obj, .\artifacts\TmrOverlay-win-x64, .\artifacts\TmrOverlay-win-x64.zip -Recurse -Force -ErrorAction SilentlyContinue; dotnet build .\src\TmrOverlay.App\TmrOverlay.App.csproj -c Release
```

Run tests:

```powershell
dotnet test .\tmrOverlay.sln
```

Run from source:

```powershell
dotnet run --project .\src\TmrOverlay.App\TmrOverlay.App.csproj -c Debug
```

Run from source with raw capture enabled:

```powershell
$env:TMR_TelemetryCapture__RawCaptureEnabled = "true"
dotnet run --project .\src\TmrOverlay.App\TmrOverlay.App.csproj -c Debug
```

Run from source with repo-local storage:

```powershell
$env:TMR_Storage__UseRepositoryLocalStorage = "true"
dotnet run --project .\src\TmrOverlay.App\TmrOverlay.App.csproj -c Debug
```

Run the built app:

```powershell
.\TmrOverlay.cmd
```

Publish self-contained Windows build:

```powershell
dotnet publish .\src\TmrOverlay.App\TmrOverlay.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\TmrOverlay-win-x64
```

Zip published build:

```powershell
Compress-Archive -Path .\artifacts\TmrOverlay-win-x64\* -DestinationPath .\artifacts\TmrOverlay-win-x64.zip -Force
```

## Capture Analysis

Create compact synthesis from a raw capture:

```powershell
python .\tools\analysis\synthesize_capture.py --capture .\captures\capture-YYYYMMDD-HHMMSS-mmm --output .\capture-synthesis.json
```

Export compact history from a raw capture:

```powershell
python .\tools\analysis\export_history_from_capture.py --capture .\captures\capture-YYYYMMDD-HHMMSS-mmm --output-root .\history-export
```

## macOS Harness

Run from `local-mac/TmrOverlayMac`:

```bash
swift build
```

```bash
swift test
```

```bash
./run.sh
```

Run mock raw capture:

```bash
TMR_MAC_RAW_CAPTURE_ENABLED=true ./run.sh
```

Run demo states:

```bash
TMR_MAC_DEMO_STATES=true ./run.sh
```

## Repo Validation

```bash
git diff --check
```

```bash
rg -n "^(<<<<<<<|>>>>>>>|=======)$" --glob '!captures/**' --glob '!captures-latest/**' --glob '!local-mac/TmrOverlayMac/.build/**' --glob '!**/*.png' --glob '!**/*.jpg' --glob '!**/*.bin'
```

```bash
env PYTHONPYCACHEPREFIX=/tmp/tmr-pycache python3 -m py_compile tools/analysis/synthesize_capture.py tools/analysis/export_history_from_capture.py tools/analysis/yaml_forensics.py
```

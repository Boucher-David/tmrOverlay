param(
    [Parameter(Mandatory = $true)]
    [string]$PublishPath,

    [Parameter(Mandatory = $true)]
    [string]$ManifestPath,

    [Parameter(Mandatory = $true)]
    [string]$PackageName,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$InformationalVersion
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $PublishPath -PathType Container)) {
    throw "Publish path does not exist: $PublishPath"
}

$forbiddenTopLevelDirectories = @(
    ".git",
    ".github",
    "captures",
    "captures-latest",
    "docs",
    "history",
    "local-mac",
    "mocks",
    "skills",
    "tests",
    "tools"
)
$requiredFiles = @(
    "TMROverlay.exe",
    "appsettings.json",
    "Assets\TMRLogo.png"
)
$allowedAssetFiles = @(
    "Assets\TMRLogo.png"
)

$allowedAssetPatterns = @(
    '^Assets\\CarSpecs\\[^\\]+\.json$',
    '^Assets\\TrackMaps\\[^\\]+\.json$'
)

$missingFiles = @($requiredFiles | Where-Object { -not (Test-Path -LiteralPath (Join-Path $PublishPath $_) -PathType Leaf) })
if ($missingFiles.Count -gt 0) {
    throw "Published package is missing required files: $($missingFiles -join ', ')"
}

$leakedDirectories = @(Get-ChildItem -LiteralPath $PublishPath -Force |
    Where-Object { $_.PSIsContainer -and $forbiddenTopLevelDirectories -contains $_.Name.ToLowerInvariant() })
if ($leakedDirectories.Count -gt 0) {
    $names = $leakedDirectories | ForEach-Object { $_.Name }
    throw "Published package contains repo/dev directories that must not ship: $($names -join ', ')"
}

$assetRoot = Join-Path $PublishPath "Assets"
$unexpectedAssetFiles = @(Get-ChildItem -LiteralPath $assetRoot -Recurse -File -Force |
    Where-Object {
        $relativePath = [System.IO.Path]::GetRelativePath($PublishPath, $_.FullName)
        $isAllowedFile = $allowedAssetFiles -contains $relativePath
        $isAllowedPattern = $false
        foreach ($pattern in $allowedAssetPatterns) {
            if ($relativePath -match $pattern) {
                $isAllowedPattern = $true
                break
            }
        }

        -not ($isAllowedFile -or $isAllowedPattern)
    })
if ($unexpectedAssetFiles.Count -gt 0) {
    $names = $unexpectedAssetFiles | ForEach-Object { [System.IO.Path]::GetRelativePath($PublishPath, $_.FullName) }
    throw "Published package contains unexpected asset files: $($names -join ', ')"
}

$entries = Get-ChildItem -LiteralPath $PublishPath -Recurse -Force |
    Sort-Object FullName |
    ForEach-Object {
        $relativePath = [System.IO.Path]::GetRelativePath($PublishPath, $_.FullName)
        if ($_.PSIsContainer) {
            "         dir  $relativePath"
        } else {
            "{0,12}  {1}" -f $_.Length, $relativePath
        }
    }

@(
    "package=$PackageName",
    "version=$Version",
    "informationalVersion=$InformationalVersion",
    "generatedAtUtc=$((Get-Date).ToUniversalTime().ToString('o'))",
    "",
    "Published files:"
) + $entries | Set-Content -Path $ManifestPath -Encoding ascii

[CmdletBinding()]
param(
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot "src\DeepSeekBalanceWidget\DeepSeekBalanceWidget.csproj"
$releaseDir = Join-Path $projectRoot "release"

New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

dotnet publish $projectFile `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $releaseDir

if ($LASTEXITCODE -ne 0) {
    throw "Publish failed. dotnet publish returned exit code $LASTEXITCODE."
}

$appPath = Join-Path $releaseDir "DeepSeekBalanceWidget.exe"
if (-not (Test-Path -LiteralPath $appPath)) {
    throw "Publish completed, but the executable was not found at $appPath."
}

Write-Host ""
Write-Host "Publish completed:" -ForegroundColor Green
Write-Host $appPath

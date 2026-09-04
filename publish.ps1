<#
.SYNOPSIS
    Builds and packages AudioSwitcher into both Framework-Dependent and Standalone releases.

.DESCRIPTION
    Compiles two editions for Windows x64 (.NET 9):
    1. Framework-Dependent (~1.8 MB): Ultra-lightweight, requires .NET 9 Desktop Runtime.
    2. Standalone (~71 MB): Self-contained zero-dependency bundle.

.PARAMETER Version
    Optional semantic version string (e.g. "1.0.0"). If omitted, extracts from AudioSwitcher.csproj.

.EXAMPLE
    .\publish.ps1
    .\publish.ps1 -Version "1.0.1"
#>

[CmdletBinding()]
param(
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

# 1. Determine Version
if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$csproj = Get-Content (Join-Path $ScriptDir "AudioSwitcher.csproj")
    $VersionNode = $csproj.SelectSingleNode("//Version")
    if ($VersionNode -and $VersionNode.InnerText) {
        $Version = $VersionNode.InnerText.Trim()
    } else {
        $Version = "1.0.0"
    }
}

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  Building AudioSwitcher v$Version (Dual Release)" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 2. Stop any running instances to avoid file lock
Get-Process AudioSwitcher -ErrorAction SilentlyContinue | Stop-Process -Force

# 3. Setup Output Directories
$DistDir = Join-Path $ScriptDir "dist"
$StagingFdd = Join-Path $DistDir "staging-fdd"
$StagingStandalone = Join-Path $DistDir "staging-standalone"

if (Test-Path $DistDir) {
    Remove-Item $DistDir -Recurse -Force
}
New-Item -ItemType Directory -Path $StagingFdd -Force | Out-Null
New-Item -ItemType Directory -Path $StagingStandalone -Force | Out-Null

# Helper to copy docs
function Copy-DocsToStaging($stagingPath) {
    if (Test-Path (Join-Path $ScriptDir "readme.md")) {
        Copy-Item (Join-Path $ScriptDir "readme.md") -Destination $stagingPath -Force
    }
    if (Test-Path (Join-Path $ScriptDir "docs")) {
        Copy-Item (Join-Path $ScriptDir "docs") -Destination (Join-Path $stagingPath "docs") -Recurse -Force
    }
    if (Test-Path (Join-Path $ScriptDir "LICENSE")) {
        Copy-Item (Join-Path $ScriptDir "LICENSE") -Destination $stagingPath -Force
    }
    Get-ChildItem -Path $stagingPath -Filter "*.pdb" | Remove-Item -Force
}

# -------------------------------------------------------------
# BUILD 1: Framework-Dependent (~1.8 MB)
# -------------------------------------------------------------
Write-Host "`n[1/2] Building Framework-Dependent release (~1.8 MB)..." -ForegroundColor Yellow

$fddArgs = @(
    "publish", "AudioSwitcher.csproj",
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "false",
    "-p:PublishSingleFile=true",
    "-p:Version=$Version",
    "-p:FileVersion=$Version.0",
    "-p:AssemblyVersion=$Version.0",
    "-o", $StagingFdd
)

dotnet @fddArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$FddExe = Join-Path $DistDir "AudioSwitcher.exe"
Copy-Item (Join-Path $StagingFdd "AudioSwitcher.exe") -Destination $FddExe -Force

Copy-DocsToStaging $StagingFdd
$FddZip = Join-Path $DistDir "AudioSwitcher-v$Version-win-x64.zip"
Write-Host "--> Creating archive: $(Split-Path -Leaf $FddZip)..." -ForegroundColor Cyan
Compress-Archive -Path "$StagingFdd\*" -DestinationPath $FddZip -Force
Remove-Item $StagingFdd -Recurse -Force

# -------------------------------------------------------------
# BUILD 2: Self-Contained Standalone (~71 MB)
# -------------------------------------------------------------
Write-Host "`n[2/2] Building Standalone self-contained release (~71 MB)..." -ForegroundColor Yellow

$standaloneArgs = @(
    "publish", "AudioSwitcher.csproj",
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:Version=$Version",
    "-p:FileVersion=$Version.0",
    "-p:AssemblyVersion=$Version.0",
    "-o", $StagingStandalone
)

dotnet @standaloneArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$StandaloneExe = Join-Path $DistDir "AudioSwitcher-standalone.exe"
Copy-Item (Join-Path $StagingStandalone "AudioSwitcher.exe") -Destination $StandaloneExe -Force

Copy-DocsToStaging $StagingStandalone
$StandaloneZip = Join-Path $DistDir "AudioSwitcher-v$Version-win-x64-standalone.zip"
Write-Host "--> Creating archive: $(Split-Path -Leaf $StandaloneZip)..." -ForegroundColor Cyan
Compress-Archive -Path "$StagingStandalone\*" -DestinationPath $StandaloneZip -Force
Remove-Item $StagingStandalone -Recurse -Force

# -------------------------------------------------------------
# Report Results
# -------------------------------------------------------------
$fddExeMb = [math]::Round(((Get-Item $FddExe).Length / 1MB), 2)
$fddZipMb = [math]::Round(((Get-Item $FddZip).Length / 1MB), 2)
$standaloneExeMb = [math]::Round(((Get-Item $StandaloneExe).Length / 1MB), 2)
$standaloneZipMb = [math]::Round(((Get-Item $StandaloneZip).Length / 1MB), 2)

Write-Host "`n==================================================" -ForegroundColor Green
Write-Host "  Dual Release Build Complete!" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
Write-Host "  [Framework-Dependent]" -ForegroundColor Cyan
Write-Host "    Executable : $FddExe ($fddExeMb MB)" -ForegroundColor White
Write-Host "    Zip Bundle : $FddZip ($fddZipMb MB)" -ForegroundColor White
Write-Host "`n  [Standalone Self-Contained]" -ForegroundColor Cyan
Write-Host "    Executable : $StandaloneExe ($standaloneExeMb MB)" -ForegroundColor White
Write-Host "    Zip Bundle : $StandaloneZip ($standaloneZipMb MB)" -ForegroundColor White
Write-Host "==================================================" -ForegroundColor Green

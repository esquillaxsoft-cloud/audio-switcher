<#
.SYNOPSIS
    Builds and packages AudioSwitcher into a standalone, single-file release.

.DESCRIPTION
    Compiles a self-contained, single-file executable for Windows x64 (.NET 9)
    and packages it into the dist/ directory along with a release zip archive.

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
Write-Host "  Building AudioSwitcher v$Version Release" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

# 2. Stop any running instances to avoid file lock
Get-Process AudioSwitcher -ErrorAction SilentlyContinue | Stop-Process -Force

# 3. Setup Output Directories
$DistDir = Join-Path $ScriptDir "dist"
$StagingDir = Join-Path $DistDir "staging"

if (Test-Path $DistDir) {
    Remove-Item $DistDir -Recurse -Force
}
New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null

# 4. Dotnet Publish
Write-Host "`n--> Publishing self-contained win-x64 single-file executable..." -ForegroundColor Yellow

$publishArgs = @(
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
    "-o", $StagingDir
)

dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# 5. Prepare Package
Write-Host "`n--> Packaging release artifacts..." -ForegroundColor Yellow

# Copy main standalone executable to dist root
$PublishedExe = Join-Path $StagingDir "AudioSwitcher.exe"
$DistExe = Join-Path $DistDir "AudioSwitcher.exe"
Copy-Item $PublishedExe -Destination $DistExe -Force

# Include documentation
if (Test-Path (Join-Path $ScriptDir "readme.md")) {
    Copy-Item (Join-Path $ScriptDir "readme.md") -Destination $StagingDir -Force
}
if (Test-Path (Join-Path $ScriptDir "docs")) {
    Copy-Item (Join-Path $ScriptDir "docs") -Destination (Join-Path $StagingDir "docs") -Recurse -Force
}
if (Test-Path (Join-Path $ScriptDir "LICENSE")) {
    Copy-Item (Join-Path $ScriptDir "LICENSE") -Destination $StagingDir -Force
}

# Remove unnecessary PDBs and extra files from zip staging if present
Get-ChildItem -Path $StagingDir -Filter "*.pdb" | Remove-Item -Force

# Create Zip Archive
$ZipPath = Join-Path $DistDir "AudioSwitcher-v$Version-win-x64.zip"
Write-Host "--> Creating archive: $(Split-Path -Leaf $ZipPath)..." -ForegroundColor Cyan
Compress-Archive -Path "$StagingDir\*" -DestinationPath $ZipPath -Force

# Clean up staging folder
Remove-Item $StagingDir -Recurse -Force

# 6. Report Results
$exeSizeMb = [math]::Round(((Get-Item $DistExe).Length / 1MB), 2)
$zipSizeMb = [math]::Round(((Get-Item $ZipPath).Length / 1MB), 2)

Write-Host "`n==================================================" -ForegroundColor Green
Write-Host "  Release Build Complete!" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
Write-Host "  Standalone Executable : $DistExe ($exeSizeMb MB)" -ForegroundColor White
Write-Host "  Release Zip Package   : $ZipPath ($zipSizeMb MB)" -ForegroundColor White
Write-Host "==================================================" -ForegroundColor Green

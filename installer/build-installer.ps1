<#
.SYNOPSIS
    Builds the Release publication package and compiles the Inno Setup installer for MoneyFlow Desktop ERP.
.DESCRIPTION
    1. Runs dotnet publish for win-x64 Release configuration into .\publish\.
    2. Searches for Inno Setup compiler (ISCC.exe).
    3. If ISCC.exe is available, compiles .\installer\MoneyFlowSetup.iss into .\installer\Output\MoneyFlowSetup.exe.
    4. Outputs file sizes and SHA256 checksums.
#>

[CmdletBinding()]
param(
    [switch]$SkipPublish = $false
)

$ErrorActionPreference = "Stop"
$rootDir = (Get-Item $PSScriptRoot).Parent.FullName
$publishDir = Join-Path $rootDir "publish"
$installerScript = Join-Path $rootDir "installer\MoneyFlowSetup.iss"
$outputDir = Join-Path $rootDir "installer\Output"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host " MoneyFlow Desktop ERP — Build & Packaging Pipeline" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

# 1. Publish Release binary
if (-not $SkipPublish) {
    Write-Host "`n[Step 1/3] Publishing MoneyFlow.Desktop (Release, win-x64)..." -ForegroundColor Yellow
    if (Test-Path $publishDir) {
        Remove-Item -Path $publishDir -Recurse -Force
    }
    
    $desktopProj = Join-Path $rootDir "MoneyFlow.Desktop\MoneyFlow.Desktop.csproj"
    dotnet publish $desktopProj -c Release -r win-x64 --self-contained false -o $publishDir
    
    if (-not (Test-Path (Join-Path $publishDir "MoneyFlow.Desktop.exe"))) {
        throw "Publish failed: MoneyFlow.Desktop.exe was not found in $publishDir."
    }
    Write-Host "-> Published successfully to $publishDir" -ForegroundColor Green
} else {
    Write-Host "`n[Step 1/3] Skipping dotnet publish as requested." -ForegroundColor Gray
}

# 2. Check for Inno Setup Compiler
Write-Host "`n[Step 2/3] Checking for Inno Setup 6 (ISCC.exe)..." -ForegroundColor Yellow

$isccCandidates = @(
    "ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
)

$isccPath = $null
foreach ($candidate in $isccCandidates) {
    if (Get-Command $candidate -ErrorAction SilentlyContinue) {
        $isccPath = (Get-Command $candidate).Source
        break
    } elseif (Test-Path $candidate) {
        $isccPath = $candidate
        break
    }
}

if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

if ($isccPath) {
    Write-Host "-> Found Inno Setup Compiler at: $isccPath" -ForegroundColor Green
    Write-Host "`n[Step 3/3] Compiling installer MoneyFlowSetup.exe..." -ForegroundColor Yellow
    
    & $isccPath $installerScript
    
    $setupExe = Join-Path $outputDir "MoneyFlowSetup.exe"
    if (Test-Path $setupExe) {
        $hash = Get-FileHash -Path $setupExe -Algorithm SHA256
        $sizeMb = [math]::Round(((Get-Item $setupExe).Length / 1MB), 2)
        Write-Host "`n[SUCCESS] Installer generated successfully!" -ForegroundColor Green
        Write-Host "Output Path: $setupExe ($sizeMb MB)" -ForegroundColor Cyan
        Write-Host "SHA256:      $($hash.Hash)" -ForegroundColor Cyan
    }
} else {
    Write-Host "`n-> Inno Setup (ISCC.exe) is not installed on this system." -ForegroundColor DarkYellow
    Write-Host "   The Inno Setup script is prepared at: $installerScript" -ForegroundColor Gray
    Write-Host "   Install Inno Setup 6 from https://jrsoftware.org/isinfo.php to compile MoneyFlowSetup.exe." -ForegroundColor Gray
    Write-Host "`n[SUCCESS] Standalone application distribution is ready in: $publishDir" -ForegroundColor Green
    
    $exePath = Join-Path $publishDir "MoneyFlow.Desktop.exe"
    $exeHash = Get-FileHash -Path $exePath -Algorithm SHA256
    Write-Host "Application Exe: $exePath" -ForegroundColor Cyan
    Write-Host "SHA256:          $($exeHash.Hash)" -ForegroundColor Cyan
}

Write-Host "`n========================================================" -ForegroundColor Cyan
Write-Host " Packaging pipeline complete." -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

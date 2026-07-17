param(
    [string]$Version = 'v1.0.3',
    [string]$Runtime = 'win-x64'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$appProject = Join-Path $repoRoot 'src\EasyLog.App\EasyLog.App.csproj'
$artifactsRoot = Join-Path $repoRoot 'artifacts'
$stagingRoot = Join-Path $artifactsRoot 'package'
$packageName = "LogPilot-AAOS-Log-Viewer-$Version-$Runtime"
$packageRoot = Join-Path $stagingRoot $packageName
$publishRoot = $packageRoot
$archivePath = Join-Path $artifactsRoot "$packageName.7z"
$betaGuideSource = Join-Path $repoRoot 'docs\Release-Notes-v1.0.3.md'
$betaGuideTarget = Join-Path $packageRoot 'RELEASE-NOTES-v1.0.3.md'
$sampleLogSource = Join-Path $repoRoot 'sample-logs\aaos-sample.log'
$sampleLogTargetDir = Join-Path $packageRoot 'sample-logs'
$toolsGuideDir = Join-Path $packageRoot 'tools'
$toolsGuidePath = Join-Path $toolsGuideDir 'README.txt'
$filterSetDir = Join-Path $packageRoot 'LogFilter'
$iconScript = Join-Path $repoRoot 'scripts\Generate-AppIcon.ps1'

function Resolve-SevenZipPath {
    $candidates = @(
        (Join-Path ${env:ProgramFiles} '7-Zip\7z.exe'),
        (Join-Path ${env:ProgramFiles(x86)} '7-Zip\7z.exe'),
        '7z.exe'
    )

    foreach ($candidate in $candidates) {
        if ($candidate -like '*\\*') {
            if (Test-Path $candidate) {
                return $candidate
            }

            continue
        }

        return $candidate
    }

    throw '7z.exe를 찾을 수 없습니다.'
}

if (Test-Path $packageRoot) {
    Remove-Item $packageRoot -Recurse -Force
}

if (Test-Path $archivePath) {
    Remove-Item $archivePath -Force
}

New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

& powershell -NoProfile -ExecutionPolicy Bypass -File $iconScript

& dotnet publish $appProject -c Release -r $Runtime --self-contained true -p:PublishSingleFile=false -o $publishRoot
if ($LASTEXITCODE -ne 0) {
    throw 'dotnet publish가 실패했습니다.'
}

Copy-Item (Join-Path $repoRoot 'README.md') (Join-Path $packageRoot 'README.md') -Force
if (Test-Path $betaGuideSource) {
    Copy-Item $betaGuideSource $betaGuideTarget -Force
}

if (Test-Path $sampleLogSource) {
    New-Item -ItemType Directory -Path $sampleLogTargetDir -Force | Out-Null
    Copy-Item $sampleLogSource (Join-Path $sampleLogTargetDir 'aaos-sample.log') -Force
}

New-Item -ItemType Directory -Path $filterSetDir -Force | Out-Null

New-Item -ItemType Directory -Path $toolsGuideDir -Force | Out-Null
@'
Optional external tools
=======================

The app itself is published as self-contained, so it can launch without installing .NET.

To use ADB live collection without relying on a system-wide Android SDK, place these files in this folder:
- adb.exe
- AdbWinApi.dll
- AdbWinUsbApi.dll

To use 7z export without relying on a system-wide 7-Zip installation, place these files in this folder:
- 7z.exe
- 7z.dll
'@ | Set-Content -Path $toolsGuidePath -Encoding UTF8

# Bundle 7z.exe and 7z.dll into tools/ so end-users don't need a system-wide 7-Zip install
$sevenZipInstallDirs = @(
    (Join-Path ${env:ProgramFiles} '7-Zip'),
    (Join-Path ${env:ProgramFiles(x86)} '7-Zip')
)

$sevenZipSourceDir = $null
foreach ($dir in $sevenZipInstallDirs) {
    if ((Test-Path (Join-Path $dir '7z.exe')) -and (Test-Path (Join-Path $dir '7z.dll'))) {
        $sevenZipSourceDir = $dir
        break
    }
}

if ($sevenZipSourceDir) {
    Copy-Item (Join-Path $sevenZipSourceDir '7z.exe') (Join-Path $toolsGuideDir '7z.exe') -Force
    Copy-Item (Join-Path $sevenZipSourceDir '7z.dll') (Join-Path $toolsGuideDir '7z.dll') -Force
    Write-Host "Bundled 7z from: $sevenZipSourceDir"
} else {
    Write-Warning '7-Zip installation not found. 7z.exe/7z.dll were NOT bundled into tools/. End-users will need a system-wide 7-Zip install.'
}

# Bundle adb.exe, AdbWinApi.dll, AdbWinUsbApi.dll into tools/ so end-users don't need Android SDK
$adbRequiredFiles = @('adb.exe', 'AdbWinApi.dll', 'AdbWinUsbApi.dll')
$adbSearchDirs = @(
    (Join-Path $env:LOCALAPPDATA 'Android\Sdk\platform-tools'),
    (Join-Path $env:USERPROFILE 'AppData\Local\Android\Sdk\platform-tools'),
    'C:\platform-tools',
    (Join-Path $env:ProgramFiles 'Android\platform-tools'),
    (Join-Path ${env:ProgramFiles(x86)} 'Android\platform-tools')
)
if ($env:ANDROID_HOME) {
    $adbSearchDirs = @((Join-Path $env:ANDROID_HOME 'platform-tools')) + $adbSearchDirs
}
if ($env:ANDROID_SDK_ROOT) {
    $adbSearchDirs = @((Join-Path $env:ANDROID_SDK_ROOT 'platform-tools')) + $adbSearchDirs
}

$adbSourceDir = $null
foreach ($dir in $adbSearchDirs) {
    if (-not (Test-Path $dir)) { continue }
    $allFound = $true
    foreach ($f in $adbRequiredFiles) {
        if (-not (Test-Path (Join-Path $dir $f))) { $allFound = $false; break }
    }
    if ($allFound) {
        $adbSourceDir = $dir
        break
    }
}

if ($adbSourceDir) {
    foreach ($f in $adbRequiredFiles) {
        Copy-Item (Join-Path $adbSourceDir $f) (Join-Path $toolsGuideDir $f) -Force
    }
    Write-Host "Bundled adb from: $adbSourceDir"
} else {
    Write-Warning 'Android SDK platform-tools not found. adb.exe was NOT bundled into tools/. End-users will need Android SDK or manual adb placement.'
}

$sevenZipPath = Resolve-SevenZipPath
Push-Location $stagingRoot
try {
    & $sevenZipPath a -t7z -mx=9 -y $archivePath ".\\$packageName\\*"
    if ($LASTEXITCODE -ne 0) {
        throw '7z 압축 생성이 실패했습니다.'
    }
}
finally {
    Pop-Location
}

Write-Host "Created beta package: $archivePath"
Write-Host "Staging folder: $packageRoot"


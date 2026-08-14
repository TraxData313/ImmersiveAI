# Builds the mod and assembles a CLEAN, reproducible module layout for the Steam Workshop
# (or any manual distribution) under dist\ImmersiveAI - exactly what deploy.ps1 puts into the
# game, but from scratch every time, so no stale file from an old build can ride along.
# Also drops a versioned zip beside it, reading the version from module\SubModule.xml.
# Usage: powershell -ExecutionPolicy Bypass -File tools\package.ps1 [-Configuration Release]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$distRoot = Join-Path $repoRoot "dist"
$moduleDir = Join-Path $distRoot "ImmersiveAI"
$binDir = Join-Path $moduleDir "bin\Win64_Shipping_Client"

# A clean slate is the whole point of packaging.
if (Test-Path $moduleDir) { Remove-Item $moduleDir -Recurse -Force }

dotnet build (Join-Path $repoRoot "src\ImmersiveAI.Module\ImmersiveAI.Module.csproj") -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

New-Item -ItemType Directory -Force $binDir | Out-Null
Copy-Item (Join-Path $repoRoot "module\SubModule.xml") $moduleDir -Force

$outDir = Join-Path $repoRoot "src\ImmersiveAI.Module\bin\$Configuration"
Copy-Item (Join-Path $outDir "ImmersiveAI.dll") $binDir -Force
Copy-Item (Join-Path $outDir "ImmersiveAI.Core.dll") $binDir -Force
Copy-Item (Join-Path $outDir "Newtonsoft.Json.dll") $binDir -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $outDir "0Harmony.dll") $binDir -Force -ErrorAction SilentlyContinue
# MIT obliges the notice to travel with the DLL it covers.
Copy-Item (Join-Path $repoRoot "lib\0Harmony.LICENSE.txt") $binDir -Force -ErrorAction SilentlyContinue

# --- The voice host ---------------------------------------------------------------------------
# The separate net8.0 TTS process, published FRAMEWORK-DEPENDENT and SINGLE-FILE. The full
# reasoning for both choices lives in deploy.ps1; the short of it is that one inert .exe cannot
# confuse the game's assembly loader the way a heap of net8.0 DLLs in this folder could, and that
# 33 MB of bundled runtime in every player's download buys nothing for a feature already gated
# behind a hand-installed, multi-gigabyte local engine. Players without the .NET 8 runtime simply
# get no voices, and the mod says so politely.
# Differences from deploy.ps1, both deliberate: no pdb ships, and a voice host that EXISTS but
# fails to build stops the packaging dead. A release that quietly lost its voices is a defect, and
# a clean-slate packager is exactly where that must be caught.
$voiceHostProj = Join-Path $repoRoot "src\ImmersiveAI.VoiceHost\ImmersiveAI.VoiceHost.csproj"
$voiceHostShipped = $false
if (Test-Path $voiceHostProj) {
    # Clean slate here too - a stale exe from an older build must never ride along.
    $voiceOut = Join-Path $repoRoot "src\ImmersiveAI.VoiceHost\bin\publish\package-$Configuration"
    if (Test-Path $voiceOut) { Remove-Item $voiceOut -Recurse -Force }

    dotnet publish $voiceHostProj -c $Configuration -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o $voiceOut
    if ($LASTEXITCODE -ne 0) { throw "The voice host failed to build - refusing to package a release without it." }

    # The game spawns it BY NAME; a renamed exe would ship as a silent no-voices bug.
    $hostExe = Join-Path $voiceOut "ImmersiveAI.VoiceHost.exe"
    if (-not (Test-Path $hostExe)) { throw "Published the voice host but no ImmersiveAI.VoiceHost.exe came out - the game spawns it by that exact name." }
    Copy-Item $hostExe $binDir -Force
    # Anything the host bundles that obliges a notice travels with it, same habit as Harmony.
    Copy-Item (Join-Path $repoRoot "src\ImmersiveAI.VoiceHost\THIRD-PARTY-NOTICES.txt") $binDir -Force -ErrorAction SilentlyContinue
    $voiceHostShipped = $true
}

# GUI assets - contents-into-ensured-destination, same trap-avoidance as deploy.ps1.
$guiSource = Join-Path $repoRoot "module\GUI"
if (Test-Path $guiSource) {
    $guiDest = Join-Path $moduleDir "GUI"
    New-Item -ItemType Directory -Force $guiDest | Out-Null
    Copy-Item (Join-Path $guiSource "*") $guiDest -Recurse -Force
}

# The version stamp comes from the manifest, so the zip name always tells the truth.
$version = "unversioned"
try {
    [xml]$manifest = Get-Content (Join-Path $repoRoot "module\SubModule.xml")
    $v = $manifest.Module.Version.value
    if ($v) { $version = $v -replace '[^\w\.\-]', '' }
} catch { }

$zipPath = Join-Path $distRoot "ImmersiveAI_$version.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path $moduleDir -DestinationPath $zipPath

Write-Host "Packaged $version to $moduleDir"
# The release dance wants this visible: a package is either voiced or it is not.
if ($voiceHostShipped) {
    Write-Host "Voice host: included (framework-dependent - players need the .NET 8 runtime for voices)."
} else {
    Write-Host "Voice host: NOT included (no project in this tree) - this build ships without voices."
}
Write-Host "Zip: $zipPath"
Write-Host "Workshop upload: point the uploader at the dist\ImmersiveAI folder."

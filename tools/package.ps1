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

# --- No voice host, and no executable of any kind (2026.09.24) -----------------------------------
# The mod carried its own speech engine as an exe from v3.0.0 to v3.3.x, and Nexus quarantined
# every one of those archives on the file type alone. The voices now come from claude-voice, a
# separate app the Voices page downloads and installs on demand - so this package holds no program
# at all, and the check at the end REFUSES to package one that does.

# GUI assets - contents-into-ensured-destination, same trap-avoidance as deploy.ps1.
$guiSource = Join-Path $repoRoot "module\GUI"
if (Test-Path $guiSource) {
    $guiDest = Join-Path $moduleDir "GUI"
    New-Item -ItemType Directory -Force $guiDest | Out-Null
    Copy-Item (Join-Path $guiSource "*") $guiDest -Recurse -Force
}

# The voices that ship with the mod - see deploy.ps1. ONE RULE, and it is a legal one rather than a
# technical one: a voice folder carries the clip it was cloned from, so shipping one hands every
# player a copy of that. Public-domain / CC0 source audio only, never a real person's voice without
# their blessing. module\Voices\README.txt says the same where it will actually be read.
$voicesSource = Join-Path $repoRoot "module\Voices"

# Voices that must NEVER leave this machine, by folder name. EMPTY today, deliberately: the three
# names that lived here (sibylla, achilles, max) were early clones of real people, since RECREATED
# whole from CC0 source audio (kyutai/tts-voices) - Anton confirmed the rework and gave the
# all-clear on 2026.08.16, and module\Voices\README.txt states the practice. The list STAYS as the
# mechanism: any future voice cloned from someone who did not consent goes here (or wears the
# Source mark below), and it stops the release dead rather than warning - the embedding IS the
# voice, so packaging one distributes it. deploy.ps1 deliberately does NOT check: the local
# install is exactly where an unshippable development clone belongs.
$neverShip = @()

# A durable mark inside voice.json, for the voices a NAME list cannot keep up with. The 91 cloned
# from Bannerlord's own dialogue VO wear it: no game audio is redistributed (the folders hold only
# embeddings), but they are still clones of named actors who never agreed to it - the same rule the
# Alba/Pitt voices fall under, and the one module\Voices\README.txt states. It survives renaming
# both the folder and the display name, which a name list does not.
$neverShipMark = "NOT FOR RELEASE"

if (Test-Path $voicesSource) {
    # Folder name AND the name/id written inside, so renaming the folder does not slip one past.
    $blocked = @(Get-ChildItem $voicesSource -Recurse -Directory -ErrorAction SilentlyContinue | Where-Object {
        $metaPath = Join-Path $_.FullName "voice.json"
        if (-not (Test-Path $metaPath)) { return $false }

        $words = @($_.Name)
        $marked = $false
        try {
            $meta = Get-Content $metaPath -Raw | ConvertFrom-Json
            if ($meta.Name) { $words += $meta.Name }
            if ($meta.Id)   { $words += $meta.Id }
            if ($meta.Source -and $meta.Source -like "*$neverShipMark*") { $marked = $true }
        } catch { }   # an unreadable voice.json is judged on its folder name alone

        if ($marked) { return $true }

        $hit = $false
        foreach ($w in $words) {
            $plain = ($w -replace '[^a-zA-Z]', '').ToLowerInvariant()
            foreach ($banned in $neverShip) { if ($plain -like "*$banned*") { $hit = $true } }
        }
        return $hit
    })
    if ($blocked.Count -gt 0) {
        $names = ($blocked | ForEach-Object { $_.Name }) -join ", "
        throw "Refusing to package: module\Voices holds voice(s) marked never-ship - $names. Move them out of the repo (they are fine to deploy locally), or take the name off `$neverShip in this script if it is genuinely a different voice."
    }

    $voicesDest = Join-Path $moduleDir "Voices"
    New-Item -ItemType Directory -Force $voicesDest | Out-Null
    Copy-Item (Join-Path $voicesSource "*") $voicesDest -Recurse -Force
    $shippedVoices = @(Get-ChildItem $voicesSource -Recurse -Filter "voice.json" -ErrorAction SilentlyContinue).Count
} else {
    $shippedVoices = 0
}

# The version stamp comes from the manifest, so the zip name always tells the truth.
$version = "unversioned"
try {
    [xml]$manifest = Get-Content (Join-Path $repoRoot "module\SubModule.xml")
    $v = $manifest.Module.Version.value
    if ($v) { $version = $v -replace '[^\w\.\-]', '' }
} catch { }

# Written entry by entry rather than with Compress-Archive, for one reason: PowerShell 5.1 writes
# BACKSLASH separators into the archive, and the ZIP spec (APPNOTE 4.4.17.1) says a name is always
# forward-slashed. Most extractors forgive it. A scanner that cannot walk the tree is entitled not
# to, and Nexus blocks any upload whose contents it failed to preview - "the tool used to create
# your archive has likely done so in an uncommon format" is one of their named quarantine causes.
# Every release up to v2.2.0 got away with it, so this is not the thing that bit us; it is the
# cheap half of making sure nothing else can.
Add-Type -AssemblyName System.IO.Compression.FileSystem
# $ModuleFolder is the ImmersiveAI folder itself; only what is INSIDE it is walked, while entry
# names are cut relative to its PARENT so the folder itself stays in the archive and the zip drops
# straight into Modules\. Walking the parent instead would sweep in whatever else shares that
# directory - including, memorably, the zip being written.
function Write-ModuleZip {
    param([string]$ModuleFolder, [string]$ZipPath)

    if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
    $prefix = (Split-Path -Parent $ModuleFolder).TrimEnd('\') + '\'
    $files = @(Get-ChildItem $ModuleFolder -Recurse -File)
    $zip = [System.IO.Compression.ZipFile]::Open($ZipPath, "Create")
    try {
        foreach ($file in $files) {
            $entryName = $file.FullName.Substring($prefix.Length).Replace('\', '/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $entryName, "Optimal") | Out-Null
        }
    } finally {
        $zip.Dispose()
    }
}

# The executable inventory, and now a hard stop. Nexus blocks any archive with a program in it,
# whatever the program is; this mod ships none since the voices moved to claude-voice, so one here
# is a mistake that would cost every Nexus player the download.
$strayExes = @(Get-ChildItem $moduleDir -Recurse -File -Include *.exe, *.com, *.scr, *.bat, *.cmd -ErrorAction SilentlyContinue)
if ($strayExes.Count -gt 0) {
    $names = ($strayExes | ForEach-Object { $_.FullName.Substring($moduleDir.Length) }) -join ", "
    throw "Refusing to package: the module holds executable(s) - $names. Nexus quarantines any archive with a program in it."
}

$zipPath = Join-Path $distRoot "ImmersiveAI_$version.zip"
Write-ModuleZip -ModuleFolder $moduleDir -ZipPath $zipPath

# ONE zip for both stores, and no optional extra: the voice app is fetched from the game itself.

Write-Host "Packaged $version to $moduleDir"
Write-Host "Voices shipped with the mod: $shippedVoices (spoken by claude-voice, installed from the Voices page)"
Write-Host "The one zip: $zipPath   (no executables inside - clean for Nexus)"
Write-Host "Workshop upload: point the uploader at the dist\ImmersiveAI folder."

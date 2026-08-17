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
# The separate net8.0 TTS process, published FRAMEWORK-DEPENDENT into a VoiceHost FOLDER at the
# module root. The full reasoning lives in deploy.ps1; the short of it is that 33 MB of bundled
# runtime in every player's download buys nothing for a feature already gated behind a
# hand-installed, multi-gigabyte local engine, and that the folder sits outside bin because the
# game must never see net8.0 assemblies among the ones it loads.
#
# IT DOES NOT GO IN THE MAIN DOWNLOAD, and that is the whole point of this comment (2026.08.17).
# It is staged HERE, outside the module folder, and zipped SEPARATELY at the bottom of this script.
#
# Learned the expensive way, twice in one day. Nexus auto-quarantined v3.0.0 and v3.1.0, which
# shipped the host as a .NET single-file bundle - their rules name "any kind of self-extracting
# file", and a single-file publish is exactly that. So it was republished as an ordinary exe with
# its DLLs beside it... and v3.1.1 was quarantined too. Three theories died getting here: the file
# is clean on VirusTotal (0/70), their own previewer reads our archive perfectly, and plain
# publishing changed nothing. What is left is that an archive containing ANY executable is blocked.
# Every release without one (v1.x, v2.x) passed; both releases with one were blocked.
#
# So the main zip carries no exe at all and always installs. Voices become a small separate
# download that unzips over the same Modules folder - VoiceEngineDiscovery already probes
# <module>\VoiceHost, so nothing in the game needs to know which of the two a player has.
# If that optional file is ever quarantined in its turn, it costs the voices and nothing else.
#
# Differences from deploy.ps1, both deliberate: no pdb ships, and a voice host that EXISTS but
# fails to build stops the packaging dead. A release that quietly lost its voices is a defect, and
# a clean-slate packager is exactly where that must be caught.
$voiceHostProj = Join-Path $repoRoot "src\ImmersiveAI.VoiceHost\ImmersiveAI.VoiceHost.csproj"
$voiceHostShipped = $false
# Staged as <staging>\ImmersiveAI\VoiceHost\... so the optional zip unpacks into Modules\ and merges
# into the module folder the main download already made.
$hostStaging = Join-Path $distRoot "VoiceHostPackage"
if (Test-Path $hostStaging) { Remove-Item $hostStaging -Recurse -Force }

if (Test-Path $voiceHostProj) {
    # Clean slate here too - a stale file from an older build must never ride along.
    $voiceOut = Join-Path $repoRoot "src\ImmersiveAI.VoiceHost\bin\publish\package-$Configuration"
    if (Test-Path $voiceOut) { Remove-Item $voiceOut -Recurse -Force }

    dotnet publish $voiceHostProj -c $Configuration -r win-x64 --self-contained false -p:DebugType=none -o $voiceOut
    if ($LASTEXITCODE -ne 0) { throw "The voice host failed to build - refusing to package a release without it." }

    # The game spawns it BY NAME; a renamed exe would ship as a silent no-voices bug.
    $hostExe = Join-Path $voiceOut "ImmersiveAI.VoiceHost.exe"
    if (-not (Test-Path $hostExe)) { throw "Published the voice host but no ImmersiveAI.VoiceHost.exe came out - the game spawns it by that exact name." }

    $hostDir = Join-Path $hostStaging "ImmersiveAI\VoiceHost"
    New-Item -ItemType Directory -Force $hostDir | Out-Null
    Copy-Item (Join-Path $voiceOut "*") $hostDir -Recurse -Force
    # Anything the host bundles that obliges a notice travels with it, same habit as Harmony.
    Copy-Item (Join-Path $repoRoot "src\ImmersiveAI.VoiceHost\THIRD-PARTY-NOTICES.txt") $hostDir -Force -ErrorAction SilentlyContinue
    $voiceHostShipped = $true
}

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

# THE GUARD THIS WHOLE SPLIT EXISTS FOR. An executable anywhere in the main module folder gets the
# download auto-quarantined on Nexus, which blocks the mod itself rather than merely the voices -
# so it stops the release here instead of being discovered on the mod page an hour later.
$strayExes = @(Get-ChildItem $moduleDir -Recurse -File -Include *.exe, *.com, *.scr, *.bat, *.cmd -ErrorAction SilentlyContinue)
if ($strayExes.Count -gt 0) {
    $names = ($strayExes | ForEach-Object { $_.FullName.Substring($moduleDir.Length).TrimStart('\') }) -join ", "
    throw "Refusing to package: the main module folder holds executable file(s) - $names. Nexus quarantines any archive containing one, which blocks the whole mod. Ship it as the separate VoiceHost download instead."
}

$zipPath = Join-Path $distRoot "ImmersiveAI_$version.zip"
Write-ModuleZip -ModuleFolder $moduleDir -ZipPath $zipPath

# The optional voice-host download. Same shape as the main zip (ImmersiveAI\... inside), so it
# unpacks into Modules\ and merges into the folder the main download already made.
$hostZipPath = ""
if ($voiceHostShipped) {
    $hostZipPath = Join-Path $distRoot "ImmersiveAI_VoiceHost_$version.zip"
    Write-ModuleZip -ModuleFolder (Join-Path $hostStaging "ImmersiveAI") -ZipPath $hostZipPath
}

# --- The Steam payload, assembled AFTER the zips are cut -------------------------------------
# The two stores want different shapes and this is the one place that difference lives.
# NEXUS scans uploads and quarantines any archive holding an executable, so its main download must
# not have one - hence the split zips above. STEAM has no such scan, and a Workshop item is a single
# folder with no notion of an optional extra file: split there, and a Workshop player simply loses
# voices with nothing to go and fetch. So the folder the uploader points at gets the host put back.
# ORDER IS LOAD-BEARING: this runs after Write-ModuleZip, so the Nexus zip is already sealed without
# it, and after the stray-executable guard, which must judge what NEXUS receives.
if ($voiceHostShipped) {
    Copy-Item (Join-Path $hostStaging "ImmersiveAI\VoiceHost") $moduleDir -Recurse -Force
}

Write-Host "Packaged $version to $moduleDir"
# The release dance wants this visible: a package is either voiced or it is not - and since
# 2026.08.17 that is TWO uploads, so say plainly that the second one exists and must go up too.
if ($voiceHostShipped) {
    Write-Host "Voice host: SEPARATE optional download (framework-dependent - players need the .NET 8 runtime for voices)."
} else {
    Write-Host "Voice host: NOT built (no project in this tree) - this build ships without voices."
}
Write-Host "Voices shipped with the mod: $shippedVoices"
Write-Host "Main zip:  $zipPath   (no executable - this is the one that must never be blocked)"
if ($hostZipPath) {
    Write-Host "Voice zip: $hostZipPath   (upload as an OPTIONAL file, not Main)"
}
Write-Host "Workshop upload: point the uploader at the dist\ImmersiveAI folder."
if ($voiceHostShipped) {
    Write-Host "  That folder HAS the voice host inside it, on purpose - Steam does not scan uploads and"
    Write-Host "  has no optional-file slot, so the Workshop copy ships whole. Only the Nexus zip is split."
}

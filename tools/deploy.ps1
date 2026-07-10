# Builds the mod and installs it into the Bannerlord Modules folder.
# Usage: powershell -ExecutionPolicy Bypass -File tools\deploy.ps1 [-Configuration Release]
param(
    [string]$Configuration = "Release",
    [string]$GameFolder = "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$moduleDir = Join-Path $GameFolder "Modules\ImmersiveAI"
$binDir = Join-Path $moduleDir "bin\Win64_Shipping_Client"

dotnet build (Join-Path $repoRoot "src\ImmersiveAI.Module\ImmersiveAI.Module.csproj") -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

New-Item -ItemType Directory -Force $binDir | Out-Null
Copy-Item (Join-Path $repoRoot "module\SubModule.xml") $moduleDir -Force

$outDir = Join-Path $repoRoot "src\ImmersiveAI.Module\bin\$Configuration"
Copy-Item (Join-Path $outDir "ImmersiveAI.dll") $binDir -Force
Copy-Item (Join-Path $outDir "ImmersiveAI.Core.dll") $binDir -Force
Copy-Item (Join-Path $outDir "Newtonsoft.Json.dll") $binDir -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $outDir "0Harmony.dll") $binDir -Force -ErrorAction SilentlyContinue

# GUI assets (prefab overrides such as the portrait map notice, the chat window) ride along with
# the module. Copy the folder's CONTENTS into an ensured destination: Copy-Item with a folder
# source and an existing folder destination would nest a GUI\GUI inside it instead of updating.
$guiSource = Join-Path $repoRoot "module\GUI"
if (Test-Path $guiSource) {
    $guiDest = Join-Path $moduleDir "GUI"
    New-Item -ItemType Directory -Force $guiDest | Out-Null
    Copy-Item (Join-Path $guiSource "*") $guiDest -Recurse -Force
}

Write-Host "Deployed to $moduleDir"

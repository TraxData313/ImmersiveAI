# Builds the mod and installs it into the Bannerlord Modules folder.
# Usage: powershell -ExecutionPolicy Bypass -File tools\deploy.ps1 [-Configuration Release]
param(
    [string]$Configuration = "Release",
    [string]$GameFolder = "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$moduleDir = Join-Path $GameFolder "Modules\LivingCalradia"
$binDir = Join-Path $moduleDir "bin\Win64_Shipping_Client"

dotnet build (Join-Path $repoRoot "src\LivingCalradia.Module\LivingCalradia.Module.csproj") -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

New-Item -ItemType Directory -Force $binDir | Out-Null
Copy-Item (Join-Path $repoRoot "module\SubModule.xml") $moduleDir -Force

$outDir = Join-Path $repoRoot "src\LivingCalradia.Module\bin\$Configuration"
Copy-Item (Join-Path $outDir "LivingCalradia.dll") $binDir -Force
Copy-Item (Join-Path $outDir "LivingCalradia.Core.dll") $binDir -Force
Copy-Item (Join-Path $outDir "Newtonsoft.Json.dll") $binDir -Force -ErrorAction SilentlyContinue

Write-Host "Deployed to $moduleDir"

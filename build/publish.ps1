<#
.SYNOPSIS
    Builds MacroController end-to-end: publish -> obfuscate -> create Setup.exe installer.

.OUTPUTS
    dist\MacroControllerSetup.exe
#>

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$buildDir = Join-Path $root "build"
$publishDir = Join-Path $buildDir "publish"
$patcherPublishDir = Join-Path $buildDir "publish-patcher"
$obfDir = Join-Path $buildDir "obfuscated"
$distDir = Join-Path $root "dist"
$appProject = Join-Path $root "src\MacroController.App\MacroController.App.csproj"
$patcherProject = Join-Path $root "src\MacroController.Patcher\MacroController.Patcher.csproj"
$obfuscar = Join-Path $env:USERPROFILE ".dotnet\tools\obfuscar.console.exe"
$iscc = "C:\Users\xdark\AppData\Local\Programs\Inno Setup 6\ISCC.exe"

# Single source of truth for the app version - keep in sync with Update/Updater.cs.
$updaterSource = Get-Content (Join-Path $root "src\MacroController.App\Update\Updater.cs") -Raw
if ($updaterSource -notmatch 'VersionString = "([\d.]+)"') { throw "Could not find VersionString in Updater.cs" }
$appVersion = $Matches[1]
Write-Host "==> Building version $appVersion" -ForegroundColor Cyan

Write-Host "==> Cleaning previous output" -ForegroundColor Cyan
Remove-Item -Recurse -Force $publishDir, $patcherPublishDir, $obfDir, $distDir -ErrorAction SilentlyContinue

Write-Host "==> Publishing self-contained win-x64 build" -ForegroundColor Cyan
dotnet publish $appProject -c Release -r win-x64 --self-contained true `
    -p:SatelliteResourceLanguages=en `
    -p:Version=$appVersion `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "==> Publishing patcher" -ForegroundColor Cyan
dotnet publish $patcherProject -c Release -r win-x64 --self-contained true `
    -p:SatelliteResourceLanguages=en `
    -p:Version=$appVersion `
    -o $patcherPublishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# Only copy the patcher's own files - it shares the .NET runtime files already
# published alongside the app (same TFM/RID), so skip those to avoid duplicating ~150MB.
Get-ChildItem $patcherPublishDir -Filter "MacroController.Patcher.*" | Copy-Item -Destination $publishDir -Force

Write-Host "==> Obfuscating assemblies" -ForegroundColor Cyan
Push-Location $buildDir
try {
    & $obfuscar "obfuscar.xml"
    if ($LASTEXITCODE -ne 0) { throw "Obfuscar failed" }
}
finally {
    Pop-Location
}

Copy-Item (Join-Path $obfDir "MacroController.App.dll") $publishDir -Force
Copy-Item (Join-Path $obfDir "MacroController.Core.dll") $publishDir -Force

Write-Host "==> Building installer" -ForegroundColor Cyan
& $iscc "/DAppVersion=$appVersion" (Join-Path $buildDir "installer.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed" }

Write-Host "==> Done: $distDir\MacroControllerSetup.exe" -ForegroundColor Green

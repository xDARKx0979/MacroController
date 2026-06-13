<#
.SYNOPSIS
    Generates an encrypted manifest.dat for the GitHub-based auto-updater.

.DESCRIPTION
    Run this after publishing a new release of MacroController:
      1. Run build\publish.ps1 to produce dist\MacroControllerSetup.exe and bump
         Update/Updater.cs's Version/VersionString for the NEW build.
      2. Zip up build\publish\* (the updated app files) - this is what gets
         downloaded and extracted by the patcher on older clients.
      3. Create a GitHub Release in the private xDARKx0979/MacroController repo
         and upload that zip as a release asset.
      4. Get the asset's API download URL: GET
         https://api.github.com/repos/xDARKx0979/MacroController/releases
         (with your PAT) and copy the matching asset's "url" field - it looks like
         https://api.github.com/repos/xDARKx0979/MacroController/releases/assets/<id>
         (the updater downloads via this URL with Accept: application/octet-stream,
         which works for private repos).
      5. Run this script with the OLD manifest Version (i.e. the version number
         that should make older clients update - typically the new build's
         Updater.Version), the asset URL from step 4, and the zip from step 2.
      6. Commit/push the resulting manifest.dat to the repo root.

.EXAMPLE
    .\make-manifest.ps1 -Version 2 -DownloadUrl "https://api.github.com/repos/xDARKx0979/MacroController/releases/assets/123456" -ZipPath .\MacroController-1.1.0.zip
#>
param(
    [Parameter(Mandatory)] [int]$Version,
    [Parameter(Mandatory)] [string]$DownloadUrl,
    [Parameter(Mandatory)] [string]$ZipPath,
    [string]$OutFile = (Join-Path $PSScriptRoot "manifest.dat")
)

$ErrorActionPreference = "Stop"

$sha256 = (Get-FileHash -Path $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()

$manifest = [ordered]@{
    Version     = $Version
    DownloadUrl = $DownloadUrl
    Sha256      = $sha256
} | ConvertTo-Json -Compress

# Must match src/MacroController.App/Update/Crypto.cs
$keyHex = "62cd71acaedb737188a7b2e221972a77e43271238bfdfe4d7db9f01283bd5012"
$ivHex = "28681a9f78d925cbdfff513ada5127a9"

function HexToBytes([string]$hex) {
    $bytes = New-Object byte[] ($hex.Length / 2)
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        $bytes[$i] = [Convert]::ToByte($hex.Substring($i * 2, 2), 16)
    }
    return $bytes
}

$aes = [System.Security.Cryptography.Aes]::Create()
$aes.Key = HexToBytes $keyHex
$aes.IV = HexToBytes $ivHex

$encryptor = $aes.CreateEncryptor()
$plainBytes = [System.Text.Encoding]::UTF8.GetBytes($manifest)
$encrypted = $encryptor.TransformFinalBlock($plainBytes, 0, $plainBytes.Length)

[System.IO.File]::WriteAllBytes($OutFile, $encrypted)
Write-Host "Wrote $OutFile ($($encrypted.Length) bytes) for version $Version" -ForegroundColor Green
Write-Host "Manifest JSON: $manifest"

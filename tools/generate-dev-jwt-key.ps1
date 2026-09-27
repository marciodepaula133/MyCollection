<#
.SYNOPSIS
Generates a local RS256 PEM keypair for dev (AD-6, AD-17). Never committed - see .gitignore.
Requires OpenSSL on PATH (ships with Git for Windows as git-bash's openssl.exe).
#>

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repoRoot "keys"
$privateKey = Join-Path $outDir "jwt-signing-key.pem"
$publicKey = Join-Path $outDir "jwt-signing-key.pub.pem"

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

if (Test-Path $privateKey) {
    Write-Host "Key already exists at $privateKey - remove it first if you want a new one."
    exit 0
}

$openssl = (Get-Command openssl -ErrorAction SilentlyContinue)
if (-not $openssl) {
    throw "openssl not found on PATH. Install Git for Windows (includes openssl) or OpenSSL directly."
}

& openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out $privateKey
& openssl rsa -in $privateKey -pubout -out $publicKey

Write-Host "Generated dev JWT signing key:"
Write-Host "  Private: $privateKey  (set Jwt__SigningKeyPath to this path)"
Write-Host "  Public:  $publicKey"
Write-Host "These are gitignored (*.pem) - never commit them."

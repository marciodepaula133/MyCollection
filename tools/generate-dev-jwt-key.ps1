<#
.SYNOPSIS
Generates a local RS256 PEM keypair for dev (AD-6, AD-17). Never committed - see .gitignore.
Needs only the .NET SDK: the key logic lives in generate-dev-jwt-key.cs (a file-based app).
#>

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repoRoot "keys"
$generator = Join-Path $PSScriptRoot "generate-dev-jwt-key.cs"

& dotnet run --file $generator -- $outDir
if ($LASTEXITCODE -ne 0) {
    throw "generate-dev-jwt-key.cs failed with exit code $LASTEXITCODE."
}

#!/usr/bin/env bash
# Generates a local RS256 PEM keypair for dev (AD-6, AD-17). Never committed - see .gitignore.
# Needs only the .NET SDK: the key logic lives in generate-dev-jwt-key.cs (a file-based app).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)/keys"

dotnet run --file "$SCRIPT_DIR/generate-dev-jwt-key.cs" -- "$OUT_DIR"

chmod 600 "$OUT_DIR/jwt-signing-key.pem"

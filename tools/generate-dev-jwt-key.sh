#!/usr/bin/env bash
# Generates a local RS256 PEM keypair for dev (AD-6, AD-17). Never committed - see .gitignore.
set -euo pipefail

OUT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/keys"
PRIVATE_KEY="$OUT_DIR/jwt-signing-key.pem"
PUBLIC_KEY="$OUT_DIR/jwt-signing-key.pub.pem"

mkdir -p "$OUT_DIR"

if [ -f "$PRIVATE_KEY" ]; then
  echo "Key already exists at $PRIVATE_KEY - remove it first if you want a new one."
  exit 0
fi

openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out "$PRIVATE_KEY"
openssl rsa -in "$PRIVATE_KEY" -pubout -out "$PUBLIC_KEY"

chmod 600 "$PRIVATE_KEY"

echo "Generated dev JWT signing key:"
echo "  Private: $PRIVATE_KEY  (set Jwt__SigningKeyPath to this path)"
echo "  Public:  $PUBLIC_KEY"
echo "These are gitignored (*.pem) - never commit them."

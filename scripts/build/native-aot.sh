#!/usr/bin/env bash
set -euo pipefail

configuration="${CONFIGURATION:-${1:-Release}}"
echo "Native AOT publishing is intentionally explicit. Run dotnet publish with the desired RID for release work. configuration=$configuration"

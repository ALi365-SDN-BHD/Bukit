#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/release/verify-release-assets.sh <release-version> <release-commit> <asset-dir>

Re-generate checksums/manifest for existing release assets and run strict validation.

Arguments:
  <release-version> 发布版本（不含 v 前缀），如 1.0.2
  <release-commit>  发布 commit SHA
  <asset-dir>       存放 release 产物目录（含平台产物/skills zip）
USAGE
}

if [ "$#" -ne 3 ]; then
  usage
  exit 1
fi

VERSION="$1"
COMMIT_SHA="$2"
ASSET_DIR="$3"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ ! -d "$ASSET_DIR" ]; then
  echo "ERROR: asset directory not found: $ASSET_DIR" >&2
  exit 1
fi

ASSETS=(
  "$ASSET_DIR/bukit-${VERSION}-linux-x64.tar.gz"
  "$ASSET_DIR/bukit-${VERSION}-osx-arm64.tar.gz"
  "$ASSET_DIR/bukit-${VERSION}-win-x64.zip"
  "$ASSET_DIR/bukit-skills.zip"
)

for asset in "${ASSETS[@]}"; do
  if [ ! -f "$asset" ]; then
    echo "ERROR: missing release asset: $asset" >&2
    exit 1
  fi
done

(
  cd "$ASSET_DIR"
  bash "$SCRIPT_DIR/prepare-release-assets.sh" "$VERSION" "$COMMIT_SHA" \
    "bukit-${VERSION}-linux-x64.tar.gz" \
    "bukit-${VERSION}-osx-arm64.tar.gz" \
    "bukit-${VERSION}-win-x64.zip" \
    "bukit-skills.zip"
)

if bash scripts/checks/release-assets.sh "$ASSET_DIR" "$VERSION" "$COMMIT_SHA" | tee "$ASSET_DIR/release-assets-check.md"; then
  echo "release asset checks passed"
  exit 0
else
  status=$?
  echo "release asset checks failed"
  exit $status
fi

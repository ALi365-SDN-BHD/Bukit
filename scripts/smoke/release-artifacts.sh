#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

[[ $# -eq 2 ]] || {
  echo "usage: bash scripts/smoke/release-artifacts.sh <archive-or-publish-dir> <rid>" >&2
  exit 2
}

artifact="$1"
rid="$2"
case "$rid" in
  linux-x64|osx-arm64) exe="bukit" ;;
  win-x64) exe="bukit.exe" ;;
  *)
    echo "unsupported RID: $rid" >&2
    exit 1
    ;;
esac

scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-release-artifacts.XXXXXX")"
scratch="$(cd "$scratch" && pwd -P)"
cleanup() {
  chmod -R u+rwX "$scratch" 2>/dev/null || true
  rm -rf "$scratch"
}
trap cleanup EXIT

if [[ -d "$artifact" ]]; then
  publish_root="$artifact"
elif [[ -f "$artifact" ]]; then
  publish_root="$scratch/publish"
  python3 scripts/smoke/extract-release-artifact.py "$artifact" "$rid" "$publish_root"
else
  echo "missing release artifact: $artifact" >&2
  exit 1
fi

matches=()
while IFS= read -r path; do
  matches+=("$path")
done < <(find "$publish_root" -type f -name "$exe" -print)

[[ ${#matches[@]} -eq 1 ]] || {
  echo "expected exactly one $exe, found ${#matches[@]}" >&2
  exit 1
}

if [[ "$rid" != "win-x64" && ! -x "${matches[0]}" ]]; then
  echo "release CLI is not executable: ${matches[0]}" >&2
  exit 1
fi

cp -R tests/fixtures/basic-markdown-site "$scratch/site"
BUKIT_BIN="${matches[0]}" \
  BUKIT_SMOKE_ROOT="$scratch/site" \
  BUKIT_SMOKE_OUTPUT="dist" \
  bash scripts/smoke/core.sh

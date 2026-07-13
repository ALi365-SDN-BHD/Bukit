#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../.."

[[ $# -ge 2 && $# -le 3 ]] || {
  echo "usage: bash scripts/build/build-repro.sh <version> <rid> [configuration]" >&2
  exit 2
}

version="$1"
rid="$2"
configuration="${3:-Release}"

scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-build-repro.XXXXXX")"
trap 'rm -rf -- "$scratch"' EXIT

export GITHUB_SHA="${GITHUB_SHA:-$(git rev-parse HEAD)}"
export SOURCE_DATE_EPOCH="${SOURCE_DATE_EPOCH:-$(git show -s --format=%ct HEAD)}"

for run in first second; do
  bash scripts/build/package-native-aot.sh "$version" "$rid" \
    "$scratch/$run" "$configuration" > "$scratch/$run.archive"
done

python3 scripts/build/compare-publish-trees.py \
  "$scratch/first/publish/$rid" "$scratch/second/publish/$rid"

printf 'Native AOT publish trees are reproducible: version=%s rid=%s configuration=%s\n' \
  "$version" "$rid" "$configuration"

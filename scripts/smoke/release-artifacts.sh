#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 1 ]; then
  echo "usage: release-artifacts.sh <publish-dir>" >&2
  exit 2
fi

publish_dir="$1"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"
source scripts/lib/common.sh

binary="$(bukit_find_binary "$publish_dir")" || {
  echo "ERROR: no bukit binary found in $publish_dir" >&2
  exit 1
}

if [ ! -x "$binary" ] && [ "${binary##*.}" != "exe" ]; then
  chmod +x "$binary"
fi

fixture="tests/fixtures/basic-markdown-site"
run_id="$(date +%Y%m%d%H%M%S)-$$"
smoke_root=".smoke-all-run/release-artifacts-$run_id"
cleanup() {
  rm -rf "$fixture/$smoke_root"
}
trap cleanup EXIT

"$binary" version >/dev/null
"$binary" --help | grep -q '^  build'

schema_path="$fixture/$smoke_root/site.schema.json"
mkdir -p "$(dirname "$schema_path")"
"$binary" config schema --output "$schema_path"
test -s "$schema_path"
python3 -m json.tool "$schema_path" >/dev/null

"$binary" config check --config "$fixture/site.yaml" --site-url https://example.com
"$binary" doctor --config "$fixture/site.yaml" --site-url https://example.com

output="$smoke_root/dist"
cache="$smoke_root/cache"
"$binary" build \
  --config "$fixture/site.yaml" \
  --output "$output" \
  --cache-dir "$cache" \
  --clean \
  --site-url https://example.com \
  --ci

full_output="$fixture/$output"
test -f "$full_output/index.html"
test -f "$full_output/sitemap.xml"
"$binary" seo audit --dir "$full_output"
"$binary" geo audit --dir "$full_output"
"$binary" publish audit --dir "$full_output"
bash scripts/validate-artifacts-json.sh "$full_output"

echo "Release artifact smoke OK: $binary"

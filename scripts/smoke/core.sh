#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"
source scripts/lib/common.sh

run_id="$(date +%Y%m%d%H%M%S)-$$"
smoke_root=".smoke-all-run/$run_id"
fixture="tests/fixtures/basic-markdown-site"
i18n_fixture="tests/fixtures/i18n-site"

cleanup() {
  rm -rf "$fixture/$smoke_root" "$i18n_fixture/$smoke_root"
}
trap cleanup EXIT

if [ "${SMOKE_SKIP_BUILD:-0}" != "1" ]; then
  dotnet build bukit.slnx -c "$configuration" -maxcpucount:1 -nodeReuse:false
fi

mkdir -p "$fixture/$smoke_root"
bukit_cli "$configuration" version >/dev/null
bukit_cli "$configuration" --help > "$fixture/$smoke_root/help.txt"
grep -q '^  build' "$fixture/$smoke_root/help.txt"
grep -q '^  config' "$fixture/$smoke_root/help.txt"
grep -q '^  publish' "$fixture/$smoke_root/help.txt"

schema_path="$fixture/$smoke_root/site.schema.json"
mkdir -p "$(dirname "$schema_path")"
bukit_cli "$configuration" config schema --output "$schema_path"
test -s "$schema_path"
python3 -m json.tool "$schema_path" >/dev/null

bukit_cli "$configuration" config check --config "$fixture/site.yaml" --site-url https://example.com
bukit_cli "$configuration" doctor --config "$fixture/site.yaml" --site-url https://example.com

basic_output="$smoke_root/basic/dist"
basic_cache="$smoke_root/basic/cache"
bukit_cli "$configuration" build \
  --config "$fixture/site.yaml" \
  --output "$basic_output" \
  --cache-dir "$basic_cache" \
  --clean \
  --site-url https://example.com \
  --ci

basic_full_output="$fixture/$basic_output"
test -f "$basic_full_output/index.html"
test -f "$basic_full_output/sitemap.xml"
test -f "$basic_full_output/search.json"
test -f "$basic_full_output/llms.txt"
python3 -m json.tool "$basic_full_output/search.json" >/dev/null
bash scripts/validate-artifacts-json.sh "$basic_full_output"

bukit_cli "$configuration" seo audit --dir "$basic_full_output"
bukit_cli "$configuration" geo audit --dir "$basic_full_output"
bukit_cli "$configuration" publish audit --dir "$basic_full_output"
bukit_cli "$configuration" completion bash | grep -q "bukit"

i18n_output="$smoke_root/i18n/dist"
i18n_cache="$smoke_root/i18n/cache"
bukit_cli "$configuration" config check --config "$i18n_fixture/site.yaml" --site-url https://example.com
bukit_cli "$configuration" build \
  --config "$i18n_fixture/site.yaml" \
  --output "$i18n_output" \
  --cache-dir "$i18n_cache" \
  --clean \
  --site-url https://example.com \
  --ci

i18n_full_output="$i18n_fixture/$i18n_output"
test -f "$i18n_full_output/sitemap.xml"
test -f "$i18n_full_output/en/index.html"
test -f "$i18n_full_output/zh-CN/index.html"
bash scripts/validate-artifacts-json.sh "$i18n_full_output"

bukit_cli "$configuration" clean --dir "$fixture/$smoke_root/clean-target"

echo "Smoke core OK"

#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"
source scripts/lib/common.sh

run_id="$(date +%Y%m%d%H%M%S)-$$"
smoke_root=".smoke-all-run/$run_id"
passed=0
failed=0
total=0

fixtures=(
  "basic-markdown:tests/fixtures/basic-markdown-site/site.yaml"
  "safe-url-content:tests/fixtures/safe-url-content-site/site.yaml"
  "plugin-policy-rejection:tests/fixtures/plugin-policy-site/site.yaml"
  "output-safety:tests/fixtures/output-safety-site/site.yaml"
  "incremental:tests/fixtures/incremental-site/site.yaml"
  "i18n:tests/fixtures/i18n-site/site.yaml"
  "taxonomy:tests/fixtures/taxonomy-site/site.yaml"
  "component-validation:tests/fixtures/component-validation-site/site.yaml"
  "dotfile-leak:tests/fixtures/dotfile-leak-site/site.yaml"
)

cleanup() {
  find tests/fixtures -type d -path "*/$smoke_root" -prune -exec rm -rf {} + 2>/dev/null || true
}
trap cleanup EXIT

check_output() {
  local output="$1"
  local ok=1

  if [ -f "$output/index.html" ] || [ -f "$output/en/index.html" ]; then
    true
  else
    ok=0
    echo -n " [index missing]"
  fi

  if [ -f "$output/sitemap.xml" ]; then
    grep -q '<url>' "$output/sitemap.xml" || { ok=0; echo -n " [sitemap invalid]"; }
  fi

  if [ -f "$output/rss.xml" ]; then
    grep -q '<channel>' "$output/rss.xml" || { ok=0; echo -n " [rss invalid]"; }
  fi

  if [ -f "$output/search.json" ]; then
    python3 -m json.tool "$output/search.json" >/dev/null 2>&1 || { ok=0; echo -n " [search invalid]"; }
  fi

  test ! -f "$output/.env" || { ok=0; echo -n " [.env leaked]"; }
  test ! -f "$output/.npmrc" || { ok=0; echo -n " [.npmrc leaked]"; }
  test ! -f "$output/.yarnrc" || { ok=0; echo -n " [.yarnrc leaked]"; }
  test ! -f "$output/private.key" || { ok=0; echo -n " [private.key leaked]"; }
  test ! -f "$output/cert.pfx" || { ok=0; echo -n " [cert.pfx leaked]"; }
  test ! -f "$output/cert.p12" || { ok=0; echo -n " [cert.p12 leaked]"; }
  test ! -d "$output/.git" || { ok=0; echo -n " [.git leaked]"; }

  if grep -qR "javascript:" "$output" 2>/dev/null; then
    ok=0
    echo -n " [javascript URL leaked]"
  fi
  if grep -qR "data:text/html" "$output" 2>/dev/null; then
    ok=0
    echo -n " [data URL leaked]"
  fi
  if grep -qR "file:///etc/passwd" "$output" 2>/dev/null; then
    ok=0
    echo -n " [file URL leaked]"
  fi
  if grep -qR "vbscript:" "$output" 2>/dev/null; then
    ok=0
    echo -n " [vbscript URL leaked]"
  fi
  if grep -qR "//evil.com" "$output" 2>/dev/null; then
    ok=0
    echo -n " [protocol-relative URL leaked]"
  fi

  if [ "$ok" -eq 1 ]; then
    bash scripts/validate-artifacts-json.sh "$output" >/dev/null
    return 0
  fi

  return 1
}

echo "=== Fixture smoke checks ==="
for entry in "${fixtures[@]}"; do
  name="${entry%%:*}"
  config="${entry#*:}"
  config_dir="$(dirname "$config")"
  output="$smoke_root/$name/dist"
  cache="$smoke_root/$name/cache"
  full_output="$config_dir/$output"

  total=$((total + 1))
  echo -n "  $name ... "

  if bukit_cli "$configuration" build \
    --config "$config" \
    --output "$output" \
    --cache-dir "$cache" \
    --clean \
    --site-url https://example.com \
    --ci >/dev/null 2>&1 && check_output "$full_output"; then
    echo "OK"
    passed=$((passed + 1))
  else
    echo "FAILED"
    failed=$((failed + 1))
  fi
done

echo "=== Fixture smoke results: $passed passed, $failed failed, total $total ==="
if [ "$failed" -gt 0 ]; then
  exit 1
fi

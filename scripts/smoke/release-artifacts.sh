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

smoke_report="$publish_dir/release-artifact-smoke.md"
{
  echo "# Bukit Release Artifact Smoke Report"
  echo "Binary: \`$binary\`"
  echo "Publish dir: \`$publish_dir\`"
  echo "Timestamp: $(date -u +%FT%TZ)"
  echo ""
} > "$smoke_report"

record_step() {
  local step_name="$1"
  shift

  set +e
  "$@"
  local status=$?
  set -e

  if [ $status -eq 0 ]; then
    echo "- [PASS] $step_name" >> "$smoke_report"
  else
    echo "- [FAIL] $step_name (exit=$status)" >> "$smoke_report"
    echo "Release artifact smoke failed at: $step_name (exit=$status)" >&2
    exit $status
  fi
}

fixture="tests/fixtures/basic-markdown-site"
run_id="$(date +%Y%m%d%H%M%S)-$$"
smoke_root=".smoke-all-run/release-artifacts-$run_id"
cleanup() {
  rm -rf "$fixture/$smoke_root"
}
trap cleanup EXIT

record_step "Binary startup" "$binary" version
record_step "Version command returns version text" bash -c "\"$binary\" version | grep -Eq '[0-9]+\\.[0-9]+\\.[0-9]+'"
record_step "CLI help includes core commands" bash -c "\"$binary\" --help | grep -qE '^  (build|config|doctor|deploy|dev|seo|geo|publish|version|clean)'"
record_step "CLI help excludes non-Core command family" bash -c "! \"$binary\" --help | grep -Eq 'bukit[[:space:]]+(docs|intent|plugin|theme|import|clone|visual|webhook|data)([[:space:]]|$)|docs[[:space:]]+check|--allow-external-plugins'"
record_step "CLI dev help includes LiveReload wording" bash -c "\"$binary\" dev --help | grep -q 'LiveReload'"
record_step "CLI dev help excludes HMR wording" bash -c "! \"$binary\" dev --help | grep -q 'HMR'"

schema_path="$fixture/$smoke_root/site.schema.json"
mkdir -p "$(dirname "$schema_path")"
record_step "Generate site schema" "$binary" config schema --output "$schema_path"
record_step "Validate schema file exists" test -s "$schema_path"
record_step "Validate schema JSON format" python3 -m json.tool "$schema_path"
record_step "Config check fixture site" "$binary" config check --config "$fixture/site.yaml" --site-url https://example.com
record_step "Doctor check fixture site" "$binary" doctor --config "$fixture/site.yaml" --site-url https://example.com
record_step "Deploy dry-run fixture site" "$binary" deploy --dry-run --skip-build --config "$fixture/site.yaml"

output="$smoke_root/dist"
cache="$smoke_root/cache"
record_step "Build fixture site" "$binary" build \
  --config "$fixture/site.yaml" \
  --output "$output" \
  --cache-dir "$cache" \
  --clean \
  --site-url https://example.com \
  --ci

full_output="$fixture/$output"
record_step "Build output contains index.html" test -f "$full_output/index.html"
record_step "Build output contains sitemap.xml" test -f "$full_output/sitemap.xml"
record_step "SEO audit" "$binary" seo audit --dir "$full_output"
record_step "Geo audit" "$binary" geo audit --dir "$full_output"
record_step "Publish audit" "$binary" publish audit --dir "$full_output"
record_step "Validate .bukit artifacts JSON" bash scripts/validate-artifacts-json.sh "$full_output"
record_step "Smoke report exists" test -s "$smoke_report"

echo "Release artifact smoke OK: $binary"

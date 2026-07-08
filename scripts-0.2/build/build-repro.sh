#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"
source scripts/lib/common.sh

config="tests/fixtures/basic-markdown-site/site.yaml"
config_dir="$(dirname "$config")"
run_id="$(date +%Y%m%d%H%M%S)-$$"
smoke_root=".smoke-all-run/repro-$run_id"
run_dir="$config_dir/$smoke_root"

cleanup() {
  rm -rf "$run_dir"
}
trap cleanup EXIT
mkdir -p "$run_dir"

run_build() {
  local name="$1"
  local clean_flag="$2"
  local incremental_flag="$3"
  local output="$smoke_root/$name/dist"
  local cache="$smoke_root/$name/cache"

  bukit_cli "$configuration" build \
    --config "$config" \
    --output "$output" \
    --cache-dir "$cache" \
    "$clean_flag" \
    "$incremental_flag" \
    --site-url https://example.com \
    --ci >&2

  printf '%s\n' "$config_dir/$output"
}

compare_json() {
  local expected="$1"
  local actual="$2"
  local label="$3"
  local expected_norm="$run_dir/$(basename "$expected").expected.norm"
  local actual_norm="$run_dir/$(basename "$actual").actual.norm"

  bash scripts/normalize-json.sh "$expected" "$expected_norm"
  bash scripts/normalize-json.sh "$actual" "$actual_norm"

  cmp --silent "$expected_norm" "$actual_norm" || {
    echo "ERROR: ${label} mismatch in reproducible build check." >&2
    echo "Expected normalized: $expected_norm" >&2
    echo "Actual normalized:   $actual_norm" >&2
    exit 1
  }
}

manifest_file() {
  local output_dir="$1"
  local manifest_path="$2"

  {
    find "$output_dir" -type f \
      -not -path "$output_dir/.bukit/*" \
      -not -name ".bukit-build-state.json" \
      -not -name ".bukit-output-marker" \
      -print0
  } | while IFS= read -r -d '' file; do
    rel_path="${file#"$output_dir"/}"
    size="$(wc -c <"$file")"
    hash="$(bukit_sha256 "$file")"
    printf '%s %s %s\n' "$hash" "$size" "$rel_path"
  done | sort > "$manifest_path"
}

run1_out="$(run_build clean-1 --clean --no-incremental)"
run2_out="$(run_build clean-2 --clean --no-incremental)"

bash scripts/validate-artifacts-json.sh "$run1_out"
bash scripts/validate-artifacts-json.sh "$run2_out"

compare_json "$run1_out/.bukit/routes.json" "$run2_out/.bukit/routes.json" ".bukit/routes.json"
compare_json "$run1_out/.bukit/assets.json" "$run2_out/.bukit/assets.json" ".bukit/assets.json"
compare_json "$run1_out/.bukit/incremental-manifest.json" "$run2_out/.bukit/incremental-manifest.json" ".bukit/incremental-manifest.json"
if [ -f "$run1_out/.bukit/security-report.json" ] && [ -f "$run2_out/.bukit/security-report.json" ]; then
  compare_json "$run1_out/.bukit/security-report.json" "$run2_out/.bukit/security-report.json" ".bukit/security-report.json"
fi

run3_out="$(run_build clean-2 --no-clean --incremental)"
bash scripts/validate-artifacts-json.sh "$run3_out"
compare_json "$run1_out/.bukit/assets.json" "$run3_out/.bukit/assets.json" "incremental .bukit/assets.json"
if [ -f "$run1_out/.bukit/security-report.json" ] && [ -f "$run3_out/.bukit/security-report.json" ]; then
  compare_json "$run1_out/.bukit/security-report.json" "$run3_out/.bukit/security-report.json" "incremental .bukit/security-report.json"
fi

test -f "$run3_out/.bukit/incremental-manifest.json"

run1_manifest="$run_dir/run1.manifest"
run2_manifest="$run_dir/run2.manifest"
run3_manifest="$run_dir/run3.manifest"
manifest_file "$run1_out" "$run1_manifest"
manifest_file "$run2_out" "$run2_manifest"
manifest_file "$run3_out" "$run3_manifest"

diff -u "$run1_manifest" "$run2_manifest" >/dev/null || {
  echo "ERROR: clean public output manifest mismatch." >&2
  exit 1
}

diff -u "$run1_manifest" "$run3_manifest" >/dev/null || {
  echo "ERROR: incremental public output manifest mismatch." >&2
  exit 1
}

echo "Reproducible build check OK"

#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"
work_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$work_root"

config_root="$work_root/examples/starter"
smoke_root=".sitegen-repro"
run_id="$(date +%Y%m%d%H%M%S)-$$"
run_dir="$config_root/$smoke_root/$run_id"
trap 'rm -rf "$run_dir"' EXIT

build_config="examples/starter/site.yaml"

mkdir -p "$run_dir"

run_clean_build() {
    local run_id="$1"
    local output_path="$smoke_root/$run_id/dist"
    local full_output_path="$config_root/$output_path"
    mkdir -p "$(dirname "$full_output_path")"
    dotnet run --project src/Bukit.Cli -c "$configuration" -- build \
        --config "$build_config" \
        --output "$output_path" \
        --clean \
        --site-url https://example.com \
        --allow-external-plugins >&2
    printf '%s\n' "$full_output_path"
}

run_incremental_build() {
    local run_id="$1"
    local output_path="$smoke_root/$run_id/dist"
    local full_output_path="$config_root/$output_path"
    mkdir -p "$(dirname "$full_output_path")"
    dotnet run --project src/Bukit.Cli -c "$configuration" -- build \
        --config "$build_config" \
        --output "$output_path" \
        --no-clean \
        --incremental \
        --site-url https://example.com \
        --allow-external-plugins >&2
    printf '%s\n' "$full_output_path"
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
        echo "ERROR: ${label} mismatch in reproducible build check."
        echo "Expected (normalized): $expected_norm"
        echo "Actual   (normalized): $actual_norm"
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
        hash="$(sha256sum "$file" | awk '{print $1}')"
        printf '%s %s %s\n' "$hash" "$size" "$rel_path"
    done | sort > "$manifest_path"
}

run1_out="$(run_clean_build "$run_id/clean-1")"
run2_out="$(run_clean_build "$run_id/clean-2")"

bash scripts/validate-artifacts-json.sh "$run1_out"
bash scripts/validate-artifacts-json.sh "$run2_out"

compare_json "$run1_out/.bukit/routes.json" "$run2_out/.bukit/routes.json" ".bukit/routes.json"
compare_json "$run1_out/.bukit/assets.json" "$run2_out/.bukit/assets.json" ".bukit/assets.json"
compare_json "$run1_out/.bukit/incremental-manifest.json" "$run2_out/.bukit/incremental-manifest.json" ".bukit/incremental-manifest.json"
if [ -f "$run1_out/.bukit/security-report.json" ] && [ -f "$run2_out/.bukit/security-report.json" ]; then
    compare_json "$run1_out/.bukit/security-report.json" "$run2_out/.bukit/security-report.json" ".bukit/security-report.json"
fi

run3_out="$(run_incremental_build "$run_id/clean-2")"
bash scripts/validate-artifacts-json.sh "$run3_out"
compare_json "$run1_out/.bukit/assets.json" "$run3_out/.bukit/assets.json" "incremental .bukit/assets.json"
if [ -f "$run1_out/.bukit/security-report.json" ] && [ -f "$run3_out/.bukit/security-report.json" ]; then
    compare_json "$run1_out/.bukit/security-report.json" "$run3_out/.bukit/security-report.json" "incremental .bukit/security-report.json"
fi

if [ ! -f "$run3_out/.bukit/incremental-manifest.json" ]; then
    echo "ERROR: run3 incremental-manifest.json missing."
    exit 1
fi

run1_manifest="$run_dir/run1.manifest"
run2_manifest="$run_dir/run2.manifest"
run3_manifest="$run_dir/run3.manifest"
manifest_file "$run1_out" "$run1_manifest"
manifest_file "$run2_out" "$run2_manifest"
manifest_file "$run3_out" "$run3_manifest"

diff -u "$run1_manifest" "$run2_manifest" >/dev/null || {
    echo "ERROR: public output manifest mismatch in reproducible build check."
    exit 1
}

diff -u "$run1_manifest" "$run3_manifest" >/dev/null || {
    echo "ERROR: incremental public output manifest mismatch in reproducible build check."
    exit 1
}

echo "OK reproducible-build check"

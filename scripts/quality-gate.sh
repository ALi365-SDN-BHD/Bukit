#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"
coverage_threshold="${COVERAGE_THRESHOLD:-80}"
cli_coverage_threshold="${CLI_COVERAGE_THRESHOLD:-50}"
coverage_root="${COVERAGE_ROOT:-TestResults}"
coverage_report_dir="${COVERAGE_REPORT_DIR:-${coverage_root}/coverage-report}"
core_coverage_report_dir="${CORE_COVERAGE_REPORT_DIR:-${coverage_report_dir}/core}"
cli_coverage_report_dir="${CLI_COVERAGE_REPORT_DIR:-${coverage_report_dir}/cli}"
overall_coverage_report_dir="${OVERALL_COVERAGE_REPORT_DIR:-${coverage_report_dir}/overall}"
core_assembly_filters="${CORE_COVERAGE_ASSEMBLY_FILTERS:--bukit;-SampleAfterBuildPlugin;-VisualFeedbackPlugin;-ProtocolEchoPlugin}"
cli_assembly_filters="${CLI_COVERAGE_ASSEMBLY_FILTERS:-+bukit}"
max_file_lines="${MAX_FILE_LINES:-600}"
oversized_baseline="${OVERSIZED_BASELINE:-scripts/.oversized-baseline.txt}"

# --- File-size guardrail (single-class cohesion check) ---
# Engineering principle: single .cs file SHOULD NOT exceed MAX_FILE_LINES.
# Auto-generated files under obj/, bin/, .codex-tmp* are excluded.
#
# Strategy: any file currently exceeding the limit but already listed in
# OVERSIZED_BASELINE is treated as known technical debt (warning only).
# Any *new* oversized file fails the gate. This keeps main green while
# preventing further regressions. To grandfather an existing violator,
# append its path to scripts/.oversized-baseline.txt with justification.
current_oversized="$(find src -type f -name '*.cs' \
    -not -path '*/obj/*' \
    -not -path '*/bin/*' \
    -not -path '*/.codex-tmp*/*' \
    -exec wc -l {} + 2>/dev/null \
    | awk -v limit="$max_file_lines" '$1 > limit && $2 != "total" { print $2 }' \
    | sort -u)"

baseline_paths=""
if [ -f "$oversized_baseline" ]; then
    baseline_paths="$(grep -vE '^\s*(#|$)' "$oversized_baseline" | sort -u || true)"
fi

new_oversized="$(comm -23 <(echo "$current_oversized") <(echo "$baseline_paths"))"

if [ -n "$baseline_paths" ] && [ -n "$current_oversized" ]; then
    grandfathered_still_present="$(comm -12 <(echo "$current_oversized") <(echo "$baseline_paths"))"
    if [ -n "$grandfathered_still_present" ]; then
        echo "WARNING: pre-existing oversized files (technical debt, see ${oversized_baseline}):"
        while IFS= read -r path; do
            [ -z "$path" ] && continue
            lines="$(wc -l <"$path" 2>/dev/null | tr -d ' ')"
            echo "  ${lines:-?} lines  $path"
        done <<<"$grandfathered_still_present"
    fi
fi

if [ -n "$new_oversized" ]; then
    echo "ERROR: the following .cs files exceed the cohesion limit of ${max_file_lines} lines and are NOT in '${oversized_baseline}':" >&2
    while IFS= read -r path; do
        [ -z "$path" ] && continue
        lines="$(wc -l <"$path" 2>/dev/null | tr -d ' ')"
        echo "  ${lines:-?} lines  $path" >&2
    done <<<"$new_oversized"
    echo "       Please split them per Engineering Principles §2 (high cohesion, low coupling)." >&2
    echo "       Or, with justification, append the path(s) to '${oversized_baseline}'." >&2
    exit 1
fi

# --- Repo hygiene (no smoke/debug build artifacts tracked) ---
bash scripts/check-repo-hygiene.sh

# --- Encoding check (UTF-8 validity + mojibake detection) ---
bash scripts/check-encoding.sh

# --- Skills strict validation ---
bash src/skills/scripts/validate-skills-strict.sh || { echo "ERROR: Skills strict validation failed"; exit 1; }


dotnet build bukit.slnx -c "$configuration" -maxcpucount:1 -nodeReuse:false

# Clean previous coverage artefacts so that the threshold check only sees this run's data.
rm -rf "$coverage_root"
mkdir -p "$coverage_root"

dotnet test bukit.slnx \
    -c "$configuration" \
    --no-build \
    -maxcpucount:1 \
    -nodeReuse:false \
    --collect:"XPlat Code Coverage" \
    --settings coverage.runsettings \
    --results-directory "$coverage_root"

dotnet format bukit.slnx --verify-no-changes --no-restore
bash scripts/check-doc-asset-consistency.sh
bash scripts/smoke.sh "$configuration"
bash scripts/build-repro.sh "$configuration"

# --- Coverage aggregation & threshold check (Cobertura) ---

# Make sure the ReportGenerator global tool is available; install on-demand into ~/.dotnet/tools.
if ! command -v reportgenerator >/dev/null 2>&1; then
    echo "ReportGenerator not found, installing dotnet-reportgenerator-globaltool ..."
    dotnet tool install -g dotnet-reportgenerator-globaltool >/dev/null
    export PATH="$PATH:$HOME/.dotnet/tools"
fi

# Each test project produces its own coverage.cobertura.xml under TestResults/<guid>/.
# Use IFS-based array assignment instead of mapfile for bash 3.x compatibility (macOS).
IFS=$'\n' coverage_files=($(find "$coverage_root" -type f -name 'coverage.cobertura.xml' | sort))
if [ "${#coverage_files[@]}" -eq 0 ]; then
    echo "ERROR: no coverage.cobertura.xml files found under '$coverage_root'." >&2
    echo "       Did 'dotnet test --collect:\"XPlat Code Coverage\"' run successfully?" >&2
    exit 1
fi

# Merge per-project Cobertura reports into coverage summaries.
report_inputs="$(IFS=';'; echo "${coverage_files[*]}")"

generate_coverage_report() {
    local label="$1"
    local target_dir="$2"
    local filters="${3:-}"

    if [ -n "$filters" ]; then
        reportgenerator \
            -reports:"$report_inputs" \
            -targetdir:"$target_dir" \
            -reporttypes:"Cobertura;TextSummary" \
            -assemblyfilters:"$filters" >/dev/null
    else
        reportgenerator \
            -reports:"$report_inputs" \
            -targetdir:"$target_dir" \
            -reporttypes:"Cobertura;TextSummary" >/dev/null
    fi

    local cobertura="$target_dir/Cobertura.xml"
    if [ ! -f "$cobertura" ]; then
        echo "ERROR: ${label} Cobertura report not found at '$cobertura'." >&2
        exit 1
    fi

    local line_rate
    line_rate="$(grep -m1 -oE 'line-rate="[0-9.]+"' "$cobertura" | head -n1 | sed -E 's/line-rate="([0-9.]+)"/\1/')"
    if [ -z "$line_rate" ]; then
        echo "ERROR: could not parse ${label} line-rate from '$cobertura'." >&2
        exit 1
    fi

    awk -v r="$line_rate" 'BEGIN { printf "%.2f", r * 100 }'
}

overall_coverage_percent="$(generate_coverage_report "overall" "$overall_coverage_report_dir")"
core_coverage_percent="$(generate_coverage_report "core" "$core_coverage_report_dir" "$core_assembly_filters")"
cli_coverage_percent="$(generate_coverage_report "cli" "$cli_coverage_report_dir" "$cli_assembly_filters")"

core_meets_threshold="$(awk -v c="$core_coverage_percent" -v t="$coverage_threshold" 'BEGIN { print (c + 0 >= t + 0) ? "yes" : "no" }')"
cli_meets_threshold="$(awk -v c="$cli_coverage_percent" -v t="$cli_coverage_threshold" 'BEGIN { print (c + 0 >= t + 0) ? "yes" : "no" }')"

echo "Coverage overall: ${overall_coverage_percent}% (informational)"
echo "Coverage core: ${core_coverage_percent}% (threshold: ${coverage_threshold}%, filters: ${core_assembly_filters})"
echo "Coverage cli: ${cli_coverage_percent}% (threshold: ${cli_coverage_threshold}%, filters: ${cli_assembly_filters})"
echo "Detailed core Cobertura report: $core_coverage_report_dir/Cobertura.xml"
echo "Detailed CLI Cobertura report: $cli_coverage_report_dir/Cobertura.xml"

if [ -f "$core_coverage_report_dir/Summary.txt" ]; then
    echo "--- Core coverage summary ---"
    cat "$core_coverage_report_dir/Summary.txt"
    echo "-----------------------------"
fi

if [ -f "$cli_coverage_report_dir/Summary.txt" ]; then
    echo "--- CLI coverage summary ---"
    cat "$cli_coverage_report_dir/Summary.txt"
    echo "----------------------------"
fi

if [ "$core_meets_threshold" != "yes" ]; then
    echo "ERROR: core coverage ${core_coverage_percent}% is below the required threshold of ${coverage_threshold}%." >&2
    exit 1
fi

if [ "$cli_meets_threshold" != "yes" ]; then
    echo "ERROR: CLI coverage ${cli_coverage_percent}% is below the required threshold of ${cli_coverage_threshold}%." >&2
    exit 1
fi

echo "=== smoke-all ==="
bash scripts/smoke-all.sh "$configuration"

echo "Quality gate OK"

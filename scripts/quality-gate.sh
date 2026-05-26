#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"
coverage_threshold="${COVERAGE_THRESHOLD:-80}"
coverage_root="${COVERAGE_ROOT:-TestResults}"
coverage_report_dir="${COVERAGE_REPORT_DIR:-${coverage_root}/coverage-report}"

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
    --results-directory "$coverage_root"

dotnet format bukit.slnx --verify-no-changes --no-restore
bash scripts/check-doc-asset-consistency.sh
bash scripts/smoke.sh "$configuration"

# --- Coverage aggregation & threshold check (Cobertura) ---

# Make sure the ReportGenerator global tool is available; install on-demand into ~/.dotnet/tools.
if ! command -v reportgenerator >/dev/null 2>&1; then
    echo "ReportGenerator not found, installing dotnet-reportgenerator-globaltool ..."
    dotnet tool install -g dotnet-reportgenerator-globaltool >/dev/null
    export PATH="$PATH:$HOME/.dotnet/tools"
fi

# Each test project produces its own coverage.cobertura.xml under TestResults/<guid>/.
mapfile -t coverage_files < <(find "$coverage_root" -type f -name 'coverage.cobertura.xml' | sort)
if [ "${#coverage_files[@]}" -eq 0 ]; then
    echo "ERROR: no coverage.cobertura.xml files found under '$coverage_root'." >&2
    echo "       Did 'dotnet test --collect:\"XPlat Code Coverage\"' run successfully?" >&2
    exit 1
fi

# Merge per-project Cobertura reports into a single summary at TestResults/coverage-report.
report_inputs="$(IFS=';'; echo "${coverage_files[*]}")"
reportgenerator \
    -reports:"$report_inputs" \
    -targetdir:"$coverage_report_dir" \
    -reporttypes:"Cobertura;TextSummary" >/dev/null

merged_cobertura="$coverage_report_dir/Cobertura.xml"
if [ ! -f "$merged_cobertura" ]; then
    echo "ERROR: merged Cobertura report not found at '$merged_cobertura'." >&2
    exit 1
fi

# Cobertura's root <coverage line-rate="0.8523" ...> is a fraction in [0, 1].
line_rate="$(grep -m1 -oE 'line-rate="[0-9.]+"' "$merged_cobertura" | head -n1 | sed -E 's/line-rate="([0-9.]+)"/\1/')"
if [ -z "$line_rate" ]; then
    echo "ERROR: could not parse line-rate from '$merged_cobertura'." >&2
    exit 1
fi

coverage_percent="$(awk -v r="$line_rate" 'BEGIN { printf "%.2f", r * 100 }')"
meets_threshold="$(awk -v c="$coverage_percent" -v t="$coverage_threshold" 'BEGIN { print (c + 0 >= t + 0) ? "yes" : "no" }')"

echo "Coverage: ${coverage_percent}% (threshold: ${coverage_threshold}%)"
echo "Detailed Cobertura report: $merged_cobertura"

if [ -f "$coverage_report_dir/Summary.txt" ]; then
    echo "--- Coverage summary ---"
    cat "$coverage_report_dir/Summary.txt"
    echo "------------------------"
fi

if [ "$meets_threshold" != "yes" ]; then
    echo "ERROR: coverage ${coverage_percent}% is below the required threshold of ${coverage_threshold}%." >&2
    exit 1
fi

echo "Quality gate OK"

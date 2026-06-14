#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

configuration="${1:-Release}"
coverage_threshold="${COVERAGE_THRESHOLD:-35}"
cli_coverage_threshold="${CLI_COVERAGE_THRESHOLD:-45}"
coverage_root="${COVERAGE_ROOT:-TestResults/coverage}"
coverage_report_dir="${COVERAGE_REPORT_DIR:-${coverage_root}/report}"
core_coverage_report_dir="${CORE_COVERAGE_REPORT_DIR:-${coverage_report_dir}/core}"
cli_coverage_report_dir="${CLI_COVERAGE_REPORT_DIR:-${coverage_report_dir}/cli}"
overall_coverage_report_dir="${OVERALL_COVERAGE_REPORT_DIR:-${coverage_report_dir}/overall}"
core_assembly_filters="${CORE_COVERAGE_ASSEMBLY_FILTERS:--bukit;-SampleAfterBuildPlugin;-VisualFeedbackPlugin;-ProtocolEchoPlugin}"
cli_assembly_filters="${CLI_COVERAGE_ASSEMBLY_FILTERS:-+bukit}"
coverage_no_build="${COVERAGE_NO_BUILD:-}"

if [ -z "$coverage_no_build" ]; then
  if [ "${CI_FULL_SKIP_FAST:-0}" = "1" ]; then
    coverage_no_build=0
  else
    coverage_no_build=1
  fi
fi

rm -rf "$coverage_root"
mkdir -p "$coverage_root"

if [ "$coverage_no_build" = "1" ]; then
  dotnet test bukit.slnx \
    -c "$configuration" \
    --no-build \
    -maxcpucount:1 \
    -nodeReuse:false \
    --collect:"XPlat Code Coverage" \
    --settings coverage.runsettings \
    --logger trx \
    --results-directory "$coverage_root"
else
  dotnet test bukit.slnx \
    -c "$configuration" \
    -maxcpucount:1 \
    -nodeReuse:false \
    --collect:"XPlat Code Coverage" \
    --settings coverage.runsettings \
    --logger trx \
    --results-directory "$coverage_root"
fi

if ! command -v reportgenerator >/dev/null 2>&1; then
  echo "ReportGenerator not found; installing dotnet-reportgenerator-globaltool ..."
  dotnet tool install -g dotnet-reportgenerator-globaltool >/dev/null
  export PATH="$PATH:$HOME/.dotnet/tools"
fi

IFS=$'\n' coverage_files=($(find "$coverage_root" -type f -name 'coverage.cobertura.xml' | sort))
if [ "${#coverage_files[@]}" -eq 0 ]; then
  echo "ERROR: no coverage.cobertura.xml files found under '$coverage_root'." >&2
  exit 1
fi

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

echo "Coverage overall: ${overall_coverage_percent}%"
echo "Coverage core: ${core_coverage_percent}% (threshold: ${coverage_threshold}%)"
echo "Coverage cli: ${cli_coverage_percent}% (threshold: ${cli_coverage_threshold}%)"

if [ "$core_meets_threshold" != "yes" ]; then
  echo "ERROR: core coverage ${core_coverage_percent}% is below ${coverage_threshold}%." >&2
  exit 1
fi

if [ "$cli_meets_threshold" != "yes" ]; then
  echo "ERROR: CLI coverage ${cli_coverage_percent}% is below ${cli_coverage_threshold}%." >&2
  exit 1
fi

echo "Coverage check OK"

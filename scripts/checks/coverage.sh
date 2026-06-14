#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

configuration="${1:-Release}"
# Core and CLI stay on the blocking gate. Bukit.Importing and bukit-labs are
# reported separately and only become blocking when their dedicated thresholds
# are set.
core_coverage_threshold="${CORE_COVERAGE_THRESHOLD:-${COVERAGE_THRESHOLD:-35}}"
cli_coverage_threshold="${CLI_COVERAGE_THRESHOLD:-45}"
importing_coverage_threshold="${IMPORTING_COVERAGE_THRESHOLD:-}"
labs_coverage_threshold="${LABS_COVERAGE_THRESHOLD:-}"
coverage_root="${COVERAGE_ROOT:-TestResults/coverage}"
coverage_report_dir="${COVERAGE_REPORT_DIR:-${coverage_root}/report}"
core_coverage_report_dir="${CORE_COVERAGE_REPORT_DIR:-${coverage_report_dir}/core}"
cli_coverage_report_dir="${CLI_COVERAGE_REPORT_DIR:-${coverage_report_dir}/cli}"
importing_coverage_report_dir="${IMPORTING_COVERAGE_REPORT_DIR:-${coverage_report_dir}/importing}"
labs_coverage_report_dir="${LABS_COVERAGE_REPORT_DIR:-${coverage_report_dir}/labs}"
overall_coverage_report_dir="${OVERALL_COVERAGE_REPORT_DIR:-${coverage_report_dir}/overall}"
core_assembly_filters="${CORE_COVERAGE_ASSEMBLY_FILTERS:--bukit;-bukit-labs;-Bukit.Importing;-SampleAfterBuildPlugin;-VisualFeedbackPlugin;-ProtocolEchoPlugin}"
cli_assembly_filters="${CLI_COVERAGE_ASSEMBLY_FILTERS:-+bukit}"
importing_assembly_filters="${IMPORTING_COVERAGE_ASSEMBLY_FILTERS:-+Bukit.Importing}"
labs_assembly_filters="${LABS_COVERAGE_ASSEMBLY_FILTERS:-+bukit-labs}"
coverage_summary_file="${COVERAGE_SUMMARY_FILE:-${coverage_report_dir}/summary.txt}"
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

# Bukit.Importing tests live outside bukit.slnx but still own the real coverage for
# the Bukit.Importing assembly, so collect them into the same merged report set.
dotnet test tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj \
  -c "$configuration" \
  -maxcpucount:1 \
  -nodeReuse:false \
  --collect:"XPlat Code Coverage" \
  --settings coverage.runsettings \
  --logger trx \
  --results-directory "$coverage_root"

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

coverage_meets_threshold() {
  local coverage_percent="$1"
  local threshold="${2:-}"

  if [ -z "$threshold" ]; then
    echo "yes"
    return
  fi

  awk -v c="$coverage_percent" -v t="$threshold" 'BEGIN { print (c + 0 >= t + 0) ? "yes" : "no" }'
}

print_coverage_status() {
  local label="$1"
  local coverage_percent="$2"
  local threshold="${3:-}"

  if [ -n "$threshold" ]; then
    echo "Coverage ${label}: ${coverage_percent}% (threshold: ${threshold}%)"
  else
    echo "Coverage ${label}: ${coverage_percent}% (tracked only)"
  fi
}

overall_coverage_percent="$(generate_coverage_report "overall" "$overall_coverage_report_dir")"
core_coverage_percent="$(generate_coverage_report "core" "$core_coverage_report_dir" "$core_assembly_filters")"
cli_coverage_percent="$(generate_coverage_report "cli" "$cli_coverage_report_dir" "$cli_assembly_filters")"
importing_coverage_percent="$(generate_coverage_report "importing" "$importing_coverage_report_dir" "$importing_assembly_filters")"
labs_coverage_percent="$(generate_coverage_report "labs" "$labs_coverage_report_dir" "$labs_assembly_filters")"

core_meets_threshold="$(coverage_meets_threshold "$core_coverage_percent" "$core_coverage_threshold")"
cli_meets_threshold="$(coverage_meets_threshold "$cli_coverage_percent" "$cli_coverage_threshold")"
importing_meets_threshold="$(coverage_meets_threshold "$importing_coverage_percent" "$importing_coverage_threshold")"
labs_meets_threshold="$(coverage_meets_threshold "$labs_coverage_percent" "$labs_coverage_threshold")"

echo "Coverage overall: ${overall_coverage_percent}%"
print_coverage_status "core" "$core_coverage_percent" "$core_coverage_threshold"
print_coverage_status "cli" "$cli_coverage_percent" "$cli_coverage_threshold"
print_coverage_status "importing" "$importing_coverage_percent" "$importing_coverage_threshold"
print_coverage_status "labs" "$labs_coverage_percent" "$labs_coverage_threshold"

mkdir -p "$(dirname "$coverage_summary_file")"
{
  printf "overall=%s\n" "$overall_coverage_percent"
  printf "core=%s\n" "$core_coverage_percent"
  printf "cli=%s\n" "$cli_coverage_percent"
  printf "importing=%s\n" "$importing_coverage_percent"
  printf "labs=%s\n" "$labs_coverage_percent"
  printf "core_threshold=%s\n" "$core_coverage_threshold"
  printf "cli_threshold=%s\n" "$cli_coverage_threshold"
  printf "importing_threshold=%s\n" "$importing_coverage_threshold"
  printf "labs_threshold=%s\n" "$labs_coverage_threshold"
} > "$coverage_summary_file"

echo "Coverage summary: ${coverage_summary_file}"

if [ "$core_meets_threshold" != "yes" ]; then
  echo "ERROR: core coverage ${core_coverage_percent}% is below ${core_coverage_threshold}%." >&2
  exit 1
fi

if [ "$cli_meets_threshold" != "yes" ]; then
  echo "ERROR: CLI coverage ${cli_coverage_percent}% is below ${cli_coverage_threshold}%." >&2
  exit 1
fi

if [ "$importing_meets_threshold" != "yes" ]; then
  echo "ERROR: importing coverage ${importing_coverage_percent}% is below ${importing_coverage_threshold}%." >&2
  exit 1
fi

if [ "$labs_meets_threshold" != "yes" ]; then
  echo "ERROR: labs coverage ${labs_coverage_percent}% is below ${labs_coverage_threshold}%." >&2
  exit 1
fi

echo "Coverage check OK"

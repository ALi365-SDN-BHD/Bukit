#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
cd "$repo_root"

configuration="${1:?configuration is required}"
project="${2:?project is required}"
output_root="${3:?output root is required}"
filter="${4:-}"
settings="${BUKIT_COVERAGE_SETTINGS:-coverage.runsettings}"
name="$(basename "$(dirname "$project")")"

output_root="$(bash scripts/checks/coverage/validate-output-root.py "$output_root" "$repo_root")"
results_dir="$(bash scripts/checks/coverage/validate-output-root.py "${output_root}/${name}" "$repo_root")"

echo "coverage project: ${name}"
echo "coverage output: ${results_dir}"
rm -rf "$results_dir"
mkdir -p "$results_dir"

args=(
  "$project"
  -c "$configuration"
  "--collect:XPlat Code Coverage"
  --settings "$settings"
  --logger "console;verbosity=minimal"
  --logger "trx;LogFileName=tests.trx"
  --results-directory "$results_dir"
)

if [[ -n "$filter" ]]; then
  args+=(--filter "$filter")
fi

dotnet test "${args[@]}"

python3 - "$results_dir/tests.trx" <<'PY_TRX'
import sys
import xml.etree.ElementTree as ET

try:
    root = ET.parse(sys.argv[1]).getroot()
    summary = root.find("{*}ResultSummary")
    counters = summary.find("{*}Counters")
    rows = root.findall("{*}Results/{*}UnitTestResult")
    passed = [row for row in rows if row.get("outcome") == "Passed"]
    skipped = [row for row in rows if row.get("outcome") == "NotExecuted"]
    for row in skipped:
        print("TRX skipped: " + row.get("testName", "unknown"))
    valid = (summary.get("outcome") in ("Completed", "Passed") and len(passed) > 0
             and len(rows) == len(passed) + len(skipped)
             and int(counters.get("total", "-1")) == len(rows)
             and int(counters.get("executed", "-1")) == len(passed)
             and int(counters.get("passed", "-1")) == len(passed)
             and int(counters.get("notExecuted", "0")) == len(skipped)
             and all(int(counters.get(key, "0")) == 0 for key in
                     ("failed", "error", "timeout", "aborted", "inconclusive", "notRunnable", "disconnected")))
    if not valid:
        raise ValueError("no executed tests, failed/aborted results, or inconsistent counters")
    print(f"TRX executed: {len(passed)}; skipped: {len(skipped)}")
except (OSError, ET.ParseError, AttributeError, ValueError) as error:
    raise SystemExit(f"Invalid coverage TRX: {error}")
PY_TRX

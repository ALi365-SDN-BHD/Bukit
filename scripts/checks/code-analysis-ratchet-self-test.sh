#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

fail() {
  echo "code analysis ratchet self-test failed: $*" >&2
  exit 1
}

wrapper="scripts/checks/code-analysis-ratchet.sh"
comparator="scripts/checks/code-analysis-ratchet.py"
baseline="scripts/checks/baselines/code-analysis.v1.json"
[[ -f "$wrapper" ]] || fail "ratchet wrapper is missing"
[[ -f "$comparator" ]] || fail "ratchet comparator is missing"
[[ -f "$baseline" ]] || fail "committed ratchet baseline is missing"
bash scripts/checks/code-analysis-ratchet-policy-self-test.sh

scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-code-analysis-ratchet-self-test.XXXXXX")"
trap 'rm -rf -- "$scratch"' EXIT

python3 - "$scratch" <<'PY'
import json
from pathlib import Path
import sys

root = Path(sys.argv[1])

def report(path: str, diagnostics: list[str]) -> None:
    payload = []
    for index, diagnostic in enumerate(diagnostics, start=1):
        payload.append({
            "DocumentId": {"ProjectId": {"Id": "project"}, "Id": f"document-{index}"},
            "FileName": f"File{index}.cs",
            "FilePath": f"/fixture/File{index}.cs",
            "FileChanges": [{
                "LineNumber": index,
                "CharNumber": 1,
                "DiagnosticId": diagnostic,
                "FormatDescription": f"info {diagnostic}: fixture",
            }],
        })
    (root / path).write_text(json.dumps(payload), encoding="utf-8")

(root / "baseline.json").write_text(json.dumps({
    "schemaVersion": 1,
    "style": {"IDE0001": 2},
    "analyzers": {"CA1000": 1},
}), encoding="utf-8")
report("style-same.json", ["IDE0001", "IDE0001"])
report("style-lower.json", ["IDE0001"])
report("style-higher.json", ["IDE0001", "IDE0001", "IDE0001"])
report("analyzers-same.json", ["CA1000"])
report("analyzers-new.json", ["CA1000", "CA9999"])
(root / "malformed.json").write_text("{not-json", encoding="utf-8")
PY

assert_exit() {
  local expected="$1" output="$2"
  shift 2
  local status=0
  "$@" >"$output" 2>&1 || status=$?
  [[ "$status" == "$expected" ]] ||
    fail "expected exit $expected, got $status: $(tr '\n' ' ' <"$output")"
}

assert_exit 0 "$scratch/unchanged.txt" python3 "$comparator" compare \
  "$scratch/baseline.json" "$scratch/style-same.json" "$scratch/analyzers-same.json"
assert_exit 0 "$scratch/decreased.txt" python3 "$comparator" compare \
  "$scratch/baseline.json" "$scratch/style-lower.json" "$scratch/analyzers-same.json"
assert_exit 1 "$scratch/increased.txt" python3 "$comparator" compare \
  "$scratch/baseline.json" "$scratch/style-higher.json" "$scratch/analyzers-same.json"
grep -Fq 'regression: style IDE0001 current 3 exceeds baseline 2' "$scratch/increased.txt" ||
  fail "increased diagnostic lacks a stable regression message"
assert_exit 1 "$scratch/new-id.txt" python3 "$comparator" compare \
  "$scratch/baseline.json" "$scratch/style-same.json" "$scratch/analyzers-new.json"
grep -Fq 'regression: analyzers CA9999 current 1 exceeds baseline 0' "$scratch/new-id.txt" ||
  fail "new diagnostic ID lacks a stable regression message"
assert_exit 2 "$scratch/malformed.txt.out" python3 "$comparator" compare \
  "$scratch/malformed.json" "$scratch/style-same.json" "$scratch/analyzers-same.json"
grep -Fq 'gate-error:' "$scratch/malformed.txt.out" || fail "malformed input lacks gate-error"

snapshot="$scratch/snapshot.json"
assert_exit 0 "$scratch/snapshot.txt" python3 "$comparator" snapshot \
  "$snapshot" "$scratch/style-same.json" "$scratch/analyzers-same.json"
assert_exit 2 "$scratch/snapshot-existing.txt" python3 "$comparator" snapshot \
  "$snapshot" "$scratch/style-same.json" "$scratch/analyzers-same.json"

mkdir -p "$scratch/bin"
cat >"$scratch/bin/dotnet" <<'SH'
#!/usr/bin/env bash
printf '%s\n' "$*" >>"$BUKIT_FORMAT_CALLS"
report=""
previous=""
for argument in "$@"; do
  if [[ "$previous" == "--report" ]]; then report="$argument"; break; fi
  previous="$argument"
done
if [[ "${BUKIT_FAKE_FORMAT_STATUS:-2}" == "2" ]]; then
  mkdir -p "$report"
  case " $* " in
    *' format style '*) cp "$BUKIT_STYLE_REPORT" "$report/format-report.json" ;;
    *' format analyzers '*) cp "$BUKIT_ANALYZER_REPORT" "$report/format-report.json" ;;
    *) exit 98 ;;
  esac
fi
exit "${BUKIT_FAKE_FORMAT_STATUS:-2}"
SH
chmod +x "$scratch/bin/dotnet"

calls="$scratch/calls.txt"
BUKIT_CODE_ANALYSIS_BASELINE="$scratch/baseline.json" \
BUKIT_FORMAT_CALLS="$calls" \
BUKIT_STYLE_REPORT="$scratch/style-same.json" \
BUKIT_ANALYZER_REPORT="$scratch/analyzers-same.json" \
PATH="$scratch/bin:$PATH" \
  bash "$wrapper" check >"$scratch/wrapper.txt" || fail "wrapper rejected formatter exit 2 reports"
[[ "$(wc -l <"$calls" | tr -d ' ')" == "2" ]] || fail "wrapper did not invoke exactly two formatter scans"
grep -Fq 'format style bukit-core.slnx --verify-no-changes --no-restore --severity info --report' "$calls" ||
  fail "wrapper style command is incomplete"
grep -Fq 'format analyzers bukit-core.slnx --verify-no-changes --no-restore --severity info --report' "$calls" ||
  fail "wrapper analyzer command is incomplete"

assert_exit 2 "$scratch/wrapper-failure.txt" env \
  BUKIT_CODE_ANALYSIS_BASELINE="$scratch/baseline.json" \
  BUKIT_FORMAT_CALLS="$calls" \
  BUKIT_STYLE_REPORT="$scratch/style-same.json" \
  BUKIT_ANALYZER_REPORT="$scratch/analyzers-same.json" \
  BUKIT_FAKE_FORMAT_STATUS=7 \
  PATH="$scratch/bin:$PATH" \
  bash "$wrapper" check
grep -Fq 'gate-error:' "$scratch/wrapper-failure.txt" || fail "unexpected formatter failure lacks gate-error"

expected_self_test='run_step "code analysis ratchet self-test" bash scripts/checks/code-analysis-ratchet-self-test.sh'
expected_gate='run_step "code analysis ratchet" bash scripts/checks/code-analysis-ratchet.sh check'
[[ "$(grep -Fxc "$expected_self_test" scripts/gates/ci-fast.sh)" == "1" ]] ||
  fail "ci-fast self-test wiring is missing or duplicated"
[[ "$(grep -Fxc "$expected_gate" scripts/gates/ci-fast.sh)" == "1" ]] ||
  fail "ci-fast real-check wiring is missing or duplicated"
grep -Fq 'bash scripts/checks/code-analysis-ratchet.sh check' guide/dev/testing.md ||
  fail "testing guide does not document the ratchet check"
grep -Fq 'bash scripts/checks/code-analysis-ratchet.sh snapshot OUTPUT' guide/dev/testing.md ||
  fail "testing guide does not document baseline maintenance"

owner_output="$(bash scripts/checks/post-change-focused-owner-checks.sh --dry-run -- \
  .editorconfig Directory.Build.props scripts/checks/code-analysis-ratchet.py \
  scripts/checks/baselines/code-analysis.v1.json)"
[[ "$(grep -Fxc '+ bash scripts/checks/dotnet-format-self-test.sh' <<<"$owner_output")" == "1" ]] ||
  fail "format-owned configuration does not route to the format self-test exactly once"
[[ "$(grep -Fxc '+ bash scripts/checks/code-analysis-ratchet-self-test.sh' <<<"$owner_output")" == "1" ]] ||
  fail "analysis-owned paths do not route to the ratchet self-test exactly once"

echo "code analysis ratchet self-test OK"

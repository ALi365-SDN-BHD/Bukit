#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-coverage-run-one.XXXXXX")"
trap 'rm -rf "$scratch"' EXIT
mkdir "$scratch/bin"
cat > "$scratch/bin/dotnet" <<'PY'
#!/usr/bin/env python3
import json, os, sys
from pathlib import Path
args = sys.argv[1:]
with open(os.environ["FAKE_CALLS"], "a") as log:
    log.write(json.dumps(args) + "\n")
assert args[:4] == ["test", "tests/Fake.Tests/Fake.Tests.csproj", "-c", "Release"], args
assert args.count("--collect:XPlat Code Coverage") == 1
assert args[args.index("--settings") + 1] == "fake.runsettings"
assert "trx;LogFileName=tests.trx" in args, args
expected_filter = os.environ["FAKE_FILTER"]
assert (args[args.index("--filter") + 1] if "--filter" in args else "") == expected_filter
root = Path(args[args.index("--results-directory") + 1])
assert not (root / "stale.txt").exists()
mode = os.environ["FAKE_MODE"]
if mode == "exit":
    (root / "diagnostic.txt").write_text("test failed")
    sys.exit(23)
if mode == "missing": sys.exit(0)
if mode == "malformed":
    (root / "tests.trx").write_text("<broken")
    sys.exit(0)
passed = 0 if mode == "zero" else 1
skipped = 1 if mode == "skipped" else 0
failed = 1 if mode == "failed" else 0
rows = '<UnitTestResult testName="works" outcome="Passed" />' * passed
rows += '<UnitTestResult testName="ignored" outcome="NotExecuted" />' * skipped
rows += '<UnitTestResult testName="broken" outcome="Failed" />' * failed
executed = passed + failed + (1 if mode == "inconsistent" else 0)
outcome = "Aborted" if mode == "aborted" else "Completed"
(root / "tests.trx").write_text(f'<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>{rows}</Results><ResultSummary outcome="{outcome}"><Counters total="{passed+failed+skipped}" executed="{executed}" passed="{passed}" failed="{failed}" notExecuted="{skipped}" /></ResultSummary></TestRun>')
PY
chmod +x "$scratch/bin/dotnet"
export FAKE_CALLS="$scratch/calls" BUKIT_COVERAGE_SETTINGS=fake.runsettings
for mode in good skipped exit missing malformed zero failed aborted inconsistent; do
  export FAKE_MODE="$mode" FAKE_FILTER='FullyQualifiedName~CompleteClass'
  [[ "$mode" != good ]] || FAKE_FILTER=''
  mkdir -p "$scratch/results/Fake.Tests" "$scratch/results/sibling"
  touch "$scratch/results/Fake.Tests/stale.txt" "$scratch/results/sibling/keep.txt"
  : > "$FAKE_CALLS"
  status=0
  PATH="$scratch/bin:$PATH" bash scripts/checks/coverage/run-one.sh Release tests/Fake.Tests/Fake.Tests.csproj "$scratch/results" "$FAKE_FILTER" > "$scratch/output" 2>&1 || status=$?
  [[ "$(wc -l < "$FAKE_CALLS" | tr -d ' ')" == 1 ]] || { cat "$scratch/output"; exit 1; }
  [[ -f "$scratch/results/sibling/keep.txt" ]]
  case "$mode" in
    good|skipped) [[ "$status" == 0 ]] || { cat "$scratch/output"; exit 1; } ;;
    exit) [[ "$status" == 23 && -f "$scratch/results/Fake.Tests/diagnostic.txt" ]] ;;
    *) [[ "$status" == 1 ]] || { cat "$scratch/output"; exit 1; } ;;
  esac
  [[ "$mode" != skipped ]] || grep -Fq 'TRX skipped: ignored' "$scratch/output"
done
echo 'coverage run-one self-test OK'

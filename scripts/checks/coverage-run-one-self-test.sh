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
original = root / "12345678-1234-1234-1234-123456789abc" / "coverage.cobertura.xml"
attachment = root / "_fake_run" / "In" / "agent" / "coverage.cobertura.xml"
for report in (original, attachment):
    report.parent.mkdir(parents=True, exist_ok=True)
    report.write_text("<coverage />")
if mode == "different": attachment.write_text('<coverage different="yes" />')
if mode == "extra": (root / "coverage.cobertura.xml").write_text("<coverage />")
if mode == "missing-attachment": attachment.unlink()
if mode == "only-attachment": original.unlink()
href = "../../../outside/coverage.cobertura.xml" if mode == "escape" else "agent/coverage.cobertura.xml"
if mode == "symlink":
    original.unlink()
    original.symlink_to(root.parent / "outside" / "coverage.cobertura.xml")
collector = f'<CollectorDataEntries><Collector uri="datacollector://microsoft/CoverletCodeCoverage/1.0"><UriAttachments><UriAttachment><A href="{href}" /></UriAttachment></UriAttachments></Collector></CollectorDataEntries>'
deployment = '<TestSettings><Deployment runDeploymentRoot="_fake_run" /></TestSettings>'
failed = 1 if mode == "failed" else 0
rows = '<UnitTestResult testName="works" outcome="Passed" />' * passed
rows += '<UnitTestResult testName="ignored" outcome="NotExecuted" />' * skipped
rows += '<UnitTestResult testName="broken" outcome="Failed" />' * failed
executed = passed + failed + (1 if mode == "inconsistent" else 0)
outcome = "Aborted" if mode == "aborted" else "Completed"
(root / "tests.trx").write_text(f'<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">{deployment}<Results>{rows}</Results><ResultSummary outcome="{outcome}"><Counters total="{passed+failed+skipped}" executed="{executed}" passed="{passed}" failed="{failed}" notExecuted="{skipped}" />{collector}</ResultSummary></TestRun>')
PY
chmod +x "$scratch/bin/dotnet"
export FAKE_CALLS="$scratch/calls" BUKIT_COVERAGE_SETTINGS=fake.runsettings
for mode in good skipped only-attachment different extra missing-attachment escape symlink exit missing malformed zero failed aborted inconsistent; do
  export FAKE_MODE="$mode" FAKE_FILTER='FullyQualifiedName~CompleteClass'
  [[ "$mode" != good ]] || FAKE_FILTER=''
  mkdir -p "$scratch/results/Fake.Tests" "$scratch/results/sibling" "$scratch/results/outside"
  printf '<coverage />' > "$scratch/results/outside/coverage.cobertura.xml"
  touch "$scratch/results/Fake.Tests/stale.txt" "$scratch/results/sibling/keep.txt"
  : > "$FAKE_CALLS"
  status=0
  PATH="$scratch/bin:$PATH" bash scripts/checks/coverage/run-one.sh Release tests/Fake.Tests/Fake.Tests.csproj "$scratch/results" "$FAKE_FILTER" > "$scratch/output" 2>&1 || status=$?
  [[ "$(wc -l < "$FAKE_CALLS" | tr -d ' ')" == 1 ]] || { cat "$scratch/output"; exit 1; }
  [[ -f "$scratch/results/sibling/keep.txt" ]]
  case "$mode" in
    good|skipped|only-attachment)
      [[ "$status" == 0 ]] || { cat "$scratch/output"; exit 1; }
      reports="$(bash scripts/checks/coverage/find-results.sh "$scratch/results/Fake.Tests" 1)"
      [[ "$reports" == "$scratch/results/Fake.Tests/_fake_run/In/agent/coverage.cobertura.xml" ]]
      [[ -f "$scratch/results/Fake.Tests/tests.trx" ]] ;;
    exit) [[ "$status" == 23 && -f "$scratch/results/Fake.Tests/diagnostic.txt" ]] ;;
    *) [[ "$status" == 1 ]] || { cat "$scratch/output"; exit 1; } ;;
  esac
  case "$mode" in
    different|extra|missing-attachment|escape|symlink)
      grep -Fq 'Invalid coverage TRX:' "$scratch/output"
      [[ -e "$scratch/results/Fake.Tests/12345678-1234-1234-1234-123456789abc/coverage.cobertura.xml" ]] ;;
  esac
  [[ "$(cat "$scratch/results/outside/coverage.cobertura.xml")" == '<coverage />' ]]
  [[ "$mode" != skipped ]] || grep -Fq 'TRX skipped: ignored' "$scratch/output"
done
echo 'coverage run-one self-test OK'

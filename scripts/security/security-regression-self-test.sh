#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
script="$root/scripts/security/security-regression.sh"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-security-self-test.XXXXXX")"
output="$scratch/output.log"
trap 'rm -rf "$scratch"' EXIT

fail() {
  echo "security regression self-test failed: $*" >&2
  exit 1
}

mkdir -p "$scratch/bin"
cat >"$scratch/bin/dotnet" <<'FAKE_DOTNET'
#!/usr/bin/env bash
set -euo pipefail

filter=""
logger=""
results_directory=""
while (($#)); do
  case "$1" in
    --filter)
      filter="${2:-}"
      shift 2
      ;;
    --logger)
      logger="${2:-}"
      shift 2
      ;;
    --results-directory)
      results_directory="${2:-}"
      shift 2
      ;;
    *)
      shift
      ;;
  esac
done

[[ -n "$logger" && -n "$results_directory" ]] || exit 0
[[ "${FAKE_TRX_MODE:-valid}" != "missing" ]] || exit 0

trx_name="${logger#*LogFileName=}"
trx="$results_directory/$trx_name"
mkdir -p "$results_directory"
IFS='|' read -r -a selectors <<<"$filter"
mode="${FAKE_TRX_MODE:-valid}"
if [[ "$mode" == "missing-selector" ]]; then
  selectors=("${selectors[@]:1}")
fi

total="${#selectors[@]}"
executed="$total"
passed="$total"
failed=0
not_executed=0
if [[ "$mode" == "zero" ]]; then
  total=0
  executed=0
  passed=0
elif [[ "$mode" == "failed" ]]; then
  failed=1
  passed=$((total - 1))
fi

{
  printf '<TestRun><TestDefinitions>\n'
  index=0
  for selector in "${selectors[@]}"; do
    name="${selector#FullyQualifiedName~}"
    printf '<UnitTest id="id-%s"><TestMethod className="%s" name="Case" /></UnitTest>\n' \
      "$index" "$name"
    index=$((index + 1))
  done
  printf '</TestDefinitions><Results>\n'
  index=0
  for selector in "${selectors[@]}"; do
    name="${selector#FullyQualifiedName~}"
    outcome="Passed"
    if [[ "$mode" == "failed" && "$index" == 0 ]]; then
      outcome="Failed"
    fi
    printf '<UnitTestResult testId="id-%s" testName="%s.Case" outcome="%s" />\n' \
      "$index" "$name" "$outcome"
    index=$((index + 1))
  done
  printf '</Results><ResultSummary><Counters total="%s" executed="%s" passed="%s" failed="%s" notExecuted="%s" /></ResultSummary></TestRun>\n' \
    "$total" "$executed" "$passed" "$failed" "$not_executed"
} >"$trx"
FAKE_DOTNET
chmod +x "$scratch/bin/dotnet"

FAKE_TRX_MODE=valid "$scratch/bin/dotnet" test fake.csproj \
  --filter "FullyQualifiedName~Suite.Case" \
  --logger "trx;LogFileName=malformed-selector.trx" \
  --results-directory "$scratch"
if python3 "$root/scripts/security/verify-trx.py" \
  "$scratch/malformed-selector.trx" Case >"$output" 2>&1; then
  fail "malformed selector unexpectedly passed"
else
  status=$?
fi
[[ "$status" == 2 ]] || fail "malformed selector returned $status instead of 2"
if ! grep -Fq "malformed security selector: Case" "$output"; then
  fail "malformed selector diagnostic is missing"
fi
if ! grep -Fq "usage: verify-trx.py" "$output"; then
  fail "malformed selector usage is missing"
fi

FAKE_TRX_MODE=valid PATH="$scratch/bin:$PATH" \
  BUKIT_SECURITY_SKIP_RESTORE=1 bash "$script" Release
for mode in zero missing-selector missing failed; do
  if FAKE_TRX_MODE="$mode" PATH="$scratch/bin:$PATH" \
    BUKIT_SECURITY_SKIP_RESTORE=1 bash "$script" Release >"$output" 2>&1; then
    fail "$mode unexpectedly passed"
  fi
done

echo "security regression self-test OK"

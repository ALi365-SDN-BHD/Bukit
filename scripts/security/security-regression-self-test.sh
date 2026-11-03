#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
script="$root/scripts/security/security-regression.sh"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-security-self-test.XXXXXX")"
scratch="$(cd "$scratch" && pwd -P)"
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
project=""
if [[ "${1:-}" == "test" ]]; then
  project="${2:-}"
  shift 2
fi
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
case "$project" in
  *Bukit.Content.Tests.csproj)
    owner="ImageAssetLocalizerTests"
    ;;
  *Bukit.Notion.Tests.csproj)
    owner="BlockRendererUrlSafetyTests"
    ;;
  *)
    owner=""
    ;;
esac
if [[ -n "$owner" ]]; then
  owned_selectors=()
  for selector in "${selectors[@]}"; do
    [[ "$selector" == "FullyQualifiedName~$owner" ]] && owned_selectors+=("$selector")
  done
  selectors=("${owned_selectors[@]}")
fi
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
[[ -z "${FAKE_RESULTS_LOG:-}" ]] || printf '%s\n' "$results_directory" >> "$FAKE_RESULTS_LOG"
[[ "$mode" != "process-failure" ]] || exit 7
FAKE_DOTNET
chmod +x "$scratch/bin/dotnet"

# Each selector must name a test declaration that exists in its own project, so
# a moved or deleted test class fails here instead of only in the Release
# security job. FullyQualifiedName~ is a substring match, so the declaration
# only has to contain the selector text.
all_entries="$(grep -oE '"tests/[^"|]+\.csproj\|[^"]*"' "$script" | tr -d '"')"
entries="$(grep -E '^tests/[^|]+\.csproj(\|FullyQualifiedName~[A-Za-z0-9_.]+)+$' <<<"$all_entries" || true)"
[[ -n "$entries" ]] || fail "no security project entries found"
[[ "$(grep -c 'tests/[^" ]*\.csproj' "$script")" == "$(wc -l <<<"$entries" | tr -d ' ')" ]] ||
  fail "security entries in $script were not all extracted; keep one quoted entry per line"
[[ "$all_entries" == "$entries" ]] || fail "security entries not understood by self-test: $(comm -23 <(sort <<<"$all_entries") <(sort <<<"$entries") | head -3)"
while IFS= read -r entry; do
  IFS='|' read -r -a fields <<<"$entry"
  project_dir="$root/$(dirname "${fields[0]}")"
  [[ -f "$root/${fields[0]}" ]] || fail "security project missing: ${fields[0]}"
  for selector in "${fields[@]:1}"; do
    name="${selector#FullyQualifiedName~}"
    name="${name##*.}"
    id='[A-Za-z0-9_]*'"$name"'[A-Za-z0-9_]*'
    # NUL-delimited so checkout paths containing spaces stay intact.
    found=0
    while IFS= read -r -d '' decl_file; do
      if grep -qE '\[(Fact|Theory)' "$decl_file"; then found=1; break; fi
    done < <(grep -rlE --null --include='*.cs' --exclude-dir=bin --exclude-dir=obj -- \
      "(class|record|struct)[[:space:]]+$id|(public|internal)[[:space:]]+(async[[:space:]]+)?(Task|void)[[:space:]]+$id[[:space:]]*\(" "$project_dir" || true)
    [[ "$found" == 1 ]] ||
      fail "selector $name not found as a test declaration in ${fields[0]}"
  done
done <<<"$entries"

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

for mode in failed process-failure; do
  if FAKE_TRX_MODE="$mode" PATH="$scratch/bin:$PATH" \
    BUKIT_SECURITY_RESULTS="$scratch/retained" bash "$script" Release >"$output" 2>&1; then
    fail "$mode unexpectedly passed"
  fi
  [[ -n "$(find "$scratch/retained" -name '*.trx' -print -quit)" ]] || fail "failure TRX not retained"
done
printf 'keep' > "$scratch/retained/sentinel"
FAKE_TRX_MODE=valid PATH="$scratch/bin:$PATH" \
  BUKIT_SECURITY_RESULTS="$scratch/retained" bash "$script" Release >"$output" 2>&1
[[ "$(cat "$scratch/retained/sentinel")" == keep ]] || fail "unrelated output changed"
if FAKE_TRX_MODE=process-failure FAKE_RESULTS_LOG="$scratch/results-paths" PATH="$scratch/bin:$PATH" \
  BUKIT_SECURITY_RESULTS= bash "$script" Release >"$output" 2>&1; then fail "process failure passed"; fi
while IFS= read -r path; do [[ ! -e "$path" ]] || fail "default temp directory retained"; done < "$scratch/results-paths"
ln -s "$scratch/retained" "$scratch/link"
for unsafe in / . ../outside "$scratch/link"; do
  if PATH="$scratch/bin:$PATH" BUKIT_SECURITY_RESULTS="$unsafe" bash "$script" Release >"$output" 2>&1; then
    fail "unsafe output accepted: $unsafe"
  fi
done
echo "security regression self-test OK"

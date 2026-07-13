#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
target="$repo_root/.trae/skills/systematic-debugging/find-polluter.sh"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-find-polluter-self-test.XXXXXX")"
trap 'rm -rf "$scratch"' EXIT

failures=0
record_failure() {
  echo "find-polluter self-test failed: $*" >&2
  failures=$((failures + 1))
}

fake_bin="$scratch/bin"
mkdir -p "$fake_bin"
printf '%s\n' \
  '#!/usr/bin/env bash' \
  'set -euo pipefail' \
  'if [[ $# -ne 3 || "$1" != "test" || "$2" != "--" || "$3" != "$EXPECTED_TEST_FILE" ]]; then' \
  '  printf "unexpected npm arguments:" >&2' \
  '  printf " <%q>" "$@" >&2' \
  '  printf "\n" >&2' \
  '  exit 97' \
  'fi' \
  'printf "%s\0" "$3" >> "$NPM_CALL_LOG"' \
  'case "$NPM_MODE" in' \
  '  pass) exit 0 ;;' \
  '  fail) exit 9 ;;' \
  '  pollute) touch "$POLLUTION_PATH"; exit 0 ;;' \
  '  fail-pollute) touch "$POLLUTION_PATH"; exit 9 ;;' \
  '  *) echo "unknown fake npm mode: $NPM_MODE" >&2; exit 98 ;;' \
  'esac' \
  > "$fake_bin/npm"
chmod +x "$fake_bin/npm"

prepare_case() {
  local label="$1" test_file="${2:-}"
  case_dir="$scratch/cases/$label"
  mkdir -p "$case_dir/tests"
  if [[ -n "$test_file" ]]; then
    mkdir -p "$(dirname "$case_dir/$test_file")"
    touch "$case_dir/$test_file"
  fi
}

run_case() {
  local mode="$1" expected_test_file="$2" pattern="$3" path_prefix="${4:-$fake_bin}"
  case_calls="$case_dir/npm.calls"
  case_stdout="$case_dir/stdout"
  case_stderr="$case_dir/stderr"
  set +e
  (
    cd "$case_dir"
    PATH="$path_prefix:$fake_bin:/usr/bin:/bin" \
      NPM_CALL_LOG="$case_calls" EXPECTED_TEST_FILE="$expected_test_file" \
      NPM_MODE="$mode" POLLUTION_PATH=".pollution" \
      bash "$target" ".pollution" "$pattern"
  ) >"$case_stdout" 2>"$case_stderr"
  case_status=$?
  set -e
}

assert_status() {
  local expected="$1" label="$2"
  [[ "$case_status" -eq "$expected" ]] ||
    record_failure "$label returned $case_status, expected $expected"
}

assert_contains() {
  local file="$1" expected="$2" label="$3"
  grep -Fq -- "$expected" "$file" ||
    record_failure "$label did not include path/message: $expected"
}

assert_no_calls() {
  local label="$1"
  [[ ! -s "$case_calls" ]] || record_failure "$label invoked npm"
}

assert_one_exact_call() {
  local expected="$1" label="$2" actual extra
  if [[ ! -s "$case_calls" ]]; then
    record_failure "$label did not invoke npm"
    return
  fi
  exec 3< "$case_calls"
  if ! IFS= read -r -d '' actual <&3; then
    exec 3<&-
    record_failure "$label did not record a NUL-terminated test path"
    return
  fi
  [[ "$actual" == "$expected" ]] ||
    record_failure "$label passed a different third argument"
  if IFS= read -r -d '' extra <&3; then
    record_failure "$label invoked npm more than once"
  fi
  exec 3<&-
}

space_path='./tests/with space.test.ts'
prepare_case space-clean 'tests/with space.test.ts'
run_case pass "$space_path" './tests/*.test.ts'
assert_status 0 "space-path clean case"
assert_one_exact_call "$space_path" "space-path clean case"

polluter_path='./tests/polluter.test.ts'
prepare_case polluter 'tests/polluter.test.ts'
run_case pollute "$polluter_path" './tests/*.test.ts'
assert_status 1 "polluter case"
assert_contains "$case_stdout" "FOUND POLLUTER: $polluter_path" "polluter case"
assert_contains "$case_stdout" "Command status: 0" "polluter case"
[[ -e "$case_dir/.pollution" ]] || record_failure "polluter case removed its evidence"

prepare_case failed-polluter 'tests/polluter.test.ts'
run_case fail-pollute "$polluter_path" './tests/*.test.ts'
assert_status 1 "failed polluter case"
assert_contains "$case_stdout" "FOUND POLLUTER: $polluter_path" "failed polluter case"
assert_contains "$case_stdout" "Command status: 9" "failed polluter case"
[[ -e "$case_dir/.pollution" ]] || record_failure "failed polluter case removed its evidence"

failed_path='./tests/failed only.test.ts'
prepare_case failed-only 'tests/failed only.test.ts'
run_case fail "$failed_path" './tests/*.test.ts'
assert_status 2 "failed-only case"
assert_contains "$case_stderr" "Test failed without pollution: $failed_path" "failed-only case"

prepare_case zero
run_case pass './tests/unused.test.ts' './tests/*.test.ts'
assert_status 2 "zero-match case"
assert_contains "$case_stderr" "No tests matched" "zero-match case"
assert_no_calls "zero-match case"

prepare_case pre-existing-directory 'tests/clean.test.ts'
mkdir "$case_dir/.pollution"
run_case pass './tests/clean.test.ts' './tests/*.test.ts'
assert_status 2 "pre-existing directory case"
assert_contains "$case_stderr" "Pollution already exists" "pre-existing directory case"
assert_no_calls "pre-existing directory case"
[[ -d "$case_dir/.pollution" ]] || record_failure "pre-existing directory was removed"

prepare_case pre-existing-dangling 'tests/clean.test.ts'
ln -s missing-target "$case_dir/.pollution"
run_case pass './tests/clean.test.ts' './tests/*.test.ts'
assert_status 2 "pre-existing dangling symlink case"
assert_no_calls "pre-existing dangling symlink case"
[[ -L "$case_dir/.pollution" ]] || record_failure "pre-existing dangling symlink was removed"

newline_path=$'./tests/with\nnewline.test.ts'
prepare_case newline $'tests/with\nnewline.test.ts'
run_case pass "$newline_path" './tests/*.test.ts'
assert_status 0 "newline-path clean case"
assert_one_exact_call "$newline_path" "newline-path clean case"

failing_find_bin="$scratch/failing-find-bin"
mkdir -p "$failing_find_bin"
printf '%s\n' '#!/usr/bin/env bash' 'exit 19' > "$failing_find_bin/find"
chmod +x "$failing_find_bin/find"
prepare_case discovery-failure 'tests/clean.test.ts'
run_case pass './tests/clean.test.ts' './tests/*.test.ts' "$failing_find_bin"
assert_status 2 "discovery failure case"
assert_contains "$case_stderr" "Test discovery failed" "discovery failure case"
assert_no_calls "discovery failure case"

if [[ "$failures" -gt 0 ]]; then
  echo "find-polluter self-test: $failures failure(s)" >&2
  exit 1
fi
echo "find-polluter self-test: PASS"

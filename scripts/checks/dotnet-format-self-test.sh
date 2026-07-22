#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

fail() {
  echo "dotnet format self-test failed: $*" >&2
  exit 1
}

wrapper="scripts/checks/dotnet-format.sh"
[[ -f "$wrapper" ]] || fail "format wrapper is missing"

scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-dotnet-format-self-test.XXXXXX")"
trap 'rm -rf -- "$scratch"' EXIT
mkdir -p "$scratch/bin"

cat >"$scratch/bin/dotnet" <<'SH'
#!/usr/bin/env bash
printf '%s\n' "$@" >"$BUKIT_FORMAT_ARGS"
exit "${BUKIT_FAKE_DOTNET_STATUS:-0}"
SH
chmod +x "$scratch/bin/dotnet"

args_file="$scratch/args.txt"
BUKIT_FORMAT_ARGS="$args_file" PATH="$scratch/bin:$PATH" bash "$wrapper" ||
  fail "wrapper rejected a successful formatter run"

expected_args="$scratch/expected-args.txt"
printf '%s\n' \
  format \
  bukit-core.slnx \
  --verify-no-changes \
  --no-restore \
  >"$expected_args"
cmp -s "$expected_args" "$args_file" || fail "wrapper command differs from the repository format contract"

rm -f "$args_file"
status=0
BUKIT_FORMAT_ARGS="$args_file" PATH="$scratch/bin:$PATH" bash "$wrapper" unexpected >"$scratch/extra.txt" 2>&1 || status=$?
[[ "$status" == "2" ]] || fail "wrapper did not reject extra arguments with exit 2"
[[ ! -e "$args_file" ]] || fail "wrapper invoked dotnet after rejecting extra arguments"

status=0
BUKIT_FORMAT_ARGS="$args_file" BUKIT_FAKE_DOTNET_STATUS=37 PATH="$scratch/bin:$PATH" bash "$wrapper" || status=$?
[[ "$status" == "37" ]] || fail "wrapper did not preserve the formatter exit status"

expected_self_test='run_step "dotnet format self-test" bash scripts/checks/dotnet-format-self-test.sh'
expected_gate='run_step "dotnet format" bash scripts/checks/dotnet-format.sh'
[[ "$(grep -Fxc "$expected_self_test" scripts/gates/ci-fast.sh)" == "1" ]] ||
  fail "ci-fast self-test wiring is missing or duplicated"
[[ "$(grep -Fxc "$expected_gate" scripts/gates/ci-fast.sh)" == "1" ]] ||
  fail "ci-fast real-check wiring is missing or duplicated"

grep -Fq 'bash scripts/checks/dotnet-format.sh' .github/PULL_REQUEST_TEMPLATE.md ||
  fail "pull request checklist does not use the repository wrapper"
grep -Fq 'bash scripts/checks/dotnet-format.sh' guide/dev/testing.md ||
  fail "testing guide does not document the repository wrapper"

echo "dotnet format self-test OK"

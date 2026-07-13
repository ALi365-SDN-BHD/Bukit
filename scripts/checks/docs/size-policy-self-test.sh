#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../../lib/common.sh"
cd "$(repo_root)"

probe="scripts/.size-policy-self-test.$$.py"
output="$(mktemp "${TMPDIR:-/tmp}/bukit-size-policy-self-test.XXXXXX")"
probe_owned=0

cleanup() {
  if ((probe_owned == 1)); then
    rm -f "$probe"
  fi
  rm -f "$output"
}

on_signal() {
  local signal="$1"
  cleanup
  trap - "$signal"
  kill -s "$signal" "$$"
}

trap cleanup EXIT
trap 'on_signal HUP' HUP
trap 'on_signal INT' INT
trap 'on_signal TERM' TERM

fail() {
  echo "size policy self-test failed: $*" >&2
  exit 1
}

if [[ -e "$probe" ]]; then
  fail "probe path already exists: $probe"
fi
if ! (set -o noclobber; : > "$probe") 2>/dev/null; then
  fail "could not claim probe path: $probe"
fi
probe_owned=1

i=0
while ((i < 201)); do
  printf 'pass\n'
  i=$((i + 1))
done > "$probe"

if bash scripts/checks/docs/size-policy.sh >"$output" 2>&1; then
  fail "oversized Python script unexpectedly passed"
fi

if ! grep -Fq "$probe has 201 lines; limit is 200" "$output"; then
  cat "$output" >&2
  fail "oversized Python probe was not reported"
fi

echo "size policy self-test OK"

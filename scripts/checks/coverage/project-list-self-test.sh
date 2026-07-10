#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
tmp_root="$(mktemp -d)"
temp_parent="${TMPDIR:-/tmp}"
coverage_root_base="$(mktemp -d "${temp_parent%/}/bukit-coverage-project-list.XXXXXX")"
trap 'rm -rf "$tmp_root" "$coverage_root_base"' EXIT
cd "$repo_root"
trace="${tmp_root}/list-trace"

fake_bash="${tmp_root}/bash"
printf '%s\n' '#!/bin/bash' \
  'if [[ "${1:-}" == "scripts/checks/coverage/list-core-projects.sh" ]]; then' \
  '  printf "%s:%s\n" "${BUKIT_FAKE_LIST_CALL:-unknown}" "${BUKIT_FAKE_LIST_MODE:-fail}" >> "${BUKIT_FAKE_LIST_TRACE:?}"' \
  '  case "${BUKIT_FAKE_LIST_MODE:-fail}" in' \
  '    fail) exit 7 ;;' \
  '    empty) exit 0 ;;' \
  '    blank) printf "\n"; exit 0 ;;' \
  '    invalid) printf "tests/NoSuch.Tests/NoSuch.Tests.csproj\t\n"; exit 0 ;;' \
  '    source) printf "tests/Bukit.Shared.Tests/EnvironmentHelperTests.cs\t\n"; exit 0 ;;' \
  '    columns) printf "tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj\tFilter\textra\n"; exit 0 ;;' \
  '  esac' \
  'fi' \
  'exec /bin/bash "$@"' > "$fake_bash"
chmod +x "$fake_bash"

expect_core_reject() {
  local mode="$1"
  if BUKIT_FAKE_LIST_CALL=core BUKIT_FAKE_LIST_TRACE="$trace" \
    BUKIT_FAKE_LIST_MODE="$mode" PATH="${tmp_root}:$PATH" \
    /bin/bash scripts/checks/core-tests.sh Release; then
    echo "ERROR: core tests accepted ${mode} project list" >&2
    exit 1
  fi
}

expect_coverage_reject() {
  local mode="$1"
  local coverage_root="${coverage_root_base}/${mode}"
  mkdir -p "$coverage_root"
  touch "${coverage_root}/marker"
  if BUKIT_FAKE_LIST_CALL=coverage BUKIT_FAKE_LIST_TRACE="$trace" \
    BUKIT_FAKE_LIST_MODE="$mode" BUKIT_COVERAGE_ROOT="$coverage_root" \
    PATH="${tmp_root}:$PATH" /bin/bash scripts/checks/coverage.sh Release >/dev/null 2>&1; then
    echo "ERROR: coverage accepted ${mode} project list" >&2
    exit 1
  fi
  if ! grep -Fxq "coverage:${mode}" "$trace"; then
    echo "ERROR: coverage did not invoke ${mode} project list" >&2
    exit 1
  fi
  if [[ ! -f "${coverage_root}/marker" ]]; then
    echo "ERROR: coverage cleaned output for ${mode} project list" >&2
    exit 1
  fi
}

for mode in fail empty blank invalid source columns; do
  expect_core_reject "$mode"
  expect_coverage_reject "$mode"
done

echo "coverage project list self-test OK"

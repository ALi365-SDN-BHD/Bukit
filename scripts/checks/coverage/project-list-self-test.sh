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

python3 - "$tmp_root" <<'PY_SELECTION'
import os, re, subprocess, sys
from pathlib import Path

windows_only = set()
for source in Path("tests/Bukit.Cli.Tests").glob("*.cs"):
    for method in re.findall(r"\[WindowsFact\]\s+public\s+(?:async\s+)?(?:Task|void)\s+(\w+)\(", source.read_text()):
        windows_only.add("Bukit.Cli.Tests." + source.stem + "." + method)
assert len(windows_only) == 5, windows_only
bin_dir = Path(sys.argv[1]) / "platform-bin"
bin_dir.mkdir()
uname = bin_dir / "uname"
uname.write_text('#!/bin/sh\nprintf "%s\\n" "$BUKIT_TEST_UNAME"\n')
uname.chmod(0o755)
for platform in ["Linux", "Darwin", "MINGW64_NT-10.0", "MSYS_NT-10.0", "CYGWIN_NT-10.0"]:
    env = dict(os.environ, PATH=str(bin_dir) + os.pathsep + os.environ["PATH"], BUKIT_TEST_UNAME=platform)
    output = subprocess.check_output(["bash", "scripts/checks/coverage/list-core-projects.sh"], env=env, text=True)
    rows = dict(line.split("\t") for line in output.splitlines())
    assert len(rows) == 13, rows
    cli_filter = rows.pop("tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj")
    assert all(not value for value in rows.values()), rows
    if platform in ("Linux", "Darwin"):
        terms = cli_filter.split("&")
        assert all(term.startswith("FullyQualifiedName!=") for term in terms), (platform, cli_filter)
        assert len(terms) == len(windows_only), (platform, cli_filter)
        assert {term.removeprefix("FullyQualifiedName!=") for term in terms} == windows_only, (platform, cli_filter)
    else:
        assert not cli_filter, (platform, cli_filter)
print("coverage selection: five Windows-only exclusions on Unix; Windows and other projects retained")
PY_SELECTION

echo "coverage project list self-test OK"

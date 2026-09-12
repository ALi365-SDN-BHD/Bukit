#!/usr/bin/env bash
# Sourced by native-aot-self-test.sh after its scratch directory is created.
fake_bin="$scratch/bin"
mkdir -p "$fake_bin"
export BUKIT_TEST_REAL_TAR="$(command -v tar)"
export BUKIT_TEST_REAL_PWSH="$(command -v pwsh || command -v powershell || true)"

cat > "$fake_bin/dotnet" <<'FAKE_DOTNET'
#!/usr/bin/env bash
set -euo pipefail

printf '%s\n' "$@" > "${FAKE_DOTNET_ARGS:?}"
output=""
rid=""
while [[ $# -gt 0 ]]; do
  if [[ "$1" == "-o" ]]; then
    output="${2:-}"
    shift 2
  elif [[ "$1" == "-r" ]]; then
    rid="$2"
    shift 2
  else
    shift
  fi
done
[[ -n "$output" ]] || { echo "fake dotnet: missing -o" >&2; exit 71; }
if [[ "${FAKE_DOTNET_FAIL:-0}" == "1" ]]; then
  echo "fake dotnet: injected publish failure" >&2
  exit 73
fi
mkdir -p "$output"
if [[ "${FAKE_DOTNET_EMPTY:-0}" != "1" ]]; then
  exe="$output/bukit"
  [[ "$rid" != win-* ]] || exe="$exe.exe"
  case "${FAKE_CLI_STATE:-valid}" in
    missing) printf 'resource\n' > "$output/resource.txt" ;;
    empty) touch "$exe" ;;
    nonexec) printf 'native\n' > "$exe" ;;
    valid) printf 'native\n' > "$exe"; chmod +x "$exe" ;;
  esac
  printf 'debug\n' > "$output/bukit.pdb"
  printf 'debug\n' > "$output/bukit.dbg"
  mkdir -p "$output/bukit.dSYM/Contents/Resources/DWARF"
  printf 'debug\n' > "$output/bukit.dSYM/Contents/Resources/DWARF/bukit"
fi
printf 'fake dotnet publish log\n'
FAKE_DOTNET

cat > "$fake_bin/pwsh" <<'FAKE_PWSH'
#!/usr/bin/env bash
set -euo pipefail

case "$*" in
  *"${BUKIT_EXPECTED_ARCHIVE:?}"*) exit 91 ;;
esac
pending="${BUKIT_ARCHIVE_PATH:?}"
if command -v cygpath >/dev/null 2>&1; then
  pending="$(cygpath -u "$pending")"
fi
[[ "$pending" == "${BUKIT_EXPECTED_ARCHIVE%/*}"/.bukit-build-win-x64.*/"${BUKIT_EXPECTED_ARCHIVE##*/}" ]] || {
  echo "fake pwsh: archive environment mismatch" >&2
  exit 92
}
if [[ "${FAKE_PWSH_SKIP_WRITE:-0}" != "1" ]]; then
  if [[ "${FAKE_ARCHIVE_FAIL:-0}" == "1" ]]; then
    printf 'partial zip\n' > "$pending"
    echo 'injected archive failure' >&2
    exit 74
  fi
  if [[ -n "$BUKIT_TEST_REAL_PWSH" ]]; then
    "$BUKIT_TEST_REAL_PWSH" "$@"
  else
    zip -qr "$pending" .
  fi
fi
FAKE_PWSH

cat > "$fake_bin/tar" <<'FAKE_TAR'
#!/usr/bin/env bash
set -euo pipefail
if [[ "${FAKE_ARCHIVE_FAIL:-0}" == "1" ]]; then
  printf 'partial tar\n' > "$4"
  echo 'injected archive failure' >&2
  exit 74
fi
exec "$BUKIT_TEST_REAL_TAR" "$@"
FAKE_TAR

chmod +x "$fake_bin/dotnet" "$fake_bin/pwsh" "$fake_bin/tar"
export PATH="$fake_bin:$PATH"
export FAKE_DOTNET_ARGS="$scratch/dotnet.args"

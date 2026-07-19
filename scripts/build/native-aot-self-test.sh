#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../.."

scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-native-aot-self-test.XXXXXX")"
trap 'rm -rf -- "$scratch"' EXIT

fake_bin="$scratch/bin"
mkdir -p "$fake_bin"

cat > "$fake_bin/dotnet" <<'FAKE_DOTNET'
#!/usr/bin/env bash
set -euo pipefail

printf '%s\n' "$@" > "${FAKE_DOTNET_ARGS:?}"
output=""
while [[ $# -gt 0 ]]; do
  if [[ "$1" == "-o" ]]; then
    output="${2:-}"
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
  printf 'native\n' > "$output/bukit"
fi
printf 'fake dotnet publish log\n'
FAKE_DOTNET

cat > "$fake_bin/pwsh" <<'FAKE_PWSH'
#!/usr/bin/env bash
set -euo pipefail

case "$*" in
  *"${BUKIT_EXPECTED_ARCHIVE:?}"*) exit 91 ;;
esac
[[ "${BUKIT_ARCHIVE_PATH:-}" == "$BUKIT_EXPECTED_ARCHIVE" ]] || {
  echo "fake pwsh: archive environment mismatch" >&2
  exit 92
}
if [[ "${FAKE_PWSH_SKIP_WRITE:-0}" != "1" ]]; then
  printf 'zip\n' > "$BUKIT_ARCHIVE_PATH"
fi
FAKE_PWSH

chmod +x "$fake_bin/dotnet" "$fake_bin/pwsh"
export PATH="$fake_bin:$PATH"
export FAKE_DOTNET_ARGS="$scratch/dotnet.args"

output_root="$scratch/it's-safe"
stale="$output_root/publish/linux-x64/stale.txt"
mkdir -p "$(dirname "$stale")"
printf 'stale\n' > "$stale"

archive="$(bash scripts/build/package-native-aot.sh 1.2.3 linux-x64 "$output_root" Release)"
[[ ! -e "$stale" ]] || {
  echo "native-aot self-test: stale publish entry survived" >&2
  exit 1
}
[[ -s "$archive" ]] || {
  echo "native-aot self-test: non-Windows archive is empty" >&2
  exit 1
}

grep -Fx -- '-p:ContinuousIntegrationBuild=true' "$FAKE_DOTNET_ARGS" >/dev/null
grep -Fx -- '-p:Deterministic=true' "$FAKE_DOTNET_ARGS" >/dev/null
grep -Fx -- '-p:NativeDebugSymbols=false' "$FAKE_DOTNET_ARGS" >/dev/null
artifacts_root="$(sed -n '/^--artifacts-path$/{n;p;}' "$FAKE_DOTNET_ARGS")"
canonical_output_root="$(cd "$output_root" && pwd -P)"
[[ "$artifacts_root" == "$canonical_output_root"/.bukit-build-linux-x64.* ]] || {
  echo "native-aot self-test: build artifacts are not isolated under output root" >&2
  exit 1
}
grep -Fx -- "-p:PathMap=$(pwd -P)=/_/src%2C$artifacts_root=/_/build" \
  "$FAKE_DOTNET_ARGS" >/dev/null
[[ ! -e "$artifacts_root" ]] || {
  echo "native-aot self-test: isolated build artifacts survived packaging" >&2
  exit 1
}

publish_fail_root="$scratch/publish-fail"
publish_fail_archive="$publish_fail_root/bukit-1.2.3-linux-x64.tar.gz"
mkdir -p "$publish_fail_root"
printf 'stale archive\n' > "$publish_fail_archive"
if FAKE_DOTNET_FAIL=1 bash scripts/build/package-native-aot.sh \
  1.2.3 linux-x64 "$publish_fail_root" Release \
  >"$scratch/publish-fail.stdout" 2>"$scratch/publish-fail.stderr"; then
  echo "native-aot self-test: injected publish failure unexpectedly succeeded" >&2
  exit 1
fi
[[ ! -e "$publish_fail_archive" ]] || {
  echo "native-aot self-test: old archive survived publish failure" >&2
  exit 1
}

if FAKE_DOTNET_EMPTY=1 bash scripts/build/package-native-aot.sh \
  1.2.3 linux-x64 "$scratch/empty-publish" Release \
  >"$scratch/empty-publish.stdout" 2>"$scratch/empty-publish.stderr"; then
  echo "native-aot self-test: empty publish directory unexpectedly succeeded" >&2
  exit 1
fi
grep -F 'publish directory is empty:' "$scratch/empty-publish.stderr" >/dev/null

invalid_root="$scratch/invalid-rid"
invalid_marker="$invalid_root/publish/not-a-rid/keep.txt"
mkdir -p "$(dirname "$invalid_marker")"
printf 'keep\n' > "$invalid_marker"
if bash scripts/build/package-native-aot.sh 1.2.3 not-a-rid "$invalid_root" Release \
  >"$scratch/invalid.stdout" 2>"$scratch/invalid.stderr"; then
  echo "native-aot self-test: unsupported RID unexpectedly succeeded" >&2
  exit 1
fi
[[ -f "$invalid_marker" ]] || {
  echo "native-aot self-test: unsupported RID reached destructive cleanup" >&2
  exit 1
}

symlink_root="$scratch/symlink-root"
escaped_root="$scratch/escaped-root"
mkdir -p "$symlink_root" "$escaped_root"
ln -s "$escaped_root" "$symlink_root/publish"
if bash scripts/build/package-native-aot.sh 1.2.3 linux-x64 "$symlink_root" Release \
  >"$scratch/symlink.stdout" 2>"$scratch/symlink.stderr"; then
  echo "native-aot self-test: symlink publish root unexpectedly succeeded" >&2
  exit 1
fi
[[ ! -e "$escaped_root/linux-x64" ]] || {
  echo "native-aot self-test: publish root escaped output root" >&2
  exit 1
}

windows_root="$scratch/windows-it's-safe"
export BUKIT_EXPECTED_ARCHIVE="$(cd "$scratch" && pwd -P)/windows-it's-safe/bukit-1.2.3-win-x64.zip"
mkdir -p "$windows_root"
printf 'stale zip\n' > "$BUKIT_EXPECTED_ARCHIVE"
if FAKE_PWSH_SKIP_WRITE=1 bash scripts/build/package-native-aot.sh \
  1.2.3 win-x64 "$windows_root" Release \
  >"$scratch/empty-archive.stdout" 2>"$scratch/empty-archive.stderr"; then
  echo "native-aot self-test: stale archive unexpectedly survived packaging" >&2
  exit 1
fi
grep -F 'archive is empty:' "$scratch/empty-archive.stderr" >/dev/null
[[ ! -e "$BUKIT_EXPECTED_ARCHIVE" ]] || {
  echo "native-aot self-test: old archive was not removed" >&2
  exit 1
}
windows_archive="$(bash scripts/build/package-native-aot.sh 1.2.3 win-x64 "$windows_root" Release)"
[[ "$windows_archive" == "$BUKIT_EXPECTED_ARCHIVE" ]] || {
  echo "native-aot self-test: unexpected Windows archive path: $windows_archive" >&2
  exit 1
}
[[ -s "$windows_archive" ]] || {
  echo "native-aot self-test: Windows archive is empty" >&2
  exit 1
}

if bash scripts/build/native-aot.sh 1.2.3 linux-x64 \
  >"$scratch/missing.stdout" 2>"$scratch/missing.stderr"; then
  echo "native-aot self-test: missing output argument unexpectedly succeeded" >&2
  exit 1
fi

wrapper_root="$scratch/wrapper"
wrapper_archive="$(bash scripts/build/native-aot.sh 1.2.3 linux-x64 "$wrapper_root" Release)"
[[ -s "$wrapper_archive" ]] || {
  echo "native-aot self-test: native-aot.sh did not produce a non-empty archive" >&2
  exit 1
}

github_output="$scratch/github-output"
GITHUB_OUTPUT="$github_output" bash scripts/build/package-native-aot.sh \
  1.2.3 linux-x64 "$scratch/github" Release >"$scratch/github.archive"
expected_github_archive="$(cat "$scratch/github.archive")"
expected_github_publish="$(cd "$scratch/github/publish/linux-x64" && pwd -P)"
grep -Fx "archive=$expected_github_archive" "$github_output" >/dev/null
grep -Fx "publish_dir=$expected_github_publish" "$github_output" >/dev/null

echo "native-aot self-test: PASS"

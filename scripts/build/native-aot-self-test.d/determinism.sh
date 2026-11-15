#!/usr/bin/env bash
# Sourced by native-aot-self-test.sh after its first linux-x64 package, with
# artifacts_root set to that run's build root.

grep -Fx -- "-p:PathMap=$(pwd -P)=/_/src%2C$artifacts_root=/_/build" \
  "$FAKE_DOTNET_ARGS" >/dev/null
if grep -F -- '-p:CustomAfterMicrosoftCommonTargets=' "$FAKE_DOTNET_ARGS" >/dev/null; then
  echo "native-aot self-test: linux packaging passed a linker hook it does not need" >&2
  exit 1
fi

osx_archive="$(bash scripts/build/package-native-aot.sh 1.2.3 osx-arm64 "$scratch/osx" Release)"
grep -F -- '-p:CustomAfterMicrosoftCommonTargets=' "$FAKE_DOTNET_ARGS" >/dev/null
grep -Fq -- '<ExtraLinkerArg Include="-objc_stubs_small" />' "$FAKE_DOTNET_ARGS.targets"
if grep -Fq -e '-no_uuid' -e 'IlcSingleThreaded' "$FAKE_DOTNET_ARGS.targets" "$FAKE_DOTNET_ARGS"; then
  echo "native-aot self-test: -no_uuid/IlcSingleThreaded only mask the macOS linker race" >&2
  exit 1
fi
[[ -s "$osx_archive" ]] || {
  echo "native-aot self-test: macOS archive is empty" >&2
  exit 1
}

# Windows: the hook is LinkerArg (ILCompiler ignores ExtraLinkerArg there).
windows_hook_root="$scratch/windows-hook"
export BUKIT_EXPECTED_ARCHIVE="$windows_hook_root/bukit-1.2.3-win-x64.zip"
bash scripts/build/package-native-aot.sh 1.2.3 win-x64 "$windows_hook_root" Release >/dev/null
grep -Fq -- '<LinkerArg Include="/Brepro" />' "$FAKE_DOTNET_ARGS.targets"
if grep -Fq -e 'ExtraLinkerArg' -e 'objc_stubs' "$FAKE_DOTNET_ARGS.targets"; then
  echo "native-aot self-test: Windows packaging passed a Unix-only linker hook" >&2
  exit 1
fi

# On a Windows host (Git Bash), MSBuild must get native paths, or PathMap
# matches nothing. The fake cygpath marks a converted path with one extra
# leading slash; the fake uname makes the host look like Git Bash.
cygpath_bin="$scratch/cygpath-bin"
windows_host_bin="$scratch/windows-host-bin"
mkdir -p "$cygpath_bin" "$windows_host_bin"
cat > "$cygpath_bin/cygpath" <<'FAKE_CYGPATH'
#!/usr/bin/env bash
set -euo pipefail
case "$1:$2" in
  -w:*) printf '/%s\n' "$2" ;;
  -u://*) printf '%s\n' "${2#/}" ;;
  -u:*) printf '%s\n' "$2" ;;
  *) echo "fake cygpath: unsupported $1" >&2; exit 93 ;;
esac
FAKE_CYGPATH
real_uname="$(command -v uname)"
cat > "$windows_host_bin/uname" <<FAKE_UNAME
#!/usr/bin/env bash
[[ "\${1:-}" != "-s" ]] || { echo MINGW64_NT-10.0-26100; exit 0; }
exec "$real_uname" "\$@"
FAKE_UNAME
chmod +x "$cygpath_bin/cygpath" "$windows_host_bin/uname"

native_root="$scratch/native-paths"
export BUKIT_EXPECTED_ARCHIVE="$native_root/bukit-1.2.3-win-x64.zip"
PATH="$windows_host_bin:$cygpath_bin:$PATH" bash scripts/build/package-native-aot.sh \
  1.2.3 win-x64 "$native_root" Release >/dev/null
native_artifacts="$(sed -n '/^--artifacts-path$/{n;p;}' "$FAKE_DOTNET_ARGS")"
grep -Fx -- "-p:PathMap=/$(pwd -P)=/_/src%2C/$native_artifacts=/_/build" \
  "$FAKE_DOTNET_ARGS" >/dev/null
grep -Fx -- "-p:CustomAfterMicrosoftCommonTargets=/$native_artifacts/deterministic-linker.targets" \
  "$FAKE_DOTNET_ARGS" >/dev/null

# A cygpath that merely exists on a non-Windows host must not be used.
stray_root="$scratch/stray-cygpath"
export BUKIT_EXPECTED_ARCHIVE="$stray_root/bukit-1.2.3-win-x64.zip"
PATH="$cygpath_bin:$PATH" bash scripts/build/package-native-aot.sh \
  1.2.3 win-x64 "$stray_root" Release >/dev/null
stray_artifacts="$(sed -n '/^--artifacts-path$/{n;p;}' "$FAKE_DOTNET_ARGS")"
grep -Fx -- "-p:PathMap=$(pwd -P)=/_/src%2C$stray_artifacts=/_/build" \
  "$FAKE_DOTNET_ARGS" >/dev/null
grep -Fx -- "-p:CustomAfterMicrosoftCommonTargets=$stray_artifacts/deterministic-linker.targets" \
  "$FAKE_DOTNET_ARGS" >/dev/null
unset BUKIT_EXPECTED_ARCHIVE

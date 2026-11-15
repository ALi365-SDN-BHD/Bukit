#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../.."

if [[ $# -lt 3 || $# -gt 4 ]]; then
  echo "usage: bash scripts/build/package-native-aot.sh <version> <rid> <output-root> [configuration]" >&2
  exit 2
fi

version="$1"
rid="$2"
output_root="$3"
configuration="${4:-Release}"

if [[ ! "$version" =~ ^[A-Za-z0-9._-]+$ ]]; then
  echo "version may contain only letters, numbers, dot, underscore, and dash" >&2
  exit 2
fi

case "$rid" in
  linux-x64|osx-arm64|win-x64) ;;
  *)
    echo "unsupported RID: $rid" >&2
    exit 2
    ;;
esac

commit="${GITHUB_SHA:-$(git rev-parse --short=12 HEAD)}"
archive_base="bukit-$version-$rid"
mkdir -p "$output_root"
output_root="$(cd "$output_root" && pwd -P)"
build_root="$(mktemp -d "$output_root/.bukit-build-$rid.XXXXXX")"
cleanup_build_root() {
  rm -rf -- "$build_root"
}
trap cleanup_build_root EXIT
publish_root="$output_root/publish"
[[ ! -L "$publish_root" ]] || {
  echo "publish root must not be a symlink" >&2
  exit 1
}
mkdir -p "$publish_root"
[[ "$(cd "$publish_root" && pwd -P)" == "$output_root/publish" ]] || {
  echo "publish root escaped output root" >&2
  exit 1
}
publish_dir="$publish_root/$rid"
rm -rf -- "$publish_dir"
mkdir -p "$publish_dir"

if [[ "$rid" == win-* ]]; then
  archive="$output_root/$archive_base.zip"
else
  archive="$output_root/$archive_base.tar.gz"
fi
rm -f -- "$archive"
pending_archive="$build_root/$archive_base.${archive##*.}"

# MSBuild sees native paths. Git Bash on Windows reports /d/a/... for what the
# compiler knows as D:\a\..., and a PathMap in the former form matches nothing,
# so the random build root ends up inside every managed assembly.
# Decide by the host rather than by whether a cygpath happens to be on PATH:
# a stray cygpath on Linux or macOS would turn every path into a Windows one.
# Git Bash, every MSYS2 environment and Cygwin report a kernel name such as
# MINGW64_NT-10.0, UCRT64_NT-10.0, MSYS_NT-10.0 or CYGWIN_NT-10.0.
host_os="$(uname -s)"
native_path() {
  case "$host_os" in
    *_NT-*) cygpath -w "$1" ;;
    *) printf '%s\n' "$1" ;;
  esac
}

publish_args=(
  publish src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj
  -c "$configuration"
  -r "$rid"
  --self-contained true
  -p:VersionPrefix="$version"
  -p:SourceRevisionId="$commit"
  -p:ContinuousIntegrationBuild=true
  -p:Deterministic=true
  -p:NativeDebugSymbols=false
  -p:BukitStripSymbols=true
  --artifacts-path "$build_root"
  -p:PathMap="$(native_path "$(pwd -P)")=/_/src%2C$(native_path "$build_root")=/_/build"
  -o "$publish_dir"
)

# Linker arguments that make the native link deterministic. ILC output is
# already byte-identical between clean builds on every RID, so single-threaded
# ILC is never needed.
case "$rid" in
  osx-*)
    # Apple's linker races when it assigns the GOT slot that its default "fast"
    # objc_msgSend stubs load from, so identical inputs link to one of two
    # images. LC_UUID is a content hash and the ad-hoc signature covers both,
    # so they only follow that 3-byte swap; -no_uuid would only hide it. Small
    # stubs branch to the shared _objc_msgSend stub and link deterministically.
    linker_item='<ExtraLinkerArg Include="-objc_stubs_small" />'
    ;;
  win-*)
    # link.exe stamps the PE header with the link time unless /Brepro replaces
    # it with a content hash. ILCompiler reads ExtraLinkerArg only on Unix.
    linker_item='<LinkerArg Include="/Brepro" />'
    ;;
  *)
    # GNU ld already gets --build-id=sha1 from ILCompiler.
    linker_item=""
    ;;
esac
if [[ -n "$linker_item" ]]; then
  linker_targets="$build_root/deterministic-linker.targets"
  printf '<Project>\n  <ItemGroup>\n    %s\n  </ItemGroup>\n</Project>\n' "$linker_item" > "$linker_targets"
  publish_args+=(-p:CustomAfterMicrosoftCommonTargets="$(native_path "$linker_targets")")
fi

dotnet "${publish_args[@]}" >&2

find "$publish_dir" -type d -name '*.dSYM' -prune -exec rm -rf -- {} +
find "$publish_dir" -type f \( -name '*.pdb' -o -name '*.dbg' \) -exec rm -f -- {} +

exe="$publish_dir/bukit"
[[ "$rid" != win-* ]] || exe="$exe.exe"
[[ -f "$exe" && -s "$exe" && ! -L "$exe" ]] || {
  echo "release CLI is missing or empty: $exe" >&2
  exit 1
}
[[ "$rid" == win-* || -x "$exe" ]] || {
  echo "release CLI is not executable: $exe" >&2
  exit 1
}

if [[ "$rid" == win-* ]]; then
  archive_for_pwsh="$(native_path "$pending_archive")"

  if command -v pwsh >/dev/null 2>&1; then
    pwsh_cmd="pwsh"
  elif command -v powershell >/dev/null 2>&1; then
    pwsh_cmd="powershell"
  else
    (cd "$publish_dir" && zip -qr "$pending_archive" .)
  fi

  if [[ -n "${pwsh_cmd:-}" ]]; then
    (cd "$publish_dir" && BUKIT_ARCHIVE_PATH="$archive_for_pwsh" "$pwsh_cmd" -NoProfile -Command \
      '$source=(Get-Location).Path; $dest=$env:BUKIT_ARCHIVE_PATH; [IO.Compression.ZipFile]::CreateFromDirectory($source,$dest)')
  fi
else
  tar -C "$publish_dir" -czf "$pending_archive" .
fi

[[ -s "$pending_archive" ]] || {
  echo "archive is empty: $pending_archive" >&2
  exit 1
}
mv -- "$pending_archive" "$archive"

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  printf 'archive=%s\npublish_dir=%s\n' "$archive" "$publish_dir" >> "$GITHUB_OUTPUT"
fi

printf '%s\n' "$archive"

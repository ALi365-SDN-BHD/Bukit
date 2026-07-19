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

dotnet publish src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj \
  -c "$configuration" \
  -r "$rid" \
  --self-contained true \
  -p:VersionPrefix="$version" \
  -p:SourceRevisionId="$commit" \
  -p:ContinuousIntegrationBuild=true \
  -p:Deterministic=true \
  -p:NativeDebugSymbols=false \
  --artifacts-path "$build_root" \
  -p:PathMap="$(pwd -P)=/_/src%2C$build_root=/_/build" \
  -o "$publish_dir" >&2

[[ -n "$(find "$publish_dir" -mindepth 1 -print -quit)" ]] || {
  echo "publish directory is empty: $publish_dir" >&2
  exit 1
}

if [[ "$rid" == win-* ]]; then
  archive_for_pwsh="$archive"
  if command -v cygpath >/dev/null 2>&1; then
    archive_for_pwsh="$(cygpath -w "$archive")"
  fi

  if command -v pwsh >/dev/null 2>&1; then
    pwsh_cmd="pwsh"
  elif command -v powershell >/dev/null 2>&1; then
    pwsh_cmd="powershell"
  else
    (cd "$publish_dir" && zip -qr "$archive" .)
  fi

  if [[ -n "${pwsh_cmd:-}" ]]; then
    (cd "$publish_dir" && BUKIT_ARCHIVE_PATH="$archive_for_pwsh" "$pwsh_cmd" -NoProfile -Command \
      '$source=(Get-Location).Path; $dest=$env:BUKIT_ARCHIVE_PATH; [IO.Compression.ZipFile]::CreateFromDirectory($source,$dest)')
  fi
else
  tar -C "$publish_dir" -czf "$archive" .
fi

[[ -s "$archive" ]] || {
  echo "archive is empty: $archive" >&2
  exit 1
}

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  printf 'archive=%s\npublish_dir=%s\n' "$archive" "$publish_dir" >> "$GITHUB_OUTPUT"
fi

printf '%s\n' "$archive"

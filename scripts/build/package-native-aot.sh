#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../.."

version="${1:-}"
rid="${2:-}"
output_root="${3:-}"
configuration="${4:-Release}"

if [[ -z "$version" || -z "$rid" || -z "$output_root" ]]; then
  echo "usage: bash scripts/build/package-native-aot.sh <version> <rid> <output-root> [configuration]" >&2
  exit 2
fi

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
output_root="$(cd "$output_root" && pwd)"
publish_dir="$output_root/publish/$rid"
mkdir -p "$publish_dir"

dotnet publish src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj \
  -c "$configuration" \
  -r "$rid" \
  --self-contained true \
  -p:VersionPrefix="$version" \
  -p:SourceRevisionId="$commit" \
  -o "$publish_dir"

if [[ "$rid" == win-* ]]; then
  archive="$output_root/$archive_base.zip"
  archive_for_pwsh="$archive"
  if command -v cygpath >/dev/null 2>&1; then
    archive_for_pwsh="$(cygpath -w "$archive")"
  fi

  if command -v pwsh >/dev/null 2>&1; then
    (cd "$publish_dir" && pwsh -NoProfile -Command "Compress-Archive -Path * -DestinationPath '$archive_for_pwsh' -Force")
  elif command -v powershell >/dev/null 2>&1; then
    (cd "$publish_dir" && powershell -NoProfile -Command "Compress-Archive -Path * -DestinationPath '$archive_for_pwsh' -Force")
  else
    (cd "$publish_dir" && zip -qr "$archive" .)
  fi
else
  archive="$output_root/$archive_base.tar.gz"
  tar -C "$publish_dir" -czf "$archive" .
fi

if [[ -n "${GITHUB_OUTPUT:-}" ]]; then
  echo "archive=$archive" >> "$GITHUB_OUTPUT"
fi

printf '%s\n' "$archive"

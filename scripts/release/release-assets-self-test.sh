#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
prepare="$script_dir/prepare-release-assets.sh"
verify="$script_dir/verify-release-assets.sh"
tmp="$(mktemp -d)"
tmp="$(cd "$tmp" && pwd -P)"
trap 'rm -rf "$tmp"' EXIT

fail() {
  echo "$*" >&2
  exit 1
}

expect_fail() {
  local message="$1"
  shift
  if "$@"; then
    fail "$message"
  fi
}

make_asset() {
  local path="$1"
  mkdir -p "$(dirname "$path")"
  printf 'asset:%s\n' "$(basename "$path")" > "$path"
}

version=1.2.3
commit=abc
linux="$tmp/input/bukit-$version-linux-x64.tar.gz"
macos="$tmp/input/bukit-$version-osx-arm64.tar.gz"
windows="$tmp/input/bukit-$version-win-x64.zip"
make_asset "$linux"
make_asset "$macos"
make_asset "$windows"

out="$tmp/duplicate-path"
if bash "$prepare" "$version" "$commit" "$out" "$linux" "$linux"; then
  fail "duplicate archive unexpectedly passed"
fi

duplicate_basename="$tmp/other/$(basename "$linux")"
make_asset "$duplicate_basename"
expect_fail "duplicate basename unexpectedly passed" \
  bash "$prepare" "$version" "$commit" "$tmp/duplicate-basename" \
  "$linux" "$duplicate_basename"

reserved="$tmp/input/checksums.txt"
make_asset "$reserved"
expect_fail "reserved metadata name unexpectedly passed" \
  bash "$prepare" "$version" "$commit" "$tmp/reserved" "$reserved"

symlink="$tmp/input/bukit-$version-linux-x64-link.tar.gz"
ln -s "$linux" "$symlink"
expect_fail "symlink archive unexpectedly passed" \
  bash "$prepare" "$version" "$commit" "$tmp/symlink" "$symlink"

wrong_extension="$tmp/input/bukit-$version-win-x64.tar.gz"
make_asset "$wrong_extension"
expect_fail "wrong RID extension unexpectedly passed" \
  bash "$prepare" "$version" "$commit" "$tmp/wrong-extension" "$wrong_extension"

out="$tmp/valid-linux"
bash "$prepare" "$version" "$commit" "$out" "$linux"
printf '%064d  extra.tar.gz\n' 0 >> "$out/checksums.txt"
expect_fail "extra checksum unexpectedly passed" \
  bash "$verify" "$version" "$commit" "$out" linux-x64

bash "$prepare" "$version" "$commit" "$out" "$linux"
printf 'stale\n' > "$out/stale-debug.zip"
expect_fail "stale disk asset unexpectedly passed" \
  bash "$verify" "$version" "$commit" "$out" linux-x64

bash "$prepare" "$version" "$commit" "$out" "$linux"
expect_fail "duplicate RID unexpectedly passed" \
  bash "$verify" "$version" "$commit" "$out" linux-x64 linux-x64

out="$tmp/all-rids"
bash "$prepare" "$version" "$commit" "$out" "$linux" "$macos" "$windows"
bash "$verify" "$version" "$commit" "$out" linux-x64 osx-arm64 win-x64

echo "release-assets self-test OK"

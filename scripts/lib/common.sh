#!/usr/bin/env bash

bukit_repo_root() {
  local script_dir
  script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
  cd "$script_dir/../.." >/dev/null 2>&1
  pwd
}

bukit_cd_repo_root() {
  cd "$(bukit_repo_root)"
}

bukit_host_rid() {
  local os arch
  os="$(uname -s)"
  arch="$(uname -m)"

  case "$os:$arch" in
    Linux:x86_64) echo "linux-x64" ;;
    Darwin:arm64) echo "osx-arm64" ;;
    Darwin:x86_64) echo "osx-x64" ;;
    MINGW*:x86_64|MSYS*:x86_64|CYGWIN*:x86_64) echo "win-x64" ;;
    *) echo "unsupported" ;;
  esac
}

bukit_cli() {
  local configuration="$1"
  shift
  dotnet run --project src/Bukit.Cli -c "$configuration" -- "$@"
}

bukit_sha256() {
  local path="$1"
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$path" | awk '{print $1}'
  else
    shasum -a 256 "$path" | awk '{print $1}'
  fi
}

bukit_find_binary() {
  local publish_dir="$1"
  if [ -f "$publish_dir/bukit" ]; then
    printf '%s\n' "$publish_dir/bukit"
    return 0
  fi
  if [ -f "$publish_dir/bukit.exe" ]; then
    printf '%s\n' "$publish_dir/bukit.exe"
    return 0
  fi
  return 1
}

is_truthy() {
  local value
  value="$(printf '%s' "${1:-}" | tr '[:upper:]' '[:lower:]')"

  case "$value" in
    1|true|yes|on)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

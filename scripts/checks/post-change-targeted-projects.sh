#!/usr/bin/env bash
set -euo pipefail

[[ $# -eq 1 ]] || { echo "usage: bash scripts/checks/post-change-targeted-projects.sh PATH" >&2; exit 2; }

path="${1#./}"

project_for_module() {
  local module="$1"
  case "$module" in
    Bukit.Cli.Shared) module="Bukit.Cli" ;;
    Bukit.Plugin.WechatSync|Bukit.WechatSyncing) module="Bukit.Plugin.WechatSync" ;;
    Bukit.Plugin.Echo)
      printf '%s\n' \
        tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj \
        tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj
      return ;;
  esac
  printf 'tests/%s.Tests/%s.Tests.csproj\n' "$module" "$module"
}

case "$path" in
  src/Bukit-Core/*/*) module="${path#src/Bukit-Core/}" ;;
  src/Bukit-Labs/*/*) module="${path#src/Bukit-Labs/}" ;;
  src/Bukit-Plugins/*/*) module="${path#src/Bukit-Plugins/}" ;;
  tests/PluginProcessProbe/*)
    printf '%s\n' tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj
    exit 0 ;;
  tests/ThrowingPlugin/*)
    printf '%s\n' tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
    exit 0 ;;
  tests/*.Tests/*)
    test_dir="${path#tests/}"; test_dir="${test_dir%%/*}"
    printf 'tests/%s/%s.csproj\n' "$test_dir" "$test_dir"
    exit 0 ;;
  *) exit 1 ;;
esac

module="${module%%/*}"
project_for_module "$module"

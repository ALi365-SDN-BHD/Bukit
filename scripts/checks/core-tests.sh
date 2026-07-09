#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

configuration="${1:-Release}"
silkroad_filter=""
if [[ ! -d examples/silkroad_biz23 ]]; then
  silkroad_filter="|FullyQualifiedName!~SilkroadBiz23ExampleTests"
fi

projects=(
  tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
  tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj
  tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj
  tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj
  "tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj${silkroad_filter}"
  tests/Bukit.Plugin.Abstractions.Tests/Bukit.Plugin.Abstractions.Tests.csproj
  tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj
  tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj
  tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj
  tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj
  tests/Bukit.Theme.Tests/Bukit.Theme.Tests.csproj
)

for entry in "${projects[@]}"; do
  project="$entry"
  filter=""
  if [[ "$entry" == *"|"* ]]; then
    project="${entry%%|*}"
    filter="${entry#*|}"
  fi

  if [[ -n "$filter" ]]; then
    run_step "$(basename "$(dirname "$project")")" dotnet test "$project" -c "$configuration" --filter "$filter"
  else
    run_step "$(basename "$(dirname "$project")")" dotnet test "$project" -c "$configuration"
  fi
done

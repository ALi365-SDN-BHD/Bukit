#!/usr/bin/env bash
set -euo pipefail

emit() {
  printf '%s\t%s\n' "$1" "${2:-}"
}

engine_filter=""
if [[ ! -d examples/silkroad_biz23 ]]; then
  engine_filter="FullyQualifiedName!~SilkroadBiz23ExampleTests"
fi

emit tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
emit tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj
emit tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj
emit tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj
emit tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj "$engine_filter"
emit tests/Bukit.Plugin.Abstractions.Tests/Bukit.Plugin.Abstractions.Tests.csproj
emit tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj
emit tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj
emit tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj
emit tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj
emit tests/Bukit.Theme.Tests/Bukit.Theme.Tests.csproj

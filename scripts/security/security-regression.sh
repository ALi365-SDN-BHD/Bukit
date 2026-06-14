#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

echo "=== Core boundary security tests ==="
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj \
  -c "$configuration" \
  --filter "FullyQualifiedName~CoreBoundaryTests"

echo "=== Safe URL tests ==="
dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj \
  -c "$configuration" \
  --filter "FullyQualifiedName~SafeUrl"

echo "=== Config rejection tests ==="
dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj \
  -c "$configuration" \
  --filter "FullyQualifiedName~ExternalPlugin|FullyQualifiedName~ConfigException"

echo "=== CLI security contract tests ==="
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj \
  -c "$configuration" \
  --filter "FullyQualifiedName~PathTraversal|FullyQualifiedName~ConfigException|FullyQualifiedName~DoesNotExposeAllowExternalPluginsFlag"

echo "=== Engine security tests ==="
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj \
  -c "$configuration" \
  --filter "FullyQualifiedName~Security|FullyQualifiedName~RouteSecurity|FullyQualifiedName~Plugin"

echo "=== Content safety tests ==="
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj \
  -c "$configuration" \
  --filter "FullyQualifiedName~SafeUrl|FullyQualifiedName~Renderer|FullyQualifiedName~Audio|FullyQualifiedName~Notion"

echo "Security regression OK"

#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"

echo "=== SafeUrl tests ==="
dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj -c "$configuration" --filter "FullyQualifiedName~SafeUrl"

echo "=== Config security tests ==="
dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj -c "$configuration" --filter "FullyQualifiedName~ExternalPluginPolicy|FullyQualifiedName~ConfigException"

echo "=== CLI security tests ==="
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c "$configuration" --filter "FullyQualifiedName~PathTraversal|FullyQualifiedName~ConfigException"

echo "=== Engine security tests ==="
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c "$configuration" --filter "FullyQualifiedName~Security|FullyQualifiedName~RouteSecurity|FullyQualifiedName~ExternalPlugin|FullyQualifiedName~Plugin"

echo "=== Content security tests ==="
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c "$configuration" --filter "FullyQualifiedName~SafeUrl|FullyQualifiedName~Renderer|FullyQualifiedName~Audio|FullyQualifiedName~Notion"

echo "=== security-regression OK ==="

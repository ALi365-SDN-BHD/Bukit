#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"

echo "=== security regression tests ==="

dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj -c "$configuration" --filter "FullyQualifiedName~SafeUrl"
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c "$configuration" --filter "FullyQualifiedName~Security"
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c "$configuration" --filter "FullyQualifiedName~SafeUrl"

echo "Security regression OK"
#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

configuration="${1:-Release}"
projects=(
  "tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj|FullyQualifiedName~SsrfGuardIntegrationTests|FullyQualifiedName~DevRequestHandler_HandleAsync_DoesNotServeBukitInternalFiles"
  "tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj|FullyQualifiedName~BlockRendererUrlSafetyTests|FullyQualifiedName~ImageAssetLocalizerTests"
  "tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj|FullyQualifiedName~RouteSecurityValidatorTests|FullyQualifiedName~SafeOutputFileSystemTests|FullyQualifiedName~BuildReporterTests|FullyQualifiedName~ThemeBootstrapperSanitizationTests|FullyQualifiedName~DirectoryCopyFollowSymlinksTests"
  "tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj|FullyQualifiedName~PluginPermissionEvaluatorTests|FullyQualifiedName~PluginHashVerifierTests|FullyQualifiedName~PluginManifestLoaderTests|FullyQualifiedName~PluginConfigLoaderTests|FullyQualifiedName~PluginPathValidatorTests"
  "tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj|FullyQualifiedName~RouteSecurityValidatorTests"
)

for entry in "${projects[@]}"; do
  project="${entry%%|*}"
  filter="${entry#*|}"
  args=(test "$project" -c "$configuration" --filter "$filter")
  if [[ "${BUKIT_SECURITY_SKIP_RESTORE:-0}" == "1" ]]; then
    args+=(--no-restore)
  fi

  run_step "$(basename "$(dirname "$project")") security" dotnet "${args[@]}"
done

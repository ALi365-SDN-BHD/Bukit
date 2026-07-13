#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

configuration="${1:-Release}"
results="$(mktemp -d "${TMPDIR:-/tmp}/bukit-security-results.XXXXXX")"
trap 'rm -rf "$results"' EXIT
projects=(
  "tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj|FullyQualifiedName~SsrfGuardIntegrationTests|FullyQualifiedName~DevRequestHandler_HandleAsync_DoesNotServeBukitInternalFiles"
  "tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj|FullyQualifiedName~BlockRendererUrlSafetyTests|FullyQualifiedName~ImageAssetLocalizerTests"
  "tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj|FullyQualifiedName~RouteSecurityValidatorTests|FullyQualifiedName~SafeOutputFileSystemTests|FullyQualifiedName~BuildReporterTests|FullyQualifiedName~ThemeBootstrapperSanitizationTests|FullyQualifiedName~DirectoryCopyFollowSymlinksTests"
  "tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj|FullyQualifiedName~PluginPermissionEvaluatorTests|FullyQualifiedName~PluginHashVerifierTests|FullyQualifiedName~PluginManifestLoaderTests|FullyQualifiedName~PluginConfigLoaderTests|FullyQualifiedName~PluginPathValidatorTests"
  "tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj|FullyQualifiedName~RouteSecurityValidatorTests"
)

for entry in "${projects[@]}"; do
  IFS='|' read -r -a fields <<< "$entry"
  project="${fields[0]}"
  selectors=("${fields[@]:1}")
  filter="$(IFS='|'; printf '%s' "${selectors[*]}")"
  name="$(basename "$(dirname "$project")")"
  trx="$results/$name.trx"
  args=(test "$project" -c "$configuration" --filter "$filter"
    --logger "trx;LogFileName=$name.trx" --results-directory "$results")
  [[ "${BUKIT_SECURITY_SKIP_RESTORE:-0}" != 1 ]] || args+=(--no-restore)
  run_step "$name security" dotnet "${args[@]}"
  [[ -f "$trx" ]] || { echo "missing security TRX: $trx" >&2; exit 1; }
  python3 scripts/security/verify-trx.py "$trx" "${selectors[@]}"
done

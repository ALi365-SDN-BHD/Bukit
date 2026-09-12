#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

configuration="${1:-Release}"
if [[ -n "${BUKIT_SECURITY_RESULTS:-}" ]]; then
  # Allocate a fresh run directory; never overwrite or clean caller-owned evidence.
  results="$(python3 - "$BUKIT_SECURITY_RESULTS" <<'PY_RESULTS'
import pathlib, sys, tempfile
path = pathlib.Path(sys.argv[1])
if '..' in path.parts or path == pathlib.Path('.') or path == pathlib.Path('/'):
    raise SystemExit("unsafe security results path")
path = path.absolute()
if any(part.is_symlink() for part in (path, *path.parents)):
    raise SystemExit("symlink security results path")
path.mkdir(parents=True, exist_ok=True)
print(tempfile.mkdtemp(prefix="run-", dir=path))
PY_RESULTS
)"
else
  results="$(mktemp -d "${TMPDIR:-/tmp}/bukit-security-results.XXXXXX")"
  trap 'rm -rf "$results"' EXIT
fi
projects=(
  "tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj|FullyQualifiedName~SsrfGuardIntegrationTests|FullyQualifiedName~DevRequestHandler_HandleAsync_DoesNotServeBukitInternalFiles"
  "tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj|FullyQualifiedName~ImageAssetLocalizerTests"
  "tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj|FullyQualifiedName~BlockRendererUrlSafetyTests"
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

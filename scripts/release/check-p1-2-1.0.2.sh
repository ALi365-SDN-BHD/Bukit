#!/usr/bin/env bash
set -euo pipefail

: "${GITHUB_REPOSITORY:?请先设置 GITHUB_REPOSITORY（如 owner/repo）}"
: "${GITHUB_SHA:?请先设置 GITHUB_SHA}"

release_dir="${1:-TestResults/release-gate/native-aot/linux-x64}"
release_version="${2:-1.0.2}"
release_commit="${3:-$GITHUB_SHA}"
release_download_dir="${4:-${release_dir}/../..}"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

abs_release_dir="$release_dir"
if [[ "$release_dir" != /* ]]; then
    abs_release_dir="$repo_root/$release_dir"
fi

abs_download_dir="$release_download_dir"
if [[ "$release_download_dir" != /* ]]; then
    abs_download_dir="$repo_root/$release_download_dir"
fi

echo "==> CI workflow evidence check"
bash scripts/checks/ci-workflow-evidence.sh "$GITHUB_REPOSITORY" "$release_commit" "ci.yml" TestResults/release-gate/ci-workflow-evidence.json 1 TestResults/release-gate/rc-gate-evidence.md main,master

echo "==> Coverage baseline JSON check"
bash scripts/checks/coverage-baseline-schema.sh

echo "==> Full solution test"
dotnet test bukit.slnx

echo "==> Dev server rebuild regression tests"
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter "FullyQualifiedName~DevFileWatcher_RebuildFailure_DoesNotDisposeWatcher|FullyQualifiedName~DevFileWatcher_RapidChanges_DebouncedToSingleRebuild|FullyQualifiedName~DevRequestHandler_LiveReloadScript_UsesSameOriginWebSocket"

echo "==> Release artifact smoke"
bash scripts/smoke/release-artifacts.sh "$abs_release_dir"

echo "==> Release asset strict checks"
bash scripts/release/verify-release-assets.sh "$release_version" "$release_commit" "$abs_download_dir"

echo "Check complete."

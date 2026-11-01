#!/usr/bin/env bash
set -euo pipefail

emit() {
  printf '%s\t%s\n' "$1" "${2:-}"
}

cli_filter=""
case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*) ;;
  *)
    # Windows-only cases must execute in the Windows job, not become coverage skips.
    for method in \
      Bukit.Cli.Tests.DeployWindowsRegressionTests.ResolveGit_Windows_PrefersNativeGitOverEarlierBatchWrapper \
      Bukit.Cli.Tests.DeployWindowsRegressionTests.ResolveGit_Windows_RejectsBatchOnlyPath \
      Bukit.Cli.Tests.DeployWindowsRegressionTests.CleanupDeploymentDirectory_LockedFileReportsWarningWithoutLeakingPath \
      Bukit.Cli.Tests.GitHubPagesDeployProviderTests.DeployAsync_RealLocalGitPushFailure_CleansReadOnlyObjectsAndAskpass \
      Bukit.Cli.Tests.GitHubPagesDeployProviderTests.DeployAsync_RealLocalGitPushCancellation_CleansReadOnlyObjectsAndAskpass; do
      cli_filter+="FullyQualifiedName!=${method}&"
    done
    cli_filter="${cli_filter%&}"
    ;;
esac

emit tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj "$cli_filter"
emit tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj
emit tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj
emit tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj
emit tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj
emit tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj
emit tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj
emit tests/Bukit.Plugin.Abstractions.Tests/Bukit.Plugin.Abstractions.Tests.csproj
emit tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj
emit tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj
emit tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj
emit tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj
emit tests/Bukit.Theme.Tests/Bukit.Theme.Tests.csproj

# The shared CLI project must include its owner command as well as Architecture.
expect_exit 0 "${tool[@]}" closure \
  --repo "$closure_fixture" \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed src/Bukit-Core/Bukit.Cli.Shared/Bukit.Cli.Shared.csproj
python3 - "$command_output" <<'PY'
import json
import sys

result = json.loads(sys.argv[1])
if "src/Bukit-Core/Bukit.Cli.Shared/Bukit.Cli.Shared.csproj" in result["unmappedFiles"]:
    raise SystemExit("named shared CLI project must be mapped")
expected_tests = [
    "dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj",
    "dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj",
]
if result["specialtyTests"] != expected_tests:
    raise SystemExit(f"unexpected shared CLI project tests: {result['specialtyTests']}")
PY

# Verify Directory.Packages.props central-package closure mapping.
expect_exit 0 "${tool[@]}" closure \
  --repo "$closure_fixture" \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed Directory.Packages.props
python3 - "$command_output" <<'PY'
import json
import sys

result = json.loads(sys.argv[1])
if "Directory.Packages.props" in result["unmappedFiles"]:
    raise SystemExit("Directory.Packages.props must not be unmapped")
expected_tests = [
    "dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj",
    "dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj",
    "dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj",
    "dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj",
    "dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj",
    "dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj",
    "dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj",
    "dotnet test tests/Bukit.Plugin.Abstractions.Tests/Bukit.Plugin.Abstractions.Tests.csproj",
    "dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj",
    "dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj",
    "dotnet test tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj",
    "dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj",
    "dotnet test tests/Bukit.Theme.Tests/Bukit.Theme.Tests.csproj",
]
if result["specialtyTests"] != expected_tests:
    raise SystemExit(
        f"unexpected specialty tests for Directory.Packages.props: "
        f"{result['specialtyTests']}"
    )
expected_consumers = sorted([
    "tests/Bukit.Architecture.Tests/ContentBoundaryTests.cs",
    "tests/Bukit.Cli.Tests/GitProcessRunnerTests.cs",
    "tests/Bukit.Cli.Tests/HelpPrinterTests.cs",
    "tests/Bukit.Config.Tests/ConfigLoaderTests.cs",
    "tests/Bukit.Content.Notion.Tests/NotionContentSourceTests.cs",
    "tests/Bukit.Content.Tests/BodyCacheDecoratorTests.cs",
    "tests/Bukit.Engine.Tests/EngineFeatureTests.cs",
    "tests/Bukit.Notion.Tests/NotionClientTests.cs",
    "tests/Bukit.Plugin.Abstractions.Tests/PluginConfigDtoTests.cs",
    "tests/Bukit.PluginHost.Tests/SystemProcessRunnerTests.cs",
    "tests/Bukit.Rendering.Tests/RenderingPackageTests.cs",
    "tests/Bukit.Routing.Tests/RoutePathBuilderTests.cs",
    "tests/Bukit.Routing.Tests/RoutingPackageTests.cs",
    "tests/Bukit.Shared.Tests/SharedPackageTests.cs",
    "tests/Bukit.Theme.Tests/ThemePackageTests.cs",
])
if result["contractConsumers"] != expected_consumers:
    raise SystemExit(
        f"unexpected contract consumers for Directory.Packages.props: "
        f"{result['contractConsumers']}"
    )
PY


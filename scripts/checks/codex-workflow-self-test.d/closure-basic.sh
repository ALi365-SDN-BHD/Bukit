expect_exit 0 "${tool[@]}" closure \
  --repo "$closure_fixture" \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed src/Bukit-Core/Bukit.Config/AppConfig.cs \
  --changed README.unknown
closure_output="$command_output"

python3 - "$closure_output" <<'PY'
import json
import sys

result = json.loads(sys.argv[1])
expected_command = "dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj"
if result["schemaVersion"] != 1:
    raise SystemExit("closure must declare schemaVersion 1")
if result["changedFiles"] != [
    "README.unknown",
    "src/Bukit-Core/Bukit.Config/AppConfig.cs",
]:
    raise SystemExit(f"unexpected changed files: {result['changedFiles']}")
if result["directConsumers"] != [
    "src/Bukit-Core/Bukit.Engine/ConfigConsumer.cs"
]:
    raise SystemExit(f"unexpected direct consumers: {result['directConsumers']}")
if result["contractConsumers"] != [
    "tests/Bukit.Config.Tests/ConfigLoaderTests.cs"
]:
    raise SystemExit(f"unexpected contract consumers: {result['contractConsumers']}")
if result["specialtyTests"] != [expected_command]:
    raise SystemExit(f"unexpected specialty tests: {result['specialtyTests']}")
if result["unmappedFiles"] != ["README.unknown"]:
    raise SystemExit(f"unexpected unmapped files: {result['unmappedFiles']}")
if result["publicContractFiles"] != [
    "src/Bukit-Core/Bukit.Config/AppConfig.cs"
]:
    raise SystemExit(f"unexpected public contract files: {result['publicContractFiles']}")
expected_closure = sorted(
    result["changedFiles"] + result["directConsumers"] + result["contractConsumers"]
)
if result["closureFiles"] != expected_closure:
    raise SystemExit(f"unexpected closure: {result['closureFiles']}")
PY

assert_closure_mapping "$closure_fixture" \
  src/Bukit-Core/Bukit.Plugin.Abstractions/Bukit.Plugin.Abstractions.csproj \
  '["dotnet test tests/Bukit.Plugin.Abstractions.Tests/Bukit.Plugin.Abstractions.Tests.csproj", "dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj"]' \
  true
assert_closure_mapping "$closure_fixture" \
  src/Bukit-Core/Bukit.Plugin.Abstractions/Manifest/PluginManifest.cs \
  '["dotnet test tests/Bukit.Plugin.Abstractions.Tests/Bukit.Plugin.Abstractions.Tests.csproj", "dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj"]' \
  true
assert_closure_mapping "$closure_fixture" \
  tests/Bukit.Plugin.Abstractions.Tests/PluginManifestBinaryCompatibilityTests.cs \
  '["dotnet test tests/Bukit.Plugin.Abstractions.Tests/Bukit.Plugin.Abstractions.Tests.csproj"]' \
  false
assert_closure_mapping "$closure_fixture" \
  src/Bukit-Core/Bukit.PluginHost/SystemProcessRunner.cs \
  '["dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj"]' \
  true
assert_closure_mapping "$closure_fixture" \
  src/Bukit-Core/Bukit.Engine.Abstractions/ContentDocumentFactory.cs \
  '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj"]' \
  true
assert_closure_mapping "$closure_fixture" \
  tests/Bukit.Engine.Abstractions.Tests/ContentDocumentFactoryTests.cs \
  '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  src/Bukit-Core/Bukit.Routing/RoutePathBuilder.cs \
  '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj", "dotnet test tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj"]' \
  true
assert_closure_mapping \
  "$closure_fixture" \
  tests/Bukit.Routing.Tests/RoutePathBuilderTests.cs \
  '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  tests/PluginProcessProbe/Program.cs \
  '["dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj"]' \
  false
expect_exit 0 "${tool[@]}" closure \
  --repo "$closure_fixture" \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --changed docs/schemas/seo-route-map.v1.schema.json \
  --changed docs/schemas/seo-observation.v1.schema.json \
  --changed docs/schemas/seo-insights-rules.v1.schema.json \
  --changed guide/user/21-seo-insights.md
seo_observability_closure_output="$command_output"

python3 - "$seo_observability_closure_output" <<'PY'
import json
import sys

result = json.loads(sys.argv[1])
expected_command = (
    "dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj"
)
if result["unmappedFiles"] != []:
    raise SystemExit(
        "expected SEO observability paths to be mapped, got unmapped: "
        f"{result['unmappedFiles']}"
    )
if result["specialtyTests"] != [expected_command]:
    raise SystemExit(
        "unexpected SEO observability specialty tests: "
        f"{result['specialtyTests']}"
    )
PY

assert_closure_mapping \
  "$closure_fixture" \
  src/Bukit-Core/Bukit.Content/BodyCacheDecorator.cs \
  '["dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj"]' \
  true
assert_closure_mapping \
  "$closure_fixture" \
  src/Bukit-Core/Bukit.Content.Notion/NotionBodyStore.cs \
  '["dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj", "dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj"]' \
  true
assert_closure_mapping \
  "$closure_fixture" \
  src/Bukit-Core/Bukit.Cli/Deploy/GitProcessRunner.cs \
  '["dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj"]' \
  true
assert_closure_mapping \
  "$closure_fixture" \
  src/Bukit-Core/Bukit.Cli.Shared/Cli/Rendering/CliHelpRenderer.cs \
  '["dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj"]' \
  true
assert_closure_mapping \
  "$closure_fixture" \
  tests/Bukit.Cli.Tests/GitProcessRunnerTests.cs \
  '["dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  src/Bukit-Core/Bukit.Notion/Transport/NotionClient.cs \
  '["dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj"]' \
  true
assert_closure_mapping \
  "$closure_fixture" \
  scripts/checks/baselines/code-analysis.v1.json \
  '["bash scripts/checks/code-analysis-ratchet-self-test.sh"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  docs/superpowers/plans/2026-08-06-code-analysis-ratchet-closure.md \
  '["bash scripts/checks/code-analysis-ratchet-self-test.sh"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  src/Bukit-Plugins/Bukit.Importing/ImportNotionPushWorkflow.cs \
  '["dotnet test tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj", "dotnet test tests/Bukit.Plugin.Import.Tests/Bukit.Plugin.Import.Tests.csproj"]' \
  true
assert_closure_mapping \
  "$closure_fixture" \
  tests/Bukit.Importing.Tests/ImportNotionPushWorkflowTests.cs \
  '["dotnet test tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj \
  '["dotnet test tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  tests/Bukit.Plugin.Import.Tests/ImportPluginInvokeCompatibilityTests.cs \
  '["dotnet test tests/Bukit.Plugin.Import.Tests/Bukit.Plugin.Import.Tests.csproj"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  tests/Bukit.Architecture.Tests/ContentBoundaryTests.cs \
  '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  guide/dev/built-in-plugins.md \
  '["dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  guide/dev/content.md \
  '["dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj"]' \
  false

for public_api_governance_path in \
  docs/governance/bukit-core-public-api-baseline.v1.json \
  docs/schemas/bukit-core-public-api-baseline.v1.schema.json \
  docs/superpowers/plans/2026-08-06-bukit-public-api-drift-remediation.md \
  guide/dev/public-api-governance.md \
  scripts/checks/public-api-drift-self-test.sh \
  tools/Bukit.PublicApiDrift/ApiSurfaceModels.cs; do
  assert_closure_mapping \
    "$closure_fixture" \
    "$public_api_governance_path" \
    '["bash scripts/checks/public-api-drift-self-test.sh"]' \
    false
done

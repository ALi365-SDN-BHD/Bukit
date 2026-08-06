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
  docs/superpowers/plans/2026-08-06-core-release-must-fix-closure.md \
  '["bash scripts/checks/codex-workflow-self-test.sh"]' \
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
  assert_closure_mapping "$closure_fixture" "$public_api_governance_path" '["bash scripts/checks/public-api-drift-self-test.sh"]' false
done

for cli_documentation_path in guide/dev/cli.md guide/skills/bukit-cli-reference/SKILL.md; do
  assert_closure_mapping "$closure_fixture" "$cli_documentation_path" '["bash guide/skills/scripts/validate-skills-strict.sh", "bash scripts/checks/cli-docs-sync.sh"]' true
done

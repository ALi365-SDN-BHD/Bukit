for notion_fallback_contract_path in \
  openspec/changes/fix-notion-relation-fallback-diagnostics/proposal.md \
  openspec/changes/fix-notion-relation-fallback-diagnostics/design.md \
  openspec/changes/fix-notion-relation-fallback-diagnostics/tasks.md \
  openspec/changes/fix-notion-relation-fallback-diagnostics/specs/notion-relation-fallback-diagnostics/spec.md \
  openspec/changes/archive/2026-10-26-fix-notion-relation-fallback-diagnostics/proposal.md \
  openspec/changes/archive/2026-10-26-fix-notion-relation-fallback-diagnostics/design.md \
  openspec/changes/archive/2026-10-26-fix-notion-relation-fallback-diagnostics/tasks.md \
  openspec/changes/archive/2026-10-26-fix-notion-relation-fallback-diagnostics/specs/notion-relation-fallback-diagnostics/spec.md \
  openspec/specs/notion-relation-fallback-diagnostics/spec.md; do
  assert_closure_mapping "$closure_fixture" "$notion_fallback_contract_path" '["dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj --filter FullyQualifiedName~CrossSourceRelationProjectionTests"]' false
done

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
  src/Bukit-Core/Bukit.Content/Notion/NotionBodyStore.cs \
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
  scripts/checks/public-api-drift-self-test.sh \
  tools/Bukit.PublicApiDrift/ApiSurfaceModels.cs; do
  assert_closure_mapping "$closure_fixture" "$public_api_governance_path" '["bash scripts/checks/public-api-drift-self-test.sh"]' false
done
for image_resize_temp_contract_path in \
  openspec/changes/fix-image-resize-temp-cleanup/proposal.md \
  openspec/changes/fix-image-resize-temp-cleanup/design.md \
  openspec/changes/fix-image-resize-temp-cleanup/tasks.md \
  openspec/changes/fix-image-resize-temp-cleanup/specs/image-resize-temp-cleanup/spec.md \
  openspec/changes/archive/2026-10-26-fix-image-resize-temp-cleanup/proposal.md \
  openspec/changes/archive/2026-10-26-fix-image-resize-temp-cleanup/design.md \
  openspec/changes/archive/2026-10-26-fix-image-resize-temp-cleanup/tasks.md \
  openspec/changes/archive/2026-10-26-fix-image-resize-temp-cleanup/specs/image-resize-temp-cleanup/spec.md \
  openspec/specs/image-resize-temp-cleanup/spec.md; do
  assert_closure_mapping "$closure_fixture" "$image_resize_temp_contract_path" '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj --filter FullyQualifiedName~BuiltInPluginSplitTests", "dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter \"FullyQualifiedName~ImageProcessingPluginTests|FullyQualifiedName~ExternalToolProcessRunnerTests\""]' false
done

for cli_documentation_path in guide/dev/cli.md guide/skills/bukit-cli-reference/SKILL.md; do
  assert_closure_mapping "$closure_fixture" "$cli_documentation_path" '["bash guide/skills/scripts/validate-skills-strict.sh", "bash scripts/checks/cli-docs-sync.sh"]' true
done

for i18n_worktree_contract_path in \
  docs/worktree.md \
  guide/user/11-i18n-seo.md \
  guide/user/14-troubleshooting.md \
  openspec/changes/add-i18n-worktree-isolation/.openspec.yaml \
  openspec/changes/add-i18n-worktree-isolation/README.md \
  openspec/changes/add-i18n-worktree-isolation/design.md \
  openspec/changes/add-i18n-worktree-isolation/proposal.md \
  openspec/changes/add-i18n-worktree-isolation/specs/i18n-worktree-build/spec.md \
  openspec/changes/add-i18n-worktree-isolation/tasks.md; do
  assert_closure_mapping "$closure_fixture" "$i18n_worktree_contract_path" '["bash scripts/checks/cli-docs-sync.sh", "openspec validate add-i18n-worktree-isolation --strict"]' true
done

# Approved quality-remediation paths stay exact; adjacent docs/workflows remain unmapped.
assert_closure_mapping "$closure_fixture" .github/workflows/ci.yaml '["bash scripts/checks/active-workflow-boundary-self-test.sh", "bash scripts/checks/active-workflow-boundary.sh", "dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj"]' false
assert_closure_mapping "$closure_fixture" scripts/checks/coverage/run-one.sh '["bash scripts/checks/coverage-run-one-self-test.sh", "dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj"]' false
assert_closure_mapping "$closure_fixture" scripts/checks/coverage-run-one-self-test.sh '["bash scripts/checks/coverage-run-one-self-test.sh"]' false
for readme_path in README.md README.zh-CN.md README.ms.md; do
  assert_closure_mapping "$closure_fixture" "$readme_path" '["bash scripts/checks/cli-docs-sync.sh", "bash scripts/checks/readme-sync.sh"]' false
done
for internal_contract_path in docs/governance/bukit-core-2.0-consumer-declaration.md guide/dev/public-api-governance.md; do
  assert_closure_mapping "$closure_fixture" "$internal_contract_path" '["bash scripts/checks/public-api-drift-self-test.sh", "dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj"]' false
done
for notion_contract_path in \
  openspec/changes/migrate-notion-compatibility/proposal.md \
  openspec/changes/migrate-notion-compatibility/design.md \
  openspec/changes/migrate-notion-compatibility/tasks.md \
  openspec/changes/migrate-notion-compatibility/specs/notion-compatibility-migration/spec.md \
  guide/dev/architecture.md \
  openspec/changes/archive/2026-10-26-migrate-notion-compatibility/proposal.md \
  openspec/changes/archive/2026-10-26-migrate-notion-compatibility/design.md \
  openspec/changes/archive/2026-10-26-migrate-notion-compatibility/tasks.md \
  openspec/changes/archive/2026-10-26-migrate-notion-compatibility/specs/notion-compatibility-migration/spec.md \
  openspec/specs/notion-compatibility-migration/spec.md; do
  assert_closure_mapping "$closure_fixture" "$notion_contract_path" '["bash scripts/checks/codex-workflow-self-test.sh", "bash scripts/checks/public-api-drift-self-test.sh", "dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj --filter \"FullyQualifiedName~NotionBoundaryTests|FullyQualifiedName~NotionCompatibilityMigrationTests\"", "dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj --filter \"FullyQualifiedName~NotionContentSourceTests|FullyQualifiedName~CrossSourceRelationProjectionTests\"", "dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj --filter FullyQualifiedName~NotionContentSourceEndToEndTests"]' false
done
assert_closure_mapping "$closure_fixture" guide/skills/bukit-content/SKILL.md '["bash guide/skills/scripts/validate-skills-strict.sh"]' false
for notion_governance_path in \
  docs/governance/bukit-core-2.0-notion-compatibility-migration.md \
  docs/analysis/bukit-core-ad03c5-notion-property-parser-retention-decision-2026-07-24.zh-CN.md; do
  assert_closure_mapping "$closure_fixture" "$notion_governance_path" '["bash scripts/checks/public-api-drift-self-test.sh", "dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj"]' false
done
assert_closure_mapping "$closure_fixture" src/Bukit-Labs/Bukit.Labs.Cli/Commands/Theme/ThemeCommand.cs '["dotnet test tests/Bukit.Labs.Cli.Tests/Bukit.Labs.Cli.Tests.csproj"]' true
assert_closure_mapping "$closure_fixture" tests/Bukit.Labs.Cli.Tests/ThemeDoctorReportPrinterTests.cs '["dotnet test tests/Bukit.Labs.Cli.Tests/Bukit.Labs.Cli.Tests.csproj"]' false
assert_closure_mapping "$closure_fixture" tests/Bukit.Importing.Tests/ThemeGeneratorTests.cs '["dotnet test tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj --filter FullyQualifiedName~ThemeGeneratorTests"]' false
for relocation_contract_path in \
  docs/governance/bukit-core-2.0-template-theme-relocation.md \
  guide/dev/theme.md \
  openspec/changes/relocate-template-theme/proposal.md \
  openspec/changes/relocate-template-theme/design.md \
  openspec/changes/relocate-template-theme/tasks.md \
  openspec/changes/relocate-template-theme/specs/template-theme-relocation/spec.md; do
  assert_closure_mapping "$closure_fixture" "$relocation_contract_path" '["bash scripts/checks/public-api-drift-self-test.sh", "dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj"]' false
done
for labs_yaml_contract_path in \
  openspec/changes/fix-labs-theme-yaml-context/proposal.md \
  openspec/changes/fix-labs-theme-yaml-context/design.md \
  openspec/changes/fix-labs-theme-yaml-context/tasks.md \
  openspec/changes/fix-labs-theme-yaml-context/specs/labs-theme-yaml-context/spec.md; do
  assert_closure_mapping "$closure_fixture" "$labs_yaml_contract_path" '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter FullyQualifiedName~MetaRemovalInventoryTests", "dotnet test tests/Bukit.Labs.Cli.Tests/Bukit.Labs.Cli.Tests.csproj"]' false
done
assert_closure_mapping "$closure_fixture" docs/superpowers/plans/2026-09-09-bukit-quality-remediation.md '["bash scripts/checks/codex-workflow-self-test.sh"]' false
for logging_contract_path in \
  openspec/changes/unify-library-logging/proposal.md \
  openspec/changes/unify-library-logging/design.md \
  openspec/changes/unify-library-logging/tasks.md \
  openspec/changes/unify-library-logging/specs/library-logging/spec.md; do
  assert_closure_mapping "$closure_fixture" "$logging_contract_path" '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter FullyQualifiedName~CommandConfigLoaderTests", "dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter \"FullyQualifiedName~LibraryDiagnosticOutputTests|FullyQualifiedName~SiteEngineLoggerTests\""]' false
done
assert_closure_mapping "$closure_fixture" docs/superpowers/plans/2026-08-05-bukit-seo-geo-next-master.md '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj"]' false
for plugin_split_contract_path in \
  openspec/changes/migrate-built-in-plugin-split/proposal.md \
  openspec/changes/migrate-built-in-plugin-split/design.md \
  openspec/changes/migrate-built-in-plugin-split/tasks.md \
  openspec/changes/migrate-built-in-plugin-split/specs/built-in-plugin-split/spec.md \
  openspec/changes/archive/2026-10-26-migrate-built-in-plugin-split/proposal.md \
  openspec/changes/archive/2026-10-26-migrate-built-in-plugin-split/design.md \
  openspec/changes/archive/2026-10-26-migrate-built-in-plugin-split/tasks.md \
  openspec/changes/archive/2026-10-26-migrate-built-in-plugin-split/specs/built-in-plugin-split/spec.md \
  openspec/specs/built-in-plugin-split/spec.md; do
  assert_closure_mapping "$closure_fixture" "$plugin_split_contract_path" '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj --filter \"FullyQualifiedName~BuiltInPluginSplitTests|FullyQualifiedName~G04D9EBuiltInPluginGraphTests\"", "dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter \"FullyQualifiedName~DataFilesPluginTests|FullyQualifiedName~ImageProcessingPluginTests\"", "dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj --filter FullyQualifiedName~ScribanTemplateRendererTests"]' false
done
for unmapped_remediation_path in .github/workflows/unplanned.yaml docs/governance/unplanned.md docs/superpowers/plans/unplanned.md; do
  expect_exit 0 "${tool[@]}" closure --repo "$closure_fixture" --policy scripts/checks/codex-workflow-policy.v1.json --changed "$unmapped_remediation_path"
  python3 - "$command_output" "$unmapped_remediation_path" <<'PY_CHECK'
import json
import sys
result = json.loads(sys.argv[1])
assert result["unmappedFiles"] == [sys.argv[2]], result
assert result["specialtyTests"] == [], result
PY_CHECK
done

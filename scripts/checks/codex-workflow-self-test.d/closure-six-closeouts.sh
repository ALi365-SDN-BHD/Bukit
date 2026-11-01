for labs_yaml_contract_path in \
  openspec/changes/fix-labs-theme-yaml-context/proposal.md \
  openspec/changes/fix-labs-theme-yaml-context/design.md \
  openspec/changes/fix-labs-theme-yaml-context/tasks.md \
  openspec/changes/fix-labs-theme-yaml-context/specs/labs-theme-yaml-context/spec.md \
  openspec/changes/archive/2026-10-26-fix-labs-theme-yaml-context/design.md \
  openspec/changes/archive/2026-10-26-fix-labs-theme-yaml-context/proposal.md \
  openspec/changes/archive/2026-10-26-fix-labs-theme-yaml-context/specs/labs-theme-yaml-context/spec.md \
  openspec/changes/archive/2026-10-26-fix-labs-theme-yaml-context/tasks.md \
  openspec/specs/labs-theme-yaml-context/spec.md; do
  assert_closure_mapping "$closure_fixture" "$labs_yaml_contract_path" '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter FullyQualifiedName~MetaRemovalInventoryTests", "dotnet test tests/Bukit.Labs.Cli.Tests/Bukit.Labs.Cli.Tests.csproj"]' false
done

for relocation_contract_path in \
  docs/governance/bukit-core-2.0-template-theme-relocation.md \
  guide/dev/theme.md \
  openspec/changes/relocate-template-theme/proposal.md \
  openspec/changes/relocate-template-theme/design.md \
  openspec/changes/relocate-template-theme/tasks.md \
  openspec/changes/relocate-template-theme/specs/template-theme-relocation/spec.md \
  openspec/changes/archive/2026-10-26-relocate-template-theme/design.md \
  openspec/changes/archive/2026-10-26-relocate-template-theme/proposal.md \
  openspec/changes/archive/2026-10-26-relocate-template-theme/specs/template-theme-relocation/spec.md \
  openspec/changes/archive/2026-10-26-relocate-template-theme/tasks.md \
  openspec/specs/template-theme-relocation/spec.md; do
  assert_closure_mapping "$closure_fixture" "$relocation_contract_path" '["bash scripts/checks/public-api-drift-self-test.sh", "dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj"]' false
done

for logging_contract_path in \
  openspec/changes/unify-library-logging/proposal.md \
  openspec/changes/unify-library-logging/design.md \
  openspec/changes/unify-library-logging/tasks.md \
  openspec/changes/unify-library-logging/specs/library-logging/spec.md \
  openspec/changes/archive/2026-10-26-unify-library-logging/design.md \
  openspec/changes/archive/2026-10-26-unify-library-logging/proposal.md \
  openspec/changes/archive/2026-10-26-unify-library-logging/specs/library-logging/spec.md \
  openspec/changes/archive/2026-10-26-unify-library-logging/tasks.md \
  openspec/specs/library-logging/spec.md; do
  assert_closure_mapping "$closure_fixture" "$logging_contract_path" '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter FullyQualifiedName~CommandConfigLoaderTests", "dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter \"FullyQualifiedName~LibraryDiagnosticOutputTests|FullyQualifiedName~SiteEngineLoggerTests\""]' false
done

for fix_core_report_contracts_path in \
  openspec/changes/fix-core-report-contracts/design.md \
  openspec/changes/fix-core-report-contracts/proposal.md \
  openspec/changes/fix-core-report-contracts/specs/core-report-contracts/spec.md \
  openspec/changes/fix-core-report-contracts/tasks.md \
  openspec/changes/archive/2026-10-26-fix-core-report-contracts/design.md \
  openspec/changes/archive/2026-10-26-fix-core-report-contracts/proposal.md \
  openspec/changes/archive/2026-10-26-fix-core-report-contracts/specs/core-report-contracts/spec.md \
  openspec/changes/archive/2026-10-26-fix-core-report-contracts/tasks.md; do
  assert_closure_mapping "$closure_fixture" "$fix_core_report_contracts_path" '["dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter FullyQualifiedName~SeoReportValidatorTests", "dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter \"FullyQualifiedName~TestInfrastructureTests|FullyQualifiedName~BuildReporterTests|FullyQualifiedName~SeoAuditReportWriterTests|FullyQualifiedName~PublishAuditReportWriterTests\"", "python3 -B scripts/checks/core-report-inventory.py --self-test"]' false
done
expect_exit 0 "${tool[@]}" closure --repo "$closure_fixture" --policy scripts/checks/codex-workflow-policy.v1.json --changed openspec/specs/core-report-contracts/spec.md
python3 - "$command_output" <<'PY_REPORT_SPEC'
import json
import sys
result = json.loads(sys.argv[1])
commands = set(result["specialtyTests"])
assert "dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj" in commands, commands
assert "dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj" in commands, commands
assert any("core-report-inventory" in c for c in commands), commands
PY_REPORT_SPEC

for fix_section_plugin_task_errors_path in \
  openspec/changes/fix-section-plugin-task-errors/design.md \
  openspec/changes/fix-section-plugin-task-errors/proposal.md \
  openspec/changes/fix-section-plugin-task-errors/specs/section-rendering/spec.md \
  openspec/changes/fix-section-plugin-task-errors/tasks.md \
  openspec/changes/archive/2026-10-26-fix-section-plugin-task-errors/design.md \
  openspec/changes/archive/2026-10-26-fix-section-plugin-task-errors/proposal.md \
  openspec/changes/archive/2026-10-26-fix-section-plugin-task-errors/specs/section-rendering/spec.md \
  openspec/changes/archive/2026-10-26-fix-section-plugin-task-errors/tasks.md \
  openspec/specs/section-rendering/spec.md; do
  assert_closure_mapping "$closure_fixture" "$fix_section_plugin_task_errors_path" '["dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj --filter \"FullyQualifiedName~SectionRenderExtendedTests|FullyQualifiedName~RenderSectionTests|FullyQualifiedName~ScribanTemplateRendererTests\""]' false
done

for replace_imagesharp_with_skia_tiff_path in \
  openspec/changes/replace-imagesharp-with-skia-tiff/design.md \
  openspec/changes/replace-imagesharp-with-skia-tiff/proposal.md \
  openspec/changes/replace-imagesharp-with-skia-tiff/specs/image-codec-backends/spec.md \
  openspec/changes/replace-imagesharp-with-skia-tiff/tasks.md \
  openspec/changes/archive/2026-10-26-replace-imagesharp-with-skia-tiff/design.md \
  openspec/changes/archive/2026-10-26-replace-imagesharp-with-skia-tiff/proposal.md \
  openspec/changes/archive/2026-10-26-replace-imagesharp-with-skia-tiff/specs/image-codec-backends/spec.md \
  openspec/changes/archive/2026-10-26-replace-imagesharp-with-skia-tiff/tasks.md \
  openspec/specs/image-codec-backends/spec.md; do
  assert_closure_mapping "$closure_fixture" "$replace_imagesharp_with_skia_tiff_path" '["dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj --filter \"FullyQualifiedName~ImageAssetLocalizerTests|FullyQualifiedName~ContentImageRewritePipelineTests|FullyQualifiedName~ImageContentValidatorTests\"", "dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter \"FullyQualifiedName~ImageOptimizerTests|FullyQualifiedName~ImageProcessingPluginTests|FullyQualifiedName~AssetToolingSymlinkTests|FullyQualifiedName~ContentProviderFactoryTests|FullyQualifiedName~Bukit.Engine.Tests.SiteEngineIntegrationTests.MediaLifecycle_|FullyQualifiedName~ImageMetadataReaderTests\"", "dotnet test tests/Bukit.Plugin.WechatSync.Tests/Bukit.Plugin.WechatSync.Tests.csproj"]' false
done

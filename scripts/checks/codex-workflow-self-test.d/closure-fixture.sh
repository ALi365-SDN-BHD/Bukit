# Priority 2: verification closure generation.
closure_fixture="$scratch/closure-fixture"
mkdir -p \
  "$closure_fixture/docs/governance" \
  "$closure_fixture/docs/schemas" \
  "$closure_fixture/docs/superpowers/plans" \
  "$closure_fixture/guide/dev" \
  "$closure_fixture/scripts/checks" \
  "$closure_fixture/src/Bukit-Core/Bukit.Config" \
  "$closure_fixture/src/Bukit-Core/Bukit.Cli/Deploy" \
  "$closure_fixture/src/Bukit-Core/Bukit.Cli.Shared/Cli/Rendering" \
  "$closure_fixture/src/Bukit-Core/Bukit.Content" \
  "$closure_fixture/src/Bukit-Core/Bukit.Content.Notion" \
  "$closure_fixture/src/Bukit-Core/Bukit.Cli.Shared" \
  "$closure_fixture/src/Bukit-Core/Bukit.Engine" \
  "$closure_fixture/src/Bukit-Core/Bukit.Engine.Abstractions" \
  "$closure_fixture/src/Bukit-Core/Bukit.Engine/obj/Debug" \
  "$closure_fixture/src/Bukit-Core/Bukit.Notion/Transport" \
  "$closure_fixture/src/Bukit-Core/Bukit.Plugin.Abstractions/Config" \
  "$closure_fixture/src/Bukit-Core/Bukit.Plugin.Abstractions/Manifest" \
  "$closure_fixture/src/Bukit-Core/Bukit.PluginHost" \
  "$closure_fixture/src/Bukit-Core/Bukit.Rendering" \
  "$closure_fixture/src/Bukit-Core/Bukit.Routing" \
  "$closure_fixture/src/Bukit-Core/Bukit.Shared" \
  "$closure_fixture/src/Bukit-Core/Bukit.Theme" \
  "$closure_fixture/src/Bukit-Plugins/Bukit.Importing" \
  "$closure_fixture/tests/Bukit.Architecture.Tests" \
  "$closure_fixture/tests/Bukit.Cli.Tests" \
  "$closure_fixture/tests/Bukit.Config.Tests" \
  "$closure_fixture/tests/Bukit.Config.Tests/obj/Debug" \
  "$closure_fixture/tests/Bukit.Content.Notion.Tests" \
  "$closure_fixture/tests/Bukit.Content.Tests" \
  "$closure_fixture/tests/Bukit.Engine.Tests" \
  "$closure_fixture/tests/Bukit.Engine.Abstractions.Tests" \
  "$closure_fixture/tests/Bukit.Importing.Tests" \
  "$closure_fixture/tests/Bukit.Notion.Tests" \
  "$closure_fixture/tests/Bukit.Plugin.Abstractions.Tests" \
  "$closure_fixture/tests/Bukit.Plugin.Import.Tests" \
  "$closure_fixture/tests/Bukit.PluginHost.Tests" \
  "$closure_fixture/tests/Bukit.Rendering.Tests" \
  "$closure_fixture/tests/Bukit.Routing.Tests" \
  "$closure_fixture/tests/Bukit.Shared.Tests" \
  "$closure_fixture/tests/Bukit.Theme.Tests" \
  "$closure_fixture/tests/PluginProcessProbe" \
  "$closure_fixture/tools/Bukit.PublicApiDrift"
git -C "$closure_fixture" init -q
git -C "$closure_fixture" config user.email codex-workflow@example.invalid
git -C "$closure_fixture" config user.name "Codex Workflow Self Test"
printf '%s\n' \
  'namespace Bukit.Config;' \
  'public sealed class AppConfig { public int Limit { get; init; } }' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Config/AppConfig.cs"
printf '%s\n' \
  'using Bukit.Config;' \
  'namespace Bukit.Engine;' \
  'internal sealed class ConfigConsumer { private readonly AppConfig _config = new(); }' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Engine/ConfigConsumer.cs"
printf '%s\n' \
  'using Bukit.Config;' \
  'public sealed class ConfigLoaderTests { private readonly AppConfig _config = new(); }' \
  >"$closure_fixture/tests/Bukit.Config.Tests/ConfigLoaderTests.cs"
printf 'internal sealed class GeneratedConsumer { private AppConfig? _config; }\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Engine/obj/Debug/Generated.cs"
printf 'internal sealed class GeneratedContract { private AppConfig? _config; }\n' \
  >"$closure_fixture/tests/Bukit.Config.Tests/obj/Debug/GeneratedTests.cs"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj"
printf 'internal sealed class GitProcessRunner {}\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Cli/Deploy/GitProcessRunner.cs"
printf 'internal sealed class CliHelpRenderer {}\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Cli.Shared/Cli/Rendering/CliHelpRenderer.cs"
printf 'public sealed class BodyCacheDecorator {}\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Content/BodyCacheDecorator.cs"
printf 'internal sealed class NotionBodyStore {}\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Content.Notion/NotionBodyStore.cs"
printf 'public sealed class NotionClient {}\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Notion/Transport/NotionClient.cs"
printf 'internal sealed class SystemProcessRunner {}\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.PluginHost/SystemProcessRunner.cs"
printf 'public sealed record PluginConfigEntry(bool Enabled, string Source);\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Plugin.Abstractions/Config/PluginConfigEntry.cs"
printf 'public sealed record PluginManifest(string Id);\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Plugin.Abstractions/Manifest/PluginManifest.cs"
printf 'public static class ContentDocumentFactory {}\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Engine.Abstractions/ContentDocumentFactory.cs"
printf 'public static class RoutePathBuilder {}\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Routing/RoutePathBuilder.cs"
printf '# Built-in plugins\n' >"$closure_fixture/guide/dev/built-in-plugins.md"
printf '# Content\n' >"$closure_fixture/guide/dev/content.md"
printf 'public sealed class ContentBoundaryTests {}\n' \
  >"$closure_fixture/tests/Bukit.Architecture.Tests/ContentBoundaryTests.cs"
printf 'public sealed class GitProcessRunnerTests {}\n' \
  >"$closure_fixture/tests/Bukit.Cli.Tests/GitProcessRunnerTests.cs"
printf 'public sealed class HelpPrinterTests {}\n' \
  >"$closure_fixture/tests/Bukit.Cli.Tests/HelpPrinterTests.cs"
printf 'public sealed class NotionContentSourceTests {}\n' \
  >"$closure_fixture/tests/Bukit.Content.Notion.Tests/NotionContentSourceTests.cs"
printf 'public sealed class BodyCacheDecoratorTests {}\n' \
  >"$closure_fixture/tests/Bukit.Content.Tests/BodyCacheDecoratorTests.cs"
printf 'public sealed class NotionClientTests {}\n' \
  >"$closure_fixture/tests/Bukit.Notion.Tests/NotionClientTests.cs"
printf 'public sealed class SystemProcessRunnerTests {}\n' \
  >"$closure_fixture/tests/Bukit.PluginHost.Tests/SystemProcessRunnerTests.cs"
printf 'public sealed class PluginConfigDtoTests {}\n' \
  >"$closure_fixture/tests/Bukit.Plugin.Abstractions.Tests/PluginConfigDtoTests.cs"
printf 'public sealed class PluginManifestBinaryCompatibilityTests {}\n' \
  >"$closure_fixture/tests/Bukit.Plugin.Abstractions.Tests/PluginManifestBinaryCompatibilityTests.cs"
printf 'public sealed class ImportNotionPushWorkflowTests {}\n' \
  >"$closure_fixture/tests/Bukit.Importing.Tests/ImportNotionPushWorkflowTests.cs"
printf 'public sealed class ImportPluginInvokeCompatibilityTests {}\n' \
  >"$closure_fixture/tests/Bukit.Plugin.Import.Tests/ImportPluginInvokeCompatibilityTests.cs"
printf 'public sealed class RenderingPackageTests {}\n' \
  >"$closure_fixture/tests/Bukit.Rendering.Tests/RenderingPackageTests.cs"
printf 'public sealed class RoutingPackageTests {}\n' \
  >"$closure_fixture/tests/Bukit.Routing.Tests/RoutingPackageTests.cs"
printf 'public sealed class SharedPackageTests {}\n' \
  >"$closure_fixture/tests/Bukit.Shared.Tests/SharedPackageTests.cs"
printf 'public sealed class ThemePackageTests {}\n' \
  >"$closure_fixture/tests/Bukit.Theme.Tests/ThemePackageTests.cs"
printf 'public sealed class ContentDocumentFactoryTests {}\n' \
  >"$closure_fixture/tests/Bukit.Engine.Abstractions.Tests/ContentDocumentFactoryTests.cs"
printf 'public sealed class RoutePathBuilderTests {}\n' \
  >"$closure_fixture/tests/Bukit.Routing.Tests/RoutePathBuilderTests.cs"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Content/Bukit.Content.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Content.Notion/Bukit.Content.Notion.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Cli.Shared/Bukit.Cli.Shared.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Plugin.Abstractions/Bukit.Plugin.Abstractions.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.PluginHost/Bukit.PluginHost.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Engine/Bukit.Engine.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Engine.Abstractions/Bukit.Engine.Abstractions.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Config/Bukit.Config.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Notion/Bukit.Notion.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Rendering/Bukit.Rendering.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Routing/Bukit.Routing.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Shared/Bukit.Shared.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Theme/Bukit.Theme.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Plugins/Bukit.Importing/Bukit.Importing.csproj"
printf 'public sealed class EngineFeatureTests {}\n' \
  >"$closure_fixture/tests/Bukit.Engine.Tests/EngineFeatureTests.cs"
for project in \
  Bukit.Engine.Abstractions.Tests \
  Bukit.Importing.Tests \
  Bukit.Plugin.Import.Tests \
  Bukit.Rendering.Tests \
  Bukit.Routing.Tests \
  Bukit.Shared.Tests \
  Bukit.Theme.Tests; do
  printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
    >"$closure_fixture/tests/$project/$project.csproj"
done
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' >"$closure_fixture/Directory.Packages.props"
printf 'return 0;\n' >"$closure_fixture/tests/PluginProcessProbe/Program.cs"
printf '{}\n' >"$closure_fixture/docs/governance/bukit-core-public-api-baseline.v1.json"
printf '{}\n' >"$closure_fixture/docs/schemas/bukit-core-public-api-baseline.v1.schema.json"
printf '# Public API remediation\n' \
  >"$closure_fixture/docs/superpowers/plans/2026-08-06-bukit-public-api-drift-remediation.md"
printf '# Public API governance\n' \
  >"$closure_fixture/guide/dev/public-api-governance.md"
printf '#!/usr/bin/env bash\n' \
  >"$closure_fixture/scripts/checks/public-api-drift-self-test.sh"
printf 'internal sealed record ApiPolicy(string Compatibility);\n' \
  >"$closure_fixture/tools/Bukit.PublicApiDrift/ApiSurfaceModels.cs"
printf 'unmapped\n' >"$closure_fixture/README.unknown"
git -C "$closure_fixture" add .
git -C "$closure_fixture" commit -qm initial

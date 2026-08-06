# Verify .csproj closure mapping (I-01).
# Existing non-owner project mappings retain only their established owner test.
assert_closure_mapping \
  "$closure_fixture" \
  src/Bukit-Core/Bukit.Content/Bukit.Content.csproj \
  '["dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj"]' \
  true

# Each named Core owner project and its exact test project carries Architecture
# plus its own specialty test; unrelated projects receive no generic fallback.
assert_closure_mapping \
  "$closure_fixture" \
  src/Bukit-Core/Bukit.Cli.Shared/Bukit.Cli.Shared.csproj \
  '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj"]' \
  true
assert_closure_mapping \
  "$closure_fixture" \
  tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj \
  '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  src/Bukit-Core/Bukit.Engine.Abstractions/Bukit.Engine.Abstractions.csproj \
  '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj"]' \
  true
assert_closure_mapping \
  "$closure_fixture" \
  tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj \
  '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  src/Bukit-Core/Bukit.Rendering/Bukit.Rendering.csproj \
  '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj"]' \
  true
assert_closure_mapping \
  "$closure_fixture" \
  tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj \
  '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  src/Bukit-Core/Bukit.Routing/Bukit.Routing.csproj \
  '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj", "dotnet test tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj"]' \
  true
assert_closure_mapping \
  "$closure_fixture" \
  tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj \
  '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  src/Bukit-Core/Bukit.Shared/Bukit.Shared.csproj \
  '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj"]' \
  true
assert_closure_mapping \
  "$closure_fixture" \
  tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj \
  '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  src/Bukit-Core/Bukit.Theme/Bukit.Theme.csproj \
  '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Theme.Tests/Bukit.Theme.Tests.csproj"]' \
  true
assert_closure_mapping \
  "$closure_fixture" \
  tests/Bukit.Theme.Tests/Bukit.Theme.Tests.csproj \
  '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Theme.Tests/Bukit.Theme.Tests.csproj"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  tests/Bukit.Plugin.Import.Tests/Bukit.Plugin.Import.Tests.csproj \
  '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Plugin.Import.Tests/Bukit.Plugin.Import.Tests.csproj"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  scripts/checks/codex_workflow/common.py \
  '["bash scripts/checks/codex-workflow-self-test.sh"]' \
  false
assert_closure_mapping \
  "$closure_fixture" \
  scripts/checks/codex-workflow-self-test.d/cache.sh \
  '["bash scripts/checks/codex-workflow-self-test.sh"]' \
  false


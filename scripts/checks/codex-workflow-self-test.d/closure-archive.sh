for core_five_findings_archive_path in \
  openspec/changes/archive/2026-11-01-fix-core-five-findings/proposal.md \
  openspec/changes/archive/2026-11-01-fix-core-five-findings/design.md \
  openspec/changes/archive/2026-11-01-fix-core-five-findings/tasks.md \
  openspec/changes/archive/2026-11-01-fix-core-five-findings/specs/in-process-build-transactions/spec.md \
  openspec/changes/archive/2026-11-01-fix-core-five-findings/specs/i18n-worktree-build/spec.md \
  openspec/changes/archive/2026-11-01-fix-core-five-findings/specs/core-deployment-identity/spec.md \
  openspec/changes/archive/2026-11-01-fix-core-five-findings/specs/core-report-contracts/spec.md \
  openspec/changes/archive/2026-11-01-fix-core-five-findings/specs/notion-load-resource-lifecycle/spec.md; do
  assert_closure_mapping "$closure_fixture" "$core_five_findings_archive_path" '["openspec validate --all --strict --no-interactive"]' false
done

assert_closure_mapping "$closure_fixture" openspec/specs/core-deployment-identity/spec.md '["dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj"]' false
assert_closure_mapping "$closure_fixture" openspec/specs/notion-load-resource-lifecycle/spec.md '["dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj", "dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj"]' false
expect_exit 0 "${tool[@]}" closure --repo "$closure_fixture" --policy scripts/checks/codex-workflow-policy.v1.json --changed openspec/specs/core-report-contracts/spec.md
python3 - "$command_output" <<'PY_CONTRACT'
import json
import sys
result = json.loads(sys.argv[1])
commands = set(result["specialtyTests"])
assert "dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj" in commands, commands
assert "dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj" in commands, commands
PY_CONTRACT

assert_closure_mapping "$closure_fixture" guide/user/06-notion-content.md '["dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj", "dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj"]' false
assert_closure_mapping "$closure_fixture" guide/user/13-deploy-github-pages.md '["dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj", "dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj"]' false
assert_closure_mapping "$closure_fixture" openspec/specs/in-process-build-transactions/spec.md '["dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj", "dotnet test tests/Bukit.Content.Notion.Tests/Bukit.Content.Notion.Tests.csproj", "dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj", "dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj"]' false
assert_closure_mapping "$closure_fixture" openspec/specs/i18n-worktree-build/spec.md '["dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj", "dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj"]' false
python3 - <<'PY_POLICY'
import json
policy = json.load(open("scripts/checks/codex-workflow-policy.v1.json"))
rule = next(r for r in policy["pathRules"] if r["id"] == "core-five-findings-public-surface")
baseline = json.load(open("docs/governance/bukit-core-public-api-baseline.v1.json"))
required = {
    "Directory.Build.props",
    "Directory.Packages.props",
    "global.json",
    "docs/governance/bukit-core-public-api-baseline.v1.json",
    "docs/schemas/bukit-core-public-api-baseline.v1.schema.json",
    "tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj",
}
required.update(entry["project"] for entry in baseline["assemblies"])
missing = sorted(required - set(rule["contractConsumerGlobs"]))
assert not missing, f"missing API contract inputs: {missing}"
PY_POLICY

assert_closure_mapping "$closure_fixture" src/Bukit-Core/Bukit.Engine/BuildTransaction.cs '["dotnet build src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj --configuration Debug --nologo", "dotnet run --project tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj --configuration Debug -- check docs/governance/bukit-core-public-api-baseline.v1.json \"$PWD\" Debug", "dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj"]' true

expect_exit 0 "${tool[@]}" classify --policy scripts/checks/codex-workflow-policy.v1.json --test-command "dotnet build src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj --configuration Debug --nologo" --test-command "dotnet run --project tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj --configuration Debug -- check docs/governance/bukit-core-public-api-baseline.v1.json \"$PWD\" Debug"
python3 - "$command_output" <<'PY_CHECK'
import json
import sys
result = json.loads(sys.argv[1])
serial = result["groups"]["dotnet-serial"]["commands"]
assert len(serial) == 2, result["groups"]
assert serial[0].startswith("dotnet build src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj"), serial
assert serial[1].startswith("dotnet run --project tools/Bukit.PublicApiDrift/"), serial
PY_CHECK

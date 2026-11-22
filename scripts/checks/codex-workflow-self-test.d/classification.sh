# Priority 5: test resource classification.
expect_exit 0 "${tool[@]}" classify \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --path guide/dev/testing.md \
  --path src/Bukit-Core/Bukit.Config/AppConfig.cs \
  --path tests/Bukit.Engine.Tests/Fixtures/site/build-manifest.json \
  --test-command "bash scripts/checks/agent-governance-contract.sh" \
  --test-command "dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj" \
  --test-command "bukit build --config site.yaml --output /tmp/output"
classification_output="$command_output"
python3 - "$classification_output" <<'PY'
import json
import sys

result = json.loads(sys.argv[1])
groups = result["groups"]
if groups["static-parallel"] != {
    "commands": ["bash scripts/checks/agent-governance-contract.sh"],
    "paths": ["guide/dev/testing.md"],
}:
    raise SystemExit(f"unexpected static group: {groups['static-parallel']}")
if groups["dotnet-serial"] != {
    "commands": ["dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj"],
    "paths": ["src/Bukit-Core/Bukit.Config/AppConfig.cs"],
}:
    raise SystemExit(f"unexpected dotnet group: {groups['dotnet-serial']}")
if groups["fixture-exclusive"] != {
    "commands": ["bukit build --config site.yaml --output /tmp/output"],
    "paths": ["tests/Bukit.Engine.Tests/Fixtures/site/build-manifest.json"],
}:
    raise SystemExit(f"unexpected fixture group: {groups['fixture-exclusive']}")
if [batch["class"] for batch in result["executionBatches"]] != [
    "static-parallel",
    "dotnet-serial",
    "fixture-exclusive",
]:
    raise SystemExit(f"unexpected execution batches: {result['executionBatches']}")
if result["executionBatches"][0]["parallel"] is not True:
    raise SystemExit("static batch must be parallel")
if any(batch["parallel"] for batch in result["executionBatches"][1:]):
    raise SystemExit("serialized batches unexpectedly marked parallel")
PY


# Build-invoking tool gates must serialize with other dotnet work.
expect_exit 0 "${tool[@]}" classify \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --test-command "bash scripts/checks/dotnet-format.sh" \
  --test-command "bash scripts/checks/code-analysis-ratchet.sh check" \
  --test-command "bash scripts/checks/public-api-drift.sh check Debug" \
  --test-command "dotnet build tests/Bukit.CloudBase.Tests/Bukit.CloudBase.Tests.csproj"
python3 - "$command_output" <<'PY'
import json
import sys

result = json.loads(sys.argv[1])
groups = result["groups"]
expected = sorted([
    "bash scripts/checks/code-analysis-ratchet.sh check",
    "bash scripts/checks/dotnet-format.sh",
    "bash scripts/checks/public-api-drift.sh check Debug",
    "dotnet build tests/Bukit.CloudBase.Tests/Bukit.CloudBase.Tests.csproj",
])
if groups["dotnet-serial"]["commands"] != expected:
    raise SystemExit(f"tool gates not serialized: {groups['dotnet-serial']}")
if groups["static-parallel"]["commands"]:
    raise SystemExit(f"tool gates unexpectedly parallel: {groups['static-parallel']}")
PY

#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

tool=(python3 scripts/checks/codex-workflow.py)
scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-codex-workflow.XXXXXX")"
lock_holder_pid=""

cleanup() {
  if [[ -n "$lock_holder_pid" ]] && kill -0 "$lock_holder_pid" 2>/dev/null; then
    kill -KILL "$lock_holder_pid" 2>/dev/null || true
    wait "$lock_holder_pid" 2>/dev/null || true
  fi
  rm -rf "$scratch"
}
trap cleanup EXIT

fail() {
  echo "codex workflow self-test failed: $*" >&2
  exit 1
}

assert_contains() {
  case "$1" in *"$2"*) ;; *) fail "expected output to contain: $2" ;; esac
}

expect_exit() {
  local expected="$1"
  shift
  set +e
  command_output="$("$@" 2>&1)"
  command_status=$?
  set -e
  [[ "$command_status" == "$expected" ]] ||
    fail "expected exit $expected, got $command_status: $command_output"
}

assert_closure_mapping() {
  local repo="$1"
  local changed="$2"
  local expected_commands_json="$3"
  local expected_public_contract="$4"

  expect_exit 0 "${tool[@]}" closure \
    --repo "$repo" \
    --policy scripts/checks/codex-workflow-policy.v1.json \
    --changed "$changed"

  python3 - "$command_output" "$changed" "$expected_commands_json" "$expected_public_contract" <<'PY'
import json
import sys

result = json.loads(sys.argv[1])
changed = sys.argv[2]
expected_commands = json.loads(sys.argv[3])
expected_public_contract = sys.argv[4] == "true"

if changed in result["unmappedFiles"]:
    raise SystemExit(f"expected mapped closure path, got unmapped: {changed}")
if result["specialtyTests"] != expected_commands:
    raise SystemExit(
        f"unexpected specialty tests for {changed}: {result['specialtyTests']}"
    )
expected_contract_files = [changed] if expected_public_contract else []
if result["publicContractFiles"] != expected_contract_files:
    raise SystemExit(
        f"unexpected public contract files for {changed}: "
        f"{result['publicContractFiles']}"
    )
PY
}

fixture="$scratch/cache-fixture"
record="$scratch/cache-evidence.json"
mkdir -p "$fixture/src"
git -C "$fixture" init -q
git -C "$fixture" config user.email codex-workflow@example.invalid
git -C "$fixture" config user.name "Codex Workflow Self Test"
printf 'alpha\n' >"$fixture/src/item.txt"
ln -s item.txt "$fixture/src/link.txt"
git -C "$fixture" add src/item.txt
git -C "$fixture" add src/link.txt
git -C "$fixture" commit -qm initial

cache_common=(
  --repo "$fixture"
  --record "$record"
  --base HEAD
  --command "dotnet test tests/Example.Tests/Example.Tests.csproj"
  --sdk-version "10.0.100"
  --env BUKIT_CODEX_WORKFLOW_TEST_STATE
  --path src/item.txt
  --path ./src/item.txt
  --path src/link.txt
)

BUKIT_CODEX_WORKFLOW_TEST_STATE=present-value "${tool[@]}" cache record \
  "${cache_common[@]}" \
  --result passed \
  --exit-code 0 \
  --duration-ms 125

cache_output="$(
  BUKIT_CODEX_WORKFLOW_TEST_STATE=another-value \
    "${tool[@]}" cache check "${cache_common[@]}"
)"
assert_contains "$cache_output" "CACHE HIT"

if grep -Fq "present-value" "$record" ||
  grep -Fq "another-value" "$record"; then
  fail "cache record leaked an environment-variable value"
fi

printf 'beta\n' >"$fixture/src/item.txt"
expect_exit 1 env BUKIT_CODEX_WORKFLOW_TEST_STATE=present-value \
  "${tool[@]}" cache check "${cache_common[@]}"
assert_contains "$command_output" "closure"
printf 'alpha\n' >"$fixture/src/item.txt"

expect_exit 1 env BUKIT_CODEX_WORKFLOW_TEST_STATE=present-value \
  "${tool[@]}" cache check \
  --repo "$fixture" \
  --record "$record" \
  --base HEAD \
  --command "dotnet test tests/Other.Tests/Other.Tests.csproj" \
  --sdk-version "10.0.100" \
  --env BUKIT_CODEX_WORKFLOW_TEST_STATE \
  --path src/item.txt
assert_contains "$command_output" "command"

expect_exit 1 env -u BUKIT_CODEX_WORKFLOW_TEST_STATE \
  "${tool[@]}" cache check "${cache_common[@]}"
assert_contains "$command_output" "environment"

expect_exit 1 env BUKIT_CODEX_WORKFLOW_TEST_STATE=present-value \
  "${tool[@]}" cache check \
  --repo "$fixture" \
  --record "$record" \
  --base HEAD \
  --command "dotnet test tests/Example.Tests/Example.Tests.csproj" \
  --sdk-version "10.0.101" \
  --env BUKIT_CODEX_WORKFLOW_TEST_STATE \
  --path src/item.txt
assert_contains "$command_output" "sdk"

failed_record="$scratch/failed-evidence.json"
BUKIT_CODEX_WORKFLOW_TEST_STATE=present-value "${tool[@]}" cache record \
  --repo "$fixture" \
  --record "$failed_record" \
  --base HEAD \
  --command "dotnet test tests/Example.Tests/Example.Tests.csproj" \
  --sdk-version "10.0.100" \
  --env BUKIT_CODEX_WORKFLOW_TEST_STATE \
  --path src/item.txt \
  --result failed \
  --exit-code 1 \
  --duration-ms 20
expect_exit 1 env BUKIT_CODEX_WORKFLOW_TEST_STATE=present-value \
  "${tool[@]}" cache check \
  --repo "$fixture" \
  --record "$failed_record" \
  --base HEAD \
  --command "dotnet test tests/Example.Tests/Example.Tests.csproj" \
  --sdk-version "10.0.100" \
  --env BUKIT_CODEX_WORKFLOW_TEST_STATE \
  --path src/item.txt
assert_contains "$command_output" "result"

python3 - "$record" <<'PY'
import json
import pathlib
import sys

path = pathlib.Path(sys.argv[1])
raw = path.read_bytes()
if not raw.endswith(b"\n"):
    raise SystemExit("cache record must end with a newline")
record = json.loads(raw)
if record.get("schemaVersion") != 1:
    raise SystemExit("cache record must declare schemaVersion 1")
if record["fingerprintInputs"]["environment"] != {
    "BUKIT_CODEX_WORKFLOW_TEST_STATE": "set"
}:
    raise SystemExit("cache record must store environment state only")
closure = record["fingerprintInputs"]["closure"]
link = next((item for item in closure if item["path"] == "src/link.txt"), None)
if link is None or link["kind"] != "symlink":
    raise SystemExit(f"cache must fingerprint the lexical symlink path: {closure}")
PY

protected_file="$scratch/protected-cache-target.txt"
symlink_record="$scratch/symlink-cache-record.json"
printf 'protected\n' >"$protected_file"
ln -s "$protected_file" "$symlink_record"
expect_exit 2 env BUKIT_CODEX_WORKFLOW_TEST_STATE=present-value \
  "${tool[@]}" cache record \
  --repo "$fixture" \
  --record "$symlink_record" \
  --base HEAD \
  --command "dotnet test tests/Example.Tests/Example.Tests.csproj" \
  --sdk-version "10.0.100" \
  --env BUKIT_CODEX_WORKFLOW_TEST_STATE \
  --path src/item.txt \
  --result passed \
  --exit-code 0 \
  --duration-ms 1
[[ "$(cat "$protected_file")" == "protected" ]] ||
  fail "cache record followed a symlink and replaced its target"

malformed_record="$scratch/malformed-cache-record.json"
printf '%s\n' \
  '{"durationMs":1,"exitCode":0,"fingerprint":"44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a","fingerprintInputs":{},"recordedAt":"2026-07-26T00:00:00Z","result":"passed","schemaVersion":1}' \
  >"$malformed_record"
expect_exit 2 env BUKIT_CODEX_WORKFLOW_TEST_STATE=present-value \
  "${tool[@]}" cache check \
  --repo "$fixture" \
  --record "$malformed_record" \
  --base HEAD \
  --command "dotnet test tests/Example.Tests/Example.Tests.csproj" \
  --sdk-version "10.0.100" \
  --env BUKIT_CODEX_WORKFLOW_TEST_STATE \
  --path src/item.txt
assert_contains "$command_output" "fingerprintInputs"

# Priority 2: verification closure generation.
closure_fixture="$scratch/closure-fixture"
mkdir -p \
  "$closure_fixture/.github/workflows" \
  "$closure_fixture/guide/dev" \
  "$closure_fixture/src/Bukit-Core/Bukit.Config" \
  "$closure_fixture/src/Bukit-Core/Bukit.Cli/Deploy" \
  "$closure_fixture/src/Bukit-Core/Bukit.Content" \
  "$closure_fixture/src/Bukit-Core/Bukit.Content.Notion" \
  "$closure_fixture/src/Bukit-Core/Bukit.Engine" \
  "$closure_fixture/src/Bukit-Core/Bukit.Engine.Abstractions" \
  "$closure_fixture/src/Bukit-Core/Bukit.Engine/obj/Debug" \
  "$closure_fixture/src/Bukit-Core/Bukit.Notion/Transport" \
  "$closure_fixture/src/Bukit-Core/Bukit.Plugin.Abstractions" \
  "$closure_fixture/src/Bukit-Core/Bukit.PluginHost" \
  "$closure_fixture/src/Bukit-Core/Bukit.Routing" \
  "$closure_fixture/tests/Bukit.Architecture.Tests" \
  "$closure_fixture/tests/Bukit.Cli.Tests" \
  "$closure_fixture/tests/Bukit.Config.Tests" \
  "$closure_fixture/tests/Bukit.Config.Tests/obj/Debug" \
  "$closure_fixture/tests/Bukit.Content.Notion.Tests" \
  "$closure_fixture/tests/Bukit.Content.Tests" \
  "$closure_fixture/tests/Bukit.Engine.Abstractions.Tests" \
  "$closure_fixture/tests/Bukit.Engine.Tests" \
  "$closure_fixture/tests/Bukit.Notion.Tests" \
  "$closure_fixture/tests/Bukit.PluginHost.Tests" \
  "$closure_fixture/tests/Bukit.Routing.Tests" \
  "$closure_fixture/tests/PluginProcessProbe"
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
printf 'public sealed class BodyCacheDecorator {}\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Content/BodyCacheDecorator.cs"
printf 'internal sealed class NotionBodyStore {}\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Content.Notion/NotionBodyStore.cs"
printf 'public sealed class NotionClient {}\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Notion/Transport/NotionClient.cs"
printf 'internal sealed class SystemProcessRunner {}\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.PluginHost/SystemProcessRunner.cs"
printf 'public static class ContentDocumentFactory {}\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Engine.Abstractions/ContentDocumentFactory.cs"
printf 'public static class RoutePathBuilder {}\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Routing/RoutePathBuilder.cs"
printf 'name: Core close Windows verification\n' \
  >"$closure_fixture/.github/workflows/core-close-windows-verification.yml"
printf '# Built-in plugins\n' >"$closure_fixture/guide/dev/built-in-plugins.md"
printf '# Content\n' >"$closure_fixture/guide/dev/content.md"
printf 'public sealed class ContentBoundaryTests {}\n' \
  >"$closure_fixture/tests/Bukit.Architecture.Tests/ContentBoundaryTests.cs"
printf 'public sealed class GitProcessRunnerTests {}\n' \
  >"$closure_fixture/tests/Bukit.Cli.Tests/GitProcessRunnerTests.cs"
printf 'public sealed class NotionContentSourceTests {}\n' \
  >"$closure_fixture/tests/Bukit.Content.Notion.Tests/NotionContentSourceTests.cs"
printf 'public sealed class BodyCacheDecoratorTests {}\n' \
  >"$closure_fixture/tests/Bukit.Content.Tests/BodyCacheDecoratorTests.cs"
printf 'public sealed class NotionClientTests {}\n' \
  >"$closure_fixture/tests/Bukit.Notion.Tests/NotionClientTests.cs"
printf 'public sealed class SystemProcessRunnerTests {}\n' \
  >"$closure_fixture/tests/Bukit.PluginHost.Tests/SystemProcessRunnerTests.cs"
printf 'public sealed class ContentDocumentFactoryTests {}\n' \
  >"$closure_fixture/tests/Bukit.Engine.Abstractions.Tests/ContentDocumentFactoryTests.cs"
printf 'public sealed class RoutePathBuilderTests {}\n' \
  >"$closure_fixture/tests/Bukit.Routing.Tests/RoutePathBuilderTests.cs"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Content/Bukit.Content.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Plugin.Abstractions/Bukit.Plugin.Abstractions.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Engine/Bukit.Engine.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Engine.Abstractions/Bukit.Engine.Abstractions.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Routing/Bukit.Routing.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Config/Bukit.Config.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj"
printf 'public sealed class EngineFeatureTests {}\n' \
  >"$closure_fixture/tests/Bukit.Engine.Tests/EngineFeatureTests.cs"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' \
  >"$closure_fixture/tests/Bukit.Routing.Tests/Bukit.Routing.Tests.csproj"
printf '<Project Sdk="Microsoft.NET.Sdk" />\n' >"$closure_fixture/Directory.Packages.props"
printf 'return 0;\n' >"$closure_fixture/tests/PluginProcessProbe/Program.cs"
printf 'unmapped\n' >"$closure_fixture/README.unknown"
git -C "$closure_fixture" add .
git -C "$closure_fixture" commit -qm initial

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

assert_closure_mapping \
  "$closure_fixture" \
  src/Bukit-Core/Bukit.PluginHost/SystemProcessRunner.cs \
  '["dotnet test tests/Bukit.PluginHost.Tests/Bukit.PluginHost.Tests.csproj"]' \
  true
assert_closure_mapping \
  "$closure_fixture" \
  src/Bukit-Core/Bukit.Engine.Abstractions/ContentDocumentFactory.cs \
  '["dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj", "dotnet test tests/Bukit.Engine.Abstractions.Tests/Bukit.Engine.Abstractions.Tests.csproj"]' \
  true
assert_closure_mapping \
  "$closure_fixture" \
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
assert_closure_mapping \
  "$closure_fixture" \
  .github/workflows/core-close-windows-verification.yml \
  '["bash scripts/checks/active-workflow-boundary-self-test.sh", "bash scripts/checks/active-workflow-boundary.sh"]' \
  false
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

# Verify .csproj closure mapping (I-01).
assert_closure_mapping \
  "$closure_fixture" \
  src/Bukit-Core/Bukit.Content/Bukit.Content.csproj \
  '["dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj"]' \
  true

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
    "dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj",
    "dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj",
]
if result["specialtyTests"] != expected_tests:
    raise SystemExit(
        f"unexpected specialty tests for Directory.Packages.props: "
        f"{result['specialtyTests']}"
    )
expected_consumers = sorted([
    "tests/Bukit.Architecture.Tests/ContentBoundaryTests.cs",
    "tests/Bukit.Cli.Tests/GitProcessRunnerTests.cs",
    "tests/Bukit.Config.Tests/ConfigLoaderTests.cs",
    "tests/Bukit.Content.Tests/BodyCacheDecoratorTests.cs",
    "tests/Bukit.Engine.Tests/EngineFeatureTests.cs",
])
if result["contractConsumers"] != expected_consumers:
    raise SystemExit(
        f"unexpected contract consumers for Directory.Packages.props: "
        f"{result['contractConsumers']}"
    )
PY

# Priority 3: delta-only final review scope.
evidence_a="$scratch/evidence-a.json"
evidence_b="$scratch/evidence-b.json"
findings="$scratch/findings.json"
printf '%s\n' \
  '{' \
  '  "cacheStatus": "hit",' \
  '  "closureFiles": ["src/A.cs", "src/Shared.cs"],' \
  '  "publicContractFiles": ["src/A.cs"],' \
  '  "schemaVersion": 1,' \
  '  "taskId": "task-a"' \
  '}' >"$evidence_a"
printf '%s\n' \
  '{' \
  '  "cacheStatus": "miss",' \
  '  "closureFiles": ["src/B.cs", "src/Shared.cs"],' \
  '  "publicContractFiles": [],' \
  '  "schemaVersion": 1,' \
  '  "taskId": "task-b"' \
  '}' >"$evidence_b"
printf '%s\n' \
  '{' \
  '  "findings": [' \
  '    {"files": ["src/B.cs"], "id": "IMP-1", "severity": "Important", "status": "open"},' \
  '    {"files": ["src/Minor.cs"], "id": "MIN-1", "severity": "Minor", "status": "open"}' \
  '  ],' \
  '  "schemaVersion": 1' \
  '}' >"$findings"

expect_exit 0 "${tool[@]}" review-scope \
  --evidence "$evidence_a" \
  --evidence "$evidence_b" \
  --findings "$findings" \
  --changed src/A.cs \
  --changed src/B.cs \
  --changed src/Shared.cs \
  --changed src/Uncovered.cs
review_output="$command_output"

python3 - "$review_output" <<'PY'
import json
import sys

result = json.loads(sys.argv[1])
if result["reusableEvidence"] != ["task-a"]:
    raise SystemExit(f"unexpected reusable evidence: {result['reusableEvidence']}")
if result["invalidatedEvidence"] != ["task-b"]:
    raise SystemExit(f"unexpected invalidated evidence: {result['invalidatedEvidence']}")
if result["crossTaskIntersections"] != [
    {"file": "src/Shared.cs", "tasks": ["task-a", "task-b"]}
]:
    raise SystemExit(f"unexpected intersections: {result['crossTaskIntersections']}")
if result["uncoveredChangedFiles"] != [
    "src/B.cs",
    "src/Uncovered.cs",
]:
    raise SystemExit(f"unexpected uncovered files: {result['uncoveredChangedFiles']}")
if result["publicContractFocus"] != ["src/A.cs"]:
    raise SystemExit(f"unexpected contract focus: {result['publicContractFocus']}")
if result["openBlockingFindings"] != [
    {
        "files": ["src/B.cs"],
        "id": "IMP-1",
        "severity": "Important",
        "status": "open",
    }
]:
    raise SystemExit(f"unexpected blocking findings: {result['openBlockingFindings']}")
if "src/Minor.cs" in result["reviewFiles"]:
    raise SystemExit("Minor finding unexpectedly expanded final review scope")
expected_review_files = ["src/A.cs", "src/B.cs", "src/Shared.cs", "src/Uncovered.cs"]
if result["reviewFiles"] != expected_review_files:
    raise SystemExit(f"unexpected review files: {result['reviewFiles']}")
PY

# Priority 4: single-writer queue.
queue_state="$scratch/writer-queue.json"
expect_exit 0 "${tool[@]}" queue init --state "$queue_state"
assert_contains "$command_output" "QUEUE INITIALIZED"

expect_exit 0 "${tool[@]}" queue acquire --state "$queue_state" --task task-a
assert_contains "$command_output" "QUEUE ACQUIRED task-a"

expect_exit 1 "${tool[@]}" queue acquire --state "$queue_state" --task task-b
assert_contains "$command_output" "active task-a"

expect_exit 2 "${tool[@]}" queue transition \
  --state "$queue_state" --task task-a --to done
assert_contains "$command_output" "invalid transition"

expect_exit 0 "${tool[@]}" queue transition \
  --state "$queue_state" --task task-a --to testing
expect_exit 0 "${tool[@]}" queue transition \
  --state "$queue_state" --task task-a --to review_wait
expect_exit 0 "${tool[@]}" queue transition \
  --state "$queue_state" --task task-a --to done

expect_exit 0 "${tool[@]}" queue acquire --state "$queue_state" --task task-b
expect_exit 0 "${tool[@]}" queue status --state "$queue_state"
queue_output="$command_output"
python3 - "$queue_output" <<'PY'
import json
import sys

result = json.loads(sys.argv[1])
if result["activeTask"] != "task-b":
    raise SystemExit(f"unexpected active task: {result['activeTask']}")
if result["tasks"] != {"task-a": "done", "task-b": "writing"}:
    raise SystemExit(f"unexpected task states: {result['tasks']}")
if result["schemaVersion"] != 1:
    raise SystemExit("queue state must declare schemaVersion 1")
PY

corrupt_queue="$scratch/corrupt-writer-queue.json"
printf '%s\n' \
  '{"activeTask":"task-a","schemaVersion":1,"tasks":{"task-a":"testing","task-b":"review_wait"}}' \
  >"$corrupt_queue"
expect_exit 2 "${tool[@]}" queue status --state "$corrupt_queue"
assert_contains "$command_output" "multiple non-terminal tasks"

interleaved_queue="$scratch/interleaved-writer-queue.json"
expect_exit 0 "${tool[@]}" queue init --state "$interleaved_queue"
expect_exit 0 "${tool[@]}" queue acquire \
  --state "$interleaved_queue" --task blocked-task
expect_exit 0 "${tool[@]}" queue transition \
  --state "$interleaved_queue" --task blocked-task --to blocked
expect_exit 0 "${tool[@]}" queue acquire \
  --state "$interleaved_queue" --task active-task
expect_exit 0 "${tool[@]}" queue transition \
  --state "$interleaved_queue" --task blocked-task --to done
expect_exit 0 "${tool[@]}" queue status --state "$interleaved_queue"
python3 - "$command_output" <<'PY'
import json
import sys

result = json.loads(sys.argv[1])
if result["activeTask"] != "active-task":
    raise SystemExit("completing a blocked task released another active task")
PY

stale_lock_queue="$scratch/stale-lock-queue.json"
expect_exit 0 "${tool[@]}" queue init --state "$stale_lock_queue"
printf 'dead-owner\n' >"${stale_lock_queue}.lock"
expect_exit 0 "${tool[@]}" queue acquire \
  --state "$stale_lock_queue" --task recovered-task
assert_contains "$command_output" "QUEUE ACQUIRED recovered-task"

live_lock_queue="$scratch/live-lock-queue.json"
live_lock_ready="$scratch/live-lock-ready"
expect_exit 0 "${tool[@]}" queue init --state "$live_lock_queue"
python3 - "${live_lock_queue}.lock" "$live_lock_ready" <<'PY' &
import fcntl
import pathlib
import signal
import sys

with open(sys.argv[1], "a+", encoding="utf-8") as handle:
    fcntl.flock(handle.fileno(), fcntl.LOCK_EX)
    pathlib.Path(sys.argv[2]).write_text("ready\n", encoding="utf-8")
    signal.pause()
PY
lock_holder_pid=$!
for _ in $(seq 1 100); do
  [[ -f "$live_lock_ready" ]] && break
  sleep 0.01
done
[[ -f "$live_lock_ready" ]] || fail "live lock holder did not become ready"

expect_exit 1 "${tool[@]}" queue acquire \
  --state "$live_lock_queue" --task live-lock-task
assert_contains "$command_output" "QUEUE BUSY"

kill -KILL "$lock_holder_pid" 2>/dev/null || true
wait "$lock_holder_pid" 2>/dev/null || true
lock_holder_pid=""
expect_exit 0 "${tool[@]}" queue acquire \
  --state "$live_lock_queue" --task live-lock-task
assert_contains "$command_output" "QUEUE ACQUIRED live-lock-task"

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

# Priority 6: speed metrics without raw commands.
metrics_state="$scratch/speed-metrics.json"
expect_exit 0 "${tool[@]}" metrics add \
  --state "$metrics_state" --task task-a --phase implementation \
  --duration-ms 100 --cache-status none --status completed
expect_exit 0 "${tool[@]}" metrics add \
  --state "$metrics_state" --task task-a --phase test \
  --duration-ms 50 --cache-status miss --command-label config-tests \
  --status completed
expect_exit 0 "${tool[@]}" metrics add \
  --state "$metrics_state" --task task-a --phase test \
  --duration-ms 40 --cache-status hit --command-label config-tests \
  --rerun --status completed
expect_exit 0 "${tool[@]}" metrics add \
  --state "$metrics_state" --task task-a --phase review \
  --duration-ms 20 --cache-status none --status completed
expect_exit 0 "${tool[@]}" metrics add \
  --state "$metrics_state" --task task-b --phase idle \
  --duration-ms 10 --cache-status none --conflict --status blocked

expect_exit 0 "${tool[@]}" metrics report --state "$metrics_state"
metrics_output="$command_output"
python3 - "$metrics_output" "$metrics_state" <<'PY'
import json
import pathlib
import sys

report = json.loads(sys.argv[1])
state_text = pathlib.Path(sys.argv[2]).read_text(encoding="utf-8")
if "dotnet test" in state_text or "--config" in state_text:
    raise SystemExit("metrics state unexpectedly stored a raw command")
if report["phaseDurationsMs"] != {
    "idle": 10,
    "implementation": 100,
    "review": 20,
    "test": 90,
}:
    raise SystemExit(f"unexpected phase totals: {report['phaseDurationsMs']}")
if report["cache"] != {"hitRate": 0.5, "hits": 1, "misses": 1}:
    raise SystemExit(f"unexpected cache metrics: {report['cache']}")
if report["duplicateCommandLabels"] != [{"count": 2, "label": "config-tests"}]:
    raise SystemExit(
        f"unexpected duplicate labels: {report['duplicateCommandLabels']}"
    )
if report["rerunCount"] != 1 or report["conflictCount"] != 1:
    raise SystemExit("unexpected rerun or conflict count")
if report["taskTotalsMs"] != {"task-a": 210, "task-b": 10}:
    raise SystemExit(f"unexpected task totals: {report['taskTotalsMs']}")
if report["statusCounts"] != {"blocked": 1, "completed": 4}:
    raise SystemExit(f"unexpected status counts: {report['statusCounts']}")
if report["eventCount"] != 5 or report["schemaVersion"] != 1:
    raise SystemExit("unexpected metrics event count or schema version")
PY

invalid_metrics="$scratch/invalid-metrics.json"
printf '%s\n' \
  '{"events":[{"cacheStatus":"hit","commandLabel":null,"conflict":false,"durationMs":"secret","phase":"test","rerun":false,"status":"completed","taskId":"task-a"}],"schemaVersion":1}' \
  >"$invalid_metrics"
expect_exit 2 "${tool[@]}" metrics report --state "$invalid_metrics"
assert_contains "$command_output" "metrics event 0"

echo "codex workflow self-test OK"

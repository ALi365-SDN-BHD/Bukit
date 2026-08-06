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


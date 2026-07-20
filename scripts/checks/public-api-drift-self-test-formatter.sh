#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

fail() { echo "public API drift formatter self-test failed: $*" >&2; exit 1; }
assert_exit() {
  local expected="$1" output="$2"; shift 2
  local status=0
  "$@" >"$output" 2>&1 || status=$?
  [[ "$status" == "$expected" ]] || fail "expected exit $expected, got $status: $(tr '\n' ' ' <"$output")"
}

[[ $# == 1 && -d "$1" ]] || fail "expected owned scratch directory"
scratch="$1"
tool_project="tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj"
fixtures="tests/fixtures/public-api-drift"
formatter_project="$fixtures/formatter/FormatterFixture.csproj"
formatter_v2_project="$fixtures/formatter-v2/FormatterFixtureV2.csproj"
identity_v1_project="$fixtures/identity-v1/IdentityContractV1.csproj"
identity_consumer_project="$fixtures/identity-consumer/IdentityConsumer.csproj"
package_free_projects=("$tool_project" "$formatter_project" "$formatter_v2_project" "$identity_v1_project" "$identity_consumer_project")
for project in "${package_free_projects[@]}"; do
  name="$(basename "${project%.csproj}")"
  assert_exit 0 "$scratch/$name-restore.txt" dotnet restore "$project" --nologo
done
assert_exit 0 "$scratch/tool-build.txt" dotnet build "$tool_project" -c Release --no-restore --nologo
assert_exit 0 "$scratch/formatter-build.txt" dotnet build "$formatter_project" -c Release --no-restore --nologo
assert_exit 0 "$scratch/formatter-v2-build.txt" dotnet build "$formatter_v2_project" -c Release --no-restore --nologo
assert_exit 0 "$scratch/identity-v1-build.txt" dotnet build "$identity_v1_project" -c Release --no-restore --nologo
assert_exit 0 "$scratch/identity-consumer-build.txt" dotnet build "$identity_consumer_project" -c Release --no-restore --nologo

formatter_candidate="$scratch/formatter-candidate.json"
assert_exit 0 "$scratch/formatter-snapshot.txt" dotnet run \
  --project "$tool_project" -c Release --no-build --no-restore -- \
  fixture-snapshot "$fixtures/formatter-policy.json" "$formatter_candidate" "$PWD" Release
jq -e '.types[] | select(.name == "Bukit.PublicApiDrift.FormatterFixture.AccessorDerived") |
  .publicMembers | index("public virtual final event System.EventHandler? Changed { add; remove; }") != null' \
  "$formatter_candidate" >/dev/null || fail "sealed event accessors lack final state"
jq -e '.types[] | select(.name == "Bukit.PublicApiDrift.FormatterFixture.AccessorDerived") |
  .publicMembers | index("public virtual final System.String! Mixed { get; }") != null' \
  "$formatter_candidate" >/dev/null || fail "public property surface includes non-public accessors"
jq -e '.types[] | select(.name == "Bukit.PublicApiDrift.FormatterFixture.AccessorDerived") |
  .publicMembers | all((contains("Mixed") and contains("protected set;")) | not)' \
  "$formatter_candidate" >/dev/null || fail "public property surface retained protected setter"
jq -e '.types[] | select(.name == "Bukit.PublicApiDrift.FormatterFixture.AccessorDerived") |
  .protectedMembers | index("protected virtual final System.String! Mixed { set; }") != null' \
  "$formatter_candidate" >/dev/null || fail "protected property surface includes public accessors"
jq -e '.types[] | select(.name == "Bukit.PublicApiDrift.FormatterFixture.AccessorDerived") |
  .protectedMembers | all((contains("Mixed") and contains("get;")) | not)' \
  "$formatter_candidate" >/dev/null || fail "protected property surface retained public getter"
jq -e '.types[] | select(.name == "Bukit.PublicApiDrift.FormatterFixture.FixtureEnum") |
  .publicMembers | index("public const Bukit.PublicApiDrift.FormatterFixture.FixtureEnum Ready = Bukit.PublicApiDrift.FormatterFixture.FixtureEnum.Ready [value=1]") != null' \
  "$formatter_candidate" >/dev/null || fail "enum field lacks fully qualified identity and invariant numeric value"
jq -e '.types[] | select(.name == "Bukit.PublicApiDrift.FormatterFixture.ClassConstraint`1") |
  .signature == "public class Bukit.PublicApiDrift.FormatterFixture.ClassConstraint<T> where T : class"' \
  "$formatter_candidate" >/dev/null || fail "class constraint is not canonical"
jq -e '.types[] | select(.name == "Bukit.PublicApiDrift.FormatterFixture.NullableClassConstraint`1") |
  .signature == "public class Bukit.PublicApiDrift.FormatterFixture.NullableClassConstraint<T> where T : class?"' \
  "$formatter_candidate" >/dev/null || fail "class? constraint is not distinguished from class"
jq -e '.types[] | select(.name == "Bukit.PublicApiDrift.FormatterFixture.StructConstraint`1") |
  .signature == "public class Bukit.PublicApiDrift.FormatterFixture.StructConstraint<T> where T : struct"' \
  "$formatter_candidate" >/dev/null || fail "struct constraint is not canonical"
jq -e '.types[] | select(.name == "Bukit.PublicApiDrift.FormatterFixture.UnmanagedConstraint`1") |
  .signature == "public class Bukit.PublicApiDrift.FormatterFixture.UnmanagedConstraint<T> where T : unmanaged"' \
  "$formatter_candidate" >/dev/null || fail "unmanaged constraint is not distinguished from struct"
jq -e '.types[] | select(.name == "Bukit.PublicApiDrift.FormatterFixture.Unconstrained`1") |
  .signature == "public class Bukit.PublicApiDrift.FormatterFixture.Unconstrained<T>"' \
  "$formatter_candidate" >/dev/null || fail "unconstrained generic parameter gained a constraint"
jq -e '.types[] | select(.name == "Bukit.PublicApiDrift.FormatterFixture.NotNullConstraint`1") |
  .signature == "public class Bukit.PublicApiDrift.FormatterFixture.NotNullConstraint<T> where T : notnull"' \
  "$formatter_candidate" >/dev/null || fail "notnull constraint is not distinguished from unconstrained"

formatter_v2_candidate="$scratch/formatter-v2-candidate.json"
assert_exit 0 "$scratch/formatter-v2-snapshot.txt" dotnet run \
  --project "$tool_project" -c Release --no-build --no-restore -- \
  fixture-snapshot "$fixtures/formatter-v2-policy.json" "$formatter_v2_candidate" "$PWD" Release
tool=(dotnet run --project "$tool_project" -c Release --no-build --no-restore -- compare)
assert_exit 1 "$scratch/enum-value-drift.txt" "${tool[@]}" "$formatter_candidate" "$formatter_v2_candidate"
grep -Fq 'breaking: bukit::Bukit.PublicApiDrift.FormatterFixture.FixtureEnum: public member removed: public const Bukit.PublicApiDrift.FormatterFixture.FixtureEnum Ready = Bukit.PublicApiDrift.FormatterFixture.FixtureEnum.Ready [value=1]' \
  "$scratch/enum-value-drift.txt" || fail "enum value-only drift lacks the prior numeric signature"
grep -Fq 'review-required: bukit::Bukit.PublicApiDrift.FormatterFixture.FixtureEnum: public member added: public const Bukit.PublicApiDrift.FormatterFixture.FixtureEnum Ready = Bukit.PublicApiDrift.FormatterFixture.FixtureEnum.Ready [value=2]' \
  "$scratch/enum-value-drift.txt" || fail "enum value-only drift lacks the new numeric signature"

identity_root="$scratch/identity-root"
identity_relative="tests/fixtures/public-api-drift/identity-consumer"
identity_output="$identity_root/$identity_relative/bin/Release/net10.0"
mkdir -p "$identity_output"
/bin/cp -R "$fixtures/identity-consumer/bin/Release/net10.0/." "$identity_output"
/bin/cp "$fixtures/identity-v1/bin/Release/net10.0/Bukit.PublicApiDrift.IdentityContract.dll" "$identity_output"
assert_exit 2 "$scratch/identity-mismatch.txt" dotnet run \
  --project "$tool_project" -c Release --no-build --no-restore -- \
  fixture-snapshot "$fixtures/identity-policy.json" "$scratch/identity-candidate.json" "$identity_root" Release
grep -Fq 'dependency assembly identity mismatch:' "$scratch/identity-mismatch.txt" || fail "dependency mismatch lacks exact identity diagnostic"

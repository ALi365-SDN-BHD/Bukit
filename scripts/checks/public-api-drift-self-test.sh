#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

fail() { echo "public API drift self-test failed: $*" >&2; exit 1; }
tool_project="tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj"
fixtures="tests/fixtures/public-api-drift"
tool=(dotnet run --project "$tool_project" -c Release --no-build --no-restore -- compare)

assert_exit() {
  local expected="$1" output="$2"; shift 2
  local status=0
  "$@" >"$output" 2>&1 || status=$?
  [[ "$status" == "$expected" ]] || fail "expected exit $expected, got $status: $(tr '\n' ' ' <"$output")"
}

self_test="${BASH_SOURCE[0]}"
real_wrapper_check='bash scripts/checks/public-api-drift.sh check'
if grep -Fq "$real_wrapper_check Release" "$self_test" ||
   grep -Eq -- '-- (check|snapshot) "\$baseline"' "$self_test"; then
  fail "ci-fast self-test must not execute a real Core check or snapshot"
fi
implicit_fixture_builds="$(grep -E 'dotnet build "\$[^" ]*_project"' "$self_test" | grep -Fv -- '--no-restore' || true)"
[[ -z "$implicit_fixture_builds" ]] || fail "fixture builds must use explicit restore followed by --no-restore"
path_safety_source="tools/Bukit.PublicApiDrift/BaselineFile.cs"
if grep -Fq 'OrdinalIgnoreCase' "$path_safety_source"; then
  fail "path containment must use ordinal comparisons on every host"
fi

scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-public-api-drift-self-test.XXXXXX")"
trap 'rm -rf -- "$scratch"' EXIT

formatter_project="$fixtures/formatter/FormatterFixture.csproj"
identity_v1_project="$fixtures/identity-v1/IdentityContractV1.csproj"
identity_consumer_project="$fixtures/identity-consumer/IdentityConsumer.csproj"
package_free_projects=("$tool_project" "$formatter_project" "$identity_v1_project" "$identity_consumer_project")
for project in "${package_free_projects[@]}"; do
  name="$(basename "${project%.csproj}")"
  assert_exit 0 "$scratch/$name-restore.txt" dotnet restore "$project" --nologo
done
assert_exit 0 "$scratch/tool-build.txt" dotnet build "$tool_project" -c Release --no-restore --nologo
assert_exit 0 "$scratch/formatter-build.txt" dotnet build "$formatter_project" -c Release --no-restore --nologo
assert_exit 0 "$scratch/identity-v1-build.txt" dotnet build "$identity_v1_project" -c Release --no-restore --nologo
assert_exit 0 "$scratch/identity-consumer-build.txt" dotnet build "$identity_consumer_project" -c Release --no-restore --nologo

formatter_candidate="$scratch/formatter-candidate.json"
assert_exit 0 "$scratch/formatter-snapshot.txt" dotnet run \
  --project "$tool_project" \
  -c Release --no-build --no-restore -- snapshot "$fixtures/formatter-policy.json" "$formatter_candidate" "$PWD" Release
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
  .publicMembers | index("public const Bukit.PublicApiDrift.FormatterFixture.FixtureEnum Ready = Bukit.PublicApiDrift.FormatterFixture.FixtureEnum.Ready") != null' \
  "$formatter_candidate" >/dev/null || fail "enum field lacks fully qualified member name"

identity_root="$scratch/identity-root"
identity_relative="tests/fixtures/public-api-drift/identity-consumer"
identity_output="$identity_root/$identity_relative/bin/Release/net10.0"
mkdir -p "$identity_output"
/bin/cp -R "$fixtures/identity-consumer/bin/Release/net10.0/." "$identity_output"
/bin/cp "$fixtures/identity-v1/bin/Release/net10.0/Bukit.PublicApiDrift.IdentityContract.dll" "$identity_output"
assert_exit 2 "$scratch/identity-mismatch.txt" dotnet run \
  --project "$tool_project" \
  -c Release --no-build --no-restore -- snapshot "$fixtures/identity-policy.json" "$scratch/identity-candidate.json" "$identity_root" Release
grep -Fq 'dependency assembly identity mismatch:' "$scratch/identity-mismatch.txt" || fail "dependency mismatch lacks exact identity diagnostic"

python3 - "$fixtures/baseline.json" "$scratch/utf8-bom.json" "$scratch/utf16.json" <<'PY'
from pathlib import Path
import sys

source = Path(sys.argv[1]).read_bytes()
Path(sys.argv[2]).write_bytes(b"\xef\xbb\xbf" + source)
Path(sys.argv[3]).write_bytes(source.decode("utf-8").encode("utf-16"))
PY

assert_exit 0 "$scratch/unchanged.txt" "${tool[@]}" "$fixtures/baseline.json" "$fixtures/unchanged.json"
assert_exit 1 "$scratch/additive.txt" "${tool[@]}" "$fixtures/baseline.json" "$fixtures/additive.json"
grep -Fq 'review-required:' "$scratch/additive.txt" || fail "additive drift lacks review-required"
if grep -Fq 'breaking:' "$scratch/additive.txt"; then fail "additive drift was mislabeled breaking"; fi
assert_exit 1 "$scratch/removal.txt" "${tool[@]}" "$fixtures/baseline.json" "$fixtures/removal.json"
grep -Fq 'breaking:' "$scratch/removal.txt" || fail "removal lacks breaking"
assert_exit 1 "$scratch/protected.txt" "${tool[@]}" "$fixtures/baseline.json" "$fixtures/protected-change.json"
grep -Fq 'protected-review:' "$scratch/protected.txt" || fail "protected drift lacks protected-review"
assert_exit 1 "$scratch/stable.txt" "${tool[@]}" "$fixtures/baseline.json" "$fixtures/stable-contract-change.json"
grep -Fq 'contract-shape-review:' "$scratch/stable.txt" || fail "stable contract drift lacks contract-shape-review"
assert_exit 1 "$scratch/aot.txt" "${tool[@]}" "$fixtures/baseline.json" "$fixtures/aot-change.json"
grep -Fq 'aot-review:' "$scratch/aot.txt" || fail "AOT drift lacks aot-review"
assert_exit 1 "$scratch/unclassified.txt" "${tool[@]}" "$fixtures/baseline.json" "$fixtures/unclassified.json"
grep -Fq 'unclassified:' "$scratch/unclassified.txt" || fail "new type lacks unclassified"
assert_exit 2 "$scratch/utf8-bom.txt" "${tool[@]}" "$scratch/utf8-bom.json" "$fixtures/unchanged.json"
grep -Fq 'gate-error:' "$scratch/utf8-bom.txt" || fail "UTF-8 BOM baseline lacks gate-error"
assert_exit 2 "$scratch/utf16.txt" "${tool[@]}" "$scratch/utf16.json" "$fixtures/unchanged.json"
grep -Fq 'gate-error:' "$scratch/utf16.txt" || fail "UTF-16 baseline lacks gate-error"
assert_exit 2 "$scratch/malformed.txt" "${tool[@]}" "$fixtures/malformed.json" "$fixtures/unchanged.json"
grep -Fq 'gate-error:' "$scratch/malformed.txt" || fail "malformed baseline lacks gate-error"
assert_exit 2 "$scratch/unsorted.txt" "${tool[@]}" "$fixtures/unsorted.json" "$fixtures/unchanged.json"
grep -Fq 'gate-error:' "$scratch/unsorted.txt" || fail "unsorted baseline lacks gate-error"
assert_exit 2 "$scratch/unresolved.txt" "${tool[@]}" "$fixtures/unresolved-baseline.json" "$fixtures/unchanged.json"
grep -Fq 'gate-error:' "$scratch/unresolved.txt" || fail "unresolved committed baseline lacks gate-error"

expected_self_test='run_step "public API drift self-test" bash scripts/checks/public-api-drift-self-test.sh'
expected_real_gate='run_step "public API drift" bash scripts/checks/public-api-drift.sh check "$configuration"'
[[ "$(grep -Fxc "$expected_self_test" scripts/gates/ci-fast.sh)" == "1" ]] || fail "ci-fast self-test wiring is missing or duplicated"
[[ "$(grep -Fxc "$expected_real_gate" scripts/gates/ci-fast.sh)" == "1" ]] || fail "ci-fast real-check wiring is missing or duplicated"
[[ "$(grep -Fxc '  docs/governance/bukit-core-public-api-baseline.v1.json' scripts/checks/docs/public-doc-contracts.sh)" == "1" ]] || fail "governed baseline documentation contract is missing or duplicated"
[[ "$(grep -Fxc '  docs/schemas/bukit-core-public-api-baseline.v1.schema.json' scripts/checks/docs/public-doc-contracts.sh)" == "1" ]] || fail "public API schema documentation contract is missing or duplicated"
[[ "$(grep -Fxc '  guide/dev/public-api-governance.md' scripts/checks/docs/public-doc-contracts.sh)" == "0" ]] || fail "Task 4 public API guide contract was registered before the guide exists"

assert_exit 2 "$scratch/ci-fast-extra-argument.txt" bash scripts/gates/ci-fast.sh Release Extra
assert_exit 2 "$scratch/missing-output.txt" bash scripts/checks/public-api-drift.sh snapshot
baseline="docs/governance/bukit-core-public-api-baseline.v1.json"
fixture_snapshot=(dotnet run \
  --project "$tool_project" \
  -c Release --no-build --no-restore -- snapshot "$fixtures/formatter-policy.json")
assert_exit 2 "$scratch/baseline-overwrite.txt" "${fixture_snapshot[@]}" "$baseline" "$PWD" Release
touch "$scratch/existing.json"
assert_exit 2 "$scratch/existing-output.txt" "${fixture_snapshot[@]}" "$scratch/existing.json" "$PWD" Release
mkdir "$scratch/existing-directory"
assert_exit 2 "$scratch/existing-directory-output.txt" "${fixture_snapshot[@]}" "$scratch/existing-directory" "$PWD" Release
ln -s "$scratch/missing-target.json" "$scratch/symlink-output.json"
assert_exit 2 "$scratch/symlink-output.txt" "${fixture_snapshot[@]}" "$scratch/symlink-output.json" "$PWD" Release

outside="$(python3 - "$PWD" "${TMPDIR:-/tmp}" "$(basename "$scratch")" <<'PY'
import os
from pathlib import Path
import sys

repository = Path(sys.argv[1]).resolve()
temporary = Path(sys.argv[2]).resolve()
token = sys.argv[3]

def contains(root: Path, candidate: Path) -> bool:
    try:
        candidate.relative_to(root)
        return True
    except ValueError:
        return False

for anchor_text in dict.fromkeys((repository.anchor, temporary.anchor)):
    candidate = Path(anchor_text) / f".bukit-public-api-outside-{token}.json"
    if not contains(repository, candidate) and not contains(temporary, candidate) and not os.path.lexists(candidate):
        print(candidate)
        break
else:
    raise SystemExit("could not derive a nonexistent path outside the repository and temporary roots")
PY
)" || fail "could not derive outside-path candidate"
[[ ! -e "$outside" && ! -L "$outside" ]] || fail "outside-path candidate already exists"
assert_exit 2 "$scratch/outside-output.txt" "${fixture_snapshot[@]}" "$outside" "$PWD" Release
[[ ! -e "$outside" && ! -L "$outside" ]] || fail "outside-path rejection created the candidate"
ln -s "$(dirname "$outside")" "$scratch/outside-link"
assert_exit 2 "$scratch/symlink-parent-output.txt" \
  "${fixture_snapshot[@]}" "$scratch/outside-link/$(basename "$outside")" "$PWD" Release
assert_exit 0 "$scratch/temp-snapshot.txt" \
  "${fixture_snapshot[@]}" "$scratch/fixture-snapshot.json" "$PWD" Release

echo "public API drift self-test OK"

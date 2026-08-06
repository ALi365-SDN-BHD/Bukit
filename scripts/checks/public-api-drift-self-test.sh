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
self_test_sources=(
  "$self_test"
  "scripts/checks/public-api-drift-self-test-formatter.sh"
  "scripts/checks/public-api-drift-self-test-policy.sh"
)
real_wrapper_check='bash scripts/checks/public-api-drift.sh check'
if grep -Fq "$real_wrapper_check Release" "${self_test_sources[@]}" ||
   grep -Eq -- '-- (check|snapshot) "\$baseline"' "${self_test_sources[@]}"; then
  fail "ci-fast self-test must not execute a real Core check or snapshot"
fi
implicit_fixture_builds="$(grep -E 'dotnet build "\$[^" ]*_project"' "${self_test_sources[@]}" | grep -Fv -- '--no-restore' || true)"
[[ -z "$implicit_fixture_builds" ]] || fail "fixture builds must use explicit restore followed by --no-restore"
path_safety_source="tools/Bukit.PublicApiDrift/BaselineFile.cs"
if grep -Fq 'OrdinalIgnoreCase' "$path_safety_source"; then
  fail "path containment must use ordinal comparisons on every host"
fi
grep -Fq 'C# `public` is CLR visibility, not an automatic supported SDK promise.' guide/dev/public-api-governance.md || fail "CLR visibility policy is missing"
grep -Fq 'bash scripts/checks/public-api-drift.sh snapshot OUTPUT Release' guide/dev/public-api-governance.md || fail "snapshot workflow is missing"
if grep -Fq 'Source-generated plugin SDK' docs/bukit-1.0-contract-matrix.zh-CN.md; then fail "stale source-generated SDK claim remains"; fi
grep -Fq 'Process protocol DTO and static JSON serialization support' docs/bukit-1.0-contract-matrix.zh-CN.md || fail "implemented plugin boundary is missing"

scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-public-api-drift-self-test.XXXXXX")"
trap 'rm -rf -- "$scratch"' EXIT
bash scripts/checks/public-api-drift-self-test-formatter.sh "$scratch"
bash scripts/checks/public-api-drift-self-test-policy.sh "$scratch"

for compatibility in 2.x-do-not-narrow 2.x-migration-safe 2.x-shape-stable; do
  output="$scratch/${compatibility}.json"
  sed "s/\"compatibility\": \"2.0-candidate\"/\"compatibility\": \"${compatibility}\"/" \
    "$fixtures/baseline.json" >"$output"
  assert_exit 0 "$scratch/${compatibility}.txt" \
    "${tool[@]}" "$output" "$output"
done
sed 's/"compatibility": "2.0-candidate"/"compatibility": "2.x-unknown"/' \
  "$fixtures/baseline.json" >"$scratch/compatibility-unknown.json"
assert_exit 2 "$scratch/compatibility-unknown.txt" \
  "${tool[@]}" "$scratch/compatibility-unknown.json" "$fixtures/unchanged.json"
grep -Fq 'gate-error:' "$scratch/compatibility-unknown.txt" || \
  fail "unknown 2.x compatibility lacks gate-error"

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
assert_exit 1 "$scratch/contract-type-addition.txt" "${tool[@]}" "$fixtures/baseline.json" "$fixtures/contract-type-addition.json"
grep -Fq 'review-required: Fixture.Core::Fixture.ZContractWidget: exported type added' "$scratch/contract-type-addition.txt" || fail "contract type addition lacks review-required"
grep -Fq 'contract-shape-review: Fixture.Core::Fixture.ZContractWidget:' "$scratch/contract-type-addition.txt" || fail "contract type addition lacks contract-shape-review"
assert_exit 1 "$scratch/contract-type-removal.txt" "${tool[@]}" "$fixtures/contract-type-addition.json" "$fixtures/baseline.json"
grep -Fq 'breaking: Fixture.Core::Fixture.ZContractWidget: exported type removed' "$scratch/contract-type-removal.txt" || fail "contract type removal lacks breaking"
grep -Fq 'contract-shape-review: Fixture.Core::Fixture.ZContractWidget:' "$scratch/contract-type-removal.txt" || fail "contract type removal lacks contract-shape-review"
assert_exit 1 "$scratch/aot-type-addition.txt" "${tool[@]}" "$fixtures/baseline.json" "$fixtures/aot-type-addition.json"
grep -Fq 'aot-review: Fixture.Core::Fixture.ZAotWidget:' "$scratch/aot-type-addition.txt" || fail "AOT type addition lacks aot-review"
assert_exit 1 "$scratch/aot-type-removal.txt" "${tool[@]}" "$fixtures/aot-type-addition.json" "$fixtures/baseline.json"
grep -Fq 'aot-review: Fixture.Core::Fixture.ZAotWidget:' "$scratch/aot-type-removal.txt" || fail "AOT type removal lacks aot-review"
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
[[ "$(grep -Fxc '  guide/dev/public-api-governance.md' scripts/checks/docs/public-doc-contracts.sh)" == "1" ]] || fail "public API guide documentation contract is missing or duplicated"

assert_exit 2 "$scratch/ci-fast-extra-argument.txt" bash scripts/gates/ci-fast.sh Release Extra
assert_exit 2 "$scratch/missing-output.txt" bash scripts/checks/public-api-drift.sh snapshot
fake_bin="$scratch/fake-bin"
mkdir "$fake_bin"
{
  printf '%s\n' '#!/usr/bin/env bash' 'if [[ "${1:-}" == "build" ]]; then' \
    '  printf "fake build failure: " >&2' \
    '  i=0; while (( i < 800 )); do printf x >&2; i=$((i + 1)); done' \
    '  printf " SECRET_UNBOUNDED_MARKER\\n" >&2' '  exit 1' 'fi' \
    'printf "unexpected fake dotnet invocation\\n" >&2' 'exit 99'
} >"$fake_bin/dotnet"
chmod +x "$fake_bin/dotnet"
fake_wrapper_mode="check"
assert_exit 2 "$scratch/wrapper-build-failure.txt" env PATH="$fake_bin:$PATH" \
  bash scripts/checks/public-api-drift.sh "$fake_wrapper_mode" Release
grep -Eq '^gate-error: .*dotnet build --no-restore failed' "$scratch/wrapper-build-failure.txt" || \
  fail "wrapper build failure lacks gate-error"
[[ "$(wc -c <"$scratch/wrapper-build-failure.txt")" -le 500 ]] || fail "wrapper build error is not bounded"
if grep -Fq 'SECRET_UNBOUNDED_MARKER' "$scratch/wrapper-build-failure.txt"; then
  fail "wrapper leaked unbounded build output"
fi
baseline="docs/governance/bukit-core-public-api-baseline.v1.json"
fixture_snapshot=(dotnet run \
  --project "$tool_project" \
  -c Release --no-build --no-restore -- fixture-snapshot "$fixtures/formatter-policy.json")
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

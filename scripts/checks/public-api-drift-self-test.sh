#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

fail() { echo "public API drift self-test failed: $*" >&2; exit 1; }
tool=(dotnet run --project tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj -c Release --no-restore -- compare)
fixtures="tests/fixtures/public-api-drift"

assert_exit() {
  local expected="$1" output="$2"; shift 2
  local status=0
  "$@" >"$output" 2>&1 || status=$?
  [[ "$status" == "$expected" ]] || fail "expected exit $expected, got $status: $(tr '\n' ' ' <"$output")"
}

scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-public-api-drift-self-test.XXXXXX")"
trap 'rm -rf -- "$scratch"' EXIT

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

baseline="docs/governance/bukit-core-public-api-baseline.v1.json"
assert_exit 0 "$scratch/real-check.txt" dotnet run \
  --project tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj \
  -c Release --no-restore -- check "$baseline" "$PWD" Release

first="$scratch/first.json"
second="$scratch/second.json"
assert_exit 0 "$scratch/snapshot-1.txt" dotnet run \
  --project tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj \
  -c Release --no-restore -- snapshot "$baseline" "$first" "$PWD" Release
assert_exit 0 "$scratch/snapshot-2.txt" dotnet run \
  --project tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj \
  -c Release --no-restore -- snapshot "$baseline" "$second" "$PWD" Release
cmp -s "$first" "$second" || fail "two captures are not byte-identical"
[[ "$(jq '.assemblies | length' "$first")" == "12" ]] || fail "capture does not contain 12 assemblies"
[[ "$(jq '.types | length' "$first")" == "472" ]] || fail "capture does not contain 472 exported types"

echo "public API drift self-test OK"

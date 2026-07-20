#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

fail() { echo "public API drift policy self-test failed: $*" >&2; exit 1; }
assert_exit() {
  local expected="$1" output="$2"; shift 2
  local status=0
  "$@" >"$output" 2>&1 || status=$?
  [[ "$status" == "$expected" ]] || fail "expected exit $expected, got $status: $(tr '\n' ' ' <"$output")"
}

[[ $# == 1 && -d "$1" ]] || fail "expected owned scratch directory"
scratch="$1"
baseline="docs/governance/bukit-core-public-api-baseline.v1.json"
schema="docs/schemas/bukit-core-public-api-baseline.v1.schema.json"

jq -e --slurpfile baseline "$baseline" '
  .properties.assemblies.minItems == 12 and
  .properties.assemblies.maxItems == 12 and
  .properties.assemblies.items == false and
  ([.properties.assemblies.prefixItems[] | {
    assembly: .properties.assembly.const,
    project: .properties.project.const
  }] == $baseline[0].assemblies)' "$schema" >/dev/null ||
  fail "schema does not encode the exact ordered governed assembly mappings"

python3 - "$baseline" "$scratch" <<'PY'
from pathlib import Path
import sys

source = Path(sys.argv[1]).read_text(encoding="utf-8")
scratch = Path(sys.argv[2])

def block(assembly: str, project: str) -> str:
    return (
        "    {\n"
        f'      "assembly": "{assembly}",\n'
        f'      "project": "{project}"\n'
        "    }"
    )

first = block("Bukit.Cli.Shared", "src/Bukit-Core/Bukit.Cli.Shared/Bukit.Cli.Shared.csproj")
second = block("Bukit.Config", "src/Bukit-Core/Bukit.Config/Bukit.Config.csproj")
last = block("bukit", "src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj")
extra = block("Bukit.Unexpected", "tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj")

mutations = {
    "missing": source.replace(first + ",\n", "", 1),
    "extra": source.replace(last, extra + ",\n" + last, 1),
    "remapped": source.replace(
        "src/Bukit-Core/Bukit.Cli.Shared/Bukit.Cli.Shared.csproj",
        "tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj",
        1,
    ),
    "duplicate": source.replace(first + ",\n", first + ",\n" + first + ",\n", 1),
    "unsorted": source.replace(first + ",\n" + second, second + ",\n" + first, 1),
}
for name, text in mutations.items():
    (scratch / f"governed-{name}.json").write_text(text, encoding="utf-8")
PY

tool=(dotnet run --project tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj -c Release --no-build --no-restore --)
for mutation in missing extra remapped duplicate unsorted; do
  output="$scratch/governed-$mutation.txt"
  assert_exit 2 "$output" "${tool[@]}" check "$scratch/governed-$mutation.json" "$scratch/no-repository-root" Release
  grep -Fq 'gate-error:' "$output" || fail "$mutation governed mapping lacks gate-error"
  grep -Fq 'governed assembly mappings must exactly match policy' "$output" || \
    fail "$mutation governed mapping was not rejected before capture"
done
assert_exit 2 "$scratch/governed-snapshot-missing.txt" "${tool[@]}" snapshot \
  "$scratch/governed-missing.json" "$scratch/should-not-exist.json" "$scratch/no-repository-root" Release
grep -Fq 'governed assembly mappings must exactly match policy' "$scratch/governed-snapshot-missing.txt" || \
  fail "snapshot did not enforce governed mappings before capture"
[[ ! -e "$scratch/should-not-exist.json" ]] || fail "invalid governed snapshot created output"

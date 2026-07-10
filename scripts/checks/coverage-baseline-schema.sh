#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
policy="${1:-${repo_root}/docs/coverage-baselines.json}"
validator="${repo_root}/scripts/checks/coverage/validate-policy.py"
tmp_root="$(mktemp -d)"
mkdir -p "${tmp_root}/schemas"
cp "${repo_root}/docs/schemas/coverage-baselines.v2.json" "${tmp_root}/schemas/"

cleanup() {
  rm -rf "$tmp_root"
}
trap cleanup EXIT

make_fixture() {
  local name="$1"
  local expression="$2"
  local target="${tmp_root}/${name}.json"

  python3 - "$policy" "$target" "$expression" <<'PY'
import json
import sys
from pathlib import Path

source = Path(sys.argv[1])
target = Path(sys.argv[2])
expression = sys.argv[3]
obj = json.loads(source.read_text(encoding="utf-8"))
exec(expression, {"obj": obj})
target.write_text(json.dumps(obj, indent=2) + "\n", encoding="utf-8")
PY

  printf '%s\n' "$target"
}

expect_fail() {
  local name="$1"
  local expression="$2"
  local fixture
  fixture="$(make_fixture "$name" "$expression")"

  if bash "$validator" "$fixture" >"${tmp_root}/${name}.out" 2>&1; then
    echo "ERROR: policy fixture should have failed: ${name}" >&2
    cat "${tmp_root}/${name}.out" >&2
    exit 1
  fi
  echo "coverage policy failed as expected: ${name}"
}

echo "coverage policy: ${policy}"
bash "$validator" "$policy"

expect_fail "missing-scope" "del obj['scope']"
expect_fail "missing-schema" "del obj['\u0024schema']"
expect_fail "wrong-schema" "obj['\u0024schema'] = 'schemas/coverage-baselines.v1.json'"
expect_fail "plugin-scope" "obj['scope'] = 'plugins'"
expect_fail "overall-above-100" "obj['minimums']['overall'] = 101"
expect_fail "overall-nan" "obj['minimums']['overall'] = float('nan')"
expect_fail "missing-project-floor" "del obj['minimums']['projectFloor']"
expect_fail "project-floor-nan" "obj['minimums']['projectFloor'] = float('nan')"
expect_fail "legacy-cli-field" "obj['cli'] = {'blocking': True, 'minimum': 75}"
expect_fail "legacy-labs-field" "obj['labs'] = {'blocking': False, 'baseline': 50}"

schema_policy="${tmp_root}/schema-policy.json"
schema_fixture="${tmp_root}/schemas/coverage-baselines.v2.json"
cp "$policy" "$schema_policy"
python3 - "$schema_fixture" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
schema = json.loads(path.read_text(encoding="utf-8"))
schema["required"] = []
schema["properties"]["minimums"] = {"type": "string"}
schema["additionalProperties"] = True
path.write_text(json.dumps(schema, indent=2) + "\n", encoding="utf-8")
PY
if bash "$validator" "$schema_policy" >"${tmp_root}/corrupt-schema.out" 2>&1; then
  echo "ERROR: corrupt coverage schema should have failed" >&2
  cat "${tmp_root}/corrupt-schema.out" >&2
  exit 1
fi
echo "coverage policy failed as expected: corrupt-schema"

echo "coverage policy schema check OK"

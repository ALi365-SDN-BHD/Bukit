#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
schema="${repo_root}/docs/schemas/coverage-baselines.v1.json"
baseline="${COVERAGE_BASELINE_FILE:-${repo_root}/docs/coverage-baselines.json}"
tmp_root="$(mktemp -d)"

cleanup() {
  rm -rf "$tmp_root"
}
trap cleanup EXIT

expect_fail() {
  local name="$1"
  local expression="$2"
  local expected="$3"
  local fixture="${tmp_root}/${name}.json"
  local output="${tmp_root}/${name}.out"

  python3 - "$baseline" "$fixture" "$expression" <<'PY'
import json
import sys
from pathlib import Path

source = Path(sys.argv[1])
target = Path(sys.argv[2])
expression = sys.argv[3]
obj = json.loads(source.read_text(encoding="utf-8"))
exec(expression, {"obj": obj})
target.write_text(json.dumps(obj, indent=2, sort_keys=False) + "\n", encoding="utf-8")
PY

  if python3 "${repo_root}/scripts/validate-json-schema.py" "$schema" "$fixture" >"$output" 2>&1; then
    echo "ERROR: coverage baseline schema fixture should have failed: $name" >&2
    cat "$output" >&2
    exit 1
  fi

  if ! grep -Fq "$expected" "$output"; then
    echo "ERROR: expected coverage baseline schema output to contain '$expected'" >&2
    cat "$output" >&2
    exit 1
  fi

  echo "coverage baseline schema failed as expected: $name"
}

python3 "${repo_root}/scripts/validate-json-schema.py" "$schema" "$baseline"
echo "coverage baseline schema passed: docs/coverage-baselines.json"

expect_fail "core-blocking-false" "obj['core']['blocking'] = False" "$.core.blocking: expected const True"
expect_fail "cli-blocking-false" "obj['cli']['blocking'] = False" "$.cli.blocking: expected const True"
expect_fail "importing-blocking-true" "obj['importing']['blocking'] = True" "$.importing.blocking: expected const False"
expect_fail "labs-blocking-true" "obj['labs']['blocking'] = True" "$.labs.blocking: expected const False"
expect_fail "missing-core-minimum" "del obj['core']['minimum']" "$.core: missing required property 'minimum'"
expect_fail "missing-cli-minimum" "del obj['cli']['minimum']" "$.cli: missing required property 'minimum'"
expect_fail "missing-importing-baseline" "del obj['importing']['baseline']" "$.importing: missing required property 'baseline'"
expect_fail "missing-labs-baseline" "del obj['labs']['baseline']" "$.labs: missing required property 'baseline'"
expect_fail "core-minimum-above-100" "obj['core']['minimum'] = 101" "$.core.minimum: expected <= 100"
expect_fail "labs-baseline-above-100" "obj['labs']['baseline'] = 101" "$.labs.baseline: expected <= 100"
expect_fail "extra-core-property" "obj['core']['unexpected'] = 1" "$.core.unexpected: additional property is not allowed"

echo "coverage baseline schema check OK"

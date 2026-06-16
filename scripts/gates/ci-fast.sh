#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

echo "=== checks: file size ==="
bash scripts/checks/file-size.sh

echo "=== checks: repo hygiene ==="
bash scripts/checks/repo-hygiene.sh

echo "=== checks: github action pin compliance ==="
bash scripts/checks/ci-workflow-action-pin.sh

echo "=== checks: encoding ==="
bash scripts/checks/encoding.sh

echo "=== checks: Core CLI script contract ==="
bash scripts/checks/core-cli-contract.sh

echo "=== checks: skills python deps ==="
bash scripts/checks/skills-python-deps.sh

echo "=== checks: skills schema ==="
bash scripts/checks/skills-schema.sh

echo "=== checks: skills strict validation ==="
bash guide/skills/scripts/validate-skills-strict.sh

echo "=== checks: coverage baseline schema ==="
bash scripts/checks/coverage-baseline-schema.sh

echo "=== checks: release assets fixture ==="
bash scripts/release/test-release-assets-fixture.sh

echo "=== checks: workflow evidence fixture ==="
bash scripts/checks/ci-workflow-evidence-fixtures.sh

echo "=== checks: release artifact smoke contract ==="
bash scripts/checks/release-artifact-smoke-contract.sh

echo "=== checks: CLI docs sync ==="
bash scripts/checks/cli-docs-sync.sh

echo "=== checks: CLI docs sync fixtures ==="
bash scripts/checks/test-cli-docs-sync-fixtures.sh

echo "=== restore ==="
dotnet restore bukit.slnx

echo "=== build ==="
dotnet build bukit.slnx -c "$configuration" -maxcpucount:1 -nodeReuse:false

echo "=== test ==="
test_args=(
  "-c"
  "$configuration"
  "--no-build"
  "-maxcpucount:1"
  "-nodeReuse:false"
  "--logger"
  "console;verbosity=minimal"
  "--logger"
  "trx"
  "--results-directory"
  "TestResults/ci-fast"
)

if [[ -n "${CI_FAST_TEST_FILTER:-}" ]]; then
  test_args+=(--filter "$CI_FAST_TEST_FILTER")
fi

if [[ "${CI_FAST_TEST_DISABLE_BLAME:-}" == "1" ]]; then
  echo "  info: CI_FAST_TEST_DISABLE_BLAME=1; skipping blame hang diagnostics."
else
  if [[ -n "${CI_FAST_TEST_HANG_TIMEOUT:-}" ]]; then
    test_args+=("--blame-hang" "--blame-hang-timeout" "$CI_FAST_TEST_HANG_TIMEOUT")
  elif [[ "${CI:-}" == "true" || "${CI:-}" == "1" ]]; then
    test_args+=("--blame-hang" "--blame-hang-timeout" "10m")
  else
    test_args+=("--blame-hang" "--blame-hang-timeout" "30m")
  fi
  test_args+=("--blame-hang-dump-type" "full" "--diag" "TestResults/ci-fast/testhost.log")
fi
test_args+=("--verbosity" "normal")

test_project="bukit.slnx"
if [[ -n "${CI_FAST_TEST_PROJECT:-}" ]]; then
  test_project="$CI_FAST_TEST_PROJECT"
fi
if [[ -n "${CI_FAST_TEST_PROJECTS:-}" ]]; then
  IFS=',' read -r -a test_projects <<< "$CI_FAST_TEST_PROJECTS"
else
  test_projects=("$test_project")
fi

run_dotnet_test() {
  local project="$1"
  local start_ts
  local elapsed
  start_ts="$(date +%s)"
  echo "=== test: start ==="
  echo "  project: $project"

  if [[ -n "${CI_FAST_TEST_TIMEOUT:-}" ]]; then
    if command -v timeout >/dev/null 2>&1; then
      timeout "$CI_FAST_TEST_TIMEOUT" dotnet test "$project" "${test_args[@]}"
    else
      echo "  warning: timeout command not found, running without CI_FAST_TEST_TIMEOUT."
      dotnet test "$project" "${test_args[@]}"
    fi
  else
    dotnet test "$project" "${test_args[@]}"
  fi
  local status=$?

  elapsed=$(( $(date +%s) - start_ts ))
  echo "=== test: end ==="
  echo "  project: $project"
  echo "  exit: $status"
  echo "  elapsed: ${elapsed}s"

  if [[ $status -ne 0 ]]; then
    return "$status"
  fi
}

for test_project in "${test_projects[@]}"; do
  run_dotnet_test "$test_project"
done

echo "=== format ==="
dotnet format bukit.slnx --verify-no-changes --no-restore

echo "=== docs consistency ==="
bash scripts/checks/docs-consistency.sh

echo "=== README sync ==="
bash scripts/checks/readme-sync.sh

echo "CI fast gate OK"

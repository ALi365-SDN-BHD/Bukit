#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

dry_run=0; paths=(); owner_checks=(); unmapped_owner_paths=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --dry-run) dry_run=1; shift ;;
    --) shift; while [[ $# -gt 0 ]]; do paths+=("${1#./}"); shift; done ;;
    -*) echo "unknown option: $1" >&2; exit 2 ;;
    *) paths+=("${1#./}"); shift ;;
  esac
done

[[ ${#paths[@]} -gt 0 ]] || exit 0

contains_item() {
  local needle="$1" item
  shift
  for item in "$@"; do [[ "$item" == "$needle" ]] && return 0; done
  return 1
}

add_owner_check() {
  local owner_check="$1"
  if [[ ${#owner_checks[@]} -eq 0 ]] || ! contains_item "$owner_check" "${owner_checks[@]}"; then
    owner_checks+=("$owner_check")
  fi
}

for path in "${paths[@]}"; do
  case "$path" in
    src/Bukit-Core/*.cs)
      add_owner_check public-api-drift ;;
    AGENTS.md|guide/dev/agent-task-workflow.md|guide/dev/testing.md|\
    scripts/checks/agent-governance-contract.sh)
      add_owner_check governance ;;
    scripts/checks/codex-workflow.py|\
    scripts/checks/codex-workflow-policy.v1.json|\
    scripts/checks/codex-workflow-self-test.sh|\
    scripts/checks/codex-workflow-self-test.d/*.sh|\
    scripts/checks/codex_workflow/*.py)
      add_owner_check "self-test:scripts/checks/codex-workflow-self-test.sh" ;;
    guide/skills/AGENTS.md)
      add_owner_check skills-strict ;;
    scripts/checks/post-change-focused.sh|scripts/checks/post-change-focused-self-test.sh|\
    scripts/checks/untracked-whitespace.sh)
      add_owner_check focused-self-test ;;
    scripts/checks/post-change-focused-owner-checks.sh|\
    scripts/checks/post-change-focused-owner-checks-self-test.sh)
      add_owner_check focused-owner-self-test ;;
    scripts/checks/post-change-targeted.sh|scripts/checks/post-change-targeted-self-test.sh)
      add_owner_check targeted-self-test ;;
    scripts/checks/post-change-targeted-paths.sh|scripts/checks/post-change-targeted-projects.sh)
      add_owner_check focused-self-test
      add_owner_check targeted-self-test ;;
    .editorconfig|Directory.Build.props)
      add_owner_check dotnet-format-self-test
      add_owner_check code-analysis-ratchet-self-test ;;
    scripts/checks/code-analysis-ratchet.py|scripts/checks/baselines/code-analysis.v1.json|\
    guide/dev/code-quality-governance.md)
      add_owner_check code-analysis-ratchet-self-test ;;
    scripts/checks/coverage/list-core-projects.sh)
      add_owner_check "self-test:scripts/checks/coverage/project-list-self-test.sh" ;;
    scripts/checks/public-api-drift-self-test-policy.sh)
      add_owner_check "self-test:scripts/checks/public-api-drift-self-test.sh" ;;
    scripts/gates/ci-fast.sh|scripts/checks/ci-fast-portability-self-test.sh)
      add_owner_check ci-fast-portability ;;
    scripts/quality-gate.sh)
      add_owner_check ci-fast-portability ;;
    scripts/checks/docs-consistency.sh)
      add_owner_check docs-consistency ;;
    .github/workflows/*)
      add_owner_check active-workflow ;;
    scripts/security/security-regression.sh|scripts/security/security-regression-self-test.sh|\
    scripts/security/verify-trx.py)
      add_owner_check security-regression ;;
    scripts/smoke/release-artifacts.sh|scripts/smoke/release-artifacts-self-test.sh|\
    scripts/smoke/extract-release-artifact.py)
      add_owner_check release-artifacts-smoke ;;
    scripts/smoke/core.sh|scripts/smoke/core-self-test.sh|scripts/smoke.sh)
      add_owner_check core-smoke ;;
    scripts/release/prepare-release-assets.sh|scripts/release/verify-release-assets.sh|\
    scripts/release/release-assets.py|scripts/release/release_asset_contract.py|\
    scripts/release/release-assets-self-test.sh)
      add_owner_check release-assets ;;
    scripts/build/native-aot.sh|scripts/build/native-aot-self-test.sh|\
    scripts/build/package-native-aot.sh)
      add_owner_check native-aot ;;
    scripts/build/build-repro.sh|scripts/build/build-repro-self-test.sh|\
    scripts/build/compare-publish-trees.py)
      add_owner_check build-repro ;;
    scripts/build/normalize-yaml-static-context.py|\
    scripts/build/normalize-yaml-static-context-self-test.sh)
      add_owner_check yaml-normalizer ;;
    scripts/build/yaml-static-context.sh)
      add_owner_check yaml-static-context ;;
    scripts/lib/common.sh)
      add_owner_check focused-self-test
      add_owner_check targeted-self-test
      add_owner_check ci-fast-portability ;;
    guide/skills/scripts/validate-skills-strict.sh|guide/skills/scripts/check-cli-commands.py)
      add_owner_check skills-strict ;;
    scripts/checks/*-self-test.sh)
      add_owner_check "self-test:$path" ;;
    scripts/checks/*.sh)
      candidate="${path%.sh}-self-test.sh"
      if [[ -f "$candidate" ]]; then
        add_owner_check "self-test:$candidate"
      else
        unmapped_owner_paths+=("$path")
      fi ;;
    scripts/gates/*.sh|scripts/release*.sh|scripts/checks/*|scripts/security/*|\
    scripts/smoke/*|scripts/release/*|scripts/build/*|scripts/lib/*|\
    guide/skills/scripts/*|scripts/*gate*.sh)
      unmapped_owner_paths+=("$path") ;;
  esac
done

if [[ ${#unmapped_owner_paths[@]} -gt 0 ]]; then
  echo "No focused owner check registered for gate-owned paths:" >&2
  printf '  %s\n' "${unmapped_owner_paths[@]}" >&2
  echo "Register a direct self-test or obtain authorization for the owning broad gate." >&2
  exit 1
fi

print_command() {
  local arg
  printf '+'
  for arg in "$@"; do printf ' %q' "$arg"; done
  printf '\n'
}

run_or_print() {
  local label="$1"
  shift
  if [[ "$dry_run" == "1" ]]; then print_command "$@"; else run_step "$label" "$@"; fi
}

if [[ ${#owner_checks[@]} -gt 0 ]]; then
  for owner_check in "${owner_checks[@]}"; do
    case "$owner_check" in
      public-api-drift)
        run_or_print "public API drift" \
          bash scripts/checks/public-api-drift.sh check Release ;;
      governance)
        run_or_print "agent governance contract" bash scripts/checks/agent-governance-contract.sh ;;
      focused-self-test)
        run_or_print "post-change focused self-test" bash scripts/checks/post-change-focused-self-test.sh ;;
      focused-owner-self-test)
        run_or_print "post-change focused owner-checks self-test" bash scripts/checks/post-change-focused-owner-checks-self-test.sh ;;
      targeted-self-test)
        run_or_print "post-change targeted self-test" bash scripts/checks/post-change-targeted-self-test.sh ;;
      dotnet-format-self-test)
        run_or_print "dotnet format self-test" bash scripts/checks/dotnet-format-self-test.sh ;;
      code-analysis-ratchet-self-test)
        run_or_print "code analysis ratchet self-test" bash scripts/checks/code-analysis-ratchet-self-test.sh ;;
      ci-fast-portability)
        run_or_print "ci-fast portability self-test" bash scripts/checks/ci-fast-portability-self-test.sh ;;
      security-regression)
        run_or_print "security regression self-test" bash scripts/security/security-regression-self-test.sh ;;
      release-artifacts-smoke)
        run_or_print "release artifact self-test" bash scripts/smoke/release-artifacts-self-test.sh ;;
      core-smoke)
        run_or_print "core smoke self-test" bash scripts/smoke/core-self-test.sh ;;
      release-assets)
        run_or_print "release assets self-test" bash scripts/release/release-assets-self-test.sh ;;
      native-aot)
        run_or_print "native AOT self-test" bash scripts/build/native-aot-self-test.sh ;;
      build-repro)
        run_or_print "build reproducibility self-test" bash scripts/build/build-repro-self-test.sh ;;
      yaml-normalizer)
        run_or_print "YAML static context normalizer self-test" bash scripts/build/normalize-yaml-static-context-self-test.sh ;;
      yaml-static-context)
        run_or_print "YAML static context gate self-test" bash scripts/checks/yaml-static-context-gate-self-test.sh
        run_or_print "YAML static context normalizer self-test" bash scripts/build/normalize-yaml-static-context-self-test.sh ;;
      skills-strict)
        run_or_print "skills strict validation" bash guide/skills/scripts/validate-skills-strict.sh ;;
      docs-consistency)
        run_or_print "docs consistency" bash scripts/checks/docs-consistency.sh ;;
      active-workflow)
        run_or_print "active workflow boundary self-test" bash scripts/checks/active-workflow-boundary-self-test.sh
        run_or_print "active workflow boundary" bash scripts/checks/active-workflow-boundary.sh ;;
      self-test:*)
        run_or_print "$(basename "${owner_check#self-test:}")" bash "${owner_check#self-test:}" ;;
      *)
        echo "Unknown focused owner check: $owner_check" >&2; exit 2 ;;
    esac
  done
fi

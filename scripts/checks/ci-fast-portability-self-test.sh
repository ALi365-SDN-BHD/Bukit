#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-ci-fast-portability.XXXXXX")"
marker="$scratch/rg-called"
trap 'rm -rf "$scratch"' EXIT

fail() {
  echo "ci-fast portability self-test failed: $*" >&2
  exit 1
}

mkdir -p "$scratch/bin"
printf '%s\n' \
  '#!/usr/bin/env bash' \
  'printf "%s\n" "rg was invoked" > "$BUKIT_RG_MARKER"' \
  'exit 127' \
  > "$scratch/bin/rg"
chmod +x "$scratch/bin/rg"

checks=(
  scripts/checks/active-workflow-boundary.sh
  scripts/checks/docs/public-doc-contracts.sh
  scripts/checks/docs/no-absolute-paths.sh
  scripts/checks/docs/no-core-drift.sh
  scripts/checks/readme-sync.sh
  scripts/checks/core-cli-contract.sh
  guide/skills/scripts/validate-skills-strict.sh
)
search_failure_messages=(
  "Active workflow boundary text search failed"
  "public documentation text search failed"
  "public absolute path text search failed"
  "Core docs command text search failed"
  "README link text search failed for README.md"
  "Core CLI contract text search failed"
  "skills strict text search failed"
)

for check in "${checks[@]}"; do
  BUKIT_RG_MARKER="$marker" PATH="$scratch/bin:$PATH" bash "$check" ||
    fail "$check failed when ripgrep was unavailable"
done

if [[ -e "$marker" ]]; then
  fail "an active ci-fast check invoked ripgrep"
fi

mkdir -p "$scratch/failing-grep"
printf '%s\n' \
  '#!/usr/bin/env bash' \
  'echo "injected grep failure" >&2' \
  'exit 2' \
  > "$scratch/failing-grep/grep"
chmod +x "$scratch/failing-grep/grep"

for index in "${!checks[@]}"; do
  check="${checks[$index]}"
  expected_message="${search_failure_messages[$index]}"
  set +e
  output="$(PATH="$scratch/failing-grep:$PATH" bash "$check" 2>&1)"
  actual_status=$?
  set -e
  if [[ "$actual_status" -ne 2 ]]; then
    fail "$check returned $actual_status after grep exited 2"
  fi
  case "$output" in
    *"$expected_message"*) ;;
    *) fail "$check did not report its scanner-specific search failure" ;;
  esac
done

mkdir -p "$scratch/recording-grep"
printf '%s\n' \
  '#!/usr/bin/env bash' \
  'printf "%s\n" "$@" > "$BUKIT_GREP_ARGS"' \
  'exit 1' \
  > "$scratch/recording-grep/grep"
chmod +x "$scratch/recording-grep/grep"

BUKIT_GREP_ARGS="$scratch/grep.args" PATH="$scratch/recording-grep:$PATH" \
  bash scripts/checks/core-cli-contract.sh ||
  fail "Core CLI contract rejected grep no-match status"
grep -Fxq '.github/workflows' "$scratch/grep.args" ||
  fail "Core CLI contract did not scan active workflows"
if grep -Fxq '.github' "$scratch/grep.args"; then
  fail "Core CLI contract scanned all of .github"
fi

echo "ci-fast portability self-test OK"

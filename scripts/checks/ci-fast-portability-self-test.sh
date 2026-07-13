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
  scripts/checks/docs/public-doc-contracts.sh
  scripts/checks/docs/no-absolute-paths.sh
  scripts/checks/docs/no-core-drift.sh
  scripts/checks/readme-sync.sh
  scripts/checks/core-cli-contract.sh
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

for check in "${checks[@]}"; do
  if output="$(PATH="$scratch/failing-grep:$PATH" bash "$check" 2>&1)"; then
    fail "$check passed after its text search failed"
  fi
  case "$output" in
    *"text search failed"*) ;;
    *) fail "$check did not classify a text search failure" ;;
  esac
done

echo "ci-fast portability self-test OK"

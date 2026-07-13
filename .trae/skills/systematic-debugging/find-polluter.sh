#!/usr/bin/env bash
# Bisection script to find which test creates unwanted files/state
# Usage: ./find-polluter.sh <file_or_dir_to_check> <test_pattern>
# Example: ./find-polluter.sh '.git' 'src/**/*.test.ts'

set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "Usage: $0 <file_to_check> <test_pattern>"
  echo "Example: $0 '.git' 'src/**/*.test.ts'"
  exit 2
fi

POLLUTION_CHECK="$1"
TEST_PATTERN="$2"

echo "🔍 Searching for test that creates: $POLLUTION_CHECK"
echo "Test pattern: $TEST_PATTERN"
echo ""

scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-find-polluter.XXXXXX")"
trap 'rm -rf "$scratch"' EXIT

pollution_exists() {
  [[ -e "$POLLUTION_CHECK" || -L "$POLLUTION_CHECK" ]]
}

TEST_FILES=()
test_list="$scratch/test-files.list"
if ! find . -path "$TEST_PATTERN" -print0 > "$test_list"; then
  echo "Test discovery failed" >&2
  exit 2
fi
while IFS= read -r -d '' test_file; do
  TEST_FILES+=("$test_file")
done < "$test_list"
TOTAL=${#TEST_FILES[@]}

if [[ "$TOTAL" -eq 0 ]]; then
  echo "No tests matched" >&2
  exit 2
fi

if pollution_exists; then
  echo "Pollution already exists: $POLLUTION_CHECK" >&2
  exit 2
fi

echo "Found $TOTAL test files"
echo ""

COUNT=0
failed_tests=()
for TEST_FILE in "${TEST_FILES[@]}"; do
  COUNT=$((COUNT + 1))
  echo "[$COUNT/$TOTAL] Testing: $TEST_FILE"

  log="$scratch/test-$COUNT.log"
  status=0
  npm test -- "$TEST_FILE" > "$log" 2>&1 || status=$?

  if pollution_exists; then
    echo ""
    echo "FOUND POLLUTER: $TEST_FILE"
    echo "   Command status: $status"
    echo "   Created: $POLLUTION_CHECK"
    exit 1
  fi

  if [[ "$status" -ne 0 ]]; then
    failed_tests+=("$TEST_FILE")
  fi
done

if [[ ${#failed_tests[@]} -gt 0 ]]; then
  printf 'Test failed without pollution: %s\n' "${failed_tests[@]}" >&2
  exit 2
fi

echo ""
echo "✅ No polluter found - all tests clean!"
exit 0

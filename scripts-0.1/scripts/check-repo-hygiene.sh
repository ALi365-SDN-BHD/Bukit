#!/usr/bin/env bash
set -euo pipefail

# Check that no build artifacts are tracked in the repository.
# Run from repo root: bash scripts/check-repo-hygiene.sh

patterns=(
    '.smoke-all-run-debug/'
    '.smoke-all-run/'
    '.sitegen-smoke-ai-'
    '.bukit-build-state.json'
    '.bukit-output-marker'
)

violations=""
for pattern in "${patterns[@]}"; do
    matches="$(git ls-files -- "$pattern" '*'"$pattern"'*' 2>/dev/null || true)"
    if [ -n "$matches" ]; then
        violations+="$matches"$'\n'
    fi
done

if [ -n "$violations" ]; then
    echo "ERROR: build artifacts found in repository:" >&2
    echo "$violations" >&2
    exit 1
fi

echo "Repo hygiene: clean"

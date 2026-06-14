#!/usr/bin/env bash
set -euo pipefail

if ! command -v python3 >/dev/null 2>&1; then
  echo "ERROR: python3 is required for skills strict validation." >&2
  exit 1
fi

if ! python3 -c "import yaml" >/dev/null 2>&1; then
  if [ "${CI:-false}" = "true" ]; then
    echo "Installing PyYAML for CI environment..." >&2
    python3 -m pip install --user pyyaml
  else
    echo "ERROR: PyYAML is required by guide/skills/scripts/validate-skills-strict.sh." >&2
    echo "Install it with: python3 -m pip install pyyaml" >&2
    exit 1
  fi
fi

if ! python3 -c "import yaml" >/dev/null 2>&1; then
  echo "ERROR: PyYAML missing or import failed after install attempt." >&2
  echo "Please ensure pip can install packages from the configured index." >&2
  exit 1
fi


#!/usr/bin/env bash
set -euo pipefail

SKILLS_DIR="$(cd "$(dirname "$0")/.." && pwd)"
YAML_FILE="$SKILLS_DIR/skills-index.yaml"
JSON_FILE="$SKILLS_DIR/skills-index.json"

if [ ! -f "$YAML_FILE" ]; then
  echo "Error: $YAML_FILE not found"
  exit 1
fi

if command -v yq &>/dev/null; then
  echo "Generating $JSON_FILE from $YAML_FILE using yq..."
  yq -o json "$YAML_FILE" > "$JSON_FILE"
  echo "Done: $(wc -c < "$JSON_FILE") bytes"
elif command -v python3 &>/dev/null; then
  echo "yq not found, using python3 fallback..."
  python3 -c "
import json, sys, os
# Simple YAML to JSON for our skills-index format
# Falls back to manual parsing if PyYAML is not available
try:
    import yaml
    with open('$YAML_FILE') as f:
        data = yaml.safe_load(f)
    with open('$JSON_FILE', 'w') as f:
        json.dump(data, f, indent=2, ensure_ascii=False)
    print(f'Done: {len(json.dumps(data))} bytes')
except ImportError:
    print('Warning: Neither PyYAML nor yq is installed. Skipping JSON generation.', file=__import__('sys').stderr)
    print('Install PyYAML: pip3 install pyyaml')
    print('Or install yq: brew install yq')
    if os.environ.get('CI', '') == 'true':
        print('ERROR: yq and PyYAML not available in CI — refusing to skip JSON generation', file=sys.stderr)
        sys.exit(1)
    exit(0)
"
else
  echo "Warning: Neither yq nor python3 is available. Skipping JSON generation."
  exit 0
fi

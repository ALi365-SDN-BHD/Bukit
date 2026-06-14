#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "usage: validate-artifacts-json.sh <output-dir>" >&2
  exit 2
fi

output_dir="$1"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [ ! -d "$output_dir" ]; then
  echo "ERROR: output directory not found: $output_dir" >&2
  exit 1
fi

found=0
while IFS= read -r -d '' artifact; do
  found=1
  schema_uri="$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1], encoding="utf-8")).get("schema", ""))' "$artifact")"
  if [ -z "$schema_uri" ]; then
    echo "ERROR: $artifact does not declare a schema field." >&2
    exit 1
  fi

  schema_name="$(basename "$schema_uri" .json).schema.json"
  schema_path="$repo_root/docs/schemas/$schema_name"
  if [ ! -f "$schema_path" ]; then
    echo "ERROR: $artifact declares $schema_uri but $schema_path is missing." >&2
    exit 1
  fi

  python3 "$repo_root/scripts/validate-json-schema.py" "$schema_path" "$artifact"
done < <(find "$output_dir" -path '*/.bukit/*.json' -type f -print0 | sort -z)

if [ "$found" -eq 0 ]; then
  echo "ERROR: no .bukit/*.json artifacts found under $output_dir" >&2
  exit 1
fi

echo "Artifact schema validation OK: $output_dir"

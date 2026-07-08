#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "Usage: $0 <input-json> <output-json>" >&2
  exit 2
fi

input_path="$1"
output_path="$2"

if [ ! -f "$input_path" ]; then
  echo "ERROR: input file not found: $input_path" >&2
  exit 1
fi

mkdir -p "$(dirname "$output_path")"

if command -v jq >/dev/null 2>&1; then
  jq --sort-keys '
    def normalize:
      walk(
        if type == "object" then
          del(
            .startedAt,
            .endedAt,
            .durationMs,
            .generatedAt,
            .ts,
            .root,
            .output,
            .outputDir
          )
        else
          .
        end
      );
    normalize
  ' "$input_path" > "$output_path"
else
  python3 -m json.tool "$input_path" "$output_path"
fi

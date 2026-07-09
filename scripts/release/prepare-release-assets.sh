#!/usr/bin/env bash
set -euo pipefail

version="${1:-}"
commit="${2:-}"
output_dir="${3:-}"

if [[ -z "$version" || -z "$commit" || -z "$output_dir" ]]; then
  echo "usage: bash scripts/release/prepare-release-assets.sh <version> <commit> <output-dir> <archive>..." >&2
  exit 2
fi

shift 3
if [[ "$#" -eq 0 ]]; then
  echo "at least one archive is required" >&2
  exit 2
fi

mkdir -p "$output_dir"
checksums="$output_dir/checksums.txt"
: > "$checksums"

hash_file() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | awk '{print $1}'
  else
    shasum -a 256 "$1" | awk '{print $1}'
  fi
}

for archive in "$@"; do
  if [[ ! -f "$archive" ]]; then
    echo "missing archive: $archive" >&2
    exit 1
  fi

  name="$(basename "$archive")"
  dest="$output_dir/$name"
  cp -f "$archive" "$dest"
  printf '%s  %s\n' "$(hash_file "$dest")" "$name" >> "$checksums"
done

python3 - "$version" "$commit" "$output_dir" <<'PY'
import json
import pathlib
import sys

version, commit, output_dir = sys.argv[1], sys.argv[2], pathlib.Path(sys.argv[3])
assets = []
for line in (output_dir / "checksums.txt").read_text(encoding="utf-8").splitlines():
    sha256, name = line.split(None, 1)
    path = output_dir / name
    assets.append({"name": name, "sha256": sha256, "bytes": path.stat().st_size})

assets.sort(key=lambda item: item["name"])
manifest = {
    "schema": "bukit-release-manifest-v1",
    "version": version,
    "commit": commit,
    "assets": assets,
}
(output_dir / "release-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
(output_dir / "checksums.json").write_text(json.dumps({"assets": assets}, indent=2) + "\n", encoding="utf-8")
PY

echo "release assets prepared: $output_dir"

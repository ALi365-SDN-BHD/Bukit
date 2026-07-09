#!/usr/bin/env bash
set -euo pipefail

version="${1:-}"
commit="${2:-}"
asset_dir="${3:-}"

if [[ -z "$version" || -z "$commit" || -z "$asset_dir" ]]; then
  echo "usage: bash scripts/release/verify-release-assets.sh <version> <commit> <asset-dir>" >&2
  exit 2
fi

if [[ ! -d "$asset_dir" ]]; then
  echo "missing asset dir: $asset_dir" >&2
  exit 1
fi

python3 - "$version" "$commit" "$asset_dir" <<'PY'
import hashlib
import json
import pathlib
import sys

version, commit, asset_dir = sys.argv[1], sys.argv[2], pathlib.Path(sys.argv[3])
manifest_path = asset_dir / "release-manifest.json"
checksums_path = asset_dir / "checksums.txt"
checksums_json_path = asset_dir / "checksums.json"

for path in (manifest_path, checksums_path, checksums_json_path):
    if not path.is_file():
        raise SystemExit(f"missing release asset metadata: {path}")

manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
checksums_json = json.loads(checksums_json_path.read_text(encoding="utf-8"))
assets = manifest.get("assets")

if manifest.get("schema") != "bukit-release-manifest-v1":
    raise SystemExit("unexpected release manifest schema")
if manifest.get("version") != version:
    raise SystemExit("release manifest version mismatch")
if manifest.get("commit") != commit:
    raise SystemExit("release manifest commit mismatch")
if not isinstance(assets, list) or not assets:
    raise SystemExit("release manifest must list at least one asset")
if checksums_json.get("assets") != assets:
    raise SystemExit("checksums.json does not match release-manifest.json")

expected = {}
for line in checksums_path.read_text(encoding="utf-8").splitlines():
    sha256, name = line.split(None, 1)
    expected[name] = sha256

for asset in assets:
    name = asset["name"]
    path = asset_dir / name
    if not path.is_file():
        raise SystemExit(f"missing release asset: {name}")
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    if digest != asset["sha256"] or expected.get(name) != digest:
        raise SystemExit(f"checksum mismatch: {name}")
    if path.stat().st_size != asset["bytes"]:
        raise SystemExit(f"size mismatch: {name}")

print(f"release assets OK: {asset_dir}")
PY

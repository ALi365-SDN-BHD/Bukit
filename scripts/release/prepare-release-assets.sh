#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/release/prepare-release-assets.sh <version> <commit> <asset...>

Generate release integrity files in current working directory:
  - checksums.txt
  - checksums.json
  - release-manifest.json

Arguments:
  <version> Version without 'v' prefix, e.g. 1.0.2
  <commit> Commit SHA used in release-manifest
  <asset...> One or more release artifact files
USAGE
}

if [ "$#" -lt 3 ]; then
  usage
  exit 1
fi

VERSION="$1"
COMMIT_SHA="$2"
shift 2
ASSETS=("$@")

for asset in "${ASSETS[@]}"; do
  if [ ! -f "$asset" ]; then
    echo "ERROR: missing release asset: $asset" >&2
    exit 1
  fi
done

python3 - "$VERSION" "$COMMIT_SHA" "${ASSETS[@]}" <<'PY'
import hashlib
import json
import sys
from pathlib import Path


def infer_rid(name: str, version: str) -> str:
    if name == "bukit-skills.zip":
        return "skills"

    prefix = f"bukit-{version}-"
    if not name.startswith(prefix):
        raise SystemExit(f"ERROR: unexpected artifact naming pattern: {name}")

    rid = name[len(prefix):]
    if rid.endswith(".tar.gz"):
        rid = rid[: -len(".tar.gz")]
    elif rid.endswith(".zip"):
        rid = rid[: -len(".zip")]

    if not rid:
        raise SystemExit(f"ERROR: failed to infer rid for artifact: {name}")
    return rid


def bundle_hash(items):
    hasher = hashlib.sha256()
    for item in items:
        line = f"{item['path']}|{item['hash']}|{item['size']}\\n"
        hasher.update(line.encode("utf-8"))
    return f"sha256:{hasher.hexdigest()}"


version, commit, *asset_names = sys.argv[1:]

artifacts = []
files = []

with open("checksums.txt", "w", encoding="utf-8") as f:
    f.write("# schema=https://bukit.dev/schemas/release-bundle-checksums.v1.json\\n")
    f.write(f"# version={version}\\n")
    f.write(f"# commit={commit}\\n")
    f.write(f"# artifacts={len(asset_names)}\\n")

for asset_name in asset_names:
    p = Path(asset_name)
    data = p.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    prefixed_hash = f"sha256:{digest}"

    with open("checksums.txt", "a", encoding="utf-8") as f:
        f.write(f"{digest}  {p.name}\\n")

    files.append(
        {
            "path": p.name,
            "hash": prefixed_hash,
            "size": p.stat().st_size,
        }
    )

    artifacts.append(
        {
            "rid": infer_rid(p.name, version),
            "file": p.name,
            "sha256": prefixed_hash,
        }
    )

artifacts.sort(key=lambda item: (item["rid"], item["file"]))
files.sort(key=lambda item: item["path"])

checksums = {
    "schema": "https://bukit.dev/schemas/release-bundle-checksums.v1.json",
    "schemaVersion": "1.0",
    "fileCount": len(files),
    "bundleHash": bundle_hash(files),
    "files": files,
}

manifest = {
    "version": version,
    "commit": commit,
    "bundleHash": checksums["bundleHash"],
    "artifacts": artifacts,
}

with open("checksums.json", "w", encoding="utf-8") as f:
    json.dump(checksums, f, indent=2, sort_keys=False)
    f.write("\\n")

with open("release-manifest.json", "w", encoding="utf-8") as f:
    json.dump(manifest, f, indent=2, sort_keys=False)
    f.write("\\n")
PY

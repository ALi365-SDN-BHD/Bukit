#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/checks/release-assets.sh <asset-dir> <release-version> [release-commit]

Validate release assets for Bukit 1.0.2-like bundle.

Arguments:
  <asset-dir>      Directory that contains release assets.
  <release-version>Release version without 'v' prefix, e.g. 1.0.2
  [release-commit] Optional commit SHA used in release-manifest.json validation.
USAGE
}

if [ "$#" -lt 2 ] || [ "$#" -gt 3 ]; then
  usage
  exit 1
fi

asset_dir="$1"
version="$2"
expected_commit="${3:-}"

if [ ! -d "$asset_dir" ]; then
  echo "ERROR: asset directory not found: $asset_dir" >&2
  exit 1
fi

checksums_txt="$asset_dir/checksums.txt"
checksums_json="$asset_dir/checksums.json"
manifest_json="$asset_dir/release-manifest.json"

for required in "$checksums_txt" "$checksums_json" "$manifest_json"; do
  if [ ! -f "$required" ]; then
    echo "ERROR: required file missing: $required" >&2
    exit 1
  fi
done

required_files=(
  "bukit-${version}-linux-x64.tar.gz"
  "bukit-${version}-osx-arm64.tar.gz"
  "bukit-${version}-win-x64.zip"
  "bukit-skills.zip"
)

for required_file in "${required_files[@]}"; do
  if [ ! -f "$asset_dir/$required_file" ]; then
    echo "ERROR: release asset missing: $required_file" >&2
    exit 1
  fi
done

python3 - "$asset_dir" "$version" "$expected_commit" "${required_files[@]}" <<'PY'
import hashlib
import json
import re
import sys
from pathlib import Path


def compute_bundle_hash(items):
    hasher = hashlib.sha256()
    for item in sorted(items, key=lambda entry: entry["path"]):
        line = f"{item['path']}|{item['hash']}|{item['size']}\n"
        hasher.update(line.encode("utf-8"))
    return f"sha256:{hasher.hexdigest()}"


asset_dir = Path(sys.argv[1])
version = sys.argv[2]
expected_commit = sys.argv[3] or None
required_files = sys.argv[4:]

checksums_txt = asset_dir / "checksums.txt"
checksums_json = asset_dir / "checksums.json"
manifest_json = asset_dir / "release-manifest.json"

hash_line = re.compile(r"^[0-9a-f]{64}\s{2}([^\s].*)$")

# Parse checksums.txt
headers = {}
text_lines = checksums_txt.read_text(encoding="utf-8").splitlines()
checksum_lines = []
for line in text_lines:
    if not line.strip():
        continue
    if line.startswith("#"):
        if "=" in line:
            key, value = line[1:].split("=", 1)
            headers[key.strip()] = value.strip()
        continue
    match = hash_line.match(line)
    if not match:
        raise SystemExit(f"ERROR: invalid checksums.txt line: {line}")
    sha, filename = line.split("  ", 1)
    checksum_lines.append((filename, sha))

required_headers = {
    "schema": "https://bukit.dev/schemas/release-bundle-checksums.v1.json",
}
for key, expected in required_headers.items():
    actual = headers.get(key)
    if actual != expected:
        raise SystemExit(f"ERROR: checksums.txt missing/invalid header: {key}={expected}")

for hdr in ("version", "commit", "artifacts"):
    if hdr not in headers:
        raise SystemExit(f"ERROR: checksums.txt missing header: #{hdr}")

if headers["version"] != version:
    raise SystemExit(f"ERROR: checksums.txt version {headers['version']} != expected {version}")

if expected_commit and headers["commit"] != expected_commit:
    raise SystemExit(f"ERROR: checksums.txt commit {headers['commit']} != expected {expected_commit}")

try:
    checksums_txt_artifact_count = int(headers["artifacts"])
except ValueError:
    raise SystemExit("ERROR: checksums.txt #artifacts header must be an integer")

required_file_set = set(required_files)

checksum_filenames = [filename for filename, _ in checksum_lines]
if len(checksum_filenames) != len(set(checksum_filenames)):
    raise SystemExit("ERROR: checksums.txt contains duplicate file entries")
if checksums_txt_artifact_count != len(checksum_lines):
    raise SystemExit("ERROR: checksums.txt #artifacts header does not match checksum line count")
if checksums_txt_artifact_count != len(required_file_set):
    raise SystemExit("ERROR: checksums.txt #artifacts header must match required artifact count")

if set(checksum_filenames) != required_file_set:
    missing = ", ".join(sorted(required_file_set - set(checksum_filenames)))
    extra = ", ".join(sorted(set(checksum_filenames) - required_file_set))
    detail = []
    if missing:
        detail.append(f"missing={missing}")
    if extra:
        detail.append(f"extra={extra}")
    raise SystemExit(
        f"ERROR: checksums.txt files must match required artifacts exactly: {', '.join(detail)}"
    )

# Parse checksums.json
checksums_obj = json.loads(checksums_json.read_text(encoding="utf-8"))
if checksums_obj.get("schema") != "https://bukit.dev/schemas/release-bundle-checksums.v1.json":
    raise SystemExit("ERROR: checksums.json schema mismatch")
if checksums_obj.get("schemaVersion") != "1.0":
    raise SystemExit("ERROR: checksums.json schemaVersion must be 1.0")
checksums_bundle_hash = checksums_obj.get("bundleHash")
if not isinstance(checksums_bundle_hash, str) or not checksums_bundle_hash.startswith("sha256:"):
    raise SystemExit("ERROR: checksums.json missing or invalid bundleHash")

files = checksums_obj.get("files")
if not isinstance(files, list):
    raise SystemExit("ERROR: checksums.json files must be an array")

if checksums_obj.get("fileCount") != len(files):
    raise SystemExit("ERROR: checksums.json fileCount mismatch")
if checksums_obj.get("fileCount") != len(required_file_set):
    raise SystemExit("ERROR: checksums.json fileCount must match required artifact count")
if len(files) != len(set(item.get("path") for item in files if isinstance(item, dict))):
    raise SystemExit("ERROR: checksums.json contains duplicate file paths")

file_entries = []
checksum_map = {}
for item in files:
    if not isinstance(item, dict):
        raise SystemExit("ERROR: checksums.json file entry must be object")
    path = item.get("path")
    hash_value = item.get("hash")
    size = item.get("size")
    if not isinstance(path, str):
        raise SystemExit("ERROR: checksums.json file entry path missing/invalid")
    if not isinstance(hash_value, str) or not hash_value.startswith("sha256:"):
        raise SystemExit(f"ERROR: checksums.json hash invalid for {path}")
    if not isinstance(size, int) or size < 0:
        raise SystemExit(f"ERROR: checksums.json size invalid for {path}")
    file_entries.append(path)
    checksum_map[path] = item

if set(file_entries) != required_file_set:
    missing = ", ".join(sorted(required_file_set - set(file_entries)))
    extra = ", ".join(sorted(set(file_entries) - required_file_set))
    detail = []
    if missing:
        detail.append(f"missing={missing}")
    if extra:
        detail.append(f"extra={extra}")
    raise SystemExit(
        f"ERROR: checksums.json files must match required artifacts exactly: {', '.join(detail)}"
    )

# Parse manifest
manifest = json.loads(manifest_json.read_text(encoding="utf-8"))
if manifest.get("schema") != "https://bukit.dev/schemas/release-manifest.v1.json":
    raise SystemExit("ERROR: manifest schema mismatch")
if manifest.get("schemaVersion") != "1.0":
    raise SystemExit("ERROR: manifest schemaVersion must be 1.0")
if manifest.get("version") != version:
    raise SystemExit(f"ERROR: manifest.version {manifest.get('version')} != expected {version}")
if manifest.get("bundleHash") != checksums_bundle_hash:
    raise SystemExit("ERROR: manifest.bundleHash must match checksums.json.bundleHash")

if expected_commit and manifest.get("commit") != expected_commit:
    raise SystemExit(f"ERROR: manifest.commit {manifest.get('commit')} != expected {expected_commit}")
if headers["commit"] != manifest.get("commit"):
    raise SystemExit("ERROR: checksums.txt commit must match manifest.commit")

artifacts = manifest.get("artifacts")
if not isinstance(artifacts, list) or not artifacts:
    raise SystemExit("ERROR: manifest artifacts missing")

if len(artifacts) != len(required_file_set):
    raise SystemExit("ERROR: manifest must contain exactly required artifact count")

artifact_files = [
    item.get("file")
    for item in artifacts
    if isinstance(item, dict)
]
if len(artifacts) != len(set(artifact_files)):
    raise SystemExit("ERROR: manifest contains duplicate artifact file names")

expected_rids = {"linux-x64", "osx-arm64", "win-x64", "skills"}
expected_artifacts_by_rid = {
    "linux-x64": f"bukit-{version}-linux-x64.tar.gz",
    "osx-arm64": f"bukit-{version}-osx-arm64.tar.gz",
    "win-x64": f"bukit-{version}-win-x64.zip",
    "skills": "bukit-skills.zip",
}

artifact_rids = [
    item.get("rid")
    for item in artifacts
    if isinstance(item, dict) and isinstance(item.get("rid"), str)
]
if set(artifact_rids) != expected_rids:
    missing = ", ".join(sorted(expected_rids - set(artifact_rids)))
    extra = ", ".join(sorted(set(artifact_rids) - expected_rids))
    detail = []
    if missing:
        detail.append(f"missing={missing}")
    if extra:
        detail.append(f"extra={extra}")
    raise SystemExit(f"ERROR: manifest rid set mismatch: {', '.join(detail)}")

seen_rids = set()
seen_files = set()
for item in artifacts:
    if not isinstance(item, dict):
        raise SystemExit("ERROR: manifest artifact entry must be object")

    if set(item.keys()) != {"rid", "file", "sha256"}:
        raise SystemExit(
            f"ERROR: manifest artifact keys must be exactly rid, file, sha256: {item.get('file')}"
        )

    rid = item.get("rid")
    filename = item.get("file")
    sha = item.get("sha256")

    if rid not in expected_rids:
        raise SystemExit(f"ERROR: unexpected rid: {rid}")

    if not filename or not isinstance(filename, str):
        raise SystemExit("ERROR: manifest item missing file")

    if filename in seen_files:
        raise SystemExit(f"ERROR: duplicate manifest file: {filename}")

    if filename != expected_artifacts_by_rid[rid]:
        raise SystemExit(f"ERROR: manifest filename mismatch for rid {rid}: {filename}")

    if not isinstance(sha, str) or not sha.startswith("sha256:"):
        raise SystemExit(f"ERROR: manifest sha missing prefix for {filename}")

    hex_value = sha[len("sha256:"):]
    if len(hex_value) != 64 or any(c not in '0123456789abcdef' for c in hex_value.lower()):
        raise SystemExit(f"ERROR: manifest sha invalid for {filename}")

    if not filename.endswith((".zip", ".tar.gz")) and rid != "skills":
        raise SystemExit(f"ERROR: unexpected manifest extension for {filename}")

    seen_rids.add(rid)
    seen_files.add(filename)

    if filename not in checksum_map:
        raise SystemExit(f"ERROR: manifest file missing in checksums.json: {filename}")

    if sha != checksum_map[filename].get("hash"):
        raise SystemExit(f"ERROR: manifest sha mismatch: {filename}")

if seen_files != required_file_set:
    missing = ", ".join(sorted(required_file_set - seen_files))
    extra = ", ".join(sorted(seen_files - required_file_set))
    detail = []
    if missing:
        detail.append(f"missing={missing}")
    if extra:
        detail.append(f"extra={extra}")
    raise SystemExit(
        f"ERROR: manifest file set must match required artifacts exactly: {', '.join(detail)}"
    )

sorted_artifacts = sorted(artifacts, key=lambda item: (item.get("rid", ""), item.get("file", "")))
if artifacts != sorted_artifacts:
    raise SystemExit("ERROR: manifest artifacts are not sorted by (rid, file)")

for filename, expected in checksum_lines:
    expected_file = asset_dir / filename
    data = expected_file.read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if digest != expected:
        raise SystemExit(f"ERROR: checksums.txt mismatch: {filename}")

    expected_obj = checksum_map.get(filename)
    if not expected_obj:
        raise SystemExit(f"ERROR: checksums.txt file not in checksums.json: {filename}")

    expected_json_hash = expected_obj.get("hash")
    if expected_json_hash != f"sha256:{digest}":
        raise SystemExit(f"ERROR: checksums.json mismatch: {filename}")

    if expected_obj.get("size") != len(data):
        raise SystemExit(f"ERROR: checksums.json size mismatch: {filename}")

computed_bundle_hash = compute_bundle_hash(files)
if checksums_bundle_hash != computed_bundle_hash:
    raise SystemExit("ERROR: checksums.json bundleHash mismatch")

for required in required_files:
    if not any(item[0] == required for item in checksum_lines):
        raise SystemExit(f"ERROR: required artifact absent in checksums.txt: {required}")

for f in required_files:
    path = asset_dir / f
    if path.exists():
        continue
    raise SystemExit(f"ERROR: expected platform artifact missing: {f}")

print("release asset checks passed")
print(f"version={version}")
print(f"commit={manifest.get('commit')}")
print(f"required_files={','.join(sorted(required_file_set))}")
print(f"artifact_count={len(required_file_set)}")
print("checksums_txt_count=%d" % len(checksum_lines))
print("manifest_rids=%s" % ",".join(sorted(expected_rids)))
print("set_mode=exact-match")
PY

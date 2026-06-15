#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
version="1.0.3"
commit="0123456789abcdef0123456789abcdef01234567"
tmp_root="$(mktemp -d)"

cleanup() {
  rm -rf "$tmp_root"
}
trap cleanup EXIT

assert_contains() {
  local file="$1"
  local expected="$2"

  if ! grep -Fq "$expected" "$file"; then
    echo "ERROR: expected output to contain '$expected'" >&2
    echo "---- output ----" >&2
    cat "$file" >&2
    echo "----------------" >&2
    exit 1
  fi
}

run_validator() {
  local asset_dir="$1"
  local expected_commit="${2:-$commit}"
  local expected_version="${3:-$version}"
  bash "${repo_root}/scripts/checks/release-assets.sh" "$asset_dir" "$expected_version" "$expected_commit"
}

prepare_valid_fixture() {
  local work_dir="$1"

  mkdir -p "$work_dir"

  python3 - "$work_dir" "$version" <<'PY'
import sys
import tarfile
import zipfile
from io import BytesIO
from pathlib import Path

work_dir = Path(sys.argv[1])
version = sys.argv[2]

artifacts = [
    f"bukit-{version}-linux-x64.tar.gz",
    f"bukit-{version}-osx-arm64.tar.gz",
    f"bukit-{version}-win-x64.zip",
    "bukit-skills.zip",
]

for name in artifacts:
    path = work_dir / name
    if name.endswith(".tar.gz"):
        buf = BytesIO()
        with tarfile.open(fileobj=buf, mode="w:gz") as tar:
            info = tarfile.TarInfo(name="dummy.txt")
            info.size = 10
            tar.addfile(info, BytesIO(b"0123456789"))
        path.write_bytes(buf.getvalue())
    elif name.endswith(".zip"):
        buf = BytesIO()
        with zipfile.ZipFile(buf, mode="w", compression=zipfile.ZIP_DEFLATED) as zf:
            zf.writestr("dummy.txt", "0123456789")
        path.write_bytes(buf.getvalue())
PY

  (
    cd "$work_dir"
    bash "${repo_root}/scripts/release/prepare-release-assets.sh" \
      "$version" \
      "$commit" \
      "bukit-${version}-linux-x64.tar.gz" \
      "bukit-${version}-osx-arm64.tar.gz" \
      "bukit-${version}-win-x64.zip" \
      "bukit-skills.zip"
  )
}

copy_valid_case() {
  local case_name="$1"
  local case_dir="${tmp_root}/${case_name}"

  cp -R "${tmp_root}/valid" "$case_dir"
  printf "%s\n" "$case_dir"
}

expect_failure() {
  local case_name="$1"
  local expected="$2"
  local expected_commit="${3:-$commit}"
  local expected_version="${4:-$version}"
  local case_dir="${tmp_root}/${case_name}"
  local output="${tmp_root}/${case_name}.out"

  if run_validator "$case_dir" "$expected_commit" "$expected_version" >"$output" 2>&1; then
    echo "ERROR: invalid fixture unexpectedly passed: $case_name" >&2
    cat "$output" >&2
    exit 1
  fi

  assert_contains "$output" "$expected"
  echo "invalid fixture failed as expected: $case_name"
}

mutate_json() {
  local file="$1"
  local expression="$2"

  python3 - "$file" "$expression" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
expression = sys.argv[2]
obj = json.loads(path.read_text(encoding="utf-8"))
exec(expression, {"obj": obj})
path.write_text(json.dumps(obj, indent=2, sort_keys=False) + "\n", encoding="utf-8")
PY
}

prepare_valid_fixture "${tmp_root}/valid"

valid_output="${tmp_root}/valid.out"
run_validator "${tmp_root}/valid" >"$valid_output"
assert_contains "$valid_output" "release asset checks passed"
assert_contains "$valid_output" "set_mode=exact-match"
assert_contains "$valid_output" "manifest_rids=linux-x64,osx-arm64,skills,win-x64"
echo "valid generated fixture passed"

missing_checksums="$(copy_valid_case missing-checksums)"
rm "$missing_checksums/checksums.txt"
expect_failure missing-checksums "required file missing"

missing_checksums_json="$(copy_valid_case missing-checksums-json)"
rm "$missing_checksums_json/checksums.json"
expect_failure missing-checksums-json "required file missing"

missing_manifest="$(copy_valid_case missing-manifest)"
rm "$missing_manifest/release-manifest.json"
expect_failure missing-manifest "required file missing"

manifest_schema_mismatch="$(copy_valid_case manifest-schema-mismatch)"
mutate_json "$manifest_schema_mismatch/release-manifest.json" "obj['schema'] = 'https://example.com/wrong-schema.json'"
expect_failure manifest-schema-mismatch "manifest schema mismatch"

manifest_schema_version_mismatch="$(copy_valid_case manifest-schema-version-mismatch)"
mutate_json "$manifest_schema_version_mismatch/release-manifest.json" "obj['schemaVersion'] = '2.0'"
expect_failure manifest-schema-version-mismatch "manifest schemaVersion must be 1.0"

duplicate_artifact="$(copy_valid_case duplicate-artifact)"
mutate_json "$duplicate_artifact/release-manifest.json" "obj['artifacts'][-1] = dict(obj['artifacts'][0])"
expect_failure duplicate-artifact "manifest contains duplicate artifact file names"

manifest_extra_file="$(copy_valid_case manifest-extra-file)"
mutate_json "$manifest_extra_file/release-manifest.json" "obj['artifacts'] = [item for item in obj['artifacts'] if item.get('rid') != 'skills']"
expect_failure manifest-extra-file "manifest must contain exactly required artifact count"

manifest_missing_rid="$(copy_valid_case manifest-missing-rid)"
mutate_json "$manifest_missing_rid/release-manifest.json" "next(item for item in obj['artifacts'] if item.get('rid') == 'skills')['rid'] = 'linux-x64'"
expect_failure manifest-missing-rid "manifest rid set mismatch: missing=skills"

checksum_count_mismatch="$(copy_valid_case checksum-count-mismatch)"
mutate_json "$checksum_count_mismatch/checksums.json" "obj['fileCount'] = obj['fileCount'] + 1"
expect_failure checksum-count-mismatch "checksums.json fileCount mismatch"

checksum_json_set_mismatch="$(copy_valid_case checksum-json-set-mismatch)"
mutate_json "$checksum_json_set_mismatch/checksums.json" "obj['files'][0]['path'] = 'unexpected-artifact.zip'"
expect_failure checksum-json-set-mismatch "checksums.json files must match required artifacts exactly: missing=bukit-1.0.3-linux-x64.tar.gz, extra=unexpected-artifact.zip"

checksum_txt_artifacts_mismatch="$(copy_valid_case checksum-txt-artifacts-mismatch)"
python3 - "$checksum_txt_artifacts_mismatch/checksums.txt" <<'PY'
import sys
from pathlib import Path

path = Path(sys.argv[1])
text = path.read_text(encoding="utf-8")
path.write_text(text.replace("# artifacts=4", "# artifacts=3"), encoding="utf-8")
PY
expect_failure checksum-txt-artifacts-mismatch "checksums.txt #artifacts header does not match checksum line count"

checksum_txt_mismatch="$(copy_valid_case checksum-txt-mismatch)"
python3 - "$checksum_txt_mismatch/checksums.txt" <<'PY'
import sys
from pathlib import Path

path = Path(sys.argv[1])
lines = path.read_text(encoding="utf-8").splitlines()
for index, line in enumerate(lines):
    if line and not line.startswith("#"):
        lines[index] = "0" * 64 + line[64:]
        break
path.write_text("\n".join(lines) + "\n", encoding="utf-8")
PY
expect_failure checksum-txt-mismatch "checksums.txt mismatch"

checksum_json_size_mismatch="$(copy_valid_case checksum-json-size-mismatch)"
mutate_json "$checksum_json_size_mismatch/checksums.json" "obj['files'][0]['size'] = obj['files'][0]['size'] + 1"
expect_failure checksum-json-size-mismatch "checksums.json size mismatch"

manifest_sha_mismatch="$(copy_valid_case manifest-sha-mismatch)"
mutate_json "$manifest_sha_mismatch/release-manifest.json" "obj['artifacts'][0]['sha256'] = 'sha256:' + ('0' * 64)"
expect_failure manifest-sha-mismatch "manifest sha mismatch: bukit-1.0.3-linux-x64.tar.gz"

bundle_hash_mismatch="$(copy_valid_case bundle-hash-mismatch)"
mutate_json "$bundle_hash_mismatch/checksums.json" "obj['bundleHash'] = 'sha256:' + ('0' * 64)"
mutate_json "$bundle_hash_mismatch/release-manifest.json" "obj['bundleHash'] = 'sha256:' + ('0' * 64)"
expect_failure bundle-hash-mismatch "checksums.json bundleHash mismatch"

wrong_version="$(copy_valid_case wrong-version)"
python3 - "$wrong_version/checksums.txt" <<'PY'
import sys
from pathlib import Path

path = Path(sys.argv[1])
text = path.read_text(encoding="utf-8")
path.write_text(text.replace("# version=1.0.3", "# version=1.0.4"), encoding="utf-8")
PY
expect_failure wrong-version "checksums.txt version 1.0.4 != expected ${version}"

wrong_commit="$(copy_valid_case wrong-commit)"
expect_failure wrong-commit "checksums.txt commit ${commit} != expected fedcba9876543210fedcba9876543210fedcba98" "fedcba9876543210fedcba9876543210fedcba98"

wrong_checksum_commit="$(copy_valid_case wrong-checksum-commit)"
python3 - "$wrong_checksum_commit/checksums.txt" <<'PY'
import sys
from pathlib import Path

path = Path(sys.argv[1])
text = path.read_text(encoding="utf-8")
path.write_text(
    text.replace(
        "# commit=0123456789abcdef0123456789abcdef01234567",
        "# commit=fedcba9876543210fedcba9876543210fedcba98",
    ),
    encoding="utf-8",
)
PY
expect_failure wrong-checksum-commit "checksums.txt commit fedcba9876543210fedcba9876543210fedcba98 != expected ${commit}"

unstable_order="$(copy_valid_case unstable-order)"
mutate_json "$unstable_order/release-manifest.json" "obj['artifacts'] = list(reversed(obj['artifacts']))"
expect_failure unstable-order "manifest artifacts are not sorted by (rid, file)"

release_assets_dir="${tmp_root}/release-assets"
mkdir -p "$release_assets_dir"
cp "${tmp_root}/valid/"* "$release_assets_dir/"
summary="${tmp_root}/release-assets.out"
run_validator "$release_assets_dir" >"$summary"
assert_contains "$summary" "release asset checks passed"
assert_contains "$summary" "set_mode=exact-match"
assert_contains "$summary" "manifest_rids=linux-x64,osx-arm64,skills,win-x64"

echo "release asset fixture test OK"

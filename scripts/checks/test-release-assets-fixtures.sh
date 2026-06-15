#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
version="1.0.4"
commit="0123456789abcdef0123456789abcdef01234567"
fixture_dir="${repo_root}/tests/fixtures/release-assets/valid"
result_dir="${repo_root}/TestResults/release-assets-fixtures"
report="${result_dir}/release-assets-fixtures.md"
tmp_root="$(mktemp -d)"

cleanup() {
  rm -rf "$tmp_root"
}
trap cleanup EXIT

mkdir -p "$result_dir"

{
  echo "# Release asset fixture tests"
  echo
  echo "- version: ${version}"
  echo "- commit: ${commit}"
  echo
} >"$report"

log_line() {
  local line="$1"
  echo "$line"
  echo "- ${line}" >>"$report"
}

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
  cp "${fixture_dir}/bukit-${version}-linux-x64.tar.gz" "$work_dir/"
  cp "${fixture_dir}/bukit-${version}-osx-arm64.tar.gz" "$work_dir/"
  cp "${fixture_dir}/bukit-${version}-win-x64.zip" "$work_dir/"
  cp "${fixture_dir}/bukit-skills.zip" "$work_dir/"

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
  log_line "${case_name}: failed as expected"
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

replace_text() {
  local file="$1"
  local old="$2"
  local new="$3"

  python3 - "$file" "$old" "$new" <<'PY'
import sys
from pathlib import Path

path = Path(sys.argv[1])
old = sys.argv[2]
new = sys.argv[3]
text = path.read_text(encoding="utf-8")
if old not in text:
    raise SystemExit(f"missing text to replace: {old}")
path.write_text(text.replace(old, new, 1), encoding="utf-8")
PY
}

prepare_valid_fixture "${tmp_root}/valid"

valid_output="${tmp_root}/valid.out"
run_validator "${tmp_root}/valid" >"$valid_output"
assert_contains "$valid_output" "release asset checks passed"
assert_contains "$valid_output" "set_mode=exact-match"
assert_contains "$valid_output" "manifest_rids=linux-x64,osx-arm64,skills,win-x64"
log_line "valid fixture: passed"

direct_valid_output="${tmp_root}/direct-valid.out"
run_validator "${fixture_dir}" >"$direct_valid_output"
assert_contains "$direct_valid_output" "release asset checks passed"
log_line "tracked valid fixture: passed"

missing_manifest="$(copy_valid_case missing-manifest)"
rm "$missing_manifest/release-manifest.json"
expect_failure missing-manifest "required file missing"

missing_checksums_txt="$(copy_valid_case missing-checksums-txt)"
rm "$missing_checksums_txt/checksums.txt"
expect_failure missing-checksums-txt "required file missing"

missing_checksums_json="$(copy_valid_case missing-checksums-json)"
rm "$missing_checksums_json/checksums.json"
expect_failure missing-checksums-json "required file missing"

wrong_version="$(copy_valid_case wrong-version)"
replace_text "$wrong_version/checksums.txt" "# version=1.0.4" "# version=1.0.3"
expect_failure wrong-version "checksums.txt version 1.0.3 != expected ${version}"

wrong_commit="$(copy_valid_case wrong-commit)"
expect_failure wrong-commit "checksums.txt commit ${commit} != expected fedcba9876543210fedcba9876543210fedcba98" "fedcba9876543210fedcba9876543210fedcba98"

missing_platform_artifact="$(copy_valid_case missing-platform-artifact)"
rm "$missing_platform_artifact/bukit-${version}-linux-x64.tar.gz"
expect_failure missing-platform-artifact "release asset missing"

extra_platform_artifact="$(copy_valid_case extra-platform-artifact)"
cp "$extra_platform_artifact/bukit-${version}-linux-x64.tar.gz" "$extra_platform_artifact/bukit-${version}-freebsd-x64.tar.gz"
mutate_json "$extra_platform_artifact/checksums.json" "obj['files'].append({'path': 'bukit-1.0.4-freebsd-x64.tar.gz', 'hash': obj['files'][0]['hash'], 'size': obj['files'][0]['size']}); obj['fileCount'] = len(obj['files'])"
expect_failure extra-platform-artifact "checksums.json fileCount must match required artifact count"

duplicate_manifest_artifact="$(copy_valid_case duplicate-manifest-artifact)"
mutate_json "$duplicate_manifest_artifact/release-manifest.json" "obj['artifacts'][-1] = dict(obj['artifacts'][0])"
expect_failure duplicate-manifest-artifact "manifest contains duplicate artifact file names"

manifest_unsorted="$(copy_valid_case manifest-unsorted)"
mutate_json "$manifest_unsorted/release-manifest.json" "obj['artifacts'] = list(reversed(obj['artifacts']))"
expect_failure manifest-unsorted "manifest artifacts are not sorted by (rid, file)"

checksum_mismatch="$(copy_valid_case checksum-mismatch)"
python3 - "$checksum_mismatch/checksums.txt" <<'PY'
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
expect_failure checksum-mismatch "checksums.txt mismatch"

size_mismatch="$(copy_valid_case size-mismatch)"
mutate_json "$size_mismatch/checksums.json" "obj['files'][0]['size'] = obj['files'][0]['size'] + 1"
expect_failure size-mismatch "checksums.json size mismatch"

missing_rid="$(copy_valid_case missing-rid)"
mutate_json "$missing_rid/release-manifest.json" "next(item for item in obj['artifacts'] if item.get('rid') == 'skills')['rid'] = 'linux-x64'"
expect_failure missing-rid "manifest rid set mismatch: missing=skills"

unexpected_rid="$(copy_valid_case unexpected-rid)"
mutate_json "$unexpected_rid/release-manifest.json" "next(item for item in obj['artifacts'] if item.get('rid') == 'skills')['rid'] = 'freebsd-x64'"
expect_failure unexpected-rid "extra=freebsd-x64"

invalid_sha_prefix="$(copy_valid_case invalid-sha-prefix)"
mutate_json "$invalid_sha_prefix/release-manifest.json" "obj['artifacts'][0]['sha256'] = obj['artifacts'][0]['sha256'].replace('sha256:', 'sha512:', 1)"
expect_failure invalid-sha-prefix "manifest sha missing prefix"

bundle_hash_mismatch="$(copy_valid_case bundle-hash-mismatch)"
mutate_json "$bundle_hash_mismatch/checksums.json" "obj['bundleHash'] = 'sha256:' + ('0' * 64)"
mutate_json "$bundle_hash_mismatch/release-manifest.json" "obj['bundleHash'] = 'sha256:' + ('0' * 64)"
expect_failure bundle-hash-mismatch "checksums.json bundleHash mismatch"

checksum_txt_artifacts_mismatch="$(copy_valid_case checksum-txt-artifacts-mismatch)"
replace_text "$checksum_txt_artifacts_mismatch/checksums.txt" "# artifacts=4" "# artifacts=3"
expect_failure checksum-txt-artifacts-mismatch "checksums.txt #artifacts header does not match checksum line count"

checksum_txt_artifacts_not_integer="$(copy_valid_case checksum-txt-artifacts-not-integer)"
replace_text "$checksum_txt_artifacts_not_integer/checksums.txt" "# artifacts=4" "# artifacts=four"
expect_failure checksum-txt-artifacts-not-integer "checksums.txt #artifacts header must be an integer"

log_line "release asset fixture tests OK"

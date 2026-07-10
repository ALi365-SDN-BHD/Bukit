#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
validator="${repo_root}/scripts/checks/coverage/validate-output-root.py"
tmp_root="$(mktemp -d)"
temp_parent="${TMPDIR:-/tmp}"
system_tmp="$(python3 -c 'import tempfile; print(tempfile.gettempdir())')"
coverage_tmp="$(mktemp -d "${temp_parent%/}/bukit-coverage-self-test.XXXXXX")"
trap 'rm -rf "$tmp_root" "$coverage_tmp"' EXIT

expect_accept() {
  bash "$validator" "$1" "$repo_root" >/dev/null
  echo "coverage output path accepted: $1"
}

expect_reject() {
  local root="${2:-$repo_root}"
  if bash "$validator" "$1" "$root" >/dev/null 2>&1; then
    echo "ERROR: unsafe coverage output path accepted: $1" >&2
    exit 1
  fi
  echo "coverage output path rejected: $1"
}

mkdir -p "${coverage_tmp}/projects"
ln -s "${repo_root}/src" "${tmp_root}/source-link"
ln -s "$coverage_tmp" "${tmp_root}/coverage-link"
fake_repo="${tmp_root}/fake-repo"
mkdir -p "${fake_repo}/TestResults/coverage"
ln -s "$coverage_tmp" "${fake_repo}/TestResults/coverage/cross-root-link"
symlink_repo="${tmp_root}/symlink-repo"
mkdir -p "${symlink_repo}/source"
ln -s "${symlink_repo}/source" "${symlink_repo}/TestResults"

expect_accept "TestResults/coverage"
expect_accept "TestResults/coverage/projects"
expect_accept "$coverage_tmp"
expect_accept "${coverage_tmp}/projects"
expect_reject "."
expect_reject "src"
expect_reject "$repo_root"
expect_reject "$(dirname "$repo_root")"
expect_reject "$(cd /tmp && pwd -P)"
expect_reject "$(cd "$system_tmp" && pwd -P)"
expect_reject "$HOME"
expect_reject "${tmp_root}/source-link"
expect_reject "${tmp_root}/coverage-link"
expect_reject "${tmp_root}/unrelated-project-data"
expect_reject "TestResults/coverage/cross-root-link" "$fake_repo"
expect_reject "TestResults/coverage" "$symlink_repo"

echo "coverage output path self-test OK"

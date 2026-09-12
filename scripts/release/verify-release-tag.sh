#!/usr/bin/env bash
set -euo pipefail

[[ $# == 2 ]] || { echo "usage: verify-release-tag.sh <version> <commit>" >&2; exit 2; }
version="$1"
commit="$2"
[[ "$version" =~ ^[0-9]+[.][0-9]+[.][0-9]+([-][A-Za-z0-9._-]+)?$ && "$commit" =~ ^[0-9a-f]{40}$ ]] || exit 2
tag="refs/tags/v$version"
git check-ref-format "$tag" || exit 2
# Read only: an absent tag is created by the approved publication step.
refs="$(git ls-remote origin "$tag" "$tag^{}")"
base=""
peeled=""
while IFS=$'\t' read -r sha ref; do
  [[ -n "$sha" || -n "$ref" ]] || continue
  [[ "$sha" =~ ^[0-9a-f]{40}$ ]] || { echo "Malformed remote tag response" >&2; exit 1; }
  case "$ref" in
    "$tag") [[ -z "$base" ]] || exit 1; base="$sha" ;;
    "$tag^{}") [[ -z "$peeled" ]] || exit 1; peeled="$sha" ;;
    *) echo "Unexpected remote ref" >&2; exit 1 ;;
  esac
done <<< "$refs"
[[ -n "$base" || -z "$peeled" ]] || exit 1
[[ -z "$base" || "${peeled:-$base}" == "$commit" ]] || {
  echo "Release tag v$version does not match build commit $commit" >&2
  exit 1
}

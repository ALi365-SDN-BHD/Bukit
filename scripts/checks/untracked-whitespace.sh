#!/usr/bin/env bash
set -euo pipefail

[[ $# -eq 1 ]] || { echo "usage: bash scripts/checks/untracked-whitespace.sh PATH" >&2; exit 2; }

stderr_file="$(mktemp "${TMPDIR:-/tmp}/bukit-untracked-whitespace.XXXXXX")"
trap 'rm -f "$stderr_file"' EXIT

rc=0
out="$(git diff --check --no-index -- /dev/null "$1" 2>"$stderr_file")" || rc=$?
err="$(cat "$stderr_file")"

if [[ -n "$err" ]]; then
  printf '%s\n' "$err" >&2
  [[ "$rc" -ne 0 ]] && exit "$rc"
  exit 2
fi
if [[ -n "$out" ]]; then
  printf '%s\n' "$out" >&2
  exit 1
fi
case "$rc" in
  0|1) exit 0 ;;
  *) exit "$rc" ;;
esac

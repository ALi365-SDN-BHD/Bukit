#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

usage() {
  echo "usage: bash scripts/checks/post-change-targeted.sh [--dry-run] [--configuration Release] [--base REF] [--] [path...]"
}

dry_run=0; configuration="Release"; base_ref="HEAD"; paths=(); paths_explicit=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --dry-run) dry_run=1; shift ;;
    --configuration)
      [[ $# -ge 2 ]] || { echo "--configuration requires a value" >&2; exit 2; }
      configuration="$2"; shift 2 ;;
    --base)
      [[ $# -ge 2 ]] || { echo "--base requires a value" >&2; exit 2; }
      base_ref="$2"; shift 2 ;;
    --help|-h) usage; exit 0 ;;
    --)
      paths_explicit=1
      shift
      while [[ $# -gt 0 ]]; do [[ -n "$1" ]] && paths+=("$1"); shift; done ;;
    -*) echo "unknown option: $1" >&2; usage >&2; exit 2 ;;
    *) paths_explicit=1; [[ -n "$1" ]] && paths+=("$1"); shift ;;
  esac
done

if [[ "$paths_explicit" == "0" && ${#paths[@]} -eq 0 ]]; then
  discovered_paths="$(bash scripts/checks/post-change-targeted-paths.sh "$base_ref")"
  if [[ -n "$discovered_paths" ]]; then
    while IFS= read -r path; do [[ -n "$path" ]] && paths+=("$path"); done <<< "$discovered_paths"
  fi
fi

if [[ ${#paths[@]} -eq 0 ]]; then
  echo "No changed paths detected." >&2
  exit 0
fi

focused_args=(--configuration "$configuration" --base "$base_ref")
[[ "$dry_run" == "1" ]] && focused_args=(--dry-run "${focused_args[@]}")
focused_args+=(--)

if [[ "$dry_run" == "1" ]]; then
  bash scripts/checks/post-change-focused.sh "${focused_args[@]}" "${paths[@]}"
  printf '+ bash scripts/gates/ci-fast.sh %q\n' "$configuration"
else
  run_step "focused affected checks" bash scripts/checks/post-change-focused.sh "${focused_args[@]}" "${paths[@]}"
  run_step "fast contract gate" bash scripts/gates/ci-fast.sh "$configuration"
fi

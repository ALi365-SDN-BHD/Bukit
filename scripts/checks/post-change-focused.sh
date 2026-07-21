#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

usage() {
  echo "usage: bash scripts/checks/post-change-focused.sh [--dry-run] [--configuration Release] [--base REF] [--] [path...]"
}

dry_run=0; configuration="Release"; base_ref="HEAD"; paths=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --dry-run)
      dry_run=1; shift ;;
    --configuration)
      [[ $# -ge 2 ]] || { echo "--configuration requires a value" >&2; exit 2; }
      configuration="$2"; shift 2 ;;
    --base)
      [[ $# -ge 2 ]] || { echo "--base requires a value" >&2; exit 2; }
      base_ref="$2"; shift 2 ;;
    --help|-h)
      usage; exit 0 ;;
    --)
      shift
      while [[ $# -gt 0 ]]; do paths+=("$1"); shift; done ;;
    -*)
      echo "unknown option: $1" >&2; usage >&2; exit 2 ;;
    *)
      paths+=("$1"); shift ;;
  esac
done

changed_paths=(); untracked_paths=(); syntax_paths=(); test_projects=()
unmapped_sources=(); blocked_paths=()

contains_item() {
  local needle="$1" item
  shift
  for item in "$@"; do [[ "$item" == "$needle" ]] && return 0; done
  return 1
}

add_changed_path() {
  local path="${1#./}"
  [[ -n "$path" ]] || return 0
  if [[ ${#changed_paths[@]} -eq 0 ]] || ! contains_item "$path" "${changed_paths[@]}"; then
    changed_paths+=("$path")
    [[ -e "$path" ]] && ! git ls-files --error-unmatch -- "$path" >/dev/null 2>&1 && untracked_paths+=("$path")
  fi
  return 0
}

add_syntax_path() {
  local path="$1"
  if [[ ${#syntax_paths[@]} -eq 0 ]] || ! contains_item "$path" "${syntax_paths[@]}"; then
    syntax_paths+=("$path")
  fi
}

add_test_project() {
  local project="$1" origin="${2:-$1}"
  if [[ ! -f "$project" ]]; then
    unmapped_sources+=("$origin -> $project")
    return
  fi
  if [[ ${#test_projects[@]} -eq 0 ]] || ! contains_item "$project" "${test_projects[@]}"; then
    test_projects+=("$project")
  fi
}

add_projects_for_path() {
  local path="$1" projects project
  projects="$(bash scripts/checks/post-change-targeted-projects.sh "$path")" || return 1
  while IFS= read -r project; do
    [[ -n "$project" ]] && add_test_project "$project" "$path"
  done <<< "$projects"
}

is_blocked_path() {
  case "$1" in
    scripts/gates/ci-full.sh|scripts/gates/release.sh|scripts/release-gate.sh|\
    scripts/test-all.sh|scripts/smoke-all.sh|.github/workflows/*release*.yml|\
    .github/workflows/*release*.yaml)
      return 0 ;;
    *)
      return 1 ;;
  esac
}

if [[ ${#paths[@]} -eq 0 ]]; then
  discovered_paths="$(bash scripts/checks/post-change-targeted-paths.sh "$base_ref")"
  if [[ -n "$discovered_paths" ]]; then
    while IFS= read -r path; do add_changed_path "$path"; done <<< "$discovered_paths"
  fi
else
  for path in "${paths[@]}"; do add_changed_path "$path"; done
fi

if [[ ${#changed_paths[@]} -eq 0 ]]; then
  echo "No changed paths detected." >&2
  exit 0
fi

for path in "${changed_paths[@]}"; do
  if is_blocked_path "$path"; then
    blocked_paths+=("$path")
    continue
  fi
  [[ "$path" == *.sh && -f "$path" ]] && add_syntax_path "$path"
  if [[ "$path" == src/* ]]; then
    add_projects_for_path "$path" || unmapped_sources+=("$path")
  elif [[ "$path" == tests/* ]]; then
    add_projects_for_path "$path" || true
  fi
done

if [[ ${#blocked_paths[@]} -gt 0 ]]; then
  echo "Refusing focused verification for blocked paths:" >&2
  printf '  %s\n' "${blocked_paths[@]}" >&2
  echo "Use an explicit user-requested full or release proof path instead." >&2
  exit 1
fi

print_command() {
  local arg
  printf '+'
  for arg in "$@"; do printf ' %q' "$arg"; done
  printf '\n'
}

reject_forbidden_command() {
  local arg
  if [[ "${1:-}" == "bash" ]]; then
    for arg in "$@"; do
      case "$arg" in
        scripts/gates/ci-full.sh|scripts/gates/release.sh|scripts/release-gate.sh|scripts/test-all.sh|scripts/smoke-all.sh)
          echo "Refusing forbidden full or release command:" >&2
          print_command "$@" >&2
          exit 1 ;;
      esac
    done
  fi
  if [[ "${1:-}" == "dotnet" && "${2:-}" == "test" ]]; then
    for arg in "$@"; do
      case "$arg" in
        bukit-test.slnx|*.slnx)
          echo "Refusing forbidden whole-solution test command:" >&2
          print_command "$@" >&2
          exit 1 ;;
      esac
    done
  fi
}

run_or_print() {
  local label="$1"
  shift
  reject_forbidden_command "$@"
  if [[ "$dry_run" == "1" ]]; then
    print_command "$@"
  else
    run_step "$label" "$@"
  fi
}

run_or_print "diff whitespace" git diff --check "$base_ref" -- "${changed_paths[@]}"
if [[ ${#untracked_paths[@]} -gt 0 ]]; then
  for path in "${untracked_paths[@]}"; do
    run_or_print "untracked whitespace: $path" bash scripts/checks/untracked-whitespace.sh "$path"
  done
fi
if [[ ${#syntax_paths[@]} -gt 0 ]]; then
  for path in "${syntax_paths[@]}"; do
    run_or_print "shell syntax: $path" bash -n "$path"
  done
fi
if [[ "$dry_run" == "1" ]]; then
  bash scripts/checks/post-change-focused-owner-checks.sh --dry-run -- "${changed_paths[@]}"
else
  run_step "focused owner checks" bash scripts/checks/post-change-focused-owner-checks.sh -- "${changed_paths[@]}"
fi

if [[ ${#unmapped_sources[@]} -gt 0 ]]; then
  echo "Cannot map these runtime source paths to focused test projects:" >&2
  printf '  %s\n' "${unmapped_sources[@]}" >&2
  echo "Add a mapping or run an explicit focused test command; no full-gate fallback is allowed." >&2
  exit 1
fi

if [[ ${#test_projects[@]} -gt 0 ]]; then
  for project in "${test_projects[@]}"; do
    run_or_print "$(basename "$(dirname "$project")")" dotnet test "$project" -c "$configuration"
  done
fi

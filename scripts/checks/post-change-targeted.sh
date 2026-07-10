#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

usage() {
  echo "usage: bash scripts/checks/post-change-targeted.sh [--dry-run] [--configuration Release] [--base REF] [--] [path...]"
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

changed_paths=(); syntax_paths=(); test_projects=(); unmapped_sources=(); blocked_paths=()

contains_item() {
  local needle="$1" item
  shift
  for item in "$@"; do [[ "$item" == "$needle" ]] && return 0; done
  return 1
}

add_changed_path() {
  local path="${1#./}"
  [[ -n "$path" ]] || return
  if [[ ${#changed_paths[@]} -eq 0 ]] || ! contains_item "$path" "${changed_paths[@]}"; then
    changed_paths+=("$path")
  fi
}

add_syntax_path() {
  local path="$1"
  [[ -f "$path" ]] || return
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

project_for_module() {
  local module="$1"
  case "$module" in
    Bukit.Cli.Shared) module="Bukit.Cli" ;;
    Bukit.Plugin.WechatSync|Bukit.WechatSyncing) module="Bukit.Plugin.WechatSync" ;;
  esac
  printf 'tests/%s.Tests/%s.Tests.csproj\n' "$module" "$module"
}

source_project_for_path() {
  local path="$1" module
  case "$path" in
    src/Bukit-Core/*/*) module="${path#src/Bukit-Core/}" ;;
    src/Bukit-Labs/*/*) module="${path#src/Bukit-Labs/}" ;;
    src/Bukit-Plugins/*/*) module="${path#src/Bukit-Plugins/}" ;;
    *) return 1 ;;
  esac
  module="${module%%/*}"
  project_for_module "$module"
}

test_project_for_path() {
  local path="$1" test_dir
  case "$path" in
    tests/*.Tests/*)
      test_dir="${path#tests/}"; test_dir="${test_dir%%/*}"
      printf 'tests/%s/%s.csproj\n' "$test_dir" "$test_dir" ;;
    *) return 1 ;;
  esac
}

is_blocked_path() {
  case "$1" in
scripts/gates/ci-full.sh|scripts/gates/release.sh|scripts/release-gate.sh|\
scripts/test-all.sh|scripts/smoke-all.sh)
      return 0 ;;
    *)
      return 1 ;;
  esac
}

if [[ ${#paths[@]} -eq 0 ]]; then
  while IFS= read -r path; do add_changed_path "$path"; done < <(git diff --name-only "$base_ref" --)
  while IFS= read -r path; do add_changed_path "$path"; done < <(git ls-files --others --exclude-standard)
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
  [[ "$path" == *.sh ]] && add_syntax_path "$path"
  if [[ "$path" == src/* ]]; then
    if project="$(source_project_for_path "$path")"; then
      add_test_project "$project" "$path"
    else
      unmapped_sources+=("$path")
    fi
  elif [[ "$path" == tests/* ]] && project="$(test_project_for_path "$path")"; then
    add_test_project "$project" "$path"
  fi
done

if [[ ${#blocked_paths[@]} -gt 0 ]]; then
  echo "Refusing targeted verification for blocked paths:" >&2
  printf '  %s\n' "${blocked_paths[@]}" >&2
  echo "Use an explicit user-requested full or release proof path instead." >&2; exit 1
fi

if [[ ${#unmapped_sources[@]} -gt 0 ]]; then
  echo "Cannot map these runtime source paths to targeted test projects:" >&2
  printf '  %s\n' "${unmapped_sources[@]}" >&2
  echo "Add a mapping or run an explicit targeted test command; no full-gate fallback is allowed." >&2; exit 1
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
          echo "Refusing forbidden full or release command:" >&2; print_command "$@" >&2; exit 1 ;;
      esac
    done
  fi
  if [[ "${1:-}" == "dotnet" && "${2:-}" == "test" ]]; then
    for arg in "$@"; do
      case "$arg" in bukit-test.slnx|*.slnx)
        echo "Refusing forbidden whole-solution test command:" >&2; print_command "$@" >&2; exit 1 ;;
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
if [[ ${#syntax_paths[@]} -gt 0 ]]; then
  for path in "${syntax_paths[@]}"; do run_or_print "shell syntax: $path" bash -n "$path"; done
fi
run_or_print "fast contract gate" bash scripts/gates/ci-fast.sh "$configuration"
if [[ ${#test_projects[@]} -gt 0 ]]; then
  for project in "${test_projects[@]}"; do run_or_print "$(basename "$(dirname "$project")")" dotnet test "$project" -c "$configuration"; done
fi

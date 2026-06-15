#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

is_truthy_value() {
  case "${1:-}" in
    1|true|TRUE|True|yes|YES|Yes|on|ON|On)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

repo="${1:-${GITHUB_REPOSITORY:-}}"
sha="${2:-${GITHUB_SHA:-}}"
workflow="${3:-ci.yml}"
output="${4:-TestResults/release-gate/ci-workflow-evidence.json}"
require_success="${5:-1}"
report="${6:-}"
required_branches="${7:-${RELEASE_GATE_REQUIRED_BRANCHES:-}}"

if [ -z "${repo}" ] || [ -z "${sha}" ]; then
  echo "Usage: $0 <repo> <sha> [workflow-file] [output-json] [require-success] [report-md] [required-branches]" >&2
  echo "Example: $0 ALi365-SDN-BHD/Bukit <commit-sha> ci.yml TestResults/release-gate/ci-workflow-evidence.json 1 TestResults/release-gate/rc-gate-evidence.md main,master" >&2
  echo "Options: [require-success: 1|0] default 1" >&2
  echo "        [report-md]: optional markdown report output path" >&2
  echo "        [required-branches]: comma/space-separated branch names (defaults to \$RELEASE_GATE_REQUIRED_BRANCHES)" >&2
  exit 2
fi

if ! is_truthy_value "${GITHUB_ACTIONS:-0}"; then
  echo "Skipping workflow evidence check outside GitHub Actions context."
  exit 0
fi

if [ "${SKIP_WORKFLOW_EVIDENCE_CHECK:-0}" = "1" ]; then
  echo "Skipping workflow evidence check (SKIP_WORKFLOW_EVIDENCE_CHECK=1)."
  exit 0
fi

mkdir -p "$(dirname "$output")"
if [ -n "${report}" ]; then
  mkdir -p "$(dirname "$report")"
fi

headers=(
  -H "Accept: application/vnd.github+json"
  -H "User-Agent: bukit-ci-evidence-check"
  -H "X-GitHub-Api-Version: 2022-11-28"
)

if [ -n "${GITHUB_TOKEN:-}" ]; then
  headers+=(-H "Authorization: Bearer ${GITHUB_TOKEN}")
elif [ -n "${GH_TOKEN:-}" ]; then
  headers+=(-H "Authorization: Bearer ${GH_TOKEN}")
fi

tmp_file="$(mktemp)"
trap 'rm -f "$tmp_file"' EXIT

query_url="https://api.github.com/repos/${repo}/actions/workflows/${workflow}/runs?head_sha=${sha}&per_page=100"
if is_truthy_value "$require_success"; then
  query_url="https://api.github.com/repos/${repo}/actions/workflows/${workflow}/runs?head_sha=${sha}&status=completed&per_page=100"
fi

curl -fsSL "${headers[@]}" "$query_url" > "$tmp_file"

python3 "$repo_root/scripts/checks/ci-workflow-evidence-evaluate.py" \
  "$tmp_file" \
  "$repo" \
  "$sha" \
  "$workflow" \
  "$require_success" \
  "$required_branches" \
  "$output" \
  "$report" \
  "$query_url"

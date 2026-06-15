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
tmp_curl_error="$(mktemp)"
trap 'rm -f "$tmp_file" "$tmp_curl_error"' EXIT

query_url="https://api.github.com/repos/${repo}/actions/workflows/${workflow}/runs?head_sha=${sha}&per_page=100"
if is_truthy_value "$require_success"; then
  query_url="https://api.github.com/repos/${repo}/actions/workflows/${workflow}/runs?head_sha=${sha}&status=completed&per_page=100"
fi

http_code="$(curl -sS -w "%{http_code}" -o "$tmp_file" "${headers[@]}" "$query_url" 2>"$tmp_curl_error" || true)"
if [ -z "${http_code}" ]; then
  http_code="000"
fi

if [ "${http_code}" != "200" ]; then
  echo "workflow evidence check failed: GitHub API request returned HTTP ${http_code} for ${query_url}" >&2
skip_auth_failure_local="${SKIP_AUTH_FAILURE_IN_LOCAL:-1}"
if ([ "${http_code}" = "401" ] || [ "${http_code}" = "403" ]) && \
   [ -z "${GITHUB_TOKEN:-}${GH_TOKEN:-}" ] && \
   [ -z "${RUNNER_OS:-}" ] && \
   is_truthy_value "${skip_auth_failure_local}"; then
    echo "Skipping workflow evidence check: GitHub API authentication failed in local environment (no token)." >&2
    echo "Set GITHUB_TOKEN/GH_TOKEN to run this check with full validation." >&2
    exit 0
  fi
  if [ "${http_code}" = "401" ] || [ "${http_code}" = "403" ] && [ -z "${GITHUB_TOKEN:-}${GH_TOKEN:-}" ]; then
    echo "Hint: this check usually requires an authenticated GitHub token with `actions: read` capability." >&2
    echo "Set `GITHUB_TOKEN` (or `GH_TOKEN`) and rerun, or ensure runner permissions include `actions: read`." >&2
  fi
  if [ -n "${GITHUB_TOKEN:-}${GH_TOKEN:-}" ]; then
    echo "Authorization header: present" >&2
  else
    echo "Authorization header: missing (unauthenticated request)" >&2
  fi
  if [ -s "$tmp_curl_error" ]; then
    echo "curl error output:" >&2
    sed -n '1,5p' "$tmp_curl_error" >&2
  fi
  if [ -s "$tmp_file" ]; then
    echo "API response body:" >&2
    sed -n '1,20p' "$tmp_file" >&2
  fi
  exit 22
fi

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

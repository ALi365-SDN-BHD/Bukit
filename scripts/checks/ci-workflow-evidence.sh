#!/usr/bin/env bash

set -euo pipefail

repo="${1:-${GITHUB_REPOSITORY:-}}"
sha="${2:-${GITHUB_SHA:-}}"
workflow="${3:-ci.yml}"
output="${4:-TestResults/release-gate/ci-workflow-evidence.json}"
require_success="${5:-1}"
report="${6:-}"

if [ -z "${repo}" ] || [ -z "${sha}" ]; then
  echo "Usage: $0 <repo> <sha> [workflow-file] [output-json] [require-success] [report-md]" >&2
  echo "Example: $0 ALi365-SDN-BHD/Bukit <commit-sha> ci.yml TestResults/release-gate/ci-workflow-evidence.json 1 TestResults/release-gate/rc-gate-evidence.md" >&2
  echo "Options: [require-success: 1|0] default 1" >&2
  echo "        [report-md]: optional markdown report output path" >&2
  exit 2
fi

if [ "${GITHUB_ACTIONS:-0}" != "1" ]; then
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
if [ "$require_success" = "1" ] || [ "${require_success,,}" = "true" ] || [ "${require_success,,}" = "yes" ] || [ "${require_success,,}" = "on" ]; then
  query_url="https://api.github.com/repos/${repo}/actions/workflows/${workflow}/runs?head_sha=${sha}&status=completed&per_page=100"
fi

curl -fsSL "${headers[@]}" "$query_url" > "$tmp_file"

python3 - "$tmp_file" "$output" "$repo" "$sha" "$workflow" "$require_success" "$query_url" "$report" <<'PY'
import json
import sys

input_path, output_path, repo, sha, workflow, require_success, query_url, report_path = sys.argv[1:9]

with open(input_path, "r", encoding="utf-8") as handle:
    payload = json.load(handle)

runs = payload.get("workflow_runs", [])
successful_runs = [
    run for run in runs
    if run.get("status") == "completed" and run.get("conclusion") == "success"
]

latest = [
    {
        "id": run.get("id"),
        "name": run.get("name"),
        "status": run.get("status"),
        "conclusion": run.get("conclusion"),
        "html_url": run.get("html_url"),
        "run_started_at": run.get("run_started_at"),
        "updated_at": run.get("updated_at"),
    }
    for run in runs[:10]
]

require_success_value = str(require_success).lower() not in {"0", "false", "no", "off"}

report = {
    "repo": repo,
    "sha": sha,
    "workflow_file": workflow,
    "query_url": query_url,
    "require_success": bool(require_success_value),
    "total_runs": len(runs),
    "successful_runs": len(successful_runs),
    "runs": latest,
}

with open(output_path, "w", encoding="utf-8") as handle:
    json.dump(report, handle, ensure_ascii=False, indent=2, sort_keys=True)

if require_success_value and not successful_runs:
    latest_runs = ", ".join(str(run.get("id")) for run in runs[:5]) or "(none)"
    print(
        "workflow evidence check failed: commit has no completed successful workflow runs.",
        file=sys.stderr,
    )
    print(f"repo: {repo}", file=sys.stderr)
    print(f"sha: {sha}", file=sys.stderr)
    print(f"workflow: {workflow}", file=sys.stderr)
    print(f"latest run ids: {latest_runs}", file=sys.stderr)
    print("Evidence file has been written to:", output_path, file=sys.stderr)
    sys.exit(1)

if (not require_success_value) and not runs:
    latest_runs = ", ".join(str(run.get("id")) for run in runs[:5]) or "(none)"
    print(
        "workflow evidence check failed: commit has no workflow runs.",
        file=sys.stderr,
    )
    print(f"repo: {repo}", file=sys.stderr)
    print(f"sha: {sha}", file=sys.stderr)
    print(f"workflow: {workflow}", file=sys.stderr)
    print(f"latest run ids: {latest_runs}", file=sys.stderr)
    print("Evidence file has been written to:", output_path, file=sys.stderr)
    sys.exit(1)

pass_msg = "passed"
if successful_runs:
    print(
        "workflow evidence check passed: "
        f"{len(successful_runs)} completed successful run(s) for {repo}@{sha[:7]}"
    )
else:
    print(f"workflow evidence check passed: 0 completed successful run(s) for {repo}@{sha[:7]}")

if successful_runs:
    print("latest successful run:")
    print(f"  - {successful_runs[0].get('html_url')}")
elif runs:
    print("latest run:")
    print(f"  - {runs[0].get('html_url')}")
else:
    print("latest run: (none)")

print(f"Evidence file written to: {output_path}")

if report_path:
    rc_pass = bool(successful_runs) if require_success_value else bool(runs)
    status = "PASS" if rc_pass else "BLOCKED"
    mode = "require completed-success workflow run" if require_success_value else "require any workflow run"
    reason = (
        "Found completed successful run(s)."
        if rc_pass
        else "No completed successful run(s)."
        if require_success_value
        else "No workflow run found."
    )

    lines = [
        "# Bukit RC Gate Evidence Report",
        "",
        "| Field | Value |",
        "| --- | --- |",
        f"| Repo | `{repo}` |",
        f"| Workflow | `{workflow}` |",
        f"| Commit | `{sha}` |",
        f"| Mode | `{mode}` |",
        f"| Decision | **{status}** |",
        f"| Reason | {reason} |",
        f"| Total Runs | `{len(runs)}` |",
        f"| Successful Runs | `{len(successful_runs)}` |",
        "",
        "## Recent Runs",
        "",
        "| id | name | status | conclusion | url |",
        "| -- | ---- | ------ | ---------- | --- |",
    ]

    for run in latest:
        lines.append(
            f"| {run.get('id')} | {run.get('name')} | {run.get('status')} | {run.get('conclusion')} | {run.get('html_url')} |"
        )

    lines.extend(
        [
            "",
            f"Evidence JSON: `{output_path}`",
            "",
            f"Query: `{query_url}`",
        ]
    )

    with open(report_path, "w", encoding="utf-8") as handle:
        handle.write("\n".join(lines) + "\n")

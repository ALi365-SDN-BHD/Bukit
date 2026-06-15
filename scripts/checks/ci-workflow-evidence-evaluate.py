#!/usr/bin/env python3
import json
import sys
from pathlib import Path


def usage() -> None:
    print(
        "Usage: ci-workflow-evidence-evaluate.py <workflow-runs-json> <repo> <sha> "
        "<workflow-file> <require-success> <required-branches> <output-json> <output-md> [query-url]",
        file=sys.stderr,
    )


def is_truthy(value: str) -> bool:
    return str(value).lower() not in {"0", "false", "no", "off"}


def parse_required_branches(value: str) -> set[str]:
    return {
        branch.strip()
        for branch in (value.replace(" ", ",") if value else "").split(",")
        if branch.strip()
    }


def latest_run_summary(run: dict) -> dict:
    return {
        "id": run.get("id"),
        "name": run.get("name"),
        "status": run.get("status"),
        "conclusion": run.get("conclusion"),
        "head_branch": run.get("head_branch"),
        "html_url": run.get("html_url"),
        "run_started_at": run.get("run_started_at"),
        "updated_at": run.get("updated_at"),
    }


def write_markdown_report(
    report_path: str,
    output_path: str,
    repo: str,
    sha: str,
    workflow: str,
    query_url: str,
    require_success_value: bool,
    required_branches_sorted: list[str],
    runs: list[dict],
    successful_runs: list[dict],
    latest: list[dict],
) -> None:
    required_branch_text = ", ".join(required_branches_sorted) if required_branches_sorted else "any branch"
    rc_pass = bool(successful_runs) if require_success_value else bool(runs)
    status = "PASS" if rc_pass else "BLOCKED"
    mode = (
        f"require completed-success workflow run on branch(es): {required_branch_text}"
        if require_success_value and required_branches_sorted
        else "require completed-success workflow run"
        if require_success_value
        else "require any workflow run"
    )
    reason = (
        "Found completed successful run(s)."
        if rc_pass
        else f"No completed successful run(s) on required branch(es): {required_branch_text}."
        if require_success_value and required_branches_sorted
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
        "| id | name | status | conclusion | branch | url |",
        "| -- | ---- | ------ | ---------- | ------ | --- |",
    ]

    for run in latest:
        lines.append(
            f"| {run.get('id')} | {run.get('name')} | {run.get('status')} | {run.get('conclusion')} | {run.get('head_branch') or '-'} | {run.get('html_url')} |"
        )

    lines.extend(
        [
            "",
            f"Evidence JSON: `{output_path}`",
            "",
            f"Query: `{query_url}`",
        ]
    )

    Path(report_path).write_text("\n".join(lines) + "\n", encoding="utf-8")


def main(argv: list[str]) -> int:
    if len(argv) not in {9, 10}:
        usage()
        return 2

    input_path, repo, sha, workflow, require_success, required_branches, output_path, report_path = argv[1:9]
    query_url = argv[9] if len(argv) == 10 else ""

    payload = json.loads(Path(input_path).read_text(encoding="utf-8"))
    runs = payload.get("workflow_runs", [])
    if not isinstance(runs, list):
        raise SystemExit("ERROR: workflow_runs must be an array")

    required_branch_set = parse_required_branches(required_branches)
    require_success_value = is_truthy(require_success)

    def is_required_branch_run(run: dict) -> bool:
        if not required_branch_set:
            return True
        return run.get("head_branch") in required_branch_set

    successful_runs = [
        run
        for run in runs
        if isinstance(run, dict)
        and run.get("status") == "completed"
        and run.get("conclusion") == "success"
        and is_required_branch_run(run)
    ]

    latest = [latest_run_summary(run) for run in runs[:10] if isinstance(run, dict)]
    required_branches_sorted = sorted(required_branch_set)
    required_branch_text = ", ".join(required_branches_sorted) if required_branches_sorted else "any branch"

    report = {
        "repo": repo,
        "sha": sha,
        "workflow_file": workflow,
        "query_url": query_url,
        "require_success": bool(require_success_value),
        "required_branches": required_branches_sorted,
        "total_runs": len(runs),
        "successful_runs": len(successful_runs),
        "runs": latest,
    }

    Path(output_path).parent.mkdir(parents=True, exist_ok=True)
    Path(output_path).write_text(json.dumps(report, ensure_ascii=False, indent=2, sort_keys=True) + "\n", encoding="utf-8")

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
        first_run = runs[0] if isinstance(runs[0], dict) else {}
        print("latest run:")
        print(f"  - {first_run.get('html_url')}")
    else:
        print("latest run: (none)")

    print(f"Evidence file written to: {output_path}")

    if report_path:
        Path(report_path).parent.mkdir(parents=True, exist_ok=True)
        write_markdown_report(
            report_path,
            output_path,
            repo,
            sha,
            workflow,
            query_url,
            require_success_value,
            required_branches_sorted,
            runs,
            successful_runs,
            latest,
        )

    if require_success_value and not successful_runs:
        latest_runs = ", ".join(str(run.get("id")) for run in runs[:5] if isinstance(run, dict)) or "(none)"
        print(
            f"workflow evidence check failed: commit has no completed successful workflow runs on required branch(es): {required_branch_text}.",
            file=sys.stderr,
        )
        print(f"repo: {repo}", file=sys.stderr)
        print(f"sha: {sha}", file=sys.stderr)
        print(f"workflow: {workflow}", file=sys.stderr)
        print(f"required branches: {required_branch_text}", file=sys.stderr)
        print(f"latest run ids: {latest_runs}", file=sys.stderr)
        print("Evidence file has been written to:", output_path, file=sys.stderr)
        if report_path:
            print("Markdown report has been written to:", report_path, file=sys.stderr)
        return 1

    if not require_success_value and not runs:
        latest_runs = ", ".join(str(run.get("id")) for run in runs[:5] if isinstance(run, dict)) or "(none)"
        print("workflow evidence check failed: commit has no workflow runs.", file=sys.stderr)
        print(f"repo: {repo}", file=sys.stderr)
        print(f"sha: {sha}", file=sys.stderr)
        print(f"workflow: {workflow}", file=sys.stderr)
        print(f"latest run ids: {latest_runs}", file=sys.stderr)
        print("Evidence file has been written to:", output_path, file=sys.stderr)
        if report_path:
            print("Markdown report has been written to:", report_path, file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))

# Priority 3: delta-only final review scope.
evidence_a="$scratch/evidence-a.json"
evidence_b="$scratch/evidence-b.json"
findings="$scratch/findings.json"
printf '%s\n' \
  '{' \
  '  "cacheStatus": "hit",' \
  '  "closureFiles": ["src/A.cs", "src/Shared.cs"],' \
  '  "publicContractFiles": ["src/A.cs"],' \
  '  "schemaVersion": 1,' \
  '  "taskId": "task-a"' \
  '}' >"$evidence_a"
printf '%s\n' \
  '{' \
  '  "cacheStatus": "miss",' \
  '  "closureFiles": ["src/B.cs", "src/Shared.cs"],' \
  '  "publicContractFiles": [],' \
  '  "schemaVersion": 1,' \
  '  "taskId": "task-b"' \
  '}' >"$evidence_b"
printf '%s\n' \
  '{' \
  '  "findings": [' \
  '    {"files": ["src/B.cs"], "id": "IMP-1", "severity": "Important", "status": "open"},' \
  '    {"files": ["src/Minor.cs"], "id": "MIN-1", "severity": "Minor", "status": "open"}' \
  '  ],' \
  '  "schemaVersion": 1' \
  '}' >"$findings"

expect_exit 0 "${tool[@]}" review-scope \
  --evidence "$evidence_a" \
  --evidence "$evidence_b" \
  --findings "$findings" \
  --changed src/A.cs \
  --changed src/B.cs \
  --changed src/Shared.cs \
  --changed src/Uncovered.cs
review_output="$command_output"

python3 - "$review_output" <<'PY'
import json
import sys

result = json.loads(sys.argv[1])
if result["reusableEvidence"] != ["task-a"]:
    raise SystemExit(f"unexpected reusable evidence: {result['reusableEvidence']}")
if result["invalidatedEvidence"] != ["task-b"]:
    raise SystemExit(f"unexpected invalidated evidence: {result['invalidatedEvidence']}")
if result["crossTaskIntersections"] != [
    {"file": "src/Shared.cs", "tasks": ["task-a", "task-b"]}
]:
    raise SystemExit(f"unexpected intersections: {result['crossTaskIntersections']}")
if result["uncoveredChangedFiles"] != [
    "src/B.cs",
    "src/Uncovered.cs",
]:
    raise SystemExit(f"unexpected uncovered files: {result['uncoveredChangedFiles']}")
if result["publicContractFocus"] != ["src/A.cs"]:
    raise SystemExit(f"unexpected contract focus: {result['publicContractFocus']}")
if result["openBlockingFindings"] != [
    {
        "files": ["src/B.cs"],
        "id": "IMP-1",
        "severity": "Important",
        "status": "open",
    }
]:
    raise SystemExit(f"unexpected blocking findings: {result['openBlockingFindings']}")
if "src/Minor.cs" in result["reviewFiles"]:
    raise SystemExit("Minor finding unexpectedly expanded final review scope")
expected_review_files = ["src/A.cs", "src/B.cs", "src/Shared.cs", "src/Uncovered.cs"]
if result["reviewFiles"] != expected_review_files:
    raise SystemExit(f"unexpected review files: {result['reviewFiles']}")
PY


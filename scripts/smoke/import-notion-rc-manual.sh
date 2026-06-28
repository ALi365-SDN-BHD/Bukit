#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "usage: import-notion-rc-manual.sh <demo-dir> <theme-name>" >&2
  exit 2
fi

demo_dir="$1"
theme_name="$2"
bukit_bin="${BUKIT_BIN:-bukit}"
project_root="$(pwd -P)"
seed_dir="$project_root/sites/$theme_name/notion-seed"
map_path="$seed_dir/notion-database-map.yaml"
acceptance_map="$project_root/.bukit/tmp/notion/rc-manual-database-map.yaml"
report_dir="$project_root/.bukit/reports/plugin-output/notion"
dry_run_report="$report_dir/rc-manual-dry-run.json"
live_report="$report_dir/rc-manual-live.json"
summary_json="$report_dir/rc-acceptance-summary.json"
summary_md="$report_dir/rc-acceptance-summary.md"

if [ ! -d "$demo_dir" ]; then
  echo "ERROR: demo directory not found: $demo_dir" >&2
  exit 2
fi

if [ -z "${NOTION_TOKEN:-}" ]; then
  echo "ERROR: NOTION_TOKEN is required for live RC acceptance." >&2
  exit 2
fi

if [ -z "${NOTION_DATA_SOURCE_ID:-}" ]; then
  echo "ERROR: NOTION_DATA_SOURCE_ID is required for live RC acceptance." >&2
  exit 2
fi

if [ "${BUKIT_NOTION_RC_CONFIRM:-}" != "YES" ]; then
  echo "ERROR: set BUKIT_NOTION_RC_CONFIRM=YES to allow create-mode sandbox writes." >&2
  exit 2
fi

if ! command -v "$bukit_bin" >/dev/null 2>&1; then
  echo "ERROR: Bukit executable not found: $bukit_bin" >&2
  exit 2
fi

"$bukit_bin" import html-demo "$demo_dir" \
  --theme "$theme_name" \
  --content-source notion \
  --build-source markdown \
  --force

if [ ! -f "$map_path" ]; then
  echo "ERROR: Import did not generate database map: $map_path" >&2
  exit 1
fi

python3 - "$map_path" "$acceptance_map" <<'PY'
import json
import os
import sys
from pathlib import Path

source_path = Path(sys.argv[1])
output_path = Path(sys.argv[2])
lines = source_path.read_text(encoding="utf-8").splitlines()
try:
    start = lines.index("  pages:")
except ValueError as error:
    raise SystemExit("ERROR: generated database map has no pages entry") from error

end = len(lines)
for index in range(start + 1, len(lines)):
    line = lines[index]
    if line.startswith("  ") and not line.startswith("    ") and line.endswith(":"):
        end = index
        break

pages = lines[start:end]
needle = '    databaseId: ""'
if needle not in pages:
    raise SystemExit("ERROR: generated pages map has no empty default databaseId")

data_source_id = json.dumps(os.environ["NOTION_DATA_SOURCE_ID"])
pages[pages.index(needle)] = f"    dataSourceId: {data_source_id}"
output_path.parent.mkdir(parents=True, exist_ok=True)
output_path.write_text(
    "databases:\n" + "\n".join(pages) + "\n",
    encoding="utf-8",
)
PY

"$bukit_bin" notion validate-seed "$seed_dir"
"$bukit_bin" notion validate-database-map "$acceptance_map"
"$bukit_bin" notion push \
  --seed "$seed_dir" \
  --database-map "$acceptance_map" \
  --mode create \
  --dry-run \
  --report "$dry_run_report"

"$bukit_bin" notion push \
  --seed "$seed_dir" \
  --database-map "$acceptance_map" \
  --token-env NOTION_TOKEN \
  --mode create \
  --report "$live_report"

python3 - "$dry_run_report" "$live_report" "$summary_json" "$summary_md" "$theme_name" <<'PY'
import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path

dry_path = Path(sys.argv[1])
live_path = Path(sys.argv[2])
summary_json_path = Path(sys.argv[3])
summary_md_path = Path(sys.argv[4])
theme = sys.argv[5]
token = os.environ["NOTION_TOKEN"]
dry_text = dry_path.read_text(encoding="utf-8")
live_text = live_path.read_text(encoding="utf-8")
if token in dry_text or token in live_text:
    raise SystemExit("ERROR: Notion token leaked into an RC report")

dry = json.loads(dry_text)
live = json.loads(live_text)
if live.get("failed", 0) != 0:
    raise SystemExit("ERROR: live RC report contains failed records")

remote_page_ids = [
    record["remotePageId"]
    for record in live.get("records", [])
    if record.get("status") == "created" and record.get("remotePageId")
]
if not remote_page_ids:
    raise SystemExit("ERROR: live RC report contains no created remote page IDs")

summary = {
    "schema": "bukit.import-notion.rc-acceptance.v1",
    "pluginVersion": "1.0.0-rc.1",
    "acceptedAt": datetime.now(timezone.utc).isoformat(),
    "theme": theme,
    "scope": "pages",
    "dryRunReport": str(dry_path),
    "liveReport": str(live_path),
    "created": live.get("created", 0),
    "failed": live.get("failed", 0),
    "skipped": live.get("skipped", 0),
    "remotePageIds": remote_page_ids,
    "tokenPresent": False,
}

summary_json = Path(summary_json_path)
summary_md = Path(summary_md_path)
summary_json.parent.mkdir(parents=True, exist_ok=True)
summary_json.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
summary_md.write_text(
    "# Import + Notion RC Sandbox Acceptance\n\n"
    f"- Plugin version: {summary['pluginVersion']}\n"
    f"- Accepted at: {summary['acceptedAt']}\n"
    f"- Theme: {theme}\n"
    "- Scope: pages\n"
    f"- Created: {summary['created']}\n"
    f"- Failed: {summary['failed']}\n"
    f"- Skipped: {summary['skipped']}\n"
    f"- Remote page IDs: {', '.join(remote_page_ids)}\n"
    "- Token present in reports: false\n",
    encoding="utf-8",
)
PY

echo "Only the generated pages collection is accepted by this sandbox run."
echo "Import + Notion RC sandbox acceptance OK"
echo "JSON evidence: $summary_json"
echo "Markdown evidence: $summary_md"

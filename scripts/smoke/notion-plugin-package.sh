#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 1 ]; then
  echo "usage: notion-plugin-package.sh <package-root> [configuration]" >&2
  exit 2
fi

package_root="$1"
configuration="${2:-Release}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"
source scripts/lib/common.sh

manifest_path="$package_root/plugins/notion/plugin.yaml"
config_path="$package_root/.bukit/plugins.yaml"
required_rids=(win-x64 linux-x64 osx-arm64)
host_rid="$(bukit_host_rid)"
verify_rids=("${required_rids[@]}")

if [ "$host_rid" != "unsupported" ]; then
  host_listed=0
  for rid in "${verify_rids[@]}"; do
    if [ "$rid" = "$host_rid" ]; then
      host_listed=1
      break
    fi
  done

  if [ "$host_listed" -eq 0 ]; then
    verify_rids+=("$host_rid")
  fi
fi

test -s "$manifest_path"
test -s "$config_path"

python3 - "$manifest_path" "${verify_rids[@]}" <<'PY'
import re
import sys
from pathlib import Path

manifest = Path(sys.argv[1])
required = sys.argv[2:]
lines = manifest.read_text(encoding="utf-8").splitlines()
platforms = {}
current = None

for line in lines:
    stripped = line.strip()
    if line.startswith("  ") and not line.startswith("    ") and stripped.endswith(":"):
        current = stripped[:-1]
        platforms[current] = {}
    elif current and line.startswith("    ") and ":" in stripped:
        key, value = stripped.split(":", 1)
        platforms[current][key.strip()] = value.strip()

missing = [rid for rid in required if rid not in platforms]
if missing:
    raise SystemExit(f"ERROR: package manifest missing platform RID(s): {', '.join(missing)}")

for rid in required:
    entry = platforms[rid].get("entry", "")
    sha = platforms[rid].get("sha256", "")
    if not entry or entry.startswith("/") or ".." in Path(entry).parts or entry.startswith(".bukit/"):
        raise SystemExit(f"ERROR: invalid entry for {rid}: {entry}")
    if not re.fullmatch(r"[a-f0-9]{64}", sha) or sha == "0" * 64:
        raise SystemExit(f"ERROR: invalid sha256 for {rid}: {sha}")
PY

while IFS='|' read -r rid entry expected_sha; do
  executable="$package_root/plugins/notion/$entry"
  if [ ! -f "$executable" ]; then
    echo "ERROR: missing executable for $rid: $executable" >&2
    exit 1
  fi

  actual_sha="$(bukit_sha256 "$executable")"
  if [ "$actual_sha" != "$expected_sha" ]; then
    echo "ERROR: sha256 mismatch for $rid: expected $expected_sha, got $actual_sha" >&2
    exit 1
  fi

  case "$rid" in
    win-*) ;;
    *)
      if [ ! -x "$executable" ]; then
        echo "ERROR: executable bit is missing for $rid: $executable" >&2
        exit 1
      fi
      ;;
  esac
done < <(python3 - "$manifest_path" "${verify_rids[@]}" <<'PY'
import sys
from pathlib import Path

manifest = Path(sys.argv[1])
required = sys.argv[2:]
lines = manifest.read_text(encoding="utf-8").splitlines()
platforms = {}
current = None

for line in lines:
    stripped = line.strip()
    if line.startswith("  ") and not line.startswith("    ") and stripped.endswith(":"):
        current = stripped[:-1]
        platforms[current] = {}
    elif current and line.startswith("    ") and ":" in stripped:
        key, value = stripped.split(":", 1)
        platforms[current][key.strip()] = value.strip()

for rid in required:
    print(f"{rid}|{platforms[rid]['entry']}|{platforms[rid]['sha256']}")
PY
)

if grep -Fq "entry:" "$config_path"; then
  echo "ERROR: .bukit/plugins.yaml must not contain entry." >&2
  exit 1
fi

if [ "$host_rid" = "unsupported" ]; then
  echo "ERROR: unsupported host RID for plugin smoke." >&2
  exit 1
fi

if ! grep -Fq "  $host_rid:" "$manifest_path"; then
  echo "ERROR: package manifest has no host RID entry: $host_rid" >&2
  exit 1
fi

dotnet_cli() {
  (cd "$package_root" && dotnet run --project "$repo_root/src/Bukit.Cli/Bukit.Cli.csproj" -c "$configuration" -- "$@")
}

dotnet_cli plugin validate-config
dotnet_cli plugin validate-manifest plugins/notion

seed_dir="$package_root/sample-notion-seed"
rm -rf "$seed_dir"
mkdir -p "$seed_dir"
cat > "$seed_dir/pages.json" <<'JSON'
[
  {
    "title": "Package Smoke",
    "slug": "package-smoke",
    "published": true,
    "content": "Package smoke notion seed content."
  }
]
JSON

cat > "$seed_dir/notion-database-map.yaml" <<'YAML'
databases:
  pages:
    seed: pages.json
    collection: page
    dataSourceId: ds-pages
    uniqueField: Slug
    properties:
      Title:
        source: title
        type: title
      Slug:
        source: slug
        type: rich_text
      Published:
        source: published
        type: checkbox
YAML

dotnet_cli notion validate-seed ./sample-notion-seed
dotnet_cli notion validate-database-map ./sample-notion-seed/notion-database-map.yaml
dotnet_cli notion push --seed ./sample-notion-seed --database-map ./sample-notion-seed/notion-database-map.yaml --mode create --dry-run

test -s "$package_root/.bukit/reports/plugin-output/notion/notion-push-report.json"
test -s "$package_root/.bukit/reports/plugin-output/notion/notion-push-report.md"

echo "Notion plugin package smoke OK: $package_root"

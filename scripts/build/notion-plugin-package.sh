#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"
source scripts/lib/common.sh

output_root="${1:-TestResults/notion-plugin-package}"
configuration="${2:-Release}"
rids="${NOTION_PLUGIN_PACKAGE_RIDS:-win-x64 linux-x64 osx-arm64}"
host_rid="$(bukit_host_rid)"

case "$output_root" in
  ""|"/"|".")
    echo "ERROR: unsafe output root: $output_root" >&2
    exit 2
    ;;
esac

package_root="$output_root"
plugin_dir="$package_root/plugins/notion"
manifest_path="$plugin_dir/plugin.yaml"

rm -rf "$package_root"
mkdir -p "$plugin_dir" "$package_root/.bukit"
cp plugins/Bukit.Plugin.Notion/examples/minimal/.bukit/plugins.yaml "$package_root/.bukit/plugins.yaml"
cp plugins/Bukit.Plugin.Notion/examples/minimal/plugins/notion/plugin.yaml "$manifest_path"

entry_for_rid() {
  case "$1" in
    win-*) printf 'bin/%s/bukit-plugin-notion.exe\n' "$1" ;;
    *) printf 'bin/%s/bukit-plugin-notion\n' "$1" ;;
  esac
}

if [ "$host_rid" != "unsupported" ]; then
  case " $rids " in
    *" $host_rid "*) ;;
    *) rids="$rids $host_rid" ;;
  esac
fi

ensure_manifest_platform() {
  local rid="$1"
  local entry="$2"

  if grep -Fq "  $rid:" "$manifest_path"; then
    return 0
  fi

  python3 - "$manifest_path" "$rid" "$entry" <<'PY'
import sys
from pathlib import Path

path = Path(sys.argv[1])
rid = sys.argv[2]
entry = sys.argv[3]
lines = path.read_text(encoding="utf-8").splitlines()
for index, line in enumerate(lines):
    if line.strip() == "platforms:":
        lines[index + 1:index + 1] = [
            f"  {rid}:",
            f"    entry: {entry}",
            "    sha256: 0000000000000000000000000000000000000000000000000000000000000000",
        ]
        path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        raise SystemExit(0)

raise SystemExit("ERROR: missing platforms section in plugin manifest")
PY
}

update_manifest_sha() {
  local rid="$1"
  local sha="$2"

  python3 - "$manifest_path" "$rid" "$sha" <<'PY'
import sys
from pathlib import Path

path = Path(sys.argv[1])
rid = sys.argv[2]
sha = sys.argv[3]
lines = path.read_text(encoding="utf-8").splitlines()
inside = False
changed = False

for index, line in enumerate(lines):
    stripped = line.strip()
    if line.startswith("  ") and not line.startswith("    ") and stripped.endswith(":"):
        inside = stripped == f"{rid}:"
        continue

    if inside and line.startswith("    sha256:"):
        lines[index] = f"    sha256: {sha}"
        changed = True
        break

if not changed:
    raise SystemExit(f"ERROR: missing sha256 field for RID {rid}")

path.write_text("\n".join(lines) + "\n", encoding="utf-8")
PY
}

for rid in $rids; do
  publish_dir="$plugin_dir/bin/$rid"
  mkdir -p "$publish_dir"
  entry="$(entry_for_rid "$rid")"
  ensure_manifest_platform "$rid" "$entry"

  dotnet publish plugins/Bukit.Plugin.Notion/Bukit.Plugin.Notion.csproj \
    -c "$configuration" \
    -r "$rid" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -p:DebugType=none \
    -p:DebugSymbols=false \
    -o "$publish_dir" \
    -maxcpucount:1 \
    -nodeReuse:false

  executable="$plugin_dir/$entry"
  if [ ! -f "$executable" ]; then
    echo "ERROR: expected plugin executable was not published: $executable" >&2
    exit 1
  fi

  case "$rid" in
    win-*) ;;
    *) chmod +x "$executable" ;;
  esac

  update_manifest_sha "$rid" "$(bukit_sha256 "$executable")"
done

if grep -Eq 'sha256: 0{64}' "$manifest_path"; then
  echo "ERROR: package manifest still contains placeholder sha256 values." >&2
  exit 1
fi

echo "Notion plugin package OK: $package_root"

#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 1 ]; then
  echo "usage: import-plugin-package.sh <package-root> [configuration]" >&2
  exit 2
fi

package_root="$1"
configuration="${2:-Release}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"
source scripts/lib/common.sh

manifest_path="$package_root/plugins/import/plugin.yaml"
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
  executable="$package_root/plugins/import/$entry"
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
dotnet_cli plugin validate-manifest plugins/import

seed_dir="$package_root/package-smoke-seed"
demo_dir="$package_root/package-smoke-demo"
rm -rf "$seed_dir" "$demo_dir" "$package_root/content" "$package_root/sites" "$package_root/themes"
mkdir -p "$seed_dir" "$demo_dir/assets"
cat > "$seed_dir/pages.json" <<'JSON'
[
  {
    "title": "Package Smoke",
    "slug": "package-smoke",
    "content": "Package smoke seed content.",
    "language": "en"
  }
]
JSON

cat > "$demo_dir/index.html" <<'HTML'
<!doctype html>
<html lang="en">
  <head><title>Package Smoke Demo</title></head>
  <body>
    <header><nav><a href="index.html">Home</a></nav></header>
    <main><h1>Package Smoke Demo</h1><p>Import plugin package smoke.</p></main>
    <footer>Footer</footer>
  </body>
</html>
HTML

dotnet_cli import seed ./package-smoke-seed --output ./content --force
test -s "$package_root/content/pages/package-smoke.md"

dotnet_cli import html-demo ./package-smoke-demo --theme package-smoke --dry-run

echo "Import plugin package smoke OK: $package_root"

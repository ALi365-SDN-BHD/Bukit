#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"
source scripts/lib/common.sh

artifact_dir="${RELEASE_GATE_ARTIFACT_DIR:-TestResults/release-gate}"
rid_list="${RELEASE_GATE_RIDS:-$(bukit_host_rid)}"

echo "=== release: full gate ==="
bash scripts/gates/ci-full.sh "$configuration"

echo "=== release: config schema artifact ==="
mkdir -p "$artifact_dir"
bukit_cli "$configuration" config schema --output "$artifact_dir/site.schema.json"
test -s "$artifact_dir/site.schema.json"
python3 -m json.tool "$artifact_dir/site.schema.json" >/dev/null

echo "=== release: Native AOT artifacts ==="
for rid in $rid_list; do
  CONFIGURATION="$configuration" bash scripts/build/native-aot.sh "$rid" "$artifact_dir/native-aot/$rid" "$artifact_dir/native-aot/$rid.log"
  bash scripts/smoke/release-artifacts.sh "$artifact_dir/native-aot/$rid"
done

echo "Release gate OK"

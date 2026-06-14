#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"
rid="${2:-$(uname -s | tr '[:upper:]' '[:lower:]')}"
sample_config="${3:-tests/fixtures/basic-markdown-site/site.yaml}"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

if [ "$rid" = "darwin" ]; then
  rid="osx-arm64"
elif [ "$rid" = "linux" ]; then
  rid="linux-x64"
fi

jit_output=".smoke-all-run/perf-jit-$$/dist"
aot_output=".smoke-all-run/perf-aot-$$/dist"
aot_publish_dir="TestResults/perf-aot/$rid"

dotnet build bukit.slnx -c "$configuration" -maxcpucount:1 -nodeReuse:false
CONFIGURATION="$configuration" bash scripts/build/native-aot.sh "$rid" "$aot_publish_dir"

config_dir="$(dirname "$sample_config")"
trap 'rm -rf "$config_dir/.smoke-all-run/perf-jit-$$" "$config_dir/.smoke-all-run/perf-aot-$$"' EXIT

echo "== JIT baseline =="
/usr/bin/time dotnet run --project src/Bukit.Cli -c "$configuration" -- \
  build --config "$sample_config" --output "$jit_output" --clean --metrics "$jit_output-metrics.json" --site-url https://example.com

binary="$aot_publish_dir/bukit"
if [ -f "$aot_publish_dir/bukit.exe" ]; then
  binary="$aot_publish_dir/bukit.exe"
fi

echo "== AOT baseline =="
/usr/bin/time "$binary" \
  build --config "$sample_config" --output "$aot_output" --clean --metrics "$aot_output-metrics.json" --site-url https://example.com

#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"
rid="${2:-osx-arm64}"
sample_config="${3:-examples/starter/site.yaml}"

jit_out="${4:-/tmp/bukit-perf-jit}"
aot_out="${5:-/tmp/bukit-perf-aot}"

jit_metrics="${jit_out}-metrics.json"
aot_metrics="${aot_out}-metrics.json"
aot_publish_dir="/tmp/bukit-aot-bench-${rid}"

echo "== Build (${configuration}) =="
dotnet build bukit.slnx -c "${configuration}" -maxcpucount:1 -nodeReuse:false

echo "== Publish AOT (${rid}) =="
dotnet publish src/Bukit.Cli/Bukit.Cli.csproj -c AOT -r "${rid}" -o "${aot_publish_dir}" -maxcpucount:1 -nodeReuse:false

echo "== JIT baseline =="
/usr/bin/time -l dotnet src/Bukit.Cli/bin/${configuration}/net10.0/bukit.dll \
  build --config "${sample_config}" --output "${jit_out}" --clean --metrics "${jit_metrics}" --log-format json

echo "== AOT baseline =="
/usr/bin/time -l "${aot_publish_dir}/bukit" \
  build --config "${sample_config}" --output "${aot_out}" --clean --metrics "${aot_metrics}" --log-format json

echo "== Metrics files =="
echo "JIT: ${jit_metrics}"
echo "AOT: ${aot_metrics}"

#!/usr/bin/env bash
set -euo pipefail

rid="${1:-linux-x64}"
out_dir="${2:-/tmp/bukit-aot-check-${rid}}"
log_file="${3:-/tmp/bukit-aot-check-${rid}.log}"

host_os="$(uname -s)"
if [[ "${rid}" == linux-* && "${host_os}" != "Linux" ]]; then
  echo "Skip: RID '${rid}' requires Linux host for native linking. Current host is ${host_os}."
  exit 0
fi

rm -f "${log_file}"

dotnet publish src/Bukit.Cli/Bukit.Cli.csproj \
  -c AOT \
  -r "${rid}" \
  -o "${out_dir}" \
  -maxcpucount:1 \
  -nodeReuse:false \
  -p:TrimmerSingleWarn=false \
  2>&1 | tee "${log_file}"

warn_lines="$(grep -E "ILC : .*warning IL[0-9]{4}" "${log_file}" || true)"
if [[ -z "${warn_lines}" ]]; then
  echo "No NativeAOT/trim warnings."
  exit 0
fi

echo "Fail: found AOT/trim warnings:" >&2
echo "${warn_lines}" >&2
exit 1

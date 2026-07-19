#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-core-smoke-self-test.XXXXXX")"
trap 'rm -rf "$scratch"' EXIT

fake="$scratch/fake-bukit"
log="$scratch/commands.log"
mkdir -p "$scratch/default-root" "$scratch/custom-root"
printf 'site:\n  name: custom\n  title: Custom\n' > "$scratch/custom-root/site.yaml"

cat > "$fake" <<'SH'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >> "${FAKE_BUKIT_LOG:?}"
SH
chmod 755 "$fake"

BUKIT_BIN="$fake" \
  BUKIT_SMOKE_ROOT="$scratch/default-root" \
  BUKIT_SMOKE_CONFIG="$scratch/custom-root/site.yaml" \
  BUKIT_SMOKE_OUTPUT="nested/out" \
  FAKE_BUKIT_LOG="$log" \
  bash "$repo_root/scripts/smoke/core.sh" >/dev/null

expected_config="$scratch/custom-root/site.yaml"
[[ "$(sed -n '1p' "$log")" == "config check --config $expected_config" ]]
[[ "$(sed -n '2p' "$log")" == \
  "build --config $expected_config --output nested/out --clean" ]]
[[ "$(sed -n '3p' "$log")" == \
  "publish audit --dir $scratch/custom-root/nested/out" ]]
[[ -z "$(sed -n '4p' "$log")" ]]

echo "core smoke self-test: PASS"

#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

tmp_root="$(mktemp -d "${TMPDIR:-/tmp}/bukit-cli-docs-sync.XXXXXX")"
cleanup() {
  rm -rf "$tmp_root"
}
trap cleanup EXIT

copy_fixture() {
  local target="$1"
  mkdir -p "$target"
  tar -C "$repo_root" \
    --exclude .git \
    --exclude .worktrees \
    --exclude bin \
    --exclude obj \
    --exclude TestResults \
    -cf - . | tar -C "$target" -xf -
}

expect_failure() {
  local name="$1"
  local fixture="$2"
  local expected="$3"

  set +e
  output="$(cd "$fixture" && bash scripts/checks/cli-docs-sync.sh 2>&1)"
  status=$?
  set -e

  if [ "$status" -eq 0 ]; then
    echo "Expected cli-docs-sync failure for ${name}, but it passed." >&2
    exit 1
  fi

  if ! grep -Fq -- "$expected" <<<"$output"; then
    echo "cli-docs-sync failure for ${name} did not include expected text: ${expected}" >&2
    echo "$output" >&2
    exit 1
  fi
}

missing_option_fixture="$tmp_root/missing-option"
copy_fixture "$missing_option_fixture"
python3 - "$missing_option_fixture/guide/dev/cli.md" <<'PY'
from pathlib import Path
import sys

path = Path(sys.argv[1])
text = path.read_text(encoding="utf-8")
text = text.replace(", `--force` |", " |")
path.write_text(text, encoding="utf-8")
PY
expect_failure "missing option" "$missing_option_fixture" "deploy: missing parameters: --force"

extra_option_fixture="$tmp_root/extra-option"
copy_fixture "$extra_option_fixture"
python3 - "$extra_option_fixture/guide/skills/bukit-cli-reference/SKILL.md" <<'PY'
from pathlib import Path
import sys

path = Path(sys.argv[1])
text = path.read_text(encoding="utf-8")
text = text.replace("`--force` |", "`--force` `--not-real` |")
path.write_text(text, encoding="utf-8")
PY
expect_failure "extra option" "$extra_option_fixture" "--not-real"

argument_fixture="$tmp_root/argument-drift"
copy_fixture "$argument_fixture"
python3 - "$argument_fixture/guide/dev/cli.md" <<'PY'
from pathlib import Path
import sys

path = Path(sys.argv[1])
text = path.read_text(encoding="utf-8")
text = text.replace("| `completion` | Generate shell completion | `<shell>` |", "| `completion` | Generate shell completion | `<profile>` |")
path.write_text(text, encoding="utf-8")
PY
expect_failure "argument drift" "$argument_fixture" "completion: missing parameters: <shell>"

echo "CLI docs sync fixture tests OK"

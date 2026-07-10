#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd -P)"
tmp_root="$(mktemp -d)"
trap 'rm -rf "$tmp_root"' EXIT

projects="${tmp_root}/projects.tsv"
printf '%s\t%s\n' \
  'tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj' '' \
  'tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj' 'FullyQualifiedName!~Fixture' > "$projects"

matrix="$(bash "${repo_root}/scripts/checks/coverage/matrix.py" "$projects")"
python3 - "$matrix" <<'PY'
import json
import sys

rows = json.loads(sys.argv[1])["include"]
assert rows == [
    {"project": "tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj", "name": "Bukit.Shared.Tests", "filter": ""},
    {"project": "tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj", "name": "Bukit.Engine.Tests", "filter": "FullyQualifiedName!~Fixture"},
]
PY

echo "coverage matrix self-test OK"

#!/usr/bin/env bash
set -euo pipefail

SKILLS_DIR="$(cd "$(dirname "$0")/.." && pwd)"
python3 - "$SKILLS_DIR" <<'PY'
import json
import sys
from pathlib import Path

import yaml

skills_dir = Path(sys.argv[1])
yaml_path = skills_dir / "skills-index.yaml"
json_path = skills_dir / "skills-index.json"

with yaml_path.open("r", encoding="utf-8") as handle:
    data = yaml.safe_load(handle)

with json_path.open("w", encoding="utf-8") as handle:
    json.dump(data, handle, indent=2, ensure_ascii=False)
    handle.write("\n")
PY

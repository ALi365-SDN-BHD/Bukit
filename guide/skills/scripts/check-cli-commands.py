#!/usr/bin/env python3
from pathlib import Path
import subprocess
import sys

repo = Path(__file__).resolve().parents[3]
subprocess.check_call([sys.executable, str(repo / "scripts/checks/cli-docs/validate_docs.py")], cwd=repo)

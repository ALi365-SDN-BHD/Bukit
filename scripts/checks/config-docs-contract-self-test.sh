#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../.."
python3 - <<'PY'
from pathlib import Path
import runpy
import shutil
import subprocess
import sys
import tempfile

root = Path.cwd()
checker = Path("scripts/checks/config-docs/validate_docs.py")
module = runpy.run_path(str(checker))
source = Path("src/Bukit-Core/Bukit.Config/AppConfig.cs")
notion_doc = Path("guide/user/06-notion-content.md")
config_doc = Path("guide/user/04-site-yaml-config.md")

with tempfile.TemporaryDirectory(prefix="bukit-config-docs-") as directory:
    fixture = Path(directory)
    for path in [checker, source, *map(Path, module["required_tokens"])]:
        target = fixture / path
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(root / path, target)

    def check(expected_code, expected_text):
        result = subprocess.run([sys.executable, str(checker)], cwd=fixture,
                                capture_output=True, text=True)
        assert result.returncode == expected_code, result.stdout + result.stderr
        assert expected_text in result.stdout, result.stdout + result.stderr

    check(0, "config docs contract OK")
    for doc in (notion_doc, config_doc):
        original = (fixture / doc).read_text()
        # Mentioning a missing key elsewhere must not satisfy the inventory.
        (fixture / doc).write_text(original.replace("`OriginalUrl`, ", "", 1)
                                  + "\n`OriginalUrl` is mentioned elsewhere.\n")
        check(1, f"{doc}: propertyMap missing field `OriginalUrl`")
        (fixture / doc).write_text(original.replace("`OriginalUrl`, ", "`originalUrl`, ", 1))
        check(1, f"{doc}: propertyMap unknown field `originalUrl`")
        (fixture / doc).write_text(original.replace("`OriginalUrl`, `References`", "`References`, `OriginalUrl`"))
        check(0, "config docs contract OK")
        (fixture / doc).write_text(original)

    original_source = (fixture / source).read_text()
    (fixture / source).write_text(original_source.replace(
        "public sealed record NotionPropertyMapConfig\n{",
        "public sealed record NotionPropertyMapConfig\n{\n    public string? FutureField { get; init; }"))
    check(1, "propertyMap missing field `FutureField`")
    (fixture / source).write_text(original_source.replace(
        "public sealed record NotionPropertyMapConfig", "public sealed record RenamedPropertyMapConfig"))
    check(1, "cannot extract NotionPropertyMapConfig")
    (fixture / source).write_text(original_source.replace(
        "public string? OriginalUrl { get; init; }", "public string? OriginalUrl => null;"))
    check(1, "unsupported NotionPropertyMapConfig declaration")

print("config docs contract self-test OK (10 cases)")
PY

#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
prepare="$script_dir/prepare-release-assets.sh"
verify="$script_dir/verify-release-assets.sh"
tmp="$(mktemp -d)"
tmp="$(cd "$tmp" && pwd -P)"
trap 'rm -rf "$tmp"' EXIT

fail() {
  echo "$*" >&2
  exit 1
}

expect_fail() {
  local message="$1"
  shift
  if "$@"; then
    fail "$message"
  fi
}

make_asset() {
  local path="$1"
  mkdir -p "$(dirname "$path")"
  printf 'asset:%s\n' "$(basename "$path")" > "$path"
}

version=1.2.3
commit=abc
linux="$tmp/input/bukit-$version-linux-x64.tar.gz"
macos="$tmp/input/bukit-$version-osx-arm64.tar.gz"
windows="$tmp/input/bukit-$version-win-x64.zip"
make_asset "$linux"
make_asset "$macos"
make_asset "$windows"

python3 - "$script_dir/release-assets.py" "$tmp" "$linux" <<'PY'
import importlib.util
import sys
from pathlib import Path

module_path, root, archive = Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3])
spec = importlib.util.spec_from_file_location("release_assets", module_path)
assert spec is not None and spec.loader is not None
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

def exercise_install_failure(name, fail_restore):
    output = root / name
    output.mkdir()
    marker = output / "old-output.marker"
    marker.write_text("keep-old-output\n", encoding="utf-8")
    real_replace = module.os.replace

    def fail_replace(source, destination):
        source_path, destination_path = Path(source), Path(destination)
        if destination_path == output:
            if ".backup." not in source_path.name:
                raise OSError("injected staging install failure")
            if fail_restore:
                raise OSError("injected previous output restore failure")
        return real_replace(source, destination)

    module.os.replace = fail_replace
    try:
        module.prepare("1.2.3", "abc", str(output), [str(archive)])
    except (OSError, module.ContractError) as error:
        return output, marker, error
    finally:
        module.os.replace = real_replace
    raise AssertionError("injected install failure unexpectedly passed")

output, marker, error = exercise_install_failure("recover-output", False)
assert isinstance(error, OSError) and "injected staging install failure" in str(error)
assert output.is_dir(), "old output directory lost after install failure"
assert marker.read_text(encoding="utf-8") == "keep-old-output\n", \
    "old output marker lost after install failure"
assert not list(root.glob(".recover-output.backup.*")), "backup leaked after recovery"

output, _, error = exercise_install_failure("double-failure-output", True)
assert isinstance(error, module.ContractError), error
assert "injected staging install failure" in str(error), error
assert "injected previous output restore failure" in str(error), error
backups = list(root.glob(".double-failure-output.backup.*"))
assert not output.exists() and len(backups) == 1, "failed recovery lost unique backup"
assert (backups[0] / "old-output.marker").read_text(encoding="utf-8") == \
    "keep-old-output\n", "failed recovery lost old output marker"

checkout_root = module_path.resolve().parents[2]
for unsafe in (checkout_root, *checkout_root.parents):
    try:
        module.resolve_output(str(unsafe))
    except module.ContractError:
        pass
    else:
        raise AssertionError(f"unsafe output ancestor unexpectedly allowed: {unsafe}")

safe = root / "safe-output"
assert module.resolve_output(str(safe)) == safe

output = root / "duplicate-json-keys"
module.prepare("1.2.3", "abc", str(output), [str(archive)])
manifest_path = output / "release-manifest.json"
checksums_path = output / "checksums.json"
originals = {
    manifest_path: manifest_path.read_text(encoding="utf-8"),
    checksums_path: checksums_path.read_text(encoding="utf-8"),
}

def duplicate_line(text, token, final=False):
    line = next(line for line in text.splitlines() if token in line)
    first = f"{line}," if final else line
    return text.replace(line, f"{first}\n{line}", 1)

manifest = originals[manifest_path]
checksums = originals[checksums_path]
cases = [
    ("manifest root schema", manifest_path, duplicate_line(manifest, '"schema"')),
    ("manifest root assets", manifest_path,
     manifest.replace('  "assets": [', '  "assets": [],\n  "assets": [', 1)),
    ("checksums root assets", checksums_path,
     checksums.replace('  "assets": [', '  "assets": [],\n  "assets": [', 1)),
]
for label, path, text in (("manifest", manifest_path, manifest),
                          ("checksums", checksums_path, checksums)):
    cases.extend([
        (f"{label} asset name", path, duplicate_line(text, '"name"')),
        (f"{label} asset sha256", path, duplicate_line(text, '"sha256"')),
        (f"{label} asset bytes", path, duplicate_line(text, '"bytes"', final=True)),
    ])

accepted = []
for label, path, mutated in cases:
    for original_path, original in originals.items():
        original_path.write_text(original, encoding="utf-8")
    path.write_text(mutated, encoding="utf-8")
    try:
        module.verify("1.2.3", "abc", output, ["linux-x64"])
    except module.ContractError as error:
        if "duplicate JSON key" not in str(error):
            raise
    else:
        accepted.append(label)

assert not accepted, f"duplicate JSON keys unexpectedly passed: {accepted}"
PY

out="$tmp/duplicate-path"
if bash "$prepare" "$version" "$commit" "$out" "$linux" "$linux"; then
  fail "duplicate archive unexpectedly passed"
fi

duplicate_basename="$tmp/other/$(basename "$linux")"
make_asset "$duplicate_basename"
expect_fail "duplicate basename unexpectedly passed" \
  bash "$prepare" "$version" "$commit" "$tmp/duplicate-basename" \
  "$linux" "$duplicate_basename"

reserved="$tmp/input/checksums.txt"
make_asset "$reserved"
expect_fail "reserved metadata name unexpectedly passed" \
  bash "$prepare" "$version" "$commit" "$tmp/reserved" "$reserved"

symlink="$tmp/input/bukit-$version-linux-x64-link.tar.gz"
ln -s "$linux" "$symlink"
expect_fail "symlink archive unexpectedly passed" \
  bash "$prepare" "$version" "$commit" "$tmp/symlink" "$symlink"

wrong_extension="$tmp/input/bukit-$version-win-x64.tar.gz"
make_asset "$wrong_extension"
expect_fail "wrong RID extension unexpectedly passed" \
  bash "$prepare" "$version" "$commit" "$tmp/wrong-extension" "$wrong_extension"

out="$tmp/valid-linux"
bash "$prepare" "$version" "$commit" "$out" "$linux"
printf '%064d  extra.tar.gz\n' 0 >> "$out/checksums.txt"
expect_fail "extra checksum unexpectedly passed" \
  bash "$verify" "$version" "$commit" "$out" linux-x64

bash "$prepare" "$version" "$commit" "$out" "$linux"
printf 'stale\n' > "$out/stale-debug.zip"
expect_fail "stale disk asset unexpectedly passed" \
  bash "$verify" "$version" "$commit" "$out" linux-x64

bash "$prepare" "$version" "$commit" "$out" "$linux"
expect_fail "duplicate RID unexpectedly passed" \
  bash "$verify" "$version" "$commit" "$out" linux-x64 linux-x64

out="$tmp/all-rids"
bash "$prepare" "$version" "$commit" "$out" "$linux" "$macos" "$windows"
bash "$verify" "$version" "$commit" "$out" linux-x64 osx-arm64 win-x64

echo "release-assets self-test OK"

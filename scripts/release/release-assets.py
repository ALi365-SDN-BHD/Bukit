#!/usr/bin/env python3
from __future__ import annotations

import hashlib
import json
import os
import re
import shutil
import sys
import tempfile
from pathlib import Path

RID_SUFFIX = {"linux-x64": ".tar.gz", "osx-arm64": ".tar.gz", "win-x64": ".zip"}
METADATA = {"checksums.txt", "checksums.json", "release-manifest.json"}
SCHEMA = "bukit-release-manifest-v1"
TOKEN = re.compile(r"[A-Za-z0-9][A-Za-z0-9._-]*")
CHECKSUM = re.compile(r"([0-9a-f]{64})  ([^/\\\x00-\x1f]+)")

class ContractError(Exception):
    pass
def require(condition: bool, message: str) -> None:
    if not condition:
        raise ContractError(message)
def validate_identity(version: str, commit: str) -> None:
    require(TOKEN.fullmatch(version) is not None, "invalid release version")
    require(TOKEN.fullmatch(commit) is not None, "invalid release commit")
def expected_name(version: str, rid: str) -> str:
    require(rid in RID_SUFFIX, f"unsupported release RID: {rid}")
    return f"bukit-{version}-{rid}{RID_SUFFIX[rid]}"
def asset_record(path: Path) -> dict[str, object]:
    return {"name": path.name, "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "bytes": path.stat().st_size}

def resolve_output(value: str) -> Path:
    require(value.strip() not in {"", ".", ".."}, "unsafe release output directory")
    output = Path(value).expanduser().absolute()
    resolved_output = output.resolve()
    require(resolved_output == output, "release output path must already be canonical")
    repo_root = Path(__file__).resolve().parents[2]
    require(output != Path(output.anchor) and not repo_root.is_relative_to(output),
            "unsafe release output directory")
    require(not output.is_symlink(), "release output directory must not be a symlink")
    parent = output.parent
    require(parent.is_dir() and not parent.is_symlink() and parent.resolve() == parent,
            "release output parent must be an existing real directory")
    require(not output.exists() or output.is_dir(), "release output must be a directory")
    return output

def archive_inputs(version: str, values: list[str]) -> tuple[list[Path], list[str]]:
    allowed = {expected_name(version, rid): rid for rid in RID_SUFFIX}
    paths: list[Path] = []
    seen_paths: set[Path] = set()
    seen_names: set[str] = set()
    rids: list[str] = []
    for value in values:
        path = Path(value)
        require(not path.is_symlink() and path.is_file(),
                f"archive must be a regular non-symlink file: {value}")
        resolved = path.resolve()
        require(resolved not in seen_paths, f"duplicate archive path: {value}")
        require(path.name not in seen_names, f"duplicate archive basename: {path.name}")
        require(path.name not in METADATA, f"reserved release metadata name: {path.name}")
        require(path.name in allowed, f"unexpected release archive name: {path.name}")
        seen_paths.add(resolved)
        seen_names.add(path.name)
        paths.append(path)
        rids.append(allowed[path.name])
    return paths, rids

def exact_keys(value: object, keys: set[str], label: str) -> dict[str, object]:
    require(isinstance(value, dict) and set(value) == keys, f"{label} must have exact keys: {sorted(keys)}")
    return value
def records(value: object, label: str) -> dict[str, dict[str, object]]:
    require(isinstance(value, list), f"{label} assets must be an array")
    by_name: dict[str, dict[str, object]] = {}
    for item in value:
        record = exact_keys(item, {"name", "sha256", "bytes"}, f"{label} asset")
        name, digest, size = record["name"], record["sha256"], record["bytes"]
        require(isinstance(name, str) and bool(name) and name not in METADATA, f"invalid {label} asset name")
        require(isinstance(digest, str) and re.fullmatch(r"[0-9a-f]{64}", digest) is not None,
                f"invalid {label} asset sha256: {name}")
        require(not isinstance(size, bool) and isinstance(size, int) and size >= 0, f"invalid {label} asset bytes: {name}")
        require(name not in by_name, f"duplicate {label} asset name: {name}")
        by_name[name] = record
    return by_name
def compare_set(label: str, expected: set[str], actual: set[str]) -> None:
    if actual == expected:
        return
    missing, extra = sorted(expected - actual), sorted(actual - expected)
    raise ContractError(f"{label} asset set mismatch; missing={missing} extra={extra}")

def reject_duplicate_keys(pairs: list[tuple[str, object]]) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        require(key not in result, f"duplicate JSON key: {key}")
        result[key] = value
    return result

def load_json(path: Path, label: str) -> dict[str, object]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"),
                           object_pairs_hook=reject_duplicate_keys)
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ContractError(f"invalid {label}: {error}") from error
    return exact_keys(value, {"assets"} if label == "checksums.json" else {"schema", "version", "commit", "assets"}, label)

def verify(version: str, commit: str, directory: str | Path, expected_rids: list[str]) -> None:
    validate_identity(version, commit)
    require(len(expected_rids) == len(set(expected_rids)), "duplicate expected release RID")
    if not expected_rids:
        expected_rids = list(RID_SUFFIX)
    expected = {expected_name(version, rid) for rid in expected_rids}
    asset_dir = Path(directory)
    require(not asset_dir.is_symlink() and asset_dir.is_dir(), f"asset directory must be a real directory: {asset_dir}")
    entries = list(asset_dir.iterdir())
    for path in entries:
        require(not path.is_symlink() and path.is_file(), f"release asset entry must be a regular non-symlink file: {path.name}")
    entry_names = {path.name for path in entries}
    disk = entry_names - METADATA
    for name in METADATA:
        require(name in entry_names, f"missing release asset metadata: {name}")
    manifest = load_json(asset_dir / "release-manifest.json", "release-manifest.json")
    checksums_json = load_json(asset_dir / "checksums.json", "checksums.json")
    require(manifest["schema"] == SCHEMA and manifest["version"] == version and manifest["commit"] == commit,
            "release manifest identity mismatch")
    manifest_by_name = records(manifest["assets"], "manifest")
    json_by_name = records(checksums_json["assets"], "checksums JSON")
    text_by_name: dict[str, str] = {}
    for line in (asset_dir / "checksums.txt").read_text(encoding="utf-8").splitlines():
        match = CHECKSUM.fullmatch(line)
        require(match is not None, f"invalid checksum line: {line!r}")
        digest, name = match.groups()
        require(name not in text_by_name, f"duplicate checksum name: {name}")
        text_by_name[name] = digest
    for label, names in (("disk", disk), ("manifest", set(manifest_by_name)),
                         ("checksums JSON", set(json_by_name)), ("checksums text", set(text_by_name))):
        compare_set(label, expected, names)
    for name in sorted(expected):
        actual = asset_record(asset_dir / name)
        require(manifest_by_name[name] == actual and json_by_name[name] == actual, f"asset record mismatch: {name}")
        require(text_by_name[name] == actual["sha256"], f"checksum mismatch: {name}")

def install_staging(staging: Path, output: Path) -> None:
    backup: Path | None = None
    if output.exists():
        backup = Path(tempfile.mkdtemp(prefix=f".{output.name}.backup.", dir=output.parent))
        backup.rmdir()
        os.replace(output, backup)
    try:
        os.replace(staging, output)
    except OSError as install_error:
        if backup is not None:
            try:
                os.replace(backup, output)
            except OSError as restore_error:
                raise ContractError(
                    f"release asset install failed: {install_error}; "
                    f"previous output restore failed: {restore_error}"
                ) from restore_error
        raise
    if backup is not None:
        shutil.rmtree(backup)

def prepare(version: str, commit: str, output_value: str, values: list[str]) -> None:
    validate_identity(version, commit)
    output = resolve_output(output_value)
    archives, rids = archive_inputs(version, values)
    staging = Path(tempfile.mkdtemp(prefix=f".{output.name}.", dir=output.parent))
    try:
        for archive in archives:
            shutil.copy2(archive, staging / archive.name)
        generated = [asset_record(path) for path in sorted(staging.iterdir())]
        manifest = {"schema": SCHEMA, "version": version, "commit": commit, "assets": generated}
        (staging / "release-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        (staging / "checksums.json").write_text(json.dumps({"assets": generated}, indent=2) + "\n", encoding="utf-8")
        lines = "".join(f'{item["sha256"]}  {item["name"]}\n' for item in generated)
        (staging / "checksums.txt").write_text(lines, encoding="utf-8")
        verify(version, commit, staging, rids)
        install_staging(staging, output)
    finally:
        if staging.exists():
            shutil.rmtree(staging)

def main(argv: list[str]) -> int:
    if not argv or argv[0] not in {"prepare", "verify"}:
        print("usage: release-assets.py <prepare|verify> ...", file=sys.stderr)
        return 2
    command, args = argv[0], argv[1:]
    if (command == "prepare" and len(args) < 4) or (command == "verify" and len(args) < 3):
        print(f"usage: release-assets.py {command} VERSION COMMIT PATH [ITEM...]", file=sys.stderr)
        return 2
    try:
        (prepare if command == "prepare" else verify)(args[0], args[1], args[2], args[3:])
    except (ContractError, OSError) as error:
        print(error, file=sys.stderr)
        return 1
    print(f"release assets {command} OK: {args[2]}")
    return 0

if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))

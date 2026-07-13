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


def validate_identity(version: str, commit: str) -> None:
    if not TOKEN.fullmatch(version):
        raise ContractError("invalid release version")
    if not TOKEN.fullmatch(commit):
        raise ContractError("invalid release commit")


def expected_name(version: str, rid: str) -> str:
    if rid not in RID_SUFFIX:
        raise ContractError(f"unsupported release RID: {rid}")
    return f"bukit-{version}-{rid}{RID_SUFFIX[rid]}"


def asset_record(path: Path) -> dict[str, object]:
    return {
        "name": path.name,
        "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        "bytes": path.stat().st_size,
    }


def resolve_output(value: str) -> Path:
    raw = Path(value).expanduser()
    if value.strip() in {"", ".", ".."}:
        raise ContractError("unsafe release output directory")
    output = raw.absolute()
    resolved_output = output.resolve()
    if resolved_output != output:
        raise ContractError("release output path must already be canonical")
    output = resolved_output
    repo_root = Path(__file__).resolve().parents[2]
    if output == Path(output.anchor) or repo_root.is_relative_to(output):
        raise ContractError("unsafe release output directory")
    if output.is_symlink():
        raise ContractError("release output directory must not be a symlink")
    parent = output.parent
    if not parent.is_dir() or parent.is_symlink() or parent.resolve() != parent:
        raise ContractError("release output parent must be an existing real directory")
    if output.exists() and not output.is_dir():
        raise ContractError("release output must be a directory")
    return output


def archive_inputs(version: str, values: list[str]) -> tuple[list[Path], list[str]]:
    allowed = {expected_name(version, rid): rid for rid in RID_SUFFIX}
    paths: list[Path] = []
    seen_paths: set[Path] = set()
    seen_names: set[str] = set()
    rids: list[str] = []
    for value in values:
        path = Path(value)
        if path.is_symlink() or not path.is_file():
            raise ContractError(f"archive must be a regular non-symlink file: {value}")
        resolved = path.resolve()
        if resolved in seen_paths:
            raise ContractError(f"duplicate archive path: {value}")
        if path.name in seen_names:
            raise ContractError(f"duplicate archive basename: {path.name}")
        if path.name in METADATA:
            raise ContractError(f"reserved release metadata name: {path.name}")
        if path.name not in allowed:
            raise ContractError(f"unexpected release archive name: {path.name}")
        seen_paths.add(resolved)
        seen_names.add(path.name)
        paths.append(path)
        rids.append(allowed[path.name])
    return paths, rids


def exact_keys(value: object, keys: set[str], label: str) -> dict[str, object]:
    if not isinstance(value, dict) or set(value) != keys:
        raise ContractError(f"{label} must have exact keys: {sorted(keys)}")
    return value


def records(value: object, label: str) -> tuple[list[dict[str, object]], dict[str, dict[str, object]]]:
    if not isinstance(value, list):
        raise ContractError(f"{label} assets must be an array")
    result: list[dict[str, object]] = []
    by_name: dict[str, dict[str, object]] = {}
    for item in value:
        record = exact_keys(item, {"name", "sha256", "bytes"}, f"{label} asset")
        name, digest, size = record["name"], record["sha256"], record["bytes"]
        if not isinstance(name, str) or not name or name in METADATA:
            raise ContractError(f"invalid {label} asset name")
        if not isinstance(digest, str) or re.fullmatch(r"[0-9a-f]{64}", digest) is None:
            raise ContractError(f"invalid {label} asset sha256: {name}")
        if isinstance(size, bool) or not isinstance(size, int) or size < 0:
            raise ContractError(f"invalid {label} asset bytes: {name}")
        if name in by_name:
            raise ContractError(f"duplicate {label} asset name: {name}")
        result.append(record)
        by_name[name] = record
    return result, by_name


def compare_set(label: str, expected: set[str], actual: set[str]) -> None:
    if actual != expected:
        missing, extra = sorted(expected - actual), sorted(actual - expected)
        raise ContractError(f"{label} asset set mismatch; missing={missing} extra={extra}")


def reject_duplicate_keys(pairs: list[tuple[str, object]]) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            raise ContractError(f"duplicate JSON key: {key}")
        result[key] = value
    return result


def load_json(path: Path, label: str) -> dict[str, object]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"),
                           object_pairs_hook=reject_duplicate_keys)
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ContractError(f"invalid {label}: {error}") from error
    return exact_keys(value, {"assets"} if label == "checksums.json" else
                      {"schema", "version", "commit", "assets"}, label)


def verify(version: str, commit: str, directory: str | Path, expected_rids: list[str]) -> None:
    validate_identity(version, commit)
    if len(expected_rids) != len(set(expected_rids)):
        raise ContractError("duplicate expected release RID")
    if not expected_rids:
        expected_rids = list(RID_SUFFIX)
    expected = {expected_name(version, rid) for rid in expected_rids}
    asset_dir = Path(directory)
    if asset_dir.is_symlink() or not asset_dir.is_dir():
        raise ContractError(f"asset directory must be a real directory: {asset_dir}")
    entries = list(asset_dir.iterdir())
    for path in entries:
        if path.is_symlink() or not path.is_file():
            raise ContractError(f"release asset entry must be a regular non-symlink file: {path.name}")
    disk = {path.name for path in entries if path.name not in METADATA}
    for name in METADATA:
        if name not in {path.name for path in entries}:
            raise ContractError(f"missing release asset metadata: {name}")

    manifest = load_json(asset_dir / "release-manifest.json", "release-manifest.json")
    checksums_json = load_json(asset_dir / "checksums.json", "checksums.json")
    if manifest["schema"] != SCHEMA or manifest["version"] != version or manifest["commit"] != commit:
        raise ContractError("release manifest identity mismatch")
    _, manifest_by_name = records(manifest["assets"], "manifest")
    _, json_by_name = records(checksums_json["assets"], "checksums JSON")

    text_by_name: dict[str, str] = {}
    for line in (asset_dir / "checksums.txt").read_text(encoding="utf-8").splitlines():
        match = CHECKSUM.fullmatch(line)
        if match is None:
            raise ContractError(f"invalid checksum line: {line!r}")
        digest, name = match.groups()
        if name in text_by_name:
            raise ContractError(f"duplicate checksum name: {name}")
        text_by_name[name] = digest

    for label, names in (("disk", disk), ("manifest", set(manifest_by_name)),
                         ("checksums JSON", set(json_by_name)), ("checksums text", set(text_by_name))):
        compare_set(label, expected, names)
    for name in sorted(expected):
        actual = asset_record(asset_dir / name)
        if manifest_by_name[name] != actual or json_by_name[name] != actual:
            raise ContractError(f"asset record mismatch: {name}")
        if text_by_name[name] != actual["sha256"]:
            raise ContractError(f"checksum mismatch: {name}")


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

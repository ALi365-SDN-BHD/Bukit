#!/usr/bin/env python3
from __future__ import annotations

import json
import os
import shutil
import sys
import tempfile
from pathlib import Path

from release_asset_contract import (
    METADATA,
    RID_SUFFIX,
    SCHEMA,
    ContractError,
    asset_record,
    checksum_records,
    compare_set,
    expected_name,
    metadata_records,
    require,
    validate_asset_directory,
    validate_asset_records,
    validate_identity,
)


def resolve_output(value: str) -> Path:
    require(value.strip() not in {"", ".", ".."}, "unsafe release output directory")
    output = Path(value).expanduser().absolute()
    resolved_output = output.resolve()
    require(resolved_output == output, "release output path must already be canonical")

    repo_root = Path(__file__).resolve().parents[2]
    require(
        output != Path(output.anchor) and not repo_root.is_relative_to(output),
        "unsafe release output directory",
    )
    require(not output.is_symlink(), "release output directory must not be a symlink")

    parent = output.parent
    require(
        parent.is_dir() and not parent.is_symlink() and parent.resolve() == parent,
        "release output parent must be an existing real directory",
    )
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
        require(
            not path.is_symlink() and path.is_file(),
            f"archive must be a regular non-symlink file: {value}",
        )
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


def verify(
    version: str,
    commit: str,
    directory: str | Path,
    expected_rids: list[str],
) -> None:
    validate_identity(version, commit)
    require(
        len(expected_rids) == len(set(expected_rids)),
        "duplicate expected release RID",
    )
    if not expected_rids:
        expected_rids = list(RID_SUFFIX)
    expected = {expected_name(version, rid) for rid in expected_rids}

    asset_dir, disk = validate_asset_directory(directory)
    manifest_by_name, json_by_name = metadata_records(asset_dir, version, commit)
    text_by_name = checksum_records(asset_dir / "checksums.txt")

    name_sets = (
        ("disk", disk),
        ("manifest", set(manifest_by_name)),
        ("checksums JSON", set(json_by_name)),
        ("checksums text", set(text_by_name)),
    )
    for label, names in name_sets:
        compare_set(label, expected, names)
    validate_asset_records(
        asset_dir,
        expected,
        manifest_by_name,
        json_by_name,
        text_by_name,
    )


def install_staging(staging: Path, output: Path) -> None:
    backup: Path | None = None
    if output.exists():
        backup = Path(
            tempfile.mkdtemp(prefix=f".{output.name}.backup.", dir=output.parent)
        )
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
        manifest = {
            "schema": SCHEMA,
            "version": version,
            "commit": commit,
            "assets": generated,
        }
        (staging / "release-manifest.json").write_text(
            json.dumps(manifest, indent=2) + "\n",
            encoding="utf-8",
        )
        (staging / "checksums.json").write_text(
            json.dumps({"assets": generated}, indent=2) + "\n",
            encoding="utf-8",
        )
        checksum_lines = "".join(
            f'{item["sha256"]}  {item["name"]}\n' for item in generated
        )
        (staging / "checksums.txt").write_text(checksum_lines, encoding="utf-8")
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
    too_few_args = (command == "prepare" and len(args) < 4) or (
        command == "verify" and len(args) < 3
    )
    if too_few_args:
        print(
            f"usage: release-assets.py {command} VERSION COMMIT PATH [ITEM...]",
            file=sys.stderr,
        )
        return 2

    try:
        handler = prepare if command == "prepare" else verify
        handler(args[0], args[1], args[2], args[3:])
    except (ContractError, OSError) as error:
        print(error, file=sys.stderr)
        return 1
    print(f"release assets {command} OK: {args[2]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))

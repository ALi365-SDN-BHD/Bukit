from __future__ import annotations

import hashlib
import json
import re
from pathlib import Path

RID_SUFFIX = {
    "linux-x64": ".tar.gz",
    "osx-arm64": ".tar.gz",
    "win-x64": ".zip",
}
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
    return {
        "name": path.name,
        "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        "bytes": path.stat().st_size,
    }


def exact_keys(value: object, keys: set[str], label: str) -> dict[str, object]:
    require(
        isinstance(value, dict) and set(value) == keys,
        f"{label} must have exact keys: {sorted(keys)}",
    )
    return value


def records(value: object, label: str) -> dict[str, dict[str, object]]:
    require(isinstance(value, list), f"{label} assets must be an array")
    by_name: dict[str, dict[str, object]] = {}
    for item in value:
        record = exact_keys(item, {"name", "sha256", "bytes"}, f"{label} asset")
        name = record["name"]
        digest = record["sha256"]
        size = record["bytes"]
        require(
            isinstance(name, str) and bool(name) and name not in METADATA,
            f"invalid {label} asset name",
        )
        require(
            isinstance(digest, str) and re.fullmatch(r"[0-9a-f]{64}", digest) is not None,
            f"invalid {label} asset sha256: {name}",
        )
        require(
            not isinstance(size, bool) and isinstance(size, int) and size >= 0,
            f"invalid {label} asset bytes: {name}",
        )
        require(name not in by_name, f"duplicate {label} asset name: {name}")
        by_name[name] = record
    return by_name


def compare_set(label: str, expected: set[str], actual: set[str]) -> None:
    if actual == expected:
        return
    missing = sorted(expected - actual)
    extra = sorted(actual - expected)
    raise ContractError(f"{label} asset set mismatch; missing={missing} extra={extra}")


def reject_duplicate_keys(pairs: list[tuple[str, object]]) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        require(key not in result, f"duplicate JSON key: {key}")
        result[key] = value
    return result


def load_json(path: Path, label: str) -> dict[str, object]:
    try:
        value = json.loads(
            path.read_text(encoding="utf-8"),
            object_pairs_hook=reject_duplicate_keys,
        )
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ContractError(f"invalid {label}: {error}") from error
    keys = (
        {"assets"}
        if label == "checksums.json"
        else {"schema", "version", "commit", "assets"}
    )
    return exact_keys(value, keys, label)


def validate_asset_directory(directory: str | Path) -> tuple[Path, set[str]]:
    asset_dir = Path(directory)
    require(
        not asset_dir.is_symlink() and asset_dir.is_dir(),
        f"asset directory must be a real directory: {asset_dir}",
    )
    entries = list(asset_dir.iterdir())
    for path in entries:
        require(
            not path.is_symlink() and path.is_file(),
            f"release asset entry must be a regular non-symlink file: {path.name}",
        )

    entry_names = {path.name for path in entries}
    for name in METADATA:
        require(name in entry_names, f"missing release asset metadata: {name}")
    return asset_dir, entry_names - METADATA


def metadata_records(
    asset_dir: Path,
    version: str,
    commit: str,
) -> tuple[dict[str, dict[str, object]], dict[str, dict[str, object]]]:
    manifest = load_json(asset_dir / "release-manifest.json", "release-manifest.json")
    checksums_json = load_json(asset_dir / "checksums.json", "checksums.json")
    require(
        manifest["schema"] == SCHEMA
        and manifest["version"] == version
        and manifest["commit"] == commit,
        "release manifest identity mismatch",
    )
    return (
        records(manifest["assets"], "manifest"),
        records(checksums_json["assets"], "checksums JSON"),
    )


def checksum_records(path: Path) -> dict[str, str]:
    by_name: dict[str, str] = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        match = CHECKSUM.fullmatch(line)
        require(match is not None, f"invalid checksum line: {line!r}")
        digest, name = match.groups()
        require(name not in by_name, f"duplicate checksum name: {name}")
        by_name[name] = digest
    return by_name


def validate_asset_records(
    asset_dir: Path,
    expected: set[str],
    manifest_by_name: dict[str, dict[str, object]],
    json_by_name: dict[str, dict[str, object]],
    text_by_name: dict[str, str],
) -> None:
    for name in sorted(expected):
        actual = asset_record(asset_dir / name)
        require(
            manifest_by_name[name] == actual and json_by_name[name] == actual,
            f"asset record mismatch: {name}",
        )
        require(text_by_name[name] == actual["sha256"], f"checksum mismatch: {name}")

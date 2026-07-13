#!/usr/bin/env python3
"""Safely extract one Bukit release archive into an isolated destination."""

from __future__ import annotations

import shutil
import stat
import sys
import tarfile
import unicodedata
import zipfile
from dataclasses import dataclass
from pathlib import Path, PurePosixPath, PureWindowsPath
from typing import BinaryIO, Callable, Iterable

@dataclass(frozen=True)
class ArchiveMember:
    relative: PurePosixPath
    is_directory: bool
    mode: int
    open_source: Callable[[], BinaryIO] | None

def existing_mode(path: Path) -> int | None:
    try:
        return path.lstat().st_mode
    except FileNotFoundError:
        return None

def safe_relative(name: str) -> PurePosixPath:
    has_control = any(unicodedata.category(character) == "Cc" for character in name)
    if not name or has_control or "\\" in name or PureWindowsPath(name).drive:
        raise ValueError(f"unsafe archive member: {name!r}")
    relative = PurePosixPath(name)
    if relative.is_absolute() or not relative.parts or ".." in relative.parts:
        raise ValueError(f"unsafe archive member: {name!r}")
    return relative

def validate_destination(destination: Path) -> None:
    for current in (destination, *destination.parents):
        mode = existing_mode(current)
        if mode is not None and not stat.S_ISDIR(mode):
            raise ValueError(f"unsafe extraction destination path: {current}")

def validate_members(members: Iterable[ArchiveMember], destination: Path) -> list[ArchiveMember]:
    validated = list(members)
    kinds: dict[PurePosixPath, bool] = {}
    for member in validated:
        if member.relative in kinds:
            raise ValueError(f"duplicate archive member: {member.relative}")
        kinds[member.relative] = member.is_directory

    for relative in kinds:
        for parent in relative.parents:
            if not parent.parts:
                break
            if parent in kinds and not kinds[parent]:
                raise ValueError(f"archive member has file parent: {relative}")
    validate_destination(destination)
    for member in validated:
        target = destination.joinpath(*member.relative.parts)
        if existing_mode(target) is not None:
            raise ValueError(f"refusing to overwrite extraction target: {target}")
        parent = target.parent
        while parent != destination:
            mode = existing_mode(parent)
            if mode is not None and not stat.S_ISDIR(mode):
                raise ValueError(f"unsafe extraction parent: {parent}")
            parent = parent.parent
    return validated

def ensure_directory(path: Path, destination: Path) -> None:
    missing: list[Path] = []
    current = path
    while current != destination:
        mode = existing_mode(current)
        if mode is None:
            missing.append(current)
        else:
            if not stat.S_ISDIR(mode):
                raise ValueError(f"unsafe extraction parent: {current}")
            break
        current = current.parent
    for directory in reversed(missing):
        directory.mkdir()

def extract_members(members: Iterable[ArchiveMember], destination: Path) -> None:
    destination.mkdir(parents=True, exist_ok=True)
    members = list(members)
    directory_modes: list[tuple[Path, int]] = []
    directories = sorted(
        (member for member in members if member.is_directory),
        key=lambda member: len(member.relative.parts),
    )
    for member in directories:
        target = destination.joinpath(*member.relative.parts)
        ensure_directory(target.parent, destination)
        target.mkdir()
        directory_modes.append((target, member.mode))
    for member in members:
        if member.is_directory:
            continue
        target = destination.joinpath(*member.relative.parts)
        ensure_directory(target.parent, destination)
        if member.open_source is None:
            raise ValueError(f"archive file cannot be opened: {member.relative}")
        with member.open_source() as source, target.open("xb") as output:
            shutil.copyfileobj(source, output)
        target.chmod(member.mode & 0o777)
    for target, mode in reversed(directory_modes):
        target.chmod(mode & 0o777)

def tar_members(archive: tarfile.TarFile) -> Iterable[ArchiveMember]:
    for member in archive.getmembers():
        if member.name in (".", "./"):
            if member.isdir():
                continue
            raise ValueError(f"unsafe tar root member: {member.name!r}")
        if not member.isdir() and not member.isreg():
            raise ValueError(f"unsupported tar member type: {member.name!r}")
        relative = safe_relative(member.name)
        if member.isdir():
            yield ArchiveMember(relative, True, member.mode, None)
        else:
            yield ArchiveMember(
                relative,
                False,
                member.mode,
                lambda member=member: require_source(archive.extractfile(member), member.name),
            )

def zip_members(archive: zipfile.ZipFile) -> Iterable[ArchiveMember]:
    for member in archive.infolist():
        relative = safe_relative(member.filename)
        mode = member.external_attr >> 16
        file_type = stat.S_IFMT(mode)
        if stat.S_ISLNK(mode):
            raise ValueError(f"unsupported zip symbolic link: {member.filename!r}")
        if member.flag_bits & 0x1:
            raise ValueError(f"encrypted zip member is unsupported: {member.filename!r}")
        if member.is_dir():
            if file_type not in (0, stat.S_IFDIR):
                raise ValueError(f"unsupported zip directory type: {member.filename!r}")
            yield ArchiveMember(relative, True, mode or 0o755, None)
        else:
            if file_type not in (0, stat.S_IFREG):
                raise ValueError(f"unsupported zip member type: {member.filename!r}")
            yield ArchiveMember(
                relative,
                False,
                mode,
                lambda member=member: archive.open(member, "r"),
            )

def require_source(source: BinaryIO | None, name: str) -> BinaryIO:
    if source is None:
        raise ValueError(f"archive file cannot be opened: {name!r}")
    return source

def extract_archive(archive_path: Path, rid: str, destination: Path) -> None:
    if not archive_path.is_file():
        raise ValueError(f"missing release archive: {archive_path}")
    if rid == "win-x64":
        if not archive_path.name.endswith(".zip"):
            raise ValueError(f"RID {rid} requires a .zip archive")
        with zipfile.ZipFile(archive_path, "r") as archive:
            members = validate_members(zip_members(archive), destination)
            extract_members(members, destination)
    elif rid in ("linux-x64", "osx-arm64"):
        if not archive_path.name.endswith(".tar.gz"):
            raise ValueError(f"RID {rid} requires a .tar.gz archive")
        with tarfile.open(archive_path, "r:gz") as archive:
            members = validate_members(tar_members(archive), destination)
            extract_members(members, destination)
    else:
        raise ValueError(f"unsupported RID: {rid}")

def main(argv: list[str]) -> int:
    if len(argv) != 4:
        print("usage: extract-release-artifact.py ARCHIVE RID DEST", file=sys.stderr)
        return 2
    try:
        extract_archive(Path(argv[1]), argv[2], Path(argv[3]))
    except (OSError, tarfile.TarError, ValueError, zipfile.BadZipFile) as error:
        print(f"release archive extraction failed: {error}", file=sys.stderr)
        return 1
    return 0

if __name__ == "__main__":
    raise SystemExit(main(sys.argv))

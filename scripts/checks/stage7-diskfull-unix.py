#!/usr/bin/env python3
"""Run the real disk-full specialty on a disposable, bounded Unix volume."""

import json
import os
from pathlib import Path
import platform
import plistlib
import shutil
import subprocess
import sys
import tempfile
import uuid
import xml.etree.ElementTree as ET


def validate_mount_facts(mounted, device, parent_device, total_bytes, identity_matches):
    if not mounted or device == parent_device or not 0 < total_bytes <= 64 * 1024 * 1024 or not identity_matches:
        raise ValueError("Not the bounded disposable volume created by this run")


def validate_trx(path):
    root = ET.parse(path).getroot()
    summary = root.find("{*}ResultSummary")
    counters = summary.find("{*}Counters")
    rows = root.findall("{*}Results/{*}UnitTestResult")
    prefix = "Bukit.Engine.Tests.Stage7DiskFullTests."
    expected = {"RealOutputVolumeFull_PreservesFormalOutputAndCache(clean: False)", "RealOutputVolumeFull_PreservesFormalOutputAndCache(clean: True)", "UnsafeVolume_IsRejectedBeforeAnyWrites"}
    units = {item.get("id"): item for item in root.findall("{*}TestDefinitions/{*}UnitTest")}
    if (summary.get("outcome") not in ("Completed", "Passed") or len(rows) != 3 or
            any(int(counters.get(key, "-1")) != 3 for key in ("total", "executed", "passed")) or
            any(int(counters.get(key, "0")) != 0 for key in ("failed", "error", "timeout", "aborted", "notExecuted")) or
            any(row.get("outcome") != "Passed" or "BUKIT_NOT_APPLICABLE:" in "".join(row.itertext()) for row in rows)):
        raise ValueError("Disk-full TRX has missing, failed, or skipped tests")
    names = []
    for row in rows:
        unit = units.get(row.get("testId"))
        method = unit.find("{*}TestMethod") if unit is not None else None
        if method is None or method.get("className", "").split(",")[0] != "Bukit.Engine.Tests.Stage7DiskFullTests":
            raise ValueError("Unexpected disk-full test class")
        name = unit.get("name", "")
        if not name.startswith(prefix):
            raise ValueError("Unexpected disk-full test name")
        names.append(name[len(prefix):])
    if sorted(names) != sorted(expected):
        raise ValueError("Unexpected disk-full test methods")


def main():
    system = platform.system()
    if system not in ("Linux", "Darwin") or os.environ.get("GITHUB_ACTIONS") != "true":
        raise RuntimeError("Disposable disk-full runner requires a GitHub Actions Unix runner")
    parent = Path(os.environ["RUNNER_TEMP"]).resolve(strict=True)
    work = Path(tempfile.mkdtemp(prefix="bukit-stage7-", dir=parent))
    mount = work / "mount"
    mount.mkdir()
    owner = uuid.uuid4().hex
    image = work / "volume.dmg"
    device = None
    attach_started = False
    mounted_by_us = False
    try:
        if system == "Darwin":
            subprocess.run(["hdiutil", "create", "-size", "32m", "-fs", "HFS+", "-type", "UDIF",
                            "-volname", "bukit-stage7", str(image)], check=True, stdout=subprocess.DEVNULL)
            attach_started = True
            attached = subprocess.run(["hdiutil", "attach", "-plist", "-nobrowse", "-noautoopen",
                                       "-mountpoint", str(mount), str(image)], check=True, capture_output=True)
            entities = plistlib.loads(attached.stdout)["system-entities"]
            matches = [item for item in entities if item.get("mount-point") == str(mount) and item.get("dev-entry")]
            if len(matches) != 1:
                raise RuntimeError("Cannot identify the attached image device")
            device = matches[0]["dev-entry"]
            mounted_by_us = True
            info = plistlib.loads(subprocess.check_output(["hdiutil", "info", "-plist"]))
            identity = any(item.get("image-path") == str(image) and any(
                entry.get("mount-point") == str(mount) and entry.get("dev-entry") == device
                for entry in item.get("system-entities", [])) for item in info["images"])
        else:
            label = "bukit-stage7-" + owner
            attach_started = True
            subprocess.run(["sudo", "-n", "mount", "-t", "tmpfs", "-o",
                            f"size=32m,mode=0700,uid={os.getuid()},gid={os.getgid()},nosuid,nodev,noexec",
                            label, str(mount)], check=True)
            mounted_by_us = True
            info = json.loads(subprocess.check_output(["findmnt", "--json", "--mountpoint", str(mount),
                                                       "--output", "TARGET,SOURCE,FSTYPE"]))
            entries = info.get("filesystems", [])
            identity = len(entries) == 1 and entries[0] == {"target": str(mount), "source": label, "fstype": "tmpfs"}

        stat = os.statvfs(mount)
        validate_mount_facts(os.path.ismount(mount), os.stat(mount).st_dev, os.stat(work).st_dev,
                             stat.f_blocks * stat.f_frsize, identity)
        with (mount / ".bukit-stage7-volume-owner").open("x", encoding="utf-8") as marker:
            marker.write(owner)
        env = os.environ.copy()
        env.update(TMPDIR=str(mount), BUKIT_STAGE7_DISK_VOLUME=str(mount), BUKIT_STAGE7_DISK_OWNER=owner,
                   DOTNET_CLI_UI_LANGUAGE="en")
        output = Path("TestResults/platform/Stage7DiskFull").resolve()
        output.mkdir(parents=True, exist_ok=True)
        trx = output / "diskfull.trx"
        trx.unlink(missing_ok=True)
        command = ["dotnet", "test", "tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj", "-c", "Release",
                   "--filter", "FullyQualifiedName~Stage7DiskFullTests", "--logger", "trx;LogFileName=diskfull.trx",
                   "--results-directory", str(output), "-p:BukitDiskFullTest=true"]
        print("Running bounded-volume specialty:", command, flush=True)
        subprocess.run(command, env=env, check=True, timeout=300)
        validate_trx(trx)
        print("Disk-full specialty: 3 passed; actual volume bytes:", stat.f_blocks * stat.f_frsize, flush=True)
    finally:
        if mounted_by_us:
            command = ["hdiutil", "detach", device] if system == "Darwin" else ["sudo", "-n", "umount", str(mount)]
            subprocess.run(command, check=True, timeout=60)
        elif attach_started:
            raise RuntimeError("Could not prove the attempted attachment was cleaned; retained at " + str(work))
        if os.path.ismount(mount):
            raise RuntimeError("Disposable volume remains mounted; retained at " + str(work))
        shutil.rmtree(work)


if __name__ == "__main__":
    if len(sys.argv) == 3 and sys.argv[1] == "--verify-trx":
        validate_trx(Path(sys.argv[2]))
    elif len(sys.argv) == 1:
        main()
    else:
        raise SystemExit("Usage: stage7-diskfull-unix.py [--verify-trx PATH]")

#!/usr/bin/env python3
"""Exercise disposable-volume safety checks without mounting or filling a disk."""

import importlib.util
from pathlib import Path
import tempfile
import xml.etree.ElementTree as ET

path = Path(__file__).with_name("stage7-diskfull-unix.py")
spec = importlib.util.spec_from_file_location("stage7_diskfull", path)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

valid = (True, 2, 1, 32 * 1024 * 1024, True)
module.validate_mount_facts(*valid)
for facts in ((False, 2, 1, valid[3], True), (True, 1, 1, valid[3], True),
              (True, 2, 1, 0, True), (True, 2, 1, 64 * 1024 * 1024 + 1, True),
              (True, 2, 1, valid[3], False)):
    try:
        module.validate_mount_facts(*facts)
    except ValueError:
        continue
    raise AssertionError("Unsafe mount was accepted: " + repr(facts))

with tempfile.TemporaryDirectory(prefix="stage7-disk-owner-") as temporary:
    trx = Path(temporary) / "test.trx"
    root = ET.Element("TestRun")
    definitions = ET.SubElement(root, "TestDefinitions")
    results = ET.SubElement(root, "Results")
    for index, name in enumerate(("RealOutputVolumeFull_PreservesFormalOutputAndCache(clean: False)", "RealOutputVolumeFull_PreservesFormalOutputAndCache(clean: True)", "UnsafeVolume_IsRejectedBeforeAnyWrites")):
        unit = ET.SubElement(definitions, "UnitTest", id=str(index), name="Bukit.Engine.Tests.Stage7DiskFullTests." + name)
        ET.SubElement(unit, "TestMethod", className="Bukit.Engine.Tests.Stage7DiskFullTests", name=name.split("(", 1)[0])
        ET.SubElement(results, "UnitTestResult", testId=str(index), outcome="Passed")
    summary = ET.SubElement(root, "ResultSummary", outcome="Completed")
    ET.SubElement(summary, "Counters", total="3", executed="3", passed="3")
    ET.ElementTree(root).write(trx)
    module.validate_trx(trx)
    for mutation in ("Failed", "NotExecuted"):
        results[0].set("outcome", mutation)
        ET.ElementTree(root).write(trx)
        try:
            module.validate_trx(trx)
        except ValueError:
            pass
        else:
            raise AssertionError("Invalid disk-full result was accepted: " + mutation)
        results[0].set("outcome", "Passed")
    results.remove(results[0])
    summary.find("{*}Counters").attrib.update(total="2", executed="2", passed="2")
    ET.ElementTree(root).write(trx)
    try:
        module.validate_trx(trx)
    except ValueError:
        pass
    else:
        raise AssertionError("Two-result disk-full TRX was accepted")
print("Stage 7 disk-full owner self-test passed")

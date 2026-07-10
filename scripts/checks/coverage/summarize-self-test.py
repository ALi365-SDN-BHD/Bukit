#!/usr/bin/env python3
from __future__ import annotations

import importlib.util
from pathlib import Path


script = Path(__file__).with_name("summarize.py")
spec = importlib.util.spec_from_file_location("bukit_coverage_summary", script)
if spec is None or spec.loader is None:
    raise RuntimeError("could not load coverage summarizer")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

assert module.below_threshold(83_995, 100_000, 84.0)
assert not module.below_threshold(84_000, 100_000, 84.0)
assert module.below_threshold(69_995, 100_000, 70.0)
assert not module.below_threshold(70_000, 100_000, 70.0)

print("coverage summary self-test OK")

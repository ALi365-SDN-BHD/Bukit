#!/usr/bin/env python3
"""Add new using statements next to old ones in all .cs files."""

import os
import re
import glob

ROOT = "/Users/ali/mydev/Git/Github/Bukit"

# Find all .cs files in src/ and tests/
cs_files = glob.glob(os.path.join(ROOT, "src/**/*.cs"), recursive=True)
cs_files += glob.glob(os.path.join(ROOT, "tests/**/*.cs"), recursive=True)

content_count = 0
routing_count = 0

for fpath in sorted(set(cs_files)):
    with open(fpath, "r") as f:
        content = f.read()

    original = content
    modified = False

    # Check if file has using Bukit.Content; but not using Bukit.Engine.Abstractions.Content;
    if "using Bukit.Content;" in content and "using Bukit.Engine.Abstractions.Content;" not in content:
        content = content.replace(
            "using Bukit.Content;",
            "using Bukit.Content;\nusing Bukit.Engine.Abstractions.Content;"
        )
        modified = True
        content_count += 1

    # Check if file has using Bukit.Routing; but not using Bukit.Engine.Abstractions.Routing;
    if "using Bukit.Routing;" in content and "using Bukit.Engine.Abstractions.Routing;" not in content:
        content = content.replace(
            "using Bukit.Routing;",
            "using Bukit.Routing;\nusing Bukit.Engine.Abstractions.Routing;"
        )
        modified = True
        routing_count += 1

    if modified:
        with open(fpath, "w") as f:
            f.write(content)
        print(f"  MODIFIED: {os.path.relpath(fpath, ROOT)}")

print(f"\nDone. Added using Bukit.Engine.Abstractions.Content to {content_count} files")
print(f"Added using Bukit.Engine.Abstractions.Routing to {routing_count} files")

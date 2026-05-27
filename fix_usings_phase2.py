#!/usr/bin/env python3
"""Phase 2: Comprehensive fix for remaining errors.

Issues:
1. Files in namespace Bukit.Content that don't have using Bukit.Content;
   but reference Abstractions types — they need the new using added.
2. Files where using Bukit.Content; causes CS0234 (project doesn't ref Bukit.Content.dll)
   — the old using should be replaced (new one already exists).
3. Files where using Bukit.Routing; causes CS0234 — same.
4. Files in namespace Bukit.Routing referencing RouteInfo — need new routing using.
"""

import os
import re
import glob

ROOT = "/Users/ali/mydev/Git/Github/Bukit"

cs_files = glob.glob(os.path.join(ROOT, "src/**/*.cs"), recursive=True)
cs_files += glob.glob(os.path.join(ROOT, "tests/**/*.cs"), recursive=True)

abstractions_types = [
    "ContentItem", "ContentBody", "ContentBodyResolver",
    "ContentField", "ContentLoadResult", "IContentBodyStore",
    "NullContentBodyStore", "TableOfContentsEntry"
]
abstractions_routing_types = ["RouteInfo"]

stats = {"add_content": 0, "replace_content": 0, "add_routing": 0, "replace_routing": 0}

for fpath in sorted(set(cs_files)):
    with open(fpath, "r") as f:
        content = f.read()

    original = content
    modified = False

    # Check namespace
    ns_match = re.search(r'^namespace\s+([\w.]+)\s*;', content, re.MULTILINE)
    ns = ns_match.group(1) if ns_match else ""

    is_content_ns = (ns == "Bukit.Content" or ns.startswith("Bukit.Content."))
    is_routing_ns = (ns == "Bukit.Routing" or ns.startswith("Bukit.Routing."))

    has_old_content_using = "using Bukit.Content;" in content
    has_new_content_using = "using Bukit.Engine.Abstractions.Content;" in content
    has_old_routing_using = "using Bukit.Routing;" in content
    has_new_routing_using = "using Bukit.Engine.Abstractions.Routing;" in content

    # Check if file references any Abstractions content types
    references_content_type = any(
        re.search(r'\b' + t + r'\b', content) for t in abstractions_types
    )
    references_route_info = bool(re.search(r'\bRouteInfo\b', content))

    # Case 1: File references content types, is NOT in Bukit.Content namespace,
    # has old using that's now stale -> remove old one (new one already exists)
    if references_content_type and not is_content_ns and has_old_content_using and has_new_content_using:
        content = content.replace("using Bukit.Content;\n", "")
        modified = True
        stats["replace_content"] += 1

    # Case 2: File references content types, is NOT in Bukit.Content namespace,
    # has old using, no new using -> replace old with new
    if references_content_type and not is_content_ns and has_old_content_using and not has_new_content_using:
        content = content.replace("using Bukit.Content;", "using Bukit.Engine.Abstractions.Content;")
        modified = True
        stats["replace_content"] += 1

    # Case 3: File references content types, is in Bukit.Content namespace,
    # doesn't have new using -> add it
    if references_content_type and is_content_ns and not has_new_content_using:
        # Add after any existing usings, or at top
        lines = content.split('\n')
        insert_at = 0
        for i, line in enumerate(lines):
            if line.strip().startswith("using "):
                insert_at = i + 1
            elif line.strip().startswith("namespace "):
                insert_at = i
                break
        lines.insert(insert_at, "using Bukit.Engine.Abstractions.Content;")
        content = '\n'.join(lines)
        modified = True
        stats["add_content"] += 1

    # Case 4: File references RouteInfo, is NOT in Bukit.Routing namespace,
    # has old using, has new using -> remove old
    if references_route_info and not is_routing_ns and has_old_routing_using and has_new_routing_using:
        content = content.replace("using Bukit.Routing;\n", "")
        modified = True
        stats["replace_routing"] += 1

    # Case 5: File references RouteInfo, is NOT in Bukit.Routing namespace,
    # has old using, no new -> replace
    if references_route_info and not is_routing_ns and has_old_routing_using and not has_new_routing_using:
        content = content.replace("using Bukit.Routing;", "using Bukit.Engine.Abstractions.Routing;")
        modified = True
        stats["replace_routing"] += 1

    # Case 6: File references RouteInfo, is in Bukit.Routing namespace,
    # doesn't have new using -> add
    if references_route_info and is_routing_ns and not has_new_routing_using:
        lines = content.split('\n')
        insert_at = 0
        for i, line in enumerate(lines):
            if line.strip().startswith("using "):
                insert_at = i + 1
            elif line.strip().startswith("namespace "):
                insert_at = i
                break
        lines.insert(insert_at, "using Bukit.Engine.Abstractions.Routing;")
        content = '\n'.join(lines)
        modified = True
        stats["add_routing"] += 1

    if modified:
        with open(fpath, "w") as f:
            f.write(content)
        print(f"  FIXED: {os.path.relpath(fpath, ROOT)}")

print(f"\nPhase 2 done:")
print(f"  Added Content using to {stats['add_content']} files (namespace Bukit.Content files)")
print(f"  Replaced old Content using in {stats['replace_content']} files (non-Bukit.Content namespace)")
print(f"  Added Routing using to {stats['add_routing']} files (namespace Bukit.Routing files)")
print(f"  Replaced old Routing using in {stats['replace_routing']} files (non-Bukit.Routing namespace)")

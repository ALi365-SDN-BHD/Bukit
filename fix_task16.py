#!/usr/bin/env python3
"""Add ["collection"] entries alongside ["type"] = "page"/"post" in test files."""

import re
import sys
from pathlib import Path

TESTS_DIR = Path("/Users/ali/mydev/Git/Github/Bukit/tests")

# Files to NEVER modify
SKIP_FILES = {
    "ScribanModelBinderTests.cs",
    "ScribanModelKnownFieldsTests.cs",
}

def needs_collection(file_path: Path) -> bool:
    """Check if file has ["type"] = "page" or "post" that might need collection."""
    if file_path.name in SKIP_FILES:
        return False
    content = file_path.read_text()
    return bool(re.search(r'\["type"]\s*=\s*"(?:page|post)"', content))

def has_collection(content: str) -> bool:
    """Check if content already has any ["collection"] entries."""
    return bool(re.search(r'\["collection"\]', content))

def add_collections(file_path: Path):
    """Process a single file, adding ["collection"] where missing."""
    content = file_path.read_text()
    original = content
    
    file_has_collection = has_collection(content)
    
    if not file_has_collection:
        # Simple global replacement for files with no collection at all
        content = re.sub(
            r'\["type"\]\s*=\s*"page"',
            r'["collection"] = "page", ["type"] = "page"',
            content
        )
        content = re.sub(
            r'\["type"\]\s*=\s*"post"',
            r'["collection"] = "post", ["type"] = "post"',
            content
        )
    else:
        # More careful: add ["collection"] before ["type"] if not already present nearby
        lines = content.split('\n')
        result_lines = []
        i = 0
        while i < len(lines):
            line = lines[i]
            
            # Check if this line has ["type"] = "page" or ["type"] = "post"
            m = re.search(r'\["type"\]\s*=\s*"(page|post)"', line)
            if m:
                # Look backwards (up to 5 lines) for ["collection"] in same block
                has_collection_nearby = False
                for j in range(max(0, i-5), i):
                    if '["collection"]' in lines[j]:
                        has_collection_nearby = True
                        break
                
                if not has_collection_nearby:
                    type_val = m.group(1)
                    indent = len(line) - len(line.lstrip())
                    # Insert collection line before this line
                    result_lines.append(' ' * indent + f'["collection"] = "{type_val}",')
            
            result_lines.append(line)
            i += 1
        
        content = '\n'.join(result_lines)
    
    if content != original:
        file_path.write_text(content)
        print(f"  MODIFIED: {file_path.relative_to(TESTS_DIR.parent)}")
        return True
    else:
        print(f"  skipped: {file_path.relative_to(TESTS_DIR.parent)}")
        return False

def main():
    print("Finding test files with [\"type\"] = \"page\" or \"post\"...")
    
    modified_count = 0
    for cs_file in sorted(TESTS_DIR.rglob("*.cs")):
        if not needs_collection(cs_file):
            continue
        
        print(f"\nProcessing: {cs_file.relative_to(TESTS_DIR.parent)}")
        if add_collections(cs_file):
            modified_count += 1
    
    print(f"\nDone. Modified {modified_count} file(s).")

if __name__ == "__main__":
    main()

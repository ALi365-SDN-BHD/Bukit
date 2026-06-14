#!/usr/bin/env bash
# Encoding check for Bukit source and documentation files.
# Scans UTF-8 text files for common mojibake (encoding corruption) patterns.
set -euo pipefail

extensions="\.(md|yaml|yml|json|html|scriban|cs|txt)$"
mojibake_patterns=("绠€" "浣撲" "鈫" "嘳")
exclude_dirs="obj|bin|.git|.codex-tmp|node_modules"
found_issues=0

check_file() {
    local f="$1"

    # Check UTF-8 validity using file command
    local encoding
    encoding=$(file --mime-encoding --brief "$f" 2>/dev/null || echo "unknown")
    if [ "$encoding" != "utf-8" ] && [ "$encoding" != "us-ascii" ] && [ "$encoding" != "ascii" ]; then
        echo "ENCODING: $f has non-UTF-8 encoding: $encoding"
        return 1
    fi

    # Check for mojibake patterns
    local line=0
    while IFS= read -r content_line; do
        line=$((line + 1))
        for pattern in "${mojibake_patterns[@]}"; do
            if echo "$content_line" | grep -qF "$pattern"; then
                echo "MOJIBAKE: $f:$line contains corrupted characters (pattern: $pattern)"
                echo "  Content: $content_line"
                return 1
            fi
        done
    done < "$f"

    return 0
}

while IFS= read -r -d '' f; do
    check_file "$f" || found_issues=$((found_issues + 1))
done < <(find . -type f -regextype posix-extended -regex ".*${extensions}" \
    -not -path '*/obj/*' \
    -not -path '*/bin/*' \
    -not -path '*/.git/*' \
    -not -path '*/.codex-tmp*/*' \
    -not -path '*/node_modules/*' \
    -not -path '*/.trae/*' \
    -print0 2>/dev/null)

if [ "$found_issues" -gt 0 ]; then
    echo ""
    echo "ERROR: $found_issues file(s) have encoding issues."
    echo "Fix them by re-saving affected files as UTF-8 (without BOM)."
    echo "Common cause: UTF-8 content decoded as GBK/CP936 and re-encoded."
    exit 1
fi

echo "Encoding check OK (all files valid UTF-8, no mojibake detected)."

#!/usr/bin/env bash
set -uo pipefail

# Smoke-all: builds every example site and validates output structure.
# Run from repo root: bash scripts/smoke-all.sh

configuration="${1:-Release}"
passed=0
failed=0
total=0

echo "=== Smoke Gold Checks ==="
echo ""

# Checks: dotnet run -- build must succeed. Then validate key output files.
check_smoke() {
    local name="$1"
    local config="$2"
    local output="$3"

    total=$((total + 1))
    echo -n "  $name ... "

    if dotnet run --project src/Bukit.Cli -c "$configuration" -- build --config "$config" --output "$output" --clean --site-url https://example.com >/dev/null 2>&1; then
        local ok=1

        if test -f "$output/sitemap.xml"; then
            grep -q '<url>' "$output/sitemap.xml" || { ok=0; echo -n " [sitemap:no <url>]"; }
        fi

        if test -f "$output/rss.xml"; then
            grep -q '<channel>' "$output/rss.xml" || { ok=0; echo -n " [rss:no <channel>]"; }
        fi

        if test -f "$output/search.json"; then
            python3 -m json.tool "$output/search.json" >/dev/null 2>&1 || { ok=0; echo -n " [search.json invalid]"; }
        fi

        if [ "$ok" -eq 1 ]; then
            echo "OK"
            passed=$((passed + 1))
        else
            echo ""
            echo "     FAILED"
            failed=$((failed + 1))
        fi
    else
        echo "BUILD FAILED"
        failed=$((failed + 1))
    fi
}

# Site-specific extra checks (called as separate asserts after basic checks pass)
assert_file() {
    local path="$1"
    local label="$2"
    test -f "$path" || { echo "     check $label: $path not found"; return 1; }
}

smoke_root=".smoke-all-run/$(date +%Y%m%d%H%M%S)-$$"

check_smoke "blog"            "examples/blog-site/site.yaml"            "$smoke_root/blog"
check_smoke "corporate"       "examples/corporate-site/site.yaml"       "$smoke_root/corporate"
check_smoke "docs"            "examples/docs-site/site.yaml"            "$smoke_root/docs"
check_smoke "plugin"          "examples/plugin-site/site.yaml"          "$smoke_root/plugin"
check_smoke "theme-inherit"   "examples/theme-inheritance-site/site.yaml" "$smoke_root/theme-inherit"
check_smoke "component-theme" "examples/component-theme/site.yaml"      "$smoke_root/component-theme"
check_smoke "multilingual"    "examples/multilingual-site/site.yaml"    "$smoke_root/multilingual"

echo ""
echo "=== Results: $passed passed, $failed failed (total: $total) ==="

rm -rf "$smoke_root"

if [ "$failed" -gt 0 ]; then
    exit 1
fi

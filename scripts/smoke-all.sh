#!/usr/bin/env bash
set -uo pipefail

# Smoke-all: builds every example site and fixture site, validates output structure.
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

# Fixture smoke check: builds fixture using its own build.output (dist), then validates dotfile/URL safety
check_fixture_smoke() {
    local name="$1"
    local config="$2"

    total=$((total + 1))
    echo -n "  $name ... "

    if dotnet run --project src/Bukit.Cli -c "$configuration" -- build --config "$config" --site-url https://example.com >/dev/null 2>&1; then
        local config_dir
        config_dir="$(dirname "$config")"
        local output="$config_dir/dist"
        local ok=1

        # index.html must exist (handle i18n subdirs)
        if test -f "$output/index.html"; then
            true
        elif test -f "$output/en/index.html"; then
            true
        else
            ok=0; echo -n " [index.html missing]"
        fi

        # dotfile leak check
        test ! -f "$output/.env" || { ok=0; echo -n " [.env leaked]"; }
        test ! -f "$output/.npmrc" || { ok=0; echo -n " [.npmrc leaked]"; }
        test ! -f "$output/.yarnrc" || { ok=0; echo -n " [.yarnrc leaked]"; }
        test ! -f "$output/private.key" || { ok=0; echo -n " [private.key leaked]"; }
        test ! -f "$output/cert.pfx" || { ok=0; echo -n " [cert.pfx leaked]"; }
        test ! -f "$output/cert.p12" || { ok=0; echo -n " [cert.p12 leaked]"; }
        test ! -d "$output/.git" || { ok=0; echo -n " [.git leaked]"; }

        # dangerous URL leak check
        if grep -qR "javascript:" "$output" 2>/dev/null; then
            ok=0; echo -n " [javascript: leak]"
        fi
        if grep -qR "data:text/html" "$output" 2>/dev/null; then
            ok=0; echo -n " [data: leak]"
        fi
        if grep -qR "file:///etc/passwd" "$output" 2>/dev/null; then
            ok=0; echo -n " [file:// leak]"
        fi
        if grep -qR "vbscript:" "$output" 2>/dev/null; then
            ok=0; echo -n " [vbscript: leak]"
        fi
        if grep -qR "//evil.com" "$output" 2>/dev/null; then
            ok=0; echo -n " [//evil.com leak]"
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

# Check that a build is expected to fail
check_fixture_must_fail() {
    local name="$1"
    local config="$2"
    local output="$3"

    total=$((total + 1))
    echo -n "  $name (must fail) ... "

    if dotnet run --project src/Bukit.Cli -c "$configuration" -- build --config "$config" --output "$output" --site-url https://example.com >/dev/null 2>&1; then
        echo "BUILD SUCCEEDED (expected failure)"
        failed=$((failed + 1))
    else
        echo "OK (failed as expected)"
        passed=$((passed + 1))
    fi
}

smoke_root=".smoke-all-run/$(date +%Y%m%d%H%M%S)-$$"

# Example sites
check_smoke "blog"            "examples/blog-site/site.yaml"            "$smoke_root/blog"
check_smoke "corporate"       "examples/corporate-site/site.yaml"       "$smoke_root/corporate"
check_smoke "docs"            "examples/docs-site/site.yaml"            "$smoke_root/docs"
check_smoke "plugin"          "examples/plugin-site/site.yaml"          "$smoke_root/plugin"
check_smoke "theme-inherit"   "examples/theme-inheritance-site/site.yaml" "$smoke_root/theme-inherit"
check_smoke "component-theme" "examples/component-theme/site.yaml"      "$smoke_root/component-theme"
check_smoke "multilingual"    "examples/multilingual-site/site.yaml"    "$smoke_root/multilingual"

echo ""
echo "=== Fixture Smoke Checks ==="
echo ""

# Fixture sites - successful builds
check_fixture_smoke "basic-markdown"     "tests/fixtures/basic-markdown-site/site.yaml"
check_fixture_smoke "safe-url-content"   "tests/fixtures/safe-url-content-site/site.yaml"
check_fixture_smoke "plugin-policy"      "tests/fixtures/plugin-policy-site/site.yaml"
check_fixture_smoke "output-safety"      "tests/fixtures/output-safety-site/site.yaml"
check_fixture_smoke "incremental"        "tests/fixtures/incremental-site/site.yaml"
check_fixture_smoke "i18n"               "tests/fixtures/i18n-site/site.yaml"
check_fixture_smoke "taxonomy"           "tests/fixtures/taxonomy-site/site.yaml"
check_fixture_smoke "component"          "tests/fixtures/component-validation-site/site.yaml"
check_fixture_smoke "dotfile-leak"       "tests/fixtures/dotfile-leak-site/site.yaml"

echo ""
echo "=== Expected-Failure Checks ==="
echo ""

# Incremental site: second build should also succeed
echo -n "  incremental-second-build ... "
if dotnet run --project src/Bukit.Cli -c "$configuration" -- build --config "tests/fixtures/incremental-site/site.yaml" --site-url https://example.com >/dev/null 2>&1; then
    echo "OK"
    passed=$((passed + 1))
else
    echo "FAILED"
    failed=$((failed + 1))
fi
total=$((total + 1))

echo ""
echo "=== Results: $passed passed, $failed failed (total: $total) ==="

rm -rf "$smoke_root"

if [ "$failed" -gt 0 ]; then
    exit 1
fi

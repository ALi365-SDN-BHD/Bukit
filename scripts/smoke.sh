#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"
smoke_root=".sitegen-smoke"
smoke_run="$smoke_root/$(date +%Y%m%d%H%M%S)-$$"

cleanup() {
  rm -rf "examples/starter/$smoke_run"
  rmdir "examples/starter/$smoke_root" 2>/dev/null || true
  rm -f "$intent_out"
}

intent_out="examples/starter/.sitegen-smoke-ai-$$.yaml"
trap cleanup EXIT

dotnet build bukit.slnx -c "$configuration"

dotnet run --project src/Bukit.Cli -c "$configuration" -- doctor --config examples/starter/site.yaml
dotnet run --project src/Bukit.Cli -c "$configuration" -- build --config examples/starter/site.yaml --output "$smoke_run/dist" --clean --site-url https://example.com --allow-external-plugins
dotnet run --project src/Bukit.Cli -c "$configuration" -- build --config examples/starter/site.i18n.merged.yaml --output "$smoke_run/dist_i18n_merged" --clean --site-url https://example.com --allow-external-plugins
dotnet run --project src/Bukit.Cli -c "$configuration" -- doctor --config examples/starter/site.modules.yaml
dotnet run --project src/Bukit.Cli -c "$configuration" -- build --config examples/starter/site.modules.yaml --output "$smoke_run/dist_modules" --clean --site-url https://example.com --allow-external-plugins

dotnet run --project src/Bukit.Cli -c "$configuration" -- build --config examples/starter/site.taxonomy.data.yaml --output "$smoke_run/dist_taxonomy_data" --clean --site-url https://example.com --allow-external-plugins
test -f "examples/starter/$smoke_run/dist_taxonomy_data/taxonomy.json"
test ! -f "examples/starter/$smoke_run/dist_taxonomy_data/tags/index.html"

dotnet run --project src/Bukit.Cli -c "$configuration" -- build --config examples/starter/site.taxonomy.disabled.yaml --output "$smoke_run/dist_taxonomy_disabled" --clean --site-url https://example.com --allow-external-plugins
test ! -f "examples/starter/$smoke_run/dist_taxonomy_disabled/taxonomy.json"
test ! -f "examples/starter/$smoke_run/dist_taxonomy_disabled/tags/index.html"

mkdir -p "$(dirname "$intent_out")"
dotnet run --project src/Bukit.Cli -c "$configuration" -- intent validate samples/intent/markdown_blog.yaml --out "$intent_out"
dotnet run --project src/Bukit.Cli -c "$configuration" -- intent apply samples/intent/markdown_blog.yaml --out "$intent_out"
test -f "$intent_out"
dotnet run --project src/Bukit.Cli -c "$configuration" -- doctor --config "$intent_out"
dotnet run --project src/Bukit.Cli -c "$configuration" -- build --config "$intent_out" --output "$smoke_run/dist_intent" --clean --site-url https://example.com --allow-external-plugins

(cd examples/starter && dotnet run --project ../../src/Bukit.Cli -c "$configuration" -- build --site blog --output "$smoke_run/dist_blog" --clean --site-url https://example.com --allow-external-plugins)

echo "Smoke OK"

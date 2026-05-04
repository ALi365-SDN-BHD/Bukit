#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"

dotnet build bukit.slnx -c "$configuration"

dotnet run --project src/Bukit.Cli -c "$configuration" -- doctor --config examples/starter/site.yaml
dotnet run --project src/Bukit.Cli -c "$configuration" -- build --config examples/starter/site.yaml --clean --site-url https://example.com
dotnet run --project src/Bukit.Cli -c "$configuration" -- build --config examples/starter/site.i18n.merged.yaml --clean --site-url https://example.com
dotnet run --project src/Bukit.Cli -c "$configuration" -- doctor --config examples/starter/site.modules.yaml
dotnet run --project src/Bukit.Cli -c "$configuration" -- build --config examples/starter/site.modules.yaml --clean --site-url https://example.com

dotnet run --project src/Bukit.Cli -c "$configuration" -- build --config examples/starter/site.taxonomy.data.yaml --clean --site-url https://example.com
test -f examples/starter/dist_taxonomy_data/taxonomy.json
test ! -f examples/starter/dist_taxonomy_data/tags/index.html

dotnet run --project src/Bukit.Cli -c "$configuration" -- build --config examples/starter/site.taxonomy.disabled.yaml --clean --site-url https://example.com
test ! -f examples/starter/dist_taxonomy_disabled/taxonomy.json
test ! -f examples/starter/dist_taxonomy_disabled/tags/index.html

intent_out="examples/starter/ai.site.yaml"
trap 'rm -f "$intent_out"' EXIT
dotnet run --project src/Bukit.Cli -c "$configuration" -- intent validate samples/intent/markdown_blog.yaml --out "$intent_out"
dotnet run --project src/Bukit.Cli -c "$configuration" -- intent apply samples/intent/markdown_blog.yaml --out "$intent_out"
dotnet run --project src/Bukit.Cli -c "$configuration" -- doctor --config "$intent_out"
dotnet run --project src/Bukit.Cli -c "$configuration" -- build --config "$intent_out" --clean --site-url https://example.com

(cd examples/starter && dotnet run --project ../../src/Bukit.Cli -c "$configuration" -- build --site blog --clean --site-url https://example.com)

echo "Smoke OK"

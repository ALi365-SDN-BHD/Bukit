param(
    [switch]$CleanGenerated
)

$ErrorActionPreference = "Stop"

$configuration = "Release"

dotnet build bukit.slnx -c $configuration

dotnet run --project src/Bukit.Cli -c $configuration -- doctor --config examples/starter/site.yaml
dotnet run --project src/Bukit.Cli -c $configuration -- build --config examples/starter/site.yaml --clean --site-url https://example.com
dotnet run --project src/Bukit.Cli -c $configuration -- build --config examples/starter/site.i18n.merged.yaml --clean --site-url https://example.com
dotnet run --project src/Bukit.Cli -c $configuration -- doctor --config examples/starter/site.modules.yaml
dotnet run --project src/Bukit.Cli -c $configuration -- build --config examples/starter/site.modules.yaml --clean --site-url https://example.com

dotnet run --project src/Bukit.Cli -c $configuration -- build --config examples/starter/site.taxonomy.data.yaml --clean --site-url https://example.com
if (!(Test-Path "examples/starter/dist_taxonomy_data/taxonomy.json")) { throw "taxonomy.json not found" }
if (Test-Path "examples/starter/dist_taxonomy_data/tags/index.html") { throw "taxonomy pages should not be generated in data mode" }

dotnet run --project src/Bukit.Cli -c $configuration -- build --config examples/starter/site.taxonomy.disabled.yaml --clean --site-url https://example.com
if (Test-Path "examples/starter/dist_taxonomy_disabled/taxonomy.json") { throw "taxonomy.json should not be generated when taxonomy plugin disabled" }
if (Test-Path "examples/starter/dist_taxonomy_disabled/tags/index.html") { throw "taxonomy pages should not be generated when taxonomy plugin disabled" }

$intentOut = "examples/starter/ai.site.yaml"
try
{
    dotnet run --project src/Bukit.Cli -c $configuration -- intent validate samples/intent/markdown_blog.yaml --out $intentOut
    dotnet run --project src/Bukit.Cli -c $configuration -- intent apply samples/intent/markdown_blog.yaml --out $intentOut
    dotnet run --project src/Bukit.Cli -c $configuration -- doctor --config $intentOut
    dotnet run --project src/Bukit.Cli -c $configuration -- build --config $intentOut --clean --site-url https://example.com
}
finally
{
    Remove-Item -Force -ErrorAction SilentlyContinue $intentOut
}

Push-Location examples/starter
try
{
    dotnet run --project ../../src/Bukit.Cli -c $configuration -- build --site blog --clean --site-url https://example.com
}
finally
{
    Pop-Location
}

Write-Output "Smoke OK"

if ($CleanGenerated) {
    $cleanScript = Join-Path $PSScriptRoot "clean-generated.ps1"
    if (Test-Path $cleanScript) {
        & $cleanScript
    }
}

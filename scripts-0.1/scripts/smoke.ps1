param(
    [switch]$CleanGenerated
)

$ErrorActionPreference = "Stop"

$configuration = "Release"
$smokeRoot = ".sitegen-smoke"
$smokeRun = Join-Path $smokeRoot ("{0}-{1}" -f (Get-Date -Format "yyyyMMddHHmmss"), $PID)
$intentOut = "examples/starter/.sitegen-smoke-ai-$PID.yaml"

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [scriptblock]$Command,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $Name"
    }
}

try
{
    Invoke-Checked { dotnet build bukit.slnx -c $configuration } "dotnet build"

    Invoke-Checked { dotnet run --project src/Bukit.Cli -c $configuration -- doctor --config examples/starter/site.yaml } "doctor examples/starter/site.yaml"
    Invoke-Checked { dotnet run --project src/Bukit.Cli -c $configuration -- build --config examples/starter/site.yaml --output "$smokeRun/dist" --clean --site-url https://example.com } "build examples/starter/site.yaml"
    Invoke-Checked { dotnet run --project src/Bukit.Cli -c $configuration -- build --config examples/starter/site.i18n.merged.yaml --output "$smokeRun/dist_i18n_merged" --clean --site-url https://example.com } "build examples/starter/site.i18n.merged.yaml"
    Invoke-Checked { dotnet run --project src/Bukit.Cli -c $configuration -- doctor --config examples/starter/site.modules.yaml } "doctor examples/starter/site.modules.yaml"
    Invoke-Checked { dotnet run --project src/Bukit.Cli -c $configuration -- build --config examples/starter/site.modules.yaml --output "$smokeRun/dist_modules" --clean --site-url https://example.com } "build examples/starter/site.modules.yaml"

    Invoke-Checked { dotnet run --project src/Bukit.Cli -c $configuration -- build --config examples/starter/site.taxonomy.data.yaml --output "$smokeRun/dist_taxonomy_data" --clean --site-url https://example.com } "build examples/starter/site.taxonomy.data.yaml"
    if (!(Test-Path "examples/starter/$smokeRun/dist_taxonomy_data/taxonomy.json")) { throw "taxonomy.json not found" }
    if (Test-Path "examples/starter/$smokeRun/dist_taxonomy_data/tags/index.html") { throw "taxonomy pages should not be generated in data mode" }

    Invoke-Checked { dotnet run --project src/Bukit.Cli -c $configuration -- build --config examples/starter/site.taxonomy.disabled.yaml --output "$smokeRun/dist_taxonomy_disabled" --clean --site-url https://example.com } "build examples/starter/site.taxonomy.disabled.yaml"
    if (Test-Path "examples/starter/$smokeRun/dist_taxonomy_disabled/taxonomy.json") { throw "taxonomy.json should not be generated when taxonomy plugin disabled" }
    if (Test-Path "examples/starter/$smokeRun/dist_taxonomy_disabled/tags/index.html") { throw "taxonomy pages should not be generated when taxonomy plugin disabled" }

try
{
    Invoke-Checked { dotnet run --project src/Bukit.Cli -c $configuration -- intent validate samples/intent/markdown_blog.yaml --out $intentOut } "intent validate"
    Invoke-Checked { dotnet run --project src/Bukit.Cli -c $configuration -- intent apply samples/intent/markdown_blog.yaml --out $intentOut } "intent apply"
    Invoke-Checked { dotnet run --project src/Bukit.Cli -c $configuration -- doctor --config $intentOut } "doctor generated intent config"
    Invoke-Checked { dotnet run --project src/Bukit.Cli -c $configuration -- build --config $intentOut --output "$smokeRun/dist_intent" --clean --site-url https://example.com } "build generated intent config"
}
finally
{
    Remove-Item -Force -ErrorAction SilentlyContinue $intentOut
}

Push-Location examples/starter
try
{
    Invoke-Checked { dotnet run --project ../../src/Bukit.Cli -c $configuration -- build --site blog --output "$smokeRun/dist_blog" --clean --site-url https://example.com } "build --site blog"
}
finally
{
    Pop-Location
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "examples/starter/$smokeRun"
}

    Write-Output "Smoke OK"

    if ($CleanGenerated) {
        $cleanScript = Join-Path $PSScriptRoot "clean-generated.ps1"
        if (Test-Path $cleanScript) {
            & $cleanScript
        }
    }
}
finally
{
    Remove-Item -Force -ErrorAction SilentlyContinue $intentOut
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "examples/starter/$smokeRun"
    Remove-Item -Force -ErrorAction SilentlyContinue "examples/starter/$smokeRoot"
}

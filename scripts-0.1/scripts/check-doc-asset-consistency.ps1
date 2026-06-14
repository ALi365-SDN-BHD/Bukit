param(
    [string[]]$ExtraPath = @()
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root
$rootPath = $root.Path

$errorCount = 0
$warningCount = 0

$rules = @(
    "src/BukitJalil",
    "BukitJalil.slnx",
    "tools/ImageSharp",
    ".github/workflows/smoke.yml",
    ".github/workflows/build.yaml"
)

$allowKeywords = @(
    "`u793a`u4f8b",
    "`u9700`u81ea`u5efa",
    "`u53c2`u8003",
    "`u81ea`u884c`u521b`u5efa",
    "`u81ea`u884c`u5728",
    "`u81ea`u5efa",
    "example",
    "examples",
    "reference",
    "create your own",
    "rg -n"
)

$requiredPaths = @(
    "bukit.slnx",
    "guide/dev",
    "guide/user"
)

function Write-DocError([string]$Message) {
    $script:errorCount++
    Write-Output "ERROR: $Message"
}

function Write-DocWarn([string]$Message) {
    $script:warningCount++
    Write-Output "WARN: $Message"
}

function IsAllowedContext([string]$Line) {
    $lineLower = $Line.ToLowerInvariant()
    foreach ($keyword in $allowKeywords) {
        if ($lineLower.Contains($keyword.ToLowerInvariant())) {
            return $true
        }
    }

    return $false
}

$scanFiles = New-Object System.Collections.Generic.List[string]
Get-ChildItem -Path "." -File -Filter "README*.md" | ForEach-Object { [void]$scanFiles.Add($_.FullName) }
Get-ChildItem -Path "guide" -Recurse -File -Filter "*.md" | ForEach-Object { [void]$scanFiles.Add($_.FullName) }

$dedup = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
$targetFiles = @()
foreach ($file in $scanFiles) {
    if ($dedup.Add($file)) {
        $targetFiles += $file
    }
}

foreach ($file in $targetFiles) {
    $lines = Get-Content -Path $file -Encoding UTF8
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        foreach ($token in $rules) {
            if ($line.IndexOf($token, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                $relative = $file
                if ($relative.StartsWith($rootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
                    $relative = $relative.Substring($rootPath.Length).TrimStart('\', '/')
                }
                $relative = $relative.Replace('\', '/')
                $lineNumber = $i + 1
                if (IsAllowedContext $line) {
                    Write-DocWarn "${relative}:$lineNumber matched '$token' but exempted by example/reference context"
                } else {
                    Write-DocError "${relative}:$lineNumber matched '$token' and may be stale assertion"
                }
            }
        }
    }
}

$allPaths = @($requiredPaths + $ExtraPath)
foreach ($path in $allPaths) {
    if (-not (Test-Path $path)) {
        Write-DocError "missing path: $path"
    }
}

$pagesDoc = Get-ChildItem -Path "guide/user" -File -Filter "*GitHub-Pages.md" -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $pagesDoc) {
    Write-DocError "missing pages deployment guide under guide/user/*GitHub-Pages.md"
}

if ($errorCount -gt 0) {
    Write-Output "ERROR: doc-asset consistency check failed, errors=$errorCount warnings=$warningCount"
    exit 1
}

Write-Output "OK doc-asset consistency check passed, errors=0 warnings=$warningCount"
exit 0

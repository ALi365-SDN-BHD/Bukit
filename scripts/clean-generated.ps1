param(
    [switch]$DryRun
)

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $root

$patterns = @(
    '^examples/starter/\.cache/',
    '^examples/starter/dist',
    '/bin/',
    '/obj/',
    '^plugins/.*\.pdb$'
)

function IsGeneratedPath([string]$path) {
    $normalized = $path.Replace('\', '/')
    foreach ($pattern in $patterns) {
        if ($normalized -match $pattern) {
            return $true
        }
    }

    return $false
}

$statusLines = git status --porcelain=v1
if (-not $statusLines) {
    Write-Output "working tree clean"
    exit 0
}

$tracked = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
$untracked = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)

foreach ($line in $statusLines) {
    if ([string]::IsNullOrWhiteSpace($line)) {
        continue
    }

    $state = $line.Substring(0, 2)
    $pathPart = $line.Substring(3).Trim()
    if ($pathPart.Contains('->')) {
        $pathPart = $pathPart.Split('->')[1].Trim()
    }

    if (-not (IsGeneratedPath $pathPart)) {
        continue
    }

    if ($state -eq '??') {
        [void]$untracked.Add($pathPart)
    } else {
        [void]$tracked.Add($pathPart)
    }
}

if ($tracked.Count -eq 0 -and $untracked.Count -eq 0) {
    Write-Output "no generated artifacts to clean"
    exit 0
}

foreach ($path in $tracked) {
    if ($DryRun) {
        Write-Output "restore: $path"
    } else {
        git restore --worktree --source=HEAD -- "$path" | Out-Null
        Write-Output "restored: $path"
    }
}

foreach ($path in $untracked) {
    if ($DryRun) {
        Write-Output "delete: $path"
    } else {
        $fullPath = Join-Path $root $path
        if (Test-Path $fullPath) {
            Remove-Item -Recurse -Force $fullPath
            Write-Output "deleted: $path"
        }
    }
}

Write-Output "done"

# Encoding check for Bukit source and documentation files.
# Scans UTF-8 text files for common mojibake (encoding corruption) patterns.

$extensions = @('*.md', '*.yaml', '*.yml', '*.json', '*.html', '*.scriban', '*.cs', '*.txt')
$mojibakePatterns = @('绠€', '浣撲', '鈫', '嘳')
$excludeDirs = @('obj', 'bin', '.git', '.codex-tmp','node_modules')
$foundIssues = 0

function Test-FileEncoding {
    param([string]$path)

    $bytes = [System.IO.File]::ReadAllBytes($path)

    # Check for UTF-16 BOM
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
        Write-Host "ENCODING: $path has UTF-16 LE encoding" -ForegroundColor Red
        return $false
    }
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) {
        Write-Host "ENCODING: $path has UTF-16 BE encoding" -ForegroundColor Red
        return $false
    }

    # Try to decode as UTF-8
    try {
        $text = [System.Text.Encoding]::UTF8.GetString($bytes)
        # Re-encode and compare to detect invalid UTF-8 sequences
        $reEncoded = [System.Text.Encoding]::UTF8.GetBytes($text)
        if ($reEncoded.Length -ne $bytes.Length) {
            $isAscii = -not ($bytes | Where-Object { $_ -gt 127 })
            if (-not $isAscii) {
                Write-Host "ENCODING: $path has non-UTF-8 content (byte mismatch)" -ForegroundColor Red
                return $false
            }
        }
    } catch {
        Write-Host "ENCODING: $path cannot be decoded as UTF-8: $_" -ForegroundColor Red
        return $false
    }

    # Check for mojibake patterns
    $lines = $text -split "`n"
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $lineNum = $i + 1
        foreach ($pattern in $mojibakePatterns) {
            if ($lines[$i] -match [regex]::Escape($pattern)) {
                Write-Host "MOJIBAKE: $path($lineNum) contains corrupted characters (pattern: $pattern)" -ForegroundColor Red
                Write-Host "  Content: $($lines[$i].Trim())" -ForegroundColor DarkYellow
                return $false
            }
        }
    }

    return $true
}

Get-ChildItem -Recurse -File | Where-Object {
    $ext = $_.Extension.ToLowerInvariant()
    $included = $false
    foreach ($e in $extensions) {
        if ($_.Name -like $e) { $included = $true; break }
    }
    if (-not $included) { return $false }

    $dir = $_.DirectoryName
    foreach ($ex in $excludeDirs) {
        if ($dir -match [regex]::Escape($ex)) { return $false }
    }
    return $true
} | ForEach-Object {
    if (-not (Test-FileEncoding $_.FullName)) {
        $script:foundIssues++
    }
}

if ($foundIssues -gt 0) {
    Write-Host ""
    Write-Host "ERROR: $foundIssues file(s) have encoding issues." -ForegroundColor Red
    Write-Host "Fix them by re-saving affected files as UTF-8 (without BOM)." -ForegroundColor Yellow
    Write-Host "Common cause: UTF-8 content decoded as GBK/CP936 and re-encoded." -ForegroundColor Yellow
    exit 1
}

Write-Host "Encoding check OK (all files valid UTF-8, no mojibake detected)." -ForegroundColor Green

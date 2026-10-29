param([switch]$SelfTest)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-SafeImageDisk($ImagePath, $Image, $Disks, $OriginalNumbers, $Style) {
    if ($null -eq $Image -or -not $Image.Attached -or
        [IO.Path]::GetFullPath($Image.ImagePath) -ine [IO.Path]::GetFullPath($ImagePath) -or @($Disks).Count -ne 1) {
        throw 'The new VHD does not identify exactly one attached disk.'
    }
    $disk = @($Disks)[0]
    if ($disk.Number -eq 0 -or $OriginalNumbers -contains $disk.Number -or
        $disk.IsSystem -ne $false -or $disk.IsBoot -ne $false -or $disk.IsOffline -ne $false -or
        $disk.IsReadOnly -ne $false -or $disk.Size -ne 64MB -or $disk.PartitionStyle -ne $Style) {
        throw 'VHD disk failed the non-system, newly attached, capacity, or partition-style checks.'
    }
    return $disk
}

if ($SelfTest) {
    $path = 'C:\stage7-selftest.vhd'
    $image = [pscustomobject]@{ Attached = $true; ImagePath = $path }
    $disk = [pscustomobject]@{ Number = 9; IsSystem = $false; IsBoot = $false; IsOffline = $false; IsReadOnly = $false; Size = 64MB; PartitionStyle = 'RAW' }
    $null = Assert-SafeImageDisk $path $image @($disk) @(0, 1) 'RAW'
    foreach ($property in @('IsSystem', 'IsBoot', 'IsOffline', 'IsReadOnly')) {
        $disk.$property = $true
        try { $null = Assert-SafeImageDisk $path $image @($disk) @(0, 1) 'RAW'; throw 'Unsafe disk accepted.' }
        catch { if ($_.Exception.Message -eq 'Unsafe disk accepted.') { throw } }
        $disk.$property = $false
    }
    foreach ($case in @('existing', 'wrong-size', 'wrong-style', 'wrong-image', 'detached', 'multiple')) {
        $other = [pscustomobject]@{ Number = 9; IsSystem = $false; IsBoot = $false; IsOffline = $false; IsReadOnly = $false; Size = 64MB; PartitionStyle = 'RAW' }
        $candidate = [pscustomobject]@{ Attached = $true; ImagePath = $path }
        $before = @(0, 1); $disks = @($other)
        switch ($case) {
            'existing' { $before = @(0, 9) }
            'wrong-size' { $other.Size = 65MB }
            'wrong-style' { $other.PartitionStyle = 'GPT' }
            'wrong-image' { $candidate.ImagePath = 'C:\other.vhd' }
            'detached' { $candidate.Attached = $false }
            'multiple' { $disks = @($other, $other) }
        }
        try { $null = Assert-SafeImageDisk $path $candidate $disks $before 'RAW'; throw 'Unsafe disk accepted.' }
        catch { if ($_.Exception.Message -eq 'Unsafe disk accepted.') { throw } }
    }
    Write-Host 'Windows VHD safety self-test passed'
    return
}

if ($env:GITHUB_ACTIONS -ne 'true' -or -not $env:RUNNER_TEMP) { throw 'A GitHub Actions runner is required.' }
$owner = [guid]::NewGuid().ToString('N')
$work = Join-Path ([IO.Path]::GetFullPath($env:RUNNER_TEMP)) ("bukit-stage7-" + $owner)
if (Test-Path -LiteralPath $work) { throw 'Disposable directory already exists.' }
$null = New-Item -ItemType Directory -Path $work
$imagePath = Join-Path $work 'volume.vhd'
$createPath = Join-Path $work 'create.txt'
if (Test-Path -LiteralPath $imagePath) { throw 'VHD already exists.' }
$attachedAttempted = $false
try {
    [IO.File]::WriteAllText($createPath, "create vdisk file=`"$imagePath`" maximum=64 type=expandable`r`n", [Text.Encoding]::ASCII)
    & diskpart.exe /s $createPath
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $imagePath -PathType Leaf)) { throw 'Could not create the new VHD.' }
    $before = @(Get-Disk | ForEach-Object { $_.Number })
    $attachedAttempted = $true
    Mount-DiskImage -ImagePath $imagePath -StorageType VHD -Access ReadWrite -NoDriveLetter | Out-Null
    $image = Get-DiskImage -ImagePath $imagePath -StorageType VHD
    $disk = Assert-SafeImageDisk $imagePath $image @($image | Get-Disk) $before 'RAW'
    Initialize-Disk -InputObject $disk -PartitionStyle MBR | Out-Null
    $image = Get-DiskImage -ImagePath $imagePath -StorageType VHD
    $disk = Assert-SafeImageDisk $imagePath $image @($image | Get-Disk) $before 'MBR'
    $partition = New-Partition -InputObject $disk -UseMaximumSize -AssignDriveLetter
    $partitions = @($image | Get-Disk | Get-Partition)
    if ($partition.DiskNumber -ne $disk.Number -or -not $partition.DriveLetter -or
        @($partitions | Where-Object { $_.DiskNumber -eq $partition.DiskNumber -and $_.PartitionNumber -eq $partition.PartitionNumber }).Count -ne 1) {
        throw 'New partition does not belong to the verified VHD.'
    }
    Format-Volume -Partition $partition -FileSystem NTFS -NewFileSystemLabel 'bukit-stage7' -Confirm:$false | Out-Null
    $volume = Get-Volume -Partition $partition
    if ($volume.FileSystem -ne 'NTFS' -or $volume.DriveLetter -ne $partition.DriveLetter -or $volume.Size -le 0 -or $volume.Size -gt 64MB) {
        throw 'Formatted VHD volume failed verification.'
    }
    $root = "$($partition.DriveLetter):\"
    [IO.File]::WriteAllText((Join-Path $root '.bukit-stage7-volume-owner'), $owner, [Text.Encoding]::ASCII)
    $env:TMP = $root; $env:TEMP = $root
    $env:BUKIT_STAGE7_DISK_VOLUME = $root; $env:BUKIT_STAGE7_DISK_OWNER = $owner
    $env:DOTNET_CLI_UI_LANGUAGE = 'en'
    $output = [IO.Path]::GetFullPath('TestResults/platform/Stage7DiskFull')
    $null = New-Item -ItemType Directory -Force -Path $output
    $trx = Join-Path $output 'diskfull.trx'
    if (Test-Path -LiteralPath $trx) { Remove-Item -LiteralPath $trx }
    & dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter 'FullyQualifiedName~Stage7DiskFullTests' --logger 'trx;LogFileName=diskfull.trx' --results-directory $output -p:BukitDiskFullTest=true
    if ($LASTEXITCODE -ne 0) { throw 'Disk-full test command failed.' }
    & python -B scripts/checks/stage7-diskfull-unix.py --verify-trx $trx
    if ($LASTEXITCODE -ne 0) { throw 'Disk-full TRX verification failed.' }
    Write-Host "Disk-full specialty: 2 passed; VHD volume bytes: $($volume.Size)"
}
finally {
    if ($attachedAttempted) {
        $image = Get-DiskImage -ImagePath $imagePath -StorageType VHD
        if ($image.Attached) { Dismount-DiskImage -ImagePath $imagePath -StorageType VHD | Out-Null }
        if ((Get-DiskImage -ImagePath $imagePath -StorageType VHD).Attached) { throw "VHD still attached: $imagePath" }
        Write-Host 'New VHD safely dismounted.'
    }
}

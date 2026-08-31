[CmdletBinding()]
param(
    [ValidateSet('Snapshot', 'Worktree', 'Heavy')]
    [string] $Admission = 'Snapshot',
    [string] $RepositoryRoot,
    [string] $ConfigPath,
    [ValidateRange(0, [int]::MaxValue)]
    [int] $ActiveHeavyOperations = 0,
    [hashtable] $Sample,
    [switch] $AsJson
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = [System.IO.Path]::GetFullPath(
        (Join-Path $PSScriptRoot '../../../..'))
}
else {
    $RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
}

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $PSScriptRoot '../resources/resource-policy.json'
}
else {
    $ConfigPath = [System.IO.Path]::GetFullPath($ConfigPath)
}

if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) {
    throw "Orchestration resource policy does not exist: $ConfigPath"
}

$policy = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
if ($policy.schemaVersion -ne 2) {
    throw "Unsupported orchestration resource policy schema: $($policy.schemaVersion)"
}

$maximumLinkedTaskWorktrees = [int] $policy.worktree.maximumLinkedTaskWorktrees
$reservedGiBPerNewWorktree = [double] $policy.worktree.reservedGiBPerNewWorktree
$minimumFreeGiBAfterReservation = [double] $policy.worktree.minimumFreeGiBAfterReservation
$minimumFreeDiskPercentWarning = [double] $policy.worktree.minimumFreeDiskPercentWarning
$heavyLimit = [int] $policy.heavyOperation.concurrentLimit
$minimumAvailableMemoryGiB = [double] $policy.heavyOperation.minimumAvailableMemoryGiB
$warningAvailableMemoryGiB = [double] $policy.heavyOperation.warningAvailableMemoryGiB

if (
    $maximumLinkedTaskWorktrees -lt 1 -or
    $reservedGiBPerNewWorktree -le 0 -or
    $minimumFreeGiBAfterReservation -le 0 -or
    $minimumFreeDiskPercentWarning -le 0 -or
    $heavyLimit -lt 1 -or
    $minimumAvailableMemoryGiB -le 0 -or
    $warningAvailableMemoryGiB -lt $minimumAvailableMemoryGiB) {
    throw 'The orchestration resource policy contains invalid limits.'
}

$useSyntheticSample = $null -ne $Sample

function Get-SyntheticValue {
    param([Parameter(Mandatory)][string] $Name)

    if ($useSyntheticSample) {
        if ($Sample.ContainsKey($Name)) {
            return $Sample[$Name]
        }

        return $null
    }

    return '__measure__'
}

function Get-AvailableMemoryGiB {
    try {
        Add-Type -AssemblyName Microsoft.VisualBasic
        return [Math]::Round(
            ([Microsoft.VisualBasic.Devices.ComputerInfo]::new().AvailablePhysicalMemory / 1GB),
            2)
    }
    catch {
        try {
            $operatingSystem = Get-CimInstance -ClassName Win32_OperatingSystem
            return [Math]::Round(($operatingSystem.FreePhysicalMemory * 1KB / 1GB), 2)
        }
        catch {
            return $null
        }
    }
}

$freeDiskGiB = Get-SyntheticValue -Name 'FreeDiskGiB'
$totalDiskGiB = Get-SyntheticValue -Name 'TotalDiskGiB'
$availableMemoryGiB = Get-SyntheticValue -Name 'AvailableMemoryGiB'
$logicalProcessors = Get-SyntheticValue -Name 'LogicalProcessors'
$linkedTaskWorktrees = Get-SyntheticValue -Name 'LinkedTaskWorktrees'

if (-not $useSyntheticSample) {
    try {
        $drive = [System.IO.DriveInfo]::new([System.IO.Path]::GetPathRoot($RepositoryRoot))
        $freeDiskGiB = [Math]::Round(($drive.AvailableFreeSpace / 1GB), 2)
        $totalDiskGiB = [Math]::Round(($drive.TotalSize / 1GB), 2)
    }
    catch {
        $freeDiskGiB = $null
        $totalDiskGiB = $null
    }

    $availableMemoryGiB = Get-AvailableMemoryGiB
    $logicalProcessors = [System.Environment]::ProcessorCount

    $worktreeLines = & git -C $RepositoryRoot worktree list --porcelain 2>$null
    if ($LASTEXITCODE -eq 0) {
        $totalWorktrees = @($worktreeLines | Where-Object { $_ -like 'worktree *' }).Count
        $linkedTaskWorktrees = [Math]::Max(0, $totalWorktrees - 1)
    }
    else {
        $linkedTaskWorktrees = $null
    }
}

$freeDiskPercent = $null
if ($null -ne $freeDiskGiB -and $null -ne $totalDiskGiB -and [double] $totalDiskGiB -gt 0) {
    $freeDiskPercent = [Math]::Round(([double] $freeDiskGiB / [double] $totalDiskGiB * 100), 1)
}

$warnings = [System.Collections.Generic.List[string]]::new()
if ($null -ne $freeDiskPercent -and $freeDiskPercent -lt $minimumFreeDiskPercentWarning) {
    $warnings.Add(
        "Disk free space is $freeDiskPercent%, below the $minimumFreeDiskPercentWarning% warning threshold.")
}
if (
    $null -ne $availableMemoryGiB -and
    [double] $availableMemoryGiB -lt $warningAvailableMemoryGiB) {
    $warnings.Add(
        "Available memory is $availableMemoryGiB GiB, below the $warningAvailableMemoryGiB GiB warning threshold; the hard floor is $minimumAvailableMemoryGiB GiB.")
}

$worktreeAllowed = $false
$worktreeReason = ''
$postReservationFreeGiB = $null
if ($null -eq $freeDiskGiB -or $null -eq $linkedTaskWorktrees) {
    $worktreeReason = 'Denied: disk or linked-worktree measurements are unavailable.'
}
elseif ([int] $linkedTaskWorktrees -ge $maximumLinkedTaskWorktrees) {
    $worktreeReason = "Denied: $linkedTaskWorktrees linked task worktrees already meet the limit of $maximumLinkedTaskWorktrees."
}
else {
    $postReservationFreeGiB = [Math]::Round(([double] $freeDiskGiB - $reservedGiBPerNewWorktree), 2)
    if ($postReservationFreeGiB -lt $minimumFreeGiBAfterReservation) {
        $worktreeReason = "Denied: reserving $reservedGiBPerNewWorktree GiB would leave $postReservationFreeGiB GiB, below the $minimumFreeGiBAfterReservation GiB floor."
    }
    else {
        $worktreeAllowed = $true
        $worktreeReason = "Allowed: reservation leaves $postReservationFreeGiB GiB and uses $linkedTaskWorktrees of $maximumLinkedTaskWorktrees linked task-worktree slots."
    }
}

$heavyAllowed = $false
$heavyReason = ''
if ($null -eq $availableMemoryGiB -or $null -eq $logicalProcessors) {
    $heavyReason = 'Denied: available-memory or logical-processor measurements are unavailable.'
}
elseif ([double] $availableMemoryGiB -lt $minimumAvailableMemoryGiB) {
    $heavyReason = "Denied: $availableMemoryGiB GiB available memory is below the $minimumAvailableMemoryGiB GiB floor."
}
elseif ($ActiveHeavyOperations -ge $heavyLimit) {
    $heavyReason = "Denied: $ActiveHeavyOperations active heavy operations meet the current limit of $heavyLimit."
}
else {
    $heavyAllowed = $true
    $heavyReason = "Allowed: $ActiveHeavyOperations of $heavyLimit heavy-operation leases are active."
}

$heavyProcesses = @()
if (-not $useSyntheticSample) {
    $heavyProcesses = @(Get-Process -Name dotnet, MSBuild, VBCSCompiler -ErrorAction SilentlyContinue)
}
$heavyProcessWorkingSetGiB = [Math]::Round(
    (($heavyProcesses | Measure-Object -Property WorkingSet64 -Sum).Sum / 1GB),
    2)

$snapshot = [pscustomobject] [ordered] @{
    GeneratedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    AdmissionMode = $Admission
    RepositoryRoot = $RepositoryRoot
    ConfigPath = [System.IO.Path]::GetFullPath($ConfigPath)
    Measurements = [pscustomobject] [ordered] @{
        FreeDiskGiB = $freeDiskGiB
        TotalDiskGiB = $totalDiskGiB
        FreeDiskPercent = $freeDiskPercent
        AvailableMemoryGiB = $availableMemoryGiB
        LogicalProcessors = $logicalProcessors
        LinkedTaskWorktrees = $linkedTaskWorktrees
        ObservedHeavyProcessCount = $heavyProcesses.Count
        ObservedHeavyProcessWorkingSetGiB = $heavyProcessWorkingSetGiB
    }
    WorktreeAdmission = [pscustomobject] [ordered] @{
        Allowed = $worktreeAllowed
        Reason = $worktreeReason
        ReservedGiB = $reservedGiBPerNewWorktree
        PostReservationFreeGiB = $postReservationFreeGiB
        MaximumLinkedTaskWorktrees = $maximumLinkedTaskWorktrees
    }
    HeavyOperationAdmission = [pscustomobject] [ordered] @{
        Allowed = $heavyAllowed
        Reason = $heavyReason
        ActiveLeases = $ActiveHeavyOperations
        CurrentLimit = $heavyLimit
        HardFloorGiB = $minimumAvailableMemoryGiB
        WarningThresholdGiB = $warningAvailableMemoryGiB
    }
    Warnings = @($warnings)
}

if ($AsJson) {
    $snapshot | ConvertTo-Json -Depth 5
}
else {
    $snapshot
}

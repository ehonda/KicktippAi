[CmdletBinding()]
param(
    [ValidateSet('Snapshot', 'Worktree', 'Heavy')]
    [string] $Admission = 'Snapshot',
    [string] $RepositoryRoot,
    [string] $ConfigPath,
    [string] $StatePath,
    [ValidateRange(0, [int]::MaxValue)]
    [int] $ActiveHeavyOperations = 0,
    [ValidateRange(0, [double]::MaxValue)]
    [double] $OutstandingWorktreeReservationsGiB = 0,
    [switch] $WorktreeInventoryConfirmed,
    [ValidateSet('ActiveBuildCapable', 'ParkedRecoveryOnly', 'RemovalReady', 'Uncertain')]
    [string] $ProposedWorktreeClass = 'ActiveBuildCapable',
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

if ([string]::IsNullOrWhiteSpace($StatePath)) {
    $StatePath = Join-Path $RepositoryRoot '.tmp/orchestration/resource-policy-state.json'
}
else {
    $StatePath = [System.IO.Path]::GetFullPath($StatePath)
}

$policy = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
if ($policy.schemaVersion -ne 3) {
    throw "Unsupported orchestration resource policy schema: $($policy.schemaVersion)"
}

$activeReservationGiB = [double] $policy.worktree.activeBuildCapableGrowthReservationGiB
$uncertainReservationGiB = [double] $policy.worktree.uncertainGrowthReservationGiB
$minimumEffectiveFreeGiB = [double] $policy.worktree.minimumEffectiveFreeGiBAfterReservation
$minimumFreeDiskPercentWarning = [double] $policy.worktree.minimumFreeDiskPercentWarning
$heavyLimit = [int] $policy.heavyOperation.concurrentLimit
$configuredMemoryFloorGiB = [double] $policy.heavyOperation.minimumAvailableMemoryGiB
$experimentalBandUpperGiB = [double] $policy.heavyOperation.experimentalBandUpperGiB
$circuitBreakerFloorGiB = [double] $policy.heavyOperation.circuitBreakerFloorGiB
$warningAvailableMemoryGiB = [double] $policy.heavyOperation.warningAvailableMemoryGiB

if (
    $activeReservationGiB -le 0 -or
    $uncertainReservationGiB -le 0 -or
    $minimumEffectiveFreeGiB -le 0 -or
    $minimumFreeDiskPercentWarning -le 0 -or
    $heavyLimit -lt 1 -or
    $configuredMemoryFloorGiB -le 0 -or
    $experimentalBandUpperGiB -lt $configuredMemoryFloorGiB -or
    $circuitBreakerFloorGiB -lt $experimentalBandUpperGiB -or
    $warningAvailableMemoryGiB -lt $circuitBreakerFloorGiB) {
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
$inventoryConfirmed = if ($useSyntheticSample) {
    [bool] (Get-SyntheticValue -Name 'WorktreeInventoryConfirmed')
}
else {
    [bool] $WorktreeInventoryConfirmed
}
$outstandingReservationsGiB = if ($useSyntheticSample) {
    $sampleReservation = Get-SyntheticValue -Name 'OutstandingWorktreeReservationsGiB'
    if ($null -eq $sampleReservation) { $null } else { [double] $sampleReservation }
}
else {
    [double] $OutstandingWorktreeReservationsGiB
}

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
        $inventoryConfirmed = $false
    }
}

$circuitBreakerActive = $false
$circuitBreakerReason = $null
$circuitBreakerStateValid = $true
if ($useSyntheticSample -and $Sample.ContainsKey('MemoryCircuitBreakerActive')) {
    $circuitBreakerActive = [bool] (Get-SyntheticValue -Name 'MemoryCircuitBreakerActive')
    $circuitBreakerReason = [string] (Get-SyntheticValue -Name 'MemoryCircuitBreakerReason')
}
elseif (Test-Path -LiteralPath $StatePath -PathType Leaf) {
    try {
        $state = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
        if (
            [int] $state.schema_version -ne 1 -or
            [string] $state.status -notin @('active', 'cleared') -or
            ([string] $state.status -eq 'active' -and
                ([double] $state.effective_floor_gib -lt $circuitBreakerFloorGiB -or
                 [string]::IsNullOrWhiteSpace([string] $state.reason)))) {
            throw 'invalid state shape'
        }
        $circuitBreakerActive = [string] $state.status -eq 'active'
        $circuitBreakerReason = if ($circuitBreakerActive) { [string] $state.reason } else { $null }
    }
    catch {
        $circuitBreakerStateValid = $false
        $circuitBreakerReason = 'The memory circuit-breaker state is unreadable or invalid.'
    }
}

$effectiveMemoryFloorGiB = if ($circuitBreakerActive) {
    [Math]::Max($configuredMemoryFloorGiB, $circuitBreakerFloorGiB)
}
else {
    $configuredMemoryFloorGiB
}

$proposedReservationGiB = switch ($ProposedWorktreeClass) {
    'ActiveBuildCapable' { $activeReservationGiB }
    'Uncertain' { $uncertainReservationGiB }
    default { 0.0 }
}

$effectivePostReservationFreeGiB = $null
$effectivePostReservationFreePercent = $null
$freeDiskPercent = $null
if ($null -ne $freeDiskGiB -and $null -ne $totalDiskGiB -and [double] $totalDiskGiB -gt 0) {
    $freeDiskPercent = [Math]::Round(([double] $freeDiskGiB / [double] $totalDiskGiB * 100), 1)
}
if ($null -ne $freeDiskGiB -and $null -ne $outstandingReservationsGiB) {
    $effectivePostReservationFreeGiB = [Math]::Round(
        ([double] $freeDiskGiB - [double] $outstandingReservationsGiB - $proposedReservationGiB), 2)
    if ($null -ne $totalDiskGiB -and [double] $totalDiskGiB -gt 0) {
        $effectivePostReservationFreePercent = [Math]::Round(
            ($effectivePostReservationFreeGiB / [double] $totalDiskGiB * 100), 1)
    }
}

$warnings = [System.Collections.Generic.List[string]]::new()
if (
    $null -ne $effectivePostReservationFreePercent -and
    $effectivePostReservationFreePercent -lt $minimumFreeDiskPercentWarning) {
    $warnings.Add(
        "Effective disk free space after reservations is $effectivePostReservationFreePercent%, below the $minimumFreeDiskPercentWarning% warning threshold.")
}
if (
    $null -ne $availableMemoryGiB -and
    [double] $availableMemoryGiB -lt $warningAvailableMemoryGiB) {
    $warnings.Add(
        "Available memory is $availableMemoryGiB GiB, below the $warningAvailableMemoryGiB GiB warning threshold; the effective hard floor is $effectiveMemoryFloorGiB GiB.")
}
if (-not $circuitBreakerStateValid) {
    $warnings.Add($circuitBreakerReason)
}

$worktreeAllowed = $false
$worktreeReason = ''
$postReservationFreeGiB = $effectivePostReservationFreeGiB
if ($null -eq $freeDiskGiB -or $null -eq $totalDiskGiB) {
    $worktreeReason = 'Denied: disk measurements are unavailable.'
}
elseif (-not $inventoryConfirmed -or $null -eq $linkedTaskWorktrees -or $null -eq $outstandingReservationsGiB) {
    $worktreeReason = 'Denied: worktree inventory or reservation reconciliation is unavailable.'
}
elseif ($postReservationFreeGiB -lt $minimumEffectiveFreeGiB) {
    $worktreeReason = "Denied: outstanding ($outstandingReservationsGiB GiB) and proposed ($proposedReservationGiB GiB) reservations would leave $postReservationFreeGiB GiB, below the $minimumEffectiveFreeGiB GiB floor."
}
else {
    $worktreeAllowed = $true
    $worktreeReason = "Allowed: $linkedTaskWorktrees linked task worktrees are inventoried; outstanding ($outstandingReservationsGiB GiB) and proposed ($proposedReservationGiB GiB) reservations leave $postReservationFreeGiB GiB."
}

$heavyAllowed = $false
$heavyReason = ''
if (-not $circuitBreakerStateValid) {
    $heavyReason = 'Denied: memory circuit-breaker state is unreadable or invalid.'
}
elseif ($null -eq $availableMemoryGiB -or $null -eq $logicalProcessors) {
    $heavyReason = 'Denied: available-memory or logical-processor measurements are unavailable.'
}
elseif ([double] $availableMemoryGiB -lt $effectiveMemoryFloorGiB) {
    $heavyReason = "Denied: $availableMemoryGiB GiB available memory is below the $effectiveMemoryFloorGiB GiB effective floor."
}
elseif ($ActiveHeavyOperations -ge $heavyLimit) {
    $heavyReason = "Denied: $ActiveHeavyOperations active heavy operations meet the current limit of $heavyLimit."
}
else {
    $heavyAllowed = $true
    $heavyReason = "Allowed: $ActiveHeavyOperations of $heavyLimit heavy-operation leases are active."
}

$experimentalMemoryBand = (
    $heavyAllowed -and
    [double] $availableMemoryGiB -lt $experimentalBandUpperGiB)

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
        InventoryConfirmed = $inventoryConfirmed
        ExistingLinkedTaskWorktrees = $linkedTaskWorktrees
        ProposedClass = $ProposedWorktreeClass
        OutstandingReservedGiB = $outstandingReservationsGiB
        ProposedReservedGiB = $proposedReservationGiB
        EffectivePostReservationFreeGiB = $postReservationFreeGiB
        EffectivePostReservationFreePercent = $effectivePostReservationFreePercent
        MinimumEffectiveFreeGiB = $minimumEffectiveFreeGiB
    }
    HeavyOperationAdmission = [pscustomobject] [ordered] @{
        Allowed = $heavyAllowed
        Reason = $heavyReason
        ActiveLeases = $ActiveHeavyOperations
        CurrentLimit = $heavyLimit
        ConfiguredHardFloorGiB = $configuredMemoryFloorGiB
        EffectiveHardFloorGiB = $effectiveMemoryFloorGiB
        ExperimentalBandUpperGiB = $experimentalBandUpperGiB
        InExperimentalBand = $experimentalMemoryBand
        ExperimentalUse = if ($experimentalMemoryBand) { 'recoverable-local-only' } else { $null }
        WarningThresholdGiB = $warningAvailableMemoryGiB
        CircuitBreakerActive = $circuitBreakerActive
        CircuitBreakerReason = $circuitBreakerReason
        CircuitBreakerStatePath = [System.IO.Path]::GetFullPath($StatePath)
    }
    Warnings = @($warnings)
}

if ($AsJson) {
    $snapshot | ConvertTo-Json -Depth 5
}
else {
    $snapshot
}

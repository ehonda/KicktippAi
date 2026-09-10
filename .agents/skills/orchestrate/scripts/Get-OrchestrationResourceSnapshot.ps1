[CmdletBinding()]
param(
    [ValidateSet('Snapshot', 'Worktree', 'Heavy')]
    [string] $Admission = 'Snapshot',
    [string] $RepositoryRoot,
    [string] $ConfigPath,
    [string] $StatePath,
    [ValidateRange(0, [double]::MaxValue)]
    [double] $ActiveMemoryReservationsGiB = 0,
    [ValidateRange(0, [int]::MaxValue)]
    [int] $ActiveWorkerFanout = 0,
    [ValidateRange(0, [int]::MaxValue)]
    [int] $ActiveHeavyProfiles = 0,
    [ValidateRange(0, [double]::MaxValue)]
    [double] $RequestedMemoryReservationGiB = 0,
    [ValidateRange(0, [int]::MaxValue)]
    [int] $RequestedWorkerFanout = 0,
    [switch] $MandatoryRecoverableLocal,
    [switch] $HasExternalOrLiveSideEffects,
    [switch] $UnmeasuredOperation,
    [switch] $ActiveExclusiveOperation,
    [switch] $Backfill,
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

function Resolve-OrchestrationPrimaryCheckout {
    param([Parameter(Mandatory)][string] $CheckoutRoot)

    $commonDirectoryOutput = @(
        & git -C $CheckoutRoot rev-parse --path-format=absolute --git-common-dir 2>$null)
    $gitExitCode = $LASTEXITCODE
    $commonDirectory = [string] ($commonDirectoryOutput | Select-Object -First 1)
    if ($gitExitCode -ne 0 -or [string]::IsNullOrWhiteSpace($commonDirectory)) {
        throw 'Git common-directory identity is unavailable.'
    }
    $commonDirectory = [System.IO.Path]::GetFullPath($commonDirectory.Trim())

    if (Test-Path -LiteralPath (Join-Path $CheckoutRoot '.git') -PathType Container) {
        $primaryGitDirectory = [System.IO.Path]::GetFullPath((Join-Path $CheckoutRoot '.git'))
        if (-not $commonDirectory.Equals($primaryGitDirectory, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw 'The checkout Git directory does not match its Git common-directory identity.'
        }
        return $CheckoutRoot
    }

    $locatorPath = Join-Path $CheckoutRoot '.codex-local/original-repository-path'
    if (-not (Test-Path -LiteralPath $locatorPath -PathType Leaf)) {
        throw 'The original-checkout locator is missing.'
    }
    $primaryRoot = [System.IO.Path]::GetFullPath(
        ([System.IO.File]::ReadAllText($locatorPath).Trim()))
    $primaryGitDirectory = [System.IO.Path]::GetFullPath((Join-Path $primaryRoot '.git'))
    if (
        -not (Test-Path -LiteralPath $primaryGitDirectory -PathType Container) -or
        -not (Test-Path -LiteralPath (Join-Path $primaryRoot 'KicktippAi.slnx') -PathType Leaf)) {
        throw 'The original-checkout locator target is invalid.'
    }
    if (-not $commonDirectory.Equals($primaryGitDirectory, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'The original-checkout locator does not match this worktree Git common directory.'
    }
    return $primaryRoot
}

$statePathResolutionValid = $true
try {
    $stateRepositoryRoot = Resolve-OrchestrationPrimaryCheckout -CheckoutRoot $RepositoryRoot
}
catch {
    $statePathResolutionValid = $false
    $stateRepositoryRoot = $RepositoryRoot
}
$expectedStatePath = [System.IO.Path]::GetFullPath(
    (Join-Path $stateRepositoryRoot '.tmp/orchestration/resource-policy-state.json'))
if ([string]::IsNullOrWhiteSpace($StatePath)) {
    $StatePath = $expectedStatePath
}
else {
    $StatePath = [System.IO.Path]::GetFullPath($StatePath)
    if (
        -not $statePathResolutionValid -or
        -not $StatePath.Equals($expectedStatePath, [System.StringComparison]::OrdinalIgnoreCase)) {
        $statePathResolutionValid = $false
    }
}

$policy = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
if ($policy.schemaVersion -ne 4) {
    throw "Unsupported orchestration resource policy schema: $($policy.schemaVersion)"
}

$activeReservationGiB = [double] $policy.worktree.activeBuildCapableGrowthReservationGiB
$uncertainReservationGiB = [double] $policy.worktree.uncertainGrowthReservationGiB
$minimumEffectiveFreeGiB = [double] $policy.worktree.minimumEffectiveFreeGiBAfterReservation
$minimumFreeDiskPercentWarning = [double] $policy.worktree.minimumFreeDiskPercentWarning
$preferredMemoryFloorGiB = [double] $policy.heavyOperation.preferredAvailableMemoryFloorGiB
$absoluteMemoryFloorGiB = [double] $policy.heavyOperation.absoluteAvailableMemoryFloorGiB
$warningAvailableMemoryGiB = [double] $policy.heavyOperation.warningAvailableMemoryGiB
$projectGateReservationGiB = [double] $policy.heavyOperation.provisionalProjectGateReservationGiB
$maximumLogicalProcessorFraction = [double] $policy.heavyOperation.maximumLogicalProcessorFraction
$unmeasuredExclusive = [bool] $policy.heavyOperation.unmeasuredOperationsAreExclusive
$degradedMemoryFloorGiB = [double] $policy.heavyOperation.degraded.availableMemoryFloorGiB
$degradedMaximumProfiles = [int] $policy.heavyOperation.degraded.maximumConcurrentProfiles
$degradedMaximumWorkerFanout = [int] $policy.heavyOperation.degraded.maximumWorkerFanout
$degradedRetryLimit = [int] $policy.heavyOperation.degraded.recoverableRetryLimit

if (
    $activeReservationGiB -le 0 -or
    $uncertainReservationGiB -le 0 -or
    $minimumEffectiveFreeGiB -le 0 -or
    $minimumFreeDiskPercentWarning -le 0 -or
    $preferredMemoryFloorGiB -le 0 -or
    $absoluteMemoryFloorGiB -le 0 -or
    $absoluteMemoryFloorGiB -ge $preferredMemoryFloorGiB -or
    $warningAvailableMemoryGiB -lt $preferredMemoryFloorGiB -or
    $projectGateReservationGiB -le 0 -or
    $maximumLogicalProcessorFraction -le 0 -or
    $maximumLogicalProcessorFraction -gt 1 -or
    $degradedMemoryFloorGiB -lt $preferredMemoryFloorGiB -or
    $degradedMaximumProfiles -lt 1 -or
    $degradedMaximumWorkerFanout -lt 1 -or
    $degradedRetryLimit -lt 1) {
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

function Test-OrchestrationTimestamp {
    param([string] $Value)

    $parsed = [DateTimeOffset]::MinValue
    return (
        -not [string]::IsNullOrWhiteSpace($Value) -and
        [DateTimeOffset]::TryParse(
            $Value,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [System.Globalization.DateTimeStyles]::AssumeUniversal,
            [ref] $parsed))
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
$circuitBreakerStateValid = $statePathResolutionValid
$circuitBreakerReason = if ($statePathResolutionValid) { $null } else {
    'The shared memory circuit-breaker path could not be resolved to the primary checkout.'
}
if ($useSyntheticSample -and $Sample.ContainsKey('MemoryCircuitBreakerActive')) {
    $circuitBreakerActive = [bool] (Get-SyntheticValue -Name 'MemoryCircuitBreakerActive')
    $circuitBreakerReason = [string] (Get-SyntheticValue -Name 'MemoryCircuitBreakerReason')
}
elseif ($statePathResolutionValid -and (Test-Path -LiteralPath $StatePath -PathType Leaf)) {
    try {
        $state = Get-Content -LiteralPath $StatePath -Raw | ConvertFrom-Json
        if (
            [int] $state.schema_version -ne 2 -or
            [string] $state.status -notin @('active', 'cleared') -or
            -not (Test-OrchestrationTimestamp -Value ([string] $state.updated_at_utc)) -or
            $null -eq $state.trigger -or
            [string]::IsNullOrWhiteSpace([string] $state.trigger.run_id) -or
            [string]::IsNullOrWhiteSpace([string] $state.trigger.operation) -or
            -not (Test-OrchestrationTimestamp -Value ([string] $state.trigger.at_utc)) -or
            [string]::IsNullOrWhiteSpace([string] $state.trigger.reason) -or
            ([string] $state.status -eq 'active' -and
                ([string] $state.mode -ne 'degraded' -or
                 [double] $state.effective_floor_gib -ne $degradedMemoryFloorGiB -or
                 [int] $state.maximum_concurrent_profiles -ne $degradedMaximumProfiles -or
                 [int] $state.maximum_worker_fanout -ne $degradedMaximumWorkerFanout -or
                 [int] $state.recoverable_retry_limit -ne $degradedRetryLimit -or
                 $null -ne $state.clearance)) -or
            ([string] $state.status -eq 'cleared' -and
                ([string] $state.mode -ne 'normal' -or
                 [double] $state.effective_floor_gib -ne $preferredMemoryFloorGiB -or
                 $null -eq $state.clearance -or
                 -not (Test-OrchestrationTimestamp -Value ([string] $state.clearance.at_utc)) -or
                 [string]::IsNullOrWhiteSpace([string] $state.clearance.reviewed_by) -or
                 [string]::IsNullOrWhiteSpace([string] $state.clearance.reason)))) {
            throw 'invalid state shape'
        }
        $circuitBreakerActive = [string] $state.status -eq 'active'
        $circuitBreakerReason = if ($circuitBreakerActive) { [string] $state.trigger.reason } else { $null }
    }
    catch {
        $circuitBreakerStateValid = $false
        $circuitBreakerReason = 'The memory circuit-breaker state is unreadable or invalid.'
    }
}

$effectiveMemoryFloorGiB = if ($circuitBreakerActive) {
    $degradedMemoryFloorGiB
}
elseif ($MandatoryRecoverableLocal -and -not $HasExternalOrLiveSideEffects) {
    $absoluteMemoryFloorGiB
}
else {
    $preferredMemoryFloorGiB
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

$isHeavyRequest = $Admission -eq 'Heavy'
$logicalProcessorBudget = if ($null -eq $logicalProcessors) {
    $null
}
else {
    [Math]::Max(1, [Math]::Floor([int] $logicalProcessors * $maximumLogicalProcessorFraction))
}
$effectiveRequestedMemoryGiB = if (-not $isHeavyRequest) {
    0.0
}
elseif ($RequestedMemoryReservationGiB -gt 0) {
    [double] $RequestedMemoryReservationGiB
}
elseif ($UnmeasuredOperation -and $null -ne $availableMemoryGiB) {
    [Math]::Max(0.0, [double] $availableMemoryGiB - $effectiveMemoryFloorGiB - $ActiveMemoryReservationsGiB)
}
else {
    $projectGateReservationGiB
}
$effectiveRequestedWorkerFanout = if (-not $isHeavyRequest) {
    0
}
elseif ($RequestedWorkerFanout -gt 0) {
    $RequestedWorkerFanout
}
elseif ($UnmeasuredOperation -and $null -ne $logicalProcessorBudget) {
    [Math]::Max(1, [int] $logicalProcessorBudget - $ActiveWorkerFanout)
}
else {
    1
}
$exclusiveRequest = $circuitBreakerActive -or ($UnmeasuredOperation -and $unmeasuredExclusive)
$memoryPoolGiB = if ($null -eq $availableMemoryGiB) {
    $null
}
else {
    [Math]::Round([double] $availableMemoryGiB - $effectiveMemoryFloorGiB, 2)
}
$postAdmissionMemoryGiB = if ($null -eq $memoryPoolGiB) {
    $null
}
else {
    [Math]::Round(
        $memoryPoolGiB - $ActiveMemoryReservationsGiB - $effectiveRequestedMemoryGiB, 2)
}
$postAdmissionWorkerCapacity = if ($null -eq $logicalProcessorBudget) {
    $null
}
else {
    [int] $logicalProcessorBudget - $ActiveWorkerFanout - $effectiveRequestedWorkerFanout
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
    $heavyReason = "Denied: $availableMemoryGiB GiB available memory is below the $effectiveMemoryFloorGiB GiB floor."
}
elseif ($isHeavyRequest -and $HasExternalOrLiveSideEffects -and $MandatoryRecoverableLocal) {
    $heavyReason = 'Denied: the experimental floor cannot be used for external or live side effects.'
}
elseif ($isHeavyRequest -and $ActiveExclusiveOperation) {
    $heavyReason = 'Denied: an active exclusive operation prevents backfill.'
}
elseif ($isHeavyRequest -and $exclusiveRequest -and $ActiveHeavyProfiles -gt 0) {
    $heavyReason = 'Denied: this unmeasured or degraded operation requires exclusive heavy admission.'
}
elseif ($isHeavyRequest -and $circuitBreakerActive -and
        ($ActiveHeavyProfiles -ge $degradedMaximumProfiles -or
         $effectiveRequestedWorkerFanout -gt $degradedMaximumWorkerFanout)) {
    $heavyReason = 'Denied: degraded mode permits only one low-fanout heavy profile.'
}
elseif ($isHeavyRequest -and $postAdmissionMemoryGiB -lt 0) {
    $heavyReason = "Denied: active ($ActiveMemoryReservationsGiB GiB) and requested ($effectiveRequestedMemoryGiB GiB) reservations exceed the $memoryPoolGiB GiB memory pool."
}
elseif ($isHeavyRequest -and $postAdmissionWorkerCapacity -lt 0) {
    $heavyReason = "Denied: active ($ActiveWorkerFanout) and requested ($effectiveRequestedWorkerFanout) workers exceed the $logicalProcessorBudget logical-processor budget."
}
else {
    $heavyAllowed = $true
    $heavyReason = if ($isHeavyRequest) {
        "Allowed: reservations leave $postAdmissionMemoryGiB GiB and $postAdmissionWorkerCapacity worker slots in the applicable pools."
    }
    else {
        "Snapshot: the measured host is above the $effectiveMemoryFloorGiB GiB applicable floor."
    }
}

$experimentalMemoryBand = (
    $heavyAllowed -and $isHeavyRequest -and
    $effectiveMemoryFloorGiB -eq $absoluteMemoryFloorGiB)

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
        ActiveProfiles = $ActiveHeavyProfiles
        ActiveReservedMemoryGiB = $ActiveMemoryReservationsGiB
        RequestedReservedMemoryGiB = $effectiveRequestedMemoryGiB
        MemoryPoolGiB = $memoryPoolGiB
        PostAdmissionMemoryGiB = $postAdmissionMemoryGiB
        ActiveWorkerFanout = $ActiveWorkerFanout
        RequestedWorkerFanout = $effectiveRequestedWorkerFanout
        LogicalProcessorBudget = $logicalProcessorBudget
        PostAdmissionWorkerCapacity = $postAdmissionWorkerCapacity
        PreferredFloorGiB = $preferredMemoryFloorGiB
        AbsoluteFloorGiB = $absoluteMemoryFloorGiB
        EffectiveFloorGiB = $effectiveMemoryFloorGiB
        UsesExperimentalFloor = $experimentalMemoryBand
        ExperimentalUse = if ($experimentalMemoryBand) { 'mandatory-recoverable-local-only' } else { $null }
        ExclusiveRequest = $exclusiveRequest
        ActiveExclusiveOperation = [bool] $ActiveExclusiveOperation
        Backfill = [bool] $Backfill
        UnmeasuredOperation = [bool] $UnmeasuredOperation
        ProvisionalProjectGateReservationGiB = $projectGateReservationGiB
        WarningThresholdGiB = $warningAvailableMemoryGiB
        CircuitBreakerActive = $circuitBreakerActive
        CircuitBreakerReason = $circuitBreakerReason
        CircuitBreakerStateValid = $circuitBreakerStateValid
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

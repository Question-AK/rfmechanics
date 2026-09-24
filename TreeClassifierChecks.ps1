# Focused source-contract checks for the shared tree classifier. No game, network or deployment.
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'

$classifier = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'src/TreeBlockClassifier.cs') -Raw
$climbing = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'src/TreeClimbingPatch.cs') -Raw
$goblin = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'src/GoblinClimbingPatch.cs') -Raw
$foliage = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'src/BranchyLeavesPassthroughPatch.cs') -Raw

function Assert-Check([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "FAILED: $Message" }
    Write-Host "PASS: $Message"
}

$trunkPrefixes = @('log-grown-', 'logsection-grown-', 'lognarrow-grown-')
foreach ($prefix in $trunkPrefixes) {
    Assert-Check $classifier.Contains('"' + $prefix + '"') "trunk prefix is centralized: $prefix"
}
Assert-Check $classifier.Contains('"leavesbranchy-"') 'branch foliage prefix is separate'
Assert-Check (-not $foliage.Contains('.Contains("branchy")')) 'foliage code has no loose substring match'
Assert-Check (-not $climbing.Contains('StartsWith("log-grown"')) 'Elf climbing has no local loose trunk match'
Assert-Check (-not $goblin.Contains('StartsWith("log-grown"')) 'Goblin climbing has no local loose trunk match'

function Test-BoundedPrefix([string]$Path, [string[]]$Prefixes) {
    foreach ($prefix in $Prefixes) {
        if ($Path.StartsWith($prefix, [StringComparison]::Ordinal)) { return $true }
    }
    return $false
}

$positive = @(
    'log-grown-redwood-ud',
    'logsection-grown-redwood-ne-ud',
    'lognarrow-grown-cypress-ud'
)
$negative = @(
    'log-placed-redwood-ud',
    'logsection-placed-redwood-ne-ud',
    'lognarrow-placed-cypress-ud',
    'modtree-log-grown-redwood-ud',
    'somebranchyunrelatedblock'
)
foreach ($path in $positive) { Assert-Check (Test-BoundedPrefix $path $trunkPrefixes) "accepted living trunk: $path" }
foreach ($path in $negative) { Assert-Check (-not (Test-BoundedPrefix $path $trunkPrefixes)) "rejected non-trunk/unrelated path: $path" }
Assert-Check (Test-BoundedPrefix 'leavesbranchy-grown-redwood' @('leavesbranchy-')) 'accepted branch foliage family'
Assert-Check (-not (Test-BoundedPrefix 'somebranchyunrelatedblock' @('leavesbranchy-'))) 'rejected branchy substring in unrelated block'

Write-Host 'Tree classifier focused checks passed.'

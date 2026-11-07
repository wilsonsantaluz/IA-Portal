#requires -Version 5.1
# Offline tests: OCI, sleeps and SSH/network discovery are replaced in memory.
$ErrorActionPreference = 'Stop'
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('ia-oci-tests-' + [Guid]::NewGuid().ToString('N'))
. (Join-Path $PSScriptRoot 'provision-always-free.ps1') -StateDirectory $testDirectory
$script:Assertions = 0
function Assert-True($Condition, [string]$Message) {
    if (-not $Condition) { throw "FAIL: $Message" }
    $script:Assertions++
}
function Assert-Throws([scriptblock]$Action, [string]$Pattern) {
    try { & $Action } catch {
        Assert-True ($_.Exception.Message -match $Pattern) "Expected error matching $Pattern; got $($_.Exception.Message)"
        return
    }
    throw "FAIL: expected error matching $Pattern"
}
function New-TcpRule([int]$Port, [string]$Source) {
    [pscustomobject]@{ protocol='6'; source=$Source; 'source-type'='CIDR_BLOCK'; 'is-stateless'=$false; 'tcp-options'=@{ 'destination-port-range'=@{min=$Port;max=$Port} }; 'icmp-options'=$null }
}
$cidr = '203.0.113.10/32'
$rules = @((New-TcpRule 22 $cidr), (New-TcpRule 80 '0.0.0.0/0'), (New-TcpRule 443 '0.0.0.0/0'))
Assert-True (@(Test-IngressRules $rules $cidr).Count -eq 0) 'SSH /32 and public web ports accepted'
Assert-True (@(Test-IngressRules @((New-TcpRule 22 '0.0.0.0/0')) $cidr).Count -eq 4) 'Current broad SSH + missing web blocked'
foreach ($port in @(5432,6333,8080,9081,9089,18081)) {
    Assert-True (@(Test-IngressRules ($rules + @((New-TcpRule $port '0.0.0.0/0'))) $cidr).Count -gt 0) "Internal port $port blocked"
}
$wide = New-TcpRule 22 $cidr; $wide.'tcp-options'.'destination-port-range'.max=65535
Assert-True (@(Test-IngressRules @($wide) $cidr).Count -gt 0) 'Broad ranges blocked'
function Error-Result([string]$Code, [int]$Status, [string]$Message) {
    [pscustomobject]@{ExitCode=1;Json=$null;ErrorText=('ServiceError:' + (@{code=$Code;status=$Status;message=$Message} | ConvertTo-Json))}
}
$capacity = Error-Result 'InternalError' 500 'Out of host capacity.'
Assert-True ((Get-LaunchOutcome $capacity) -eq 'Capacity') 'Explicit capacity retries'
Assert-True ((Get-LaunchOutcome (Error-Result 'InternalError' 500 'Out of capacity for shape VM.Standard.A1.Flex')) -eq 'Capacity') 'Reported capacity wording retries'
foreach ($code in @('NotAuthenticated','NotAuthorizedOrNotFound','LimitExceeded','QuotaExceeded','InvalidParameter','PaymentRequired','TooManyRequests')) {
    Assert-True ((Get-LaunchOutcome (Error-Result $code 400 'No capacity')) -eq 'Fatal') "$code never retries"
}
Assert-True ((Get-LaunchOutcome (Error-Result 'InternalError' 500 'Unexpected server error')) -eq 'Ambiguous') 'Generic 500 reconciles without retry'
Assert-True ((Get-LaunchOutcome ([pscustomobject]@{ExitCode=1;Json=$null;ErrorText='Timeout'})) -eq 'Ambiguous') 'Transport timeout ambiguous'
Assert-True ((Get-LaunchOutcome ([pscustomobject]@{ExitCode=0;Json=$null;ErrorText=''})) -eq 'Ambiguous') 'Empty successful response ambiguous'
Assert-True ((Get-LaunchOutcome ([pscustomobject]@{ExitCode=0;Json=[pscustomobject]@{data=[pscustomobject]@{}};ErrorText=''})) -eq 'Ambiguous') 'Successful response without instance ID ambiguous'

# Exercise the real preflight against synthetic OCI response fixtures.
$script:FixtureRules=$rules; $script:FixtureAvailable=200; $script:FixtureUsage=@(); $script:FixtureReadDenied=$false; $script:FixtureInstances=@()
function Invoke-Oci($Arguments) {
    if ($script:FixtureReadDenied) { return [pscustomobject]@{ExitCode=1;Json=$null;ErrorText='Denied'} }
    $command=$Arguments[0..2] -join ' '
    $data=switch ($command) {
        'iam region-subscription list' { ,@([pscustomobject]@{'is-home-region'=$true;'region-name'='sa-saopaulo-1';status='READY'}) }
        'iam availability-domain list' { ,@([pscustomobject]@{name='test-ad'}) }
        'iam compartment list' { ,@() }
        'compute instance list' { ,$script:FixtureInstances }
        'bv volume list' { ,@() }
        'bv boot-volume list' { ,@() }
        'limits quota list' { ,@() }
        'compute shape list' { ,@([pscustomobject]@{shape='VM.Standard.A1.Flex';'billing-type'='LIMITED_FREE'}) }
        'limits value list' {
            ,@('standard-a1-core-count','standard-a1-core-regional-count','standard-a1-memory-count','standard-a1-memory-regional-count' | ForEach-Object {
                [pscustomobject]@{name=$_;'scope-type'=if ($_ -like '*regional*') {'REGION'} else {'AD'};'availability-domain'='test-ad'}
            })
        }
        'limits resource-availability get' { [pscustomobject]@{available=$script:FixtureAvailable} }
        'compute image list' { ,@([pscustomobject]@{id='image-test';'display-name'='Canonical-Ubuntu-24.04-aarch64-2026.10.27-0';'lifecycle-state'='AVAILABLE';'compartment-id'=$null;'time-created'='2026-10-30T00:00:00Z';'size-in-mbs'=47694}) }
        'compute image-shape-compatibility-entry get' { [pscustomobject]@{shape='VM.Standard.A1.Flex'} }
        'usage-api usage-summary request-summarized-usages' { [pscustomobject]@{items=$script:FixtureUsage} }
        'network subnet get' { [pscustomobject]@{'vcn-id'='vcn-test';'lifecycle-state'='AVAILABLE';'prohibit-public-ip-on-vnic'=$false;'prohibit-internet-ingress'=$false;'availability-domain'=$null;'ipv6-cidr-blocks'=$null;'route-table-id'='route-test';'security-list-ids'=@('list-test')} }
        'network vcn get' { [pscustomobject]@{id='vcn-test';'lifecycle-state'='AVAILABLE'} }
        'network route-table get' { [pscustomobject]@{'vcn-id'='vcn-test';'route-rules'=@([pscustomobject]@{destination='0.0.0.0/0';'destination-type'='CIDR_BLOCK';'network-entity-id'='ocid1.internetgateway.test'})} }
        'network internet-gateway get' { [pscustomobject]@{'vcn-id'='vcn-test';'is-enabled'=$true;'lifecycle-state'='AVAILABLE';'route-table-id'=$null} }
        'network security-list get' { [pscustomobject]@{'vcn-id'='vcn-test';'lifecycle-state'='AVAILABLE';'ingress-security-rules'=$script:FixtureRules;'egress-security-rules'=@([pscustomobject]@{protocol='all';destination='0.0.0.0/0';'is-stateless'=$false})} }
        default { throw "Unexpected OCI call in preflight: $command" }
    }
    [pscustomobject]@{ExitCode=0;Json=[pscustomobject]@{data=$data};ErrorText='';InvalidJson=$false}
}
Assert-True ((Get-Preflight 'tenant-test' 'tenant-test' $cidr).Blockers.Count -eq 0) 'Full preflight permits safe network and empty free inventory'
$script:FixtureRules=@((New-TcpRule 22 '0.0.0.0/0'))
Assert-True ((Get-Preflight 'tenant-test' 'tenant-test' $cidr).Blockers.Count -eq 4) 'Full preflight blocks current OCI network policy'
$script:FixtureRules=$rules; $script:FixtureAvailable=1
Assert-True ((Get-Preflight 'tenant-test' 'tenant-test' $cidr).Blockers.Count -gt 0) 'Full preflight blocks exhausted compute/storage quota'
$script:FixtureAvailable=200; $script:FixtureUsage=@([pscustomobject]@{'sku-name'='Compute Standard A1 OCPU';'computed-quantity'=12})
Assert-True ((Get-Preflight 'tenant-test' 'tenant-test' $cidr).Blockers.Count -gt 0) 'Full preflight blocks historical A1 use even with empty inventory'
$script:FixtureUsage=@(); $script:FixtureInstances=@([pscustomobject]@{id='other';'display-name'='other-vm';'lifecycle-state'='STOPPED';shape='VM.Standard.A1.Flex';'freeform-tags'=[pscustomobject]@{}})
Assert-True ((Get-Preflight 'tenant-test' 'tenant-test' $cidr).Blockers.Count -gt 0) 'Full preflight counts stopped A1 with empty tags against free allowance'
$script:FixtureInstances=@()
$script:FixtureReadDenied=$true
Assert-Throws { Get-Preflight 'tenant-test' 'tenant-test' $cidr } 'OCI read failed'

# Main state machine with no OCI access and no real waiting.
function Read-LocalSettings { @{tenancy='ocid1.tenancy.test';region='sa-saopaulo-1'} }
function Resolve-PublicKey { 'ssh-ed25519 QUJDRA== synthetic-test' }
function Resolve-AdminCidr { $cidr }
function Start-Sleep { param($Seconds) $script:Sleeps += @($Seconds) }
function Wait-ExistingInstance($Instance) { $script:Observed += @($Instance.id) }
function Get-Preflight($Tenancy, $TargetCompartment, $Cidr) {
    $script:Reads++
    if ($script:ReadFailure) { throw 'read denied' }
    $found = if ($script:Existing -or ($script:Reconcile -and $script:Launches -gt 0)) { [pscustomobject]@{id='instance-test';'lifecycle-state'='RUNNING'} } else { $null }
    [pscustomobject]@{ Existing=$found;Blockers=$script:Blockers;AvailabilityDomain='test-ad';Image=[pscustomobject]@{id='test-image';'display-name'='Ubuntu fixture'};BootBudgetGB=50;StorageUsedGB=0 }
}
function Invoke-Oci($Arguments) {
    Assert-True (($Arguments[0..2] -join ' ') -eq 'compute instance launch') 'Only intended launch command issued'
    $script:Launches++
    $request = Get-Content -LiteralPath (Join-Path $StateDirectory 'launch-request.json') -Raw | ConvertFrom-Json
    Assert-True ($request.shape -eq 'VM.Standard.A1.Flex' -and $request.shapeConfig.ocpus -eq 2 -and $request.shapeConfig.memoryInGBs -eq 12) 'Fixed free shape budget'
    Assert-True ($request.assignPublicIp -and -not $request.assignIpv6Ip) 'Only IPv4 public assigned'
    Assert-True ($request.sourceDetails.bootVolumeVpusPerGB -eq 10 -and $request.sourceDetails.PSObject.Properties.Name -notcontains 'bootVolumeSizeInGBs') 'Default boot size and balanced performance'
    Assert-True ((Get-Content -LiteralPath (Join-Path $StateDirectory 'journal.json') -Raw | ConvertFrom-Json).Outcome -eq 'Pending') 'Journal durable before launch'
    if ($script:ResultQueue.Count -eq 0) { throw 'Unexpected additional launch' }
    $next=$script:ResultQueue[0]
    $script:ResultQueue=@($script:ResultQueue | Select-Object -Skip 1)
    $next
}
function Reset-Scenario {
    [void][IO.Directory]::CreateDirectory($StateDirectory)
    foreach ($name in @('journal.json','dry-run.json','operations.log')) {
        $path=Join-Path $StateDirectory $name
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path }
    }
    $script:Launches=0; $script:Reads=0; $script:Existing=$false; $script:Reconcile=$false; $script:ReadFailure=$false
    $script:Blockers=@(); $script:Sleeps=@(); $script:Observed=@(); $script:ResultQueue=@()
    $script:Mode='Provision'; $script:ProvisionApproved=$true
}
$accepted=[pscustomobject]@{ExitCode=0;Json=[pscustomobject]@{data=[pscustomobject]@{id='instance-test'}};ErrorText=''}
try {
    Reset-Scenario; $script:Mode='DryRun'
    Assert-True ((Invoke-Provision).Status -eq 'Ready' -and $script:Launches -eq 0) 'DryRun never launches'
    Reset-Scenario; $script:Mode='DryRun'; $script:Blockers=@('unsafe SSH')
    Assert-True ((Invoke-Provision).Status -eq 'Blocked' -and $script:Launches -eq 0) 'DryRun reports unsafe network'
    Reset-Scenario; $script:ProvisionApproved=$false
    Assert-Throws { Invoke-Provision } 'explicit prior authorization'
    Assert-True ($script:Launches -eq 0) 'No approval means no launch'
    Reset-Scenario; $script:Blockers=@('quota')
    Assert-Throws { Invoke-Provision } 'Preflight blocked'
    Assert-True ($script:Launches -eq 0) 'Quota/network blocker means no launch'
    Reset-Scenario; $script:Existing=$true
    Assert-True ((Invoke-Provision).Status -eq 'Existing' -and $script:Launches -eq 0) 'Existing instance prevents duplicate'
    Reset-Scenario; $script:ReadFailure=$true
    Assert-Throws { Invoke-Provision } 'read denied'
    Assert-True ($script:Launches -eq 0) 'Inventory authorization failure stops'
    Reset-Scenario; $script:ResultQueue=@($capacity,$accepted)
    # Mock sleep advances journal time, preserving the production 30-minute policy.
    function Start-Sleep { param($Seconds)
        $script:Sleeps+=@($Seconds)
        $journalPath=Join-Path $StateDirectory 'journal.json'
        if (Test-Path -LiteralPath $journalPath) {
            $j=Get-Content -LiteralPath $journalPath -Raw | ConvertFrom-Json
            $j.At=[DateTime]::UtcNow.AddSeconds(-1801).ToString('o');Save-Journal $j
        }
    }
    Assert-True ((Invoke-Provision).Status -eq 'Success') 'Capacity followed by success terminates'
    Assert-True ($script:Launches -eq 2 -and $script:Sleeps[0] -eq 1800 -and $script:Reads -eq 2) '30 minute interval and fresh preflight before retry'
    Reset-Scenario; $script:ResultQueue=@((Error-Result 'QuotaExceeded' 400 'quota'))
    Assert-Throws { Invoke-Provision } 'Fatal OCI launch error'
    Assert-True ($script:Launches -eq 1 -and $script:Sleeps.Count -eq 0 -and $script:Reads -eq 1) 'Fatal error stops immediately without retry, sleep or new reads'
    Reset-Scenario; $script:ResultQueue=@([pscustomobject]@{ExitCode=1;Json=$null;ErrorText='timeout'})
    Assert-Throws { Invoke-Provision } 'Launch failed or response ambiguous'
    Assert-True ($script:Launches -eq 1 -and $script:Reads -eq 4) 'Ambiguous response reconciled three times without relaunch'
    Assert-Throws { Invoke-Provision } 'Journal requires reconciliation'
    Assert-True ($script:Launches -eq 1) 'Restart after ambiguity cannot relaunch'
    Reset-Scenario; $script:Reconcile=$true; $script:ResultQueue=@([pscustomobject]@{ExitCode=1;Json=$null;ErrorText='timeout'})
    Assert-True ((Invoke-Provision).Status -eq 'Reconciled' -and $script:Launches -eq 1) 'Timeout with eventual instance reconciles successfully'
    Reset-Scenario
    $handle=[IO.File]::Open((Join-Path $StateDirectory 'provision.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    try { Assert-Throws { Invoke-Provision } 'process|processo' } finally { $handle.Dispose() }
    Assert-True ($script:Launches -eq 0) 'Concurrent process blocked by file lock'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $StateDirectory 'launch-request.json'))) 'Temporary public-key payload removed'
    Write-Host "PASS: $script:Assertions offline assertions; no OCI calls, no VM, no real sleep."
} finally {
    # Remove only files created in this unique test directory; no recursive delete.
    if (Test-Path -LiteralPath $testDirectory) {
        Get-ChildItem -LiteralPath $testDirectory -File | Remove-Item -Force
        Remove-Item -LiteralPath $testDirectory
    }
}

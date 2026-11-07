#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidateSet('DryRun', 'Provision')][string]$Mode = 'DryRun',
    [string]$ConfigFile = (Join-Path $env:USERPROFILE '.oci/config'),
    [string]$Profile = 'DEFAULT',
    [string]$CompartmentId,
    [string]$SubnetId = 'ocid1.subnet.oc1.sa-saopaulo-1.aaaaaaaaiyzth2hpnnajytzvaokoru6vvnjtgojzr3qonci2z5axng5h5hpa',
    [string]$SshPublicKeyPath,
    [string]$AdminCidr,
    [string]$ImageId,
    [string]$StateDirectory = (Join-Path $env:LOCALAPPDATA 'IA-Portal/oci-always-free'),
    [switch]$ProvisionApproved
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:Region = 'sa-saopaulo-1'
$script:Shape = 'VM.Standard.A1.Flex'
$script:DisplayName = 'ia-portal-prod'
$script:IntervalSeconds = 1800

function Write-OperationLog([string]$Message) {
    # Only our controlled summaries, never CLI stdout/stderr, config, keys or metadata.
    $line = '{0} {1}' -f [DateTime]::UtcNow.ToString('o'), $Message
    Write-Host $line
    Add-Content -LiteralPath (Join-Path $StateDirectory 'operations.log') -Value $line -Encoding UTF8
}

function ConvertTo-NativeArgument([string]$Value) {
    # Windows CommandLineToArgvW quoting, including paths with spaces/trailing slashes.
    '"' + (($Value -replace '(\\*)"', '$1$1\"') -replace '(\\+)$', '$1$1') + '"'
}

function Invoke-Oci([string[]]$Arguments) {
    $all = @($Arguments) + @('--config-file', $ConfigFile, '--profile', $Profile,
        '--auth', 'api_key', '--region', $script:Region, '--output', 'json',
        '--no-retry', '--connection-timeout', '15', '--read-timeout', '90')
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = (Get-Command oci -CommandType Application -ErrorAction Stop).Source
    $psi.Arguments = (($all | ForEach-Object { ConvertTo-NativeArgument $_ }) -join ' ')
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    try {
        [void]$process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $output = $stdout.GetAwaiter().GetResult()
        $errorOutput = $stderr.GetAwaiter().GetResult()
        $data = $null; $invalidJson = $false
        if ($process.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($output)) {
            try { $data = $output | ConvertFrom-Json } catch { $invalidJson = $true }
        }
        [pscustomobject]@{ ExitCode = $process.ExitCode; Json = $data; ErrorText = $errorOutput; InvalidJson = $invalidJson }
    } finally { $process.Dispose() }
}

function Get-OciData([string[]]$Arguments, [switch]$List) {
    $result = Invoke-Oci $Arguments
    if ($result.PSObject.Properties.Name -contains 'InvalidJson' -and $result.InvalidJson) { throw 'OCI read returned malformed JSON; no launch.' }
    if ($result.ExitCode -ne 0) {
        # Do not echo service error text: it can include request metadata.
        throw ('OCI read failed ({0}); inspect IAM/configuration in the Console. No launch.' -f ($Arguments[0..1] -join ' '))
    }
    if ($null -eq $result.Json) {
        # OCI CLI emits *no output* for an empty list with --all.
        if ($List) { return }
        throw 'OCI returned an empty response to a required read.'
    }
    foreach ($pageKey in @('opc-next-page','next-page')) {
        if ($result.Json.PSObject.Properties.Name -contains $pageKey -and $result.Json.$pageKey) { throw 'Incomplete paginated OCI response; refusing a partial inventory.' }
    }
    $result.Json.data
}

function Read-LocalSettings {
    $sections = @{}
    $section = 'DEFAULT'
    foreach ($line in Get-Content -LiteralPath $ConfigFile) {
        if ($line -match '^\s*\[([^\]]+)\]\s*$') { $section = $Matches[1]; continue }
        if ($line -match '^\s*(tenancy|region)\s*=\s*(.*?)\s*$') {
            if (-not $sections.ContainsKey($section)) { $sections[$section] = @{} }
            $sections[$section][$Matches[1]] = $Matches[2]
        }
    }
    $settings = @{}
    foreach ($name in @('DEFAULT', $Profile)) {
        if ($sections.ContainsKey($name)) { foreach ($k in $sections[$name].Keys) { $settings[$k] = $sections[$name][$k] } }
    }
    if ($settings['tenancy'] -notmatch '^ocid1\.tenancy\.') { throw 'Missing tenancy in OCI configuration.' }
    if ($settings['region'] -ne $script:Region) { throw 'Configured region must be sa-saopaulo-1; refusing another region.' }
    $settings
}

function Resolve-PublicKey {
    if (-not $SshPublicKeyPath) {
        $candidates = @()
        foreach ($directory in @((Join-Path $env:USERPROFILE '.ssh'), (Join-Path $env:USERPROFILE 'Downloads'))) {
            if (Test-Path -LiteralPath $directory) {
                $candidates += @(Get-ChildItem -LiteralPath $directory -Filter '*.pub' -File | Where-Object { $_.Name -match '^(id_|ssh-key)' })
            }
        }
        if ($candidates.Count -ne 1) { throw 'Specify -SshPublicKeyPath: zero or multiple SSH public keys found.' }
        $script:SshPublicKeyPath = $candidates[0].FullName
    }
    if ([IO.Path]::GetExtension($SshPublicKeyPath) -ne '.pub') { throw 'Only a .pub SSH public key is accepted. Private keys are never read.' }
    $public = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $SshPublicKeyPath).Path).Trim()
    if ($public -notmatch '^(ssh-ed25519|ssh-rsa|ecdsa-sha2-nistp(?:256|384|521)) [A-Za-z0-9+/]+=*(?: [^\r\n]*)?$') { throw 'Invalid OpenSSH public key.' }
    try { [void][Convert]::FromBase64String($public.Split(' ')[1]) } catch { throw 'Invalid SSH public key encoding.' }
    $public
}

function Resolve-AdminCidr {
    if (-not $AdminCidr) {
        $ip = (Invoke-RestMethod -Uri 'https://checkip.amazonaws.com' -TimeoutSec 15).Trim()
        $script:AdminCidr = "$ip/32"
    }
    if ($AdminCidr -notmatch '^(\d{1,3}\.){3}\d{1,3}/32$') { throw 'AdminCidr must be a single public IPv4 /32.' }
    $parsed = [System.Net.IPAddress]::Parse($AdminCidr.Split('/')[0])
    $bytes = $parsed.GetAddressBytes()
    if ($bytes[0] -in @(0,10,127) -or $bytes[0] -ge 224 -or
        ($bytes[0] -eq 169 -and $bytes[1] -eq 254) -or
        ($bytes[0] -eq 172 -and $bytes[1] -ge 16 -and $bytes[1] -le 31) -or
        ($bytes[0] -eq 192 -and $bytes[1] -eq 168) -or
        ($bytes[0] -eq 100 -and $bytes[1] -ge 64 -and $bytes[1] -le 127)) { throw 'AdminCidr must be publicly routable.' }
    $AdminCidr
}

function Test-IngressRules([object[]]$Rules, [string]$Cidr) {
    $issues = [Collections.Generic.List[string]]::new()
    $ssh = $false; $http = $false; $https = $false
    foreach ($rule in $Rules) {
        # Preserve the default ICMP fragmentation-needed and VCN destination-unreachable.
        if ($rule.protocol -eq '1' -and $null -ne $rule.'icmp-options' -and $rule.'icmp-options'.type -eq 3) { continue }
        $range = $null
        if ($null -ne $rule.'tcp-options') { $range = $rule.'tcp-options'.'destination-port-range' }
        if ($rule.protocol -ne '6' -or $null -eq $range -or $range.min -ne $range.max -or
            $rule.'source-type' -ne 'CIDR_BLOCK' -or $rule.'is-stateless') {
            $issues.Add('Unsupported/broad ingress rule; only stateful TCP 22/80/443 and limited ICMP are accepted.'); continue
        }
        if ($range.min -eq 22 -and $rule.source -eq $Cidr) { $ssh = $true; continue }
        if ($range.min -eq 80 -and $rule.source -eq '0.0.0.0/0') { $http = $true; continue }
        if ($range.min -eq 443 -and $rule.source -eq '0.0.0.0/0') { $https = $true; continue }
        $issues.Add('Ingress exposes a port/source outside the approved policy (SSH must be admin /32).')
    }
    if (-not $ssh) { $issues.Add('Missing TCP 22 restricted to the administrator /32.') }
    if (-not $http) { $issues.Add('Missing public TCP 80.') }
    if (-not $https) { $issues.Add('Missing public TCP 443.') }
    $issues.ToArray()
}

function Get-Preflight([string]$Tenancy, [string]$TargetCompartment, [string]$Cidr) {
    $blockers = [Collections.Generic.List[string]]::new()
    $subscriptions = @(Get-OciData @('iam','region-subscription','list','--tenancy-id',$Tenancy) -List)
    if (@($subscriptions | Where-Object { $_.'is-home-region' -and $_.'region-name' -eq $script:Region -and $_.status -eq 'READY' }).Count -ne 1) { throw 'Home region is not sa-saopaulo-1 and READY.' }
    $ads = @(Get-OciData @('iam','availability-domain','list','--compartment-id',$Tenancy) -List)
    if ($ads.Count -ne 1) { throw 'Unexpected availability domains; refusing an automatic architecture change.' }
    $ad = $ads[0].name
    $compartments = @($Tenancy) + @(Get-OciData @('iam','compartment','list','--compartment-id',$Tenancy,'--compartment-id-in-subtree','true','--access-level','ANY','--all') -List | Where-Object { $_.'lifecycle-state' -eq 'ACTIVE' } | ForEach-Object { $_.id })
    if ($TargetCompartment -notin $compartments) { throw 'Target compartment was not found in tenancy inventory.' }
    $instances = @(); $volumes = @()
    foreach ($compartment in $compartments) {
        $instances += @(Get-OciData @('compute','instance','list','--compartment-id',$compartment,'--all') -List | Where-Object { $_.'lifecycle-state' -ne 'TERMINATED' })
        $volumes += @(Get-OciData @('bv','volume','list','--compartment-id',$compartment,'--all') -List | Where-Object { $_.'lifecycle-state' -ne 'TERMINATED' })
        foreach ($domain in $ads) {
            $volumes += @(Get-OciData @('bv','boot-volume','list','--compartment-id',$compartment,'--availability-domain',$domain.name,'--all') -List | Where-Object { $_.'lifecycle-state' -ne 'TERMINATED' })
        }
        # A failed IAM/quota read blocks provisioning instead of assuming no limits.
        $null = @(Get-OciData @('limits','quota','list','--compartment-id',$compartment,'--all') -List)
    }
    $existing = @($instances | Where-Object { $_.'display-name' -eq $script:DisplayName -or ($null -ne $_.'freeform-tags' -and $null -ne $_.'freeform-tags'.PSObject.Properties['ia-portal-provision'] -and $_.'freeform-tags'.'ia-portal-provision' -eq $script:DisplayName) })
    if ($existing.Count -gt 1) { throw 'Multiple matching instances already exist. No launch.' }
    if ($existing.Count -eq 1) {
        if ($existing[0].shape -ne $script:Shape -or $existing[0].'shape-config'.ocpus -ne 2 -or $existing[0].'shape-config'.'memory-in-gbs' -ne 12 -or $existing[0].'compartment-id' -ne $TargetCompartment) { throw 'Existing target differs from the approved configuration. No launch.' }
        return [pscustomobject]@{ Existing = $existing[0]; Blockers = @(); AvailabilityDomain = $ad }
    }
    $a1 = @($instances | Where-Object shape -EQ $script:Shape)
    if ($a1.Count -gt 0) { $blockers.Add('Other A1 instances already consume the 2 OCPU/12 GB Always Free allowance (including stopped instances).') }
    $shapes = @(Get-OciData @('compute','shape','list','--compartment-id',$TargetCompartment,'--availability-domain',$ad,'--all') -List)
    $shape = @($shapes | Where-Object shape -EQ $script:Shape)
    if ($shape.Count -ne 1 -or $shape[0].'billing-type' -notin @('LIMITED_FREE','ALWAYS_FREE')) { $blockers.Add('A1 shape is not marked free-eligible by OCI.') }
    $limits = @(Get-OciData @('limits','value','list','--compartment-id',$Tenancy,'--service-name','compute','--all') -List)
    foreach ($name in @('standard-a1-core-count','standard-a1-core-regional-count','standard-a1-memory-count','standard-a1-memory-regional-count')) {
        $limit = @($limits | Where-Object { $_.name -eq $name -and ($_.'scope-type' -eq 'REGION' -or $_.'availability-domain' -eq $ad) })
        if ($limit.Count -ne 1) { throw 'Required A1 service limit not found.' }
        $limitArgs = @('limits','resource-availability','get','--compartment-id',$TargetCompartment,'--service-name','compute','--limit-name',$name)
        if ($limit[0].'scope-type' -eq 'AD') { $limitArgs += @('--availability-domain',$ad) }
        $available = Get-OciData $limitArgs
        $required = if ($name -like '*core*') { 2 } else { 12 }
        if ($available.available -lt $required) { $blockers.Add("Insufficient service limit/quota: $name.") }
    }
    $images = @(Get-OciData @('compute','image','list','--compartment-id',$TargetCompartment,'--operating-system','Canonical Ubuntu','--operating-system-version','24.04','--shape',$script:Shape,'--all') -List)
    $images = @($images | Where-Object { $_.'display-name' -match '^Canonical-Ubuntu-24\.04-aarch64-' -and $_.'lifecycle-state' -eq 'AVAILABLE' -and -not $_.'compartment-id' } | Sort-Object 'time-created' -Descending)
    if ($ImageId) { $images = @($images | Where-Object id -EQ $ImageId) }
    if ($images.Count -eq 0) { throw 'No official Ubuntu 24.04 aarch64 platform image compatible with A1.' }
    $image = $images[0]
    $compat = Get-OciData @('compute','image-shape-compatibility-entry','get','--image-id',$image.id,'--shape-name',$script:Shape)
    if ($compat.shape -ne $script:Shape) { throw 'Image/shape compatibility was not confirmed.' }
    # No custom size: OCI uses the platform-image default. Reserve at least 50 GB in checks.
    $bootBudget = [Math]::Max(50, [Math]::Ceiling($image.'size-in-mbs' / 1024))
    $volumeGB = 0
    foreach ($volume in $volumes) { $volumeGB += $volume.'size-in-gbs' }
    $freeStorage = Get-OciData @('limits','resource-availability','get','--compartment-id',$TargetCompartment,'--service-name','block-storage','--limit-name','total-free-storage-gb-regional')
    if ($volumeGB + $bootBudget -gt 200 -or $freeStorage.available -lt $bootBudget) { $blockers.Add('Boot + block storage would exceed the 200 GB free allowance or effective quota.') }
    # Historical usage is separate from current inventory, with a reporting delay.
    $now = [DateTime]::UtcNow
    $month = [DateTime]::new($now.Year,$now.Month,1,0,0,0,[DateTimeKind]::Utc)
    $end = $now.Date.AddDays(1)
    $usage = Get-OciData @('usage-api','usage-summary','request-summarized-usages','--tenant-id',$Tenancy,'--time-usage-started',$month.ToString('yyyy-MM-ddTHH:mm:ssZ'),'--time-usage-ended',$end.ToString('yyyy-MM-ddTHH:mm:ssZ'),'--granularity','MONTHLY','--query-type','USAGE','--group-by','["skuPartNumber","skuName","unit"]')
    if ($usage.PSObject.Properties.Name -contains 'next-page' -and $usage.'next-page') { throw 'Usage is paginated; manual billing review required.' }
    $priorA1 = @($usage.items | Where-Object { $_.'sku-name' -match '(?i)(Ampere|Standard.*A1)' -and $_.'computed-quantity' -gt 0 })
    if ($priorA1.Count -gt 0) { $blockers.Add('A1 usage already reported this month; review historical allowance before creating another full-month instance.') }
    $subnet = Get-OciData @('network','subnet','get','--subnet-id',$SubnetId)
    $vcn = Get-OciData @('network','vcn','get','--vcn-id',$subnet.'vcn-id')
    if ($subnet.'lifecycle-state' -ne 'AVAILABLE' -or $vcn.'lifecycle-state' -ne 'AVAILABLE' -or $subnet.'prohibit-public-ip-on-vnic' -or $subnet.'prohibit-internet-ingress') { $blockers.Add('Subnet/VCN unavailable or public IPv4 ingress prohibited.') }
    if ($subnet.'availability-domain' -and $subnet.'availability-domain' -ne $ad) { $blockers.Add('Subnet belongs to another availability domain.') }
    if ($subnet.'ipv6-cidr-blocks') { $blockers.Add('IPv6-enabled subnet requires a separate security review; this automation only approves IPv4.') }
    $route = Get-OciData @('network','route-table','get','--rt-id',$subnet.'route-table-id')
    if ($route.'vcn-id' -ne $vcn.id) { throw 'Route table does not belong to selected VCN.' }
    $default = @($route.'route-rules' | Where-Object { $_.destination -eq '0.0.0.0/0' -and $_.'destination-type' -eq 'CIDR_BLOCK' })
    if ($default.Count -ne 1 -or $default[0].'network-entity-id' -notmatch '^ocid1\.internetgateway\.') { $blockers.Add('Missing unique default IPv4 route to an Internet Gateway.') }
    else {
        $gateway = Get-OciData @('network','internet-gateway','get','--ig-id',$default[0].'network-entity-id')
        if (-not $gateway.'is-enabled' -or $gateway.'lifecycle-state' -ne 'AVAILABLE' -or $gateway.'vcn-id' -ne $vcn.id -or $gateway.'route-table-id') { $blockers.Add('Internet Gateway disabled, wrong VCN, or advanced ingress routing requires review.') }
    }
    $ingress = @(); $egress = @()
    foreach ($id in $subnet.'security-list-ids') {
        $list = Get-OciData @('network','security-list','get','--security-list-id',$id)
        if ($list.'vcn-id' -ne $vcn.id -or $list.'lifecycle-state' -ne 'AVAILABLE') { throw 'Security list is not available in selected VCN.' }
        $ingress += @($list.'ingress-security-rules'); $egress += @($list.'egress-security-rules')
    }
    foreach ($issue in @(Test-IngressRules $ingress $Cidr)) { $blockers.Add($issue) }
    if (@($egress | Where-Object { $_.protocol -eq 'all' -and $_.destination -eq '0.0.0.0/0' -and -not $_.'is-stateless' }).Count -eq 0) { $blockers.Add('Expected stateful outbound internet access not found; review updates/DNS/LLM egress.') }
    [pscustomobject]@{ Existing = $null; Blockers = $blockers.ToArray(); AvailabilityDomain = $ad; Image = $image; StorageUsedGB = $volumeGB; BootBudgetGB = $bootBudget; InstanceCount = $instances.Count }
}

function Get-LaunchOutcome($Result) {
    if ($Result.ExitCode -eq 0 -and $null -ne $Result.Json -and $null -ne $Result.Json.PSObject.Properties['data'] -and
        $null -ne $Result.Json.data -and $null -ne $Result.Json.data.PSObject.Properties['id'] -and $Result.Json.data.id) { return 'Accepted' }
    # Only an authenticated OCI ServiceError with explicit capacity wording can retry.
    $match = [regex]::Match($Result.ErrorText, '(?s)ServiceError:\s*(\{.*\})\s*$')
    if ($match.Success) {
        try {
            $errorData = $match.Groups[1].Value | ConvertFrom-Json
            if ($errorData.code -eq 'InternalError' -and $errorData.status -eq 500 -and $errorData.message -match '(?i)out of (?:host )?capacity' -and $errorData.message -notmatch '(?i)quota|limit|billing|payment|auth|permission') { return 'Capacity' }
            if ($errorData.status -ge 500 -and $errorData.message -notmatch '(?i)quota|limit|billing|payment|auth|permission') { return 'Ambiguous' }
            return 'Fatal'
        } catch { return 'Ambiguous' }
    }
    'Ambiguous'
}

function Save-Journal($Journal) {
    $path = Join-Path $StateDirectory 'journal.json'
    $temp = Join-Path $StateDirectory 'journal.tmp'
    [IO.File]::WriteAllText($temp, ($Journal | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temp -Destination $path -Force
}

function Wait-ExistingInstance($Instance) {
    Write-OperationLog ('Instance already accepted/found: {0}; state={1}. No further launch.' -f $Instance.id, $Instance.'lifecycle-state')
    for ($i = 0; $i -lt 80; $i++) {
        if ($Instance.'lifecycle-state' -eq 'RUNNING') {
            $attachments = @(Get-OciData @('compute','vnic-attachment','list','--compartment-id',$Instance.'compartment-id','--instance-id',$Instance.id,'--all') -List)
            foreach ($attachment in $attachments | Where-Object { $_.'lifecycle-state' -eq 'ATTACHED' }) {
                $vnic = Get-OciData @('network','vnic','get','--vnic-id',$attachment.'vnic-id')
                if ($vnic.'is-primary') {
                    if ($vnic.'subnet-id' -ne $SubnetId -or -not $vnic.'public-ip') { throw 'Existing instance has unexpected subnet or no public IPv4. No new instance.' }
                    Write-OperationLog ('SUCCESS: RUNNING; public IPv4={0}. No application deployed.' -f $vnic.'public-ip')
                    return
                }
            }
            throw 'RUNNING but no primary VNIC found. No new instance.'
        }
        if ($Instance.'lifecycle-state' -notin @('PROVISIONING','STARTING')) { throw 'Existing instance is not running/provisioning. Resolve manually; no duplicate launch.' }
        Start-Sleep -Seconds 15
        $Instance = Get-OciData @('compute','instance','get','--instance-id',$Instance.id)
    }
    throw 'Instance still provisioning after 20 minutes. Rerun to observe, never create another.'
}

function Invoke-Provision {
    if ($Mode -eq 'Provision' -and -not $ProvisionApproved) { throw 'Provision requires explicit prior authorization and -ProvisionApproved. Use -Mode DryRun first.' }
    [void][IO.Directory]::CreateDirectory($StateDirectory)
    $lock = $null; $requestFile = Join-Path $StateDirectory 'launch-request.json'
    try {
        # Shared state/lock across profiles/compartments on this Windows account.
        $lock = [IO.File]::Open((Join-Path $StateDirectory 'provision.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $settings = Read-LocalSettings
        $tenancy = $settings['tenancy']
        $target = if ($CompartmentId) { $CompartmentId } else { $tenancy }
        $publicKey = Resolve-PublicKey
        $cidr = Resolve-AdminCidr
        while ($true) {
            Write-OperationLog 'Preflight: authenticated reads, tenancy inventory, free limits, image and network.'
            $preflight = Get-Preflight $tenancy $target $cidr
            if ($null -ne $preflight.Existing) {
                if ($Mode -eq 'Provision') { Wait-ExistingInstance $preflight.Existing }
                return [pscustomobject]@{ Status = 'Existing'; InstanceId = $preflight.Existing.id; State = $preflight.Existing.'lifecycle-state' }
            }
            $plan = [pscustomobject]@{ Status = if ($preflight.Blockers.Count) { 'Blocked' } else { 'Ready' }; Region = $script:Region; AvailabilityDomain = $preflight.AvailabilityDomain; Shape = $script:Shape; Ocpus = 2; MemoryGB = 12; DisplayName = $script:DisplayName; SubnetId = $SubnetId; AssignPublicIp = $true; AdminCidr = $cidr; ImageId = $preflight.Image.id; ImageName = $preflight.Image.'display-name'; BootVolume = 'Image default (no custom size), balanced 10 VPU/GB'; BootBudgetGB = $preflight.BootBudgetGB; StorageUsedGB = $preflight.StorageUsedGB; Blockers = $preflight.Blockers }
            [IO.File]::WriteAllText((Join-Path $StateDirectory 'dry-run.json'), ($plan | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
            Write-OperationLog ('Preflight result: {0}; blockers={1}.' -f $plan.Status, $preflight.Blockers.Count)
            if ($Mode -eq 'DryRun') { return $plan }
            if ($preflight.Blockers.Count) { throw 'Preflight blocked. Review local dry-run.json; no remote change.' }
            $journalPath = Join-Path $StateDirectory 'journal.json'
            if (Test-Path -LiteralPath $journalPath) {
                $journal = Get-Content -LiteralPath $journalPath -Raw | ConvertFrom-Json
                if ($journal.Outcome -ne 'Capacity' -or $journal.Tenancy -ne $tenancy -or $journal.Compartment -ne $target -or $journal.Subnet -ne $SubnetId -or $journal.Image -ne $plan.ImageId) { throw 'Journal requires reconciliation. No new launch; do not delete it without reviewing OCI instances and orphaned volumes.' }
                $remaining = $script:IntervalSeconds - ([DateTime]::UtcNow - [DateTime]::Parse($journal.At).ToUniversalTime()).TotalSeconds
                if ($remaining -gt 0) { Start-Sleep -Seconds ([Math]::Ceiling($remaining)); continue }
            }
            $request = @{ availabilityDomain = $preflight.AvailabilityDomain; compartmentId = $target; displayName = $script:DisplayName; shape = $script:Shape; shapeConfig = @{ ocpus = 2; memoryInGBs = 12 }; subnetId = $SubnetId; assignPublicIp = $true; assignIpv6Ip = $false; sourceDetails = @{ sourceType = 'image'; imageId = $plan.ImageId; bootVolumeVpusPerGB = 10 }; metadata = @{ ssh_authorized_keys = $publicKey }; freeformTags = @{ 'ia-portal-provision' = $script:DisplayName }; instanceOptions = @{ areLegacyImdsEndpointsDisabled = $true } }
            [IO.File]::WriteAllText($requestFile, ($request | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
            $journal = @{ Outcome = 'Pending'; At = [DateTime]::UtcNow.ToString('o'); Tenancy = $tenancy; Compartment = $target; Subnet = $SubnetId; Image = $plan.ImageId }
            Save-Journal $journal # Durable BEFORE POST: Ctrl+C/timeouts cannot silently trigger a duplicate.
            Write-OperationLog 'Launching exactly one A1 instance (2 OCPUs/12 GB); OCI automatic retries disabled.'
            $result = Invoke-Oci @('compute','instance','launch','--from-json',('file://' + $requestFile))
            $outcome = Get-LaunchOutcome $result
            if ($outcome -eq 'Accepted') {
                $journal.Outcome = 'Accepted'; $journal.InstanceId = $result.Json.data.id; Save-Journal $journal
                Wait-ExistingInstance $result.Json.data
                return [pscustomobject]@{ Status = 'Success'; InstanceId = $result.Json.data.id }
            }
            if ($outcome -eq 'Capacity') {
                $journal.Outcome = 'Capacity'; Save-Journal $journal
                Write-OperationLog 'Explicit capacity exhaustion; next preflight/attempt in 30 minutes. Ctrl+C stops.'
                Start-Sleep -Seconds $script:IntervalSeconds
                continue
            }
            $journal.Outcome = $outcome; Save-Journal $journal
            if ($outcome -eq 'Fatal') { throw 'Fatal OCI launch error; stopped immediately without retry. Review journal and OCI Console.' }
            # Bounded read-only reconciliation, never resubmit an ambiguous request.
            for ($i = 0; $i -lt 3; $i++) {
                $recheck = Get-Preflight $tenancy $target $cidr
                if ($null -ne $recheck.Existing) { Wait-ExistingInstance $recheck.Existing; return [pscustomobject]@{ Status = 'Reconciled'; InstanceId = $recheck.Existing.id } }
                if ($i -lt 2) { Start-Sleep -Seconds 15 }
            }
            throw 'Launch failed or response ambiguous. Reconciliation did not confirm success; journal latched. No automatic resubmission.'
        }
    } finally {
        if ($null -ne $lock) {
            if (Test-Path -LiteralPath $requestFile) { Remove-Item -LiteralPath $requestFile -Force }
            $lock.Dispose()
        }
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    try {
        $result = Invoke-Provision
        $result | ConvertTo-Json -Depth 10
        if ($result.Status -eq 'Blocked') { exit 2 }
    } catch {
        # Controlled exceptions only. Never print full native CLI output.
        Write-Error $_.Exception.Message
        exit 1
    }
}

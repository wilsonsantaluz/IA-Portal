[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidatePattern('^(?=.{1,253}$)[a-zA-Z0-9](?:[a-zA-Z0-9.-]*[a-zA-Z0-9])?\.[a-zA-Z]{2,63}$')][string]$Domain,
    [string]$ExpectedIPv6,
    [string]$ExpectedIPv4,
    [string]$StateDirectory=(Join-Path $env:LOCALAPPDATA 'IA-Portal\https'),
    [switch]$Staging,
    [switch]$OpenFirewall,
    [switch]$Start
)
$ErrorActionPreference='Stop'
$state=[IO.Path]::GetFullPath($StateDirectory)
$binary=Join-Path $state 'caddy.exe'
if(!(Test-Path -LiteralPath $binary)){throw 'Run install-caddy.ps1 first.'}
$caddyfile=Join-Path $PSScriptRoot 'Caddyfile'
$candidates=@(Get-NetIPAddress -AddressFamily IPv6 | Where-Object {
    $_.AddressState -eq 'Preferred' -and $_.SuffixOrigin -ne 'Random' -and $_.IPAddress -match '^[23][0-9a-fA-F]{3}:'
})
if(!$ExpectedIPv6 -and $candidates.Count -eq 1){$ExpectedIPv6=$candidates[0].IPAddress}
if($ExpectedIPv6 -and !($candidates | Where-Object { $_.IPAddress -eq $ExpectedIPv6 })){throw 'Expected IPv6 is not an assigned, preferred, non-temporary global address.'}
if($ExpectedIPv4){
    $parsed=[Net.IPAddress]::Parse($ExpectedIPv4)
    if($parsed.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork -or $ExpectedIPv4 -match '^(127\.|10\.|192\.168\.|169\.254\.|172\.(1[6-9]|2[0-9]|3[01])\.|100\.(6[4-9]|[7-9][0-9]|1[01][0-9]|12[0-7])\.)'){throw 'Expected IPv4 must be a public routed address, not CGNAT/private.'}
}
$blockers=[Collections.Generic.List[string]]::new()
function Addresses([string]$Type) {
    try { @(Resolve-DnsName -Name $Domain -Type $Type -Server 1.1.1.1 -DnsOnly -ErrorAction Stop | Where-Object IPAddress | Select-Object -ExpandProperty IPAddress) }
    catch { @() }
}
$aaaa=@(Addresses 'AAAA');$a=@(Addresses 'A')
if(!$a.Count -and !$aaaa.Count){$blockers.Add('No public A/AAAA record was resolved.')}
if($aaaa.Count -and (!$ExpectedIPv6 -or @($aaaa | Where-Object { $_ -ne $ExpectedIPv6 }).Count)){$blockers.Add('AAAA does not exclusively match this machine expected IPv6.')}
if($ExpectedIPv6 -and !$aaaa.Count){$blockers.Add('AAAA missing for the expected local IPv6.')}
if($a.Count -and (!$ExpectedIPv4 -or @($a | Where-Object { $_ -ne $ExpectedIPv4 }).Count)){$blockers.Add('A still targets another host/unverified IPv4. Remove it for IPv6-only operation.')}
$runnerPath=Join-Path $state 'runner.json';$owned=$null
if(Test-Path -LiteralPath $runnerPath){
    $saved=Get-Content -LiteralPath $runnerPath -Raw | ConvertFrom-Json
    $owned=Get-Process -Id $saved.ProcessId -ErrorAction SilentlyContinue
    if($owned -and ($owned.Path -ne $binary -or $owned.StartTime.ToUniversalTime().ToString('o') -ne $saved.StartedUtc)){throw 'Saved PID belongs to another process; refusing to act on it.'}
    if($owned -and ($saved.Domain -ne $Domain -or [bool]$saved.Staging -ne [bool]$Staging)){throw 'Owned Caddy is running with a different domain/issuer. Stop that process explicitly first.'}
}
if(!$owned){
    foreach($port in @(80,443)){if(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue){$blockers.Add("TCP $port is already in use.")}}
}
try { $ready=Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:9089/health/ready' -TimeoutSec 5; if($ready.StatusCode -ne 200){$blockers.Add('Gateway not ready.')} }
catch {$blockers.Add('Local gateway readiness failed.')}
$env:PORTAL_SITE=$Domain
$env:PORTAL_TLS_STORAGE=Join-Path $state $(if($Staging){'staging-certs'}else{'certs'})
$env:PORTAL_ACME_CA=if($Staging){'https://acme-staging-v02.api.letsencrypt.org/directory'}else{'https://acme-v02.api.letsencrypt.org/directory'}
& $binary validate --config $caddyfile --adapter caddyfile
if($LASTEXITCODE -ne 0){throw 'Caddy configuration invalid.'}
[pscustomobject]@{Domain=$Domain;ExpectedIPv6=$ExpectedIPv6;A=$a;AAAA=$aaaa;Blockers=@($blockers);Mode=$(if($Start){'Start requested'}else{'Validation only'})} | ConvertTo-Json -Depth 3
if(!$Start){return}
if($blockers.Count){throw 'HTTPS not started. Resolve the reported DNS/local blockers first.'}
if($owned){Write-Output "Owned Caddy already running (PID $($owned.Id)); no duplicate started.";return}
if($OpenFirewall){
    $ruleName='IA-Portal-Caddy-HTTPS'
    $existing=Get-NetFirewallRule -Name $ruleName -ErrorAction SilentlyContinue
    if($existing){
        $app=$existing | Get-NetFirewallApplicationFilter
        $port=$existing | Get-NetFirewallPortFilter
        if($app.Program -ne $binary -or $port.Protocol -ne 'TCP' -or ($port.LocalPort -join ',') -ne '80,443'){throw 'Existing named firewall rule differs; refusing to replace it.'}
    }else{New-NetFirewallRule -Name $ruleName -DisplayName 'IA-Portal Caddy TCP 80/443' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 80,443 -Program $binary -Profile Public,Private | Out-Null}
}
$process=Start-Process -FilePath $binary -ArgumentList @('run','--config',('"'+$caddyfile+'"'),'--adapter','caddyfile') -WindowStyle Hidden -PassThru -WorkingDirectory $state -RedirectStandardOutput (Join-Path $state 'stdout.log') -RedirectStandardError (Join-Path $state 'stderr.log')
Start-Sleep -Seconds 2
if($process.HasExited){throw 'Caddy stopped during startup; inspect its private logs.'}
@{ProcessId=$process.Id;StartedUtc=$process.StartTime.ToUniversalTime().ToString('o');Domain=$Domain;Staging=[bool]$Staging;Config=$caddyfile} | ConvertTo-Json | Set-Content -LiteralPath $runnerPath -Encoding UTF8
Write-Output "Caddy running (PID $($process.Id)). Public certificate/reachability still require an external TLS check."

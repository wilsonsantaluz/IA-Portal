param([string]$StateDirectory=(Join-Path $env:LOCALAPPDATA 'IA-Portal\https'))
$ErrorActionPreference='Stop'
$temp=Join-Path $env:TEMP ('portal-https-tests-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
Copy-Item -LiteralPath (Join-Path $StateDirectory 'caddy.exe') -Destination $temp
$global:PortalHttpsFixtureA=@();$global:PortalHttpsFixtureAAAA=@('2001:db8::1')
function Get-NetIPAddress {param($AddressFamily) [pscustomobject]@{AddressState='Preferred';SuffixOrigin='Link';IPAddress='2001:db8::1'}}
function Resolve-DnsName {param($Name,$Type,$Server,[switch]$DnsOnly,$ErrorAction) if($Type -eq 'AAAA'){$global:PortalHttpsFixtureAAAA | ForEach-Object {[pscustomobject]@{IPAddress=$_}}}else{$global:PortalHttpsFixtureA | ForEach-Object {[pscustomobject]@{IPAddress=$_}}}}
function Get-NetTCPConnection {param($LocalPort,$State,$ErrorAction)}
function Invoke-WebRequest {param([switch]$UseBasicParsing,$Uri,$TimeoutSec) [pscustomobject]@{StatusCode=200}}
try {
    $target=Join-Path $PSScriptRoot 'start-https.ps1'
    $global:PortalHttpsFixtureA=@('192.0.2.1')
    try{& $target -Domain portal.example.test -StateDirectory $temp -Start;throw 'Expected stale-A denial.'}catch{if($_.Exception.Message -notmatch 'Resolve the reported'){throw}}
    $global:PortalHttpsFixtureA=@();$global:PortalHttpsFixtureAAAA=@()
    try{& $target -Domain portal.example.test -StateDirectory $temp -Start;throw 'Expected missing-AAAA denial.'}catch{if($_.Exception.Message -notmatch 'Resolve the reported'){throw}}
    $global:PortalHttpsFixtureAAAA=@('2001:db8::2')
    try{& $target -Domain portal.example.test -StateDirectory $temp -Start;throw 'Expected foreign-AAAA denial.'}catch{if($_.Exception.Message -notmatch 'Resolve the reported'){throw}}
    $global:PortalHttpsFixtureAAAA=@('2001:db8::1')
    $valid=(& $target -Domain portal.example.test -StateDirectory $temp | Out-String)
    if($valid -notmatch '"Blockers":\s*\[\s*\]'){throw 'Valid DNS fixture unexpectedly blocked.'}
    if(Test-Path -LiteralPath (Join-Path $temp 'runner.json')){throw 'Validation started a process.'}
    try{& $target -Domain portal.example.test -StateDirectory $temp -ExpectedIPv4 100.64.0.1;throw 'Expected CGNAT denial.'}catch{if($_.Exception.Message -notmatch 'CGNAT/private'){throw}}
    Write-Output 'PASS: stale A, missing/foreign AAAA and CGNAT denied; valid DNS dry-run does not start a listener or request a certificate.'
}finally{
    Remove-Variable -Name PortalHttpsFixtureA,PortalHttpsFixtureAAAA -Scope Global -ErrorAction SilentlyContinue
    # Remove only this explicitly created temporary fixture, never a computed parent.
    $resolved=[IO.Path]::GetFullPath($temp)
    if($resolved.StartsWith([IO.Path]::GetFullPath($env:TEMP)+[IO.Path]::DirectorySeparatorChar) -and [IO.Path]::GetFileName($resolved) -like 'portal-https-tests-*'){
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}

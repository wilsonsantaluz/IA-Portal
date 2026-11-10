param([Parameter(Mandatory=$true)][string]$KeyPath)
$ErrorActionPreference='Stop'
$runner=Join-Path $PSScriptRoot 'reverse-tunnel.ps1'
$plan=(& $runner -RemoteHost example.test -KeyPath $KeyPath | Out-String | ConvertFrom-Json)
foreach($required in @('StrictHostKeyChecking=yes','ExitOnForwardFailure=yes','ServerAliveInterval=30','127.0.0.1:19080:127.0.0.1:9080','127.0.0.1:19089:127.0.0.1:9089')){
    if($required -notin $plan.Arguments){throw "Missing required tunnel argument: $required"}
}
if($plan.Arguments -match '0\.0\.0\.0|GatewayPorts=yes|StrictHostKeyChecking=no'){throw 'Unsafe binding or host verification.'}
try{& $runner -RemoteHost 'example.test;invalid' -KeyPath $KeyPath;throw 'Expected host validation denial.'}catch{if($_.FullyQualifiedErrorId -notmatch 'ParameterArgumentValidationError'){throw}}
$temp=Join-Path $env:TEMP ('portal-tunnel-tests-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
try{
    @{ProcessId=$PID;StartedUtc='mismatched';Script='not-this-process';SshProcessId=$null} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $temp 'runner.json')
    try{& (Join-Path $PSScriptRoot 'stop-reverse-tunnel.ps1') -StateDirectory $temp;throw 'Expected runner identity denial.'}catch{if($_.Exception.Message -ne 'Runner identity mismatch.'){throw}}
    Write-Output 'PASS: validation makes no connection; loopback forwards, host/key checks and keepalive enforced; shell-like host rejected; unrelated process stop denied.'
}finally{
    $resolved=[IO.Path]::GetFullPath($temp)
    if($resolved.StartsWith([IO.Path]::GetFullPath($env:TEMP)+[IO.Path]::DirectorySeparatorChar) -and [IO.Path]::GetFileName($resolved) -like 'portal-tunnel-tests-*'){
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}

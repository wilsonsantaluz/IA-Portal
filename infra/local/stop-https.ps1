[CmdletBinding()]
param([string]$StateDirectory=(Join-Path $env:LOCALAPPDATA 'IA-Portal\https'))
$ErrorActionPreference='Stop'
$state=[IO.Path]::GetFullPath($StateDirectory)
$runner=Join-Path $state 'runner.json'
if(!(Test-Path -LiteralPath $runner)){Write-Output 'No managed HTTPS process recorded.';return}
$saved=Get-Content -LiteralPath $runner -Raw | ConvertFrom-Json
$process=Get-Process -Id $saved.ProcessId -ErrorAction SilentlyContinue
if(!$process){Write-Output 'Managed HTTPS process is already stopped.';return}
if($process.Path -ne (Join-Path $state 'caddy.exe') -or $process.StartTime.ToUniversalTime().ToString('o') -ne $saved.StartedUtc){throw 'PID identity mismatch; no process stopped.'}
Stop-Process -Id $process.Id
Write-Output 'Stopped only the managed local HTTPS edge. Containers, DNS and certificates preserved.'

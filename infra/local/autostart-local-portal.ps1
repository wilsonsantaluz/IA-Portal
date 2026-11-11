[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-zA-Z0-9.-]+$')][string]$RemoteHost,
    [Parameter(Mandatory=$true)][string]$KeyPath,
    [Parameter(Mandatory=$true)][string]$DockerDesktopPath
)
$ErrorActionPreference='Stop'
$desktop=(Resolve-Path -LiteralPath $DockerDesktopPath).Path
$running=Get-Process -Name 'Docker Desktop' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $desktop }
if(!$running){Start-Process -FilePath $desktop -WindowStyle Hidden | Out-Null}
# The existing runner waits/retries gateway readiness while Docker restores the
# unless-stopped containers. Its mutex also rejects a duplicate logon invocation.
& (Join-Path $PSScriptRoot 'reverse-tunnel.ps1') -RemoteHost $RemoteHost -KeyPath $KeyPath -Run

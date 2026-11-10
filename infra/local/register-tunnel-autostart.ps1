[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-zA-Z0-9.-]+$')][string]$RemoteHost,
    [Parameter(Mandatory=$true)][string]$KeyPath,
    [string]$DockerDesktopPath,
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$TaskName='IA-Portal-ReverseTunnel',
    [switch]$Register
)
$ErrorActionPreference='Stop'
$key=(Resolve-Path -LiteralPath $KeyPath).Path
$sourceRunner=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot 'autostart-local-portal.ps1')).Path
$installRoot=Join-Path $env:LOCALAPPDATA 'IA-Portal\reverse-tunnel\installed'
$runner=Join-Path $installRoot 'autostart-local-portal.ps1'
if(!$DockerDesktopPath){$DockerDesktopPath=Get-Process -Name 'Docker Desktop' -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Path}
if(!$DockerDesktopPath){$entry=(Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue).'Docker Desktop';if($entry){$DockerDesktopPath=$entry.Trim('"')}}
if(!$DockerDesktopPath){throw 'Provide the installed Docker Desktop executable path.'}
$desktop=(Resolve-Path -LiteralPath $DockerDesktopPath).Path
foreach($path in @($key,$runner,$desktop)){if($path.Contains('"') -or $path.Contains("`n") -or $path.Contains("`r")){throw 'Unsupported path characters.'}}
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
$executable=Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
$arguments='-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "'+$runner+'" -RemoteHost '+$RemoteHost+' -KeyPath "'+$key+'" -DockerDesktopPath "'+$desktop+'"'
$marker='IA-Portal managed reverse SSH tunnel; current-user logon, no stored password.'
$action=New-ScheduledTaskAction -Execute $executable -Argument $arguments -WorkingDirectory (Split-Path $runner)
$trigger=New-ScheduledTaskTrigger -AtLogOn -User $identity.Name
$principal=New-ScheduledTaskPrincipal -UserId $identity.Name -LogonType Interactive -RunLevel Limited
$settings=New-ScheduledTaskSettingsSet -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
$definition=New-ScheduledTask -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description $marker
if(!$Register){$definition | Select-Object Description,Actions,Triggers,Principal,Settings;Write-Output 'Validation only; task not registered.';return}
$existing=Get-ScheduledTask -TaskName $TaskName -TaskPath '\' -ErrorAction SilentlyContinue
$matching=$false
if($existing){
    if($existing.Description -ne $marker){throw 'An unrelated task uses this name; refusing to overwrite.'}
    $existingUser=$existing.Principal.UserId
    try{$existingSid=([Security.Principal.NTAccount]::new($existingUser)).Translate([Security.Principal.SecurityIdentifier]).Value}catch{$existingSid=$existingUser}
    if($existingSid -ne $identity.User.Value){throw 'Existing task belongs to another identity.'}
    if($existing.Actions.Execute -eq $executable -and $existing.Actions.Arguments -eq $arguments -and $existing.Settings.ExecutionTimeLimit -eq 'PT0S'){
        $matching=$true
    }
}
New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
foreach($name in @('autostart-local-portal.ps1','reverse-tunnel.ps1','stop-reverse-tunnel.ps1')){
    $source=Join-Path $PSScriptRoot $name;$target=Join-Path $installRoot $name
    if(!(Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash){Copy-Item -LiteralPath $source -Destination $target -Force}
}
if($matching){Write-Output 'Matching managed autostart task already registered; installed scripts refreshed without interrupting the running process.';return}
Register-ScheduledTask -TaskName $TaskName -TaskPath '\' -InputObject $definition -Force | Select-Object TaskName,State
Write-Output 'Registered for current-user logon without storing a password. Starts Docker Desktop if needed, then waits for gateway and connects SSH.'

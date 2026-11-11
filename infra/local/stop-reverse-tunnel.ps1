param([string]$StateDirectory=(Join-Path $env:LOCALAPPDATA 'IA-Portal\reverse-tunnel'))
$ErrorActionPreference='Stop'
$state=Get-Content -LiteralPath (Join-Path $StateDirectory 'runner.json') -Raw | ConvertFrom-Json
function Owned($id,$started,$path){
    if(!$id){return $null}
    $p=Get-Process -Id $id -ErrorAction SilentlyContinue
    if(!$p){return $null}
    if($p.Path -ne $path -or $p.StartTime.ToUniversalTime().ToString('o') -ne $started){throw 'Process identity mismatch; no unrelated process stopped.'}
    return $p
}
$parent=Get-Process -Id $state.ProcessId -ErrorAction SilentlyContinue
if($parent){
    $cmd=Get-CimInstance Win32_Process -Filter "ProcessId=$($state.ProcessId)"
    if(!$cmd -or $parent.HasExited){$parent=$null}
    else{
        $started=$parent.StartTime
        if(!$started -or !$cmd.CommandLine -or $started.ToUniversalTime().ToString('o') -ne $state.StartedUtc -or $state.Script -isnot [string] -or ![IO.Path]::IsPathRooted($state.Script)){throw 'Runner identity mismatch.'}
        $launcher=Join-Path (Split-Path -Parent $state.Script) 'autostart-local-portal.ps1'
        if(!$cmd.CommandLine.Contains($state.Script) -and !$cmd.CommandLine.Contains($launcher)){throw 'Runner identity mismatch.'}
    }
}
$child=Owned $state.SshProcessId $state.SshStartedUtc $state.SshExecutable
if($parent){Stop-Process -Id $parent.Id}
if($child){Stop-Process -Id $child.Id}
Write-Output 'Stopped only the recorded runner/SSH child. Public edge returns unavailable until the tunnel resumes.'

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-zA-Z0-9.-]+$')][string]$RemoteHost,
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$RemoteUser='opc',
    [Parameter(Mandatory=$true)][string]$KeyPath,
    [string]$StateDirectory=(Join-Path $env:LOCALAPPDATA 'IA-Portal\reverse-tunnel'),
    [switch]$Run
)
$ErrorActionPreference='Stop'
$key=(Resolve-Path -LiteralPath $KeyPath).Path
$ssh=(Get-Command ssh.exe -ErrorAction Stop).Source
$arguments=@('-N','-T','-i',$key,'-o','BatchMode=yes','-o','StrictHostKeyChecking=yes',
    '-o','ExitOnForwardFailure=yes','-o','ServerAliveInterval=30','-o','ServerAliveCountMax=3',
    '-o','ConnectTimeout=15','-R','127.0.0.1:19080:127.0.0.1:9080',
    '-R','127.0.0.1:19089:127.0.0.1:9089',"$RemoteUser@$RemoteHost")
if(!$Run){[pscustomobject]@{Executable=$ssh;Arguments=$arguments;Mode='Validation only; no connection opened'} | ConvertTo-Json;return}
$state=[IO.Path]::GetFullPath($StateDirectory)
New-Item -ItemType Directory -Path $state -Force | Out-Null
$mutex=[Threading.Mutex]::new($false,'Local\IA-Portal-ReverseTunnel')
try{$acquired=$mutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{$acquired=$true}
if(!$acquired){Write-Output 'A tunnel runner already owns the lock; no duplicate started.';$mutex.Dispose();return}
$child=$null
function Save-State {
    $self=Get-Process -Id $PID
    @{ProcessId=$PID;StartedUtc=$self.StartTime.ToUniversalTime().ToString('o');RemoteHost=$RemoteHost;RemoteUser=$RemoteUser;
      SshProcessId=$(if($child){$child.Id}else{$null});SshStartedUtc=$(if($child){$child.StartTime.ToUniversalTime().ToString('o')}else{$null});
      Script=$PSCommandPath;SshExecutable=$ssh} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $state 'runner.json') -Encoding UTF8
}
try {
    while($true){
        try{
            $ready=Invoke-WebRequest -UseBasicParsing -Uri http://127.0.0.1:9089/health/ready -TimeoutSec 5
            if($ready.StatusCode -ne 200){throw 'Local gateway not ready.'}
            # Start-Process joins arguments; quote only the validated existing key path.
            $childArgs=$arguments.Clone();$childArgs[3]='"'+$key+'"'
            if($key.Contains('"') -or $key.Contains("`n")){throw 'Unsupported key path characters.'}
            $child=Start-Process -FilePath $ssh -ArgumentList $childArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $state 'ssh.stdout.log') -RedirectStandardError (Join-Path $state 'ssh.stderr.log')
            Save-State
            Add-Content -LiteralPath (Join-Path $state 'operations.log') -Value ((Get-Date).ToUniversalTime().ToString('o')+' SSH attempt started; remote binds are loopback only.')
            $null=$child.Handle
            $child.WaitForExit()
            $child.Refresh()
            Add-Content -LiteralPath (Join-Path $state 'operations.log') -Value ((Get-Date).ToUniversalTime().ToString('o')+" SSH exited ($($child.ExitCode)); retry in 15 seconds.")
            $child.Dispose();$child=$null
        }catch{Add-Content -LiteralPath (Join-Path $state 'operations.log') -Value ((Get-Date).ToUniversalTime().ToString('o')+' Local/tunnel startup unavailable; details omitted; retry in 15 seconds.')}
        Save-State
        Start-Sleep -Seconds 15
    }
}finally{
    if($child -and !$child.HasExited){$child.Kill()}
    $mutex.ReleaseMutex();$mutex.Dispose()
}

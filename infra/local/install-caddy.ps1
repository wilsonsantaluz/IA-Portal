[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='2.11.9',
    [string]$StateDirectory=(Join-Path $env:LOCALAPPDATA 'IA-Portal\https')
)
$ErrorActionPreference='Stop'
$state=[IO.Path]::GetFullPath($StateDirectory)
New-Item -ItemType Directory -Path $state -Force | Out-Null
$acl=Get-Acl -LiteralPath $state
$acl.SetAccessRuleProtection($true,$false)
$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User
$acl.SetOwner($sid)
foreach($identity in @($sid,[Security.Principal.SecurityIdentifier]::new('S-1-5-18'),[Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))){
    $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($identity,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))
}
Set-Acl -LiteralPath $state -AclObject $acl
$target=Join-Path $state 'caddy.exe'
$asset="caddy_${Version}_windows_amd64.zip"
$base="https://github.com/caddyserver/caddy/releases/download/v$Version"
$zipPath=Join-Path $state $asset
$checksums=(Invoke-WebRequest -UseBasicParsing -Uri "$base/caddy_${Version}_checksums.txt").Content
if($checksums -is [byte[]]){$checksums=[Text.Encoding]::UTF8.GetString($checksums)}
$line=@($checksums -split "`n" | Where-Object { $_.Trim() -match ('^[a-fA-F0-9]{128}\s+\*?'+[regex]::Escape($asset)+'$') })
if($line.Count -ne 1){throw 'Official checksum entry missing or ambiguous.'}
$expected=($line[0].Trim() -split '\s+')[0]
if(!(Test-Path -LiteralPath $zipPath)) { Invoke-WebRequest -UseBasicParsing -Uri "$base/$asset" -OutFile $zipPath }
if((Get-FileHash -LiteralPath $zipPath -Algorithm SHA512).Hash -ne $expected){throw 'Caddy download checksum mismatch; refusing extraction.'}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $entry=$archive.GetEntry('caddy.exe')
    if(!$entry){throw 'Expected executable missing.'}
    if(Test-Path -LiteralPath $target){
        $running=Get-Process -Name caddy -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $target }
        if($running){throw 'Caddy is running. Stop the owned process before replacing its executable.'}
    }
    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$target,$true)
} finally { $archive.Dispose() }
& $target version
if($LASTEXITCODE -ne 0){throw 'Caddy executable verification failed.'}
Write-Output 'Installed from the official release with matching SHA512; no service or certificate installed.'

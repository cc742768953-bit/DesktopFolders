param([string]$InstallDirectory=(Join-Path $env:LOCALAPPDATA 'DesktopFolders'),[switch]$StartWithWindows,[switch]$NoLaunch)
$ErrorActionPreference='Stop'
if(![Environment]::Is64BitOperatingSystem){throw 'DesktopFolders requires 64-bit Windows.'}
$release=(Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -Name Release -ErrorAction SilentlyContinue).Release
if($release -lt 528040){throw 'Install Microsoft .NET Framework 4.8 or later first: https://dotnet.microsoft.com/download/dotnet-framework/net48'}
$source=Join-Path $PSScriptRoot 'DesktopFolders.exe'
if(!(Test-Path -LiteralPath $source)){throw 'Run Install.ps1 from the extracted release ZIP.'}
$target=[IO.Path]::GetFullPath($InstallDirectory)
if($target.TrimEnd('\') -eq [IO.Path]::GetPathRoot($target).TrimEnd('\')){throw 'Choose an application folder, not a drive root.'}
$running=@(Get-Process DesktopFolders -ErrorAction SilentlyContinue | Where-Object Path -eq (Join-Path $target 'DesktopFolders.exe'))
if($running.Count){throw 'Exit DesktopFolders from its tray menu before updating.'}
New-Item -ItemType Directory -Path $target -Force | Out-Null
if($target.TrimEnd('\') -ne $PSScriptRoot.TrimEnd('\')){
 foreach($name in @('DesktopFolders.exe','DesktopFolders.exe.config','LICENSE','README.txt','Install.ps1','Install.cmd')){
  $file=Join-Path $PSScriptRoot $name
  if(Test-Path -LiteralPath $file){Copy-Item -LiteralPath $file -Destination (Join-Path $target $name) -Force}
 }
}
$shell=New-Object -ComObject WScript.Shell
$folders=@([Environment]::GetFolderPath('DesktopDirectory'),[Environment]::GetFolderPath('Programs'))
if($StartWithWindows){$folders+=@([Environment]::GetFolderPath('Startup'))}
foreach($folder in $folders){
 if([string]::IsNullOrWhiteSpace($folder)){continue}
 $path=Join-Path $folder 'DesktopFolders.lnk'
 if(Test-Path -LiteralPath $path){$old=$shell.CreateShortcut($path);if($old.TargetPath -ne (Join-Path $target 'DesktopFolders.exe')){throw "An unrelated shortcut already exists: $path"}}
 $link=$shell.CreateShortcut($path);$link.TargetPath=Join-Path $target 'DesktopFolders.exe';$link.WorkingDirectory=$target;$link.IconLocation=$link.TargetPath+',0';$link.Description='DesktopFolders';$link.Save()
}
if(!$NoLaunch){Start-Process -FilePath (Join-Path $target 'DesktopFolders.exe') -WorkingDirectory $target}
Write-Output "Installed: $target"

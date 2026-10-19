param([string]$Version='0.1.0-beta.1')
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$artifacts=Join-Path $project 'artifacts'
$packageName='DesktopFolders-v'+$Version+'-windows-x64'
$package=Join-Path $artifacts $packageName
$zip=Join-Path $artifacts ($packageName+'.zip')
if((Test-Path -LiteralPath $package) -or (Test-Path -LiteralPath $zip)){throw 'Package output already exists; use a new version or a fresh workspace.'}
& (Join-Path $project 'build.ps1')
New-Item -ItemType Directory -Path $package -Force | Out-Null
# Explicit allowlist: runtime settings, imported shortcuts, icons and logs are never packaged.
Copy-Item -LiteralPath (Join-Path $project 'dist\DesktopFolders\DesktopFolders.exe'),(Join-Path $project 'dist\DesktopFolders\DesktopFolders.exe.config') -Destination $package
foreach($name in @('LICENSE','Install.ps1','Install.cmd')){Copy-Item -LiteralPath (Join-Path $project $name) -Destination $package}
Copy-Item -LiteralPath (Join-Path $project 'docs\QUICKSTART.txt') -Destination (Join-Path $package 'README.txt')
Compress-Archive -LiteralPath $package -DestinationPath $zip -CompressionLevel Optimal
$hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $artifacts ($packageName+'.sha256')),$hash+'  '+[IO.Path]::GetFileName($zip)+[Environment]::NewLine,[Text.Encoding]::ASCII)
Get-Item -LiteralPath $zip | Select-Object FullName,Length

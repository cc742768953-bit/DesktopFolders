param([string]$OutputDirectory=(Join-Path $PSScriptRoot 'dist\DesktopFolders'))
$ErrorActionPreference='Stop'
$framework=Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319'
$compiler=Join-Path $framework 'csc.exe'
if(!(Test-Path -LiteralPath $compiler)){throw 'Build requires Windows x64 and .NET Framework 4.8.'}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$refs=@('System.dll','System.Core.dll','System.Xaml.dll','System.Web.Extensions.dll','System.Drawing.dll','System.Windows.Forms.dll') | ForEach-Object {'/reference:'+(Join-Path $framework $_)}
$refs+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll') | ForEach-Object {'/reference:'+(Join-Path $framework ('WPF\'+$_))}
$sources=@('DesktopFolders.cs','FirstRun.cs','AssemblyInfo.cs') | ForEach-Object {Join-Path $PSScriptRoot ('src\'+$_)}
$compilerArgs=@('/nologo','/target:winexe','/platform:x64','/optimize+','/codepage:65001',('/win32manifest:'+(Join-Path $PSScriptRoot 'src\app.manifest')),('/win32icon:'+(Join-Path $PSScriptRoot 'assets\desktopfolders.ico')),('/resource:'+(Join-Path $PSScriptRoot 'assets\desktopfolders.ico')+',DesktopFolders.Main.ico'),('/resource:'+(Join-Path $PSScriptRoot 'assets\desktopfolders-tray.ico')+',DesktopFolders.Tray.ico'),('/out:'+(Join-Path $OutputDirectory 'DesktopFolders.exe')))+$refs+$sources
& $compiler @compilerArgs
if($LASTEXITCODE -ne 0){throw 'DesktopFolders build failed'}
@'
<?xml version="1.0" encoding="utf-8"?><configuration><startup useLegacyV2RuntimeActivationPolicy="true"><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8"/></startup></configuration>
'@ | Set-Content -LiteralPath (Join-Path $OutputDirectory 'DesktopFolders.exe.config') -Encoding UTF8
Write-Output (Join-Path $OutputDirectory 'DesktopFolders.exe')

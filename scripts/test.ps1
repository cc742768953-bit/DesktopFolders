param([switch]$IncludeUiTests)
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$output=Join-Path $project 'dist\DesktopFolders'
& (Join-Path $project 'build.ps1')
$process=Start-Process -FilePath (Join-Path $output 'DesktopFolders.exe') -ArgumentList '--self-test' -WindowStyle Hidden -Wait -PassThru
if($process.ExitCode -ne 0){throw 'Configuration and icon self-tests failed.'}
if($IncludeUiTests){
 $fixture=Join-Path $project ('artifacts\ui-tests-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
 New-Item -ItemType Directory -Path $fixture -Force | Out-Null
 Copy-Item -LiteralPath (Join-Path $output 'DesktopFolders.exe'),(Join-Path $output 'DesktopFolders.exe.config') -Destination $fixture
 # A public, empty fixture keeps personal desktop imports out of test screenshots and results.
 $groups=@(foreach($group in @(@('apps','应用'),@('tools','工具'),@('all','集合'))){[ordered]@{Id=$group[0];Name=$group[1];IsCollection=($group[0] -eq 'all');Items=@()}})
 [ordered]@{Version=1;Left=40;Top=100;Locked=$false;IconSize=48;ClickToReveal=$true;Groups=$groups} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $fixture 'config.json') -Encoding UTF8
 $framework=Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319'
 $refs=@('System.dll','System.Core.dll','System.Xaml.dll','System.Web.Extensions.dll') | ForEach-Object {'/reference:'+(Join-Path $framework $_)}
 $refs+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll') | ForEach-Object {'/reference:'+(Join-Path $framework ('WPF\'+$_))}
 & (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:x64 /codepage:65001 ('/out:'+(Join-Path $fixture 'ClickRevealTests.exe')) ('/reference:'+(Join-Path $fixture 'DesktopFolders.exe')) @refs (Join-Path $project 'tests\ClickRevealTests.cs')
 if($LASTEXITCODE -ne 0){throw 'UI test build failed.'}
 $ui=Start-Process -FilePath (Join-Path $fixture 'ClickRevealTests.exe') -WorkingDirectory $fixture -WindowStyle Hidden -Wait -PassThru
 Get-Content -LiteralPath (Join-Path $fixture 'click-reveal-tests.json') -Encoding UTF8
 if($ui.ExitCode -ne 0){throw 'Click-to-reveal UI tests failed.'}
}
Write-Output 'DesktopFolders tests passed.'

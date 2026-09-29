param([switch]$Integration)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$output = Join-Path $projectRoot 'build\tests'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /reference:System.Web.Extensions.dll ('/out:' + (Join-Path $output 'FakeEngine.exe')) (Join-Path $projectRoot 'tests\FakeEngine.cs')
if ($LASTEXITCODE -ne 0) { throw 'Fake engine build failed.' }
$references = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Net.Http.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll')
$compilerArgs = @('/nologo','/target:exe','/main:Tests',('/out:' + (Join-Path $output 'Tests.exe')))
$compilerArgs += $references | ForEach-Object { '/reference:' + $_ }
$compilerArgs += @('native\Core.cs','native\Kuaishou.cs','native\Engine.cs','native\Installer.cs','tests\Tests.cs') | ForEach-Object { Join-Path $projectRoot $_ }
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
$testArgs = @($projectRoot)
if ($Integration) { $testArgs += '--integration' }
& (Join-Path $output 'Tests.exe') @testArgs
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }

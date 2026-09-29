$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'common.ps1')
$app = Join-Path $projectRoot 'build\app'
$release = Join-Path $projectRoot 'release'
$portable = Join-Path $release 'Shiying-0.1.0-Portable'
$installed = Join-Path $projectRoot 'build\installer-payload'
& (Join-Path $PSScriptRoot 'setup-engine.ps1')
Copy-Item -LiteralPath (Join-Path $projectRoot 'tools\engine\yt-dlp.exe') -Destination (Join-Path $app 'bin\yt-dlp.exe') -Force
foreach ($taskDirectory in @($portable,$installed)) {
  $resolvedTarget = [System.IO.Path]::GetFullPath($taskDirectory)
  $allowedRoot = [System.IO.Path]::GetFullPath($projectRoot).TrimEnd('\') + '\'
  if (-not $resolvedTarget.StartsWith($allowedRoot,[System.StringComparison]::OrdinalIgnoreCase)) { throw 'Packaging target escaped workspace.' }
  if (Test-Path -LiteralPath $resolvedTarget) { throw "Packaging destination already exists. Move the previous build aside first: $resolvedTarget" }
  New-Item -ItemType Directory -Force -Path $resolvedTarget,(Join-Path $resolvedTarget 'ui'),(Join-Path $resolvedTarget 'bin'),(Join-Path $resolvedTarget 'licenses') | Out-Null
  Get-ChildItem -LiteralPath $app -File | Where-Object { $_.Extension -in '.exe','.dll','.config','.json','.md' } | Copy-Item -Destination $resolvedTarget -Force
  Copy-Item -Path (Join-Path $projectRoot 'dist\*') -Destination (Join-Path $resolvedTarget 'ui') -Recurse -Force
  Copy-Item -LiteralPath (Join-Path $app 'bin\yt-dlp.exe') -Destination (Join-Path $resolvedTarget 'bin\yt-dlp.exe')
  Copy-Item -LiteralPath (Join-Path $app 'bin\qjs.exe') -Destination (Join-Path $resolvedTarget 'bin\qjs.exe')
  Copy-Item -Path (Join-Path $app 'licenses\*') -Destination (Join-Path $resolvedTarget 'licenses') -Force
  Copy-Item -LiteralPath (Join-Path $projectRoot '使用说明.txt'),(Join-Path $projectRoot 'LICENSE') -Destination $resolvedTarget
}
[System.IO.File]::WriteAllText((Join-Path $portable 'portable.flag'), 'Portable mode: keep settings, optional components and browser data in ./Data.')
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$references = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll') | ForEach-Object { '/reference:' + $_ }
$baseArgs = @('/nologo','/target:winexe','/platform:x64','/optimize+',('/win32icon:' + (Join-Path $projectRoot 'resources\icon.ico')),('/win32manifest:' + (Join-Path $projectRoot 'native\app.manifest'))) + $references
& $compiler @baseArgs /define:UNINSTALL ('/out:' + (Join-Path $installed 'Uninstall.exe')) (Join-Path $projectRoot 'native\Installer.cs')
if ($LASTEXITCODE -ne 0) { throw 'Uninstaller build failed.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$payload = Join-Path $projectRoot 'build\payload.zip'
if (Test-Path -LiteralPath $payload) { Remove-Item -LiteralPath $payload }
[System.IO.Compression.ZipFile]::CreateFromDirectory($installed,$payload,[System.IO.Compression.CompressionLevel]::Optimal,$false)
& $compiler @baseArgs ('/resource:' + $payload + ',Payload') ('/out:' + (Join-Path $release 'Shiying-0.1.0-Setup.exe')) (Join-Path $projectRoot 'native\Installer.cs')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
$portableZip = Join-Path $release 'Shiying-0.1.0-Portable.zip'
[System.IO.Compression.ZipFile]::CreateFromDirectory($portable,$portableZip,[System.IO.Compression.CompressionLevel]::Optimal,$true)
Get-ChildItem -LiteralPath $release -File | Where-Object { $_.Extension -in '.zip','.exe' } | ForEach-Object {
  $hash = Get-Sha256 $_.FullName
  "$hash  $($_.Name)"
} | Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Encoding UTF8
Get-ChildItem -LiteralPath $release -File | Select-Object Name,@{n='SizeMB';e={[Math]::Round($_.Length / 1MB,2)}} | Format-Table -AutoSize

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'common.ps1')
& (Join-Path $PSScriptRoot 'setup-qjs.ps1')
$sdk = (& (Join-Path $PSScriptRoot 'prepare.ps1') | Select-Object -Last 1)
$output = Join-Path $projectRoot 'build\app'
New-Item -ItemType Directory -Force -Path $output,(Join-Path $output 'ui'),(Join-Path $output 'bin'),(Join-Path $output 'licenses') | Out-Null
$core = Join-Path $sdk 'lib\net462\Microsoft.Web.WebView2.Core.dll'
$forms = Join-Path $sdk 'lib\net462\Microsoft.Web.WebView2.WinForms.dll'
Copy-Item -LiteralPath $core,$forms,(Join-Path $sdk 'runtimes\win-x64\native\WebView2Loader.dll') -Destination $output -Force
Copy-Item -Path (Join-Path $projectRoot 'dist\*') -Destination (Join-Path $output 'ui') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'resources\components.json') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD_PARTY_NOTICES.md') -Destination $output -Force
Get-ChildItem -LiteralPath $sdk -File | Where-Object Name -Match 'LICENSE' | Copy-Item -Destination (Join-Path $output 'licenses') -Force
Copy-Item -LiteralPath (Join-Path $sdk 'NOTICE.txt') -Destination (Join-Path $output 'licenses\WebView2-NOTICE.txt') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'tools\engine\qjs.exe') -Destination (Join-Path $output 'bin\qjs.exe') -Force
if (Test-Path -LiteralPath (Join-Path $projectRoot 'resources\licenses')) { Copy-Item -Path (Join-Path $projectRoot 'resources\licenses\*') -Destination (Join-Path $output 'licenses') -Force }
Copy-Item -LiteralPath (Join-Path $projectRoot 'node_modules\react\LICENSE') -Destination (Join-Path $output 'licenses\React.txt') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'node_modules\lucide-react\LICENSE') -Destination (Join-Path $output 'licenses\Lucide.txt') -Force
if (Test-Path -LiteralPath (Join-Path $projectRoot 'tools\engine\yt-dlp.exe')) {
  $taskHash = Get-Sha256 (Join-Path $projectRoot 'tools\engine\yt-dlp.exe')
  if ($taskHash -eq '66674953fe251b89f4d08c5f0e35e0728679bd67ab3d7d05c0562af101dd3e7a') {
    Copy-Item -LiteralPath (Join-Path $projectRoot 'tools\engine\yt-dlp.exe') -Destination (Join-Path $output 'bin\yt-dlp.exe') -Force
  } else { Write-Warning 'yt-dlp download is incomplete. The app can be previewed, but packaging will require a verified engine.' }
}
Add-Type -AssemblyName System.Drawing
$bitmap = New-Object System.Drawing.Bitmap(64,64)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(33,85,205))
$taskPoints = [System.Drawing.Point[]]@([System.Drawing.Point]::new(25,17),[System.Drawing.Point]::new(48,32),[System.Drawing.Point]::new(25,47))
$graphics.FillPolygon([System.Drawing.Brushes]::White,$taskPoints)
$icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())
$iconPath = Join-Path $projectRoot 'resources\icon.ico'
$iconStream = [System.IO.File]::Create($iconPath)
$icon.Save($iconStream)
$iconStream.Dispose(); $icon.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$references = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Net.Http.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll',$core,$forms)
$compilerArgs = @('/nologo','/target:winexe','/platform:x64','/optimize+',('/out:' + (Join-Path $output 'Shiying.exe')),('/win32icon:' + $iconPath),('/win32manifest:' + (Join-Path $projectRoot 'native\app.manifest')))
$compilerArgs += $references | ForEach-Object { '/reference:' + $_ }
$compilerArgs += @('Core.cs','Kuaishou.cs','Engine.cs','Program.cs') | ForEach-Object { Join-Path $projectRoot ('native\' + $_) }
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Native build failed.' }
[System.IO.File]::WriteAllText((Join-Path $output 'Shiying.exe.config'), '<?xml version="1.0"?><configuration><startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" /></startup><runtime><AppContextSwitchOverrides value="Switch.System.Net.DontEnableSchUseStrongCrypto=false;Switch.System.Net.DontEnableSystemDefaultTlsVersions=false" /></runtime></configuration>')
[System.IO.File]::WriteAllText((Join-Path $output 'portable.flag'), 'Remove this marker only for an installed build.')
Write-Output "Built: $output\Shiying.exe"

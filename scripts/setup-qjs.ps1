$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'common.ps1')
$componentRoot = Join-Path $projectRoot 'tools\engine'
$target = Join-Path $componentRoot 'qjs.exe'
$expected = '2aeabf0092c3262d6b2609824418f7dd7ed1f1df939f73b2b15645230cac0d77'
if ((Test-Path -LiteralPath $target) -and (Get-Sha256 $target) -eq $expected) { Write-Output 'QuickJS-ng 0.17.0 verified.'; return }
New-Item -ItemType Directory -Force -Path $componentRoot | Out-Null
$download = $target + '.download'
$curlArgs = @('-fsSL','--retry','2','--connect-timeout','20','--max-time','120')
$proxySettings = Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings' -ErrorAction SilentlyContinue
if ($proxySettings.ProxyEnable -eq 1 -and $proxySettings.ProxyServer -match '^[\w.-]+:\d+$') { $curlArgs += @('--proxy', ('http://' + $proxySettings.ProxyServer)) }
$curlArgs += @('https://github.com/quickjs-ng/quickjs/releases/download/v0.17.0/qjs-windows-x86_64.exe','-o',$download)
& curl.exe @curlArgs
if ($LASTEXITCODE -ne 0) { throw 'QuickJS-ng download failed.' }
if ((Get-Sha256 $download) -ne $expected) { throw 'QuickJS-ng SHA-256 mismatch.' }
Move-Item -LiteralPath $download -Destination $target -Force
Write-Output 'QuickJS-ng 0.17.0 verified.'

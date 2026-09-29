$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'common.ps1')
$engineRoot = Join-Path $projectRoot 'tools\engine'
$engineFile = Join-Path $engineRoot 'yt-dlp.exe'
$expected = '66674953fe251b89f4d08c5f0e35e0728679bd67ab3d7d05c0562af101dd3e7a'
New-Item -ItemType Directory -Force -Path $engineRoot | Out-Null
if ((Test-Path -LiteralPath $engineFile) -and (Get-Sha256 $engineFile) -eq $expected) { Write-Output 'yt-dlp 2026.08.19 already verified.'; return }
$download = $engineFile + '.download'
$curlArgs = @('-fsSL','--retry','2','--connect-timeout','20','--max-time','300')
$proxySettings = Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings' -ErrorAction SilentlyContinue
if ($proxySettings.ProxyEnable -eq 1 -and $proxySettings.ProxyServer -match '^[\w.-]+:\d+$') { $curlArgs += @('--proxy', ('http://' + $proxySettings.ProxyServer)) }
$curlArgs += @('https://github.com/yt-dlp/yt-dlp/releases/download/2026.08.19/yt-dlp.exe','-o',$download)
& curl.exe @curlArgs
if ($LASTEXITCODE -ne 0) { throw 'yt-dlp download failed. Check network/proxy and retry.' }
if ((Get-Sha256 $download) -ne $expected) { throw 'yt-dlp SHA-256 mismatch; the downloaded file was NOT installed.' }
Move-Item -LiteralPath $download -Destination $engineFile -Force
Write-Output 'yt-dlp 2026.08.19 verified.'

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$toolsRoot = Join-Path $projectRoot 'tools'
$webviewVersion = '1.0.4258.31'
$webviewRoot = Join-Path $toolsRoot "webview2-$webviewVersion"
if (-not (Test-Path -LiteralPath (Join-Path $webviewRoot 'lib\net462\Microsoft.Web.WebView2.Core.dll'))) {
  New-Item -ItemType Directory -Force -Path $toolsRoot | Out-Null
  $archive = Join-Path $toolsRoot 'webview2.zip'
  & curl.exe -fL --retry 2 --connect-timeout 20 --max-time 180 "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$webviewVersion/microsoft.web.webview2.$webviewVersion.nupkg" -o $archive
  if ($LASTEXITCODE -ne 0) { throw 'Cannot download Microsoft WebView2 SDK.' }
  Expand-Archive -LiteralPath $archive -DestinationPath $webviewRoot -Force
}
Write-Output $webviewRoot

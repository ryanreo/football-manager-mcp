$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskTools = Join-Path $taskRoot '.tools'
$taskSdk = Join-Path $taskTools 'dotnet'
if (Test-Path (Join-Path $taskSdk 'sdk/6.0.428')) { exit 0 }
New-Item -ItemType Directory -Force -Path $taskTools | Out-Null
$taskMetadata = Invoke-RestMethod 'https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/6.0/releases.json'
$taskRelease = $taskMetadata.releases | Where-Object { $_.sdk.version -eq '6.0.428' } | Select-Object -First 1
$taskAsset = $taskRelease.sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.zip' } | Select-Object -First 1
if (-not $taskAsset) { throw 'Official SDK archive not found.' }
$taskZip = Join-Path $taskTools 'dotnet-sdk.zip'
Invoke-WebRequest -Uri $taskAsset.url -OutFile $taskZip
if ((Get-FileHash -LiteralPath $taskZip -Algorithm SHA512).Hash -ne $taskAsset.hash) { throw 'SDK checksum mismatch.' }
Expand-Archive -LiteralPath $taskZip -DestinationPath $taskSdk -Force
Remove-Item -LiteralPath $taskZip
& (Join-Path $taskSdk 'dotnet.exe') --version

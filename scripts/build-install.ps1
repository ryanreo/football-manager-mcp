param([string]$GamePath = 'C:\Games\Football Manager 26')
$ErrorActionPreference = 'Stop'
if (Get-Process -Name fm -ErrorAction SilentlyContinue) {
    throw 'Save and close Football Manager before installing the plugin; the running game locks its DLL.'
}
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskCompiler = Join-Path $taskRoot '.tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $taskCompiler)) { & (Join-Path $PSScriptRoot 'bootstrap-dotnet.ps1') }
& $taskCompiler build (Join-Path $taskRoot 'plugin/FM26McpBridge.csproj') -c Release --nologo "-p:GamePath=$GamePath"
if ($LASTEXITCODE -ne 0) { throw 'Plugin compilation failed.' }
$taskSource = Join-Path $taskRoot 'plugin/bin/Release/net6.0/FM26McpBridge.dll'
$taskDestination = Join-Path $GamePath 'BepInEx/plugins/FM26McpBridge'
New-Item -ItemType Directory -Force -Path $taskDestination | Out-Null
Copy-Item -LiteralPath $taskSource -Destination (Join-Path $taskDestination 'FM26McpBridge.dll')
Write-Output "Installed plugin at $taskDestination. Start or restart Football Manager to load it."

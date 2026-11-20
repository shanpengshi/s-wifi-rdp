$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'src\build.ps1')
$destination = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src\RemoteDesktopHelper.exe') -Destination $destination -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src\RemoteDesktopHelper.exe.config') -Destination $destination -Force
Write-Output (Join-Path $destination 'RemoteDesktopHelper.exe')

$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Cannot find the .NET Framework C# compiler.' }
$outputFile = Join-Path $PSScriptRoot 'RemoteDesktopHelper.exe'
& $compiler /nologo /codepage:65001 /target:winexe /optimize+ /platform:anycpu "/out:$outputFile" "/win32manifest:$PSScriptRoot\app.manifest" "/win32icon:$PSScriptRoot\app.ico" "/resource:$PSScriptRoot\app.ico,CampusRemoteDesktop.AppIcon" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "$PSScriptRoot\RemoteDesktopHelper.cs" "$PSScriptRoot\NetworkDetails.cs" "$PSScriptRoot\ResponsiveLayout.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output $outputFile

Copy-Item -LiteralPath "$PSScriptRoot\app.config" -Destination "$outputFile.config" -Force

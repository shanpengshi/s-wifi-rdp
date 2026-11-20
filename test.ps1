$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
$sourceDir = Join-Path $PSScriptRoot 'src'
$resultsDir = Join-Path $PSScriptRoot 'test-results'
New-Item -ItemType Directory -Path $resultsDir -Force | Out-Null
$testExe = Join-Path $sourceDir 'VerifyRemoteDesktopHelper.exe'
& $compiler /nologo /codepage:65001 /target:exe "/out:$testExe" "/reference:$sourceDir\RemoteDesktopHelper.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'tests\VerifyRemoteDesktopHelper.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& $testExe (Join-Path $resultsDir 'controller.png')
if ($LASTEXITCODE -ne 0) { throw 'Verification failed.' }
$layoutExe = Join-Path $sourceDir 'LayoutVerification.exe'
& $compiler /nologo /codepage:65001 /target:exe "/out:$layoutExe" "/reference:$sourceDir\RemoteDesktopHelper.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'tests\LayoutVerification.cs')
if ($LASTEXITCODE -ne 0) { throw 'Layout test compilation failed.' }
& $layoutExe $resultsDir
if ($LASTEXITCODE -ne 0) { throw 'Layout verification failed.' }

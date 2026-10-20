$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$testOutput = Join-Path $PSScriptRoot '..\bin\CurrencyOwnershipTests.exe'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $testOutput) | Out-Null
& $compiler /nologo /noconfig /target:exe /optimize+ ('/out:' + $testOutput) (Join-Path $PSScriptRoot 'RuntimeStubs.cs') (Join-Path $PSScriptRoot 'CurrencyOwnershipTests.cs') (Join-Path $PSScriptRoot '..\src\RemoteVPNGreen.cs')
if ($LASTEXITCODE -ne 0) { throw 'Synthetic test compilation failed.' }
& $testOutput
if ($LASTEXITCODE -ne 0) { throw 'Currency ownership assertions failed.' }

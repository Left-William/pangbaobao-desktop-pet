$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$portableDotnet = Join-Path $projectRoot '..\work\dotnet8\sdk\dotnet.exe'
$dotnetCli = if (Test-Path -LiteralPath $portableDotnet) { (Resolve-Path $portableDotnet).Path } else { 'dotnet' }
$env:DOTNET_CLI_HOME = $projectRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location $projectRoot
try {
    & $dotnetCli run --project 'tests\PangBaoBaoPet.Tests\PangBaoBaoPet.SmokeTests.csproj' -c Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
} finally { Pop-Location }

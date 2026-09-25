param([switch]$SelfContained, [switch]$ReleaseCandidate)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$portableDotnet = Join-Path $projectRoot '..\work\dotnet8\sdk\dotnet.exe'
$dotnetCli = if (Test-Path -LiteralPath $portableDotnet) { (Resolve-Path $portableDotnet).Path } else { 'dotnet' }
$env:DOTNET_CLI_HOME = $projectRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$packageName = if ($SelfContained) { 'PangBaoBaoPet-0.5.0-preview.1-win-x64-selfcontained' } else { 'PangBaoBaoPet-0.5.0-preview.1-win-x64' }
$distRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'dist'))
$target = [System.IO.Path]::GetFullPath((Join-Path $distRoot $packageName))
$zip = [System.IO.Path]::GetFullPath((Join-Path $distRoot "$packageName.zip"))
if (-not $target.StartsWith($distRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Invalid package target'
}
Push-Location $projectRoot
try {
    if ($ReleaseCandidate) {
        & python 'tools\assets\validate_assets.py' --release
        if ($LASTEXITCODE -ne 0) { throw 'Asset quality gate failed' }
        & $dotnetCli run --project 'tests\PangBaoBaoPet.Tests\PangBaoBaoPet.SmokeTests.csproj' -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    }
    New-Item -ItemType Directory -Path $distRoot -Force | Out-Null
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
    $mode = if ($SelfContained) { 'true' } else { 'false' }
    & $dotnetCli publish 'src\PangBaoBaoPet.Desktop\PangBaoBaoPet.csproj' -c Release -r win-x64 --self-contained $mode -o $target
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Copy-Item -LiteralPath 'docs\安装说明.md' -Destination (Join-Path $target '安装说明.md') -Force
    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
    Compress-Archive -LiteralPath $target -DestinationPath $zip
    Get-Item -LiteralPath $zip | Select-Object FullName,Length
    Get-FileHash -LiteralPath $zip -Algorithm SHA256 | Select-Object Hash
} finally { Pop-Location }

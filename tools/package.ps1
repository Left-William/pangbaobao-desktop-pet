param([switch]$SelfContained, [switch]$Installer, [switch]$PreviewCandidate, [switch]$ReleaseCandidate)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$portableDotnet = Join-Path $projectRoot '..\work\dotnet8\sdk\dotnet.exe'
$dotnetCli = if (Test-Path -LiteralPath $portableDotnet) { (Resolve-Path $portableDotnet).Path } else { 'dotnet' }
$env:DOTNET_CLI_HOME = $projectRoot
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
if ($Installer) { $SelfContained = $true }
$packageName = if ($SelfContained) { 'PangBaoBaoPet-0.5.0-preview.2-win-x64-selfcontained' } else { 'PangBaoBaoPet-0.5.0-preview.2-win-x64' }
$distRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'dist'))
$target = [System.IO.Path]::GetFullPath((Join-Path $distRoot $packageName))
$zip = [System.IO.Path]::GetFullPath((Join-Path $distRoot "$packageName.zip"))
if (-not $target.StartsWith($distRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Invalid package target'
}
Push-Location $projectRoot
try {
    if ($PreviewCandidate -and $ReleaseCandidate) { throw 'Choose one asset quality gate' }
    if ($PreviewCandidate) {
        & python 'tools\assets\validate_assets.py' --preview
        if ($LASTEXITCODE -ne 0) { throw 'Preview asset quality gate failed' }
        & $dotnetCli run --project 'tests\PangBaoBaoPet.Tests\PangBaoBaoPet.SmokeTests.csproj' -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    }
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
    & python 'tools\assets\validate_package.py' $zip
    if ($LASTEXITCODE -ne 0) { throw 'Package content validation failed' }
    Get-Item -LiteralPath $zip | Select-Object FullName,Length
    Get-FileHash -LiteralPath $zip -Algorithm SHA256 | Select-Object Hash
    if ($Installer) {
        $iscc = if ($env:INNO_SETUP_ISCC) { $env:INNO_SETUP_ISCC } else {
            $command = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
            if ($command) { $command.Source } else { Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe' }
        }
        if (-not (Test-Path -LiteralPath $iscc -PathType Leaf)) {
            throw 'Inno Setup 7 ISCC.exe not found. Set INNO_SETUP_ISCC to its full path.'
        }
        & $iscc 'installer\PangBaoBaoPet.iss'
        if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed' }
        $setup = Join-Path $distRoot 'PangBaoBaoPet-0.5.0-preview.2-win-x64-setup.exe'
        if (-not (Test-Path -LiteralPath $setup -PathType Leaf)) { throw 'Installer output missing' }
        Get-Item -LiteralPath $setup | Select-Object FullName,Length
        Get-FileHash -LiteralPath $setup -Algorithm SHA256 | Select-Object Hash
    }
} finally { Pop-Location }

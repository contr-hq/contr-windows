# Builds the Windows installer: the app published self-contained, the pinned core release beside it, packed by Velopack
# into Setup.exe, an MSI and the update packages.
# Usage: scripts/pack-release.ps1 -Version 0.1.0 -CoreZip contr-core-windows-x64-v0.1.0-alpha.1.zip
#        [-Output artifacts/release] [-SignMetadata metadata.json]
param(
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $CoreZip,
    [string] $Output = 'artifacts/release',
    # Azure Artifact Signing metadata; without it nothing is signed.
    [string] $SignMetadata = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$publish = Join-Path $root 'artifacts/publish'
$core = Join-Path $root 'artifacts/core'
Remove-Item -Recurse -Force $publish, $core, $Output -ErrorAction SilentlyContinue

dotnet publish (Join-Path $root 'src/App/Contr.App/Contr.App.csproj') `
    --configuration Release --runtime win-x64 --self-contained true `
    -p:Platform=x64 -p:InformationalVersion=$Version -p:Product=Contr `
    --output $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish exited with $LASTEXITCODE" }

# The daemon runs from beside the app and finds its guest, CLI tools and core.json next to itself.
Expand-Archive -Path $CoreZip -DestinationPath $core
$payload = Join-Path $core 'contr-core-windows-x64'
$coreLicenses = Join-Path $publish 'licenses/contr-core'
New-Item -ItemType Directory -Force $coreLicenses | Out-Null
foreach ($notice in 'LICENSE', 'THIRD-PARTY-NOTICES.md') {
    Move-Item (Join-Path $payload $notice) $coreLicenses
}
Get-ChildItem (Join-Path $payload 'licenses') | Move-Item -Destination $coreLicenses
Remove-Item (Join-Path $payload 'licenses')
Copy-Item -Recurse -Path (Join-Path $payload '*') -Destination $publish
Copy-Item (Join-Path $root 'LICENSE') $publish

$pack = @(
    'pack',
    # Not "Contr": Velopack deletes %LOCALAPPDATA%\<packId> on uninstall, and the engine's data lives in ...\Contr.
    '--packId', 'Contr.App',
    '--packVersion', $Version,
    '--packTitle', 'Contr',
    '--packAuthors', 'Contr',
    '--packDir', $publish,
    '--mainExe', 'Contr.App.exe',
    '--icon', (Join-Path $root 'src/App/Contr.App/Assets/app.ico'),
    '--channel', 'win-x64',
    '--msi',
    '--outputDir', $Output
)
if ($SignMetadata) {
    $pack += '--azureTrustedSignFile', (Resolve-Path $SignMetadata)
}

vpk @pack
if ($LASTEXITCODE -ne 0) { throw "vpk pack exited with $LASTEXITCODE" }

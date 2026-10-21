param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$publishPath = Join-Path $projectRoot 'publish'
$distPath = Join-Path $projectRoot 'dist'
$packageContentPath = Join-Path $projectRoot 'package'
$packagePath = Join-Path $distPath 'library-statistics_1.2.1.0.zip'

dotnet test (Join-Path $projectRoot 'Jellyfin.Plugin.LibraryStatistics.slnx') -c $Configuration
dotnet publish (Join-Path $projectRoot 'src/Jellyfin.Plugin.LibraryStatistics/Jellyfin.Plugin.LibraryStatistics.csproj') -c $Configuration -o $publishPath

New-Item -ItemType Directory -Path $distPath -Force | Out-Null
if (Test-Path -LiteralPath $packageContentPath) {
    Remove-Item -LiteralPath $packageContentPath -Recurse -Force
}

New-Item -ItemType Directory -Path $packageContentPath -Force | Out-Null
Copy-Item (Join-Path $publishPath 'Jellyfin.Plugin.LibraryStatistics.dll') $packageContentPath -Force
Copy-Item (Join-Path $projectRoot 'meta.json') $packageContentPath -Force
Compress-Archive -Path (Join-Path $packageContentPath '*') -DestinationPath $packagePath -Force

Write-Host "Created $packagePath"
Write-Host "MD5: $((Get-FileHash -LiteralPath $packagePath -Algorithm MD5).Hash.ToLowerInvariant())"

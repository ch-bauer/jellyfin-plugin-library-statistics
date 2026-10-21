param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$publishPath = Join-Path $projectRoot 'publish'
$distPath = Join-Path $projectRoot 'dist'
$packagePath = Join-Path $distPath 'library-statistics_1.1.0.0.zip'

dotnet test (Join-Path $projectRoot 'Jellyfin.Plugin.LibraryStatistics.slnx') -c $Configuration
dotnet publish (Join-Path $projectRoot 'src/Jellyfin.Plugin.LibraryStatistics/Jellyfin.Plugin.LibraryStatistics.csproj') -c $Configuration -o $publishPath

New-Item -ItemType Directory -Path $distPath -Force | Out-Null
Copy-Item (Join-Path $projectRoot 'meta.json') (Join-Path $publishPath 'meta.json') -Force
Compress-Archive -Path (Join-Path $publishPath '*') -DestinationPath $packagePath -Force

Write-Host "Created $packagePath"

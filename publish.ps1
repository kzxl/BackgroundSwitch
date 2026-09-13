<#
    publish.ps1 — Publish script for ZeroWall (Dual Mode: Full & Lite)
    Adheres to AgentOption .NET Publish Release standard & ZeroUniverse rules.
#>
[CmdletBinding()]
param(
    [ValidateSet('Full', 'Lite', 'All')]
    [string]$Mode = 'All',
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$Root = $PSScriptRoot
$Proj = Join-Path $Root "ZeroWall.csproj"
$Dist = Join-Path $Root "publish"

if (Test-Path $Dist) {
    Remove-Item $Dist -Recurse -Force -ErrorAction SilentlyContinue
}

if ($Mode -eq 'Full' -or $Mode -eq 'All') {
    Write-Host ">>> Publishing ZeroWall FULL (Self-Contained Single File)..." -ForegroundColor Cyan
    $outFull = Join-Path $Dist "full"
    dotnet publish $Proj -c $Configuration -r $Runtime --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o $outFull
    Write-Host "  ✔ Full build generated at: $outFull\ZeroWall.exe" -ForegroundColor Green
}

if ($Mode -eq 'Lite' -or $Mode -eq 'All') {
    Write-Host ">>> Publishing ZeroWall LITE (Framework-Dependent Single File)..." -ForegroundColor Cyan
    $outLite = Join-Path $Dist "lite"
    dotnet publish $Proj -c $Configuration -r $Runtime --self-contained false `
        -p:PublishSingleFile=true `
        -o $outLite
    Write-Host "  ✔ Lite build generated at: $outLite\ZeroWall.exe" -ForegroundColor Green
}

Write-Host ">>> ZeroWall publish completed successfully!" -ForegroundColor Green

<#
.SYNOPSIS
    Produit dist\Pointik.exe : un seul fichier autonome (aucun runtime .NET à installer).

.DESCRIPTION
    Publication Release, win-x64, autonome, fichier unique, bibliothèques natives incluses
    (extraites au premier lancement, voir README), compression activée, sans trimming.
    Les tests unitaires du Core sont exécutés avant la publication (désactivable avec -SkipTests).
#>
param(
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$project = Join-Path $root 'src\Pointik.App.Windows\Pointik.App.Windows.csproj'
$publishDir = Join-Path $root 'src\Pointik.App.Windows\bin\Release\net8.0-windows\win-x64\publish'
$dist = Join-Path $root 'dist'

if (-not $SkipTests) {
    Write-Host 'Tests unitaires du Core…' -ForegroundColor Cyan
    dotnet test (Join-Path $root 'tests\Pointik.Core.Tests\Pointik.Core.Tests.csproj') -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Les tests ont échoué : publication annulée.' }
}

Write-Host 'Publication de Pointik.exe…' -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishTrimmed=false `
    --nologo
if ($LASTEXITCODE -ne 0) { throw 'La publication a échoué.' }

New-Item -ItemType Directory -Force $dist | Out-Null
Copy-Item (Join-Path $publishDir 'Pointik.exe') (Join-Path $dist 'Pointik.exe') -Force

# Le fichier unique doit être seul : signaler tout fichier annexe inattendu.
$extra = Get-ChildItem $publishDir -File | Where-Object { $_.Name -ne 'Pointik.exe' }
if ($extra) {
    Write-Warning ("Fichiers annexes non inclus dans l'exe : " + ($extra.Name -join ', '))
}

$exe = Get-Item (Join-Path $dist 'Pointik.exe')
Write-Host ("Terminé : {0} ({1:N1} Mo)" -f $exe.FullName, ($exe.Length / 1MB)) -ForegroundColor Green

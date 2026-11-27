param([string]$AssemblyPath = (Join-Path $PSScriptRoot '..\bin\Debug\PineTree.dll'))
# The former shared multi-page UI has been replaced by working-artboard editing.
& (Join-Path $PSScriptRoot 'RunWorkingArtboardRegression.ps1') -AssemblyPath $AssemblyPath

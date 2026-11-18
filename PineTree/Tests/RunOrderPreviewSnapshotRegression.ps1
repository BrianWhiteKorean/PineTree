param([string]$AssemblyPath = (Join-Path $PSScriptRoot '..\bin\Debug\PineTree.dll'))
$ErrorActionPreference = 'Stop'
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\tmp\order-preview-snapshot'))
[void][IO.Directory]::CreateDirectory($taskRoot)
$taskEngine = (Resolve-Path (Join-Path $PSScriptRoot '..\..\PineTree.PdfEngine\bin\Debug\PineTree.PdfEngine.dll')).Path
$taskRunner = Join-Path $taskRoot 'OrderPreviewSnapshotRegression.exe'
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $taskCompiler /nologo /target:exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Data.dll "/r:$AssemblyPath" "/r:$taskEngine" "/out:$taskRunner" (Join-Path $PSScriptRoot 'OrderPreviewSnapshotRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Preview regression compilation failed' }
$taskCache = [Security.SecurityElement]::Escape((Join-Path $taskRoot 'forbidden-runtime'))
[IO.File]::WriteAllText($taskRunner + '.config', '<configuration><appSettings><add key="PdfEngineCacheDirectory" value="' + $taskCache + '" /></appSettings></configuration>')
& $taskRunner $AssemblyPath
if ($LASTEXITCODE -ne 0) { throw 'Saved preview regression failed' }

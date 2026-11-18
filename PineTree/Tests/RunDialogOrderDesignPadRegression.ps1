param([string]$AssemblyPath = (Join-Path $PSScriptRoot '..\bin\Debug\PineTree.dll'))
$ErrorActionPreference = 'Stop'
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\tmp\dialog-order-design-pad'))
[void][IO.Directory]::CreateDirectory($taskRoot)
$taskRunner = Join-Path $taskRoot 'DialogOrderDesignPadRegression.exe'
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $taskCompiler /nologo /target:exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Data.dll "/out:$taskRunner" (Join-Path $PSScriptRoot 'DialogOrderDesignPadRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Regression compilation failed' }
$taskCache = [Security.SecurityElement]::Escape((Join-Path $taskRoot '..\source-preview-cache\cache'))
[IO.File]::WriteAllText($taskRunner + '.config', '<configuration><appSettings><add key="PdfEngineCacheDirectory" value="' + $taskCache + '" /></appSettings></configuration>')
& $taskRunner $AssemblyPath
if ($LASTEXITCODE -ne 0) { throw 'Dialog restoration regression failed' }

$taskSnapshotRunner = Join-Path $taskRoot 'DesignPadSnapshotRegression.exe'
& $taskCompiler /nologo /target:exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Data.dll "/out:$taskSnapshotRunner" (Join-Path $PSScriptRoot 'DesignPadSnapshotRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Snapshot regression compilation failed' }
[IO.File]::WriteAllText($taskSnapshotRunner + '.config', [IO.File]::ReadAllText($taskRunner + '.config'))
& $taskSnapshotRunner $AssemblyPath
if ($LASTEXITCODE -ne 0) { throw 'Snapshot restoration regression failed' }

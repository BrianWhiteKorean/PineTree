param([Parameter(Mandatory=$true)][string]$ApplicationPath, [Parameter(Mandatory=$true)][string]$SourcePath)
$ErrorActionPreference = 'Stop'
$ApplicationPath = (Resolve-Path -LiteralPath $ApplicationPath).Path
$SourcePath = (Resolve-Path -LiteralPath $SourcePath).Path
$results = Join-Path (Split-Path $PSScriptRoot -Parent) ('TestResults/deployment-' + [Guid]::NewGuid().ToString('N'))
$isolation = Join-Path $results 'isolated'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$cache = Join-Path $workspace ('tmp/pdf-cache-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($isolation) | Out-Null
$application = Join-Path $isolation 'PineTree.exe'
$runner = Join-Path $isolation 'DeploymentRegression.exe'
$output = Join-Path $results 'generated.pdf'
Copy-Item -LiteralPath $ApplicationPath -Destination $application
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /target:exe "/out:$runner" (Join-Path $PSScriptRoot 'PdfEngineDeploymentRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Deployment test compilation failed' }
$escapedCache = [Security.SecurityElement]::Escape($cache)
[IO.File]::WriteAllText($runner + '.config', '<configuration><appSettings><add key="PdfEngineCacheDirectory" value="' + $escapedCache + '" /></appSettings></configuration>')
& $runner $application $SourcePath $output cancel-prepare
if ($LASTEXITCODE -ne 0) { throw 'Preparation cancellation failed' }
if (Test-Path -LiteralPath $cache) {
    if (@(Get-ChildItem -LiteralPath $cache -Directory -Filter 'staging-*').Count) { throw 'Cancelled extraction left a staging directory' }
}
$processes = @()
for ($index = 0; $index -lt 2; $index++) {
    $quotedArgs = @($application, $SourcePath, $output, 'inspect') | ForEach-Object { '"' + $_ + '"' }
    $processes += Start-Process -FilePath $runner -ArgumentList $quotedArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $results "concurrent-$index.out.txt") -RedirectStandardError (Join-Path $results "concurrent-$index.err.txt")
}
foreach ($process in $processes) {
    if (!$process.WaitForExit(180000)) { $process.Kill(); throw 'Concurrent runtime preparation timed out' }
    if ($process.ExitCode -ne 0) { throw "Concurrent preparation failed. See $results" }
}
Write-Host 'PASS: Cross-process cold preparation'
& $runner $application $SourcePath $output
if ($LASTEXITCODE -ne 0) { throw 'Isolated deployment checks failed' }
$cachedWorker = @(Get-ChildItem -LiteralPath $cache -Filter 'pdf_worker.py' -Recurse)
if ($cachedWorker.Count -ne 1) { throw 'Expected one runtime cache' }
$worker = $cachedWorker[0].FullName
$original = [IO.File]::ReadAllBytes($worker)
[IO.File]::WriteAllText($worker, 'damaged-runtime')
& $runner $application $SourcePath $output inspect
if ($LASTEXITCODE -ne 0) { throw 'Corrupted runtime recovery failed' }
if ([Convert]::ToBase64String([IO.File]::ReadAllBytes($worker)) -cne [Convert]::ToBase64String($original)) { throw 'Recovered worker differs from original' }
Write-Host 'PASS: Corrupted cache repaired in a new process'
Remove-Item -LiteralPath $worker
& $runner $application $SourcePath $output inspect
if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $worker)) { throw 'Missing runtime recovery failed' }
Write-Host 'PASS: Missing cache file restored'
Write-Host "Deployment regression passed. Results: $results"

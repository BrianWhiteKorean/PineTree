param(
    [Parameter(Mandatory=$true)][string]$ApplicationPath,
    [Parameter(Mandatory=$true)][string]$SourcePath,
    [string]$LegacyApplicationPath,
    [string]$UpdatedEnginePath
)
$ErrorActionPreference = 'Stop'
$ApplicationPath = (Resolve-Path -LiteralPath $ApplicationPath).Path
$SourcePath = (Resolve-Path -LiteralPath $SourcePath).Path
$results = Join-Path (Split-Path $PSScriptRoot -Parent) ('TestResults/deployment-' + [Guid]::NewGuid().ToString('N'))
$isolation = Join-Path $results 'isolated'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$cache = Join-Path $workspace ('tmp/pdf-cache-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
[IO.Directory]::CreateDirectory($isolation) | Out-Null
$application = Join-Path $isolation ('PineTree' + [IO.Path]::GetExtension($ApplicationPath))
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
    if (@(Get-ChildItem -LiteralPath $cache -Directory -Filter 'staging-*' -Recurse).Count) { throw 'Cancelled extraction left a staging directory' }
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
$runtimeRoot = Join-Path $cache 'runtimes'
$workerRoot = Join-Path $cache 'workers'
$runtimes = @(Get-ChildItem -LiteralPath $runtimeRoot -Directory)
$cachedWorker = @(Get-ChildItem -LiteralPath $workerRoot -Filter 'pdf_worker.py' -Recurse)
if ($runtimes.Count -ne 1 -or $cachedWorker.Count -ne 1) { throw 'Expected one shared runtime and one worker' }
$runtime = $runtimes[0].FullName
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

$pythonConfig = Join-Path $runtime 'python312._pth'
$pythonConfigHash = (Get-FileHash -LiteralPath $pythonConfig).Hash
[IO.File]::WriteAllText($pythonConfig, 'damaged-python-config')
& $runner $application $SourcePath $output inspect
if ($LASTEXITCODE -ne 0 -or (Get-FileHash -LiteralPath $pythonConfig).Hash -cne $pythonConfigHash) { throw 'Shared runtime corruption recovery failed' }
Write-Host 'PASS: Shared runtime SHA-256 check repairs corruption'

$eps = Join-Path $results 'bundled-ghostscript.eps'
[IO.File]::WriteAllText($eps, "%!PS-Adobe-3.0 EPSF-3.0`n%%BoundingBox: 0 0 120 80`n0 0 1 setrgbcolor 0 0 120 80 rectfill`nshowpage`n%%EOF`n")
& $runner $application $eps (Join-Path $results 'eps.pdf') inspect
if ($LASTEXITCODE -ne 0) { throw 'Bundled Ghostscript resolution failed' }
Write-Host 'PASS: EPS inspection through shared Ghostscript DLL'

if ($UpdatedEnginePath) {
    $updatedEngine = Join-Path $isolation 'UpdatedEngine.dll'
    Copy-Item -LiteralPath $UpdatedEnginePath -Destination $updatedEngine
    $runtimeCreatedAt = (Get-Item -LiteralPath $runtime).CreationTimeUtc
    & $runner $updatedEngine $SourcePath $output engine-inspect
    if ($LASTEXITCODE -ne 0) { throw 'Updated worker inspection failed' }
    if (@(Get-ChildItem -LiteralPath $runtimeRoot -Directory).Count -ne 1 -or
        @(Get-ChildItem -LiteralPath $workerRoot -Directory).Count -ne 2 -or
        (Get-Item -LiteralPath $runtime).CreationTimeUtc -ne $runtimeCreatedAt) { throw 'Worker-only update did not reuse the shared runtime' }
    Write-Host 'PASS: Worker-only update reuses the installed runtime'
}

if ($LegacyApplicationPath) {
    $legacy = Join-Path $isolation 'LegacyPineTree.exe'
    Copy-Item -LiteralPath $LegacyApplicationPath -Destination $legacy
    $legacyOutput = Join-Path $results 'legacy.pdf'
    & $runner $legacy $SourcePath $legacyOutput
    if ($LASTEXITCODE -ne 0) { throw 'Legacy single-EXE deployment failed with the shared cache layout present' }
    $legacyFolders = @(Get-ChildItem -LiteralPath $cache -Directory | Where-Object Name -Match '^[0-9a-f]{64}$')
    if ($legacyFolders.Count -ne 1 -or !(Test-Path -LiteralPath (Join-Path $legacyFolders[0].FullName 'runtime/python.exe'))) { throw 'Legacy runtime cache was not isolated' }
    & $runner $application $SourcePath $output inspect
    if ($LASTEXITCODE -ne 0) { throw 'New deployment failed after running the old deployment' }
    Write-Host 'PASS: Legacy EXE and new Costura deployment coexist in one cache root'
}
Write-Host "Isolated cache: $cache"
Write-Host "Deployment regression passed. Results: $results"

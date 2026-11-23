[CmdletBinding()]
param(
    [string]$CacheDirectory = (Join-Path $env:LOCALAPPDATA 'PineTree\PdfEngine'),
    [ValidatePattern('^[0-9a-fA-F]{64}$')][string[]]$RetainLegacyKey = @(),
    [string[]]$RetainLegacyManifest = @(),
    [switch]$DeploymentInventoryComplete
)

$ErrorActionPreference = 'Stop'
$root = Get-Item -LiteralPath $CacheDirectory -Force
if (!$root.PSIsContainer -or ($root.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    throw 'Expected a cache directory that is not a reparse point.'
}

$retained = @{}
foreach ($key in $RetainLegacyKey) { $retained[$key.ToLowerInvariant()] = $true }
foreach ($path in $RetainLegacyManifest) {
    $manifest = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $path).Path)
    if ($manifest -cnotmatch '(?m)^[0-9a-f]{64}\tpdf_worker\.py$' -or
        $manifest -cnotmatch '(?m)^[0-9a-f]{64}\truntime/python\.exe$') {
        throw "Expected a legacy PdfWorker.manifest: $path"
    }
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hash = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($manifest))) }
    finally { $sha.Dispose() }
    $retained[$hash.Replace('-', '').ToLowerInvariant()] = $true
}

foreach ($folder in Get-ChildItem -LiteralPath $root.FullName -Directory -Force) {
    if ($folder.Name -cnotmatch '^[0-9a-f]{64}$') { continue }
    $status = 'Unrecognized'
    if ($folder.Attributes -band [IO.FileAttributes]::ReparsePoint) { $status = 'ReparsePoint' }
    elseif ($retained.ContainsKey($folder.Name)) { $status = 'Retain' }
    else {
        $runtime = Get-Item -LiteralPath (Join-Path $folder.FullName 'runtime') -Force -ErrorAction SilentlyContinue
        $worker = Get-Item -LiteralPath (Join-Path $folder.FullName 'pdf_worker.py') -Force -ErrorAction SilentlyContinue
        if ($runtime -and $runtime.PSIsContainer -and $worker -and !$worker.PSIsContainer) {
            if (($runtime.Attributes -bor $worker.Attributes) -band [IO.FileAttributes]::ReparsePoint) {
                $status = 'ReparsePoint'
            }
            elseif ($DeploymentInventoryComplete) { $status = 'Candidate' }
            else { $status = 'InventoryRequired' }
        }
    }
    [pscustomobject]@{ LegacyKey = $folder.Name; Status = $status; Path = $folder.FullName }
}

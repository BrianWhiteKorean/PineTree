param([Parameter(Mandatory=$true)][string]$Source, [Parameter(Mandatory=$true)][string]$Archive, [Parameter(Mandatory=$true)][string]$Manifest)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$Source = (Resolve-Path -LiteralPath $Source).Path.TrimEnd('\')
foreach ($required in @('pdf_worker.py', 'runtime/python.exe', 'runtime/python312.dll', 'runtime/python312._pth', 'runtime/Lib/site-packages/pypdf/__init__.py', 'runtime/Lib/site-packages/PIL/__init__.py', 'runtime/Lib/site-packages/pypdfium2/__init__.py', 'runtime/Lib/site-packages/pypdfium2_raw/pdfium.dll', 'runtime/ghostscript/gsdll64.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $Source $required) -PathType Leaf)) { throw "Missing PDF runtime file: $required. See PdfWorker/build_runtime.py." }
}
$files = @(Get-ChildItem -LiteralPath $Source -Recurse -File | Where-Object {
    $relative = $_.FullName.Substring($Source.Length + 1).Replace('\', '/')
    ($relative -eq 'pdf_worker.py' -or $relative.StartsWith('runtime/')) -and $relative -notmatch '(^|/)__pycache__/|\.pyc$'
} | Sort-Object FullName)
$lines = @($files | ForEach-Object {
    $relative = $_.FullName.Substring($Source.Length + 1).Replace('\', '/')
    $sha = [Security.Cryptography.SHA256]::Create()
    $inputStream = [IO.File]::OpenRead($_.FullName)
    try { $hash = [BitConverter]::ToString($sha.ComputeHash($inputStream)).Replace('-', '').ToLowerInvariant() }
    finally { $inputStream.Dispose(); $sha.Dispose() }
    $hash + "`t" + $relative
})
$content = ($lines -join "`n") + "`n"
if ((Test-Path -LiteralPath $Archive) -and (Test-Path -LiteralPath $Manifest) -and [IO.File]::ReadAllText($Manifest) -ceq $content) { exit 0 }
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Archive)) | Out-Null
$temporary = $Archive + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    $stream = [IO.File]::Create($temporary)
    try {
        $zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            foreach ($file in $files) {
                $relative = $file.FullName.Substring($Source.Length + 1).Replace('\', '/')
                $entry = $zip.CreateEntry($relative, [IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
                $entryStream = $entry.Open()
                $fileStream = [IO.File]::OpenRead($file.FullName)
                try { $fileStream.CopyTo($entryStream) }
                finally { $fileStream.Dispose(); $entryStream.Dispose() }
            }
        } finally { $zip.Dispose() }
    } finally { $stream.Dispose() }
    [IO.File]::Copy($temporary, $Archive, $true)
    [IO.File]::WriteAllText($Manifest, $content, [Text.UTF8Encoding]::new($false))
    Write-Host "Embedded PDF runtime: $($files.Count) files"
} finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }

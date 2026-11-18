param([string]$AssemblyPath = (Join-Path $PSScriptRoot '..\bin\Debug\PineTree.dll'))
$ErrorActionPreference = 'Stop'
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
$taskResults = Join-Path (Split-Path $AssemblyPath -Parent) ('BannerOrderRegression\' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($taskResults) | Out-Null
$taskPython = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\PineTree.PdfEngine\PdfWorker\runtime\python.exe')).Path
@'
from pathlib import Path
import sys
from PIL import Image, ImageDraw
from pypdf import PdfWriter
folder=Path(sys.argv[1])
image=Image.new('RGBA',(300,600),'white')
draw=ImageDraw.Draw(image)
draw.rectangle((15,15,285,585),fill=(25,115,150,255))
draw.rectangle((45,120,255,480),fill=(245,185,65,255))
image.save(folder/'source.png',dpi=(150,150))
image.convert('RGB').save(folder/'source.jpg',quality=95)
writer=PdfWriter()
writer.add_blank_page(width=300,height=600)
writer.write(folder/'source.pdf')
(folder/'source.ai').write_bytes((folder/'source.pdf').read_bytes())
(folder/'source.eps').write_text('%!PS-Adobe-3.0 EPSF-3.0\n%%BoundingBox: 0 0 300 600\n%%EndComments\n0.1 0.5 0.7 setrgbcolor 0 0 300 600 rectfill\nshowpage\n%%EOF\n',encoding='ascii')
(folder/'unsupported.txt').write_text('unsupported',encoding='ascii')
'@ | & $taskPython - $taskResults
if ($LASTEXITCODE -ne 0) { throw 'Fixture generation failed' }
$taskRunner = Join-Path $taskResults 'BannerOrderPdfRegression.exe'
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $taskCompiler /nologo /target:exe /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$taskRunner" (Join-Path $PSScriptRoot 'BannerOrderPdfRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
$taskCache = [Security.SecurityElement]::Escape((Join-Path ([IO.Path]::GetTempPath()) 'PineTreeBannerPdfCache'))
[IO.File]::WriteAllText($taskRunner + '.config', '<configuration><appSettings><add key="PdfEngineCacheDirectory" value="' + $taskCache + '" /></appSettings></configuration>')
& $taskRunner $AssemblyPath $taskResults
if ($LASTEXITCODE -ne 0) { throw 'Banner editor regression failed' }
Write-Host "Results: $taskResults"

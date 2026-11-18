param([string]$AssemblyPath = (Join-Path $PSScriptRoot '..\bin\Debug\PineTree.dll'), [ValidateSet('Banner','Pet')][string]$EditorKind = 'Banner')
$ErrorActionPreference = 'Stop'
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
$taskResultsName = if ($EditorKind -eq 'Pet') { 'pet-artboard-ui' } else { 'working-artboard-ui' }
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ('..\..\..\tmp\' + $taskResultsName)))
[IO.Directory]::CreateDirectory($taskRoot) | Out-Null
$taskPython = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\PineTree.PdfEngine\PdfWorker\runtime\python.exe')).Path
@'
from pathlib import Path
import sys
from pypdf import PdfWriter
from pypdf.generic import DecodedStreamObject, NameObject
from PIL import Image
root=Path(sys.argv[1]); writer=PdfWriter()
for color,width,height in [('1 0 0',600,300),('0 0 1',300,600)]:
    page=writer.add_blank_page(width=width,height=height)
    content=DecodedStreamObject(); content.set_data(f'{color} rg 0 0 {width} {height} re f'.encode())
    page[NameObject('/Contents')]=writer._add_object(content)
writer.write(root/'source.ai')
writer.write(root/'source.pdf')
Image.new('RGB',(600,300),'green').save(root/'source.jpg',dpi=(150,150))
single=PdfWriter(); single.add_page(writer.pages[0]); single.write(root/'single.ai')
'@ | & $taskPython - $taskRoot
if ($LASTEXITCODE -ne 0) { throw 'Fixture generation failed' }
$taskRunner = Join-Path $taskRoot 'WorkingArtboardRegression.exe'
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $taskCompiler /nologo /target:exe /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$taskRunner" (Join-Path $PSScriptRoot 'WorkingArtboardRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
$taskCache = [Security.SecurityElement]::Escape((Join-Path ([IO.Path]::GetTempPath()) 'PineTreeWorkingArtboardCache'))
[IO.File]::WriteAllText($taskRunner + '.config', '<configuration><appSettings><add key="PdfEngineCacheDirectory" value="' + $taskCache + '" /></appSettings></configuration>')
& $taskRunner $AssemblyPath $taskRoot $EditorKind
if ($LASTEXITCODE -ne 0) { throw 'UI regression failed' }
@'
from pathlib import Path
import sys, hashlib
from pypdf import PdfReader
import pypdfium2 as pdfium
root=Path(sys.argv[1])
for index, channel in [(1,0),(2,2)]:
    path=root/f'working-page{index}.pdf'
    reader=PdfReader(path); assert len(reader.pages)==1
    box=reader.pages[0].mediabox
    assert (float(box.width)>float(box.height))==(index==1), 'per-page dimensions were not preserved'
    with pdfium.PdfDocument(path) as doc:
        page=doc[0]; bitmap=page.render(scale=.5); image=bitmap.to_pil()
        pixel=image.getpixel((image.width//2,image.height//2)); assert pixel[channel]>240 and pixel[(channel+1)%3]<20, pixel
        image.close(); bitmap.close(); page.close()
    resources=reader.pages[0]['/Resources']
    spots=str(resources.get('/ColorSpace',{}))
    assert ('/Separation' in spots)==(index==2), spots
assert (root/'working-page2.pdf').read_bytes()==(root/'working-saved.pdf').read_bytes()
print('PASS: both selected outputs have one correct page; independent cutline and identical saved preview')
'@ | & $taskPython - $taskRoot
if ($LASTEXITCODE -ne 0) { throw 'Output verification failed' }
Write-Host "Results: $taskRoot"

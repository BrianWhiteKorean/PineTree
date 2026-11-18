param([string]$AssemblyPath = (Join-Path $PSScriptRoot '..\bin\Debug\PineTree.dll'))
$ErrorActionPreference = 'Stop'
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\tmp\design-pad-source'))
[void][IO.Directory]::CreateDirectory($taskRoot)
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
    stream=DecodedStreamObject(); stream.set_data(f'{color} rg 0 0 {width} {height} re f'.encode())
    page[NameObject('/Contents')]=writer._add_object(stream)
writer.write(root/'source.ai'); writer.write(root/'source.pdf')
Image.new('RGB',(300,600),'green').save(root/'source.jpg',dpi=(150,150))
'@ | & $taskPython - $taskRoot
if ($LASTEXITCODE -ne 0) { throw 'Fixture generation failed' }
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$taskRunner = Join-Path $taskRoot 'DesignPadSourceRegression.exe'
& $taskCompiler /nologo /target:exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Data.dll "/out:$taskRunner" (Join-Path $PSScriptRoot 'DesignPadSourceRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Regression compilation failed' }
$taskCache = [Security.SecurityElement]::Escape([IO.Path]::GetFullPath((Join-Path $taskRoot '..\source-preview-cache\cache')))
[IO.File]::WriteAllText($taskRunner + '.config', '<configuration><appSettings><add key="PdfEngineCacheDirectory" value="' + $taskCache + '" /></appSettings></configuration>')
& $taskRunner $AssemblyPath $taskRoot
if ($LASTEXITCODE -ne 0) { throw 'Shared source UI regression failed' }
@'
from pathlib import Path
import sys
from pypdf import PdfReader
import pypdfium2 as pdfium
root=Path(sys.argv[1])
for name,channel in [('banner-page1.pdf',0),('pet-page2.pdf',2)]:
    path=root/name
    assert len(PdfReader(path).pages)==1
    with pdfium.PdfDocument(path) as doc:
        page=doc[0]; bitmap=page.render(scale=.5); image=bitmap.to_pil()
        pixel=image.getpixel((image.width//2,image.height//2))
        assert pixel[channel]>240 and pixel[(channel+1)%3]<20, (name,pixel)
        image.close(); bitmap.close(); page.close()
print('PASS: generated PDFs each contain the correct single source page')
'@ | & $taskPython - $taskRoot
if ($LASTEXITCODE -ne 0) { throw 'Generated PDF verification failed' }

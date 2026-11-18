param([string]$AssemblyPath = (Join-Path $PSScriptRoot '..\bin\Debug\PineTree.dll'))
$ErrorActionPreference = 'Stop'
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\tmp\source-transform'))
[void][IO.Directory]::CreateDirectory($taskRoot)
$taskPython = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\PineTree.PdfEngine\PdfWorker\runtime\python.exe')).Path
$taskFixtureModule = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\PineTree.PdfEngine\Tests'))
@'
import sys
from pathlib import Path
sys.path.insert(0,sys.argv[2])
from PdfSourceTransformRegression import fixture
root=Path(sys.argv[1]);fixture(root/'source.pdf',2);fixture(root/'replacement.pdf')
'@ | & $taskPython - $taskRoot $taskFixtureModule
if ($LASTEXITCODE -ne 0) { throw 'Fixture generation failed' }
$taskRunner = Join-Path $taskRoot 'SourceTransformRegression.exe'
& (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe') /nologo /target:exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Data.dll "/out:$taskRunner" (Join-Path $PSScriptRoot 'SourceTransformRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Regression compilation failed' }
$taskCache = [Security.SecurityElement]::Escape([IO.Path]::GetFullPath((Join-Path $taskRoot '..\source-preview-cache\cache')))
[IO.File]::WriteAllText($taskRunner+'.config','<configuration><appSettings><add key="PdfEngineCacheDirectory" value="'+$taskCache+'" /></appSettings></configuration>')
& $taskRunner $AssemblyPath $taskRoot
if ($LASTEXITCODE -ne 0) { throw 'Source transform UI regression failed' }
@'
import sys
from pathlib import Path
from pypdf import PdfReader
import pypdfium2 as pdfium
root=Path(sys.argv[1]);pdf=root/'output.pdf';page=PdfReader(pdf).pages[0]
assert abs(float(page.mediabox.width)*25.4/72-40)<.001
assert abs(float(page.mediabox.height)*25.4/72-80)<.001
assert '/Image' not in str(page['/Resources'])
with pdfium.PdfDocument(pdf) as document:
    p=document[0];bitmap=p.render(scale=2);im=bitmap.to_pil().convert('RGB')
    for (x,y),color in zip(((0,0),(1,0),(0,1),(1,1)),((255,255,0),(0,255,0),(0,0,255),(255,0,0))):
        actual=im.getpixel((int(im.width*(.25+.5*x)),int(im.height*(.25+.5*y))))
        assert actual==color,(x,y,actual,color)
    im.close();bitmap.close();p.close()
print('PASS: generated PDF matches mixed flip and rotation, 40 x 80 mm, vector artwork preserved')
'@ | & $taskPython - $taskRoot
if ($LASTEXITCODE -ne 0) { throw 'Generated PDF verification failed' }

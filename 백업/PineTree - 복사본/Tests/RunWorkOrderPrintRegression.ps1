param([string]$AssemblyPath = (Join-Path $PSScriptRoot '..\bin\Debug\PineTree.dll'))
$ErrorActionPreference = 'Stop'
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
$results = Join-Path $PSScriptRoot ('bin\WorkOrderPrintRegression\' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($results) | Out-Null
$python = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\PineTree.PdfEngine\PdfWorker\runtime\python.exe')).Path
$fixture = Join-Path $results 'two-pages.pdf'
@'
from pypdf import PdfWriter
from pypdf.generic import DecodedStreamObject,NameObject
import sys
w=PdfWriter();w.add_blank_page(width=595,height=842)
p=w.add_blank_page(width=842,height=595)
s=DecodedStreamObject();s.set_data(b'1 0 0 rg 0 0 842 595 re f')
p[NameObject('/Contents')]=w._add_object(s)
w.write(sys.argv[1])
'@ | & $python - $fixture
if ($LASTEXITCODE -ne 0) { throw 'Multi-page fixture generation failed' }
$runner = Join-Path $results 'WorkOrderPrintRegression.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Data.dll "/out:$runner" (Join-Path $PSScriptRoot 'WorkOrderPrintRegression.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
# Requires a logged-on Windows user session with Excel and a configured printer.
# Rendering is tested in memory; no print job is submitted.
& $runner $AssemblyPath $results $fixture
if ($LASTEXITCODE -ne 0) { throw 'Work order regression failed' }
& $python (Join-Path $PSScriptRoot 'VerifyWorkOrderWorkbook.py') (Join-Path $results 'work-order.xlsx') (Join-Path $PSScriptRoot '..\Resources\작업지시서_표준.xlsx')
if ($LASTEXITCODE -ne 0) { throw 'Excel content verification failed' }
Write-Host "Results: $results"

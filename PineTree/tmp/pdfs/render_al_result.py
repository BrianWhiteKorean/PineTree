from pathlib import Path
import importlib.util
from pypdf import PdfReader
import pypdfium2 as pdfium
spec=importlib.util.spec_from_file_location('worker','../PineTree.PdfEngine/PdfWorker/pdf_worker.py');w=importlib.util.module_from_spec(spec);spec.loader.exec_module(w)
source=Path('../../1.요구분석/2.AL족자_상하여백및재단선제거_원본추정.pdf');out=Path('tmp/pdfs/al-generated.pdf')
s=dict(TargetWidthMm=904,TargetHeightMm=1482,EdgeTopMm=0,EdgeBottomMm=0,EdgeLeftMm=0,EdgeRightMm=0,MakeAlScroll=True,ScrollTopMm=5,ScrollBottomMm=5,MakeCutline=True,LineWidthMm=.18,CutInsetTopMm=2,CutInsetBottomMm=2,CutInsetLeftMm=2,CutInsetRightMm=2,MakeHole=False)
r=w.execute(dict(Operation='generate',SourcePath=str(source),OutputPath=str(out),Settings=s));print(r['Recipe'].encode('unicode_escape').decode())
d=pdfium.PdfDocument(str(out));im=d[0].render(scale=1200/max(d[0].get_size())).to_pil();im.save('tmp/pdfs/al-generated.png');d.close()

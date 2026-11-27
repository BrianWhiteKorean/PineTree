from pathlib import Path
import sys,json,importlib.util
from pypdf import PdfReader
from pypdf.generic import ContentStream
import pypdfium2 as pdfium
spec=importlib.util.spec_from_file_location('geom','Tests/CustomerPdfGeometryRegression.py');g=importlib.util.module_from_spec(spec);spec.loader.exec_module(g)
for i,p in enumerate(Path('../../1.요구분석').glob('1.*724X2154*.pdf')):
 r=PdfReader(p);pg=r.pages[0]
 print(str(p)); print(json.dumps(g.inspect(p),ensure_ascii=False));print('boxes',[(k,str(pg.get(k))) for k in ['/MediaBox','/CropBox','/TrimBox','/BleedBox','/ArtBox']]);print('metadata',str(r.metadata)[:600])
 d=pdfium.PdfDocument(str(p));im=d[0].render(scale=1200/max(d[0].get_size())).to_pil();im.save('tmp/pdfs/pet-reference-'+str(i)+'.png');d.close()
 print('render',i)

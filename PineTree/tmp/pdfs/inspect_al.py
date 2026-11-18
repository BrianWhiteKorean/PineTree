from pathlib import Path
import importlib.util,json
from pypdf import PdfReader
import pypdfium2 as pdfium
s=importlib.util.spec_from_file_location('g','Tests/CustomerPdfGeometryRegression.py');g=importlib.util.module_from_spec(s);s.loader.exec_module(g)
for i,p in enumerate(Path('../../1.요구분석').glob('2.*pdf')):
 r=PdfReader(p);pg=r.pages[0];print(p.name.encode('unicode_escape').decode());print(g.inspect(p));print('boxes',[(k,str(pg.get(k)))for k in ['/MediaBox','/CropBox','/TrimBox','/ArtBox']])
 print('text',pg.extract_text()[:150].encode('unicode_escape').decode())
 d=pdfium.PdfDocument(str(p));im=d[0].render(scale=1200/max(d[0].get_size())).to_pil();im.save('tmp/pdfs/al-reference-'+str(i)+'.png');d.close()

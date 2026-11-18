from pathlib import Path
from pypdf import PdfReader
from pypdf.generic import ContentStream
for p in Path('../../1.요구분석').glob('2.*pdf'):
 r=PdfReader(p);pg=r.pages[0];ops=ContentStream(pg.get_contents(),r).operations
 print(p.name.encode('unicode_escape').decode(),'ops',len(ops))
 print('head',ops[:18]); print('tail',ops[-18:])
 print('forms',[(str(k),str(v.get_object().get('/BBox')),str(v.get_object().get('/Matrix'))) for k,v in pg['/Resources'].get('/XObject',{}).items()])

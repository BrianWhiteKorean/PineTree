from pathlib import Path
from pypdf import PdfReader
from pypdf.generic import ContentStream
for p in Path('../../1.요구분석').glob('2.*pdf'):
 r=PdfReader(p);pg=r.pages[0]
 print(p.name.encode('unicode_escape').decode())
 def show(c,res,depth=0):
  ops=ContentStream(c,r).operations
  print(' '*depth,'ops',len(ops),'head',ascii(ops[:8]),'tail',ascii(ops[-12:]))
  if depth<3:
   for k,v in res.get('/XObject',{}).items():
    o=v.get_object()
    if o.get('/Subtype')=='/Form':
     print('form',k,'bbox',o.get('/BBox'),'matrix',o.get('/Matrix'));show(o,o.get('/Resources',res),depth+1)
 show(pg.get_contents(),pg['/Resources'])

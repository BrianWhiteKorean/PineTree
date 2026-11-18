from pathlib import Path
import json, hashlib, importlib.util
from pypdf import PdfReader
from pypdf.generic import ContentStream
import pypdfium2 as pdfium
folder=Path('../../1.요구분석/샘플파일')
out=Path('tmp/pdfs/customer-al')
spec=importlib.util.spec_from_file_location('worker',Path('../PineTree.PdfEngine/PdfWorker/pdf_worker.py'));w=importlib.util.module_from_spec(spec);spec.loader.exec_module(w)
def obj(value):return value.get_object() if hasattr(value,'get_object') else value
report={}
for ext in ('ai','pdf'):
 p=folder/('AL족자_상하여백_904X1514_1EA_별색.'+ext)
 r=PdfReader(p);page=r.pages[0]
 info={'bytes':p.stat().st_size,'pages':len(r.pages),'metadata':dict(r.metadata or {}),'rotation':page.get('/Rotate',0),'userUnit':page.get('/UserUnit',1),'boxes_mm':{k:[round(float(x)/w.MM,5) for x in getattr(page,k)] for k in ('mediabox','cropbox','trimbox','bleedbox','artbox')},'cutline':w.detect_cutline(page),'page_keys':list(page.keys())}
 data=page.get_contents().get_data();info['content_hash']=hashlib.sha256(data).hexdigest();info['content_length']=len(data)
 (out/(ext+'-content.txt')).write_bytes(data)
 res=obj(page.get('/Resources',{}));info['colors']={str(k):str(obj(v)) for k,v in obj(res.get('/ColorSpace',{})).items()}
 info['xobjects']={str(k):{'type':str(obj(v).get('/Subtype')),'bbox':str(obj(v).get('/BBox')),'length':len(obj(v).get_data()),'hash':hashlib.sha256(obj(v).get_data()).hexdigest()} for k,v in obj(res.get('/XObject',{})).items()}
 report[ext]=info
 with pdfium.PdfDocument(str(p)) as doc:
  pg=doc[0];width,height=pg.get_size();bitmap=pg.render(scale=1500/max(width,height));bitmap.to_pil().save(out/(ext+'-full.png'));bitmap.close();pg.close()
(out/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2,default=str),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2,default=str))

from pathlib import Path
import importlib.util
from pypdf import PdfReader
from PIL import ImageChops,ImageStat
import pypdfium2 as pdfium
spec=importlib.util.spec_from_file_location('pet','../PineTree.PdfEngine/Tests/PdfPetCutRegression.py');t=importlib.util.module_from_spec(spec);spec.loader.exec_module(t)
out=Path('tmp/pdfs/pet-generated.pdf');t.worker.execute(dict(Operation='generate',SourcePath=str(t.SOURCE),OutputPath=str(out),Settings=t.PetCutTests().settings()))
imgs=[]
for p in (t.REFERENCE,out):
 d=pdfium.PdfDocument(str(p));im=d[0].render(scale=1800/max(d[0].get_size())).to_pil().convert('RGB');imgs.append(im);im.save(str(p if p==out else Path('tmp/pdfs/pet-customer')).replace('.pdf','')+'.png');d.close()
print('render sizes',imgs[0].size,imgs[1].size)
print('mean RGB difference',ImageStat.Stat(ImageChops.difference(imgs[0],imgs[1].resize(imgs[0].size))).mean)
for im,label in zip(imgs,['reference','generated']):
 im.crop((0,0,90,100)).resize((450,500)).save('tmp/pdfs/pet-'+label+'-corner.png')

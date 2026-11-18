import hashlib
import importlib.util
from pathlib import Path
import tempfile
import unittest
from PIL import Image
from pypdf import PdfReader, PdfWriter
from pypdf.generic import DecodedStreamObject, NameObject, RectangleObject
import pypdfium2 as pdfium
PROJECT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('worker', PROJECT/'PdfWorker/pdf_worker.py')
worker = importlib.util.module_from_spec(spec); spec.loader.exec_module(worker)

def fixture(path, pages=1):
    writer = PdfWriter()
    for _ in range(pages):
        page = writer.add_blank_page(width=100*worker.MM, height=80*worker.MM)
        page.cropbox = RectangleObject([10*worker.MM,20*worker.MM,90*worker.MM,60*worker.MM])
        stream = DecodedStreamObject()
        content = []
        for color, x, y in [('1 0 0',10,40), ('0 1 0',50,40), ('0 0 1',10,20), ('1 1 0',50,20)]:
            content.append(f'{color} rg {x*worker.MM} {y*worker.MM} {40*worker.MM} {20*worker.MM} re f')
        stream.set_data(' '.join(content).encode()); page[NameObject('/Contents')] = writer._add_object(stream)
    writer.write(path)

class TransformTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.folder = Path(self.temp.name)
        self.source = self.folder/'source.pdf'; fixture(self.source)
        self.digest = hashlib.sha256(self.source.read_bytes()).hexdigest()
    def tearDown(self):
        self.assertEqual(hashlib.sha256(self.source.read_bytes()).hexdigest(),self.digest)
        self.temp.cleanup()
    def generate(self, rotation, flip, **extra):
        width,height = (40,80) if rotation%180 else (80,40)
        settings = dict(TargetWidthMm=width,TargetHeightMm=height,ExpandImage=True,
                        SourceRotationDegreesClockwise=rotation,SourceFlipHorizontal=flip,
                        MakeCutline=False,MakeHole=False,EdgeLeftMm=0,EdgeRightMm=0,EdgeTopMm=0,EdgeBottomMm=0)
        settings.update(extra); out = self.folder/'output.pdf'
        result = worker.execute(dict(Operation='generate',SourcePath=str(self.source),OutputPath=str(out),Settings=settings))
        return result, PdfReader(out), out
    def test_all_orientations_and_crop_offsets(self):
        base = Image.new('RGB',(2,2)); base.putdata([(255,0,0),(0,255,0),(0,0,255),(255,255,0)])
        for angle in (0,90,180,270):
            for flip in (False,True):
                with self.subTest(angle=angle,flip=flip):
                    result, reader, output = self.generate(angle,flip)
                    expected = base.rotate(-angle)
                    if flip: expected = expected.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                    with pdfium.PdfDocument(output) as doc:
                        pg=doc[0]; bitmap=pg.render(scale=2); image=bitmap.to_pil().convert('RGB')
                        for x,y in ((0,0),(1,0),(0,1),(1,1)):
                            pixel=image.getpixel((int(image.width*(.25+.5*x)),int(image.height*(.25+.5*y))))
                            self.assertEqual(pixel,expected.getpixel((x,y)))
                        image.close(); bitmap.close(); pg.close()
                    self.assertAlmostEqual(result['OutputWidthMm'],40 if angle%180 else 80)
                    self.assertAlmostEqual(result['OutputHeightMm'],80 if angle%180 else 40)
                    self.assertNotIn('/Image', str(reader.pages[0]['/Resources']))
    def test_finishing_is_applied_after_transform(self):
        result,reader,_=self.generate(90,True,HemSewPasses=[dict(TypeCode='SINGLE',LineThicknessMm=.06)],
                                      MakeCutline=True,CutInsetsFromPage=True,CutInsetTopMm=3,CutInsetBottomMm=5,CutInsetLeftMm=7,CutInsetRightMm=9)
        self.assertAlmostEqual(result['OutputWidthMm'],70);self.assertAlmostEqual(result['OutputHeightMm'],110)
        p=reader.pages[0];self.assertAlmostEqual(float(p.trimbox.left)/worker.MM,7,places=6)
        self.assertAlmostEqual((float(p.mediabox.top)-float(p.trimbox.top))/worker.MM,3,places=6)
    def test_all_page_dimension_calculation_uses_rotation(self):
        reader=PdfReader(self.source);out=self.folder/'all.pdf'
        result=worker.generate_pages(self.source,reader,{},out,dict(SourceSizingMode='SCALE',SourceScale=10,
             RoundSourceDimensionsBeforeScale=True,SourceRotationDegreesClockwise=90,SourceFlipHorizontal=True,
             MakeCutline=False,MakeHole=False))
        self.assertAlmostEqual(result['OutputWidthMm'],400);self.assertAlmostEqual(result['OutputHeightMm'],800)
    def test_invalid_transform_rejected(self):
        for angle in (45,-90,360,True,'90'):
            with self.assertRaises(ValueError): self.generate(0,False,SourceRotationDegreesClockwise=angle)
        with self.assertRaises(ValueError): self.generate(0,False,SourceFlipHorizontal='yes')

if __name__=='__main__':unittest.main(verbosity=2)

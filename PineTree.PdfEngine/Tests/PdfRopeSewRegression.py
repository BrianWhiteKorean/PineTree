import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from PIL import Image, ImageChops, ImageStat
from pypdf import PdfReader
from pypdf.generic import ContentStream

PROJECT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('worker', PROJECT/'PdfWorker/pdf_worker.py')
worker = importlib.util.module_from_spec(spec); spec.loader.exec_module(worker)
DATA = PROJECT.parents[1]/'4.테스트데이터'
SOURCE = DATA/'(원본)현수막_사방줄미싱_1830X1230_10배_1EA.pdf'
REFERENCE = DATA/'현수막_사방줄미싱_1830X1230_10배_1EA.pdf'

def rectangles(page):
    width = 1; pending = []; result = []; stack = []
    for args, op in ContentStream(page.get_contents(), page.pdf).operations:
        if op == b'q': stack.append(width)
        elif op == b'Q' and stack: width = stack.pop()
        elif op == b'w': width = float(args[0])
        elif op == b're':
            x,y,w,h=map(float,args)
            pending.append((min(x,x+w),min(y,y+h),abs(w),abs(h)))
        elif op == b'S':
            result.extend((rect,width) for rect in pending); pending=[]
        elif op in (b'n',b'f',b'f*'): pending=[]
    return result

class RopeTests(unittest.TestCase):
    def setUp(self): self.temp=tempfile.TemporaryDirectory();self.folder=Path(self.temp.name)
    def tearDown(self): self.temp.cleanup()
    def settings(self, **changes):
        p=PdfReader(SOURCE).pages[0]
        settings=dict(TargetWidthMm=float(p.mediabox.width)/worker.MM,TargetHeightMm=float(p.mediabox.height)/worker.MM,
            EdgeTopMm=0,EdgeBottomMm=0,EdgeLeftMm=0,EdgeRightMm=0,MakeCutline=False,MakeHole=False,
            RopeSewPasses=[dict(TypeCode='ROPE_5',TopMm=3,BottomMm=3,LeftMm=3,RightMm=3,LineThicknessMm=.17/worker.MM)])
        settings.update(changes);return settings
    def generate(self, **changes):
        output=self.folder/'rope.pdf'
        result=worker.execute(dict(Operation='generate',SourcePath=str(SOURCE),OutputPath=str(output),Settings=self.settings(**changes)))
        return result,PdfReader(output),output
    def test_physical_margins_at_original_and_ten_times(self):
        for width, height in ((180, 120), (1800, 1200)):
            with self.subTest(width=width):
                rope = dict(TypeCode='ROPE_5', TopMm=15, BottomMm=15, LeftMm=15, RightMm=15,
                            LineThicknessMm=.17/worker.MM)
                result, reader, output = self.generate(TargetWidthMm=width, TargetHeightMm=height,
                                                       ExpandImage=True, RopeSewPasses=[rope])
                page = reader.pages[0]
                self.assertAlmostEqual(float(page.mediabox.width)/worker.MM, width+30, places=5)
                self.assertAlmostEqual(float(page.mediabox.height)/worker.MM, height+30, places=5)
                rects = sorted(rectangles(page))
                self.assertEqual(len(rects), 2)
                outer, stroke = rects[0]
                self.assertAlmostEqual(outer[0]-stroke/2, 0, places=6)
                self.assertAlmostEqual(outer[1]-stroke/2, 0, places=6)
                self.assertAlmostEqual(outer[0]+outer[2]+stroke/2, float(page.mediabox.width), delta=.00001)
                inner = rects[1][0]
                self.assertAlmostEqual(inner[0]/worker.MM, 15, places=6)
                self.assertAlmostEqual(inner[1]/worker.MM, 15, places=6)
                worker.render_preview(output,page,tuple(page.cropbox),self.folder/'actual.png',1200,True)
                with Image.open(self.folder/'actual.png') as image:
                    self.assertEqual(image.convert('RGB').getpixel((5,image.height//2)), (255,255,255))

    def test_ten_times_output_and_custom_margins(self):
        rope=dict(TypeCode='ROPE_8',TopMm=30,BottomMm=40,LeftMm=50,RightMm=60,LineThicknessMm=.17/worker.MM*10)
        result,reader,_=self.generate(TargetWidthMm=1800,TargetHeightMm=1200,RopeSewPasses=[rope])
        self.assertAlmostEqual(result['OutputWidthMm'],1910)
        self.assertAlmostEqual(result['OutputHeightMm'],1270)
        inner=sorted(rectangles(reader.pages[0]))[1][0]
        self.assertAlmostEqual(inner[0]/worker.MM,50,places=6)
        self.assertAlmostEqual(inner[1]/worker.MM,40,places=6)
        snapshot=json.loads(reader.metadata['/PineTreeGenerationSettings'])
        self.assertEqual(snapshot['settings']['RopeSewPasses'][0]['TypeCode'],'ROPE_8')
    def test_existing_operations_compose(self):
        result,reader,_=self.generate(MakeWood=True,WoodExtraMm=8,MakeWoodDesignLine=True,
            MakeWoodTopBottom=True,WoodTopBottomExtraMm=6,MakeCutline=True,MakeHole=True,
            MarkLeftRightMm=10,MarkTopBottomMm=10,EdgeLeftMm=2,EdgeTopMm=4)
        self.assertAlmostEqual(result['OutputWidthMm'],180.000275+16+6+2)
        self.assertEqual(result['MarkCount'],4)
        self.assertTrue(worker.has_thru(reader.pages[0]['/Resources']))
    def test_multiple_passes_preserved(self):
        ropes=self.settings()['RopeSewPasses']*2
        result,reader,_=self.generate(RopeSewPasses=ropes)
        self.assertAlmostEqual(result['OutputWidthMm'],180.000275+12)
        self.assertEqual(len(rectangles(reader.pages[0])),4)
    def test_required_values_and_limits(self):
        for key in ('TopMm','BottomMm','LeftMm','RightMm','LineThicknessMm'):
            for value in (0,-1,None,'',float('nan'),float('inf')):
                rope=dict(self.settings()['RopeSewPasses'][0]);rope[key]=value
                with self.assertRaises(ValueError):self.generate(RopeSewPasses=[rope])
        for change in (dict(RopeSewPasses=[dict(TypeCode='UNKNOWN')]),dict(TargetWidthMm=14400),dict(RopeSewPasses=[None])):
            with self.assertRaises(ValueError):self.generate(**change)

if __name__=='__main__':unittest.main(verbosity=2)

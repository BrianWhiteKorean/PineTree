import importlib.util
from pathlib import Path
import tempfile
import unittest
from PIL import Image, ImageChops, ImageStat
from pypdf import PdfReader
from pypdf.generic import ContentStream

PROJECT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('worker',PROJECT/'PdfWorker/pdf_worker.py')
worker=importlib.util.module_from_spec(spec);spec.loader.exec_module(worker)
SAMPLES=PROJECT.parents[1]/'1.요구분석/1026'
SOURCE=SAMPLES/'현수막_좌우각목_8000x600_1EA.ai'
REFERENCE=SOURCE.with_suffix('.pdf')

def guide_lines(page, vertical=True):
    # This reference's guides are page-level process-black straight strokes.
    matrix=(1,0,0,1,0,0);stack=[];width=1;path=[];result=[]
    def point(values):
        x,y=map(float,values);a,b,c,d,e,f=matrix
        return ((a*x+c*y+e)/worker.MM,(b*x+d*y+f)/worker.MM)
    for args,op in ContentStream(page.get_contents(),page.pdf).operations:
        if op==b'q':stack.append((matrix,width))
        elif op==b'Q' and stack:matrix,width=stack.pop()
        elif op==b'cm':
            a,b,c,d,e,f=map(float,args);A,B,C,D,E,F=matrix
            matrix=(a*A+b*C,a*B+b*D,c*A+d*C,c*B+d*D,e*A+f*C+E,e*B+f*D+F)
        elif op==b'w':width=float(args[0])
        elif op in (b'm',b'l'):path.append((op,point(args)))
        elif op in (b'c',b'v',b'y',b're'):path.append((op,None))
        elif op==b'S':
            if len(path)==2 and path[0][0]==b'm' and path[1][0]==b'l':
                a,b=path[0][1],path[1][1]
                if vertical and abs(a[0]-b[0])<.0001:result.append((a[0],min(a[1],b[1]),abs(a[1]-b[1]),width))
                if not vertical and abs(a[1]-b[1])<.0001:result.append((a[1],min(a[0],b[0]),abs(a[0]-b[0]),width))
            path=[]
        elif op in (b'f',b'f*',b'F',b'B',b'B*',b'n',b's',b'b',b'b*'):path=[]
    return sorted(result)

def vertical_lines(page): return guide_lines(page)
def horizontal_lines(page): return guide_lines(page,False)

class WoodTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.folder=Path(self.temp.name)
    def tearDown(self):self.temp.cleanup()
    def generate(self,extra=8.03,design=True,offset=0,**changes):
        settings=dict(TargetWidthMm=800,TargetHeightMm=59,FitMode='CONTAIN',
            EdgeTopMm=0,EdgeBottomMm=0,EdgeLeftMm=0,EdgeRightMm=0,MakeCutline=False,MakeHole=False,
            MakeWood=True,WoodExtraMm=extra,MakeWoodDesignLine=design,WoodDesignOffsetMm=offset)
        settings.update(changes);output=self.folder/'wood.pdf'
        result=worker.execute(dict(Operation='generate',SourcePath=str(SOURCE),OutputPath=str(output),Settings=settings))
        return result,PdfReader(output),output
    def test_reference_geometry(self):
        result,reader,output=self.generate()
        expected=PdfReader(REFERENCE).pages[0];actual=reader.pages[0]
        self.assertAlmostEqual(result['OutputWidthMm'],816.06,places=6)
        self.assertAlmostEqual(result['OutputHeightMm'],59,places=6)
        self.assertAlmostEqual(float(expected.mediabox.width)/worker.MM,result['OutputWidthMm'],places=3)
        a,b=vertical_lines(actual),vertical_lines(expected)
        self.assertEqual(len(a),4);self.assertEqual(len(b),4)
        for line,reference in zip(a,b):
            for value,wanted in zip(line,reference):self.assertAlmostEqual(value,wanted,places=3)
        self.assertAlmostEqual(result['Scale'],1,places=4)
        # White extra space, unchanged original picture, and full-height black guides.
        for path,name in ((output,'actual.png'),(REFERENCE,'reference.png')):
            page=PdfReader(path).pages[0]
            worker.render_preview(path,page,tuple(page.cropbox),self.folder/name,2200,True)
        with Image.open(self.folder/'actual.png') as image,Image.open(self.folder/'reference.png') as ref:
            self.assertEqual(image.convert('RGB').getpixel((10,50)),(255,255,255))
            ref=ref.convert('RGB').resize(image.size)
            diff=ImageStat.Stat(ImageChops.difference(image.convert('RGB'),ref)).mean
            self.assertLess(sum(diff)/3,3)
    def test_design_offset_never_changes_page_size(self):
        result,reader,_=self.generate(extra=80,offset=20)
        self.assertEqual(result['OutputWidthMm'],960)
        xs=[x[0] for x in vertical_lines(reader.pages[0])]
        for value,wanted in zip(xs,[.17/worker.MM/2,60,900,960-.17/worker.MM/2]):self.assertAlmostEqual(value,wanted,places=5)
    def test_off_and_zero(self):
        result,reader,_=self.generate(extra=8,design=False)
        self.assertEqual(result['OutputWidthMm'],816);self.assertEqual(len(vertical_lines(reader.pages[0])),2)
        result,reader,_=self.generate(extra=0,design=False)
        self.assertAlmostEqual(result['OutputWidthMm'],800,places=5);self.assertEqual(vertical_lines(reader.pages[0]),[])
    def test_bleed_cut_holes_use_expanded_finished_width(self):
        result,reader,_=self.generate(extra=8,design=True,EdgeLeftMm=2,EdgeRightMm=2,EdgeTopMm=2,EdgeBottomMm=2,MakeCutline=True,MakeHole=True)
        self.assertEqual(result['OutputWidthMm'],820);self.assertEqual(result['MarkCount'],4)
        self.assertAlmostEqual(float(reader.pages[0].trimbox.width)/worker.MM,816,places=5)
    def test_invalid_offsets(self):
        for extra,offset in ((-1,0),(8,-1),(8,9)):
            with self.subTest(extra=extra,offset=offset):
                with self.assertRaises(ValueError):self.generate(extra=extra,offset=offset)

class TopBottomTests(unittest.TestCase):
    setUp = WoodTests.setUp
    tearDown = WoodTests.tearDown
    generate = WoodTests.generate
    def top(self,extra=12,design=True,offset=4,**changes):
        return self.generate(extra=0,design=False,MakeWood=False,MakeWoodTopBottom=True,
            WoodTopBottomExtraMm=extra,MakeWoodTopBottomDesignLine=design,WoodTopBottomDesignOffsetMm=offset,**changes)
    def test_top_bottom_geometry_and_offset(self):
        result,reader,_=self.top()
        self.assertAlmostEqual(result['OutputWidthMm'],800,places=5)
        self.assertAlmostEqual(result['OutputHeightMm'],83,places=5)
        lines=horizontal_lines(reader.pages[0]);self.assertEqual(len(lines),4)
        for line,expected in zip(lines,[.17/worker.MM/2,8,75,83-.17/worker.MM/2]):
            self.assertAlmostEqual(line[0],expected,places=5)
            self.assertAlmostEqual(line[2],800,places=5)
        self.assertEqual(vertical_lines(reader.pages[0]),[])
        _,page,path=self.top(offset=0)
        self.assertAlmostEqual(float(page.pages[0].mediabox.height)/worker.MM,83,places=5)
    def test_top_blank_and_design_off(self):
        result,page,_=self.top(design=False)
        self.assertEqual(len(horizontal_lines(page.pages[0])),2)
        result,page,_=self.top(extra=0,design=False,offset=0)
        self.assertAlmostEqual(result['OutputHeightMm'],59,places=5)
        self.assertEqual(horizontal_lines(page.pages[0]),[])
    def test_top_after_ten_times_zoom_is_absolute(self):
        result,page,_=self.top(extra=80,offset=20,TargetWidthMm=8000,TargetHeightMm=590)
        self.assertAlmostEqual(result['OutputHeightMm'],750,places=5)
        ys=[line[0] for line in horizontal_lines(page.pages[0])]
        self.assertAlmostEqual(ys[1],60,places=5)
        self.assertAlmostEqual(ys[2],690,places=5)
    def test_both_axes_and_cut_holes(self):
        result,page,_=self.generate(extra=8,design=True,MakeWoodTopBottom=True,WoodTopBottomExtraMm=12,
            MakeWoodTopBottomDesignLine=True,WoodTopBottomDesignOffsetMm=4,MakeCutline=True,MakeHole=True,
            EdgeTopMm=2,EdgeBottomMm=2,EdgeLeftMm=2,EdgeRightMm=2)
        self.assertAlmostEqual(result['OutputWidthMm'],820,places=5)
        self.assertAlmostEqual(result['OutputHeightMm'],87,places=5)
        self.assertEqual(result['MarkCount'],4)
        self.assertEqual(len(vertical_lines(page.pages[0])),4)
        self.assertEqual(len(horizontal_lines(page.pages[0])),4)
        self.assertAlmostEqual(float(page.pages[0].trimbox.height)/worker.MM,83,places=5)
    def test_top_invalid_settings(self):
        for extra,offset in [(-1,0),(12,-1),(12,13)]:
            with self.assertRaises(ValueError):self.top(extra=extra,offset=offset)

if __name__=='__main__':unittest.main(verbosity=2)

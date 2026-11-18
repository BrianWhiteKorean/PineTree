"""AL scroll: white, absolute top/bottom margins and a spot cut inside the output page."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ContentStream,DecodedStreamObject,NameObject
import pypdfium2 as pdfium
PROJECT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('worker',PROJECT/'PdfWorker/pdf_worker.py')
worker=importlib.util.module_from_spec(spec);spec.loader.exec_module(worker)
SAMPLES=PROJECT.parents[1]/'1.요구분석'
class ScrollTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory();self.folder=Path(self.temp.name);self.source=self.folder/'source.pdf'
        w=PdfWriter();p=w.add_blank_page(width=200*worker.MM,height=100*worker.MM)
        c=DecodedStreamObject();c.set_data(f'1 0 0 rg 0 0 {200*worker.MM} {100*worker.MM} re f'.encode())
        p[NameObject('/Contents')]=w._add_object(c);w.write(self.source)
    def tearDown(self):self.temp.cleanup()
    def settings(self,**changes):
        s=dict(TargetWidthMm=200,TargetHeightMm=100,EdgeTopMm=0,EdgeBottomMm=0,EdgeLeftMm=0,EdgeRightMm=0,
               MakeAlScroll=True,ScrollTopMm=5,ScrollBottomMm=5,MakeCutline=True,LineWidthMm=.18,
               CutInsetTopMm=2,CutInsetBottomMm=2,CutInsetLeftMm=2,CutInsetRightMm=2,MakeHole=False)
        s.update(changes);return s
    def generate(self,**changes):
        output=self.folder/'output.pdf';worker.execute(dict(Operation='generate',SourcePath=str(self.source),OutputPath=str(output),Settings=self.settings(**changes)))
        r=PdfReader(output);p=r.pages[0];ops=ContentStream(p.get_contents(),r).operations
        cut=[list(map(float,a)) for a,op in ops if op==b're'][-1]
        return output,p,[v/worker.MM for v in cut]
    def test_white_margins_and_artwork_scale(self):
        output,p,cut=self.generate(ScrollTopMm=7,ScrollBottomMm=13)
        for a,b in zip(cut,(2,2,196,116)):self.assertAlmostEqual(a,b,places=5)
        self.assertAlmostEqual(float(p.mediabox.height)/worker.MM,120,places=5)
        doc=pdfium.PdfDocument(str(output));im=doc[0].render(scale=1/worker.MM).to_pil().convert('RGB')
        self.assertEqual(im.getpixel((100,3)),(255,255,255));self.assertEqual(im.getpixel((100,115)),(255,255,255))
        self.assertEqual(im.getpixel((100,8)),(255,0,0));self.assertEqual(im.getpixel((100,106)),(255,0,0));doc.close()
    def test_enlargement_does_not_scale_finishing_margins(self):
        _,p,cut=self.generate(TargetWidthMm=2000,TargetHeightMm=1000,ScrollTopMm=7,ScrollBottomMm=13)
        self.assertAlmostEqual(float(p.mediabox.height)/worker.MM,1020,places=4)
        for a,b in zip(cut,(2,2,1996,1016)):self.assertAlmostEqual(a,b,places=4)
    def test_zero_and_invalid(self):
        _,p,_=self.generate(ScrollTopMm=0,ScrollBottomMm=0)
        self.assertAlmostEqual(float(p.mediabox.height)/worker.MM,100,places=5)
        for c in (dict(ScrollTopMm=-1),dict(ScrollBottomMm=float('nan')),dict(ScrollTopMm=15000)):
            with self.assertRaises(ValueError):worker.generation_settings(self.settings(**c))
    def test_reference_cut_geometry_and_restored_source_limitation(self):
        reference=PdfReader(SAMPLES/'2.AL족자_상하여백_904X1514_1EA_별색.pdf').pages[0]
        self.source=SAMPLES/'2.AL족자_상하여백및재단선제거_원본추정.pdf'
        source=PdfReader(self.source).pages[0]
        sw,sh=float(source.mediabox.width)/worker.MM,float(source.mediabox.height)/worker.MM
        output,p,cut=self.generate(TargetWidthMm=sw,TargetHeightMm=sh)
        self.assertAlmostEqual(float(p.mediabox.height)/worker.MM,sh+10,places=4)
        self.assertGreater(float(reference.mediabox.height)/worker.MM-(sh+10),20)
        self.assertTrue(worker.detect_cutline(p)['HasCutline'])
        self.assertFalse(worker.detect_cutline(source)['HasCutline'])
    def test_holes_follow_expanded_height(self):
        s=worker.generation_settings(self.settings(MakeHole=True,MarkLeftRightMm=20,MarkTopBottomMm=20))
        self.assertEqual(sorted(worker.mark_positions(s)),[(20,20),(20,90),(180,20),(180,90)])
if __name__=='__main__':unittest.main()

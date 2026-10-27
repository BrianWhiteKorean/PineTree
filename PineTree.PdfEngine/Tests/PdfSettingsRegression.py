"""Meaningful geometry/render checks for the direct order-file controls."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from PIL import Image
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ContentStream, DecodedStreamObject, NameObject
import pypdfium2 as pdfium

PROJECT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('worker', PROJECT/'PdfWorker/pdf_worker.py')
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)


class SettingsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.folder = Path(self.temp.name)
        self.source = self.folder/'wide.pdf'
        writer = PdfWriter()
        page = writer.add_blank_page(width=200*worker.MM, height=100*worker.MM)
        stream = DecodedStreamObject()
        stream.set_data(f'1 0 0 rg 0 0 {200*worker.MM} {100*worker.MM} re f'.encode('ascii'))
        page[NameObject('/Contents')] = writer._add_object(stream)
        writer.write(self.source)

    def tearDown(self):
        self.temp.cleanup()

    def generate(self, settings):
        output = self.folder/'output.pdf'
        result = worker.execute({'Operation':'generate','SourcePath':str(self.source),
                                 'OutputPath':str(output),'Settings':settings})
        return result, PdfReader(output), output

    def settings(self, **changes):
        return dict(TargetWidthMm=100, TargetHeightMm=100, EdgeLeftMm=0, EdgeRightMm=0,
                    EdgeTopMm=0, EdgeBottomMm=0, MakeCutline=False, MakeHole=False, **changes)

    def test_cutline_matches_trim_box_after_corner_bleed(self):
        writer = PdfWriter()
        page = writer.add_blank_page(width=820*worker.MM, height=59*worker.MM)
        stream = DecodedStreamObject()
        stream.set_data(f'1 0 0 rg 0 0 {820*worker.MM} {59*worker.MM} re f'.encode('ascii'))
        page[NameObject('/Contents')] = writer._add_object(stream)
        writer.write(self.source)
        for mode in ('CONTAIN', 'COVER'):
            for left, right, top, bottom in ((2, 2, 2, 2), (1, 3, 5, 7), (0, 0, 0, 0)):
                with self.subTest(mode=mode, edges=(left, right, top, bottom)):
                    settings = self.settings()
                    settings.update(TargetWidthMm=820, TargetHeightMm=59, FitMode=mode,
                        EdgeLeftMm=left, EdgeRightMm=right, EdgeTopMm=top, EdgeBottomMm=bottom,
                        MakeCutline=True, LineWidthMm=.18)
                    _, reader, _ = self.generate(settings)
                    content = ContentStream(reader.pages[0].get_contents(), reader)
                    rectangles = [values for values, operator in content.operations if operator == b're']
                    actual = [float(v)/worker.MM for v in rectangles[-1]]
                    for value, expected in zip(actual, (left, bottom, 820, 59)):
                        self.assertAlmostEqual(value, expected, places=5)

    def test_asymmetric_edges_and_optional_marks(self):
        s=self.settings();s.update(EdgeLeftMm=1,EdgeRightMm=3,EdgeTopMm=5,EdgeBottomMm=7)
        result,r,_=self.generate(s);p=r.pages[0]
        self.assertAlmostEqual(float(p.mediabox.width)/worker.MM,104,places=5)
        self.assertAlmostEqual(float(p.mediabox.height)/worker.MM,112,places=5)
        self.assertAlmostEqual(float(p.trimbox.bottom)/worker.MM,7,places=5)
        self.assertFalse(worker.has_thru(p['/Resources']))
        self.assertEqual(result['MarkCount'],0)

    def test_contain_letterbox_and_cover_crop(self):
        for mode, expected in [('CONTAIN',(255,255,255)),('COVER',(255,0,0))]:
            s=self.settings(FitMode=mode)
            result,_,output=self.generate(s)
            with pdfium.PdfDocument(str(output)) as doc:
                image=doc[0].render(scale=1).to_pil().convert('RGB')
                self.assertEqual(image.getpixel((image.width//2,10)),expected)
                self.assertEqual(image.getpixel((image.width//2,image.height//2)),(255,0,0))
            self.assertAlmostEqual(result['Scale'],.5 if mode=='CONTAIN' else 1)

    def test_spot_name_line_width_and_disabled_punch(self):
        s=self.settings();s.update(MakeCutline=True,SpotColorName='Kiss Cut#1',LineWidthMm=.35)
        _,r,output=self.generate(s);p=r.pages[0]
        self.assertTrue(worker.has_thru(p['/Resources'],spot_name='Kiss Cut#1'))
        ops=ContentStream(p.get_contents(),r).operations
        self.assertTrue(any(op==b'w' and abs(float(a[0])-.35*worker.MM)<1e-6 for a,op in ops))
        self.assertFalse(any(op==b'gs' and a[0]=='/BlackOverprint' for a,op in ops))

    def test_four_corners_positions(self):
        s=self.settings();s.update(MakeHole=True,MarkLeftRightMm=10,MarkTopBottomMm=15)
        result,r,_=self.generate(s)
        snap=json.loads(r.metadata['/PineTreeGenerationSettings'])
        self.assertEqual(result['MarkCount'],4)
        self.assertEqual(worker.mark_positions(snap['settings']),[(10,15),(10,85),(90,15),(90,85)])

    def test_custom_max_spacing_and_no_duplicate_corners(self):
        s=self.settings();s.update(MakeHole=True,HoleType='CUSTOM_SPACING',HoleSpacingMm=30,
                                  MarkLeftRightMm=10,MarkTopBottomMm=10)
        result,r,_=self.generate(s)
        positions=worker.mark_positions(worker.generation_settings(s))
        self.assertEqual(result['MarkCount'],12)
        self.assertEqual(len(set(positions)),12)
        self.assertEqual(sum(op==b'gs' and a[0]=='/BlackOverprint'
                             for a,op in ContentStream(r.pages[0].get_contents(),r).operations),12)

    def test_invalid_input_rejected_before_output_write(self):
        for change in [dict(TargetWidthMm=0),dict(EdgeLeftMm=-1),dict(TargetWidthMm=float('nan')),
                       dict(FitMode='STRETCH'),dict(MakeHole=True,MarkLeftRightMm=60),
                       dict(MakeHole=True,HoleType='CUSTOM_SPACING',HoleSpacingMm=.0001)]:
            s=self.settings();s.update(change)
            with self.assertRaises(ValueError):self.generate(s)
            self.assertFalse((self.folder/'output.pdf').exists())

    def test_pdf_ai_eps_jpg_accept_settings(self):
        jpg=self.folder/'input.jpg';Image.new('RGB',(200,100),'red').save(jpg)
        eps=self.folder/'input.eps';eps.write_text('%!PS-Adobe-3.0 EPSF-3.0\n%%BoundingBox: 0 0 200 100\n1 0 0 setrgbcolor 0 0 200 100 rectfill\nshowpage\n',encoding='ascii')
        ai=self.folder/'input.ai';ai.write_bytes(self.source.read_bytes())
        for source in (self.source,ai,eps,jpg):
            result=worker.execute({'Operation':'generate','SourcePath':str(source),
                'OutputPath':str(self.folder/(source.suffix[1:]+'-result.pdf')),'Settings':self.settings()})
            self.assertAlmostEqual(result['OutputWidthMm'],100)

    def test_ai_sample_design_regions(self):
        folder=PROJECT.parents[1]/'1.요구분석/샘플파일'
        for name, size in [('AL족자_상하여백_904X1514_1EA_별색.ai',(904,1504)),
                           ('현수막_상하원형각목_700x1000.ai',(700,1000))]:
            result=worker.execute({'Operation':'inspect','SourcePath':str(folder/name)})
            self.assertAlmostEqual(result['DesignWidthMm'],size[0],places=2)
            self.assertAlmostEqual(result['DesignHeightMm'],size[1],places=2)

if __name__=='__main__':unittest.main(verbosity=2)

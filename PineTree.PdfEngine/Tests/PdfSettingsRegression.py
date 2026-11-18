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

    def test_hole_colors_for_dot_and_cross(self):
        for kind in ('DOT', 'CROSS'):
            with self.subTest(kind=kind):
                s = self.settings()
                s.update(MakeHole=True, HoleMarkKind=kind, MarkLeftRightMm=20, MarkTopBottomMm=20,
                         HoleBackgroundColor='#00FF00', HoleMarkerColor='#0000FF')
                _, reader, path = self.generate(s)
                snapshot = json.loads(reader.metadata['/PineTreeGenerationSettings'])
                self.assertEqual(snapshot['settings']['HoleBackgroundColor'], '#00FF00')
                with pdfium.PdfDocument(path) as doc:
                    page = doc[0]; bitmap = page.render(scale=4)
                    image = bitmap.to_pil().convert('RGB')
                    x, y = round(20*worker.MM*4), round(80*worker.MM*4)
                    center = image.getpixel((x, y))
                    halo = image.getpixel((x+5, y if kind == 'DOT' else y+5))
                    self.assertGreater(center[2], 120); self.assertGreater(center[2]-center[0], 70)
                    self.assertGreater(halo[1], 120); self.assertGreater(halo[1]-halo[2], 60)
                    image.close(); bitmap.close(); page.close()
        for color in ('red', '#GG0000', '#FFFF', 12):
            s = self.settings(); s.update(MakeHole=True, HoleBackgroundColor=color)
            with self.assertRaises(ValueError): self.generate(s)

    def test_banner_cut_insets_and_thickness_in_output_mm(self):
        for width, height in ((100, 80), (1000, 800)):
            with self.subTest(width=width):
                s = self.settings()
                s.update(TargetWidthMm=width, TargetHeightMm=height, MakeCutline=True,
                         LineWidthMm=.25, CutInsetsFromPage=True, ExpandImage=True,
                         EdgeLeftMm=2, EdgeRightMm=4, EdgeTopMm=6, EdgeBottomMm=8,
                         CutInsetTopMm=3, CutInsetBottomMm=5, CutInsetLeftMm=7, CutInsetRightMm=11)
                _, reader, _ = self.generate(s)
                page = reader.pages[0]
                self.assertAlmostEqual(float(page.mediabox.width)/worker.MM, width+6, places=5)
                self.assertAlmostEqual(float(page.mediabox.height)/worker.MM, height+14, places=5)
                ops = ContentStream(page.get_contents(), reader).operations
                rect = [list(map(float, args)) for args, op in ops if op == b're'][-1]
                for actual, expected in zip(rect, (7, 5, width+6-18, height+14-8)):
                    self.assertAlmostEqual(actual/worker.MM, expected, places=5)
                stroke = [float(args[0])/worker.MM for args, op in ops if op == b'w'][-1]
                self.assertAlmostEqual(stroke, .25, places=7)
                s['CutInsetLeftMm'] = width+6
                with self.assertRaises(ValueError): self.generate(s)

    def test_whole_image_expansion(self):
        # A central stripe must scale with the complete design, rather than
        # retaining its old width while only the outermost pixels repeat.
        writer = PdfWriter()
        page = writer.add_blank_page(width=1000*worker.MM, height=800*worker.MM)
        stream = DecodedStreamObject()
        stream.set_data(f'1 0 0 rg 0 0 {1000*worker.MM} {800*worker.MM} re f '
                        f'0 0 1 rg {250*worker.MM} 0 {500*worker.MM} {800*worker.MM} re f'.encode('ascii'))
        page[NameObject('/Contents')] = writer._add_object(stream)
        writer.write(self.source)
        for left, right, top, bottom in ((5, 5, 5, 5), (10, 30, 20, 40), (0, 0, 0, 0)):
            with self.subTest(edges=(left, right, top, bottom)):
                s = self.settings()
                s.update(TargetWidthMm=1000, TargetHeightMm=800, ExpandImage=True,
                         EdgeLeftMm=left, EdgeRightMm=right, EdgeTopMm=top, EdgeBottomMm=bottom)
                result, reader, path = self.generate(s)
                width, height = 1000+left+right, 800+top+bottom
                self.assertAlmostEqual(result['OutputWidthMm'], width)
                self.assertAlmostEqual(result['OutputHeightMm'], height)
                self.assertAlmostEqual(float(reader.pages[0].trimbox.left)/worker.MM, left)
                operations = ContentStream(reader.pages[0].get_contents(), reader).operations
                matrices = [list(map(float, values)) for values, op in operations if op == b'cm']
                self.assertTrue(any(abs(m[0]-width/1000) < 1e-7 and abs(m[3]-height/800) < 1e-7 for m in matrices))
                with pdfium.PdfDocument(path) as doc:
                    bitmap = doc[0].render(scale=.25)
                    image = bitmap.to_pil().convert('RGB')
                    for x, channel in ((.01, 0), (.24, 0), (.26, 2), (.74, 2), (.76, 0), (.99, 0)):
                        pixel = image.getpixel((int(image.width*x), image.height//2))
                        self.assertGreater(pixel[channel], 240)
                        self.assertLess(pixel[(channel+1)%3], 20)
                    image.close(); bitmap.close()

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

    def test_mark_kind_geometry_and_render(self):
        for kind, mode, expected in [('DOT','FOUR_CORNERS',4),('CROSS','FOUR_CORNERS',4),
                                      ('DOT','CUSTOM_SPACING',12),('CROSS','CUSTOM_SPACING',12)]:
            with self.subTest(kind=kind, mode=mode):
                s=self.settings()
                s.update(FitMode="COVER",MakeHole=True,HoleMarkKind=kind,HoleType=mode,HoleSpacingMm=30,
                         MarkLeftRightMm=10,MarkTopBottomMm=10)
                result,r,out=self.generate(s)
                self.assertEqual(result['MarkCount'],expected)
                snap=json.loads(r.metadata['/PineTreeGenerationSettings'])
                self.assertEqual(snap['settings']['HoleMarkKind'],kind)
                self.assertIn('십자마크' if kind=='CROSS' else '점',result['Recipe'])
                ops=ContentStream(r.pages[0].get_contents(),r).operations
                # Black paths use the same centers; cross arms are 5mm, dots have zero length.
                black=False; start=None; segments=[]
                for a,op in ops:
                    if op==b'gs':black=a[0]=='/BlackOverprint'
                    elif black and op==b'm':start=tuple(map(float,a))
                    elif black and op==b'l':segments.append((start,tuple(map(float,a))))
                    elif op==b'Q':black=False
                self.assertEqual(len(segments),expected*(2 if kind=='CROSS' else 1))
                for a,b in segments:
                    self.assertAlmostEqual(abs(a[0]-b[0])+abs(a[1]-b[1]),5*worker.MM if kind=='CROSS' else 0,places=5)
                doc=pdfium.PdfDocument(out)
                image=doc[0].render(scale=4).to_pil().convert('RGB')
                def pixel(x,y):return image.getpixel((round(x*worker.MM*4),round((100-y)*worker.MM*4)))
                self.assertTrue(all(c<40 for c in pixel(10,10)))
                if kind=='CROSS':self.assertTrue(all(c<40 for c in pixel(12,10)))
                else:self.assertEqual(pixel(12,10),(255,0,0))
                doc.close()

    def test_invalid_input_rejected_before_output_write(self):
        for change in [dict(TargetWidthMm=0),dict(EdgeLeftMm=-1),dict(TargetWidthMm=float('nan')),
                       dict(MakeHole=True,HoleMarkKind='UNKNOWN'),dict(FitMode='STRETCH'),dict(MakeHole=True,MarkLeftRightMm=60),
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

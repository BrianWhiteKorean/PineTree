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
                    page = doc[0]; bitmap = page.render(scale=10)
                    image = bitmap.to_pil().convert('RGB')
                    x, y = round(20*worker.MM*10), round(80*worker.MM*10)
                    center = image.getpixel((x, y))
                    halo = image.getpixel((x+25, y) if kind == 'DOT' else (x+12, y+12))
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

    def test_black_cutline_uses_process_black_at_both_delivery_scales(self):
        for rip_scale in (1, 10):
            with self.subTest(rip_scale=rip_scale):
                settings = self.settings()
                settings.update(MakeCutline=True, CutlineColorMode='BLACK', SpotColorName='IgnoredCut',
                                OutputRipScale=rip_scale, LineWidthMm=.25,
                                CutInsetsFromPage=True, CutInsetLeftMm=7)
                _, reader, _ = self.generate(settings)
                page = reader.pages[0]
                self.assertFalse(worker.has_thru(page['/Resources'], spot_name='IgnoredCut'))
                self.assertFalse(worker.has_thru(page['/Resources']))
                self.assertAlmostEqual(float(page.trimbox.left)/worker.MM, 7/rip_scale, places=5)
                ops = ContentStream(page.get_contents(), reader).operations
                stroke_index = max(index for index, (_, op) in enumerate(ops) if op == b'S')
                self.assertEqual(ops[stroke_index-3], ([0, 0, 0, 1], b'K'))
                width = float(ops[stroke_index-2][0][0])/worker.MM
                factor = next(float(args[0]) for args, op in ops if op == b'cm') if rip_scale == 10 else 1
                self.assertAlmostEqual(width*factor, .25/rip_scale, places=7)

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

    def test_selected_edges_with_independent_insets(self):
        settings = self.settings()
        settings.update(MakeHole=True, HoleMarkKind='DOT', HoleSpacingMm=40,
                        EdgeLeftMm=2, EdgeBottomMm=3)
        edges = dict(LeftEnabled=True, RightEnabled=False, TopEnabled=True, BottomEnabled=False,
                     LeftMm=10, RightMm=15, TopMm=25, BottomMm=20)
        settings['HoleEdges'] = edges
        expected = [(12, 23), (12, 78), (87, 78)]
        for mode, positions in (('FOUR_CORNERS', expected),
                                ('CUSTOM_SPACING', expected + [(49.5, 78), (12, 50.5)])):
            with self.subTest(mode=mode):
                settings['HoleType'] = mode
                result, reader, _ = self.generate(settings)
                snapshot = json.loads(reader.metadata['/PineTreeGenerationSettings'])
                self.assertEqual(worker.mark_positions(snapshot['settings']), positions)
                self.assertEqual(result['MarkCount'], len(positions))
        edges.update(LeftEnabled=False, TopEnabled=False)
        result, _, _ = self.generate(settings)
        self.assertEqual(result['MarkCount'], 0)

    def test_hole_placement_rows_are_combined_and_empty_list_clears_marks(self):
        settings = self.settings()
        settings.update(MakeHole=True, HoleMarkKind='DOT', OutputRipScale=10)
        settings['HolePlacements'] = [
            dict(TypeCode='FOUR_CORNERS', Edges=dict(LeftMm=10, RightMm=15, TopMm=20, BottomMm=25)),
            dict(TypeCode='CUSTOM_SPACING', SpacingMm=40,
                 Edges=dict(LeftMm=15, RightMm=10, TopMm=15, BottomMm=15,
                            LeftEnabled=False, RightEnabled=False, BottomEnabled=False)),
        ]
        result, reader, _ = self.generate(settings)
        snapshot = json.loads(reader.metadata['/PineTreeGenerationSettings'])
        self.assertEqual(worker.mark_positions(snapshot['settings']),
                         [(10, 25), (10, 80), (85, 25), (85, 80), (15, 85), (90, 85), (52.5, 85)])
        self.assertEqual(result['MarkCount'], 7)
        self.assertEqual(result['OutputRipScale'], 10)
        settings['HolePlacements'] = []
        self.assertEqual(self.generate(settings)[0]['MarkCount'], 0)

    def test_custom_runs_use_top_left_coordinates_with_rip_scaling(self):
        settings = self.settings()
        settings.update(MakeHole=True, HoleMarkKind='DOT', OutputRipScale=10,
                        EdgeLeftMm=2, EdgeBottomMm=3)
        settings['HolePlacements'] = [dict(
            TypeCode='CUSTOM_SPACING',
            TopRun=dict(Mode='UNIFORM', Count=3, StartLeftMm=0, StartTopMm=10,
                        EndLeftMm=80, EndTopMm=20),
            BottomRun=dict(Mode='FIXED', Count=2, StartLeftMm=15, StartTopMm=80, SpacingMm=20),
        )]
        result, reader, _ = self.generate(settings)
        self.assertEqual(result['MarkCount'], 5)
        snapshot = json.loads(reader.metadata['/PineTreeGenerationSettings'])
        expected = [(2, 93), (42, 88), (82, 83), (17, 23), (37, 23)]
        self.assertEqual(worker.mark_positions(snapshot['settings']), expected)

        ops = ContentStream(reader.pages[0].get_contents(), reader).operations
        scale = float(next(args for args, op in ops if op == b'cm')[0])
        self.assertAlmostEqual(scale, .1)
        positions = [tuple(float(v)*scale/worker.MM for v in args)
                     for args, op in ops if op == b'm']
        self.assertEqual(len(positions), 10)
        for (x, y), (left, bottom) in zip(positions[::2], expected):
            self.assertAlmostEqual(x, left/10, places=6)
            self.assertAlmostEqual(y, bottom/10, places=6)

    def test_custom_run_validates_endpoints_and_count(self):
        settings = self.settings()
        settings.update(MakeHole=True, HoleMarkKind='DOT')
        row = dict(TypeCode='CUSTOM_SPACING')
        settings['HolePlacements'] = [row]
        for run in (
            dict(Mode='UNIFORM', Count=2, StartLeftMm=5, StartTopMm=10, EndLeftMm=5, EndTopMm=10),
            dict(Mode='FIXED', Count=3, StartLeftMm=105, StartTopMm=90, SpacingMm=15),
            dict(Mode='FIXED', Count=1, StartLeftMm=10, StartTopMm=90, SpacingMm=0),
        ):
            row['BottomRun'] = run
            with self.subTest(run=run), self.assertRaises(ValueError):
                self.generate(settings)
        row['BottomRun'] = dict(Mode='UNIFORM', Count=1, StartLeftMm=5, StartTopMm=90, EndLeftMm=5, EndTopMm=90)
        self.assertEqual(self.generate(settings)[0]['MarkCount'], 1)
        row['BottomRun']['EndLeftMm'] = 15
        self.assertEqual(worker.mark_positions(worker.generation_settings(settings)), [(10, 10)])

    def test_right_inset_and_fixed_truncation_use_image_bounds(self):
        settings = self.settings()
        settings.update(MakeHole=True, HoleMarkKind='DOT', TargetWidthMm=94, MakeWood=True, WoodExtraMm=10)
        settings['HolePlacements'] = [dict(
            TypeCode='CUSTOM_SPACING', ImageCoordinates=True,
            TopRun=dict(Mode='UNIFORM', Count=10, StartLeftMm=2, StartTopMm=2,
                        EndRightMm=2, EndTopMm=2),
            BottomRun=dict(Mode='FIXED', Count=10, StartLeftMm=80, StartTopMm=90, SpacingMm=10),
        )]
        result, reader, _ = self.generate(settings)
        self.assertEqual(result['MarkCount'], 12)
        snapshot = json.loads(reader.metadata['/PineTreeGenerationSettings'])
        self.assertEqual(worker.mark_positions(snapshot['settings']),
                         [(12 + step*10, 98) for step in range(10)] + [(90, 10), (100, 10)])

        settings['HolePlacements'][0]['BottomRun']['SpacingMm'] = 14
        result, reader, _ = self.generate(settings)
        self.assertEqual(result['MarkCount'], 12)
        snapshot = json.loads(reader.metadata['/PineTreeGenerationSettings'])
        self.assertEqual(worker.mark_positions(snapshot['settings'])[-1], (104, 10))

    def test_bottom_runs_measure_vertical_coordinates_from_image_bottom(self):
        settings = self.settings()
        settings.update(MakeHole=True, HoleMarkKind='DOT', EdgeBottomMm=3,
                        MakeWoodTopBottom=True, WoodTopBottomExtraMm=5)
        placement = dict(TypeCode='CUSTOM_SPACING', ImageCoordinates=True,
                         BottomRun=dict(Mode='UNIFORM', Count=3, StartLeftMm=2, StartBottomMm=2,
                                        EndRightMm=2, EndBottomMm=6))
        settings['HolePlacements'] = [placement]
        result, reader, _ = self.generate(settings)
        snapshot = json.loads(reader.metadata['/PineTreeGenerationSettings'])
        self.assertEqual(result['MarkCount'], 3)
        self.assertEqual(worker.mark_positions(snapshot['settings']), [(2, 10), (50, 12), (98, 14)])

        placement['BottomRun'] = dict(Mode='FIXED', Count=10, StartLeftMm=90, StartBottomMm=2, SpacingMm=5)
        result, reader, _ = self.generate(settings)
        snapshot = json.loads(reader.metadata['/PineTreeGenerationSettings'])
        self.assertEqual(result['MarkCount'], 3)
        self.assertEqual(worker.mark_positions(snapshot['settings']), [(90, 10), (95, 10), (100, 10)])

    def test_side_runs_use_right_insets_and_fixed_spacing_stops_at_image_bottom(self):
        settings = self.settings()
        settings.update(MakeHole=True, HoleMarkKind='DOT', OutputRipScale=10, EdgeLeftMm=3, EdgeBottomMm=5)
        placement = dict(TypeCode='CUSTOM_SPACING', ImageCoordinates=True,
                         LeftRun=dict(Mode='UNIFORM', Count=3, StartLeftMm=2, StartTopMm=2,
                                      EndLeftMm=4, EndBottomMm=6),
                         RightRun=dict(Mode='FIXED', Count=10, StartRightMm=3, StartTopMm=80, SpacingMm=10))
        settings['HolePlacements'] = [placement]
        result, reader, _ = self.generate(settings)
        snapshot = json.loads(reader.metadata['/PineTreeGenerationSettings'])
        positions = [(5, 103), (6, 57), (7, 11), (100, 25), (100, 15), (100, 5)]
        self.assertEqual(result['MarkCount'], len(positions))
        self.assertEqual(worker.mark_positions(snapshot['settings']), positions)
        ops = ContentStream(reader.pages[0].get_contents(), reader).operations
        scale = float(next(args for args, op in ops if op == b'cm')[0])
        centers = [tuple(float(v)*scale/worker.MM for v in args) for args, op in ops if op == b'm']
        self.assertEqual(len(centers), len(positions)*2)
        for actual, expected in zip(centers[::2], positions):
            for coordinate, units in zip(actual, expected):
                self.assertAlmostEqual(coordinate, units/10, places=6)

        placement['RightRun'] = dict(Mode='UNIFORM', Count=3, StartRightMm=3, StartTopMm=2,
                                     EndRightMm=5, EndBottomMm=2)
        self.assertEqual(worker.mark_positions(worker.generation_settings(settings))[-3:],
                         [(100, 103), (99, 55), (98, 7)])

    def test_uniform_runs_skip_preexisting_perpendicular_endpoints_in_row_order(self):
        settings = self.settings()
        settings.update(MakeHole=True, HoleMarkKind='DOT')
        horizontal = dict(TypeCode='CUSTOM_SPACING', ImageCoordinates=True,
                          TopRun=dict(Mode='UNIFORM', Count=2, StartLeftMm=2, StartTopMm=2, EndRightMm=2, EndTopMm=2),
                          BottomRun=dict(Mode='UNIFORM', Count=2, StartLeftMm=2, StartBottomMm=2, EndRightMm=2, EndBottomMm=2))
        vertical = dict(TypeCode='CUSTOM_SPACING', ImageCoordinates=True,
                        LeftRun=dict(Mode='UNIFORM', Count=2, StartLeftMm=2, StartTopMm=2, EndLeftMm=2, EndBottomMm=2),
                        RightRun=dict(Mode='UNIFORM', Count=2, StartRightMm=2, StartTopMm=2, EndRightMm=2, EndBottomMm=2))
        for rows, endpoints, inside in (
            ([horizontal, vertical], [(2, 98), (98, 98), (2, 2), (98, 2)], [(2, 66), (2, 34), (98, 66), (98, 34)]),
            ([vertical, horizontal], [(2, 98), (2, 2), (98, 98), (98, 2)], [(34, 98), (66, 98), (34, 2), (66, 2)]),
        ):
            with self.subTest(first=rows[0]):
                settings['HolePlacements'] = rows
                result, reader, _ = self.generate(settings)
                snapshot = json.loads(reader.metadata['/PineTreeGenerationSettings'])
                self.assertEqual(result['MarkCount'], 8)
                self.assertEqual(worker.mark_positions(snapshot['settings']), endpoints + inside)

        settings['HolePlacements'] = [horizontal, vertical]
        vertical['LeftRun']['Count'] = 1
        vertical['RightRun']['EndRightMm'] = 4
        self.assertEqual(worker.mark_positions(worker.generation_settings(settings))[-3:],
                         [(2, 50), (97, 50), (96, 2)])
        vertical['RightRun']['StartRightMm'] = 4
        self.assertEqual(worker.mark_positions(worker.generation_settings(settings))[-2:],
                         [(96, 98), (96, 2)])
        vertical['RightRun'] = dict(Mode='FIXED', Count=2, StartRightMm=2, StartTopMm=2, SpacingMm=20)
        self.assertEqual(worker.mark_positions(worker.generation_settings(settings))[-2:],
                         [(98, 98), (98, 78)])

    def test_dot_diameters_follow_delivery_pdf_scale(self):
        for scale, rip_scale in ((1, 1), (10, 10), (10, 1)):
            with self.subTest(scale=scale, rip_scale=rip_scale):
                writer = PdfWriter()
                size = 100*worker.MM/scale
                page = writer.add_blank_page(width=size, height=size)
                stream = DecodedStreamObject()
                stream.set_data(f'1 0 0 rg 0 0 {size} {size} re f'.encode('ascii'))
                page[NameObject('/Contents')] = writer._add_object(stream)
                writer.write(self.source)
                settings = self.settings()
                settings.update(MakeHole=True, HoleMarkKind='DOT', OutputRipScale=rip_scale,
                                MakeCutline=True, EdgeLeftMm=2, EdgeRightMm=2)
                result, reader, _ = self.generate(settings)
                self.assertAlmostEqual(result['Scale'], scale, places=5)
                self.assertEqual(result['OutputRipScale'], rip_scale)
                page = reader.pages[0]
                self.assertAlmostEqual(float(page.mediabox.width)/worker.MM, 104/rip_scale, places=5)
                self.assertAlmostEqual(float(page.trimbox.left)/worker.MM, 2/rip_scale, places=5)
                self.assertAlmostEqual(float(page.trimbox.width)/worker.MM, 100/rip_scale, places=5)
                self.assertAlmostEqual(result['OutputWidthMm'], 104/rip_scale, places=5)
                ops = ContentStream(page.get_contents(), reader).operations
                delivery_factor = 1
                if rip_scale != 1:
                    transform = next(args for args, op in ops if op == b'cm')
                    delivery_factor = float(transform[0])
                    self.assertAlmostEqual(delivery_factor, 1/rip_scale)
                    self.assertAlmostEqual(float(transform[3]), delivery_factor)
                widths = [float(args[0])/worker.MM for args, op in ops if op == b'w']
                self.assertEqual(len(widths), 9)
                for halo, center in zip(widths[:8:2], widths[1:8:2]):
                    self.assertAlmostEqual(halo*delivery_factor, 2/rip_scale, places=6)
                    self.assertAlmostEqual(center*delivery_factor, 1.6/rip_scale, places=6)
                self.assertAlmostEqual(widths[-1]*delivery_factor, .18/rip_scale, places=6)

    def test_invalid_input_rejected_before_output_write(self):
        for change in [dict(TargetWidthMm=0),dict(EdgeLeftMm=-1),dict(TargetWidthMm=float('nan')),dict(OutputRipScale=.1),
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

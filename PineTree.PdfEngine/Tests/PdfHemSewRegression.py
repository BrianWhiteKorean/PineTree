import importlib.util
import tempfile
import unittest
from pathlib import Path
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ContentStream, DecodedStreamObject, NameObject
import pypdfium2 as pdfium
PROJECT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('worker', PROJECT/'PdfWorker/pdf_worker.py')
worker = importlib.util.module_from_spec(spec); spec.loader.exec_module(worker)

class HemTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.folder = Path(self.temp.name)
        self.source = self.folder/'source.pdf'
        writer = PdfWriter(); page = writer.add_blank_page(width=180*worker.MM, height=120*worker.MM)
        stream = DecodedStreamObject(); stream.set_data(f'1 0 0 rg 0 0 {180*worker.MM} {120*worker.MM} re f'.encode())
        page[NameObject('/Contents')] = writer._add_object(stream); writer.write(self.source)
    def tearDown(self): self.temp.cleanup()
    def generate(self, rate=1, kind='SINGLE', **changes):
        s = dict(TargetWidthMm=180*rate, TargetHeightMm=120*rate, ExpandImage=True,
                 MakeCutline=False, MakeHole=False, EdgeLeftMm=0, EdgeRightMm=0, EdgeTopMm=0, EdgeBottomMm=0,
                 HemSewPasses=[dict(TypeCode=kind, LineThicknessMm=.06*rate)])
        s.update(changes); output = self.folder/'output.pdf'
        result = worker.execute(dict(Operation='generate', SourcePath=str(self.source), OutputPath=str(output), Settings=s))
        return result, PdfReader(output), output
    def test_types_scales_geometry_and_black_stroke(self):
        for rate in (1, 10, 2.5):
            for kind, margin in (('SINGLE', 15), ('DOUBLE', 30)):
                with self.subTest(rate=rate, kind=kind):
                    result, reader, output = self.generate(rate, kind); page = reader.pages[0]
                    width, height = 180*rate+2*margin, 120*rate+2*margin
                    self.assertAlmostEqual(result['OutputWidthMm'], width)
                    self.assertAlmostEqual(result['OutputHeightMm'], height)
                    self.assertAlmostEqual(float(page.mediabox.width)/worker.MM, width, places=5)
                    strokes = []; path = []; thickness = None; color = None
                    for args, op in ContentStream(page.get_contents(), reader).operations:
                        if op == b'w': thickness = float(args[0])/worker.MM
                        elif op == b'K': color = list(map(float, args))
                        elif op == b're': path.append(list(map(float, args)))
                        elif op == b'S':
                            strokes.extend((r, thickness, color) for r in path); path = []
                        elif op in (b'n', b'f'): path = []
                    self.assertEqual(len(strokes), 2)
                    inner, outer = strokes
                    for actual, expected in zip(inner[0], (margin, margin, 180*rate, 120*rate)):
                        self.assertAlmostEqual(actual/worker.MM, expected, places=5)
                    for rect, thickness, color in strokes:
                        self.assertAlmostEqual(thickness, .06*rate, places=7)
                        self.assertEqual(color, [0, 0, 0, 1])
                    self.assertAlmostEqual((outer[0][0]+outer[0][2])/worker.MM+outer[1]/2, width, places=5)
                    with pdfium.PdfDocument(output) as doc:
                        pg = doc[0]; bitmap = pg.render(scale=.5); image = bitmap.to_pil().convert('RGB')
                        self.assertEqual(image.getpixel((2, image.height//2)), (255, 255, 255))
                        center = image.getpixel((image.width//2, image.height//2))
                        self.assertGreater(center[0], 240); self.assertLess(center[1], 20)
                        image.close(); bitmap.close(); pg.close()
    def test_compose_marks_cut_and_image_expansion(self):
        result, reader, _ = self.generate(10, MakeHole=True, MakeCutline=True, CutInsetsFromPage=True,
                                        CutInsetLeftMm=5, CutInsetRightMm=5, CutInsetTopMm=5, CutInsetBottomMm=5,
                                        EdgeLeftMm=2, EdgeRightMm=3, EdgeTopMm=4, EdgeBottomMm=6)
        self.assertAlmostEqual(result['OutputWidthMm'], 1835)
        self.assertAlmostEqual(result['OutputHeightMm'], 1240)
        self.assertEqual(result['MarkCount'], 4)
        self.assertAlmostEqual(float(reader.pages[0].trimbox.left)/worker.MM, 5, places=6)
    def test_invalid_settings(self):
        for passes in (None, 'bad', [None], [dict(TypeCode='UNKNOWN')], [dict(LineThicknessMm=0)], [dict(LineThicknessMm=float('nan'))]):
            if passes is None: continue  # Null follows the legacy default empty list.
            with self.subTest(passes=passes):
                with self.assertRaises(ValueError): self.generate(HemSewPasses=passes)
        with self.assertRaises(ValueError): self.generate(TargetWidthMm=14400)

if __name__ == '__main__': unittest.main(verbosity=2)

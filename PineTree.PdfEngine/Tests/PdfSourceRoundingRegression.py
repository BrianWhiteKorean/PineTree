import importlib.util
import io
from pathlib import Path
import unittest
from pypdf import PdfReader
PROJECT = Path(__file__).resolve().parents[1]
SOURCE = PROJECT.parents[1]/'1.요구분석/1026/(원본복원)현수막_시접미싱제거_1830X1230.pdf'
spec = importlib.util.spec_from_file_location('worker', PROJECT/'PdfWorker/pdf_worker.py')
worker = importlib.util.module_from_spec(spec); spec.loader.exec_module(worker)
class RoundingTests(unittest.TestCase):
    def test_customer_crop_rounds_before_enlargement(self):
        for rate, expected in ((1, (210, 150)), (10, (1830, 1230))):
            with self.subTest(rate=rate):
                reader = PdfReader(SOURCE); output = io.BytesIO()
                result = worker.generate_pages(SOURCE, reader, {}, output, dict(
                    SourceSizingMode='SCALE', SourceScale=rate, RoundSourceDimensionsBeforeScale=True,
                    MakeCutline=False, MakeHole=False, ExpandImage=True,
                    HemSewPasses=[dict(TypeCode='SINGLE', LineThicknessMm=.06*rate)]))
                self.assertAlmostEqual(result['OutputWidthMm'], expected[0])
                self.assertAlmostEqual(result['OutputHeightMm'], expected[1])
                page = PdfReader(output).pages[0]
                self.assertAlmostEqual(float(page.mediabox.width)/worker.MM, expected[0], places=5)
                self.assertAlmostEqual(float(page.mediabox.height)/worker.MM, expected[1], places=5)
    def test_legacy_engine_call_can_preserve_exact_source(self):
        reader = PdfReader(SOURCE); info = worker.inspect_file(SOURCE, reader, {}, 0)
        result = worker.generate_pages(SOURCE, reader, {}, io.BytesIO(), dict(
            SourceSizingMode='SCALE', SourceScale=10, MakeCutline=False, MakeHole=False))
        self.assertAlmostEqual(result['OutputWidthMm'], info['SourceWidthMm']*10)
if __name__ == '__main__': unittest.main(verbosity=2)

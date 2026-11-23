"""AI page order, per-page geometry, common finishing and lossless page extraction."""
import hashlib
import importlib.util
from pathlib import Path
import tempfile
import unittest
import shutil
from PIL import Image, ImageChops
from pypdf import PdfReader, PdfWriter
from pypdf.generic import DecodedStreamObject, NameObject

PROJECT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('worker', PROJECT / 'PdfWorker/pdf_worker.py')
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)


class MultiArtboardTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)
        self.root = Path(self.folder.name)
        self.source = self.root / '한글 (여러 대지).ai'
        writer = PdfWriter()
        for width, height, color, rotation in [(200, 100, '1 0 0', 0), (120, 80, '0 0 1', 90)]:
            page = writer.add_blank_page(width=width * worker.MM, height=height * worker.MM)
            stream = DecodedStreamObject()
            stream.set_data(f'{color} rg 0 0 {width * worker.MM} {height * worker.MM} re f'.encode())
            page[NameObject('/Contents')] = writer._add_object(stream)
            page.rotate(rotation)
        writer.write(self.source)

    def generate(self, source=None, **settings):
        output = self.root / 'all.pdf'
        base = dict(GenerateAllPages=True, SourceSizingMode='ORIGINAL', MakeCutline=True,
                    MakeHole=True, TargetWidthMm=200, TargetHeightMm=100,
                    MarkLeftRightMm=10, MarkTopBottomMm=10)
        base.update(settings)
        result = worker.execute(dict(Operation='generate', SourcePath=str(source or self.source),
                                     OutputPath=str(output), Settings=base))
        return output, result

    def test_geometry_rotation_finishing_and_extract(self):
        before = hashlib.sha256(self.source.read_bytes()).digest()
        output, result = self.generate(EdgeLeftMm=2, EdgeRightMm=2, EdgeTopMm=2, EdgeBottomMm=2)
        self.assertEqual(result['PageCount'], 2)
        self.assertEqual(result['MarkCount'], 8)
        pages = PdfReader(output).pages
        for page, (width, height) in zip(pages, [(204, 104), (84, 124)]):
            self.assertAlmostEqual(float(page.mediabox.width) / worker.MM, width, places=4)
            self.assertAlmostEqual(float(page.mediabox.height) / worker.MM, height, places=4)
            self.assertTrue(worker.has_thru(page['/Resources']))
        for index in range(2):
            extracted = self.root / f'page{index}.pdf'
            info = worker.execute(dict(Operation='extract', SourcePath=str(output), PageIndex=index, OutputPath=str(extracted)))
            self.assertEqual(info['PageCount'], 1)
            self.assertEqual(len(PdfReader(extracted).pages), 1)
            full = self.root / f'full{index}.png'
            single = self.root / f'single{index}.png'
            worker.execute(dict(Operation='preview', SourcePath=str(output), PageIndex=index, OutputPath=str(full), MaxPixels=600))
            worker.execute(dict(Operation='preview', SourcePath=str(extracted), OutputPath=str(single), MaxPixels=600))
            with Image.open(full) as a, Image.open(single) as b:
                self.assertIsNone(ImageChops.difference(a, b).getbbox())
        with Image.open(self.root / 'full0.png') as a, Image.open(self.root / 'full1.png') as b:
            self.assertGreater(a.getpixel((a.width//2, a.height//2))[0], 200)
            self.assertGreater(b.getpixel((b.width//2, b.height//2))[2], 200)
        self.assertEqual(hashlib.sha256(self.source.read_bytes()).digest(), before)

    def test_scale_and_fixed_width(self):
        output, _ = self.generate(SourceSizingMode='SCALE', SourceScale=2)
        self.assertAlmostEqual(float(PdfReader(output).pages[1].trimbox.width) / worker.MM, 160, places=4)
        output, _ = self.generate(SourceSizingMode='FIXED_SIZE', TargetWidthMm=160)
        self.assertAlmostEqual(float(PdfReader(output).pages[1].trimbox.height) / worker.MM, 240, places=4)

    def test_invalid_second_page_reports_number(self):
        with self.assertRaisesRegex(ValueError, '대지 2:'):
            self.generate(MarkLeftRightMm=45)

    def test_inner_artwork_clip_is_not_a_multi_artboard_boundary(self):
        writer = PdfWriter()
        for _ in range(2):
            page = writer.add_blank_page(width=800, height=700)
            stream = DecodedStreamObject()
            stream.set_data(b'1 0 0 rg 0 0 800 700 re f q 0 0 200 350 re W n 0 0 1 rg 0 0 200 350 re f Q')
            page[NameObject('/Contents')] = writer._add_object(stream)
        writer.write(self.source)
        info = worker.execute(dict(Operation='inspect', SourcePath=str(self.source)))
        self.assertEqual(info['Region'], [0, 0, 800, 700])
        self.assertAlmostEqual(info['SourceWidthMm'], 800 / worker.MM)
        self.assertAlmostEqual(info['SourceHeightMm'], 700 / worker.MM)

    def test_missing_source_cutline_is_not_silently_skipped(self):
        with self.assertRaisesRegex(ValueError, '대지 1:'):
            self.generate(MakeCutline=False, RequireSourceCutline=True, SpotColorName='thru')

    def test_single_ai_keeps_explicit_target_dimensions(self):
        source = self.root / 'single.ai'
        writer = PdfWriter()
        writer.add_page(PdfReader(self.source).pages[0])
        writer.write(source)
        output, result = self.generate(source, SourceSizingMode='ORIGINAL', TargetWidthMm=300, TargetHeightMm=200)
        self.assertEqual(result['PageCount'], 1)
        self.assertAlmostEqual(float(PdfReader(output).pages[0].trimbox.width) / worker.MM, 300, places=4)
        self.assertAlmostEqual(float(PdfReader(output).pages[0].trimbox.height) / worker.MM, 200, places=4)

    def test_other_formats_keep_single_page_generation(self):
        pdf = self.root / 'input.pdf'
        pdf.write_bytes(self.source.read_bytes())
        output, result = self.generate(pdf)
        self.assertEqual(result['PageCount'], 1)
        self.assertEqual(len(PdfReader(output).pages), 1)

    def test_yokel_real_sample(self):
        source = PROJECT.parents[1] / '4.테스트데이터/대지 여러개/Yokel.ai'
        if not source.exists():
            self.skipTest('Yokel.ai not available')
        with source.open('rb') as handle:
            before = hashlib.file_digest(handle, 'sha256').hexdigest()
        for index in range(2):
            info = worker.execute(dict(Operation='preview', SourcePath=str(source), PageIndex=index,
                                       OutputPath=str(self.root / f'yokel{index}.png'), MaxPixels=600))
            self.assertEqual(info['PageCount'], 2)
            self.assertAlmostEqual(info['PageWidthMm'], 293.60, places=1)
            self.assertAlmostEqual(info['PageHeightMm'], 248.60, places=1)
            self.assertAlmostEqual(info['SourceWidthMm'], 293.60, places=1)
            self.assertAlmostEqual(info['SourceHeightMm'], 248.60, places=1)
            self.assertEqual(info['Region'], [0.0, 0.0, 832.252, 704.693])
        output, result = self.generate(source)
        self.assertEqual(result['PageCount'], 2)
        self.assertTrue(all(worker.has_thru(p['/Resources']) for p in PdfReader(output).pages))
        for index in range(2):
            worker.execute(dict(Operation='preview', SourcePath=str(output), PageIndex=index,
                                OutputPath=str(self.root / f'result{index}.png'), MaxPixels=600))
        worker.execute(dict(Operation='preview', SourcePath=str(source), PageIndex=0, FullCanvas=True,
                            OutputPath=str(self.root / 'full-canvas.png'), MaxPixels=600))
        with Image.open(self.root / 'yokel0.png') as a, Image.open(self.root / 'full-canvas.png') as b:
            self.assertIsNone(ImageChops.difference(a, b).getbbox(), 'default must show the complete artboard')
        artifacts = PROJECT.parents[1] / 'tmp/multi-artboard-fix'
        artifacts.mkdir(parents=True, exist_ok=True)
        for name in ('yokel0.png', 'yokel1.png', 'result0.png', 'result1.png'):
            shutil.copyfile(self.root / name, artifacts / name)
        with source.open('rb') as handle:
            self.assertEqual(before, hashlib.file_digest(handle, 'sha256').hexdigest())


if __name__ == '__main__':
    unittest.main(verbosity=2)

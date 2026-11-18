"""Large Illustrator image streams, without a large checked-in fixture.

Run with PdfWorker/runtime/python.exe Tests/PdfLargeStreamRegression.py.
"""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from PIL import Image
from pypdf import PdfReader, PdfWriter, filters
from pypdf.errors import LimitReachedError
from pypdf.generic import DecodedStreamObject, DictionaryObject, NameObject, NumberObject

spec = importlib.util.spec_from_file_location(
    "worker", Path(__file__).resolve().parents[1] / "PdfWorker/pdf_worker.py")
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)


class LargeStreamTests(unittest.TestCase):
    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.addCleanup(self.folder.cleanup)
        self.root = Path(self.folder.name)
        self.source = self.root / "large-image.ai"
        writer = PdfWriter()
        page = writer.add_blank_page(width=96, height=96)
        image = DecodedStreamObject()
        image.set_data(bytes([0, 180, 0]) * (96 * 96))
        image.update({NameObject("/Type"): NameObject("/XObject"),
                      NameObject("/Subtype"): NameObject("/Image"),
                      NameObject("/Width"): NumberObject(96),
                      NameObject("/Height"): NumberObject(96),
                      NameObject("/ColorSpace"): NameObject("/DeviceRGB"),
                      NameObject("/BitsPerComponent"): NumberObject(8)})
        page[NameObject("/Resources")] = DictionaryObject({
            NameObject("/XObject"): DictionaryObject({NameObject("/Im0"): writer._add_object(image)})})
        content = DecodedStreamObject()
        content.set_data(b"q 96 0 0 96 0 0 cm /Im0 Do Q")
        page[NameObject("/Contents")] = writer._add_object(content)
        writer.write(self.source)

    def test_ai_inspection_preview_and_vector_output_with_large_image(self):
        original = self.source.read_bytes()
        # Scale down the library limit to reproduce a >75 MB image cheaply.
        with patch.object(filters, "MAX_DECLARED_STREAM_LENGTH", 16384):
            reader = PdfReader(self.source)
            with self.assertRaises(LimitReachedError):
                reader.pages[0]["/Resources"]["/XObject"]["/Im0"]
            info = worker.execute(dict(Operation="inspect", SourcePath=str(self.source)))
            self.assertFalse(info["HasCutline"])
            self.assertAlmostEqual(info["SourceWidthMm"], 96 / worker.MM)
            preview = self.root / "preview.png"
            worker.execute(dict(Operation="preview", SourcePath=str(self.source), OutputPath=str(preview)))
            with Image.open(preview) as bitmap:
                self.assertEqual(bitmap.convert("RGB").getpixel((800, 800)), (0, 180, 0))
            output = self.root / "output.pdf"
            worker.execute(dict(Operation="generate", SourcePath=str(self.source), OutputPath=str(output),
                Settings=dict(TargetWidthMm=96, TargetHeightMm=96, MakeCutline=False, MakeHole=False)))
            with worker.source_stream_limit(output):
                result = PdfReader(output).pages[0]
                artwork = result["/Resources"]["/XObject"]["/Artwork"]
                self.assertEqual(artwork["/Resources"]["/XObject"]["/Im0"].get_data(), bytes([0, 180, 0]) * (96 * 96))
            self.assertEqual(filters.MAX_DECLARED_STREAM_LENGTH, 16384)
        self.assertEqual(self.source.read_bytes(), original)

    def test_invalid_stream_length_still_rejected_and_limit_restored(self):
        self.source.write_bytes(self.source.read_bytes().replace(b"/Length 27648", b"/Length 99999"))
        with patch.object(filters, "MAX_DECLARED_STREAM_LENGTH", 16384):
            # PDF input avoids AI's optional text-extraction error handling.
            source = self.source.with_suffix(".pdf")
            self.source.rename(source)
            with self.assertRaises(LimitReachedError):
                worker.execute(dict(Operation="inspect", SourcePath=str(source)))
            self.assertEqual(filters.MAX_DECLARED_STREAM_LENGTH, 16384)


if __name__ == "__main__":
    unittest.main()

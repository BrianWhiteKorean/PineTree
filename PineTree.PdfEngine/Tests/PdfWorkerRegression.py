"""Run with PdfWorker/runtime/python.exe Tests/PdfWorkerRegression.py."""
import hashlib
import importlib.util
from pathlib import Path
import tempfile
import unittest

from PIL import Image, ImageChops, ImageDraw, ImageStat
import pypdfium2 as pdfium
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ContentStream, DictionaryObject, NameObject, FloatObject, DecodedStreamObject

PROJECT = Path(__file__).resolve().parents[1]
ROOT = PROJECT.parents[1]
AI = ROOT / "1.요구분석" / "샘플파일" / "페트_재단선+사방타공마크_604X1804_1EA_별색.ai"
REFERENCE = ROOT / "1.요구분석" / "페트_재단선+사방타공마크_604X1804_1EA_별색.pdf"
spec = importlib.util.spec_from_file_location("pdf_worker", PROJECT / "PdfWorker" / "pdf_worker.py")
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)


class PdfWorkerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.folder = tempfile.TemporaryDirectory(prefix="pinetree-regression-")
        cls.temp = Path(cls.folder.name)
        cls.ai_hash = hashlib.sha256(AI.read_bytes()).hexdigest()
        cls.jpeg = cls.temp / "한글 원본 ' $ (샘플).jpg"
        Image.new("RGB", (600, 1800), (218, 231, 245)).save(cls.jpeg, dpi=(300, 150))
        cls.eps = cls.temp / "한글 EPS.eps"
        cls.eps.write_text("%!PS-Adobe-3.0 EPSF-3.0\n%%BoundingBox: 0 0 170 510\n%%EndComments\n"
                           "0 0.5 1 setrgbcolor 0 0 170 510 rectfill\n"
                           "/Helvetica findfont 12 scalefont setfont 0 setgray 10 250 moveto (EPS TEST) show\nshowpage\n%%EOF\n", encoding="ascii")

    @classmethod
    def tearDownClass(cls):
        assert hashlib.sha256(AI.read_bytes()).hexdigest() == cls.ai_hash, "AI original modified"
        cls.folder.cleanup()

    def test_ai_metadata_uses_design_not_artbox(self):
        info = worker.execute({"Operation": "inspect", "SourcePath": str(AI)})
        self.assertTrue(info["FileType"].startswith("AI : "))
        self.assertAlmostEqual(info["DesignWidthMm"], 60, places=2)
        self.assertAlmostEqual(info["DesignHeightMm"], 180, places=2)
        self.assertGreater(info["PageWidthMm"], 5000)

    def test_png_transparency_preview_and_generation(self):
        source = self.temp / "투명 원본.png"
        image = Image.new("RGBA", (240, 120), (0, 0, 0, 0))
        ImageDraw.Draw(image).rectangle((80, 30, 160, 90), fill=(255, 0, 0, 255))
        image.save(source, dpi=(300, 300))
        original = hashlib.sha256(source.read_bytes()).hexdigest()
        info = worker.execute({"Operation": "inspect", "SourcePath": str(source)})
        self.assertTrue(info["FileType"].startswith("PNG : "))
        self.assertEqual((info["PixelWidth"], info["PixelHeight"]), (240, 120))
        preview = self.temp / "png-preview.png"
        worker.execute({"Operation": "preview", "SourcePath": str(source), "OutputPath": str(preview)})
        with Image.open(preview) as rendered:
            self.assertTrue(all(channel > 245 for channel in rendered.convert("RGB").getpixel((10, 10))))
        output = self.temp / "png-output.pdf"
        result = worker.execute({"Operation": "generate", "SourcePath": str(source), "OutputPath": str(output),
            "Settings": {"TargetWidthMm": 600, "TargetHeightMm": 1800, "MakeCutline": True, "MakeHole": True}})
        self.assertAlmostEqual(result["OutputWidthMm"], 604)
        self.assertEqual(result["MarkCount"], 4)
        self.assertEqual(hashlib.sha256(source.read_bytes()).hexdigest(), original)

    def test_mislabeled_png_rejected(self):
        source = self.temp / "not-png.png"
        Image.new("RGB", (10, 10)).save(source, "JPEG")
        with self.assertRaises(ValueError):
            worker.execute({"Operation": "inspect", "SourcePath": str(source)})

    def test_source_physical_dimensions_use_image_dpi(self):
        info = worker.execute({"Operation": "inspect", "SourcePath": str(self.jpeg)})
        self.assertAlmostEqual(info["SourceWidthMm"], 50.8, places=2)
        self.assertAlmostEqual(info["SourceHeightMm"], 304.8, places=2)
        self.assertFalse(info["HasCutline"])
        self.assertEqual(info["CutlineColorName"], "")
        no_dpi = self.temp / "no-dpi.png"
        Image.new("RGB", (72, 144), "white").save(no_dpi)
        info = worker.execute({"Operation": "inspect", "SourcePath": str(no_dpi)})
        self.assertAlmostEqual(info["SourceWidthMm"], 25.4)
        self.assertIn("72 DPI", info["SourceDimensionNote"])

    def test_explicit_cutline_metadata(self):
        output = self.temp / "cutline-metadata.pdf"
        worker.execute({"Operation": "generate", "SourcePath": str(self.jpeg), "OutputPath": str(output),
            "Settings": {"TargetWidthMm": 600, "TargetHeightMm": 1800, "MakeCutline": True,
                         "MakeHole": False, "SpotColorName": "CutContour", "LineWidthMm": .23}})
        info = worker.execute({"Operation": "inspect", "SourcePath": str(output)})
        self.assertTrue(info["HasCutline"])
        self.assertEqual(info["CutlineColorName"], "CutContour")
        self.assertAlmostEqual(info["CutlineWidthMm"], .23, places=5)
        self.assertAlmostEqual(info["SourceWidthMm"], 604, places=4)

    def test_pdf_metadata(self):
        info = worker.execute({"Operation": "inspect", "SourcePath": str(REFERENCE)})
        self.assertAlmostEqual(info["PageWidthMm"], 604, places=2)
        self.assertEqual(info["PageCount"], 1)

    def test_jpg_metadata(self):
        info = worker.execute({"Operation": "inspect", "SourcePath": str(self.jpeg)})
        self.assertEqual((info["PixelWidth"], info["PixelHeight"]), (600, 1800))
        self.assertEqual(info["Dpi"], [300, 150])

    def test_eps_conversion_with_korean_path_and_fonts(self):
        info = worker.execute({"Operation": "inspect", "SourcePath": str(self.eps)})
        self.assertAlmostEqual(info["PageWidthMm"], 170 / worker.MM, places=2)
        self.assertEqual(info["PageCount"], 1)

    def test_old_postscript_ai(self):
        old = self.temp / "구형.ai"
        old.write_bytes(self.eps.read_bytes())
        self.assertEqual(worker.execute({"Operation": "inspect", "SourcePath": str(old)})["PageCount"], 1)

    def test_preview_all_four_formats(self):
        for index, source in enumerate((AI, REFERENCE, self.jpeg, self.eps)):
            with self.subTest(source=source.suffix):
                output = self.temp / (str(index) + ".png")
                worker.execute({"Operation": "preview", "SourcePath": str(source), "OutputPath": str(output), "MaxPixels": 1000})
                with Image.open(output) as image:
                    self.assertLessEqual(max(image.size), 1002)
                    self.assertGreater(image.height, image.width)

    def test_sample_geometry_spot_marks_and_vector_bleed(self):
        output = self.temp / "출력 샘플.pdf"
        result = worker.execute({"Operation": "generate", "SourcePath": str(AI), "OutputPath": str(output)})
        reader = PdfReader(output)
        page = reader.pages[0]
        self.assertEqual(len(reader.pages), 1)
        self.assertAlmostEqual(float(page.mediabox.width)/worker.MM, 604, places=5)
        self.assertAlmostEqual(float(page.mediabox.height)/worker.MM, 1804, places=5)
        self.assertAlmostEqual(float(page.trimbox.width)/worker.MM, 600, places=5)
        self.assertAlmostEqual(float(page.trimbox.height)/worker.MM, 1800, places=5)
        self.assertEqual(page["/Resources"]["/ColorSpace"]["/CutColor"][1], "/thru")
        operations = ContentStream(page.get_contents(), reader).operations
        self.assertEqual(sum(op == b"Do" and args[0] == "/Artwork" for args, op in operations), 8)
        self.assertEqual(sum(op == b"gs" and args[0] == "/BlackOverprint" for args, op in operations), 4)
        self.assertAlmostEqual(result["Scale"], 10, places=3)
        # Geometry checks alone missed a nested-transparency regression. Compare
        # the artwork pixels too, excluding the border and added punch marks.
        with pdfium.PdfDocument(str(output)) as actual, pdfium.PdfDocument(str(REFERENCE)) as reference:
            a = actual[0].render(scale=.3).to_pil().convert("RGB")
            b = reference[0].render(scale=.3).to_pil().convert("RGB")
            self.assertEqual(a.size, b.size)
            diff = ImageChops.difference(a, b)
            draw = ImageDraw.Draw(diff)
            for cx in (22, 582):
                for cy in (22, 1782):
                    px, py = cx*worker.MM*.3, (1804-cy)*worker.MM*.3
                    draw.rectangle((px-7, py-7, px+7, py+7), fill=(0, 0, 0))
            mean_error = sum(ImageStat.Stat(diff.crop((5, 5, a.width-5, a.height-5))).mean)/3
            self.assertLess(mean_error, .5, "source artwork compositing changed")

    def test_generate_jpg_and_eps(self):
        for source in (self.jpeg, self.eps):
            output = self.temp / (source.suffix[1:] + "_sample.pdf")
            worker.execute({"Operation": "generate", "SourcePath": str(source), "OutputPath": str(output)})
            self.assertTrue(worker.has_thru(PdfReader(output).pages[0]["/Resources"]))

    def test_duplicate_cut_line_rejected(self):
        output = self.temp / "duplicate.pdf"
        with self.assertRaisesRegex(ValueError, "이미 thru"):
            worker.execute({"Operation": "generate", "SourcePath": str(REFERENCE), "OutputPath": str(output)})
        self.assertFalse(output.exists())

    def test_source_overwrite_rejected(self):
        with self.assertRaisesRegex(ValueError, "덮어쓸"):
            worker.execute({"Operation": "generate", "SourcePath": str(self.jpeg), "OutputPath": str(self.jpeg)})

    def test_imported_color_and_transparency_resource_names_are_preserved(self):
        original = self.temp / "resource-names.pdf"
        writer = PdfWriter()
        page = writer.add_blank_page(width=60*worker.MM, height=180*worker.MM)
        page[NameObject("/Resources")] = DictionaryObject({
            NameObject("/ColorSpace"): DictionaryObject({NameObject("/CutColor"): NameObject("/DeviceRGB")}),
            NameObject("/ExtGState"): DictionaryObject({
                NameObject("/NoOverprint"): DictionaryObject({NameObject("/ca"): FloatObject(.4)}),
                NameObject("/BlackOverprint"): DictionaryObject({NameObject("/ca"): FloatObject(.6)})})})
        content = DecodedStreamObject()
        content.set_data(b"/NoOverprint gs /CutColor cs 1 0 0 sc 0 0 50 100 re f")
        page[NameObject("/Contents")] = writer._add_object(content)
        writer.write(original)
        output = self.temp / "resource-names-output.pdf"
        worker.execute({"Operation": "generate", "SourcePath": str(original), "OutputPath": str(output)})
        resources = PdfReader(output).pages[0]["/Resources"]
        self.assertEqual(resources["/ColorSpace"]["/CutColor"], "/DeviceRGB")
        self.assertEqual(resources["/ColorSpace"]["/CutColor1"][1], "/thru")
        self.assertAlmostEqual(float(resources["/ExtGState"]["/NoOverprint"]["/ca"]), .4)
        self.assertIn("/BlackOverprint1", resources["/ExtGState"])

    def test_unsupported_private_ai_is_explicit(self):
        source = self.temp / "private.ai"
        source.write_bytes(b"not PDF or PostScript")
        with self.assertRaisesRegex(ValueError, "PDF/PostScript"):
            worker.execute({"Operation": "inspect", "SourcePath": str(source)})

    def test_page_bounds_and_multiple_pages(self):
        source = self.temp / "two.pdf"
        writer = PdfWriter()
        writer.add_blank_page(width=100, height=300)
        writer.add_blank_page(width=200, height=600)
        writer.write(source)
        self.assertEqual(worker.execute({"Operation": "inspect", "SourcePath": str(source), "PageIndex": 1})["PageCount"], 2)
        with self.assertRaisesRegex(ValueError, "페이지"):
            worker.execute({"Operation": "inspect", "SourcePath": str(source), "PageIndex": 2})


if __name__ == "__main__":
    unittest.main(verbosity=2)

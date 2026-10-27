"""Verify vector output and sharp, correctly positioned viewport rendering."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from PIL import Image, ImageChops, ImageStat
from pypdf import PdfWriter, PdfReader
from pypdf.generic import DictionaryObject, NameObject, DecodedStreamObject
import pypdfium2 as pdfium

spec = importlib.util.spec_from_file_location("worker", Path(__file__).resolve().parents[1]/"PdfWorker/pdf_worker.py")
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)

class DetailTests(unittest.TestCase):
    def test_vector_text_and_visible_region(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            source, output = folder/"source.pdf", folder/"output.pdf"
            writer = PdfWriter()
            page = writer.add_blank_page(width=800, height=60)
            font = DictionaryObject({NameObject("/Type"):NameObject("/Font"), NameObject("/Subtype"):NameObject("/Type1"), NameObject("/BaseFont"):NameObject("/Helvetica")})
            page[NameObject("/Resources")] = DictionaryObject({NameObject("/Font"):DictionaryObject({NameObject("/F1"):writer._add_object(font)})})
            stream = DecodedStreamObject()
            stream.set_data(b"1 .9 .9 rg 0 0 800 60 re f 0 0 0 rg BT /F1 2 Tf 4 4 Td (SMALL VECTOR TEXT) Tj ET 1 0 0 rg 0 50 20 10 re f")
            page[NameObject("/Contents")] = writer._add_object(stream)
            writer.write(source)
            worker.execute({"Operation":"generate", "SourcePath":str(source), "OutputPath":str(output), "Settings":dict(TargetWidthMm=8000, TargetHeightMm=600, EdgeLeftMm=0, EdgeRightMm=0, EdgeTopMm=0, EdgeBottomMm=0, MakeCutline=False, MakeHole=False)})
            generated = PdfReader(output).pages[0]
            self.assertIn("SMALL VECTOR TEXT", generated.extract_text())
            self.assertFalse(any(obj.get_object().get("/Subtype") == "/Image" for obj in generated["/Resources"].get("/XObject",{}).values()))
            tile = folder/"tile.png"
            request = dict(Operation="preview", SourcePath=str(output), OutputPath=str(tile), MaxPixels=1200, FullCanvas=True, ViewportRegion=[0,.6,.1,.4])
            worker.execute(request)
            with Image.open(tile) as image:
                self.assertAlmostEqual(image.width,1200,delta=1)
                self.assertAlmostEqual(image.height,360,delta=1)
                # Bottom-left contains tiny text; the red top-left rectangle must be absent.
                self.assertLess(min(image.convert("L").getdata()),20)
                with pdfium.PdfDocument(str(output)) as doc:
                    full = doc[0].render(scale=12000/doc[0].get_size()[0]).to_pil().convert("RGB")
                    reference = full.crop((0, round(full.height*.6),1200,full.height))
                    actual = image.convert("RGB").resize(reference.size)
                    self.assertLess(sum(ImageStat.Stat(ImageChops.difference(actual,reference)).mean)/3,3)
            for region in ([0,0,0,1], [0,0,2,1], [float("nan"),0,1,1]):
                request["ViewportRegion"] = region
                with self.assertRaises(ValueError): worker.execute(request)

if __name__ == "__main__": unittest.main()

"""PET cutting insets must not resize artwork or expand the page."""
import importlib.util
from pathlib import Path
import tempfile
import unittest
from pypdf import PdfReader
from pypdf.generic import ContentStream

PROJECT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('worker', PROJECT/'PdfWorker/pdf_worker.py')
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)
SAMPLES = PROJECT.parents[1]/'1.요구분석'
SOURCE = SAMPLES/'1.(원본복원)페트_재단선만_724X2154_별색.pdf'
REFERENCE = SAMPLES/'1.페트_재단선만_724X2154_별색.pdf'

class PetCutTests(unittest.TestCase):
    def settings(self, **changes):
        s = dict(TargetWidthMm=724, TargetHeightMm=2154, EdgeLeftMm=0, EdgeRightMm=0,
                 EdgeTopMm=0, EdgeBottomMm=0, MakeCutline=True, MakeHole=False,
                 CutInsetTopMm=2, CutInsetBottomMm=2, CutInsetLeftMm=2, CutInsetRightMm=2)
        s.update(changes)
        return s

    def generate(self, folder, **changes):
        output = Path(folder)/'pet-cut.pdf'
        worker.execute(dict(Operation='generate', SourcePath=str(SOURCE), OutputPath=str(output), Settings=self.settings(**changes)))
        r = PdfReader(output)
        ops = ContentStream(r.pages[0].get_contents(), r).operations
        cut = [list(map(float,a)) for a,op in ops if op == b're'][-1]
        return r.pages[0], [v/worker.MM for v in cut]

    def test_customer_geometry_and_artwork_scale(self):
        with tempfile.TemporaryDirectory() as folder:
            page,cut = self.generate(folder)
            for actual,expected in zip(cut,(2,2,720,2150)):
                self.assertAlmostEqual(actual,expected,places=5)
            self.assertAlmostEqual(float(page.mediabox.width)/worker.MM,724,places=5)
            self.assertAlmostEqual(float(page.mediabox.height)/worker.MM,2154,places=5)
            self.assertAlmostEqual(float(page.trimbox.width)/worker.MM,720,places=5)
            # Source embedded image streams survive unchanged (no rasterization).
            def images(resources):
                result=[]
                for ref in resources.get('/XObject',{}).values():
                    o=ref.get_object()
                    if o.get('/Subtype')=='/Image': result.append(o.get_data())
                    elif o.get('/Subtype')=='/Form': result.extend(images(o['/Resources']))
                return result
            self.assertEqual(set(images(page['/Resources'])),set(images(PdfReader(SOURCE).pages[0]['/Resources'])))

    def test_asymmetric_insets_and_absolute_units_after_enlargement(self):
        with tempfile.TemporaryDirectory() as folder:
            page,cut = self.generate(folder, TargetWidthMm=7240, TargetHeightMm=10000,
                                     CutInsetLeftMm=3,CutInsetRightMm=7,CutInsetTopMm=5,CutInsetBottomMm=11)
            for actual,expected in zip(cut,(3,11,7230,9984)):
                self.assertAlmostEqual(actual,expected,places=5)
            self.assertAlmostEqual(float(page.mediabox.width)/worker.MM,7240,places=4)

    def test_invalid_margins(self):
        for changes in (dict(CutInsetLeftMm=float('nan')),
                        dict(CutInsetLeftMm=724),dict(CutInsetTopMm=2154)):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                worker.generation_settings(self.settings(**changes))

    def test_zero_insets_preserve_banner_cut(self):
        with tempfile.TemporaryDirectory() as folder:
            _,cut = self.generate(folder,CutInsetTopMm=0,CutInsetBottomMm=0,CutInsetLeftMm=0,CutInsetRightMm=0)
            for actual,expected in zip(cut,(0,0,724,2154)):
                self.assertAlmostEqual(actual,expected,places=5)

    def test_existing_cut_is_not_duplicated(self):
        with tempfile.TemporaryDirectory() as folder, self.assertRaises(ValueError):
            worker.execute(dict(Operation='generate',SourcePath=str(REFERENCE),OutputPath=str(Path(folder)/'duplicate.pdf'),Settings=self.settings()))

if __name__=='__main__': unittest.main()

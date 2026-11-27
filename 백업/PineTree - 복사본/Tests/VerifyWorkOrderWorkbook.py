"""Read-only checks of the actual Excel-generated workbook and template layout."""
import sys
import zipfile
import xml.etree.ElementTree as ET

ns = {"s": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
with zipfile.ZipFile(sys.argv[1]) as generated, zipfile.ZipFile(sys.argv[2]) as template:
    strings = ["".join(si.itertext()) for si in ET.fromstring(generated.read("xl/sharedStrings.xml"))]
    sheet = ET.fromstring(generated.read("xl/worksheets/sheet1.xml"))
    original = ET.fromstring(template.read("xl/worksheets/sheet1.xml"))
    cells = {c.get("r"): c for c in sheet.findall(".//s:c", ns)}

    def value(address):
        cell = cells.get(address)
        element = cell.find("s:v", ns) if cell is not None else None
        text = element.text if element is not None else ""
        return strings[int(text)] if cell is not None and cell.get("t") == "s" else text

    expected = {
        "B1": "테스트 거래처", "B8": "테스트 거래처",
        "C2": "작업서 미리보기 검증", "C9": "작업서 미리보기 검증",
        "A3": "2024", "A10": "2024", "A4": "3월 15일", "A11": "3월 15일", "C14": "2.5배",
    }
    for address, text in expected.items():
        assert value(address) == text, (address, value(address), text)
        assert cells[address].find("s:f", ns) is None, address
    for address in ("C11", "D11", "E11", "F11", "H11", "G12"):
        assert value(address) == "", (address, value(address))
    print("PASS: all field mappings, historical order date and unknown blank fields in exported XLSX")
    assert {c.get("ref") for c in sheet.find("s:mergeCells", ns)} == {
        c.get("ref") for c in original.find("s:mergeCells", ns)
    }
    for attribute in ("paperSize", "orientation", "scale"):
        assert sheet.find("s:pageSetup", ns).get(attribute) == original.find("s:pageSetup", ns).get(attribute)
    assert sheet.find("s:pageMargins", ns).attrib == original.find("s:pageMargins", ns).attrib
    print("PASS: merged cells, paper size, orientation, print scale and margins preserved")

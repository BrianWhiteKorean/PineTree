"""Isolated inspection, previews and configurable output PDF generation.

The request/response JSON protocol keeps user filenames out of shell commands.
Original files are never modified. Run this worker with its packaged runtime.
"""
from __future__ import annotations

import copy
import ctypes
import io
import json
import math
import os
from pathlib import Path
import sys
import tempfile
import traceback

from PIL import Image, ImageOps
import pypdfium2 as pdfium
from pypdf import PdfReader, PdfWriter, Transformation
from pypdf.errors import PdfReadError
from pypdf.generic import (
    ArrayObject, BooleanObject, ContentStream, DecodedStreamObject, DictionaryObject,
    FloatObject, NameObject, NumberObject, RectangleObject,
)

MM = 72.0 / 25.4
SUPPORTED = {".pdf", ".ai", ".eps", ".png", ".jpg", ".jpeg"}
DESCRIPTIONS = {".pdf": "Adobe PDF 문서", ".ai": "Adobe Illustrator 문서",
                ".png": "PNG 이미지", ".eps": "Encapsulated PostScript 문서", ".jpg": "JPEG 이미지", ".jpeg": "JPEG 이미지"}


def array(values):
    return ArrayObject([FloatObject(v) for v in values])


def ghostscript_pdf(source: Path, target: Path, dll_path: str | None):
    dll_path = dll_path or str(Path(__file__).parent / "runtime" / "ghostscript" / "gsdll64.dll")
    if not Path(dll_path).is_file():
        raise ValueError("EPS/구형 AI 변환 엔진이 없습니다. PdfWorker 런타임의 Ghostscript를 배포해주세요.")
    gs = ctypes.CDLL(str(dll_path))
    instance = ctypes.c_void_p()
    gs.gsapi_new_instance.argtypes = [ctypes.POINTER(ctypes.c_void_p), ctypes.c_void_p]
    gs.gsapi_set_arg_encoding.argtypes = [ctypes.c_void_p, ctypes.c_int]
    gs.gsapi_init_with_args.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.POINTER(ctypes.c_char_p)]
    gs.gsapi_exit.argtypes = [ctypes.c_void_p]
    gs.gsapi_delete_instance.argtypes = [ctypes.c_void_p]
    if gs.gsapi_new_instance(ctypes.byref(instance), None) < 0:
        raise ValueError("EPS 변환 엔진을 시작할 수 없습니다.")
    try:
        gs.gsapi_set_arg_encoding(instance, 1)  # UTF-8 paths, including Korean filenames.
        args = ["pinetree", "-dSAFER", "-dBATCH", "-dNOPAUSE", "-dNOPROMPT", "-dEPSCrop",
                "-sDEVICE=pdfwrite", "-dCompatibilityLevel=1.7", "-sOutputFile=" + str(target), "-f", str(source)]
        encoded = [s.encode("utf-8") for s in args]
        argv = (ctypes.c_char_p * len(encoded))(*encoded)
        result = gs.gsapi_init_with_args(instance, len(encoded), argv)
        if result not in (0, -101):
            raise ValueError("EPS/구형 AI를 PDF로 변환하지 못했습니다. 파일 및 포함 글꼴을 확인해주세요.")
    finally:
        gs.gsapi_exit(instance)
        gs.gsapi_delete_instance(instance)
    if not target.is_file() or target.stat().st_size == 0:
        raise ValueError("변환 엔진이 PDF를 생성하지 못했습니다.")


def open_document(source: Path, work: Path, request: dict):
    if not source.is_file():
        raise ValueError("입력파일을 찾을 수 없습니다.")
    ext = source.suffix.lower()
    if ext not in SUPPORTED:
        raise ValueError("PDF / AI / EPS / PNG / JPG 파일만 선택할 수 있습니다.")
    details = {}
    normalized = source
    with source.open("rb") as handle:
        header = handle.read(1024)
    if ext in (".png", ".jpg", ".jpeg"):
        with Image.open(source) as image:
            expected_format = "PNG" if ext == ".png" else "JPEG"
            if image.format != expected_format:
                raise ValueError(f"확장자와 이미지 형식이 다릅니다. {expected_format} 파일을 선택해주세요.")
            image.load()
            details = {"PixelWidth": image.width, "PixelHeight": image.height,
                       "ColorMode": image.mode, "Dpi": list(image.info.get("dpi", []))}
            orientation = image.getexif().get(274, 1)
            image = ImageOps.exif_transpose(image)
            details["PixelWidth"], details["PixelHeight"] = image.size
            if orientation in (5, 6, 7, 8) and len(details["Dpi"]) >= 2:
                details["Dpi"] = details["Dpi"][::-1]
            if "A" in image.getbands() or "transparency" in image.info:
                rgba = image.convert("RGBA")
                background = Image.new("RGB", rgba.size, "white")
                background.paste(rgba, mask=rgba.getchannel("A"))
                image = background
            elif image.mode not in ("RGB", "CMYK", "L"):
                image = image.convert("RGB")
            normalized = work / "image.pdf"
            # The physical output size is defined by the sample, never by a JPG DPI tag.
            image.save(normalized, "PDF", resolution=72, quality=100, subsampling=0)
    elif b"%PDF-" in header:
        pass
    elif ext == ".eps" or (ext == ".ai" and b"%!PS" in header):
        normalized = work / "converted.pdf"
        ghostscript_pdf(source, normalized, request.get("GhostscriptDll"))
    elif ext == ".ai":
        raise ValueError("이 AI에는 읽을 수 있는 PDF/PostScript 데이터가 없습니다. Illustrator에서 'PDF 호환 파일 만들기'로 저장하거나 PDF로 내보내주세요.")
    else:
        raise ValueError("PDF 파일 내용을 읽을 수 없습니다. 파일 형식을 확인해주세요.")
    reader = PdfReader(normalized, strict=False)
    if reader.is_encrypted:
        raise ValueError("암호화된 PDF는 지원하지 않습니다. 암호를 해제한 파일을 선택해주세요.")
    if not reader.pages:
        raise ValueError("파일에 표시할 페이지가 없습니다.")
    if ext == ".ai":
        text = reader.pages[0].extract_text() or ""
        if "saved without PDF" in text or "Create PDF Compatible File" in text:
            raise ValueError("이 AI는 PDF 호환 데이터 없이 저장되었습니다. Illustrator에서 PDF 호환 옵션으로 다시 저장해주세요.")
    # Normalize page rotation in a temporary copy so render and crop coordinates agree.
    if any(int(p.get("/Rotate", 0)) % 360 for p in reader.pages):
        writer = PdfWriter()
        for page in reader.pages:
            page.transfer_rotation_to_content()
            writer.add_page(page)
        normalized = work / "rotated.pdf"
        writer.write(normalized)
        reader = PdfReader(normalized)
    return reader, normalized, details


def source_region(page, is_ai: bool):
    box = tuple(map(float, page.cropbox))
    if not is_ai:
        return box, "페이지 전체"
    # Prefer a large shallow design clip/background over the Illustrator canvas.
    # ArtBox may also include notes; a full-page clip is not a design boundary.
    candidates = []
    depth = 0
    stack = []
    matrix = (1., 0., 0., 1., 0., 0.)
    operations = ContentStream(page.get_contents(), page.pdf).operations
    for i, (args, op) in enumerate(operations):
        if op == b"q":
            stack.append(matrix)
            depth += 1
        elif op == b"Q":
            if stack:
                matrix = stack.pop()
            depth = max(0, depth - 1)
        elif op == b"cm":
            a, b, c, d, e, f = map(float, args)
            A, B, C, D, E, F = matrix
            matrix = (a*A+b*C, a*B+b*D, c*A+d*C, c*B+d*D, e*A+f*C+E, e*B+f*D+F)
        elif op == b"re" and i + 1 < len(operations) and (
                operations[i+1][1] in (b"W", b"W*") or (depth == 0 and operations[i+1][1] in (b"f", b"f*"))):
            a, b, c, d, e, f = matrix
            if abs(b) > 1e-6 or abs(c) > 1e-6:
                continue
            x, y, w, h = map(float, args)
            x0, x1 = sorted((x*a+e, (x+w)*a+e))
            y0, y1 = sorted((y*d+f, (y+h)*d+f))
            rw, rh = x1-x0, y1-y0
            if rw <= 0 or rh <= 0:
                continue
            tolerance = .02
            if (depth <= 2 and box[0]-tolerance <= x0 < x1 <= box[2]+tolerance
                    and box[1]-tolerance <= y0 < y1 <= box[3]+tolerance
                    and rw*rh < (box[2]-box[0])*(box[3]-box[1])*.995):
                candidates.append((-(rw*rh), depth, (max(box[0], x0), max(box[1], y0), min(box[2], x1), min(box[3], y1))))
    if candidates:
        return sorted(candidates)[0][2], "디자인 영역 자동 인식 (사각 클립/배경 기준)"
    return box, "페이지 전체 (디자인 영역 자동 인식 안 됨)"


def detect_cutline(page):
    """Only report explicit, recognized spot-color strokes; ambiguous artwork stays unset."""
    known = {"thru", "cutcontour", "cutline", "diecut", "kisscut", "perfcut"}
    matches = set()

    def spot_names(resources):
        names = {}
        spaces = resources.get("/ColorSpace", {})
        if hasattr(spaces, "get_object"): spaces = spaces.get_object()
        for key, color in spaces.items():
            color = color.get_object()
            if isinstance(color, list) and len(color) > 1 and str(color[0]) == "/Separation":
                name = str(color[1]).lstrip("/")
                if "".join(c for c in name.lower() if c.isalnum()) in known:
                    names[str(key)] = name
        return names

    def walk(contents, resources, matrix, visited, depth=0):
        if depth > 16 or contents is None:
            return
        if hasattr(resources, "get_object"): resources = resources.get_object()
        colors = spot_names(resources)
        forms = resources.get("/XObject", {})
        if hasattr(forms, "get_object"): forms = forms.get_object()
        if not colors and not forms:
            return
        width, color, stack = 1.0, None, []
        for args, op in ContentStream(contents, page.pdf).operations:
            if op == b"q": stack.append((width, color, matrix))
            elif op == b"Q" and stack: width, color, matrix = stack.pop()
            elif op == b"w": width = float(args[0])
            elif op == b"CS": color = colors.get(str(args[0]))
            elif op in (b"G", b"RG", b"K"): color = None
            elif op == b"cm":
                a, b, c, d, e, f = map(float, args)
                A, B, C, D, E, F = matrix
                matrix = (a*A+b*C, a*B+b*D, c*A+d*C, c*B+d*D, e*A+f*C+E, e*B+f*D+F)
            elif op in (b"S", b"s", b"B", b"B*", b"b", b"b*") and color:
                sx, sy = math.hypot(matrix[0], matrix[1]), math.hypot(matrix[2], matrix[3])
                if abs(sx-sy) <= 1e-6*max(sx, sy, 1) and 0 < width*sx/MM <= 10:
                    matches.add((color, round(width*sx/MM, 6)))
            elif op == b"Do":
                ref = forms.get(args[0])
                if ref is None: continue
                obj = ref.get_object()
                if obj.get("/Subtype") != "/Form" or id(obj) in visited: continue
                a, b, c, d, e, f = map(float, obj.get("/Matrix", [1, 0, 0, 1, 0, 0]))
                A, B, C, D, E, F = matrix
                transform = (a*A+b*C, a*B+b*D, c*A+d*C, c*B+d*D, e*A+f*C+E, e*B+f*D+F)
                walk(obj, obj.get("/Resources", resources), transform, visited | {id(obj)}, depth+1)

    try:
        walk(page.get_contents(), page.get("/Resources", {}), (1., 0., 0., 1., 0., 0.), set())
    except (AttributeError, TypeError, ValueError, KeyError, PdfReadError):
        # Optional cutline analysis must not prevent otherwise valid file previews.
        matches.clear()
    if len(matches) == 1:
        name, width = next(iter(matches))
        return {"HasCutline": True, "CutlineColorName": name, "CutlineWidthMm": width}
    return {"HasCutline": False, "CutlineColorName": "", "CutlineWidthMm": None}


def inspect_file(source, reader, details, page_index):
    if not 0 <= page_index < len(reader.pages):
        raise ValueError("선택한 페이지가 없습니다.")
    page = reader.pages[page_index]
    region, region_label = source_region(page, source.suffix.lower() == ".ai")
    cw, ch = float(page.cropbox.width)/MM, float(page.cropbox.height)/MM
    rw, rh = (region[2]-region[0])/MM, (region[3]-region[1])/MM
    description = DESCRIPTIONS[source.suffix.lower()]
    parts = [f"{source.stat().st_size/1024/1024:.2f} MB"]
    if details:
        parts += [f"{details['PixelWidth']}×{details['PixelHeight']} px", details["ColorMode"]]
        dpi = details.get("Dpi", [])
        parts.append(f"{dpi[0]:g}×{dpi[1]:g} DPI" if len(dpi) >= 2 else "DPI 정보 없음")
    else:
        parts += [f"{len(reader.pages)}쪽", f"{cw:.2f}×{ch:.2f} mm"]
        if source.suffix.lower() == ".ai" and "자동 인식" in region_label and "안 됨" not in region_label:
            parts.append(f"디자인 {rw:.2f}×{rh:.2f} mm")
        parts.append("PDF/벡터: 단일 DPI 없음")
    source_width, source_height, dimension_note = rw, rh, ""
    if details:
        dpi = details.get("Dpi", [])
        valid_dpi = len(dpi) >= 2 and all(isinstance(v, (int, float)) and math.isfinite(v) and v > 0 for v in dpi[:2])
        dx, dy = dpi[:2] if valid_dpi else (72, 72)
        source_width = details["PixelWidth"] * 25.4 / dx
        source_height = details["PixelHeight"] * 25.4 / dy
        if not valid_dpi: dimension_note = "DPI 정보가 없어 72 DPI 기준 치수를 사용합니다."
    return {"SourceWidthMm": source_width, "SourceHeightMm": source_height, "SourceDimensionNote": dimension_note,
            **detect_cutline(page), "FileType": source.suffix[1:].upper() + " : " + description,
            "FileInfo": " · ".join(parts), "PageCount": len(reader.pages), "Region": list(region),
            "RegionDescription": region_label, "PageWidthMm": cw, "PageHeightMm": ch,
            "DesignWidthMm": rw, "DesignHeightMm": rh, **details}


def render_preview(pdf_path, page, region, destination, max_pixels, full_canvas, viewport_region=None):
    maximum = max(400, min(int(max_pixels), 5000))
    crop = tuple(map(float, page.cropbox))
    region = crop if full_canvas else region
    if viewport_region is not None:
        if len(viewport_region) != 4 or not all(math.isfinite(float(v)) for v in viewport_region):
            raise ValueError("Invalid preview viewport")
        x, y, w, h = map(float, viewport_region)
        if x < 0 or y < 0 or w <= 0 or h <= 0 or x+w > 1.00001 or y+h > 1.00001:
            raise ValueError("Invalid preview viewport")
        x0, y0, x1, y1 = region
        rw, rh = x1-x0, y1-y0
        region = (x0+rw*x, y1-rh*(y+h), x0+rw*(x+w), y1-rh*y)
    doc = pdfium.PdfDocument(str(pdf_path))
    try:
        index = int(page.page_number)
        pdfpage = doc[index]
        try:
            width, height = pdfpage.get_size()
            left, bottom = max(0, region[0]-crop[0]), max(0, region[1]-crop[1])
            right, top = max(0, crop[2]-region[2]), max(0, crop[3]-region[3])
            scale = maximum / max(region[2]-region[0], region[3]-region[1])
            bitmap = pdfpage.render(scale=scale, crop=(left, bottom, right, top))
            try:
                bitmap.to_pil().save(destination, "PNG")
            finally:
                bitmap.close()
        finally:
            pdfpage.close()
    finally:
        doc.close()


def has_thru(resources, visited=None, spot_name="thru"):
    visited = set() if visited is None else visited
    for value in resources.get("/ColorSpace", {}).values():
        color = value.get_object()
        if isinstance(color, list) and len(color) > 1 and str(color[0]) == "/Separation" and str(color[1]) == "/"+spot_name:
            return True
    for ref in resources.get("/XObject", {}).values():
        identity = getattr(ref, "idnum", id(ref))
        if identity in visited:
            continue
        visited.add(identity)
        obj = ref.get_object()
        if obj.get("/Subtype") == "/Form" and has_thru(obj.get("/Resources", {}), visited, spot_name):
            return True
    return False


def generation_settings(raw=None):
    # No Settings is the legacy PET preset used by DialogOrderInfoEdit.
    s = {"TargetWidthMm": 600, "TargetHeightMm": 1800, "FitMode": "CONTAIN",
         "EdgeTopMm": 2, "EdgeBottomMm": 2, "EdgeLeftMm": 2, "EdgeRightMm": 2,
         "MakeCutline": True, "LineWidthMm": .18, "SpotColorName": "thru", "MakeHole": True,
         "HoleType": "FOUR_CORNERS", "MarkLeftRightMm": 20, "MarkTopBottomMm": 20, "HoleSpacingMm": 500,
         "MakeWood": False, "WoodExtraMm": 0, "MakeWoodDesignLine": False, "WoodDesignOffsetMm": 0,
         "MakeWoodTopBottom": False, "WoodTopBottomExtraMm": 0, "MakeWoodTopBottomDesignLine": False, "WoodTopBottomDesignOffsetMm": 0}
    if raw is not None:
        if not isinstance(raw, dict):
            raise ValueError("PDF 생성 조건은 JSON 객체여야 합니다.")
        s.update({k: v for k, v in raw.items() if k in s and v is not None and v != ""})
    for key in ("TargetWidthMm", "TargetHeightMm", "EdgeTopMm", "EdgeBottomMm", "EdgeLeftMm", "EdgeRightMm",
                "LineWidthMm", "MarkLeftRightMm", "MarkTopBottomMm", "HoleSpacingMm", "WoodExtraMm", "WoodDesignOffsetMm", "WoodTopBottomExtraMm", "WoodTopBottomDesignOffsetMm"):
        try:
            s[key] = float(s[key])
        except (ValueError, TypeError):
            raise ValueError("숫자가 아닌 PDF 생성 조건입니다: " + key)
        if not math.isfinite(s[key]):
            raise ValueError("유효한 숫자가 아닙니다: " + key)
    for key in ("MakeCutline", "MakeHole", "MakeWood", "MakeWoodDesignLine", "MakeWoodTopBottom", "MakeWoodTopBottomDesignLine"):
        if not isinstance(s[key], bool):
            raise ValueError("생성 여부는 true/false 값이어야 합니다.")
    s["SpotColorName"] = str(s["SpotColorName"]).strip() or "thru"
    if min(s["TargetWidthMm"], s["TargetHeightMm"]) <= 0:
        raise ValueError("완성 가로·세로는 양수여야 합니다.")
    if min(s[k] for k in ("EdgeTopMm", "EdgeBottomMm", "EdgeLeftMm", "EdgeRightMm")) < 0:
        raise ValueError("가장자리 여유는 0 이상이어야 합니다.")
    if s["MakeWood"] and (s["WoodExtraMm"] < 0 or s["WoodDesignOffsetMm"] < 0
            or (s["MakeWoodDesignLine"] and s["WoodDesignOffsetMm"] > s["WoodExtraMm"])):
        raise ValueError("각목 여분은 0 이상이며 디자인선 이격은 여분 이내여야 합니다.")
    if s["MakeWoodTopBottom"] and (s["WoodTopBottomExtraMm"] < 0 or s["WoodTopBottomDesignOffsetMm"] < 0
            or (s["MakeWoodTopBottomDesignLine"] and s["WoodTopBottomDesignOffsetMm"] > s["WoodTopBottomExtraMm"])):
        raise ValueError("상하 각목 여분은 0 이상이며 디자인선 이격은 여분 이내여야 합니다.")
    finished_height = s["TargetHeightMm"] + (2*s["WoodTopBottomExtraMm"] if s["MakeWoodTopBottom"] else 0)
    finished_width = s["TargetWidthMm"] + (2*s["WoodExtraMm"] if s["MakeWood"] else 0)
    if max(finished_width+s["EdgeLeftMm"]+s["EdgeRightMm"], finished_height+s["EdgeTopMm"]+s["EdgeBottomMm"]) > 14400:
        raise ValueError("출력 크기는 14,400mm 이하여야 합니다.")
    if s["FitMode"] not in ("CONTAIN", "COVER"):
        raise ValueError("지원하지 않는 배치방식입니다.")
    if s["MakeCutline"] and (not 0 < s["LineWidthMm"] <= 10 or len(s["SpotColorName"]) > 100
                              or any(c in s["SpotColorName"] for c in "\r\n\0")):
        raise ValueError("재단선 두께 또는 별색 이름이 올바르지 않습니다.")
    if s["MakeHole"]:
        if s["HoleType"] not in ("FOUR_CORNERS", "CUSTOM_SPACING"):
            raise ValueError("지원하지 않는 타공 배치입니다.")
        if min(s["MarkLeftRightMm"], s["MarkTopBottomMm"]) < 2.5 or 2*s["MarkLeftRightMm"] >= finished_width or 2*s["MarkTopBottomMm"] >= finished_height:
            raise ValueError("마크 안쪽 거리는 2.5mm 이상이며 완성 크기의 절반보다 작아야 합니다.")
        if s["HoleType"] == "CUSTOM_SPACING" and s["HoleSpacingMm"] <= 0:
            raise ValueError("타공 최대 간격은 양수여야 합니다.")
    return s


def mark_positions(s):
    if not s["MakeHole"]:
        return []
    x0, y0 = s["EdgeLeftMm"]+s["MarkLeftRightMm"], s["EdgeBottomMm"]+s["MarkTopBottomMm"]
    w, h = s["TargetWidthMm"]+(2*s["WoodExtraMm"] if s["MakeWood"] else 0)-2*s["MarkLeftRightMm"], s["TargetHeightMm"]+(2*s["WoodTopBottomExtraMm"] if s["MakeWoodTopBottom"] else 0)-2*s["MarkTopBottomMm"]
    if s["HoleType"] == "FOUR_CORNERS":
        return [(x0+x, y0+y) for x in (0, w) for y in (0, h)]
    nx, ny = max(1, math.ceil(w/s["HoleSpacingMm"])), max(1, math.ceil(h/s["HoleSpacingMm"]))
    if 2*(nx+ny) > 2000:
        raise ValueError("타공마크는 최대 2,000개까지 생성할 수 있습니다. 간격을 늘려주세요.")
    return ([(x0+w*i/nx, y0+y) for y in (0, h) for i in range(nx+1)]
            + [(x0+x, y0+h*i/ny) for x in (0, w) for i in range(1, ny)])


def wood_process_colors(resources, visited=None):
    # The customer wood sample preserves CMYK numbers but uses DeviceCMYK instead
    # of the AI's four-channel ICC alias. Keep RGB profiles and spot/DeviceN intact.
    visited = set() if visited is None else visited
    resources = resources.get_object() if hasattr(resources, "get_object") else resources
    if id(resources) in visited: return
    visited.add(id(resources))
    spaces = resources.get("/ColorSpace", {})
    spaces = spaces.get_object() if hasattr(spaces, "get_object") else spaces
    for key, ref in list(spaces.items()):
        color = ref.get_object()
        if isinstance(color, list) and len(color) == 2 and color[0] == "/ICCBased" and color[1].get_object().get("/N") == 4:
            spaces[key] = NameObject("/DeviceCMYK")
    forms = resources.get("/XObject", {})
    forms = forms.get_object() if hasattr(forms, "get_object") else forms
    for ref in forms.values():
        form = ref.get_object()
        if form.get("/Subtype") == "/Form": wood_process_colors(form.get("/Resources", {}), visited)


def generate_pdf(reader, page_index, region, destination, settings=None):
    s = generation_settings(settings)
    marks = mark_positions(s)
    source = reader.pages[page_index]
    if source.get_contents() is None:
        raise ValueError("선택한 페이지에 출력할 그림이 없습니다.")
    if s["MakeCutline"] and has_thru(source.get("/Resources", {}), spot_name=s["SpotColorName"]):
        raise ValueError("입력파일에 이미 " + s["SpotColorName"] + " 별색 경로가 있습니다. 재단선 생성을 끄거나 후가공 전 원본을 선택해주세요.")
    if s["MakeWood"] or s["MakeWoodTopBottom"]:
        wood_process_colors(source.get("/Resources", {}))
    writer = PdfWriter()
    wood = s["WoodExtraMm"]*MM if s["MakeWood"] else 0
    wood_y = s["WoodTopBottomExtraMm"]*MM if s["MakeWoodTopBottom"] else 0
    image_width, image_height = s["TargetWidthMm"]*MM, s["TargetHeightMm"]*MM
    tw, th = image_width+2*wood, image_height+2*wood_y
    left, bottom = s["EdgeLeftMm"]*MM, s["EdgeBottomMm"]*MM
    pw, ph = tw+left+s["EdgeRightMm"]*MM, th+bottom+s["EdgeTopMm"]*MM
    page = writer.add_blank_page(width=pw, height=ph)
    page.trimbox = RectangleObject([left, bottom, left+tw, bottom+th])
    page.bleedbox = RectangleObject([0, 0, pw, ph])
    form = DecodedStreamObject()
    form.set_data(source.get_contents().get_data())
    form.update({NameObject("/Type"): NameObject("/XObject"), NameObject("/Subtype"): NameObject("/Form"),
                 NameObject("/BBox"): RectangleObject(region),
                 NameObject("/Resources"): source["/Resources"].clone(writer)})
    if "/Group" in source:
        form[NameObject("/Group")] = source["/Group"].clone(writer)
    artwork = writer._add_object(form)
    resources = DictionaryObject({NameObject("/XObject"): DictionaryObject({NameObject("/Artwork"): artwork})})
    page[NameObject("/Resources")] = resources
    instructions = []

    def stamp(src, dst):
        x0, y0, x1, y1 = src
        x, y, w, h = dst
        if w <= 0 or h <= 0:
            return
        sx, sy = w/(x1-x0), h/(y1-y0)
        instructions.append(f"q {x:.8f} {y:.8f} {w:.8f} {h:.8f} re W n "
                            f"{sx:.10f} 0 0 {sy:.10f} {x-x0*sx:.8f} {y-y0*sy:.8f} cm /Artwork Do Q")

    x0, y0, x1, y1 = region
    sw, sh = x1-x0, y1-y0
    image_left, image_bottom = left+wood, bottom+wood_y
    factor = (max if s["FitMode"] == "COVER" else min)(image_width/sw, image_height/sh)
    w, h = sw*factor, sh*factor
    x, y = image_left+(image_width-w)/2, image_bottom+(image_height-h)/2
    # Keep the main artwork at page level. Wrapping this AI's existing non-isolated
    # transparency groups in another Form changes soft-mask compositing in PDFium.
    imported = copy.copy(source)
    imported.cropbox = RectangleObject(region)
    page.merge_transformed_page(imported, Transformation((factor, 0, 0, factor, x-x0*factor, y-y0*factor)))
    main_content = (f"q {image_left:.8f} {image_bottom:.8f} {image_width:.8f} {image_height:.8f} re W n\n".encode("ascii")
                    + page.get_contents().get_data() + b"\nQ\n")
    resources = page["/Resources"]
    # Extend only edge strips; the main design remains a vector at its exact scale.
    # Contain letterboxing stays white when the source aspect ratio differs.
    if s["FitMode"] == "COVER" or (abs(w-image_width) < .1*MM and abs(h-image_height) < .1*MM):
        # Cover uses the visible crop edges, not the parts outside the trim box.
        x0, y0, x1, y1 = max(x0, x0+(image_left-x)/factor), max(y0, y0+(image_bottom-y)/factor), min(x1, x1-(x+w-image_left-image_width)/factor), min(y1, y1-(y+h-image_bottom-image_height)/factor)
        sw, sh = x1-x0, y1-y0
        x, y, w, h = image_left, image_bottom, image_width, image_height
        band = min(.1*MM/factor, sw/4, sh/4)
        right, top = pw-x-w, ph-y-h
        # Wood margins remain white instead of repeating stretched edge pixels.
        if not wood:
            stamp((x0, y0, x0+band, y1), (0, y, x, h))
            stamp((x1-band, y0, x1, y1), (x+w, y, right, h))
        if not wood_y:
            stamp((x0, y0, x1, y0+band), (x, 0, w, y))
            stamp((x0, y1-band, x1, y1), (x, y+h, w, top))
        if not wood and not wood_y:
            for sx0, sx1, tx, corner_width in [(x0, x0+band, 0, x), (x1-band, x1, x+w, right)]:
                for sy0, sy1, ty, corner_height in [(y0, y0+band, 0, y), (y1-band, y1, y+h, top)]:
                    stamp((sx0, sy0, sx1, sy1), (tx, ty, corner_width, corner_height))
    tint = DictionaryObject({NameObject("/FunctionType"): NumberObject(2), NameObject("/Domain"): array([0, 1]),
                             NameObject("/C0"): array([0, 0, 0, 0]), NameObject("/C1"): array([1, 0, 0, 0]), NameObject("/N"): NumberObject(1)})
    # Add our resources without replacing the original CMYK/DeviceN and soft masks.
    colors = resources.setdefault(NameObject("/ColorSpace"), DictionaryObject())
    states = resources.setdefault(NameObject("/ExtGState"), DictionaryObject())
    colors = colors.get_object()
    states = states.get_object()
    # Imported PDFs may already use the same resource names.
    def unused_name(dictionary, stem):
        name = stem
        index = 1
        while name in dictionary:
            name = stem + str(index)
            index += 1
        return NameObject(name)
    cut_name = unused_name(colors, "/CutColor")
    white_name = unused_name(states, "/NoOverprint")
    black_name = unused_name(states, "/BlackOverprint")
    if s["MakeCutline"]:
        colors[cut_name] = ArrayObject([
            NameObject("/Separation"), NameObject("/"+s["SpotColorName"]), NameObject("/DeviceCMYK"), writer._add_object(tint)])
    states[white_name] = DictionaryObject({NameObject("/OP"): BooleanObject(False), NameObject("/op"): BooleanObject(False)})
    states[black_name] = DictionaryObject({NameObject("/OP"): BooleanObject(True), NameObject("/op"): BooleanObject(True), NameObject("/OPM"): NumberObject(1)})
    if s["MakeWood"]:
        # Customer sample: process-black vertical strokes, 0.17pt (~0.06mm).
        line_width = .17
        positions = []
        if wood > 0:
            positions += [left+line_width/2, left+tw-line_width/2]
        if s["MakeWoodDesignLine"]:
            offset = s["WoodDesignOffsetMm"]*MM
            positions += [image_left-offset, image_left+image_width+offset]
        drawn = []
        for position in sorted(positions):
            position = max(left+line_width/2, min(left+tw-line_width/2, position))
            if any(abs(position-prior) < .001 for prior in drawn): continue
            drawn.append(position)
            instructions.append(f"q {white_name} gs 0 0 0 1 K {line_width:g} w "
                                f"{position:.8f} {bottom:.8f} m {position:.8f} {bottom+th:.8f} l S Q")
    if s["MakeWoodTopBottom"]:
        line_width = .17
        positions = []
        if wood_y > 0:
            positions += [bottom+line_width/2, bottom+th-line_width/2]
        if s["MakeWoodTopBottomDesignLine"]:
            offset = s["WoodTopBottomDesignOffsetMm"]*MM
            positions += [image_bottom-offset, image_bottom+image_height+offset]
        drawn = []
        for position in sorted(positions):
            position = max(bottom+line_width/2, min(bottom+th-line_width/2, position))
            if any(abs(position-prior) < .001 for prior in drawn): continue
            drawn.append(position)
            instructions.append(f"q {white_name} gs 0 0 0 1 K {line_width:g} w "
                                f"{left:.8f} {position:.8f} m {left+tw:.8f} {position:.8f} l S Q")
    for mx, my in marks:
        cx, cy = mx*MM, my*MM
        radius = 2.5*MM
        cross = f"{cx-radius:.8f} {cy:.8f} m {cx+radius:.8f} {cy:.8f} l {cx:.8f} {cy-radius:.8f} m {cx:.8f} {cy+radius:.8f} l S"
        instructions += [f"q {cx-radius:.8f} {cy-radius:.8f} {5*MM:.8f} {5*MM:.8f} re W n 0 J {white_name} gs 0 0 0 0 K {1.5*MM:.8f} w {cross}",
                         f"{black_name} gs 0 0 0 1 K 1 w {cross} Q"]
    if s["MakeCutline"]:
        instructions.append(f"q {white_name} gs {cut_name} CS 1 SCN {s['LineWidthMm']*MM:.8f} w {left:.8f} {bottom:.8f} {tw:.8f} {th:.8f} re S Q")
    content = DecodedStreamObject()
    content.set_data(main_content + ("\n".join(instructions) + "\n").encode("ascii"))
    page[NameObject("/Contents")] = writer._add_object(content)
    page.compress_content_streams()
    snapshot = {"schemaVersion": 1, "sourceRegionPt": list(region), "pageIndex": page_index,
                "settings": s, "markCount": len(marks), "engineVersion": "PineTreePdf/2"}
    writer.add_metadata({"/Title": f"PineTree output - {s['TargetWidthMm']:g}x{s['TargetHeightMm']:g}mm",
                         "/Producer": "PineTree PDF worker", "/PineTreeGenerationSettings": json.dumps(snapshot, ensure_ascii=False)})
    if "/OutputIntents" in reader.trailer["/Root"]:
        writer._root_object[NameObject("/OutputIntents")] = reader.trailer["/Root"]["/OutputIntents"].clone(writer)
    writer.write(destination)
    check = PdfReader(destination)
    if len(check.pages) != 1 or (s["MakeCutline"] and not has_thru(check.pages[0]["/Resources"], spot_name=s["SpotColorName"])):
        raise ValueError("생성한 PDF의 페이지/별색 검증에 실패했습니다.")
    return {"OutputPath": str(destination), "Recipe": f"완성 {tw/MM:g}×{th/MM:g}mm / 출력 {pw/MM:g}×{ph/MM:g}mm / "
            + ("맞춤" if s["FitMode"] == "CONTAIN" else "채움")
            + (f" / {s['SpotColorName']} 재단선 {s['LineWidthMm']:g}mm" if s["MakeCutline"] else " / 재단선 없음")
            + (f" / 좌우각목 여분 각 {s['WoodExtraMm']:g}mm" if s["MakeWood"] else "")
            + (f" / 상하각목 여분 각 {s['WoodTopBottomExtraMm']:g}mm" if s["MakeWoodTopBottom"] else "")
            + f" / 십자마크 {len(marks)}개", "Scale": factor, "FitMode": s["FitMode"].lower(), "PageCount": 1,
            "OutputWidthMm": pw/MM, "OutputHeightMm": ph/MM, "MarkCount": len(marks)}


def execute(request):
    source = Path(request["SourcePath"]).resolve()
    with tempfile.TemporaryDirectory(prefix="pinetree_pdf_", dir=request.get("WorkDirectory")) as folder:
        work = Path(folder)
        reader, normalized, details = open_document(source, work, request)
        index = int(request.get("PageIndex", 0))
        info = inspect_file(source, reader, details, index)
        operation = request["Operation"]
        if operation == "inspect":
            return info
        destination = Path(request["OutputPath"]).resolve()
        if destination == source or (destination.exists() and os.path.samefile(source, destination)):
            raise ValueError("원본파일을 덮어쓸 수 없습니다. 다른 저장 경로를 선택해주세요.")
        destination.parent.mkdir(parents=True, exist_ok=True)
        if operation == "preview":
            render_preview(normalized, reader.pages[index], info["Region"], destination,
                           request.get("MaxPixels", 1600), request.get("FullCanvas", False), request.get("ViewportRegion"))
            return {**info, "PreviewPath": str(destination)}
        if operation == "generate":
            return {**info, **generate_pdf(reader, index, info["Region"], destination, request.get("Settings"))}
        raise ValueError("지원하지 않는 파일 처리 작업입니다.")


def main():
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    request_path, response_path = map(Path, sys.argv[1:3])
    try:
        request = json.loads(request_path.read_text(encoding="utf-8-sig"))
        response = {"Success": True, **execute(request)}
    except Exception as error:
        traceback.print_exc(file=sys.stderr)
        response = {"Success": False, "Error": str(error)}
    response_path.write_text(json.dumps(response, ensure_ascii=False), encoding="utf-8")
    return 0 if response["Success"] else 1


if __name__ == "__main__":
    raise SystemExit(main())

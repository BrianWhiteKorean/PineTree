"""Isolated inspection, previews and configurable output PDF generation.

The request/response JSON protocol keeps user filenames out of shell commands.
Original files are never modified. Run this worker with its packaged runtime.
"""
from __future__ import annotations

import os
from time import perf_counter_ns

_IMPORT_START = perf_counter_ns() if os.environ.get("PINETREE_PDF_WORKER_TIMING") == "1" else None

import copy
from contextlib import contextmanager, nullcontext
import ctypes
import io
import json
import math
from pathlib import Path
import sys
import tempfile
import traceback

from PIL import Image, ImageOps
import pypdfium2 as pdfium
from pypdf import filters as pdf_filters
from pypdf import PdfReader, PdfWriter, Transformation, PageObject
from pypdf.errors import PdfReadError
from pypdf.generic import (
    ArrayObject, BooleanObject, ContentStream, DecodedStreamObject, DictionaryObject,
    FloatObject, NameObject, NumberObject, RectangleObject,
)

_TIMINGS = {"module_init": perf_counter_ns() - _IMPORT_START} if _IMPORT_START is not None else None
_NO_TIMING = nullcontext()


@contextmanager
def _timed(stage):
    start = perf_counter_ns()
    try:
        yield
    finally:
        _TIMINGS[stage] = _TIMINGS.get(stage, 0) + perf_counter_ns() - start


def measure(stage):
    return _NO_TIMING if _TIMINGS is None else _timed(stage)

MM = 72.0 / 25.4
SUPPORTED = {".pdf", ".ai", ".eps", ".png", ".jpg", ".jpeg"}
DESCRIPTIONS = {".pdf": "Adobe PDF 문서", ".ai": "Adobe Illustrator 문서",
                ".png": "PNG 이미지", ".eps": "Encapsulated PostScript 문서", ".jpg": "JPEG 이미지", ".jpeg": "JPEG 이미지"}


def array(values):
    return ArrayObject([FloatObject(v) for v in values])


@contextmanager
def source_stream_limit(source):
    # Print-ready AI/PDF images can exceed pypdf's 75 MB encoded-stream limit.
    # Bound reads by the actual file size; keep decompression limits unchanged.
    # Apply this before AI text extraction, which also resolves image objects.
    previous = pdf_filters.MAX_DECLARED_STREAM_LENGTH
    pdf_filters.MAX_DECLARED_STREAM_LENGTH = max(previous, source.stat().st_size if source.is_file() else 0)
    try:
        yield
    finally:
        pdf_filters.MAX_DECLARED_STREAM_LENGTH = previous


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
    # Multi-page PDF-compatible AI already has one bounded page per artboard.
    # An internal clip may be just one tile (e.g. the apple in Yokel), not the design.
    if not is_ai or len(page.pdf.pages) > 1 or "/ArtBox" not in page:
        return box, "페이지 전체"
    # Only infer a design region for legacy canvases with an explicit inner ArtBox.
    # When the ArtBox spans the page, internal clips can cover individual objects.
    art_box = tuple(map(float, page.artbox))
    tolerance = .02
    page_area = (box[2]-box[0])*(box[3]-box[1])
    inner_area_limit = page_area*.995
    if (not (box[0]-tolerance <= art_box[0] < art_box[2] <= box[2]+tolerance
                    and box[1]-tolerance <= art_box[1] < art_box[3] <= box[3]+tolerance)
            or (art_box[2]-art_box[0])*(art_box[3]-art_box[1]) >= inner_area_limit):
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
            if (depth <= 2 and box[0]-tolerance <= x0 < x1 <= box[2]+tolerance
                    and box[1]-tolerance <= y0 < y1 <= box[3]+tolerance
                    and rw*rh < inner_area_limit):
                candidates.append((-(rw*rh), depth, (max(box[0], x0), max(box[1], y0), min(box[2], x1), min(box[3], y1))))
    if candidates:
        return sorted(candidates)[0][2], "디자인 영역 자동 인식 (사각 클립/배경 기준)"
    return box, "페이지 전체 (디자인 영역 자동 인식 안 됨)"


def detect_cutline(page, spot_name=None):
    """Only report explicit, recognized spot-color strokes; ambiguous artwork stays unset."""
    known = {"thru", "cutcontour", "cutline", "diecut", "kisscut", "perfcut"}
    if spot_name is not None:
        known = {"".join(c for c in spot_name.lower() if c.isalnum())}
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
    if len(matches) == 1 or (spot_name is not None and matches):
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
    s = {"TargetWidthMm": 600, "TargetHeightMm": 1800, "OutputRipScale": 1, "FitMode": "CONTAIN", "ExpandImage": False, "SourceRotationDegreesClockwise": 0, "SourceFlipHorizontal": False,
         "EdgeTopMm": 0, "EdgeBottomMm": 0, "EdgeLeftMm": 0, "EdgeRightMm": 0,
         "MakeAlScroll": False, "ScrollTopMm": 5, "ScrollBottomMm": 5,
         "CutInsetsFromPage": False, "CutInsetTopMm": 0, "CutInsetBottomMm": 0, "CutInsetLeftMm": 0, "CutInsetRightMm": 0,
         "MakeCutline": True, "LineWidthMm": .18, "CutlineColorMode": "SPOT", "SpotColorName": "thru", "MakeHole": True,
         "HoleBackgroundColor": "#FFFFFF", "HoleMarkerColor": "#000000", "HoleMarkKind": "CROSS", "HoleType": "FOUR_CORNERS", "MarkLeftRightMm": 20, "MarkTopBottomMm": 20, "HoleEdges": None, "HolePlacements": None, "HoleSpacingMm": 500,
         "MakeWood": False, "WoodExtraMm": 0, "MakeWoodDesignLine": False, "WoodDesignOffsetMm": 0,
         "WoodExtraLineThicknessMm": .17/MM, "WoodDesignLineThicknessMm": .17/MM,
         "MakeWoodTopBottom": False, "WoodTopBottomExtraMm": 0, "MakeWoodTopBottomDesignLine": False, "WoodTopBottomDesignOffsetMm": 0,
         "WoodTopBottomExtraLineThicknessMm": .17/MM, "WoodTopBottomDesignLineThicknessMm": .17/MM,
         "RopeSewPasses": [], "HemSewPasses": []}
    if raw is not None:
        if not isinstance(raw, dict):
            raise ValueError("PDF 생성 조건은 JSON 객체여야 합니다.")
        s.update({k: v for k, v in raw.items() if k in s and v is not None and v != ""})
    for key in ("TargetWidthMm", "TargetHeightMm", "OutputRipScale", "EdgeTopMm", "EdgeBottomMm", "EdgeLeftMm", "EdgeRightMm",
                "ScrollTopMm", "ScrollBottomMm", "CutInsetTopMm", "CutInsetBottomMm", "CutInsetLeftMm", "CutInsetRightMm",
                "LineWidthMm", "MarkLeftRightMm", "MarkTopBottomMm", "HoleSpacingMm", "WoodExtraMm", "WoodDesignOffsetMm", "WoodTopBottomExtraMm", "WoodTopBottomDesignOffsetMm",
                "WoodTopBottomExtraLineThicknessMm", "WoodTopBottomDesignLineThicknessMm", "WoodExtraLineThicknessMm", "WoodDesignLineThicknessMm"):
        try:
            s[key] = float(s[key])
        except (ValueError, TypeError):
            raise ValueError("숫자가 아닌 PDF 생성 조건입니다: " + key)
        if not math.isfinite(s[key]):
            raise ValueError("유효한 숫자가 아닙니다: " + key)
    if s["OutputRipScale"] < 1:
        raise ValueError("RIP 출력 배율은 1 이상이어야 합니다.")
    for key in ("SourceFlipHorizontal", "CutInsetsFromPage", "ExpandImage", "MakeAlScroll", "MakeCutline", "MakeHole", "MakeWood", "MakeWoodDesignLine", "MakeWoodTopBottom", "MakeWoodTopBottomDesignLine"):
        if not isinstance(s[key], bool):
            raise ValueError("생성 여부는 true/false 값이어야 합니다.")
    if isinstance(s["SourceRotationDegreesClockwise"], bool) or s["SourceRotationDegreesClockwise"] not in (0, 90, 180, 270):
        raise ValueError("원본 회전은 0, 90, 180, 270도여야 합니다.")
    s["SpotColorName"] = str(s["SpotColorName"]).strip() or "thru"
    if not isinstance(s["RopeSewPasses"], list):
        raise ValueError("사방줄미싱 목록이 올바르지 않습니다.")
    ropes = []
    for raw_rope in s["RopeSewPasses"]:
        if not isinstance(raw_rope, dict) or raw_rope.get("TypeCode", "ROPE_5") not in ("ROPE_5", "ROPE_8"):
            raise ValueError("사방줄미싱 종류를 선택해주세요.")
        rope = {"TypeCode": raw_rope.get("TypeCode", "ROPE_5")}
        for key in ("TopMm", "BottomMm", "LeftMm", "RightMm", "LineThicknessMm"):
            try: value = float(raw_rope.get(key, .17/MM if key == "LineThicknessMm" else 15))
            except (ValueError, TypeError): raise ValueError("사방줄미싱 간격은 숫자여야 합니다.")
            if not math.isfinite(value) or not 0 < value <= 100000:
                raise ValueError("사방줄미싱 간격과 선두께는 양수여야 합니다.")
            rope[key] = value
        ropes.append(rope)
    s["RopeSewPasses"] = ropes
    if not isinstance(s["HemSewPasses"], list):
        raise ValueError("사방시접미싱 목록이 올바르지 않습니다.")
    hems = []
    for raw_hem in s["HemSewPasses"]:
        if not isinstance(raw_hem, dict) or raw_hem.get("TypeCode", "SINGLE") not in ("SINGLE", "DOUBLE"):
            raise ValueError("사방시접미싱 종류를 선택해주세요.")
        try: thickness = float(raw_hem.get("LineThicknessMm", .06))
        except (ValueError, TypeError): raise ValueError("사방시접미싱 선두께는 숫자여야 합니다.")
        if not math.isfinite(thickness) or not 0 < thickness <= 100000:
            raise ValueError("사방시접미싱 선두께는 양수여야 합니다.")
        kind = raw_hem.get("TypeCode", "SINGLE")
        hems.append({"TypeCode": kind, "MarginMm": 30 if kind == "DOUBLE" else 15, "LineThicknessMm": thickness})
    s["HemSewPasses"] = hems
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
    if s["MakeAlScroll"] and min(s["ScrollTopMm"], s["ScrollBottomMm"]) < 0:
        raise ValueError("족자및쫄대 상·하 여백은 0 이상이어야 합니다.")
    if s["MakeWood"] and any(not 0 < s[k] <= 100000 for k in ("WoodExtraLineThicknessMm", "WoodDesignLineThicknessMm")):
        raise ValueError("좌우각목 선두께는 0 초과 100,000mm 이하로 입력해주세요.")
    if s["MakeWoodTopBottom"] and any(not 0 < s[k] <= 100000 for k in ("WoodTopBottomExtraLineThicknessMm", "WoodTopBottomDesignLineThicknessMm")):
        raise ValueError("상하각목 선두께는 0 초과 100,000mm 이하로 입력해주세요.")
    finished_width, finished_height = hole_finished_size(s)
    if max(finished_width+s["EdgeLeftMm"]+s["EdgeRightMm"], finished_height+s["EdgeTopMm"]+s["EdgeBottomMm"]) > 14400:
        raise ValueError("출력 크기는 14,400mm 이하여야 합니다.")
    if s["FitMode"] not in ("CONTAIN", "COVER"):
        raise ValueError("지원하지 않는 배치방식입니다.")
    if s["MakeCutline"] and (min(s[k] for k in ("CutInsetTopMm", "CutInsetBottomMm", "CutInsetLeftMm", "CutInsetRightMm")) < 0
            or s["CutInsetLeftMm"]+s["CutInsetRightMm"] >= finished_width+(s["EdgeLeftMm"]+s["EdgeRightMm"] if s["CutInsetsFromPage"] else 0)
            or s["CutInsetTopMm"]+s["CutInsetBottomMm"] >= finished_height+(s["EdgeTopMm"]+s["EdgeBottomMm"] if s["CutInsetsFromPage"] else 0)):
        raise ValueError("재단 여백은 0 이상이며 좌우·상하 합은 출력 폭·길이보다 작아야 합니다.")
    if s["MakeCutline"]:
        if not 0 < s["LineWidthMm"] <= 10:
            raise ValueError("재단선 두께는 0 초과 10mm 이하로 입력해주세요.")
        if s["CutlineColorMode"] not in ("SPOT", "BLACK"):
            raise ValueError("재단선 색상방식을 선택해주세요.")
        if s["CutlineColorMode"] == "SPOT" and (len(s["SpotColorName"]) > 100
                or any(c in s["SpotColorName"] for c in "\r\n\0")):
            raise ValueError("별색 이름은 100자 이내로 입력해주세요.")
    if s["MakeHole"]:
        for key in ("HoleBackgroundColor", "HoleMarkerColor"):
            color = s[key]
            if not isinstance(color, str) or len(color) != 7 or color[0] != "#" or any(c not in "0123456789abcdefABCDEF" for c in color[1:]):
                raise ValueError("타공 색상은 #RRGGBB 형식이어야 합니다.")
        if s["HoleMarkKind"] not in ("DOT", "CROSS"):
            raise ValueError("지원하지 않는 타공 마크종류입니다.")
        for kind, edges, spacing, runs, image_coordinates in hole_placements(s):
            if kind not in ("FOUR_CORNERS", "CUSTOM_SPACING"):
                raise ValueError("지원하지 않는 타공 배치입니다.")
            if runs:
                for run, vertical in runs:
                    hole_run_endpoints(run, s["TargetWidthMm"] if image_coordinates else finished_width,
                                       s["TargetHeightMm"] if image_coordinates else finished_height, vertical)
                continue
            if any(edges[side+"Enabled"] for side in ("Left", "Right", "Top", "Bottom")):
                if min(edges[side+"Mm"] for side in ("Left", "Right", "Top", "Bottom")) < 1.5:
                    raise ValueError("타공 안쪽 거리는 1.5mm 이상으로 입력해주세요.")
                if edges["LeftMm"] + edges["RightMm"] >= finished_width or edges["TopMm"] + edges["BottomMm"] >= finished_height:
                    raise ValueError("타공 안쪽 거리의 좌우·상하 합은 각각 완성 가로·세로보다 작아야 합니다.")
                if kind == "CUSTOM_SPACING" and (type(spacing) not in (int, float) or not math.isfinite(spacing) or spacing <= 0):
                    raise ValueError("타공 최대 간격은 양수여야 합니다.")
    return s


def sewing_passes(s):
    # Both sewing operations add physical white margins, without extra stroke padding.
    hems = [dict(TopMm=h["MarginMm"], BottomMm=h["MarginMm"], LeftMm=h["MarginMm"], RightMm=h["MarginMm"],
                 LineThicknessMm=h["LineThicknessMm"]) for h in s["HemSewPasses"]]
    return s["RopeSewPasses"] + hems


def rope_padding(s, side):
    # Margins are physical output millimetres; guide strokes stay inside them.
    return sum(rope[side] for rope in sewing_passes(s))


def hole_edges(s, raw):
    if raw is not None and not isinstance(raw, dict):
        raise ValueError("타공 방향 설정이 올바르지 않습니다.")
    raw = raw or {}
    edges = {}
    for side, legacy in (("Left", "MarkLeftRightMm"), ("Right", "MarkLeftRightMm"),
                         ("Top", "MarkTopBottomMm"), ("Bottom", "MarkTopBottomMm")):
        enabled_key, inset_key = side+"Enabled", side+"Mm"
        enabled = raw.get(enabled_key, True)
        inset = raw.get(inset_key, s[legacy])
        if not isinstance(enabled, bool) or type(inset) not in (int, float) or not math.isfinite(inset):
            raise ValueError("타공 방향 또는 안쪽 거리가 올바르지 않습니다.")
        edges.update({enabled_key: enabled, inset_key: inset})
    return edges


def hole_placements(s):
    rows = s.get("HolePlacements")
    if rows is None:
        yield s["HoleType"], hole_edges(s, s.get("HoleEdges")), s["HoleSpacingMm"], (), False
        return
    if not isinstance(rows, list):
        raise ValueError("타공 배치 목록이 올바르지 않습니다.")
    for row in rows:
        if not isinstance(row, dict):
            raise ValueError("타공 배치가 비어 있습니다.")
        kind = row.get("TypeCode")
        runs = tuple((row[key], key in ("LeftRun", "RightRun"))
                     for key in ("TopRun", "BottomRun", "LeftRun", "RightRun") if row.get(key) is not None)
        if runs:
            if kind != "CUSTOM_SPACING":
                raise ValueError("지원하지 않는 타공 배치입니다.")
            yield kind, None, None, runs, row.get("ImageCoordinates", False)
        else:
            if not isinstance(row.get("Edges"), dict):
                raise ValueError("타공 배치가 비어 있습니다.")
            yield kind, hole_edges(s, row["Edges"]), row.get("SpacingMm"), (), False


def hole_finished_size(s):
    width = s["TargetWidthMm"] + (2*s["WoodExtraMm"] if s["MakeWood"] else 0)
    height = s["TargetHeightMm"] + (2*s["WoodTopBottomExtraMm"] if s["MakeWoodTopBottom"] else 0)
    if s["MakeAlScroll"]:
        height += s["ScrollTopMm"] + s["ScrollBottomMm"]
    return (width + rope_padding(s, "LeftMm") + rope_padding(s, "RightMm"),
            height + rope_padding(s, "TopMm") + rope_padding(s, "BottomMm"))


def run_coordinate(run, near_key, far_key, size):
    far = run.get(far_key)
    if far is None:
        return run.get(near_key)
    return max(0, size - far) if valid_coordinate(far, size) else None


def hole_run_endpoints(run, width, height, vertical):
    if not isinstance(run, dict) or type(run.get("Count")) is not int or not 1 <= run["Count"] <= 2000:
        raise ValueError("타공 개수는 1~2,000개로 입력해주세요.")
    count = run["Count"]
    left = run_coordinate(run, "StartLeftMm", "StartRightMm", width)
    top = run_coordinate(run, "StartTopMm", "StartBottomMm", height)
    if not valid_coordinate(left, width) or not valid_coordinate(top, height):
        raise ValueError("타공 시작 위치가 완성품의 가로·세로 범위를 벗어났습니다.")
    if run.get("Mode") == "UNIFORM":
        end_left = run_coordinate(run, "EndLeftMm", "EndRightMm", width)
        end_top = run_coordinate(run, "EndTopMm", "EndBottomMm", height)
        if not valid_coordinate(end_left, width) or not valid_coordinate(end_top, height):
            raise ValueError("타공 끝 위치가 이미지 가로·세로 범위를 벗어났습니다.")
        if count != 1 and same_hole_position((left, top), (end_left, end_top)):
            raise ValueError("균등간격의 시작점과 끝점이 같으면 1개를 입력해주세요.")
    elif run.get("Mode") == "FIXED":
        spacing = run.get("SpacingMm")
        if type(spacing) not in (int, float) or not math.isfinite(spacing) or spacing <= 0:
            raise ValueError("고정간격은 0mm보다 커야 합니다.")
        actual_count = 1
        start, limit = (top, height) if vertical else (left, width)
        while actual_count < count and valid_coordinate(start + actual_count * spacing, limit):
            actual_count += 1
        count = actual_count
        offset = (count - 1) * spacing
        end_left, end_top = (left, top + offset) if vertical else (left + offset, top)
    else:
        raise ValueError("타공 간격 방식을 선택해주세요.")
    return count, left, top, end_left, end_top


def valid_coordinate(value, limit):
    return type(value) in (int, float) and 0 <= value <= limit + 1e-9


def same_hole_position(point, other):
    return abs(point[0] - other[0]) <= 1e-9 and abs(point[1] - other[1]) <= 1e-9


def uniform_mark_positions(start, end, count, perpendicular_marks):
    if same_hole_position(start, end):
        return [start]
    skip_start = any(same_hole_position(start, point) for point in perpendicular_marks)
    skip_end = any(same_hole_position(end, point) for point in perpendicular_marks)
    intervals = count + skip_start + skip_end - 1
    if intervals == 0:
        return [((start[0] + end[0])/2, (start[1] + end[1])/2)]

    step_x, step_y = (end[0] - start[0])/intervals, (end[1] - start[1])/intervals
    marks = []
    for i in range(int(skip_start), int(skip_start) + count):
        if i == 0:
            marks.append(start)
        elif i == intervals:
            marks.append(end)
        else:
            marks.append((start[0] + i*step_x, start[1] + i*step_y))
    return marks


def run_mark_positions(s, run, width, height, image_coordinates, vertical, perpendicular_marks):
    origin_x = s["EdgeLeftMm"]
    origin_bottom = s["EdgeBottomMm"]
    if image_coordinates:
        width, height = s["TargetWidthMm"], s["TargetHeightMm"]
        origin_x += rope_padding(s, "LeftMm") + (s["WoodExtraMm"] if s["MakeWood"] else 0)
        origin_bottom += (rope_padding(s, "BottomMm") + (s["WoodTopBottomExtraMm"] if s["MakeWoodTopBottom"] else 0)
                          + (s["ScrollBottomMm"] if s["MakeAlScroll"] else 0))
    count, left, top, end_left, end_top = hole_run_endpoints(run, width, height, vertical)
    origin_y = origin_bottom + height
    start = (origin_x + left, origin_y - top)
    if run["Mode"] == "FIXED":
        if vertical:
            return [(start[0], start[1] - i*run["SpacingMm"]) for i in range(count)]
        return [(start[0] + i*run["SpacingMm"], start[1]) for i in range(count)]
    end = (origin_x + end_left, origin_y - end_top)
    return uniform_mark_positions(start, end, count, perpendicular_marks)


def mark_positions(s):
    if not s["MakeHole"]:
        return []
    marks = []
    earlier_runs = {False: [], True: []}
    width, height = hole_finished_size(s)
    for kind, edges, spacing, runs, image_coordinates in hole_placements(s):
        if runs:
            row_runs = {False: [], True: []}
            for run, vertical in runs:
                points = run_mark_positions(s, run, width, height, image_coordinates, vertical, earlier_runs[not vertical])
                marks.extend(points)
                row_runs[vertical].extend(points)
            for direction, points in row_runs.items():
                earlier_runs[direction].extend(points)
        else:
            marks.extend(placement_mark_positions(s, kind, edges, spacing, width, height))
        if len(marks) > 2000:
            raise ValueError("전체 타공마크는 최대 2,000개까지 생성할 수 있습니다.")
    return marks


def placement_mark_positions(s, kind, edges, spacing, width, height):
    left, right = edges["LeftEnabled"], edges["RightEnabled"]
    bottom, top = edges["BottomEnabled"], edges["TopEnabled"]
    if not (left or right or bottom or top):
        return []
    x0, y0 = s["EdgeLeftMm"]+edges["LeftMm"], s["EdgeBottomMm"]+edges["BottomMm"]
    w = width - edges["LeftMm"] - edges["RightMm"]
    h = height - edges["TopMm"] - edges["BottomMm"]
    vertical_edges = [(x, enabled) for x, enabled in ((x0, left), (x0+w, right))]
    horizontal_edges = [(y, enabled) for y, enabled in ((y0, bottom), (y0+h, top))]
    corners = [(x, y) for x, vertical in vertical_edges for y, horizontal in horizontal_edges if vertical or horizontal]
    if kind == "FOUR_CORNERS":
        return corners
    horizontal_count, vertical_count = int(bottom)+int(top), int(left)+int(right)
    horizontal_steps = w/spacing if horizontal_count else 0
    vertical_steps = h/spacing if vertical_count else 0
    if max(horizontal_steps, vertical_steps) > 2000:
        raise ValueError("타공마크는 최대 2,000개까지 생성할 수 있습니다. 간격을 늘려주세요.")
    nx, ny = max(1, math.ceil(horizontal_steps)), max(1, math.ceil(vertical_steps))
    if len(corners)+horizontal_count*(nx-1)+vertical_count*(ny-1) > 2000:
        raise ValueError("타공마크는 최대 2,000개까지 생성할 수 있습니다. 간격을 늘려주세요.")
    return (corners
            + [(x0+w*i/nx, y) for y, enabled in horizontal_edges if enabled for i in range(1, nx)]
            + [(x, y0+h*i/ny) for x, enabled in vertical_edges if enabled for i in range(1, ny)])


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


def transform_artwork(source, region, clockwise_degrees, flip_horizontal):
    if not clockwise_degrees and not flip_horizontal:
        return source, region
    x0, y0, x1, y1 = region
    width, height = x1-x0, y1-y0
    matrices = {
        0: (1, 0, 0, 1, -x0, -y0),
        90: (0, -1, 1, 0, -y0, width+x0),
        180: (-1, 0, 0, -1, width+x0, height+y0),
        270: (0, 1, -1, 0, height+y0, -x0),
    }
    a, b, c, d, e, f = matrices[clockwise_degrees]
    out_width, out_height = (height, width) if clockwise_degrees % 180 else (width, height)
    if flip_horizontal:
        a, c, e = -a, -c, out_width-e
    transformed = PageObject.create_blank_page(width=out_width, height=out_height)
    clipped = copy.copy(source)
    clipped.cropbox = RectangleObject(region)
    transformed.merge_transformed_page(clipped, Transformation((a, b, c, d, e, f)))
    if "/Group" in source:
        transformed[NameObject("/Group")] = source["/Group"]
    return transformed, (0, 0, out_width, out_height)


def generate_pdf(reader, page_index, region, destination, settings=None, writer=None):
    s = generation_settings(settings)
    marks = mark_positions(s)
    source = reader.pages[page_index]
    if source.get_contents() is None:
        raise ValueError("선택한 페이지에 출력할 그림이 없습니다.")
    spot_cutline = s["MakeCutline"] and s["CutlineColorMode"] == "SPOT"
    if spot_cutline and detect_cutline(source, s["SpotColorName"])["HasCutline"]:
        raise ValueError("입력파일에 이미 " + s["SpotColorName"] + " 별색 경로가 있습니다. 재단선 생성을 끄거나 후가공 전 원본을 선택해주세요.")
    if s["MakeWood"] or s["MakeWoodTopBottom"]:
        wood_process_colors(source.get("/Resources", {}))
    source, region = transform_artwork(source, region, s["SourceRotationDegreesClockwise"], s["SourceFlipHorizontal"])
    own_writer = writer is None
    if own_writer:
        writer = PdfWriter()
    sewings = sewing_passes(s)
    wood = s["WoodExtraMm"]*MM if s["MakeWood"] else 0
    wood_y = s["WoodTopBottomExtraMm"]*MM if s["MakeWoodTopBottom"] else 0
    image_width, image_height = s["TargetWidthMm"]*MM, s["TargetHeightMm"]*MM
    art_width, art_height = image_width+2*wood, image_height+2*wood_y
    rope_left, rope_right = rope_padding(s,"LeftMm")*MM, rope_padding(s,"RightMm")*MM
    rope_top, rope_bottom = rope_padding(s,"TopMm")*MM, rope_padding(s,"BottomMm")*MM
    scroll_top = s["ScrollTopMm"]*MM if s["MakeAlScroll"] else 0
    scroll_bottom = s["ScrollBottomMm"]*MM if s["MakeAlScroll"] else 0
    tw, th = art_width+rope_left+rope_right, art_height+rope_bottom+rope_top+scroll_top+scroll_bottom
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
    image_left, image_bottom = left+rope_left+wood, bottom+rope_bottom+wood_y+scroll_bottom
    factor = (max if s["FitMode"] == "COVER" else min)(image_width/sw, image_height/sh)
    w, h = sw*factor, sh*factor
    x, y = image_left+(image_width-w)/2, image_bottom+(image_height-h)/2
    # Image expansion scales the complete artwork to the requested outer bounds.
    # Finishing geometry still uses the original finished dimensions/trim box.
    scale_x = scale_y = factor
    if s["ExpandImage"]:
        image_left -= left
        image_bottom -= bottom
        image_width += left + s["EdgeRightMm"]*MM
        image_height += bottom + s["EdgeTopMm"]*MM
        scale_x, scale_y = image_width/sw, image_height/sh
        x, y = image_left, image_bottom
        w, h = image_width, image_height
    # Keep the main artwork at page level. Wrapping this AI's existing non-isolated
    # transparency groups in another Form changes soft-mask compositing in PDFium.
    imported = copy.copy(source)
    imported.cropbox = RectangleObject(region)
    page.merge_transformed_page(imported, Transformation((scale_x, 0, 0, scale_y, x-x0*scale_x, y-y0*scale_y)))
    main_content = (f"q {image_left:.8f} {image_bottom:.8f} {image_width:.8f} {image_height:.8f} re W n\n".encode("ascii")
                    + page.get_contents().get_data() + b"\nQ\n")
    resources = page["/Resources"]
    # Extend only edge strips; the main design remains a vector at its exact scale.
    # Contain letterboxing stays white when the source aspect ratio differs.
    if not s["ExpandImage"] and (s["FitMode"] == "COVER" or (abs(w-image_width) < .1*MM and abs(h-image_height) < .1*MM)):
        # Cover uses the visible crop edges, not the parts outside the trim box.
        x0, y0, x1, y1 = max(x0, x0+(image_left-x)/factor), max(y0, y0+(image_bottom-y)/factor), min(x1, x1-(x+w-image_left-image_width)/factor), min(y1, y1-(y+h-image_bottom-image_height)/factor)
        sw, sh = x1-x0, y1-y0
        x, y, w, h = image_left, image_bottom, image_width, image_height
        band = min(.1*MM/factor, sw/4, sh/4)
        right, top = pw-x-w, ph-y-h
        # Wood margins remain white instead of repeating stretched edge pixels.
        if not wood and not sewings:
            stamp((x0, y0, x0+band, y1), (0, y, x, h))
            stamp((x1-band, y0, x1, y1), (x+w, y, right, h))
        if not wood_y and not sewings and not s["MakeAlScroll"]:
            stamp((x0, y0, x1, y0+band), (x, 0, w, y))
            stamp((x0, y1-band, x1, y1), (x, y+h, w, top))
        if not wood and not wood_y and not sewings and not s["MakeAlScroll"]:
            for sx0, sx1, tx, corner_width in [(x0, x0+band, 0, x), (x1-band, x1, x+w, right)]:
                for sy0, sy1, ty, corner_height in [(y0, y0+band, 0, y), (y1-band, y1, y+h, top)]:
                    stamp((sx0, sy0, sx1, sy1), (tx, ty, corner_width, corner_height))
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
    white_name = unused_name(states, "/NoOverprint")
    black_name = unused_name(states, "/BlackOverprint")
    cut_stroke_color = "0 0 0 1 K"
    if spot_cutline:
        cut_name = unused_name(colors, "/CutColor")
        tint = DictionaryObject({NameObject("/FunctionType"): NumberObject(2), NameObject("/Domain"): array([0, 1]),
                                 NameObject("/C0"): array([0, 0, 0, 0]), NameObject("/C1"): array([1, 0, 0, 0]), NameObject("/N"): NumberObject(1)})
        colors[cut_name] = ArrayObject([
            NameObject("/Separation"), NameObject("/"+s["SpotColorName"]), NameObject("/DeviceCMYK"), writer._add_object(tint)])
        cut_stroke_color = f"{cut_name} CS 1 SCN"
    states[white_name] = DictionaryObject({NameObject("/OP"): BooleanObject(False), NameObject("/op"): BooleanObject(False)})
    states[black_name] = DictionaryObject({NameObject("/OP"): BooleanObject(True), NameObject("/op"): BooleanObject(True), NameObject("/OPM"): NumberObject(1)})
    if s["MakeWood"]:
        extra_width = s["WoodExtraLineThicknessMm"]*MM
        design_width = s["WoodDesignLineThicknessMm"]*MM
        positions = []
        if wood > 0:
            positions += [(left+rope_left+extra_width/2, extra_width), (left+rope_left+art_width-extra_width/2, extra_width)]
        if s["MakeWoodDesignLine"]:
            offset = s["WoodDesignOffsetMm"]*MM
            positions += [(image_left-offset, design_width), (image_left+image_width+offset, design_width)]
        drawn = []
        for position, line_width in sorted(positions):
            position = max(left+rope_left+line_width/2, min(left+rope_left+art_width-line_width/2, position))
            if any(abs(position-prior) < .001 and abs(line_width-prior_width) < .001 for prior, prior_width in drawn): continue
            drawn.append((position, line_width))
            instructions.append(f"q {white_name} gs 0 0 0 1 K {line_width:.10f} w "
                                f"{position:.8f} {bottom+rope_bottom:.8f} m {position:.8f} {bottom+rope_bottom+art_height:.8f} l S Q")
    if s["MakeWoodTopBottom"]:
        extra_width = s["WoodTopBottomExtraLineThicknessMm"]*MM
        design_width = s["WoodTopBottomDesignLineThicknessMm"]*MM
        positions = []
        if wood_y > 0:
            positions += [(bottom+rope_bottom+extra_width/2, extra_width), (bottom+rope_bottom+art_height-extra_width/2, extra_width)]
        if s["MakeWoodTopBottomDesignLine"]:
            offset = s["WoodTopBottomDesignOffsetMm"]*MM
            positions += [(image_bottom-offset, design_width), (image_bottom+image_height+offset, design_width)]
        drawn = []
        for position, line_width in sorted(positions):
            position = max(bottom+rope_bottom+line_width/2, min(bottom+rope_bottom+art_height-line_width/2, position))
            if any(abs(position-prior) < .001 and abs(line_width-prior_width) < .001 for prior, prior_width in drawn): continue
            drawn.append((position, line_width))
            instructions.append(f"q {white_name} gs 0 0 0 1 K {line_width:.10f} w "
                                f"{left+rope_left:.8f} {position:.8f} m {left+rope_left+art_width:.8f} {position:.8f} l S Q")
    # Keep the vector artwork intact, adding white sewing margins and two black rectangles per pass.
    rx, ry, rw, rh = left+rope_left, bottom+rope_bottom, art_width, art_height
    for rope in sewings:
        stroke = rope["LineThicknessMm"]*MM
        instructions.append(f"q {white_name} gs 0 0 0 1 K {stroke:.10f} w {rx:.8f} {ry:.8f} {rw:.8f} {rh:.8f} re S Q")
        rx -= rope["LeftMm"]*MM
        ry -= rope["BottomMm"]*MM
        rw += (rope["LeftMm"]+rope["RightMm"])*MM
        rh += (rope["TopMm"]+rope["BottomMm"])*MM
        instructions.append(f"q {white_name} gs 0 0 0 1 K {stroke:.10f} w {rx+stroke/2:.8f} {ry+stroke/2:.8f} {rw-stroke:.8f} {rh-stroke:.8f} re S Q")
    def marker_color(hex_color):
        red, green, blue = (int(hex_color[i:i+2], 16)/255 for i in (1, 3, 5))
        black = 1-max(red, green, blue)
        cyan, magenta, yellow = (0, 0, 0) if black == 1 else tuple((1-v-black)/(1-black) for v in (red, green, blue))
        return f"{cyan:g} {magenta:g} {yellow:g} {black:g} K"

    hole_color = marker_color(s["HoleBackgroundColor"])
    dot_color = marker_color(s["HoleMarkerColor"])
    # Keep the existing black overprint; other marker colors knock out the halo.
    dot_state = black_name if s["HoleMarkerColor"].upper() == "#000000" else white_name
    for mx, my in marks:
        cx, cy = mx*MM, my*MM
        if s["HoleMarkKind"] == "DOT":
            # The halo is 2 mm at final print size. The delivery page is scaled below.
            halo_diameter = 2*MM
            center_diameter = halo_diameter*.8
            point = f"{cx:.8f} {cy:.8f} m {cx:.8f} {cy:.8f} l S"
            instructions += [f"q 1 J {white_name} gs {hole_color} {halo_diameter:.8f} w {point}",
                             f"{dot_state} gs {dot_color} {center_diameter:.8f} w {point} Q"]
            continue
        radius = 2.5*MM
        cross = f"{cx-radius:.8f} {cy:.8f} m {cx+radius:.8f} {cy:.8f} l {cx:.8f} {cy-radius:.8f} m {cx:.8f} {cy+radius:.8f} l S"
        instructions += [f"q {cx-radius:.8f} {cy-radius:.8f} {5*MM:.8f} {5*MM:.8f} re W n 0 J {white_name} gs {hole_color} {1.5*MM:.8f} w {cross}",
                         f"{dot_state} gs {dot_color} 1 w {cross} Q"]
    if s["MakeCutline"]:
        cut_left, cut_bottom = (0 if s["CutInsetsFromPage"] else left)+s["CutInsetLeftMm"]*MM, (0 if s["CutInsetsFromPage"] else bottom)+s["CutInsetBottomMm"]*MM
        cut_width = (pw if s["CutInsetsFromPage"] else tw)-(s["CutInsetLeftMm"]+s["CutInsetRightMm"])*MM
        cut_height = (ph if s["CutInsetsFromPage"] else th)-(s["CutInsetTopMm"]+s["CutInsetBottomMm"])*MM
        page.trimbox = RectangleObject([cut_left, cut_bottom, cut_left+cut_width, cut_bottom+cut_height])
        instructions.append(f"q {white_name} gs {cut_stroke_color} {s['LineWidthMm']*MM:.8f} w {cut_left:.8f} {cut_bottom:.8f} {cut_width:.8f} {cut_height:.8f} re S Q")
    content = DecodedStreamObject()
    content.set_data(main_content + ("\n".join(instructions) + "\n").encode("ascii"))
    page[NameObject("/Contents")] = writer._add_object(content)
    rip_scale = s["OutputRipScale"]
    if rip_scale != 1:
        # Scale artwork, finishing marks and page boxes together for RIP delivery.
        page.scale_by(1/rip_scale)
    page.compress_content_streams()
    pdf_width, pdf_height = pw/MM/rip_scale, ph/MM/rip_scale
    snapshot = {"schemaVersion": 1, "sourceRegionPt": list(region), "pageIndex": page_index,
                "settings": s, "markCount": len(marks), "engineVersion": "PineTreePdf/2"}
    writer.add_metadata({"/Title": f"PineTree output - {pdf_width:g}x{pdf_height:g}mm",
                         "/PineTreeRipScale": f"{rip_scale:g}",
                         "/Producer": "PineTree PDF worker", "/PineTreeGenerationSettings": json.dumps(snapshot, ensure_ascii=False)})
    if "/OutputIntents" in reader.trailer["/Root"]:
        writer._root_object[NameObject("/OutputIntents")] = reader.trailer["/Root"]["/OutputIntents"].clone(writer)
    if own_writer:
        with measure("pdf_write"):
            writer.write(destination)
        with PdfReader(destination) as check:
            if len(check.pages) != 1:
                raise ValueError("생성한 PDF의 페이지 수 검증에 실패했습니다.")
    if spot_cutline and not has_thru(page["/Resources"], spot_name=s["SpotColorName"]):
        raise ValueError("생성한 PDF의 페이지/별색 검증에 실패했습니다.")
    cutline_color_label = s["SpotColorName"] if spot_cutline else "검정(Black)"
    return {"OutputPath": str(destination), "Recipe": f"완성 {tw/MM:g}×{th/MM:g}mm / 출력 {pw/MM:g}×{ph/MM:g}mm / "
            + (f"전달 PDF {pdf_width:g}×{pdf_height:g}mm / RIP {rip_scale:g}배 / " if rip_scale != 1 else "")
            + ("맞춤" if s["FitMode"] == "CONTAIN" else "채움")
            + (f" / {cutline_color_label} 재단선 {s['LineWidthMm']:g}mm" if s["MakeCutline"] else " / 재단선 없음")
            + (f" / 족자및쫄대 상 {s['ScrollTopMm']:g}·하 {s['ScrollBottomMm']:g}mm" if s["MakeAlScroll"] else "")
            + (f" / 좌우각목 여분 각 {s['WoodExtraMm']:g}mm" if s["MakeWood"] else "")
            + (f" / 상하각목 여분 각 {s['WoodTopBottomExtraMm']:g}mm" if s["MakeWoodTopBottom"] else "")
            + (f" / 사방줄미싱 {len(s['RopeSewPasses'])}건" if s["RopeSewPasses"] else "")
            + (f" / 사방시접미싱 {len(s['HemSewPasses'])}건" if s["HemSewPasses"] else "")
            + f" / 타공 {'십자마크' if s['HoleMarkKind'] == 'CROSS' else '점'} {len(marks)}개", "Scale": factor, "FitMode": s["FitMode"].lower(), "PageCount": 1,
            "OutputWidthMm": pdf_width, "OutputHeightMm": pdf_height, "OutputRipScale": rip_scale, "MarkCount": len(marks)}


def generate_pages(source, reader, details, destination, settings):
    writer = PdfWriter()
    results = []
    for index in range(len(reader.pages)):
        try:
            with measure("source_analysis"):
                info = inspect_file(source, reader, details, index)
                current = dict(settings)
                if current.get("RequireSourceCutline") and not detect_cutline(reader.pages[index], current.get("SpotColorName"))["HasCutline"]:
                    raise ValueError("첫 대지에서 인식한 원본 재단선이 이 페이지에는 없습니다. 원본의 재단선을 확인해주세요.")
            width, height = info["SourceWidthMm"], info["SourceHeightMm"]
            if current.get("SourceRotationDegreesClockwise", 0) in (90, 270):
                width, height = height, width
            mode = current.get("SourceSizingMode", "FIXED_SIZE")
            if mode in ("ORIGINAL", "SCALE"):
                rate = float(current.get("SourceScale", 1)) if mode == "SCALE" else 1
                if not math.isfinite(rate) or rate <= 0:
                    raise ValueError("축척은 양수여야 합니다.")
                if current.get("RoundSourceDimensionsBeforeScale"):
                    width, height = math.floor(width+.5), math.floor(height+.5)
                current.update(TargetWidthMm=width * rate, TargetHeightMm=height * rate)
            elif mode == "FIXED_SIZE":
                current["TargetHeightMm"] = current["TargetWidthMm"] * height / width
            else:
                raise ValueError("지원하지 않는 크기 설정입니다.")
            results.append(generate_pdf(reader, index, info["Region"], destination, current, writer))
        except Exception as error:
            raise ValueError(f"대지 {index + 1}: {error}") from error
    writer.add_metadata({"/PineTreePageResults": json.dumps(results, ensure_ascii=False)})
    with measure("pdf_write"):
        writer.write(destination)
    with PdfReader(destination) as check:
        if len(check.pages) != len(reader.pages):
            raise ValueError("생성한 PDF의 페이지 수 검증에 실패했습니다.")
    return {**results[0], "PageCount": len(results), "MarkCount": sum(r["MarkCount"] for r in results),
            "Recipe": f"{len(results)}페이지 / " + results[0]["Recipe"]}


def execute(request):
    source = Path(request["SourcePath"]).resolve()
    with source_stream_limit(source), tempfile.TemporaryDirectory(prefix="pinetree_pdf_", dir=request.get("WorkDirectory")) as folder:
        work = Path(folder)
        with measure("source_analysis"):
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
            with measure("preview_render"):
                render_preview(normalized, reader.pages[index], info["Region"], destination,
                               request.get("MaxPixels", 1600), request.get("FullCanvas", False), request.get("ViewportRegion"))
            return {**info, "PreviewPath": str(destination)}
        if operation == "generate":
            settings = request.get("Settings") or {}
            with measure("pdf_generation"):
                if settings.get("GenerateAllPages") and source.suffix.lower() == ".ai" and len(reader.pages) > 1:
                    return {**info, **generate_pages(source, reader, details, destination, settings)}
                return {**info, **generate_pdf(reader, index, info["Region"], destination, request.get("Settings"))}
        if operation == "extract":
            writer = PdfWriter()
            writer.add_page(reader.pages[index])
            if "/OutputIntents" in reader.trailer["/Root"]:
                writer._root_object[NameObject("/OutputIntents")] = reader.trailer["/Root"]["/OutputIntents"].clone(writer)
            with measure("pdf_write"):
                writer.write(destination)
            return {**info, "OutputPath": str(destination), "PageCount": 1}
        raise ValueError("지원하지 않는 파일 처리 작업입니다.")


def main():
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    request_path, response_path = map(Path, sys.argv[1:3])
    request = None
    try:
        request = json.loads(request_path.read_text(encoding="utf-8-sig"))
        response = {"Success": True, **execute(request)}
    except Exception as error:
        traceback.print_exc(file=sys.stderr)
        response = {"Success": False, "Error": str(error)}
    response_path.write_text(json.dumps(response, ensure_ascii=False), encoding="utf-8")
    if _TIMINGS is not None and isinstance(request, dict) and request.get("RequestId"):
        timing = {"requestId": request["RequestId"],
                  "elapsedMs": {stage: round(ns / 1_000_000, 3) for stage, ns in _TIMINGS.items()}}
        print("\nPINETREE_PDF_TIMING " + json.dumps(timing))
    return 0 if response["Success"] else 1


if __name__ == "__main__":
    raise SystemExit(main())

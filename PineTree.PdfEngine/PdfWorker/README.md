# 주문파일 미리보기와 샘플 출력 PDF

`DialogOrderInfoEdit`의 기존 Designer 배치는 유지합니다.

- `btnSelectFile`: PDF/AI/EPS/JPG 선택. `tbSourceFile`에 전체 경로, `tbFileType`에 확장자와 설명, `tbFileInfo`에 파일 크기·페이지/mm 또는 픽셀/DPI·색상 모드를 비동기로 표시합니다. 긴 정보는 툴팁으로도 표시합니다.
- `btnPreview1`: 별도 PictureBox 창에 원본 미리보기. 페이지 이동, 전체 맞춤, 확대/축소, AI 전체 캔버스 보기를 지원합니다.
- `DialogOrderItem.btnGenerate`: 주문파일 탭의 직접 입력값으로 첫 페이지의 출력용 PDF를 비동기 생성합니다. `inputTextBox2`에 완료된 PDF 경로를 반영합니다. 상세 해석과 기본값은 `ORDER_ITEM_PDF.md`를 참고합니다.
- `DialogOrderInfoEdit.btnGenerate`: 기존 PET 고정 샘플 호출을 유지합니다.
- `btnPreview2`: 생성하거나 기존에 저장된 출력 PDF를 같은 미리보기 창으로 엽니다.
- 파일 경로는 기존 주문의 [확인] 저장 흐름에 그대로 전달합니다. 이번 기능은 DB 테이블을 변경하지 않습니다.
- 생성 도중 [취소] 또는 창 닫기는 작업 프로세스를 취소합니다. 임시 결과를 먼저 만들고 완료 시 최종 파일로 교체하므로 실패·취소 시 기존 출력은 보존합니다. 원본 덮어쓰기를 차단합니다.

## 샘플 조건

- 완성 600×1800mm, 사방 2mm 도련, PDF 페이지 604×1804mm.
- 사각 재단선: `thru` 별색 100%, 선 두께 0.18mm, 채움 없음.
- 십자마크 4개: 중심이 재단 경계에서 각각 20mm 안쪽, 십자 길이 5mm.
- 십자: 흰 받침 1.5mm + K100 검정 선 1pt, 검정 오버프린트.
- AI는 캔버스보다 작은 큰 사각 클립/배경을 기준으로 디자인 영역을 추정합니다. PET 60×180mm, AL족자 904×1504mm, 현수막 700×1000mm를 확인했습니다. 임의 AI의 선택 영역을 완전히 자동 판정하는 일반 편집기는 아닙니다.
- 종횡비는 유지합니다. 1:3 디자인은 가장자리 벡터 영역을 연장해 도련을 채웁니다. 다른 비율은 중앙 맞춤과 흰 여백을 사용합니다.
- `DialogOrderItem`은 직접 입력 조건을 적용하며 후가공 선택 목록은 아직 실행하지 않습니다. 조건은 출력 PDF 메타데이터에 기록합니다. 이번 범위에서 DB 테이블이나 조건 저장 모델은 변경하지 않습니다.
- PDF의 첫 페이지를 사용합니다. 미리보기의 페이지 이동이 생성 페이지 선택을 바꾸지는 않습니다.
- 원본의 벡터/투명도/색상 리소스를 보존하고, 미리보기 PNG를 출력 원본으로 사용하지 않습니다. JPG는 래스터 원본의 한계가 유지됩니다.
- 기존 `thru` 별색 경로가 있으면 중복 재단선 생성을 막고 안내합니다.

## 지원 범위

- PDF: 비암호화 PDF.
- AI: PDF 호환 AI 및 PostScript 기반 구형 AI. PDF/PostScript 데이터가 없는 최신 비호환 AI는 Illustrator에서 PDF 호환 저장/내보내기가 필요하며 현재 엔진이 직접 해석하지 않습니다.
- EPS: 동봉 Ghostscript DLL로 PDF 변환. 글꼴 누락 등 원본 문제는 변환 결과에 영향을 줄 수 있습니다.
- JPG/JPEG: EXIF 방향을 적용해 미리보기/PDF 배치. 물리적 출력 크기는 JPG DPI가 아니라 입력한 완성 규격을 사용합니다.
- 처리 제한 시간은 작업당 3분입니다. 원본 선택 중 결과가 늦게 도착하더라도 최신 파일의 표시를 덮어쓰지 않습니다.

## 실행 및 배포

PDF 처리 코드는 `PineTree.PdfEngine` 프로젝트에서 관리합니다. `pdf_worker.py`와 `runtime`은 빌드 시 ZIP 리소스로 DLL에 내장되며, PineTree의 Costura가 DLL을 EXE에 포함합니다. 사용자에게 별도 PdfWorker 폴더를 배포하지 않습니다.

PDF 기능을 처음 사용할 때 사용자 캐시 폴더에 자동으로 풀어 실행합니다. 패키지 해시, 파일별 무결성 검사, 동시 준비 잠금, 캐시 복구와 설정은 상위 `../README.md`를 참고합니다.

Windows x64 Python 3.12 런타임이며 특정 사용자나 Codex 설치 경로에 의존하지 않습니다. 개발 환경의 `PdfWorker/runtime`은 약 87MiB이고, 빌드 산출물에는 압축한 리소스로 포함됩니다.

재구성하려면 Windows x64 Python 3.12에 `pypdf`, `pypdfium2`, `Pillow`를 설치하고 다음을 실행합니다. 검증 버전은 pypdf 6.10.0 / pypdfium2 5.14.0 / Pillow 12.3.0입니다.

```text
python build_runtime.py <Python3.12 설치 폴더> <ghostscript.nativeassets.10.8.0.nupkg>
```

Ghostscript 네이티브 패키지 다운로드 위치:
https://api.nuget.org/v3-flatcontainer/ghostscript.nativeassets/10.8.0/ghostscript.nativeassets.10.8.0.nupkg

선택 설정은 PineTree.exe.config의 appSettings에 `PdfPythonPath`, `PdfGhostscriptDllPath`, `PdfEngineCacheDirectory`로 지정할 수 있습니다. 기본값은 자동으로 준비한 캐시 경로입니다.

## 라이선스

Python 및 Python 패키지의 라이선스 파일을 runtime에 함께 복사했습니다. EPS 검증에는 Ghostscript 10.08.0 AGPL 배포 바이너리를 사용했습니다. Ghostscript를 상용 제품과 배포할 때는 AGPL 조건에 맞는 배포인지 검토하거나 Artifex 상용 라이선스를 확보해야 합니다. 다른 라이선스의 변환 엔진으로 대체하는 것도 가능합니다.

- https://www.ghostscript.com/releases/gsdnld.html
- https://artifex.com/licensing/
- runtime/ghostscript/LICENSE.md

## 검증

- `Tests/PdfSettingsRegression.py`: 직접 입력 조건의 비대칭 여유, 맞춤/채움 렌더, 별색명/두께, 체크 해제, 네 모서리/최대 간격 배치, 잘못된 입력, 네 입력 형식, 추가 AI 샘플의 영역 인식 등 8개 시나리오.
- `PineTree/Tests/OrderItemPdfRegression.cs`: 실제 주문품목 대화상자의 빈값 기본 처리, 컨트롤 매핑, 개수 계산, 비동기 생성, 처리 중 잠금, 결과 경로·미리보기, 조건 변경 후 경로 무효화 검사. DB에 접속하지 않습니다.

- `Tests/PdfWorkerRegression.py`: 네 형식의 속성·미리보기, EPS 글꼴/한글 경로, 구형 AI, 생성 geometry/별색/십자/벡터 도련, 기준 PDF와 본문 렌더 비교, 원본 보존, 중복 재단선 차단, 원본 색상/투명도 리소스 이름 보존 등 13개 시나리오.
- `PineTree/Tests/OrderDocumentRegression.cs`: 실제 C# 프로세스 통신과 비동기 생성, 취소 시 기존 파일 보존, 주문 대화상자의 필드 반영·기존 저장 모델 연결, PictureBox 표시 등 13개 검사. DB에는 접속하지 않습니다.
- UI는 프로그램으로 컨트롤/바인딩을 검증했습니다. 실제 사용자의 파일 선택·저장 대화상자 클릭 시나리오 및 RIP/실물 출력 검수는 별도입니다.
- 기존 `OrderItemDialogRegression.ps1`도 통과했습니다. 주문품목 선택·면적/수량·부가세 계산·기존 저장 모델 연결을 유지했습니다.

# 현수막 AI 다중 PDF 호환 페이지 처리

## 기존 구조와 변경

- `UcOrder현수막.LoadSourceFileAsync` / `BeginRestoredPreview` → `OrderDocumentService` → 별도 Python worker 프로세스 순서다. PDF 호환 AI는 pypdf로 직접 읽으며 PDFium으로 화면을 렌더링한다. 기존 EPS/PostScript 변환 경로는 유지했다. 새 Ghostscript 의존성은 추가하지 않았다.
- 기존에는 원본 미리보기와 출력 생성이 페이지 0으로 고정되어 있었다. `PageCount`로 원본 탭을 생성하고 완료된 Bitmap 최대 4개를 재사용한다. 초기 다음 3페이지는 백그라운드에서 순서대로 준비하며, 동일 페이지의 진행 중 작업도 중복 생성하지 않는다. 탭 전환은 작업을 취소하지 않고, 파일 교체·종료 시 취소 및 리소스 정리를 수행한다. 관리자 권한 Shell 드롭도 새 탭에 연결했다.
- `ReadPdfGenerationSettings`는 현수막 AI에만 `GenerateAllPages`를 설정한다. 실제 다중 페이지 AI일 때만 새 생성 루프를 사용하며 단일 AI와 다른 형식의 기존 생성 경로는 유지한다.
- `generate_pages`는 페이지 순서대로 기존 `generate_pdf`를 공유 writer에 호출한다. 후가공 설정은 공통이고 각 페이지의 영역·크기·회전을 반영해 기하를 다시 계산한다. 실패하면 해당 대지 번호를 포함한 오류를 반환하고 최종 파일을 저장하지 않는다.
- 원본/축척 모드는 각 페이지의 인식된 디자인 크기에 배율을 적용한다. 가로 크기 지정 모드는 공통 가로와 각 페이지의 비율로 세로를 계산한다(기존 현수막 가로 지정 UI와 동일). 첫 페이지 기준 주문 치수·수량·금액 데이터 구조는 변경하지 않았다.
- 첫 대지에서 원본 재단선을 인식해 생성을 생략하는 경우 다른 페이지에도 같은 별색 재단선이 있는지 검증한다. 혼합된 원본의 누락을 조용히 건너뛰지 않는다.
- `DialogDocumentPreview.UpdatePages` / `SelectPage`는 다중 페이지 문서에 결과 탭과 각 탭의 저장 버튼을 만든다. 기존 확대·맞춤·스크롤·상세 영역 렌더러를 재사용하며 페이지/전체 캔버스 모드별 Bitmap 최대 4개를 캐시한다. 다음 페이지는 미리 준비하고, 탭 왕복 시 준비된 이미지와 진행 중 작업을 재사용한다. 원본/결과 모두 문서당 렌더링 worker는 하나씩 실행해 빠른 탭 전환에 따른 동시 메모리 사용 증가를 제한한다.
- 상단 **파일 저장**은 생성된 전체 PDF를 그대로 복사한다. 각 탭의 **대지 N 저장**은 `ExtractPageAsync` → worker `extract`로 해당 결과 페이지만 추출한다. 저장 단계에서 후가공을 재실행하거나 비트맵으로 변환하지 않는다. 두 저장 경로 모두 임시 파일과 원자적 교체를 사용한다.

## 수정 파일

1. `PineTree/UserControlsOrder/UcOrder현수막.cs`: 원본 탭, 지연 미리보기, 취소/정리, AI 전용 설정, 관리자 드롭.
2. `PineTree/Dialogs/DialogDocumentPreview.cs`: 결과 탭, 페이지별 저장, 기존 뷰포트 재사용.
3. `PineTree.PdfEngine/OrderDocumentService.cs`: 페이지 생성 옵션, 페이지 추출 API, 기존 atomic 저장 재사용.
4. `PineTree.PdfEngine/PdfWorker/pdf_worker.py`: 공유 writer, 다중 페이지 생성, 페이지별 검증/축척, 결과 페이지 추출.
5. `PineTree.PdfEngine/Tests/PdfMultiArtboardRegression.py`: 엔진 회귀 테스트 7개.
6. `PineTree/Tests/MultiArtboardRegression.cs`, `RunMultiArtboardRegression.ps1`: 실제 WinForms 통합 검증.

기존 작업 트리에 있던 다른 수정은 유지했다.

## 검증 결과 (2026-11-05)

- Debug / AnyCPU 빌드 성공. 기존의 사용하지 않는 필드·이벤트 관련 경고가 있다.
- Python 전체 회귀 테스트 65개 통과(229.154초). 그중 신규 다중 대지 테스트 5개 포함. 이후 추가한 원본 재단선 누락 및 단일 AI 출력 크기 유지 테스트 2개도 각각 통과했다.
- 실제 `Yokel.ai`: 원본 2페이지 각각 렌더링, 두 페이지의 약 293.60 × 248.60mm 크기 확인, 공통 재단선·타공 적용 후 2페이지 생성 및 각각 렌더링 성공. 실행 전후 SHA-256 일치. 별도 신규 테스트 첫 실행은 5개 합계 135.522초였다.
- 합성 AI: 서로 다른 크기 및 90도 회전, 원본·배율·고정 가로, 페이지 순서(빨강/파랑), 두 페이지 모두 재단 별색 및 타공, 적용 불가한 두 번째 페이지의 명시적 오류 검증 통과.
- 개별 추출본과 통합본의 해당 페이지는 PDFium 렌더링 픽셀 비교에서 일치했다.
- 실제 WinForms 통합 검사 12개 통과: 원본 2탭, 페이지 전환/재방문, 비선택 이미지 해제, 다중 결과 탭과 각 저장 버튼, 결과 2페이지 렌더링, 독립 저장, 빠른 탭 전환 후 종료, 새 단일 파일 로드 시 탭 정리.
- WinForms에서 저장한 전체 PDF는 2페이지, 개별 저장 PDF는 1페이지. 개별 저장 페이지의 content stream은 통합본 2페이지와 일치했다.
- 로그: 프로젝트 루트 `tmp/multi-artboard-engine-tests.log`, `tmp/multi-artboard-ui-tests.log`, `tmp/multi-artboard-build.log`. 작은 UI 테스트 출력: `tmp/multi-artboard-ui/output.pdf`, `saved-all.pdf`, `saved-page2.pdf`.

재실행:

```powershell
& .\PineTree.PdfEngine\PdfWorker\runtime\python.exe -m unittest discover -s .\PineTree.PdfEngine\Tests -p 'Pdf*Regression.py' -v
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\PineTree\Tests\RunMultiArtboardRegression.ps1
```

위 명령은 `3.개발`에서 실행한다. UI 테스트는 먼저 최신 Debug 빌드를 완료한 후 실행한다.

## 남은 제약과 수동 검증

- 대지는 Illustrator 메타데이터가 아니라 PDF 호환 페이지를 의미한다. PDF 비호환 AI는 기존 안내/변환 정책을 따른다. 1:10 등 실제 출력 축척은 자동 추정하지 않는다.
- 기존 벡터 콘텐츠, 리소스, 투명도 그룹 및 OutputIntents를 재사용한다. 테스트에서 생성 재단 별색과 개별 추출의 렌더링 일치를 확인했으나 모든 원본 별색·ICC·투명도·오버프린트·RIP 결과의 무손실 보존을 보증하지 않는다. Foxit 직접 확인과 실물 출력 비교는 미실행이다.
- 기존 AI 디자인 영역 자동 인식 정책을 유지한다. MediaBox/CropBox와 디자인 영역 치수가 다를 수 있다. 주문 치수/금액은 첫 페이지 기준 데이터 구조를 유지하며 페이지별 주문/가격 계산 기능은 이번 범위에 포함하지 않았다.
- 화면 Bitmap은 원본/결과 각각 최대 4개로 제한하지만 pypdf는 큰 원본 스트림을 메모리에 읽을 수 있다. 정밀한 최고 메모리 측정, 장시간 반복 로드 누수 및 OOM 강제 테스트는 미실행이다. 기존 작업당 3분 제한과 파일 교체/창 종료 시 worker 종료는 유지한다. 캐시에서 밀려난 페이지와 아직 준비되지 않은 페이지는 최초 렌더링 대기가 필요하다.
- PDF 페이지에 들어 있지 않은 Illustrator 편집용 전용 객체/메타데이터와 일부 주석 등은 기존 출력 엔진과 마찬가지로 출력용 벡터 구성의 대상이 아니다.

## 사용자 캡처 재현 후 수정 (2026-11-05)

이전 검증은 페이지 수와 렌더링 성공에 집중하여 첫 페이지가 내부 사각형으로 잘려 표시되는 문제와 탭마다 다시 렌더링하는 UX를 놓쳤다. 위 구조 설명은 후속 수정을 반영한 현재 상태이며, 앞의 최초 검증 기록은 당시 실행 기록이다.

- **일부 영역만 표시**: 기존 AI 디자인 영역 추정이 `Yokel.ai` 첫 대지의 사과 타일 클리핑을 선택했다. 다중 페이지 AI는 PDF 페이지 CropBox 전체를 사용하도록 변경했다. 단일 AI의 기존 대형 캔버스 디자인 추정은 유지한다. 미리보기뿐 아니라 출력 치수와 실제 생성 영역에도 같은 수정이 적용된다.
- **반복 생성/대기**: 원본·결과 각각 최대 4개 LRU 캐시와 진행 중 작업 공유를 추가했다. 원본 초기 로드는 메타데이터를 포함하는 preview 호출 한 번으로 처리하여 기존 inspect→preview 중복 읽기를 제거했다. 원본은 초기 다음 페이지들, 결과는 다음 페이지를 백그라운드로 준비한다.
- **탭 전환 취소 throw**: 탭 이동에서는 worker를 취소하지 않는다. 새 `PreviewPageAsync`는 문서 교체/종료 취소를 `IsCanceled`로 반환하며 `ShouldStop`에서 예외를 던지지 않는다. 다른 생성·저장 경로의 기존 취소 정책은 유지한다.
- **작업 수 제한**: 빠른 탭 전환 시에도 문서별 SemaphoreSlim으로 worker를 직렬 실행한다. 취소된 대기 작업은 예외 없이 종료한다.
- 실제 첫 대지의 8개 영역이 원본/후가공 결과 모두 표시됨을 PNG로 직접 확인했다. 기본 미리보기와 전체 캔버스의 픽셀 비교도 일치했다. 원본 인식 치수는 약 293.60×248.60mm다. 확인 이미지: 프로젝트 루트 `tmp/multi-artboard-fix/yokel0.png`, `yokel1.png`, `result0.png`, `result1.png`.
- WinForms 22개 검사 통과: 초기 표시, 로딩 중 첫 탭 복귀, 진행 중 작업 재사용, 캐시 Bitmap 참조 유지와 즉시 전환, 전체/개별 저장, 6페이지 입력에서 4개 캐시 제한, 파일 교체와 창 닫기 시 first-chance `OperationCanceledException` 0건 확인.
- 사용자 디버거가 기본 빌드의 PDB를 사용 중이므로 `OutputPath=bin\MultiArtboardFix\`, `IntermediateOutputPath=obj\MultiArtboardFix\`로 격리 빌드하여 검증했다. 현재 실행 중인 예전 프로세스에는 자동 적용되지 않으므로 재빌드 후 새 미리보기를 생성해야 한다.
- 후속 로그: 프로젝트 루트 `tmp/multi-artboard-fix-engine-tests.log`, `tmp/multi-artboard-fix-ui-tests.log`, `tmp/multi-artboard-fix-build.log`.
- 최종 확인: 격리 빌드 성공, 엔진 회귀 68개 통과(400.531초), worker 직렬 처리까지 반영한 WinForms 22개 및 저장 PDF 검증 통과. 원본 SHA-256 유지. Foxit/실물 출력 검증은 여전히 미실행이다.

# PineTree.PdfEngine

.NET Framework 4.7.1 클래스 라이브러리입니다. `OrderDocumentService`, `PdfGenerationSettings`, `OrderDocumentResult`의 네임스페이스는 `PineTree.PdfEngine`입니다. WinForms 화면은 PineTree에 두고 PDF 처리와 모델, Python 실행은 이 프로젝트에서 담당합니다.

## 빌드와 배포

- 필수 프레임워크 참조와 런타임 내장 빌드 설정은 `Directory.Build.targets`에서 관리합니다. MSBuild가 이 파일을 자동으로 가져오므로 프로젝트 파일 저장과 독립적으로 유지됩니다. 이 파일도 소스와 함께 보관해야 합니다.
- `PdfWorker/pdf_worker.py` 및 `PdfWorker/runtime` 전체를 이 프로젝트에서 관리합니다. Python 런타임 재구성은 `PdfWorker/build_runtime.py`를 사용합니다.
- MSBuild가 `Build/PackPdfWorker.ps1`을 실행해 Python·Ghostscript 런타임 ZIP 및 SHA-256 파일 목록을 `obj/<Configuration>`에 생성합니다. DLL에는 런타임 리소스와 `pdf_worker.py` 리소스가 각각 들어갑니다. 빌드 머신에는 구성된 `runtime` 폴더가 필요합니다.
- 필수 런타임 파일이 없으면 빌드는 실패합니다. `.pyc`와 `__pycache__`는 포함하지 않으며 라이선스 파일은 포함합니다. 내용이 같으면 기존 패키지를 재사용합니다.
- PineTree의 Costura가 PdfEngine을 애플리케이션 어셈블리에 내장합니다. 기존 단일 EXE 호스트와 현재 런처가 로드하는 `PineTree.dll`에서 모두 `Tools/PdfWorker`, 외부 Python 설치, 별도 PdfEngine DLL 없이 오프라인으로 PDF를 처리할 수 있습니다.
- 대상 환경은 Windows x64 및 호환되는 .NET Framework입니다. 메인 앱이 32비트로 실행되어도 별도 x64 Python 프로세스를 사용합니다.

## 실행과 캐시

기본 루트는 `%LOCALAPPDATA%/PineTree/PdfEngine`입니다. `runtimes/<런타임 파일 목록의 SHA-256>`에는 Python·Ghostscript가, `workers/<스크립트의 SHA-256>`에는 `pdf_worker.py`가 들어갑니다. 스크립트만 바뀌면 런타임을 검증해 재사용합니다. 파일별 SHA-256을 확인한 뒤 임시 폴더를 최종 폴더로 이동합니다. 같은 프로세스의 요청은 SemaphoreSlim으로, 여러 프로그램의 동시 준비는 파일 잠금으로 조정합니다. 준비 중 취소 시 미완성 폴더를 정리합니다.

프로그램 프로세스에서 처음 사용할 때 기존 캐시의 파일을 검증하고, 손상되거나 누락되었으면 복원합니다. 이후 같은 프로세스에서는 준비된 경로를 재사용합니다. 구형 배포본은 기존 루트의 `<64자리 해시>` 경로를 계속 사용합니다. 구형 캐시의 목록 및 보존 후보는 `Build/Get-LegacyPdfCacheCandidates.ps1`에서 읽기 전용으로 확인할 수 있습니다. 실제 정리는 사용하는 구형 배포본을 확인하고 관련 앱과 Worker가 모두 종료된 상태에서만 진행합니다.

Python은 기존 요청/응답 JSON 프로토콜, 최대 3분 제한, 취소 시 프로세스 종료, 성공 후 출력 파일 교체 방식을 유지합니다. 런타임 캐시는 재사용하고 작업별 임시 파일은 매번 제거합니다.

필요한 경우 호스트 EXE의 appSettings에서 다음을 지정할 수 있습니다. 기본 배포에는 설정 파일이 필요하지 않습니다.

| 키 | 용도 |
|---|---|
| `PdfEngineCacheDirectory` | 캐시 위치 재정의 (테스트 또는 관리 환경용) |
| `PdfPythonPath` | 기존 외부 Python 실행 파일 재정의 |
| `PdfGhostscriptDllPath` | Ghostscript DLL 경로 재정의. 절대 경로를 사용하거나 호스트 기준 상대 경로로 지정 |

### 요청별 성능 계측

앱을 시작하는 셸에서 `PINETREE_PDF_TIMING_DIRECTORY`에 쓰기 가능한 로그 디렉터리를 설정하면 `pdf-timing-<PID>.jsonl`을 생성합니다. 요청 ID가 같은 C# `host`와 Python `worker` 구간이 한 줄에 기록됩니다. 값은 ms입니다. `runtime_prepare`는 설치·검증 및 대기를 포함하고, `python_process`는 프로세스 시작부터 종료까지입니다. Python은 `module_init`, `source_analysis`, `preview_render`, `pdf_generation`, `pdf_write`를 작업에 따라 기록합니다. `final_pdf_save`는 C#의 최종 복사·플러시·교체입니다. 구간에 포함 관계가 있으므로 합산할 때 중복에 주의합니다. 결과 캐시 적중 시 `source_cache_hit`가 기록됩니다. 환경 변수를 지정하지 않으면 계측 로그를 기록하지 않습니다.

DLL 내장은 Python 소스를 암호화하거나 네이티브 코드로 변환하는 기능이 아닙니다. Ghostscript 등 의존성의 기존 라이선스 검토 사항은 `PdfWorker/README.md`를 참고합니다.

## 검증

### 주문 원본 미리보기

현수막·페트 주문 화면은 `InspectAsync`로 파일 정보와 치수를 먼저 반영하고,
기존 대지 로더의 `PreviewPageAsync`로 이미지를 백그라운드에서 생성합니다.
이미지를 기다리는 동안 변경한 수량·후가공 설정은 미리보기 완료 후에도 유지합니다.

분석 결과와 전체 대지 미리보기는 프로세스 내 공용 LRU 캐시를 사용합니다.
원본의 절대 경로·파일 크기·UTC 수정/생성 시간·대지 번호로 원본을 구분하고,
미리보기 해상도와 전체 캔버스 여부도 구분합니다. 원본이 바뀌면 재분석합니다.
캐시는 최대 64항목, 메타데이터 및 압축 PNG 데이터 합계 32MiB로 제한하며
프로그램 종료 시 사라집니다. 각 호출은 별도 Bitmap을 소유하므로 다른 화면의
이미지 해제에 영향을 받지 않습니다. 확대 영역 미리보기와 출력 PDF 생성은 캐시하지 않습니다.

`Tests/SourcePreviewCacheRegression.cs`는 캐시 적중 시 작업 프로세스가 필요 없는지,
이미지 소유권·취소·원본 변경·해상도별 구분을 검증합니다. 실행 인수는 테스트 폴더,
해당 실행만 사용하는 `PdfEngineCacheDirectory` 폴더, 선택적인 대용량 AI 경로입니다.
검사 중 테스트 캐시의 Python 실행 파일을 잠시 이동하므로 전용 캐시를 사용해야 합니다.
주문 화면 검사는 `PineTree/Tests/RunWorkingArtboardRegression.ps1`을
`-EditorKind Banner` 및 `-EditorKind Pet`으로 실행하며,
정보 우선 표시와 렌더링 중 수정한 설정 유지도 확인합니다.

엔진 프로젝트 폴더에서:

```powershell
& .\PdfWorker\runtime\python.exe -B .\Tests\PdfSettingsRegression.py
& .\PdfWorker\runtime\python.exe -B .\Tests\PdfWorkerRegression.py
```

PineTree의 `Tests/OrderDocumentRegression.cs`, `Tests/OrderItemPdfRegression.cs`는 새 DLL 네임스페이스를 사용합니다. 컴파일 시 PineTree.exe 외에 PineTree.PdfEngine.dll도 참조합니다.

`Tests/PdfEngineDeploymentRegression.cs`는 엔진 DLL을 직접 참조하지 않는 검사 호스트입니다. 애플리케이션 어셈블리의 모듈 초기화 후 Costura 로드 및 외부 PdfWorker 없이 조회·미리보기·생성·취소를 검증합니다. 선택 인수 `inspect`는 캐시 복구 및 동시 실행 검사에 사용합니다.

```powershell
& .\Tests\Test-Deployment.ps1 -ApplicationPath ..\PineTree\bin\Release\PineTree.dll -SourcePath <검증용원본AI경로>
```

배포 검사는 격리 폴더에 지정한 호스트 어셈블리만 복사하고 테스트 실행기를 별도로 생성합니다. 검증 결과는 `TestResults`에, 검증용 캐시는 작업폴더의 `tmp/pdf-cache-*`에 남깁니다. 최초 준비 취소, 두 프로세스 동시 준비, 메모리에서 DLL 로드, 미리보기·생성, EPS 변환, 취소 시 출력 보존 및 손상·누락 캐시 복구를 확인합니다. `-LegacyApplicationPath`는 구형 EXE 공존을, `-UpdatedEnginePath`는 스크립트만 바꿔 빌드한 엔진으로 런타임 재사용을 검사합니다.

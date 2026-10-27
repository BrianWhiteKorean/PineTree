# PineTree.PdfEngine

.NET Framework 4.7.1 클래스 라이브러리입니다. `OrderDocumentService`, `PdfGenerationSettings`, `OrderDocumentResult`의 네임스페이스는 `PineTree.PdfEngine`입니다. WinForms 화면은 PineTree에 두고 PDF 처리와 모델, Python 실행은 이 프로젝트에서 담당합니다.

## 빌드와 배포

- 필수 프레임워크 참조와 런타임 내장 빌드 설정은 `Directory.Build.targets`에서 관리합니다. MSBuild가 이 파일을 자동으로 가져오므로 프로젝트 파일 저장과 독립적으로 유지됩니다. 이 파일도 소스와 함께 보관해야 합니다.
- `PdfWorker/pdf_worker.py` 및 `PdfWorker/runtime` 전체를 이 프로젝트에서 관리합니다. Python 런타임 재구성은 `PdfWorker/build_runtime.py`를 사용합니다.
- MSBuild가 `Build/PackPdfWorker.ps1`을 실행하여 ZIP과 SHA-256 파일 목록을 `obj/<Configuration>`에 생성하고 DLL에 내장합니다. 별도의 Python 설치나 수동 ZIP 생성은 빌드에 필요하지 않습니다. 단, 개발 머신에는 재구성된 runtime 폴더가 있어야 합니다.
- 필수 런타임 파일이 없으면 빌드는 실패합니다. `.pyc`와 `__pycache__`는 포함하지 않으며 라이선스 파일은 포함합니다. 내용이 같으면 기존 패키지를 재사용합니다.
- PineTree의 기존 Costura가 프로젝트 참조 DLL을 EXE에 내장합니다. 사용자에게 `Tools/PdfWorker`, Python 설치 파일, 별도 PdfEngine DLL을 전달할 필요가 없습니다.
- 대상 환경은 Windows x64 및 호환되는 .NET Framework입니다. 메인 앱이 32비트로 실행되어도 별도 x64 Python 프로세스를 사용합니다.

## 실행과 캐시

최초 PDF 요청 시 `%LOCALAPPDATA%/PineTree/PdfEngine/<manifest SHA-256>`에 내장 패키지를 풉니다. 파일별 SHA-256을 확인한 뒤 임시 폴더를 최종 폴더로 이동합니다. 같은 프로세스의 요청은 SemaphoreSlim으로, 여러 프로그램의 동시 준비는 파일 잠금으로 조정합니다. 준비 중 취소 시 미완성 폴더는 제거합니다.

프로그램 프로세스에서 처음 사용할 때 기존 캐시의 모든 파일을 검증하고, 손상되거나 누락되었으면 복원합니다. 이후 같은 프로세스에서는 준비된 경로를 재사용합니다. 패키지 내용이 바뀌면 새 해시 폴더를 사용하며 실행 중인 다른 버전의 캐시는 삭제하지 않습니다. 실행 중인 Python을 보호하기 위해 캐시 정리는 프로그램을 종료한 뒤 수행합니다.

Python은 기존 요청/응답 JSON 프로토콜, 최대 3분 제한, 취소 시 프로세스 종료, 성공 후 출력 파일 교체 방식을 유지합니다. 런타임 캐시는 재사용하고 작업별 임시 파일은 매번 제거합니다.

필요한 경우 호스트 EXE의 appSettings에서 다음을 지정할 수 있습니다. 기본 배포에는 설정 파일이 필요하지 않습니다.

| 키 | 용도 |
|---|---|
| `PdfEngineCacheDirectory` | 캐시 위치 재정의 (테스트 또는 관리 환경용) |
| `PdfPythonPath` | 기존 외부 Python 실행 파일 재정의 |
| `PdfGhostscriptDllPath` | 기존 Ghostscript DLL 경로 재정의 |

DLL 내장은 Python 소스를 암호화하거나 네이티브 코드로 변환하는 기능이 아닙니다. Ghostscript 등 의존성의 기존 라이선스 검토 사항은 `PdfWorker/README.md`를 참고합니다.

## 검증

엔진 프로젝트 폴더에서:

```powershell
& .\PdfWorker\runtime\python.exe -B .\Tests\PdfSettingsRegression.py
& .\PdfWorker\runtime\python.exe -B .\Tests\PdfWorkerRegression.py
```

PineTree의 `Tests/OrderDocumentRegression.cs`, `Tests/OrderItemPdfRegression.cs`는 새 DLL 네임스페이스를 사용합니다. 컴파일 시 PineTree.exe 외에 PineTree.PdfEngine.dll도 참조합니다.

`Tests/PdfEngineDeploymentRegression.cs`는 엔진 DLL을 직접 참조하지 않는 검사 호스트입니다. PineTree.exe의 모듈 초기화를 실행한 후 Costura가 내장 DLL을 로드하는지 확인하고, 외부 PdfWorker 없이 조회/미리보기/생성/취소를 검증합니다. 선택 인수 `inspect`는 캐시 복구 및 동시 실행 검사에 사용합니다.

```powershell
& .\Tests\Test-Deployment.ps1 -ApplicationPath ..\PineTree\bin\Release\PineTree.exe -SourcePath <검증용원본AI경로>
```

배포 검사는 격리 폴더에 PineTree.exe만 복사하고 테스트 호스트를 별도로 생성합니다. 검증 결과는 `TestResults`에, 검증용 캐시는 작업폴더의 `tmp/pdf-cache-*`에 남깁니다. 최초 준비 취소, 두 프로세스 동시 준비, 메모리에서 DLL 로드, 미리보기·생성, 출력 보존 및 손상/누락 캐시 복구를 확인합니다.

# 선택 대지별 편집·출력 (2026-11-06)

이 문서는 기존 `MULTI_ARTBOARD.md`의 **모든 페이지에 공통 후가공/다중 페이지 출력 UI**를 대체하는 현재 현수막 동작을 설명한다.

## 동작

- 디자이너에 준비된 `panelCanvas`와 단일 `pictureBox1`을 사용한다. 원본 탭은 없다.
- 파일 분석 후 `대지 1`, `대지 2`… 버튼을 만들고 첫 대지를 선택한다. 선택 버튼은 파란 배경·흰 글자·두꺼운 테두리로 구분한다. 버튼이 많으면 수평 스크롤한다.
- 대지 버튼을 누르면 해당 원본 페이지의 미리보기를 요청한다. 이미 준비된 Bitmap과 진행 중 요청은 재사용하며 최대 4개 이미지를 보관한다. 문서별 worker는 직렬 실행하고 파일 교체/종료 시 취소한다.
- 미리보기 생성이 필요한 대지에서는 기존 `DialogProgressbar`에 "대지 N 미리보기를 생성하는 중입니다…"를 표시한다. 완료·실패·파일 교체·종료 및 캐시된 대지로 복귀할 때 닫는다. 초기 파일 분석/출력 생성 진행창과 별도로 관리해 이전 요청의 종료가 현재 진행창을 닫지 않도록 한다. 실제 백분율을 제공하지 않는 작업이므로 기존처럼 Marquee 표시를 사용한다.
- 대지별 후가공 목록, 크기/축척, 배치, 도련, 수량, 출력 서버 파일 참조를 독립 보관한다. 새 대지는 원본 크기, 수량 1, 후가공 없음으로 시작한다. 원본에서 발견한 재단선은 해당 대지에서만 기존 정책으로 인식한다.
- 대지 전환 전 현재 입력을 저장하고 이동한 대지의 설정을 복원한다. 메타데이터를 처음 읽는 동안에는 편집·출력 입력을 잠가 미완성 설정이 저장되는 것을 방지한다. PDF 생성/업로드 중에는 대지 전환을 막는다.
- 기존 주문 편집 JSON에 선택 대지 번호와 방문한 대지별 설정을 추가했다. 기존 버전 1 JSON은 그대로 읽으며 DB 스키마 변경은 없다. 원본을 다시 로드하면 저장된 대지 선택과 설정을 복원한다.
- 현수막 출력은 `GenerateAllPages=false`, `SourcePageIndex=선택 페이지(0부터)`를 사용한다. 기존 worker에 원본 페이지 번호를 전달해 해당 페이지에만 후가공한다. 출력은 단일 페이지 PDF다. 파일명에는 `_대지N_출력용.pdf`를 넣는다.
- `DialogDocumentPreview`의 TabControl과 탭별 저장 UI를 제거했다. 현수막은 현재 대지의 1페이지 결과만 열며 상단 저장 버튼으로 동일 PDF를 복사한다. 확대·맞춤·스크롤·상세 렌더링을 유지한다. 다른 기존 호출자가 다중 페이지 문서를 열 경우에는 이전/다음 버튼으로 이동할 수 있다.

## 주요 파일

- `PineTree/UserControlsOrder/UcOrder현수막.cs`: `SelectWorkingArtboard`, `ShowWorkingArtboard`, `ApplyWorkingArtboard`, `CaptureEditorState`/`RestoreEditorState`, 페이지별 캐시와 생성 설정.
- `PineTree/Classes/BannerOrderEditorState.cs`: `WorkingArtboardIndex`, `Artboards`, 대지별 JSON 검증.
- `PineTree.PdfEngine/OrderDocumentService.cs`: `SourcePageIndex` 검증 및 worker 요청 전달. 다른 호출자의 기본값은 기존처럼 페이지 0이다.
- `PineTree/Dialogs/DialogDocumentPreview.cs`: 탭 제거, 단일 결과 저장.
- `PineTree/Tests/WorkingArtboardRegression.cs`, `RunWorkingArtboardRegression.ps1`: UI/저장/재로드 회귀 검사.

사용자가 수정한 디자이너 배치는 그대로 활용했으며 다른 기존 작업 트리 변경은 유지했다. 기존 다중 페이지 생성 엔진 API는 다른 명시적 호출자를 위해 유지하지만 현수막에서 사용하지 않는다.

## 검증

- 격리 Debug/AnyCPU 빌드: `bin/WorkingArtboard`, `obj/WorkingArtboard`.
- 최종 빌드 성공. WinForms 검사 25개 통과: 탭 없음, 대지 버튼/선택 표시, 로딩 중 원래 대지로 복귀와 캐시, 진행창 즉시 표시·캐시 복귀 시 닫기·진행 중 작업 재선택 시 표시·완료 시 닫기, 대지별 독립 타공/재단/여백, 주문 JSON 저장·파일 재로드 후 선택 및 양쪽 설정 복원, 결과 창 1페이지·탭 없음, 새 파일 드롭 시 이전 설정 정리, JPEG/PDF 입력 유지, 정상 전환의 취소 예외 0건.
- 실제 생성 PDF 추가 검증: 가로형 대지1과 세로형 대지2의 치수를 독립 반영하고 각각 1페이지 출력, 대지1 중앙은 빨강·대지2 중앙은 파랑으로 원본 선택 확인, 대지2에만 재단 별색 존재, 저장본은 미리보기 PDF와 바이트 동일.
- 테스트는 외부 서버 업로드 없이 로컬 생성 경로를 사용했다. DB 실제 저장/서버 업로드와 Foxit/실물 출력 검증은 미실행이다. 실제 `Yokel.ai`의 전체 페이지 영역 처리는 이전 수정 상태를 유지했다.
- 로그: 프로젝트 루트 `tmp/working-artboard-build.log`, `tmp/working-artboard-ui-tests.log`. 출력: `tmp/working-artboard-ui/working-page1.pdf`, `working-page2.pdf`, `working-saved.pdf`.

`3.개발`에서 재실행:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\PineTree\Tests\RunWorkingArtboardRegression.ps1 -AssemblyPath .\PineTree\bin\WorkingArtboard\PineTree.dll
```

기본 Debug 경로로 빌드했다면 `-AssemblyPath`를 생략한다. 처음 선택하는 대지 또는 캐시에서 밀려난 대지는 렌더링 대기가 필요하다. 페이지별 설정은 이미지 캐시에서 제거되어도 유지된다. 주문 행은 기존처럼 하나이며 금액·면적 계산은 현재 Working 대지 값으로 갱신한다. 대지별 주문 행 자동 생성/합산은 추가하지 않았다.

## 페트 적용 (2026-11-07)

`UcOrder페트.cs`에도 현수막과 동일한 대지 버튼, Working 대지, 페이지별 설정 저장/복원, 4개 이미지 캐시, 진행 중 요청 재사용, worker 직렬 실행 및 진행 대화상자를 적용했다. 출력은 선택 대지의 후가공만 반영한 1페이지 PDF다. 초기 파일 로드는 preview에 포함된 메타데이터를 재사용해 중복 분석을 피한다.

페트의 기존 `InitializePdfInputs`, 편집정보 캡처의 도련 0mm, PDF 설정과 치수 계산은 유지했다. 페트에는 현수막의 바깥 도련 입력을 추가하지 않았다. `UcOrder페트.PostProcesses.cs`와 디자이너는 수정하지 않았으므로 페트 전용 재단 대화상자·안쪽 재단 여백·AL족자 처리 및 CUT의 우선 적용 규칙을 그대로 사용한다. 이미 공유하는 편집정보 JSON과 `DialogDocumentPreview`를 사용해 별도 DB/엔진 변경은 없다.

- 격리 Debug/AnyCPU 빌드 성공: `bin/PetArtboard`, `obj/PetArtboard`.
- 페트 WinForms 30개 검사 통과: 대지 선택/선택 표시, 개별 수량·후가공, 주문 JSON 및 원본 재로드 복원, 진행창 표시/종료, 로딩 중 왕복과 캐시 재사용, JPEG/PDF 입력, 새 파일 정리, 취소 예외 0건.
- 페트 특성 검증: 전용 재단 대화상자 유지, 바깥 도련 4방향 0mm, 안쪽 재단 여백 3/5/7/11mm 적용과 재로드 복원, AL족자와 함께 사용할 때 명시적 CUT 설정 우선 적용.
- 현수막 회귀 25개 검사 통과. 생성 PDF는 두 품목 모두 가로형/세로형 원본 순서와 선택을 유지하며 각 1페이지다. 원본 빨강/파랑 이미지와 대지2만의 재단 별색, 미리보기와 저장본 바이트 동일을 확인했다.
- 로컬 생성/저장 경로로 검증했으며 실제 DB/서버 업로드, Foxit, 실물 출력 및 실제 대용량 샘플의 재검증은 이번 변경에서 실행하지 않았다.
- 로그: 프로젝트 루트 `tmp/pet-artboard-build.log`, `tmp/pet-artboard-ui-tests.log`, `tmp/pet-artboard-banner-tests.log`. 페트 출력: `tmp/pet-artboard-ui/working-page1.pdf`, `working-page2.pdf`, `working-saved.pdf`.

동일한 테스트 러너에 품목 인자를 추가했다. 기존 기본값은 현수막이다.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\PineTree\Tests\RunWorkingArtboardRegression.ps1 -AssemblyPath .\PineTree\bin\PetArtboard\PineTree.dll -EditorKind Pet
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\PineTree\Tests\RunWorkingArtboardRegression.ps1 -AssemblyPath .\PineTree\bin\PetArtboard\PineTree.dll -EditorKind Banner
```

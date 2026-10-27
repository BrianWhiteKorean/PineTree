# 주문파일 직접 입력 PDF 생성

`DialogOrderItem`의 주문파일 탭을 대상으로 한다. 후가공 규칙 목록은 생성에 적용하지 않는다. 파일 선택 → 조건 입력 → 생성 → 저장 위치 지정 → 출력 미리보기 순서로 사용한다. PDF/AI/EPS/JPG를 같은 처리 경로로 정규화하며 원본을 수정하지 않는다.

## 컨트롤 해석과 빈값 기본값

| 컨트롤 | 의미 | 기본값 |
|---|---|---|
| `tbTargetWidth`, `tbTargetHeight` | 가장자리 여유를 제외한 완성 가로·세로, mm | 600 / 1800 |
| `cbDeploy` | 원본 종횡비를 유지하는 중앙 배치 | 비율 유지: 영역에 맞춤 |
| `tbEdgeTop`, `tbEdgeBottom`, `tbEdgeLeft`, `tbEdgeRight` | 완성 영역 바깥에 추가하는 방향별 여유, mm | 각각 2 |
| `chkMakeCutline` | 새 재단선 생성 여부 | 디자이너의 체크 상태 유지: 현재 해제 |
| `tbLineWidth` | 재단선 두께, mm | 0.18 |
| `cbSpot` | 재단선 색상 방식 | 별색(Spot) |
| `tbColorName` | 별색 채널명 | thru |
| `chkMakeHole` | 새 타공 위치 마크 생성 여부 | 디자이너의 체크 상태 유지: 현재 해제 |
| `cbHoleType` | 사방타공 또는 요청 간격별 타공 | 사방타공: 네 모서리 |
| `tbHoleCount` | 실제 생성할 마크 수, 읽기 전용 | 생성 해제 시 0 / 사방타공 4 / 간격별 자동 계산 |
| `tbMarkLeftRight`, `tbMarkTopBottom` | 완성 영역의 각 경계에서 마크 중심까지 안쪽 거리, mm | 각각 20 |
| `tbHoleSpacing` | 간격별 타공의 최대 간격, mm. 해당 모드에서만 표시 | 500 |
| `inputTextBox2` | 생성 완료한 출력 파일 경로 | 성공 후 반영 |

화면이 처음 열릴 때 숫자 기본값과 별색 이름을 표시한다. 사용자가 비우면 생성 시 동일한 기본값으로 해석한다. **명시적으로 입력한 0은 빈값이 아니다.** 가장자리 0은 허용하며 완성 크기 0은 오류로 안내한다.

`tbHoleSpacing`/`lblHoleSpacing`와 `lblPdfGenerationSummary`는 코드에서 추가한다. 사용자가 편집한 Designer 파일은 변경하지 않았다. 요약에는 가장자리를 포함한 출력 크기와 마크 개수를 표시한다.

## 배치 및 표시

- 맞춤(CONTAIN): 완성 영역 안에 디자인 전체가 들어오도록 중앙 배치한다. 종횡비가 다르면 남는 공간은 흰색이다.
- 채움(COVER): 완성 영역을 채우도록 중앙 확대하고 영역 밖은 클리핑한다. 디자인 전체를 비율 왜곡해 늘리지 않는다.
- 출력 크기 = 완성 크기 + 해당 방향의 가장자리 여유. 예: 완성 600×1800 + 사방 2 = 출력 604×1804mm.
- 가장자리는 배경 연장으로 처리한다. 맞춤으로 빈 공간이 생긴 경우에는 흰색 여백을 유지한다. 채움은 실제 보이는 디자인 경계에서 연장한다. 흰색/미러 등의 선택 UI는 이번 화면에 없어 추가하지 않았다.
- 재단선은 완성 영역 경계의 닫힌 사각형이다. 별색은 입력한 이름으로 생성하며 대체색은 C100/M0/Y0/K0, 농도 100%, 오버프린트 해제다.
- 타공은 실제 구멍이 아닌 십자 위치 표시다. 현재 UI에 없는 속성은 전체 길이 5mm, K100 선 1pt, 흰 받침선 1.5mm, 검정 오버프린트 사용으로 고정한다.
- 사방타공은 네 모서리다. 간격별은 안쪽 거리를 적용한 사각형의 각 변을 최대 간격 이내로 균등 분할한다. 네 모서리는 중복하지 않는다. 예: 완성 600×1800, 안쪽 20, 최대 간격 500 → 12개. 길이가 딱 나누어지지 않으면 실제 간격은 요청값보다 작아진다.
- 마크가 완성 영역 밖으로 나가지 않도록 안쪽 거리 2.5mm 이상, 해당 완성 길이의 절반 미만을 검증한다. 최대 2,000개로 제한한다.
- 입력 PDF의 기존 재단선/마크를 제거하는 기능은 아니다. 같은 별색 이름이 이미 있으면 새 재단선 생성을 중단하고 안내한다. 체크 해제 시에는 원본의 해당 요소를 유지한다.

## 원본 영역과 범위

- 첫 페이지를 생성한다. 미리보기에서 페이지를 이동해도 생성 대상 페이지는 바뀌지 않는다.
- PDF/EPS/JPG는 정규화된 페이지 전체를 사용한다. AI는 페이지보다 작은 큰 사각 클립 또는 최상위 배경 영역을 탐색한다. 적절한 후보가 없으면 페이지 전체다. 인식 영역의 크기는 파일정보에 표시한다.
- 제공된 PET/AL족자/현수막 AI의 디자인 영역을 확인했다. 임의 파일의 디자인 의도를 항상 자동 판정할 수는 없으며 수동 원본 영역 선택은 후속 기능이다.
- AL족자의 디자인 영역과 별색 재단선 영역을 따로 정하는 기능, 원본에 이미 포함된 작업 여유를 재사용하는 기능, 각목/미싱의 작업선, 후가공 규칙 실행은 이번 직접 입력 범위에 추가하지 않았다.
- PDF 호환/구형 PostScript AI를 지원한다. PDF/PostScript 데이터가 없는 AI는 호환 저장 또는 PDF 내보내기가 필요하다.

## 결과와 저장

별도 작업 프로세스에서 생성한 후 성공 시에만 최종 파일로 교체한다. 생성 중 입력과 확인 버튼을 잠그고 창 취소/닫기 시 프로세스를 취소한다. 실패·취소 시 기존 최종 출력 파일을 보존한다.

생성 완료 후 원본 또는 PDF 조건을 바꾸면 표시 중인 출력 경로를 비워 재생성이 필요함을 나타낸다. 이미 생성한 파일을 삭제하지 않는다. 주문 확인 시의 기존 요청/출력 파일 경로 저장 흐름은 유지한다.

이번에는 DB 스키마를 변경하지 않았다. 실제 조건은 PDF 문서정보의 `PineTreeGenerationSettings`에 schemaVersion, 원본 영역 좌표(pt, PDF 좌하단 기준), 페이지, 적용값, 마크 수, 엔진 버전으로 기록한다. 대화상자 입력값 자체를 DB에서 복원하는 기능은 후속 범위다.

## 검증 실행

PineTree 솔루션을 MSBuild로 빌드한 후, PineTree 프로젝트 폴더에서 실행한다. Python 검사는 형제 PdfEngine 프로젝트에서 관리한다.

```powershell
& ..\PineTree.PdfEngine\PdfWorker\runtime\python.exe -B ..\PineTree.PdfEngine\Tests\PdfSettingsRegression.py
& ..\PineTree.PdfEngine\PdfWorker\runtime\python.exe -B ..\PineTree.PdfEngine\Tests\PdfWorkerRegression.py
& C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:exe /out:bin\Debug\OrderItemPdfRegression.exe /reference:bin\Debug\PineTree.exe /reference:..\PineTree.PdfEngine\bin\Debug\PineTree.PdfEngine.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll Tests\OrderItemPdfRegression.cs
& .\bin\Debug\OrderItemPdfRegression.exe <PET원본AI경로> <검증용출력PDF경로>
```

마지막 검사는 DB 접속 없이 실제 `DialogOrderItem` 컨트롤을 설정해 생성한다. 검증 출력 위치에 PDF와 주문파일 탭 화면 이미지를 저장한다.

# 주문 품목별 후가공 저장

`OrderItemEditorStates`의 기존 `StateJSON`에 `PostProcesses` 배열을 추가합니다.
별도의 후가공 테이블은 필요하지 않습니다. 테이블이 이미 있으면 스키마 변경도 필요 없습니다.
테이블이 없는 환경은 `OrderPostProcesses.schema.sql`을 직접 실행합니다.
DB에 접속하거나 스키마를 자동 변경하는 코드는 추가하지 않았습니다.

## 저장과 복원

- 주문 품목(OrderItems) 1건 : 편집 상태(OrderItemEditorStates) 1건 : 후가공 설정 N건.
- 주문 저장 트랜잭션에서 기본정보, 품목 및 편집 상태를 함께 저장합니다.
- `EditorType`은 `현수막`이며 `Version`은 기존과 동일하게 1입니다.
- 원본파일 없이 폭·길이·수량이 0인 상태에서도 후가공 설정을 저장할 수 있습니다.
- `PostProcesses`가 없는 과거 JSON은 기존 MakeCutline/MakeHole 등의 값으로 목록을 복원합니다.
- `PostProcesses: []`는 사용자가 후가공을 모두 삭제한 상태입니다. 과거 필드로 되돌리지 않습니다.
- 컬렉션 순서는 화면의 순번이며 PDF 명령의 처리 순서가 아닙니다.
- 순번과 실제 설정값을 저장하고 표시 이름·설정 요약·타공 개수는 복원 시 계산합니다.
- 원본파일 경로와 편집 설정을 저장하며 파일 바이너리를 DB에 복사하지 않습니다.

## 배열 원소 예시

```json
{
  "SeqNo": 1,
  "ProcessCode": "HOLE",
  "Parameters": {
    "TypeCode": "CUSTOM_SPACING",
    "MarkLeftRightMm": 20,
    "MarkTopBottomMm": 20,
    "HoleSpacingMm": 500
  }
}
```

다른 편집 조건(치수·배치·도련·확대·원본파일 정보)은 기존 JSON 속성을 유지합니다.
Parameters는 공용 DTO로 직렬화하므로 해당 후가공에서 사용하지 않는 기본 속성도 포함됩니다.
모든 치수와 재단선 두께의 단위는 기존 PDF 엔진과 동일한 **mm**입니다.

| ProcessCode | 작업 | 주요 설정 | 현재 PDF 반영 |
|---|---|---|---|
| HOLE | 타공 | TypeCode, MarkLeftRightMm, MarkTopBottomMm, HoleSpacingMm | 반영 |
| CUT | 재단 | TypeCode, LineWidthMm, ColorMode, SpotColorName, FromSource | 기존 재단선 기능 반영 |
| WOOD | 좌우각목 | TypeCode, ExtraLineMm, MakeDesignLine, DesignLineMm | 좌우 흰 여백·여분선·디자인선 반영 |
| POLE_SEW | 봉미싱 | TypeCode, DimensionMm, MakeSlit | 입력·저장·표시 |
| HEM_SEW | 사방시접미싱 | TypeCode, DimensionMm | 입력·저장·표시 |
| BAND_SEW | 밴드미싱 | TypeCode, DimensionMm | 입력·저장·표시 |
| ROPE_SEW | 사방줄미싱 | TypeCode, DimensionMm | 입력·저장·표시 |

타공과 재단은 각각 한 건만 등록하며 같은 종류를 추가하면 기존 설정을 수정합니다.
좌우각목도 한 건만 등록하며 같은 종류 추가 시 기존 설정을 수정합니다. 다른 미싱 작업은 여러 건 등록할 수 있습니다. 미확정 작업 사이의 충돌 조건은 아직 적용하지 않습니다.
재단의 일반 재단/정재단 구분은 저장하며 둘 다 현재의 사각 별색 재단선 생성 기능을 사용합니다.
원본에 재단선이 있으면 원본을 유지하고 추가 재단선을 중복 생성하지 않습니다.
목록의 원본 재단선 행을 삭제해도 원본 PDF 내부 벡터는 제거되지 않습니다.
미확정 미싱 작업의 PDF 편집, 절개선, 원단 확장 규칙은 고객사 확인 후 별도로 구현합니다.

## 타공 대화상자 UI

사용자가 디자이너에서 `Dialog타공마크`에 `InputNumericBox`인 `tbHoleSpacing`을 추가합니다.
사방타공에서는 간격 입력을 비활성화하고 개수 4를 표시합니다.
요청 간격별 타공에서는 간격을 활성화하고 완성 치수·안쪽 거리·최대 간격으로 개수를 계산합니다.
간격 입력란이 아직 없으면 간격별 타공의 확인을 막고 누락된 컨트롤 이름을 안내합니다.

## 좌우각목

- `ExtraLineMm`: 좌우 각각 늘릴 흰 여백(mm). 빈값은 0. 과거 JSON의 이 값이 null이면 기존 DimensionMm를 사용합니다.
- `MakeDesignLine`: 디자인선 표시 여부.
- `DesignLineMm`: 원본 그림 경계에서 추가 여백 쪽으로 이격(mm). 별도 확장은 하지 않습니다.
- 출력 폭 = 원본 목표 폭 + 2 × ExtraLineMm + 좌우 도련. 출력 높이는 상하 도련 외에 추가하지 않습니다.
- 여분선과 디자인선은 고객사 샘플처럼 검정 세로선(0.17pt)으로 표시합니다. 여분선은 확대된 영역의 양 끝, 디자인선은 원본 경계에서 바깥쪽으로 배치합니다.
- 여백은 흰색이며 원본 그림을 늘이거나 가장자리 색을 반복해 채우지 않습니다.
- 재단·타공을 함께 선택하면 각목 여백을 포함한 완성 영역을 기준으로 계산합니다. 주문 면적 과금은 기존 목표 치수 기준을 유지합니다.
- 고객사 샘플은 PDF 내부 치수 기준 원본 약 800×59mm, 좌우 여분 각 8.03mm, 디자인선 이격 0mm, 도련 0일 때 약 816.06×59mm입니다.
- 고객사 완성본과 동일하게 좌우각목 작업에서는 4채널 CMYK 별칭을 DeviceCMYK로 출력합니다. RGB 프로파일·별색·이미지의 색공간은 유지합니다.
- 추가 DB 테이블이나 컬럼 변경 없이 기존 StateJSON에서 저장·복원합니다.

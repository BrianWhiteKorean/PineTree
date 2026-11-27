# 후가공 규칙 등록·수정 구현 결과

작성일: 2026-10-18

## 수정한 파일

- `Classes/ProcessRuleData.cs`: 편집 데이터, 명령별 파라미터 검증·JSON 변환.
- `Classes/ProcessRuleRepository.cs`: 규칙 조회, 마스터·명령 저장, 상품 연결, 공유 규칙 복제, 충돌 검사.
- `Dialogs/DialogProcessRule.cs`: 활성화 조건, 입력 검증, 조회·저장 및 결과 반환.
- `UserControls/UcProcessRule.cs`: 신규 대화상자의 DB 저장 결과 반영, 상품별 규칙 재조회, 조회 캐시 갱신.
- `UserControls/UcProcessRuleEach.cs`: 기존 카드의 명령 파라미터 편집 진입, 저장 상태 반영.
- `PineTree.csproj`: .NET Framework 기본 JSON 라이브러리 `System.Web.Extensions` 참조 추가.
- `Tests/ProcessRuleRegression.cs`: 로컬 및 DB 롤백 회귀검증.
- `Tests/ProcessRuleVersion.optional.sql`: 버전 컬럼 추가 검토용 SQL. 실행하지 않음.

기존 Designer 파일과 컨트롤 배치는 변경하지 않았습니다. 작업 시작 전에 있던 다른 변경은 유지했습니다.

## 사용 방법과 저장 흐름

- 상품 선택 후 신규 규칙 대화상자에서 입력하고 [확인]하면 즉시 저장됩니다. 이전처럼 카드에서 다시 [적용]할 필요가 없습니다.
- 신규 저장은 ProcessRule → ProcessRuleStep(StepSeq=1) → PriceItemProcessRule을 동일 트랜잭션에서 처리합니다. 상품 연결 중복 INSERT를 피하도록 호출부를 변경했습니다.
- `new DialogProcessRule()`은 상품 연결 없이 마스터와 명령만 저장합니다.
- `new DialogProcessRule(ruleID)`는 기존 규칙을 조회합니다. 상품 연결을 포함한 호출은 `(priceItemSeqNo, ruleID, mappingSeqNo)`를 사용합니다.
- 기존 카드의 **PDF 편집명령을 두 번 클릭**하면 저장된 명령과 입력값이 편집창에 표시됩니다. 명령 변경, 파라미터 수정 후 [확인]으로 저장합니다.
- 카드의 규칙명·설명 [적용]도 유지했습니다. JSON이 비어 있는 예전 규칙은 편집창에서 필수 파라미터부터 입력해야 합니다.
- 저장 결과는 `Rule` 속성으로 반환하며, 커밋 성공 후에만 `DialogResult.OK`로 닫습니다. 저장 실패 시 창을 유지합니다.
- 기존 WorkName은 별도 업무 텍스트로 보존합니다. PDF 명령 표시는 ActionCode로 변환합니다.
- 여러 상품이 공유하는 규칙은 창 제목에 연결 수와 복제 저장 방식을 표시합니다. 선택 상품용 규칙을 복제하여 해당 연결만 바꾸며, 원본과 비고는 보존합니다.
- 여러 Step이 있거나 비활성 Step이 있는 규칙은 이 단일 명령 화면에서 편집을 차단합니다. 나머지 명령을 무시하거나 삭제하지 않습니다. 향후 명령 목록 편집 UI가 필요합니다.
- 저장 전 마스터·명령 원본과 현재 DB 값을 비교해 다른 화면의 변경을 덮어쓰지 않도록 처리했습니다.

## 입력과 JSON 예시

다음 값은 출력 확인을 위한 예시이며, 고객사의 기본값으로 지정하지 않았습니다.

ADD_MARGIN:
```json
{"topMm":3.125,"bottomMm":0,"leftMm":2.5,"rightMm":10,"unit":"mm"}
```

ADD_CUT_LINE:
```json
{"topMm":3,"bottomMm":3,"leftMm":3,"rightMm":3,"unit":"mm"}
```

ADD_GUIDE_LINE:
```json
{"topMm":30,"bottomMm":30,"leftMm":0,"rightMm":0,"unit":"mm"}
```

ADD_PUNCH_MARK / CUSTOM_SPACING:
```json
{"punchMode":"CUSTOM_SPACING","punchCount":4,"diameterMm":5.25,"spacingMm":300,"unit":"mm"}
```

ADD_PUNCH_MARK / FOUR_SIDES:
```json
{"punchMode":"FOUR_SIDES","punchCount":4,"diameterMm":5.25,"spacingMm":null,"unit":"mm"}
```

타공 개수는 양의 정수, 직경과 요청 간격은 양수, 여백은 0 이상을 검사합니다. 직경 미정은 빈칸으로 표시하고 임의 기본값을 넣지 않습니다. 현재 저장 정책은 직경 확정 후 저장입니다. 비활성 입력은 JSON에 넣지 않으며 입력값 자체는 명령 전환 시 유지합니다. 신규 mm 입력은 소수점 3자리까지 표시하고 기존 데이터의 더 높은 소수 정밀도는 로드 시 유지합니다.

## 실제 DB 확인 및 남은 사항

실제 DB의 SHOW CREATE TABLE 조회 결과:

- 세 테이블 모두 InnoDB이므로 트랜잭션 사용 가능.
- ProcessRule에는 InputMode가 없음. 해당 컬럼 입력이나 스키마 변경 없이 저장.
- **ProcessRule에는 RuleVersion도 없음.** 임의로 컬럼을 추가하지 않았습니다. 컬럼 존재 여부를 런타임에 확인하며, 있으면 신규 1 / 수정 +1 / 공유 복제 이전 버전 +1로 저장합니다. 현재 DB에서는 버전 증가가 동작하지 않습니다. 검토용 SQL을 별도 제공했으며 실제 버전 기능 활성화에는 해당 컬럼 추가가 필요합니다.
- RuleID는 실제로 INT, RuleStepID와 PriceItemSeqNo는 BIGINT. C# 모델은 long으로 처리합니다.
- ParamsJSON은 LONGTEXT. 유효한 JSON만 파라미터 바인딩으로 저장합니다.
- 타공 위치·개수 계산, 고객사의 직경 기준 및 미정 직경 저장 허용 여부는 추가 확인 대상입니다.
- 주문별 스냅샷 저장은 이번 범위가 아닙니다. 과거 주문 데이터는 수정하지 않았습니다.
- 다른 스키마에 필수 InputMode 컬럼이 남아 있다면 실제 타입과 기본값을 추가 확인해야 합니다. 현재 확인한 DB에는 없습니다.

## 검증 결과

- Visual Studio 2022 MSBuild / .NET Framework 4.7.1 Debug 빌드 성공.
- 로컬 회귀검증 28개 통과: 네 명령 JSON 왕복 변환, 잘못된 값 거부, 활성화 분기, 입력값 유지, 소수 입력, 직경 기본값 미강제.
- 실제 DB 검증 13개 통과: 마스터·Step·연결 저장, 기존 명령 수정, WorkName 보존, 충돌 감지, 공유 규칙 복제·원본·비고 보존, 단독 규칙 저장, 다중 Step 차단, Step 오류 발생, 롤백 후 세 테이블 테스트 행 미잔류.
- DB 테스트는 단일 트랜잭션 내에서 수행하고 finally에서 전부 롤백했습니다. 자동증가 번호는 DB 특성상 소비될 수 있습니다.
- 화면 컨트롤은 폼 인스턴스를 생성해 프로그램으로 검증했습니다. 화면을 띄운 사용자 클릭 시나리오의 시각 검수는 별도로 필요합니다.
- RuleVersion 컬럼이 있는 DB의 증가 분기는 현재 DB에 컬럼이 없어 실DB 검증하지 못했습니다.

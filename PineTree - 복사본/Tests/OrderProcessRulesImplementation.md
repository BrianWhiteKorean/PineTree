# 주문별 후가공 규칙 연결 구현

적용일: 2026-10-20

## 실제 DB 변경

`OrderProcessRules.migration.sql`을 실제 DB에 실행하여 `OrderProcessRules` 테이블을 추가했습니다.
기존 `OrderInfo`, `OrderItems`, `OrderFiles`, `ProcessRule`의 컬럼 및 기존 데이터는 변경하지 않았습니다.

- OrderInfo 1:N OrderItems 관계를 기준으로 주문품목마다 후가공을 N개 저장합니다.
- 기존 OrderFiles의 OrderId / OrderItemId 관계는 유지합니다.
- OrderProcessRules의 OrderId, OrderItemId, RuleID는 필수 FK입니다.
- OrderFileId는 선택 FK이며 현재 UI에서는 같은 주문품목의 활성 REQUEST 파일에 연결합니다.
- 파일 없이도 규칙을 먼저 등록할 수 있습니다. 나중에 요청파일을 저장하면 연결합니다.
- 파일 삭제 시 규칙의 파일 연결만 NULL로 변경하고, 품목 삭제 시 해당 규칙 연결은 함께 삭제합니다.
- (OrderItemId, SortOrder) 유일키로 품목 내 실행 순서를 관리합니다. 3개 제한은 없습니다.
- ProcessName, Summary, StepsJSON에 주문 당시 명령과 ParamsJSON을 복사하여 원본 규칙 변경의 영향을 차단합니다.
- 실제 OrderItems.OrderId는 varchar(20), OrderInfo.OrderId와 OrderFiles.OrderId는 bigint입니다. 이번 작업에서는 기존 타입을 변경하지 않았고 저장 시 품목을 잠근 뒤 주문·파일 소유 관계를 확인합니다.

## UI 및 저장 흐름

두 대화상자가 `OrderProcessRuleEditor`를 공유합니다.

1. 기존 후가공 1~3 선택 버튼에서 규칙을 선택하고, `후가공 추가`로 행을 더 만듭니다. 넘치는 행은 세로 스크롤로 표시합니다.
2. 선택 시 ID, 순서, 표시 요약, 모든 활성 Step을 보관합니다. 요약은 읽기 전용이며 제거 버튼으로 선택을 해제합니다.
3. 작업요청파일 선택 버튼은 로컬 파일을 선택하고 해당 경로를 기록합니다.
4. 신규 입력은 대화상자 확인 후 상위 주문 저장에서 주문·품목·파일·후가공을 동일 트랜잭션에 저장합니다.
5. 기존 품목 수정은 수정창 확인 시 품목·파일·후가공을 동일 트랜잭션에 저장합니다. 취소 시 저장하지 않습니다.
6. 주문 재조회 시 파일 경로와 저장된 명령 스냅샷을 복원하며 4개 이상의 규칙도 수정창에서 모두 복원합니다.
7. 부모 주문 그리드의 숨김 데이터에도 파일 경로와 규칙을 유지해 재저장 시 연결이 사라지지 않습니다.

저장 API에서 ProcessRules=null은 기존값 유지, 빈 목록은 전체 제거입니다. 파일 경로도 null은 유지, 빈 문자열은 해당 역할 파일 비활성화입니다.
파일 생성·변환·서버 업로드 기능은 이번 구현에 포함하지 않습니다. UI의 파일 선택은 파일 경로 메타데이터를 DB에 저장합니다.

## 검증

- Visual Studio 2022 / .NET Framework 4.7.1 Debug 빌드 성공.
- 기존 OrderItemDialogRegression.ps1 및 ProcessRuleListRegression.ps1 통과.
- OrderProcessRuleRegression.cs: 양쪽 UI에서 7개 규칙과 경로 전달, 부모 저장 매핑, DB 저장·삭제·순서 변경·스냅샷 재조회·UI 복원, 다른 주문/비활성 파일 연결 차단, 기존 품목 삭제 흐름과 FK 연동을 검사합니다.
- 실제 DB 검증은 테스트 주문·품목·파일·연결만 트랜잭션 안에서 생성하고 finally에서 모두 롤백합니다. 롤백 후 주문·파일·규칙 테스트 행이 남지 않는지 확인합니다. 자동증가 번호는 소비될 수 있습니다.
- N행 UI를 폼 렌더링으로 확인했고 세로 스크롤을 확인했습니다.

회귀검증 실행: Tests/bin/OrderProcessRuleRegression.exe --database
마이그레이션 재적용: powershell -File Tests/ApplyOrderProcessRulesMigration.ps1

# 기존 주문 연결

`UcOrderDesignPad`의 `기존주문에 연결`은 새 작업을 별도의 OrderInfo 행으로 저장하면서 원주문과 같은 그룹으로 연결한다. 기존 OrderId와 관련 품목·파일 ID는 유지한다.

- 첫 연결 저장: 기본번호 → 기본번호_1, 새 주문 → 기본번호_2.
- 기존 하위 주문을 선택해도 OrderGroupId로 원주문을 찾아 같은 그룹의 최대 순번 + 1을 사용한다.
- 화면의 예정 번호는 저장 시 달라질 수 있다. 원주문 행을 FOR UPDATE로 잠근 후 순번을 배정하며, 복합 유일 인덱스로 중복을 방지한다.
- 원주문의 번호 변경과 신규 주문 저장은 같은 트랜잭션이다. 선택 취소나 저장 실패는 원주문을 변경하지 않는다.
- 기본번호를 OrderBaseNo에 보존하므로 일반 신규 주문 번호 계산 시 연결된 기본번호를 재사용하지 않는다.
- 원주문의 주문일·납기·거래처·채널·배송방법·택배비를 상속하고 잠근다. 연결 선택 후 원주문 정보가 바뀌면 저장을 거부하고 다시 선택하도록 안내한다.
- 비고, 제품, 규격, 후가공, 부자재는 새 작업의 독립 정보다. 택배비는 요청대로 각 연결 주문에 같은 값을 복사한다.

## DB 변경

`Tests/ApplyOrderLinkMigration.cs`를 기존 앱 어셈블리 경로를 인자로 실행하면 다음 변경을 멱등 적용한다.

- OrderInfo.OrderGroupId BIGINT NULL: 하위 주문이 참조하는 원주문 OrderId. 원주문 자체는 NULL.
- OrderInfo.OrderGroupSequence INT UNSIGNED NULL: 원주문 1, 하위 주문 2 이상. 미연결 주문은 NULL.
- OrderInfo.OrderBaseNo VARCHAR(64) NULL: 접미 순번을 제외한 기본번호.
- OrderNo를 VARCHAR(64)로 확장한다. 기존 주문번호 유일 인덱스는 유지한다.
- UX_OrderInfo_GroupSequence (OrderGroupId, OrderGroupSequence) 유일 인덱스.
- FK_OrderInfo_GroupRoot: 원주문을 참조하는 FK, ON DELETE RESTRICT.

적용 전 기존 `_숫자` 형식의 주문번호가 없는 것을 확인했다. 외부에서 접미 번호를 직접 입력하여 연결을 표현하지 말고 OrderGroupId/OrderGroupSequence/OrderBaseNo를 함께 관리해야 한다.

## 검증

`Tests/OrderLinkRegression.cs`는 UI 기본정보 상속·잠금·초기화, 선택 대화상자, 저장 롤백, 하위 주문 선택, 동시 순번 배정, 일반 번호 재사용 방지, 연결 주문 편집 시 기본정보 보존을 검증한다. 임시 주문은 2099년 날짜와 고유 표식으로 생성하고 해당 테스트 주문만 삭제한다.

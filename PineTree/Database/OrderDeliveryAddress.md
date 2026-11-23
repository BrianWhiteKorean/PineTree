# 주문 배송지 주소

OrderInfo.DeliveryAddress TEXT NULL 컬럼을 사용한다. 기존 주문은 NULL(미입력)로 유지한다.

```sql
ALTER TABLE OrderInfo
ADD COLUMN DeliveryAddress TEXT NULL COMMENT '배송지 주소';
```

`Tests/ApplyOrderAddressMigration.cs`는 컬럼 존재 여부를 확인하여 멱등 적용한다.

UcOrderDesignPad와 DialogOrderDesignPad의 tbAddress 입력을 OrderSaveData.DeliveryAddress에 연결하고, 신규·수정 저장과 GetOrderForEdit 조회에 반영한다. 새 주문 초기화 시 주소도 비운다.

수정 저장에서 null은 주소를 제공하지 않는 기존 편집 경로를 의미하여 기존 값을 유지한다. 빈 문자열은 사용자가 주소를 지운 경우이며 DB에는 NULL을 저장한다. 새 입력 화면은 입력값의 앞뒤 공백을 제거하여 전달한다.

`Tests/OrderAddressRegression.cs`는 실제 DB 트랜잭션에서 한글·따옴표 포함 주소 저장, 수정, 빈 값 처리와 두 화면의 복원을 검증한 뒤 전체 롤백한다.

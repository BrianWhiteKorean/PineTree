# 주문번호별 저장 미리보기

- `OrderPreviewSnapshots.OrderNo`가 기본키이며, `OrderId`는 주문 삭제 시 함께 삭제되는 고유 외래키입니다.
- `SourceFileInfo`는 분석 완료 당시 파일 정보입니다. `PreviewJSON`은 대지별 분석 메타데이터, 전체 PNG의 서버 참조, 원래 픽셀 크기를 보관합니다.
- 최초 저장 시 이미 분석한 대지의 전체 비트맵을 축소하거나 화면 영역으로 잘라내지 않고 PNG로 업로드합니다. 여러 대지는 모두 저장합니다.
- 이미지는 기존 파일 서버의 해시 기반 저장소를 사용하고, 주문번호와 이미지 참조의 관계는 DB에 보관합니다.
- 이미지 업로드가 모두 성공한 후 주문과 미리보기 참조를 같은 DB 트랜잭션으로 저장합니다. 업로드 실패 시 주문 저장도 완료 처리하지 않습니다.
- `DialogOrderDesignPad`는 DB 메타데이터로 편집 상태를 복원하고 서버 PNG만 내려받습니다. 원본 다운로드, 분석, PDF 런타임 준비를 자동 실행하지 않습니다. 대지 전환도 PNG를 불러옵니다.
- 미리보기가 없는 이전 주문은 저장된 파일 정보와 안내를 표시합니다. 자동 재분석으로 돌아가지 않으며, 해당 주문에 원본을 다시 불러온 뒤 저장하면 적용됩니다.
- 사용자가 출력 PDF를 다시 생성할 때만 필요한 원본을 내려받습니다. 출력 처리와 대화상자 열기는 별개입니다.

마이그레이션: `Tests/ApplyOrderPreviewSnapshotsMigration.ps1` (`OrderPreviewSnapshots.migration.sql`).
서버/DB 통합 검증: `Tests/RunOrderPreviewSnapshotRegression.ps1`. 테스트 주문은 롤백하며, 테스트 PNG는 기존 해시 저장소에 중복 없이 저장됩니다.

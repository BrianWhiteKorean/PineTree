# 현수막 주문 파일 서버 저장

`UcOrder수성_그룹A`와 `UcOrder수성_그룹B`의 출력 미리보기 버튼은 PDF 생성 → 원본 업로드 → 출력 PDF 업로드 → 미리보기 표시 순서로 동작한다. 생성·전송 중에는 입력과 주문 저장을 잠근다. 원본이 100MB 이상이면 안내 후 업로드를 생략한다. 서버 전송 실패 시에는 원인을 알리고 생성된 로컬 PDF 미리보기를 표시한다. 실패한 출력은 서버 저장 완료로 기록하지 않으며, 이미 업로드한 원본 참조는 유지한다.

2026-11-05 실제 443 포트는 XAMPP 대신 시스템 Apache가 처리하는 것으로 확인했다. 사용자 인증서 갱신 이후, 승인받아 JSON 모듈과 파일 API 전용 PHP 풀·Apache 핸들러·인증서 갱신 훅을 적용했다. 실제 클라이언트의 PDF 업로드·다운로드와 SHA-256 일치 검증을 통과했다. 적용 내역과 복구 방법은 [ActiveApache/README.md](ActiveApache/README.md)를 참조한다.

주문 확인/저장 시 기존 `OrderItemEditorStates.StateJSON`에 `SourceServerFile`, `OutputServerFile`을 함께 저장한다. 파일 참조에는 저장소 키, 파일 ID, 원래 파일명, 바이트 크기, SHA-256이 들어간다. 새 컬럼이나 기존 주문 데이터 변경은 없다. 서버 연결 설정용 `OrderFileStorageSettings` 테이블만 추가했다.

수정모드에서 주문 그리드의 현수막 행을 표시하면 서버 원본을 임시 폴더로 내려받고 크기·해시 검증 후 다시 분석한다. 저장된 폭·길이·수량·확대모드·도련·후가공·금액을 초기화하지 않는다. 서버 참조가 없는 기존 주문은 종전의 로컬 원본 복원 경로를 사용한다. 서버 참조가 있는데 다운로드에 실패하면 로컬의 다른 파일로 대체하지 않고 파일정보 영역에 오류를 표시한다.

새 원본을 드롭하면 이전 서버 원본/출력 참조를 해제한다. 출력 조건을 변경하면 이전 출력 참조를 해제한다. 변경 후 미리보기와 주문 저장을 다시 수행한다. 내려받은 임시 원본은 컨트롤 해제 시 제거하며 임시 경로를 주문 원본 경로에 덮어쓰지 않는다.

## 서버 구성

- 파일 저장: `/var/lib/pinetree/order-files` (웹 루트 외부, daemon 소유, 0700)
- API: `https://yeowoosai.co.kr/pinetreefiles/api.php`
- 서버 전용 설정: `/opt/lampp/etc/pinetree-order-files.php` (root:daemon, 0640)
- 클라이언트 연결 설정: `PineTree.OrderFileStorageSettings`의 `BANNER` 행

API는 HTTPS와 `X-PineTree-Token`을 요구한다. 클라이언트에는 SSH 관리자 계정이 필요하지 않다. 인증 토큰은 소스/로그에 넣지 않고 서버 전용 설정과 DB 연결 설정에 보관한다. 업로드는 SHA-256 기반 파일 ID로 중복 저장을 방지하고, 임시 파일에 받은 내용의 크기·해시를 확인한 뒤 원자적으로 확정한다.

주문 삭제 시 DB 트랜잭션 안에서 업로드 파일 ID를 삭제 대기열에 등록한다. 저장된 다른 주문에서 참조하는 파일은 유지한다. 파일 서버에서 삭제가 실패하면 대기열에 남겨 다음 주문관리 화면 초기화 또는 주문 삭제 때 재시도한다. 클라이언트 배포 전에 [삭제 대기열 스키마](../../PineTree/Database/OrderFileDeletionQueue.schema.sql)를 적용하고 파일 API를 배포한다.

## 배포와 검증

`Deploy.py`는 개발메모에서 서버 접속정보를 읽고 `DB.cs`에서 기존 DB 연결정보를 읽는다. Python의 paramiko가 필요하다. 첫 연결의 서버 호스트키를 `server-host-key.sha256`에 기록하고 이후 배포에서 확인한다. 재배포 시 토큰을 유지하고 기존 주문 테이블/파일, SSH·Apache 설정을 변경하지 않는다.

서버 배포는 작업 루트에서 다음과 같이 실행한다.

```powershell
python -W ignore '3.개발/Server/OrderFiles/Deploy.py'
```

클라이언트는 `3.개발/PineTree.sln`의 기존 빌드/런처 배포 경로를 따른다. 추가 NuGet 패키지나 서버 자격증명 배포파일은 필요하지 않다.

`PineTree/Tests/BannerServerFilesRegression.cs`는 실제 서버에 생성한 테스트 이미지/PDF를 전송하며 기존 주문은 저장하거나 변경하지 않는다. 로컬 원본 삭제 후 컨트롤 복원, 실제 DialogOrder 그리드 연결, 주문 저장 데이터에 서버 참조 포함, 금액 유지, 누락 파일 오류, 새 원본 드롭 시 참조 해제를 검증한다. 테스트 호스트에는 System.Windows.Forms, System.Drawing, System.Data 참조와 STA 진입점이 필요하다. 실행 인수는 PineTree.dll 경로와 테스트 결과 폴더다.

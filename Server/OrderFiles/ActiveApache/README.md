# 파일 서버 복구 내역

2026-11-05 확인 결과, 443 포트는 `/usr/sbin/httpd`가 처리한다. 기존 배포 문서의 XAMPP Apache는 실행 중이지 않다. 실제 HTTPS 기본 호스트는 도메인이 다른 만료된 자체 인증서를 제공한다. `/etc/letsencrypt/live/yeowoosai.co.kr/fullchain.pem`에는 2027-01-04까지 유효한 도메인 인증서가 있다.

2026-11-05 사용자 승인 후 다음 설정을 운영 서버에 적용했다. 사용자가 갱신·연결한 도메인 인증서와 가상 호스트는 유지했다.

- 기존 PHP와 같은 버전의 `php-json-7.2.24-1.module+el8.4.0+413+c9202dda.x86_64` 패키지만 추가 설치했다. 기존 PHP 패키지는 업그레이드하지 않았다.
- `pinetree-files.pool.conf`를 `/etc/php-fpm.d/pinetree-files.conf`에 추가했다. 기존 설정과 저장파일에 접근할 수 있는 `daemon` 사용자로 별도 PHP 풀을 실행한다. 기존 소유권과 인증 토큰은 유지했다.
- `pinetree-files-handler.conf`를 `/etc/httpd/conf.d/pinetree-files-handler.conf`에 추가했다. 파일 API 디렉터리만 전용 PHP 소켓에 연결한다.
- PHP-FPM과 Apache 문법 검사 후 두 서비스를 graceful reload했다. 전체 서비스를 중지하거나 XAMPP를 시작하지 않았다.
- `reload-active-httpd.sh`를 `/etc/letsencrypt/renewal-hooks/deploy/pinetree-active-httpd.sh`에 추가하고 실행 검증했다. 해당 도메인 인증서 갱신 후 실제 Apache를 reload한다.
- Windows 클라이언트의 인증서 검증을 켠 HTTPS 요청에서 인증되지 않은 요청이 401을 반환했다. 실제 `OrderFileStorageService`를 통해 문제의 원본 PDF(88,533바이트)와 출력 PDF(66,753바이트)를 업로드·다운로드하고 SHA-256 일치를 확인했다.

기존 설정 백업과 적용한 후보 파일은 서버의 `/root/pinetree-api-repair-20261105.mh6MiM`에 보관했다. 되돌릴 때는 이번에 추가한 PHP 풀·Apache 핸들러 설정과 갱신 훅만 제거하고 설정 검사 후 다시 reload한다. `php-json` 패키지는 유지해도 된다. 기존 주문과 업로드 파일은 삭제하지 않는다.

`pinetree-files-ssl.conf`는 최초 진단 시 만든 참고 후보이며 **서버에 적용하지 않았다**. 현재 도메인의 `/etc/httpd/conf.d/yeowoosai.conf`가 인증서를 처리하므로 중복 가상 호스트를 추가하지 않는다.

인증서 갱신의 기존 XAMPP 훅과 다른 도메인의 설정은 수정하지 않는다. 위의 별도 훅으로 실제 `httpd`도 갱신된 인증서를 읽도록 한다.

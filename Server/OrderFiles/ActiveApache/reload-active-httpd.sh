#!/bin/sh
set -eu
# Deploy hook for this certificate only; other domains keep their own hooks.
case " ${RENEWED_DOMAINS:-} " in
    *" yeowoosai.co.kr "*) ;;
    *) exit 0 ;;
esac
if systemctl is-active --quiet httpd; then
    /usr/sbin/httpd -t
    systemctl reload httpd
fi

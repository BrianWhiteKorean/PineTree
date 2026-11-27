-- 검토용: 현재 DB에서 RuleVersion 컬럼이 없는 것을 확인했습니다.
-- 이 파일은 실행하지 않았습니다. 운영 반영 절차에 따라 검토 후 적용하세요.
-- 기존 주문의 스냅샷/버전 이력을 생성하는 마이그레이션은 아닙니다.
ALTER TABLE ProcessRule ADD COLUMN RuleVersion INT NOT NULL DEFAULT 1 COMMENT '후가공 규칙 버전';

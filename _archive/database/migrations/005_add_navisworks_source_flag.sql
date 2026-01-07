-- ========================================
-- 005_add_navisworks_source_flag.sql
-- Navisworks 원본 여부를 명시하는 컬럼 추가
-- ========================================

BEGIN;

ALTER TABLE navisworks_hierarchy
    ADD COLUMN IF NOT EXISTS source_system VARCHAR(50) DEFAULT 'navisworks';

ALTER TABLE navisworks_hierarchy
    ALTER COLUMN source_system SET NOT NULL;

ALTER TABLE navisworks_hierarchy
    DROP CONSTRAINT IF EXISTS chk_navisworks_source_system;

ALTER TABLE navisworks_hierarchy
    ADD CONSTRAINT chk_navisworks_source_system
        CHECK (source_system IN ('navisworks'));

COMMENT ON COLUMN navisworks_hierarchy.source_system IS
    '데이터가 유입된 시스템 식별자 (기본값: navisworks)';

COMMIT;

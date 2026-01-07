-- ============================================
-- Snowdon Towers 데이터 삭제 스크립트
-- ============================================
-- 작성일: 2025-10-18
-- 목적: Snowdon Towers 프로젝트 데이터 안전하게 제거
-- ⚠️ 주의: 삭제 전 반드시 백업하세요!
--
-- 실행 방법:
-- psql -h localhost -U postgres -d DX_platform -f delete_snowdon_towers.sql
-- ============================================

-- ============================================
-- 1. 백업 (선택사항이지만 강력 권장)
-- ============================================
-- 터미널에서 실행:
-- pg_dump -h localhost -U postgres -d DX_platform -t objects -t metadata -t relationships > backup_before_delete_$(date +%Y%m%d_%H%M%S).sql

-- ============================================
-- 2. 삭제 전 데이터 확인
-- ============================================

-- 2-1. Snowdon Towers 관련 model_version 확인
SELECT DISTINCT model_version
FROM metadata
WHERE project_name = 'Snowdon Towers';

-- 예상 결과: Snowdon Towers_20251017_170052

-- 2-2. 삭제될 데이터 통계 확인
SELECT
    'metadata' AS table_name,
    COUNT(*) AS count
FROM metadata
WHERE project_name = 'Snowdon Towers'

UNION ALL

SELECT
    'objects' AS table_name,
    COUNT(*) AS count
FROM objects
WHERE model_version LIKE 'Snowdon Towers%'

UNION ALL

SELECT
    'relationships' AS table_name,
    COUNT(*) AS count
FROM relationships
WHERE model_version LIKE 'Snowdon Towers%';

-- 예상 결과:
-- table_name    | count
-- --------------+-------
-- metadata      | 1
-- objects       | 13710
-- relationships | 70

-- ============================================
-- 3. 트랜잭션 시작 (안전한 삭제)
-- ============================================

BEGIN;

-- 3-1. relationships 테이블에서 삭제
DELETE FROM relationships
WHERE model_version LIKE 'Snowdon Towers%';
-- 예상: 70개 행 삭제

-- 3-2. objects 테이블에서 삭제
DELETE FROM objects
WHERE model_version LIKE 'Snowdon Towers%';
-- 예상: 13710개 행 삭제

-- 3-3. metadata 테이블에서 삭제
DELETE FROM metadata
WHERE project_name = 'Snowdon Towers';
-- 예상: 1개 행 삭제

-- ============================================
-- 4. 삭제 결과 확인
-- ============================================

-- 4-1. 남아있는 데이터 확인
SELECT
    'metadata' AS table_name,
    COUNT(*) AS remaining_count
FROM metadata

UNION ALL

SELECT
    'objects' AS table_name,
    COUNT(*) AS remaining_count
FROM objects

UNION ALL

SELECT
    'relationships' AS table_name,
    COUNT(*) AS remaining_count
FROM relationships;

-- 예상 결과:
-- table_name    | remaining_count
-- --------------+----------------
-- metadata      | 1  (프로젝트 이름만 남음)
-- objects       | 852
-- relationships | 0

-- 4-2. 남아있는 프로젝트 확인
SELECT
    project_name,
    model_version,
    total_object_count,
    created_at
FROM metadata
ORDER BY created_at DESC;

-- ============================================
-- 5. 커밋 또는 롤백 결정
-- ============================================

-- 결과가 예상과 일치하면 커밋
COMMIT;

-- 만약 문제가 있다면 롤백 (데이터 복구)
-- ROLLBACK;

-- ============================================
-- 6. 삭제 후 테이블 최적화 (선택사항)
-- ============================================

-- 6-1. VACUUM으로 공간 회수
VACUUM FULL objects;
VACUUM FULL relationships;
VACUUM FULL metadata;

-- 6-2. 통계 정보 업데이트
ANALYZE objects;
ANALYZE relationships;
ANALYZE metadata;

-- ============================================
-- 완료!
-- ============================================

-- 최종 확인: 전체 데이터 카운트
SELECT
    (SELECT COUNT(*) FROM metadata) AS metadata_count,
    (SELECT COUNT(*) FROM objects) AS objects_count,
    (SELECT COUNT(*) FROM relationships) AS relationships_count;

-- ========================================
-- DX Platform 데이터베이스 초기화 스크립트
-- ========================================
--
-- 사용법:
-- 1. PostgreSQL 설치 후 데이터베이스 생성:
--    CREATE DATABASE dx_platform WITH ENCODING='UTF8';
--
-- 2. 생성된 데이터베이스에 연결:
--    \c dx_platform
--
-- 3. 이 스크립트 실행:
--    \i init_database.sql
--
-- 또는 커맨드라인에서:
--    psql -U postgres -d dx_platform -f init_database.sql
-- ========================================

\echo '========================================';
\echo 'DX Platform 데이터베이스 초기화 시작';
\echo '========================================';

-- 1. 원시 데이터 테이블 생성
\echo '';
\echo '1. 원시 데이터 테이블 생성 중...';
\i tables/metadata.sql
\i tables/objects.sql
\i tables/relationships.sql
\echo '✓ 원시 데이터 테이블 생성 완료';

-- 2. 분석용 뷰 생성
\echo '';
\echo '2. 분석용 뷰 생성 중...';
\i views/analytics_version_summary.sql
\i views/analytics_4d_link_data.sql
\echo '✓ 분석용 뷰 생성 완료';

-- 3. 함수 생성
\echo '';
\echo '3. 함수 생성 중...';
\i functions/fn_compare_versions.sql
\i functions/fn_get_object_history.sql
\echo '✓ 함수 생성 완료';

-- 4. 트리거 생성 (불변성 보장)
\echo '';
\echo '4. 트리거 생성 중 (불변성 보장)...';
\i triggers/prevent_raw_data_modification.sql
\echo '✓ 트리거 생성 완료';

-- 5. 역할 및 권한 설정
\echo '';
\echo '5. 역할 및 권한 설정 중...';
\i security/roles_and_permissions.sql
\echo '✓ 역할 및 권한 설정 완료';

-- 완료 메시지
\echo '';
\echo '========================================';
\echo '✓ DX Platform 데이터베이스 초기화 완료!';
\echo '========================================';
\echo '';
\echo '다음 단계:';
\echo '1. 비밀번호 변경: ALTER ROLE dx_api_role PASSWORD ''새비밀번호'';';
\echo '2. FastAPI 서버 설정에서 연결 정보 업데이트';
\echo '3. 테스트 데이터 삽입으로 검증';
\echo '';

-- 데이터베이스 객체 요약
SELECT
    'Tables' AS object_type,
    COUNT(*) AS count
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_type = 'BASE TABLE'
UNION ALL
SELECT
    'Views' AS object_type,
    COUNT(*) AS count
FROM information_schema.views
WHERE table_schema = 'public'
UNION ALL
SELECT
    'Functions' AS object_type,
    COUNT(*) AS count
FROM information_schema.routines
WHERE routine_schema = 'public'
  AND routine_type = 'FUNCTION';

-- Additional initialization (RAG/Knowledge + analytics)
\echo '';
\echo 'Additional: RAG/Knowledge tables and analytics views ...';
\i extensions/pgvector.sql
\i tables/knowledge_sources.sql
\i tables/rag_documents.sql
\i tables/rag_document_chunks.sql
\i tables/rag_chunk_embeddings.sql
\i tables/rag_qa_logs.sql
\i views/analytics_rag_usage.sql
\echo 'Additional initialization complete';

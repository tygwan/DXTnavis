-- ========================================
-- 역할 및 권한 관리
-- ========================================

-- API 서버 전용 역할 생성
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'dx_api_role') THEN
        CREATE ROLE dx_api_role LOGIN PASSWORD 'ChangeThisPassword123!';
    END IF;
END
$$;

-- 원시 데이터 테이블에 대한 권한
GRANT SELECT, INSERT ON TABLE metadata TO dx_api_role;
GRANT SELECT, INSERT ON TABLE objects TO dx_api_role;
GRANT SELECT, INSERT ON TABLE relationships TO dx_api_role;

-- 시퀀스에 대한 권한 (SERIAL 컬럼 사용)
GRANT USAGE, SELECT ON SEQUENCE objects_id_seq TO dx_api_role;
GRANT USAGE, SELECT ON SEQUENCE relationships_id_seq TO dx_api_role;

-- 분석용 뷰에 대한 읽기 권한
GRANT SELECT ON analytics_version_summary TO dx_api_role;
GRANT SELECT ON analytics_4d_link_data TO dx_api_role;

-- 함수 실행 권한
GRANT EXECUTE ON FUNCTION fn_compare_versions TO dx_api_role;
GRANT EXECUTE ON FUNCTION fn_get_object_history TO dx_api_role;

-- 읽기 전용 역할 (Power BI 등 분석 도구용)
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'dx_readonly_role') THEN
        CREATE ROLE dx_readonly_role LOGIN PASSWORD 'ReadOnlyPassword123!';
    END IF;
END
$$;

GRANT SELECT ON TABLE metadata TO dx_readonly_role;
GRANT SELECT ON TABLE objects TO dx_readonly_role;
GRANT SELECT ON TABLE relationships TO dx_readonly_role;
GRANT SELECT ON analytics_version_summary TO dx_readonly_role;
GRANT SELECT ON analytics_4d_link_data TO dx_readonly_role;
GRANT EXECUTE ON FUNCTION fn_compare_versions TO dx_readonly_role;
GRANT EXECUTE ON FUNCTION fn_get_object_history TO dx_readonly_role;

-- 주의: 운영 환경에서는 반드시 비밀번호를 변경하세요!

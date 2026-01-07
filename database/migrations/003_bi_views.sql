-- ========================================
-- AWP 2025 BI Views (Business Intelligence 뷰)
-- ========================================
-- 목적: Power BI, Tableau 등 BI 도구 연동
-- 작성일: 2025-10-19
--
-- 주요 뷰:
-- 1. v_bi_objects: 통합 객체 뷰 (평면화)
-- 2. v_bi_hierarchy: 계층 구조 뷰
-- 3. v_bi_4d_schedule: 4D 시뮬레이션 뷰
-- 4. v_bi_project_summary: 프로젝트 요약 뷰
-- ========================================

BEGIN;

-- ============================================
-- 1. BI 객체 뷰 (평면화)
-- ============================================
DROP MATERIALIZED VIEW IF EXISTS v_bi_objects CASCADE;

CREATE MATERIALIZED VIEW v_bi_objects AS
SELECT
    -- 프로젝트 정보
    p.code AS project_code,
    p.name AS project_name,
    p.client_name,
    p.building_name,
    p.project_number,

    -- 리비전 정보
    r.revision_number,
    r.version_tag,
    r.source_type AS revision_source,
    r.created_at AS revision_date,

    -- 객체 정보
    o.id AS internal_id,
    o.object_id,
    o.element_id,
    o.source_type,
    o.display_name,
    o.category,
    o.family,
    o.type,

    -- 계층 정보
    o.parent_object_id,
    o.level,
    o.spatial_path,

    -- 스케줄 정보
    o.activity_id,
    a.activity_name,
    a.planned_start_date,
    a.planned_end_date,
    a.actual_start_date,
    a.actual_end_date,
    a.duration,
    a.progress,
    a.discipline,

    -- 속성 (주요 속성만 추출)
    o.properties->>'이름' AS prop_name,
    o.properties->>'유형' AS prop_type,
    o.properties->>'GUID' AS prop_guid,
    o.properties->>'Element ID' AS prop_element_id,
    o.properties->>'소스 파일' AS prop_source_file,

    -- Bounding Box (공간 분석용)
    (o.bounding_box->>'MinX')::DOUBLE PRECISION AS bbox_min_x,
    (o.bounding_box->>'MinY')::DOUBLE PRECISION AS bbox_min_y,
    (o.bounding_box->>'MinZ')::DOUBLE PRECISION AS bbox_min_z,
    (o.bounding_box->>'MaxX')::DOUBLE PRECISION AS bbox_max_x,
    (o.bounding_box->>'MaxY')::DOUBLE PRECISION AS bbox_max_y,
    (o.bounding_box->>'MaxZ')::DOUBLE PRECISION AS bbox_max_z,

    -- 계산 필드: 중심점
    CASE
        WHEN o.bounding_box IS NOT NULL THEN
            ((o.bounding_box->>'MinX')::DOUBLE PRECISION + (o.bounding_box->>'MaxX')::DOUBLE PRECISION) / 2
        ELSE NULL
    END AS center_x,
    CASE
        WHEN o.bounding_box IS NOT NULL THEN
            ((o.bounding_box->>'MinY')::DOUBLE PRECISION + (o.bounding_box->>'MaxY')::DOUBLE PRECISION) / 2
        ELSE NULL
    END AS center_y,
    CASE
        WHEN o.bounding_box IS NOT NULL THEN
            ((o.bounding_box->>'MinZ')::DOUBLE PRECISION + (o.bounding_box->>'MaxZ')::DOUBLE PRECISION) / 2
        ELSE NULL
    END AS center_z,

    -- 매칭 상태 (Revit ↔ Navisworks)
    CASE
        WHEN o.source_type = 'revit' AND EXISTS (
            SELECT 1 FROM unified_objects n
            WHERE n.source_type = 'navisworks'
              AND n.project_id = o.project_id
              AND (n.properties->>'Element ID')::INTEGER = o.element_id
        ) THEN 'matched'
        WHEN o.source_type = 'navisworks' AND EXISTS (
            SELECT 1 FROM unified_objects r
            WHERE r.source_type = 'revit'
              AND r.project_id = o.project_id
              AND r.element_id = (o.properties->>'Element ID')::INTEGER
        ) THEN 'matched'
        ELSE 'unmatched'
    END AS match_status,

    -- 타임스탬프
    o.created_at

FROM unified_objects o
JOIN revisions r ON o.revision_id = r.id
JOIN projects p ON o.project_id = p.id
LEFT JOIN object_activity_mappings oam ON o.id = oam.object_id
LEFT JOIN activities a ON oam.activity_id = a.id;

-- 인덱스 생성 (성능 최적화)
CREATE UNIQUE INDEX idx_bi_objects_id ON v_bi_objects(internal_id);
CREATE INDEX idx_bi_objects_project ON v_bi_objects(project_code);
CREATE INDEX idx_bi_objects_category ON v_bi_objects(category);
CREATE INDEX idx_bi_objects_activity ON v_bi_objects(activity_id) WHERE activity_id IS NOT NULL;
CREATE INDEX idx_bi_objects_source ON v_bi_objects(source_type);
CREATE INDEX idx_bi_objects_match ON v_bi_objects(match_status);

-- 주석
COMMENT ON MATERIALIZED VIEW v_bi_objects IS 'BI 도구용 통합 객체 뷰 (평면화)';


-- ============================================
-- 2. BI 계층 뷰 (트리 구조)
-- ============================================
DROP MATERIALIZED VIEW IF EXISTS v_bi_hierarchy CASCADE;

CREATE MATERIALIZED VIEW v_bi_hierarchy AS
WITH RECURSIVE hierarchy AS (
    -- 루트 노드 (Level 0)
    SELECT
        o.id AS internal_id,
        o.object_id,
        o.parent_object_id,
        o.level,
        o.display_name,
        o.category,
        o.display_name::TEXT AS hierarchy_path,
        p.code AS project_code,
        p.name AS project_name,
        r.revision_number,
        r.version_tag,
        1 AS depth
    FROM unified_objects o
    JOIN revisions r ON o.revision_id = r.id
    JOIN projects p ON o.project_id = p.id
    WHERE o.level = 0
      AND o.source_type = 'navisworks'

    UNION ALL

    -- 자식 노드
    SELECT
        o.id,
        o.object_id,
        o.parent_object_id,
        o.level,
        o.display_name,
        o.category,
        h.hierarchy_path || ' > ' || o.display_name,
        h.project_code,
        h.project_name,
        h.revision_number,
        h.version_tag,
        h.depth + 1
    FROM unified_objects o
    JOIN hierarchy h ON o.parent_object_id = h.object_id
    JOIN revisions r ON o.revision_id = r.id
    WHERE o.source_type = 'navisworks'
      AND h.depth < 50  -- 무한 루프 방지
)
SELECT
    internal_id,
    object_id,
    parent_object_id,
    level,
    display_name,
    category,
    hierarchy_path,
    project_code,
    project_name,
    revision_number,
    version_tag,
    depth
FROM hierarchy;

-- 인덱스
CREATE UNIQUE INDEX idx_bi_hierarchy_id ON v_bi_hierarchy(internal_id);
CREATE INDEX idx_bi_hierarchy_project ON v_bi_hierarchy(project_code);
CREATE INDEX idx_bi_hierarchy_level ON v_bi_hierarchy(level);
CREATE INDEX idx_bi_hierarchy_parent ON v_bi_hierarchy(parent_object_id);

-- 주석
COMMENT ON MATERIALIZED VIEW v_bi_hierarchy IS 'BI 도구용 계층 구조 뷰 (Navisworks 트리)';


-- ============================================
-- 3. BI 4D 스케줄 뷰
-- ============================================
DROP MATERIALIZED VIEW IF EXISTS v_bi_4d_schedule CASCADE;

CREATE MATERIALIZED VIEW v_bi_4d_schedule AS
SELECT
    p.code AS project_code,
    p.name AS project_name,

    -- 활동 정보
    a.id AS activity_internal_id,
    a.activity_id,
    a.activity_name,
    a.wbs_code,
    a.discipline,

    -- 스케줄
    a.planned_start_date,
    a.planned_end_date,
    a.actual_start_date,
    a.actual_end_date,
    a.duration,
    a.progress,

    -- 연결된 객체 통계
    COUNT(DISTINCT oam.object_id) AS linked_objects_count,
    COUNT(DISTINCT o.object_id) FILTER (WHERE o.source_type = 'revit') AS revit_objects_count,
    COUNT(DISTINCT o.object_id) FILTER (WHERE o.source_type = 'navisworks') AS navis_objects_count,

    -- 카테고리별 객체 수 (상위 5개)
    COUNT(DISTINCT o.object_id) FILTER (WHERE o.category IN ('벽', 'Walls')) AS wall_count,
    COUNT(DISTINCT o.object_id) FILTER (WHERE o.category IN ('기둥', 'Columns')) AS column_count,
    COUNT(DISTINCT o.object_id) FILTER (WHERE o.category IN ('배관', 'Pipes')) AS pipe_count,
    COUNT(DISTINCT o.object_id) FILTER (WHERE o.category IN ('덕트', 'Ducts')) AS duct_count,
    COUNT(DISTINCT o.object_id) FILTER (WHERE o.category IN ('슬라브', 'Floors')) AS slab_count,

    -- 진행 상태
    CASE
        WHEN a.actual_end_date IS NOT NULL THEN 'Completed'
        WHEN a.actual_start_date IS NOT NULL THEN 'In Progress'
        WHEN a.planned_start_date > CURRENT_DATE THEN 'Upcoming'
        WHEN a.planned_start_date <= CURRENT_DATE AND a.planned_end_date >= CURRENT_DATE THEN 'Active'
        ELSE 'Not Started'
    END AS status,

    -- 지연 여부
    CASE
        WHEN a.actual_end_date IS NULL
            AND a.planned_end_date < CURRENT_DATE
            THEN 'Delayed'
        WHEN a.actual_end_date > a.planned_end_date
            THEN 'Completed Late'
        ELSE 'On Track'
    END AS schedule_status,

    -- 타임스탬프
    a.created_at,
    a.updated_at

FROM activities a
JOIN projects p ON a.project_id = p.id
LEFT JOIN object_activity_mappings oam ON a.id = oam.activity_id
LEFT JOIN unified_objects o ON oam.object_id = o.id
GROUP BY
    p.code, p.name, a.id, a.activity_id, a.activity_name, a.wbs_code,
    a.discipline, a.planned_start_date, a.planned_end_date,
    a.actual_start_date, a.actual_end_date, a.duration, a.progress,
    a.created_at, a.updated_at;

-- 인덱스
CREATE UNIQUE INDEX idx_bi_4d_activity ON v_bi_4d_schedule(activity_internal_id);
CREATE INDEX idx_bi_4d_project ON v_bi_4d_schedule(project_code);
CREATE INDEX idx_bi_4d_status ON v_bi_4d_schedule(status);
CREATE INDEX idx_bi_4d_dates ON v_bi_4d_schedule(planned_start_date, planned_end_date);

-- 주석
COMMENT ON MATERIALIZED VIEW v_bi_4d_schedule IS 'BI 도구용 4D 시뮬레이션 뷰';


-- ============================================
-- 4. BI 프로젝트 요약 뷰
-- ============================================
DROP MATERIALIZED VIEW IF EXISTS v_bi_project_summary CASCADE;

CREATE MATERIALIZED VIEW v_bi_project_summary AS
SELECT
    p.code AS project_code,
    p.name AS project_name,
    p.client_name,
    p.building_name,
    p.project_number,
    p.address,

    -- 최신 리비전 정보
    (SELECT MAX(revision_number) FROM revisions WHERE project_id = p.id AND source_type = 'revit') AS latest_revit_revision,
    (SELECT MAX(revision_number) FROM revisions WHERE project_id = p.id AND source_type = 'navisworks') AS latest_navis_revision,
    (SELECT MAX(created_at) FROM revisions WHERE project_id = p.id) AS last_updated,

    -- 객체 통계
    (SELECT COUNT(*) FROM unified_objects o
     JOIN revisions r ON o.revision_id = r.id
     WHERE r.project_id = p.id
       AND o.source_type = 'revit') AS revit_objects_count,

    (SELECT COUNT(*) FROM unified_objects o
     JOIN revisions r ON o.revision_id = r.id
     WHERE r.project_id = p.id
       AND o.source_type = 'navisworks') AS navisworks_objects_count,

    (SELECT COUNT(DISTINCT category) FROM unified_objects o
     JOIN revisions r ON o.revision_id = r.id
     WHERE r.project_id = p.id) AS categories_count,

    -- 매칭 통계
    (SELECT COUNT(*) FROM unified_objects o
     JOIN revisions r ON o.revision_id = r.id
     WHERE r.project_id = p.id
       AND o.source_type = 'revit'
       AND EXISTS (
           SELECT 1 FROM unified_objects n
           WHERE n.source_type = 'navisworks'
             AND n.project_id = o.project_id
             AND (n.properties->>'Element ID')::INTEGER = o.element_id
       )) AS matched_objects_count,

    -- 활동 통계
    (SELECT COUNT(*) FROM activities WHERE project_id = p.id) AS total_activities,
    (SELECT COUNT(*) FROM activities
     WHERE project_id = p.id AND actual_end_date IS NOT NULL) AS completed_activities,
    (SELECT COUNT(*) FROM activities
     WHERE project_id = p.id
       AND actual_start_date IS NOT NULL
       AND actual_end_date IS NULL) AS in_progress_activities,

    -- 진행률
    (SELECT AVG(progress) FROM activities WHERE project_id = p.id) AS overall_progress,

    -- 스케줄 상태
    (SELECT COUNT(*) FROM activities
     WHERE project_id = p.id
       AND actual_end_date IS NULL
       AND planned_end_date < CURRENT_DATE) AS delayed_activities,

    -- 타임스탬프
    p.created_at,
    p.updated_at

FROM projects p
WHERE p.is_active = true;

-- 인덱스
CREATE UNIQUE INDEX idx_bi_summary_project ON v_bi_project_summary(project_code);

-- 주석
COMMENT ON MATERIALIZED VIEW v_bi_project_summary IS 'BI 도구용 프로젝트 요약 뷰';


-- ============================================
-- 5. Materialized View 새로고침 함수
-- ============================================
CREATE OR REPLACE FUNCTION refresh_bi_views()
RETURNS void AS $$
BEGIN
    REFRESH MATERIALIZED VIEW CONCURRENTLY v_bi_objects;
    REFRESH MATERIALIZED VIEW CONCURRENTLY v_bi_hierarchy;
    REFRESH MATERIALIZED VIEW CONCURRENTLY v_bi_4d_schedule;
    REFRESH MATERIALIZED VIEW CONCURRENTLY v_bi_project_summary;

    RAISE NOTICE 'All BI views refreshed successfully';
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION refresh_bi_views IS 'BI 뷰 전체 새로고침 (CONCURRENTLY 사용으로 잠금 최소화)';


-- ============================================
-- 6. 자동 새로고침 트리거
-- ============================================
CREATE OR REPLACE FUNCTION trigger_refresh_bi_views()
RETURNS TRIGGER AS $$
BEGIN
    -- 비동기 새로고침 (실제 운영 환경에서는 pg_cron 사용 권장)
    PERFORM refresh_bi_views();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- 리비전 생성 시 자동 새로고침
DROP TRIGGER IF EXISTS after_revision_insert ON revisions;
CREATE TRIGGER after_revision_insert
AFTER INSERT ON revisions
FOR EACH ROW
EXECUTE FUNCTION trigger_refresh_bi_views();

-- 객체 변경 시 자동 새로고침 (대량 데이터 고려하여 선택적 사용)
-- CREATE TRIGGER after_object_change
-- AFTER INSERT OR UPDATE OR DELETE ON unified_objects
-- FOR EACH STATEMENT
-- EXECUTE FUNCTION trigger_refresh_bi_views();


COMMIT;

-- ============================================
-- BI Views 생성 완료
-- ============================================
-- Power BI / Tableau 연결 방법:
-- 1. PostgreSQL Connector 사용
-- 2. Server: localhost
-- 3. Database: DX_platform
-- 4. 뷰 선택:
--    - v_bi_objects (객체 상세)
--    - v_bi_hierarchy (계층 구조)
--    - v_bi_4d_schedule (4D 스케줄)
--    - v_bi_project_summary (프로젝트 요약)
-- ============================================

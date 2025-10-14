-- ========================================
-- analytics_version_summary 뷰: 버전별 요약 정보
-- ========================================

CREATE OR REPLACE VIEW analytics_version_summary AS
SELECT
    m.model_version,
    m.timestamp,
    m.project_name,
    m.created_by,
    m.description,
    m.total_object_count,

    -- 카테고리별 객체 수 계산
    (
        SELECT JSONB_OBJECT_AGG(category, count)
        FROM (
            SELECT
                COALESCE(category, 'Unknown') AS category,
                COUNT(*) AS count
            FROM objects
            WHERE model_version = m.model_version
            GROUP BY category
        ) AS category_counts
    ) AS category_breakdown,

    -- ActivityId가 있는 객체 수 (4D 연결 가능 객체)
    (
        SELECT COUNT(*)
        FROM objects
        WHERE model_version = m.model_version
          AND activity_id IS NOT NULL
    ) AS linkable_object_count,

    -- 총 관계 수
    (
        SELECT COUNT(*)
        FROM relationships
        WHERE model_version = m.model_version
    ) AS total_relationship_count

FROM metadata m;

-- 주석
COMMENT ON VIEW analytics_version_summary IS '버전별 요약 정보 뷰';

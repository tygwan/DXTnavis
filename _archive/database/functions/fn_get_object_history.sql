-- ========================================
-- fn_get_object_history: 특정 객체의 전체 버전 이력 조회
-- ========================================

CREATE OR REPLACE FUNCTION fn_get_object_history(
    obj_id VARCHAR(255)
)
RETURNS TABLE (
    model_version VARCHAR(255),
    timestamp TIMESTAMP WITH TIME ZONE,
    category VARCHAR(255),
    family VARCHAR(255),
    type VARCHAR(255),
    activity_id VARCHAR(100),
    properties JSONB
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        o.model_version,
        m.timestamp,
        o.category,
        o.family,
        o.type,
        o.activity_id,
        o.properties
    FROM objects o
    INNER JOIN metadata m ON o.model_version = m.model_version
    WHERE o.object_id = obj_id
    ORDER BY m.timestamp;
END;
$$ LANGUAGE plpgsql;

-- 주석
COMMENT ON FUNCTION fn_get_object_history IS '특정 객체의 전체 버전 이력 조회 함수';

-- ========================================
-- fn_compare_versions: 두 버전 간 변경 사항 계산
-- ========================================

CREATE OR REPLACE FUNCTION fn_compare_versions(
    v1 VARCHAR(255),
    v2 VARCHAR(255)
)
RETURNS TABLE (
    object_id VARCHAR(255),
    change_type VARCHAR(20),
    category VARCHAR(255),
    family VARCHAR(255),
    type VARCHAR(255),
    activity_id VARCHAR(100),
    properties_v1 JSONB,
    properties_v2 JSONB
) AS $$
BEGIN
    RETURN QUERY
    -- 추가된 객체 (v2에만 있음)
    SELECT
        o2.object_id,
        'ADDED'::VARCHAR(20) AS change_type,
        o2.category,
        o2.family,
        o2.type,
        o2.activity_id,
        NULL::JSONB AS properties_v1,
        o2.properties AS properties_v2
    FROM objects o2
    WHERE o2.model_version = v2
      AND NOT EXISTS (
          SELECT 1 FROM objects o1
          WHERE o1.model_version = v1 AND o1.object_id = o2.object_id
      )

    UNION ALL

    -- 삭제된 객체 (v1에만 있음)
    SELECT
        o1.object_id,
        'DELETED'::VARCHAR(20) AS change_type,
        o1.category,
        o1.family,
        o1.type,
        o1.activity_id,
        o1.properties AS properties_v1,
        NULL::JSONB AS properties_v2
    FROM objects o1
    WHERE o1.model_version = v1
      AND NOT EXISTS (
          SELECT 1 FROM objects o2
          WHERE o2.model_version = v2 AND o2.object_id = o1.object_id
      )

    UNION ALL

    -- 수정된 객체 (양쪽에 있지만 properties가 다름)
    SELECT
        o1.object_id,
        'MODIFIED'::VARCHAR(20) AS change_type,
        o1.category,
        o1.family,
        o1.type,
        o1.activity_id,
        o1.properties AS properties_v1,
        o2.properties AS properties_v2
    FROM objects o1
    INNER JOIN objects o2 ON o1.object_id = o2.object_id
    WHERE o1.model_version = v1
      AND o2.model_version = v2
      AND o1.properties IS DISTINCT FROM o2.properties;
END;
$$ LANGUAGE plpgsql;

-- 주석
COMMENT ON FUNCTION fn_compare_versions IS '두 버전 간 변경 사항 계산 함수';

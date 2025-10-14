-- ========================================
-- analytics_4d_link_data 뷰: TimeLiner 자동화 매핑 데이터
-- ========================================

CREATE OR REPLACE VIEW analytics_4d_link_data AS
SELECT
    m.model_version,
    o.activity_id,
    o.object_id,
    o.element_id,
    o.category,
    o.family,
    o.type,
    o.properties
FROM metadata m
INNER JOIN objects o ON m.model_version = o.model_version
WHERE o.activity_id IS NOT NULL
ORDER BY o.activity_id, o.category;

-- 주석
COMMENT ON VIEW analytics_4d_link_data IS 'TimeLiner 자동 연결을 위한 매핑 데이터 뷰';

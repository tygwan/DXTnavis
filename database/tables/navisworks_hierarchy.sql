-- ========================================
-- navisworks_hierarchy 테이블: Navisworks 계층구조 및 속성 데이터 저장
-- ========================================
--
-- 용도: Navisworks에서 추출한 모델 계층구조 및 객체 속성 저장
-- 데이터 모델: EAV (Entity-Attribute-Value) 패턴
-- 원본: Hierarchy_*.csv 파일 (DXrevit 플러그인 출력)
--

CREATE TABLE IF NOT EXISTS navisworks_hierarchy (
    -- Primary Key
    id BIGSERIAL PRIMARY KEY,

    -- 계층구조 정보
    object_id UUID NOT NULL,
    parent_id UUID NOT NULL,
    level INTEGER NOT NULL,
    display_name VARCHAR(500),

    -- 속성 정보 (EAV 패턴)
    category VARCHAR(255),
    property_name VARCHAR(255),
    property_value TEXT,

    -- 메타데이터
    model_version VARCHAR(255),
    project_id UUID REFERENCES projects(id) ON DELETE CASCADE,
    revision_id UUID REFERENCES revisions(id) ON DELETE CASCADE,
    ifc_guid VARCHAR(64),
    source_file_path TEXT,
    source_system VARCHAR(50) DEFAULT 'navisworks',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),

    -- 제약조건: 같은 객체의 같은 속성은 한 번만 저장
    CONSTRAINT uq_hierarchy_object_property
        UNIQUE (object_id, property_name, model_version)
);

-- ========================================
-- 인덱스: 쿼리 성능 최적화
-- ========================================

-- 객체 ID 기반 검색 (가장 빈번)
CREATE INDEX IF NOT EXISTS idx_hierarchy_object_id
    ON navisworks_hierarchy(object_id);

-- 부모-자식 관계 탐색
CREATE INDEX IF NOT EXISTS idx_hierarchy_parent_id
    ON navisworks_hierarchy(parent_id);

-- 계층 레벨 필터링
CREATE INDEX IF NOT EXISTS idx_hierarchy_level
    ON navisworks_hierarchy(level);

-- 카테고리별 검색
CREATE INDEX IF NOT EXISTS idx_hierarchy_category
    ON navisworks_hierarchy(category);

-- 속성명 검색 (특정 속성을 가진 객체 찾기)
CREATE INDEX IF NOT EXISTS idx_hierarchy_property_name
    ON navisworks_hierarchy(property_name);

-- 모델 버전별 검색
CREATE INDEX IF NOT EXISTS idx_hierarchy_model_version
    ON navisworks_hierarchy(model_version)
    WHERE model_version IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_hierarchy_project
    ON navisworks_hierarchy(project_id);

CREATE INDEX IF NOT EXISTS idx_hierarchy_revision
    ON navisworks_hierarchy(revision_id);

CREATE INDEX IF NOT EXISTS idx_hierarchy_ifc_guid
    ON navisworks_hierarchy(ifc_guid);

-- 복합 인덱스: 객체별 속성 조회 최적화
CREATE INDEX IF NOT EXISTS idx_hierarchy_object_category
    ON navisworks_hierarchy(object_id, category);

-- Full-text search를 위한 GIN 인덱스 (속성값 검색)
CREATE INDEX IF NOT EXISTS idx_hierarchy_property_value_gin
    ON navisworks_hierarchy USING GIN (to_tsvector('simple', property_value));

-- ========================================
-- 주석
-- ========================================

COMMENT ON TABLE navisworks_hierarchy IS
    'Navisworks 모델의 계층구조 및 객체 속성 데이터 (EAV 패턴)';

COMMENT ON COLUMN navisworks_hierarchy.id IS
    '내부 일련번호 (자동 증가)';

COMMENT ON COLUMN navisworks_hierarchy.object_id IS
    'Navisworks 객체의 고유 GUID';

COMMENT ON COLUMN navisworks_hierarchy.parent_id IS
    '부모 객체의 GUID (계층구조 표현)';

COMMENT ON COLUMN navisworks_hierarchy.level IS
    '계층 깊이 (0: 루트, 1: 1차 자식, ...)';

COMMENT ON COLUMN navisworks_hierarchy.display_name IS
    'Navisworks에 표시되는 객체 이름';

COMMENT ON COLUMN navisworks_hierarchy.category IS
    '속성 카테고리 (예: 항목, 객체, 지오메트리)';

COMMENT ON COLUMN navisworks_hierarchy.property_name IS
    '속성 이름 (예: 이름, 유형, GUID)';

COMMENT ON COLUMN navisworks_hierarchy.property_value IS
    '속성 값 (문자열 형식, 타입 정보 포함 가능)';

COMMENT ON COLUMN navisworks_hierarchy.model_version IS
    '모델 버전 식별자 (선택적, 버전 추적용)';

COMMENT ON COLUMN navisworks_hierarchy.created_at IS
    '데이터 삽입 시각 (UTC)';

-- ========================================
-- 유틸리티 뷰: 객체별 속성 피벗
-- ========================================

CREATE OR REPLACE VIEW v_hierarchy_objects AS
SELECT
    object_id,
    parent_id,
    level,
    display_name,
    model_version,
    project_id,
    revision_id,
    ifc_guid,
    jsonb_object_agg(
        property_name,
        property_value
    ) FILTER (WHERE property_name IS NOT NULL) AS properties,
    MIN(created_at) AS created_at
FROM navisworks_hierarchy
GROUP BY object_id, parent_id, level, display_name, model_version, project_id, revision_id, ifc_guid;

COMMENT ON VIEW v_hierarchy_objects IS
    '객체별로 속성을 집계한 뷰 (EAV → 정규화된 형태, 프로젝트/리비전/IFC GUID 포함)';

COMMENT ON COLUMN navisworks_hierarchy.source_system IS
    'Navisworks 업로드 원본 식별 (기본값: navisworks)';

-- ========================================
-- 유틸리티 함수: 계층 경로 조회
-- ========================================

CREATE OR REPLACE FUNCTION fn_get_hierarchy_path(target_object_id UUID)
RETURNS TABLE (
    object_id UUID,
    parent_id UUID,
    level INTEGER,
    display_name VARCHAR(500),
    path TEXT
) AS $$
WITH RECURSIVE hierarchy_path AS (
    -- 시작점: 대상 객체
    SELECT
        h.object_id,
        h.parent_id,
        h.level,
        h.display_name,
        h.display_name::TEXT AS path
    FROM navisworks_hierarchy h
    WHERE h.object_id = target_object_id
    AND h.property_name = '이름'  -- 이름 속성만 선택

    UNION ALL

    -- 재귀: 부모 객체로 올라가기
    SELECT
        h.object_id,
        h.parent_id,
        h.level,
        h.display_name,
        h.display_name || ' > ' || hp.path
    FROM navisworks_hierarchy h
    INNER JOIN hierarchy_path hp ON h.object_id = hp.parent_id
    WHERE h.property_name = '이름'
    AND h.object_id != '00000000-0000-0000-0000-000000000000'::UUID
)
SELECT * FROM hierarchy_path
ORDER BY level;
$$ LANGUAGE SQL;

COMMENT ON FUNCTION fn_get_hierarchy_path IS
    '특정 객체의 루트까지의 전체 계층 경로를 반환';

-- ========================================
-- 샘플 쿼리 (주석)
-- ========================================

/*
-- 1. 특정 객체의 모든 속성 조회
SELECT property_name, property_value
FROM navisworks_hierarchy
WHERE object_id = '8dd55e0a-2aee-5612-8465-b8f7ff0e7da3'
ORDER BY property_name;

-- 2. 특정 카테고리의 모든 객체 수 조회
SELECT category, COUNT(DISTINCT object_id) AS object_count
FROM navisworks_hierarchy
GROUP BY category
ORDER BY object_count DESC;

-- 3. 루트 객체 조회
SELECT DISTINCT object_id, display_name
FROM navisworks_hierarchy
WHERE level = 0;

-- 4. 특정 객체의 직계 자식 조회
SELECT DISTINCT object_id, display_name, level
FROM navisworks_hierarchy
WHERE parent_id = 'TARGET_PARENT_ID'
ORDER BY display_name;

-- 5. 특정 속성값을 가진 객체 검색
SELECT DISTINCT object_id, display_name
FROM navisworks_hierarchy
WHERE property_name = 'GUID'
AND property_value LIKE '%8dd55e0a%';

-- 6. 객체별 속성 집계 (뷰 사용)
SELECT * FROM v_hierarchy_objects
WHERE object_id = '8dd55e0a-2aee-5612-8465-b8f7ff0e7da3';

-- 7. 계층 경로 조회 (함수 사용)
SELECT * FROM fn_get_hierarchy_path('8dd55e0a-2aee-5612-8465-b8f7ff0e7da3');
*/

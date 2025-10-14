-- ========================================
-- objects 테이블: BIM 객체(Element) 데이터 저장
-- ========================================

CREATE TABLE IF NOT EXISTS objects (
    id BIGSERIAL PRIMARY KEY,
    model_version VARCHAR(255) NOT NULL,
    object_id VARCHAR(255) NOT NULL,
    element_id INTEGER NOT NULL,
    category VARCHAR(255) NOT NULL,
    family VARCHAR(255),
    type VARCHAR(255),
    activity_id VARCHAR(100),
    properties JSONB,
    bounding_box JSONB,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),

    -- 외래키
    CONSTRAINT fk_objects_metadata FOREIGN KEY (model_version)
        REFERENCES metadata(model_version) ON DELETE CASCADE,

    -- 고유 제약 (같은 버전에서 같은 object_id는 중복 불가)
    CONSTRAINT uq_objects_version_objectid UNIQUE (model_version, object_id)
);

-- 인덱스 (쿼리 성능 최적화)
CREATE INDEX IF NOT EXISTS idx_objects_model_version ON objects(model_version);
CREATE INDEX IF NOT EXISTS idx_objects_object_id ON objects(object_id);
CREATE INDEX IF NOT EXISTS idx_objects_category ON objects(category);
CREATE INDEX IF NOT EXISTS idx_objects_activity_id ON objects(activity_id) WHERE activity_id IS NOT NULL;

-- JSONB 컬럼에 대한 GIN 인덱스 (JSON 쿼리 최적화)
CREATE INDEX IF NOT EXISTS idx_objects_properties_gin ON objects USING GIN (properties);
CREATE INDEX IF NOT EXISTS idx_objects_bounding_box_gin ON objects USING GIN (bounding_box);

-- 주석
COMMENT ON TABLE objects IS 'BIM 객체 데이터 테이블';
COMMENT ON COLUMN objects.id IS '내부 일련번호 (자동 증가)';
COMMENT ON COLUMN objects.model_version IS '어떤 버전에 속하는지';
COMMENT ON COLUMN objects.object_id IS 'Revit 고유 식별자 (InstanceGuid 또는 해시)';
COMMENT ON COLUMN objects.element_id IS 'Revit ElementId (정수형, 세션 종속적)';
COMMENT ON COLUMN objects.category IS '카테고리 (예: Walls, Doors)';
COMMENT ON COLUMN objects.family IS '패밀리 이름';
COMMENT ON COLUMN objects.type IS '타입 이름';
COMMENT ON COLUMN objects.activity_id IS '공정 ID (TimeLiner 연결용)';
COMMENT ON COLUMN objects.properties IS '모든 매개변수 (JSON 형식)';
COMMENT ON COLUMN objects.bounding_box IS '3D 바운딩 박스 (JSON 형식)';

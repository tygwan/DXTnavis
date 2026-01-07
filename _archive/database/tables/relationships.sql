-- ========================================
-- relationships 테이블: 객체 간 관계 저장
-- ========================================

CREATE TABLE IF NOT EXISTS relationships (
    id BIGSERIAL PRIMARY KEY,
    model_version VARCHAR(255) NOT NULL,
    source_object_id VARCHAR(255) NOT NULL,
    target_object_id VARCHAR(255) NOT NULL,
    relation_type VARCHAR(50) NOT NULL,
    is_directed BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),

    -- 외래키
    CONSTRAINT fk_relationships_metadata FOREIGN KEY (model_version)
        REFERENCES metadata(model_version) ON DELETE CASCADE,

    -- 고유 제약 (같은 관계 중복 방지)
    CONSTRAINT uq_relationships_version_relation UNIQUE (
        model_version, source_object_id, target_object_id, relation_type
    )
);

-- 인덱스
CREATE INDEX IF NOT EXISTS idx_relationships_model_version ON relationships(model_version);
CREATE INDEX IF NOT EXISTS idx_relationships_source ON relationships(source_object_id);
CREATE INDEX IF NOT EXISTS idx_relationships_target ON relationships(target_object_id);
CREATE INDEX IF NOT EXISTS idx_relationships_type ON relationships(relation_type);

-- 주석
COMMENT ON TABLE relationships IS '객체 간 관계 테이블';
COMMENT ON COLUMN relationships.source_object_id IS '관계의 출발 객체';
COMMENT ON COLUMN relationships.target_object_id IS '관계의 도착 객체';
COMMENT ON COLUMN relationships.relation_type IS '관계 유형 (예: HostedBy, ConnectsTo)';
COMMENT ON COLUMN relationships.is_directed IS '관계 방향성 (true: 단방향, false: 양방향)';

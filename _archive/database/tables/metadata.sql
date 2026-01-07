-- ========================================
-- metadata 테이블: 버전 메타데이터 저장
-- ========================================

CREATE TABLE IF NOT EXISTS metadata (
    model_version VARCHAR(255) PRIMARY KEY,
    timestamp TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    project_name VARCHAR(255) NOT NULL,
    created_by VARCHAR(100) NOT NULL,
    description TEXT,
    total_object_count INTEGER DEFAULT 0,
    revit_file_path TEXT,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),

    -- 제약조건
    CONSTRAINT chk_model_version_format CHECK (model_version ~ '^[A-Za-z0-9_-]+$')
);

-- 인덱스
CREATE INDEX IF NOT EXISTS idx_metadata_timestamp ON metadata(timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_metadata_project_name ON metadata(project_name);
CREATE INDEX IF NOT EXISTS idx_metadata_created_by ON metadata(created_by);

-- 주석
COMMENT ON TABLE metadata IS '버전별 메타데이터 테이블';
COMMENT ON COLUMN metadata.model_version IS '버전 고유 식별자 (Primary Key)';
COMMENT ON COLUMN metadata.timestamp IS '스냅샷 생성 시각 (UTC)';
COMMENT ON COLUMN metadata.project_name IS '프로젝트 이름';
COMMENT ON COLUMN metadata.created_by IS 'BIM 엔지니어 이름';
COMMENT ON COLUMN metadata.description IS '변경 사유 또는 설명';
COMMENT ON COLUMN metadata.total_object_count IS '총 객체 수 (성능 최적화용)';
COMMENT ON COLUMN metadata.revit_file_path IS 'Revit 파일 경로 (추적용)';

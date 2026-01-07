-- ========================================
-- AWP 2025 통합 데이터베이스 스키마 v2.0
-- ========================================
-- 목적: Revit + Navisworks 통합 데이터 관리
-- 작성일: 2025-10-19
--
-- 주요 변경사항:
-- 1. 프로젝트 기반 데이터 조직화
-- 2. 리비전 관리 시스템 추가
-- 3. 통합 객체 테이블 (Revit + Navisworks)
-- 4. 계층 구조 완벽 지원
-- ========================================

BEGIN;

-- ============================================
-- 1. Projects 테이블 (프로젝트 마스터)
-- ============================================
CREATE TABLE IF NOT EXISTS projects (
    -- 식별자
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(50) NOT NULL UNIQUE,        -- PIPE_TEST, SNOWDON_TOWERS
    name VARCHAR(255) NOT NULL,              -- 배관테스트, Snowdon Towers

    -- 파일 정보
    revit_file_name VARCHAR(255),            -- 배관테스트.rvt
    revit_file_path TEXT,                    -- C:\Users\...\배관테스트.rvt

    -- 프로젝트 정보 (Revit ProjectInfo)
    project_number VARCHAR(100),             -- 프로젝트 번호
    client_name VARCHAR(255),                -- 소유자
    address TEXT,                            -- 주소
    building_name VARCHAR(255),              -- 건물명

    -- 위치 정보 (Navisworks Location)
    latitude DOUBLE PRECISION,               -- 위도
    longitude DOUBLE PRECISION,              -- 경도
    elevation DOUBLE PRECISION,              -- 고도 (m)
    timezone INTEGER,                        -- 시간대 offset

    -- 메타데이터
    created_by VARCHAR(100) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    is_active BOOLEAN DEFAULT true,

    -- 추가 정보 (JSONB)
    metadata JSONB DEFAULT '{}'::jsonb,

    CONSTRAINT chk_project_code CHECK (code ~ '^[A-Z0-9_가-힣]+$')
);

-- 인덱스
CREATE INDEX IF NOT EXISTS idx_projects_code ON projects(code);
CREATE INDEX IF NOT EXISTS idx_projects_active ON projects(is_active);

-- 주석
COMMENT ON TABLE projects IS '프로젝트 마스터 테이블 - 각 BIM 프로젝트의 기본 정보';
COMMENT ON COLUMN projects.code IS '프로젝트 코드 (파일명 기반 자동 생성, 예: PIPE_TEST)';
COMMENT ON COLUMN projects.metadata IS '추가 프로젝트 정보 (JSON 형식)';


-- ============================================
-- 2. Revisions 테이블 (리비전 이력)
-- ============================================
CREATE TABLE IF NOT EXISTS revisions (
    -- 식별자
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,

    -- 리비전 정보
    revision_number INTEGER NOT NULL,        -- 1, 2, 3, ... (자동 증가)
    version_tag VARCHAR(50),                 -- v1.0, RC1, DESIGN_PHASE
    description TEXT,                        -- 변경 설명

    -- 소스 정보
    source_type VARCHAR(20) NOT NULL,        -- 'revit' | 'navisworks' | 'both'
    source_file_path TEXT,                   -- 실제 파일 경로
    source_file_hash VARCHAR(64),            -- 파일 무결성 검증 (SHA256)

    -- 통계 정보
    total_objects INTEGER DEFAULT 0,
    total_categories INTEGER DEFAULT 0,
    revit_objects INTEGER DEFAULT 0,
    navisworks_objects INTEGER DEFAULT 0,

    -- 변경 추적
    parent_revision_id UUID REFERENCES revisions(id),
    changes_summary JSONB DEFAULT '{}'::jsonb,  -- {added: 10, modified: 5, deleted: 2}

    -- 메타데이터
    created_by VARCHAR(100) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    metadata JSONB DEFAULT '{}'::jsonb,

    CONSTRAINT uq_project_revision UNIQUE (project_id, revision_number, source_type),
    CONSTRAINT chk_source_type CHECK (source_type IN ('revit', 'navisworks', 'both'))
);

-- 인덱스
CREATE INDEX IF NOT EXISTS idx_revisions_project ON revisions(project_id);
CREATE INDEX IF NOT EXISTS idx_revisions_number ON revisions(revision_number);
CREATE INDEX IF NOT EXISTS idx_revisions_source ON revisions(source_type);

-- 주석
COMMENT ON TABLE revisions IS '리비전 이력 테이블 - 프로젝트 버전 관리';
COMMENT ON COLUMN revisions.revision_number IS '리비전 번호 (프로젝트 내 순차 증가)';
COMMENT ON COLUMN revisions.changes_summary IS '변경 내역 요약 (JSON)';


-- ============================================
-- 3. Unified Objects 테이블 (통합 객체)
-- ============================================
CREATE TABLE IF NOT EXISTS unified_objects (
    id BIGSERIAL PRIMARY KEY,

    -- 프로젝트 및 리비전
    project_id UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    revision_id UUID NOT NULL REFERENCES revisions(id) ON DELETE CASCADE,

    -- 객체 식별자
    object_id UUID NOT NULL,                 -- Navisworks GUID 또는 Revit UniqueId
    element_id INTEGER,                      -- Revit Element ID (Navisworks는 NULL 가능)
    source_type VARCHAR(20) NOT NULL,        -- 'revit' | 'navisworks'

    -- ⭐ 계층 정보 (양쪽 모두 지원)
    parent_object_id UUID,                   -- 부모 객체 ID
    level INTEGER DEFAULT 0,                 -- 계층 깊이 (0: 루트)
    display_name VARCHAR(500),               -- 표시 이름
    spatial_path TEXT,                       -- 공간 경로: Building > Level > Room

    -- 분류 정보
    category VARCHAR(255) NOT NULL,
    family VARCHAR(255),                     -- Revit만
    type VARCHAR(255),                       -- Revit만

    -- 스케줄 연계
    activity_id VARCHAR(100),                -- 4D 시뮬레이션용 Activity ID

    -- 속성 데이터 (JSONB)
    properties JSONB NOT NULL DEFAULT '{}'::jsonb,
    bounding_box JSONB,                      -- {MinX, MinY, MinZ, MaxX, MaxY, MaxZ}

    -- 상태 추적
    change_type VARCHAR(20) DEFAULT 'added', -- 'added' | 'modified' | 'deleted' | 'unchanged'
    previous_object_id BIGINT REFERENCES unified_objects(id),

    -- 메타데이터
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),

    CONSTRAINT chk_object_source_type CHECK (source_type IN ('revit', 'navisworks')),
    CONSTRAINT chk_change_type CHECK (change_type IN ('added', 'modified', 'deleted', 'unchanged')),
    CONSTRAINT uq_revision_object UNIQUE (revision_id, object_id, source_type)
);

-- 인덱스 (성능 최적화)
CREATE INDEX IF NOT EXISTS idx_unified_objects_project ON unified_objects(project_id);
CREATE INDEX IF NOT EXISTS idx_unified_objects_revision ON unified_objects(revision_id);
CREATE INDEX IF NOT EXISTS idx_unified_objects_object_id ON unified_objects(object_id);
CREATE INDEX IF NOT EXISTS idx_unified_objects_parent ON unified_objects(parent_object_id);
CREATE INDEX IF NOT EXISTS idx_unified_objects_level ON unified_objects(level);
CREATE INDEX IF NOT EXISTS idx_unified_objects_category ON unified_objects(category);
CREATE INDEX IF NOT EXISTS idx_unified_objects_element_id ON unified_objects(element_id) WHERE element_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_unified_objects_activity ON unified_objects(activity_id) WHERE activity_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS idx_unified_objects_source ON unified_objects(source_type);

-- JSONB 인덱스 (속성 검색 최적화)
CREATE INDEX IF NOT EXISTS idx_unified_objects_properties ON unified_objects USING GIN (properties);

-- 주석
COMMENT ON TABLE unified_objects IS '통합 객체 테이블 - Revit 및 Navisworks 데이터 통합';
COMMENT ON COLUMN unified_objects.object_id IS 'Navisworks InstanceGuid 또는 Revit UniqueId';
COMMENT ON COLUMN unified_objects.parent_object_id IS '부모 객체 ID (계층 구조)';
COMMENT ON COLUMN unified_objects.level IS '계층 깊이 (0: 루트, 1: 1차 자식, ...)';
COMMENT ON COLUMN unified_objects.properties IS '모든 속성을 JSONB로 저장 (유연한 스키마)';


-- ============================================
-- 4. Activities 테이블 (스케줄/4D)
-- ============================================
CREATE TABLE IF NOT EXISTS activities (
    id BIGSERIAL PRIMARY KEY,
    project_id UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,

    -- 활동 정보
    activity_id VARCHAR(100) NOT NULL,       -- WBS 코드 또는 Activity ID
    activity_name VARCHAR(255) NOT NULL,     -- 작업명

    -- 스케줄 정보
    planned_start_date DATE,
    planned_end_date DATE,
    actual_start_date DATE,
    actual_end_date DATE,
    duration INTEGER,                        -- 일수
    progress DECIMAL(5, 2) DEFAULT 0.00,     -- 진행률 (%)

    -- 분류
    wbs_code VARCHAR(100),                   -- WBS 코드
    discipline VARCHAR(50),                  -- 공종 (건축, 전기, 기계 등)

    -- 메타데이터
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    metadata JSONB DEFAULT '{}'::jsonb,

    CONSTRAINT uq_project_activity UNIQUE (project_id, activity_id)
);

-- 인덱스
CREATE INDEX IF NOT EXISTS idx_activities_project ON activities(project_id);
CREATE INDEX IF NOT EXISTS idx_activities_activity_id ON activities(activity_id);
CREATE INDEX IF NOT EXISTS idx_activities_dates ON activities(planned_start_date, planned_end_date);

-- 주석
COMMENT ON TABLE activities IS '스케줄 활동 테이블 - 4D 시뮬레이션용';
COMMENT ON COLUMN activities.activity_id IS 'Activity ID (WBS 코드)';
COMMENT ON COLUMN activities.progress IS '진행률 (0.00 ~ 100.00)';


-- ============================================
-- 5. Object-Activity 매핑 테이블
-- ============================================
CREATE TABLE IF NOT EXISTS object_activity_mappings (
    id BIGSERIAL PRIMARY KEY,
    object_id BIGINT NOT NULL REFERENCES unified_objects(id) ON DELETE CASCADE,
    activity_id BIGINT NOT NULL REFERENCES activities(id) ON DELETE CASCADE,

    -- 매핑 정보
    mapping_type VARCHAR(50) DEFAULT 'manual',  -- 'direct' | 'inherited' | 'manual'
    confidence DECIMAL(3, 2) DEFAULT 1.00,      -- 0.00 ~ 1.00 (매칭 신뢰도)

    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),

    CONSTRAINT uq_object_activity UNIQUE (object_id, activity_id)
);

-- 인덱스
CREATE INDEX IF NOT EXISTS idx_oam_object ON object_activity_mappings(object_id);
CREATE INDEX IF NOT EXISTS idx_oam_activity ON object_activity_mappings(activity_id);

-- 주석
COMMENT ON TABLE object_activity_mappings IS '객체-활동 매핑 테이블 - 4D 연결';
COMMENT ON COLUMN object_activity_mappings.mapping_type IS '매핑 유형 (direct: 직접, inherited: 상속, manual: 수동)';


-- ============================================
-- 6. 자동 업데이트 트리거
-- ============================================

-- projects.updated_at 자동 업데이트
CREATE OR REPLACE FUNCTION update_project_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trigger_update_project_timestamp
BEFORE UPDATE ON projects
FOR EACH ROW
EXECUTE FUNCTION update_project_timestamp();

-- activities.updated_at 자동 업데이트
CREATE OR REPLACE FUNCTION update_activity_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trigger_update_activity_timestamp
BEFORE UPDATE ON activities
FOR EACH ROW
EXECUTE FUNCTION update_activity_timestamp();


-- ============================================
-- 7. 유틸리티 함수: 다음 리비전 번호 생성
-- ============================================
CREATE OR REPLACE FUNCTION get_next_revision_number(p_project_id UUID, p_source_type VARCHAR)
RETURNS INTEGER AS $$
DECLARE
    next_number INTEGER;
BEGIN
    SELECT COALESCE(MAX(revision_number), 0) + 1
    INTO next_number
    FROM revisions
    WHERE project_id = p_project_id
      AND source_type = p_source_type;

    RETURN next_number;
END;
$$ LANGUAGE plpgsql;

COMMENT ON FUNCTION get_next_revision_number IS '프로젝트의 다음 리비전 번호 반환';


-- ============================================
-- 8. 유틸리티 함수: 계층 경로 생성
-- ============================================
CREATE OR REPLACE FUNCTION get_hierarchy_path(p_object_id UUID, p_revision_id UUID)
RETURNS TEXT AS $$
WITH RECURSIVE hierarchy AS (
    -- 시작점: 대상 객체
    SELECT
        object_id,
        parent_object_id,
        level,
        display_name,
        display_name::TEXT AS path
    FROM unified_objects
    WHERE object_id = p_object_id
      AND revision_id = p_revision_id
      AND source_type = 'navisworks'

    UNION ALL

    -- 재귀: 부모 객체로 올라가기
    SELECT
        u.object_id,
        u.parent_object_id,
        u.level,
        u.display_name,
        u.display_name || ' > ' || h.path
    FROM unified_objects u
    INNER JOIN hierarchy h ON u.object_id = h.parent_object_id
    WHERE u.revision_id = p_revision_id
      AND u.source_type = 'navisworks'
      AND u.object_id != '00000000-0000-0000-0000-000000000000'::UUID
)
SELECT path FROM hierarchy
ORDER BY level DESC
LIMIT 1;
$$ LANGUAGE SQL;

COMMENT ON FUNCTION get_hierarchy_path IS '특정 객체의 전체 계층 경로 반환 (예: Building > Level > Room)';


-- ============================================
-- 9. 기존 데이터 마이그레이션 준비
-- ============================================

-- 기존 테이블 백업 (선택사항)
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'objects') THEN
        CREATE TABLE IF NOT EXISTS objects_backup AS SELECT * FROM objects;
        CREATE TABLE IF NOT EXISTS metadata_backup AS SELECT * FROM metadata;
        CREATE TABLE IF NOT EXISTS relationships_backup AS SELECT * FROM relationships;

        RAISE NOTICE '기존 테이블 백업 완료: objects_backup, metadata_backup, relationships_backup';
    END IF;
END $$;


COMMIT;

-- ============================================
-- 스키마 생성 완료
-- ============================================
-- 다음 단계:
-- 1. BI 뷰 생성: database/migrations/003_bi_views.sql
-- 2. 기존 데이터 마이그레이션: scripts/migrate_existing_data.py
-- 3. 플러그인 수정: DXrevit, DXnavis
-- ============================================

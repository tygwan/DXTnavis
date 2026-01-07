# Phase 3: PostgreSQL 데이터베이스 스키마 설계

## 문서 목적
PostgreSQL 데이터베이스의 스키마 설계, 테이블 생성, 인덱스, 뷰, 함수 등 모든 데이터베이스 관련 구현을 다룹니다.

---

## 1. 개요

### 1.1. 데이터베이스 역할
**핵심 책임**: 중앙 데이터 저장소 및 분석 계층

**주요 기능**:
1. 원시 데이터 영구 보존 (불변성)
2. 버전 이력 관리
3. 분석용 데이터 가공 (Views & Functions)
4. 데이터 무결성 보장

### 1.2. 기술 스펙
- **데이터베이스**: PostgreSQL 15+
- **인코딩**: UTF-8
- **타임존**: UTC
- **접근**: FastAPI 서버만 직접 접근 허용

---

## 2. 데이터베이스 구조

### 2.1. 스키마 구성
```
dx_platform (데이터베이스)
├── public (기본 스키마)
│   ├── raw_data (원시 데이터 테이블)
│   │   ├── metadata
│   │   ├── objects
│   │   └── relationships
│   ├── analytics (분석용 뷰)
│   │   ├── analytics_version_summary
│   │   ├── analytics_version_delta
│   │   └── analytics_4d_link_data
│   └── functions (저장 프로시저)
│       ├── fn_compare_versions()
│       └── fn_get_object_history()
```

---

## 3. 원시 데이터 테이블 (Raw Data Tables)

### 3.1. metadata 테이블

**목적**: 버전 메타데이터 저장

**DDL**:
```sql
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
CREATE INDEX idx_metadata_timestamp ON metadata(timestamp DESC);
CREATE INDEX idx_metadata_project_name ON metadata(project_name);
CREATE INDEX idx_metadata_created_by ON metadata(created_by);

-- 주석
COMMENT ON TABLE metadata IS '버전별 메타데이터 테이블';
COMMENT ON COLUMN metadata.model_version IS '버전 고유 식별자 (Primary Key)';
COMMENT ON COLUMN metadata.timestamp IS '스냅샷 생성 시각 (UTC)';
COMMENT ON COLUMN metadata.project_name IS '프로젝트 이름';
COMMENT ON COLUMN metadata.created_by IS 'BIM 엔지니어 이름';
COMMENT ON COLUMN metadata.description IS '변경 사유 또는 설명';
COMMENT ON COLUMN metadata.total_object_count IS '총 객체 수 (성능 최적화용)';
COMMENT ON COLUMN metadata.revit_file_path IS 'Revit 파일 경로 (추적용)';
```

**주의사항**:
- ✅ model_version은 고유해야 함 (PRIMARY KEY)
- ✅ timestamp는 UTC 사용 (TIME ZONE 포함)
- ✅ model_version 형식 제약 (영문, 숫자, -, _ 만 허용)
- ❌ UPDATE/DELETE 작업 금지 (불변성 보장)

### 3.2. objects 테이블

**목적**: BIM 객체(Element) 데이터 저장

**DDL**:
```sql
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
CREATE INDEX idx_objects_model_version ON objects(model_version);
CREATE INDEX idx_objects_object_id ON objects(object_id);
CREATE INDEX idx_objects_category ON objects(category);
CREATE INDEX idx_objects_activity_id ON objects(activity_id) WHERE activity_id IS NOT NULL;

-- JSONB 컬럼에 대한 GIN 인덱스 (JSON 쿼리 최적화)
CREATE INDEX idx_objects_properties_gin ON objects USING GIN (properties);
CREATE INDEX idx_objects_bounding_box_gin ON objects USING GIN (bounding_box);

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
```

**JSONB 예시**:
```json
// properties 컬럼 예시
{
  "Length": 5000.0,
  "Width": 200.0,
  "Height": 3000.0,
  "Volume": 3.0,
  "Material": "Concrete",
  "Level": "Level 1",
  "Comments": "외벽"
}

// bounding_box 컬럼 예시
{
  "MinX": 0.0,
  "MinY": 0.0,
  "MinZ": 0.0,
  "MaxX": 5000.0,
  "MaxY": 200.0,
  "MaxZ": 3000.0
}
```

**주의사항**:
- ✅ object_id는 버전 간 동일한 객체를 추적하는 유일한 키
- ✅ JSONB 타입 사용 (JSON보다 빠른 쿼리)
- ✅ GIN 인덱스로 JSONB 쿼리 성능 향상
- ❌ element_id는 다른 Revit 세션에서 변경될 수 있으므로 절대 유일 키로 사용 금지

### 3.3. relationships 테이블

**목적**: 객체 간 관계 저장

**DDL**:
```sql
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
CREATE INDEX idx_relationships_model_version ON relationships(model_version);
CREATE INDEX idx_relationships_source ON relationships(source_object_id);
CREATE INDEX idx_relationships_target ON relationships(target_object_id);
CREATE INDEX idx_relationships_type ON relationships(relation_type);

-- 주석
COMMENT ON TABLE relationships IS '객체 간 관계 테이블';
COMMENT ON COLUMN relationships.source_object_id IS '관계의 출발 객체';
COMMENT ON COLUMN relationships.target_object_id IS '관계의 도착 객체';
COMMENT ON COLUMN relationships.relation_type IS '관계 유형 (예: HostedBy, ConnectsTo)';
COMMENT ON COLUMN relationships.is_directed IS '관계 방향성 (true: 단방향, false: 양방향)';
```

**관계 유형 예시**:
- `HostedBy`: 문이 벽에 호스팅됨
- `ConnectsTo`: 파이프가 다른 파이프에 연결됨
- `Contains`: 벽이 창문을 포함
- `DependsOn`: 객체가 다른 객체에 의존

---

## 4. 분석용 뷰 (Analytics Views)

### 4.1. analytics_version_summary 뷰

**목적**: 버전별 요약 정보 제공

**DDL**:
```sql
CREATE OR REPLACE VIEW analytics_version_summary AS
SELECT
    m.model_version,
    m.timestamp,
    m.project_name,
    m.created_by,
    m.description,
    m.total_object_count,

    -- 카테고리별 객체 수
    JSONB_OBJECT_AGG(
        COALESCE(o.category, 'Unknown'),
        category_counts.count
    ) AS category_breakdown,

    -- ActivityId가 있는 객체 수 (4D 연결 가능 객체)
    COUNT(DISTINCT o.object_id) FILTER (WHERE o.activity_id IS NOT NULL) AS linkable_object_count,

    -- 총 관계 수
    COUNT(DISTINCT r.id) AS total_relationship_count

FROM metadata m
LEFT JOIN objects o ON m.model_version = o.model_version
LEFT JOIN relationships r ON m.model_version = r.model_version
LEFT JOIN LATERAL (
    SELECT category, COUNT(*) AS count
    FROM objects
    WHERE model_version = m.model_version
    GROUP BY category
) AS category_counts ON TRUE
GROUP BY m.model_version, m.timestamp, m.project_name, m.created_by, m.description, m.total_object_count;

-- 주석
COMMENT ON VIEW analytics_version_summary IS '버전별 요약 정보 뷰';
```

**사용 예시**:
```sql
SELECT * FROM analytics_version_summary
WHERE project_name = 'MyProject'
ORDER BY timestamp DESC;
```

### 4.2. analytics_version_delta 뷰

**목적**: 두 버전 간 변경 사항 계산

**DDL (함수 방식)**:
```sql
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
```

**사용 예시**:
```sql
SELECT * FROM fn_compare_versions('v1.0.0', 'v1.0.1')
WHERE change_type = 'ADDED';
```

### 4.3. analytics_4d_link_data 뷰

**목적**: TimeLiner 자동화를 위한 매핑 데이터 제공

**DDL**:
```sql
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
```

**사용 예시**:
```sql
SELECT * FROM analytics_4d_link_data
WHERE model_version = 'v1.0.0'
  AND activity_id = 'A1010';
```

---

## 5. 유틸리티 함수

### 5.1. fn_get_object_history 함수

**목적**: 특정 객체의 모든 버전 이력 조회

**DDL**:
```sql
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
```

**사용 예시**:
```sql
SELECT * FROM fn_get_object_history('12345678-abcd-1234-abcd-1234567890ab');
```

---

## 6. 데이터 무결성 및 보안

### 6.1. 트리거 (불변성 보장)

**목적**: 원시 데이터 테이블에서 UPDATE/DELETE 방지

**DDL**:
```sql
-- UPDATE 방지 트리거 함수
CREATE OR REPLACE FUNCTION prevent_raw_data_update()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'UPDATE operation is not allowed on raw data tables. All changes must be recorded as new versions.';
END;
$$ LANGUAGE plpgsql;

-- DELETE 방지 트리거 함수
CREATE OR REPLACE FUNCTION prevent_raw_data_delete()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'DELETE operation is not allowed on raw data tables. Use logical deletion or versioning instead.';
END;
$$ LANGUAGE plpgsql;

-- metadata 테이블에 트리거 적용
CREATE TRIGGER trg_prevent_metadata_update
BEFORE UPDATE ON metadata
FOR EACH ROW
EXECUTE FUNCTION prevent_raw_data_update();

CREATE TRIGGER trg_prevent_metadata_delete
BEFORE DELETE ON metadata
FOR EACH ROW
EXECUTE FUNCTION prevent_raw_data_delete();

-- objects 테이블에 트리거 적용
CREATE TRIGGER trg_prevent_objects_update
BEFORE UPDATE ON objects
FOR EACH ROW
EXECUTE FUNCTION prevent_raw_data_update();

CREATE TRIGGER trg_prevent_objects_delete
BEFORE DELETE ON objects
FOR EACH ROW
EXECUTE FUNCTION prevent_raw_data_delete();

-- relationships 테이블에 트리거 적용
CREATE TRIGGER trg_prevent_relationships_update
BEFORE UPDATE ON relationships
FOR EACH ROW
EXECUTE FUNCTION prevent_raw_data_update();

CREATE TRIGGER trg_prevent_relationships_delete
BEFORE DELETE ON relationships
FOR EACH ROW
EXECUTE FUNCTION prevent_raw_data_delete();
```

**주의사항**:
- ✅ 개발 환경에서는 트리거를 비활성화하여 테스트 용이성 확보
- ✅ 운영 환경에서는 반드시 트리거 활성화
- ⚠️ CASCADE DELETE는 의도적 버전 삭제를 위해 허용 (관리자만)

### 6.2. 역할 및 권한 관리

**DDL**:
```sql
-- API 서버 전용 역할 생성
CREATE ROLE dx_api_role LOGIN PASSWORD 'strong_password_here';

-- 원시 데이터 테이블에 대한 권한
GRANT SELECT, INSERT ON TABLE metadata TO dx_api_role;
GRANT SELECT, INSERT ON TABLE objects TO dx_api_role;
GRANT SELECT, INSERT ON TABLE relationships TO dx_api_role;

-- 시퀀스에 대한 권한 (SERIAL 컬럼 사용)
GRANT USAGE, SELECT ON SEQUENCE objects_id_seq TO dx_api_role;
GRANT USAGE, SELECT ON SEQUENCE relationships_id_seq TO dx_api_role;

-- 분석용 뷰에 대한 읽기 권한
GRANT SELECT ON analytics_version_summary TO dx_api_role;
GRANT SELECT ON analytics_4d_link_data TO dx_api_role;

-- 함수 실행 권한
GRANT EXECUTE ON FUNCTION fn_compare_versions TO dx_api_role;
GRANT EXECUTE ON FUNCTION fn_get_object_history TO dx_api_role;

-- 읽기 전용 역할 (Power BI 등 분석 도구용)
CREATE ROLE dx_readonly_role LOGIN PASSWORD 'readonly_password_here';

GRANT SELECT ON TABLE metadata TO dx_readonly_role;
GRANT SELECT ON TABLE objects TO dx_readonly_role;
GRANT SELECT ON TABLE relationships TO dx_readonly_role;
GRANT SELECT ON analytics_version_summary TO dx_readonly_role;
GRANT SELECT ON analytics_4d_link_data TO dx_readonly_role;
GRANT EXECUTE ON FUNCTION fn_compare_versions TO dx_readonly_role;
GRANT EXECUTE ON FUNCTION fn_get_object_history TO dx_readonly_role;
```

**주의사항**:
- ⚠️ 비밀번호는 환경 변수로 관리
- ⚠️ 정기적인 비밀번호 변경
- ⚠️ 최소 권한 원칙 준수

---

## 7. 초기화 스크립트

### 7.1. 전체 스키마 생성 스크립트

**init_database.sql**:
```sql
-- ========================================
-- DX Platform 데이터베이스 초기화 스크립트
-- ========================================

-- 데이터베이스 생성 (psql 명령줄에서 실행)
-- CREATE DATABASE dx_platform
-- WITH ENCODING='UTF8'
--      OWNER=postgres
--      LC_COLLATE='en_US.UTF-8'
--      LC_CTYPE='en_US.UTF-8'
--      TEMPLATE=template0;

-- \c dx_platform

-- 1. 원시 데이터 테이블 생성
\i tables/metadata.sql
\i tables/objects.sql
\i tables/relationships.sql

-- 2. 분석용 뷰 생성
\i views/analytics_version_summary.sql
\i views/analytics_4d_link_data.sql

-- 3. 함수 생성
\i functions/fn_compare_versions.sql
\i functions/fn_get_object_history.sql

-- 4. 트리거 생성 (불변성 보장)
\i triggers/prevent_raw_data_modification.sql

-- 5. 역할 및 권한 설정
\i security/roles_and_permissions.sql

-- 완료 메시지
SELECT 'DX Platform 데이터베이스 초기화 완료!' AS message;
```

---

## 8. 백업 및 복구 전략

### 8.1. 백업 스크립트

**backup.sh** (Linux):
```bash
#!/bin/bash
# DX Platform 데이터베이스 백업 스크립트

BACKUP_DIR="/var/backups/dx_platform"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
BACKUP_FILE="$BACKUP_DIR/dx_platform_$TIMESTAMP.sql.gz"

# 디렉토리 생성
mkdir -p $BACKUP_DIR

# 백업 실행 (압축)
pg_dump -U postgres -d dx_platform | gzip > $BACKUP_FILE

# 30일 이상 된 백업 파일 삭제
find $BACKUP_DIR -name "*.sql.gz" -mtime +30 -delete

echo "백업 완료: $BACKUP_FILE"
```

**backup.bat** (Windows):
```batch
@echo off
REM DX Platform 데이터베이스 백업 스크립트

SET BACKUP_DIR=C:\Backups\dx_platform
SET TIMESTAMP=%date:~0,4%%date:~5,2%%date:~8,2%_%time:~0,2%%time:~3,2%%time:~6,2%
SET BACKUP_FILE=%BACKUP_DIR%\dx_platform_%TIMESTAMP%.sql

REM 디렉토리 생성
if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"

REM 백업 실행
"C:\Program Files\PostgreSQL\15\bin\pg_dump.exe" -U postgres -d dx_platform > "%BACKUP_FILE%"

echo 백업 완료: %BACKUP_FILE%
```

### 8.2. 복구 스크립트

**restore.sh** (Linux):
```bash
#!/bin/bash
# DX Platform 데이터베이스 복구 스크립트

BACKUP_FILE=$1

if [ -z "$BACKUP_FILE" ]; then
    echo "사용법: ./restore.sh <백업파일경로>"
    exit 1
fi

# 압축 해제하며 복구
gunzip -c $BACKUP_FILE | psql -U postgres -d dx_platform

echo "복구 완료"
```

---

## 9. 성능 최적화

### 9.1. 인덱스 최적화

**주기적인 인덱스 재구성**:
```sql
-- 인덱스 통계 업데이트
ANALYZE metadata;
ANALYZE objects;
ANALYZE relationships;

-- 필요 시 인덱스 재구성
REINDEX TABLE objects;
REINDEX TABLE relationships;
```

### 9.2. VACUUM 및 ANALYZE

**정기적인 유지보수**:
```sql
-- VACUUM으로 공간 회수
VACUUM FULL metadata;
VACUUM FULL objects;
VACUUM FULL relationships;

-- ANALYZE로 통계 업데이트
ANALYZE;
```

---

## 10. 모니터링 쿼리

### 10.1. 데이터베이스 크기 모니터링

```sql
SELECT
    pg_size_pretty(pg_database_size('dx_platform')) AS total_size,
    pg_size_pretty(pg_total_relation_size('metadata')) AS metadata_size,
    pg_size_pretty(pg_total_relation_size('objects')) AS objects_size,
    pg_size_pretty(pg_total_relation_size('relationships')) AS relationships_size;
```

### 10.2. 느린 쿼리 모니터링

```sql
SELECT
    query,
    calls,
    total_time / 1000 AS total_seconds,
    mean_time / 1000 AS mean_seconds
FROM pg_stat_statements
ORDER BY total_time DESC
LIMIT 10;
```

---

## 11. 주의사항 및 금지사항

### 11.1. ✅ 해야 할 것

**데이터 관리**:
- ✅ 모든 timestamp는 UTC 사용
- ✅ 인덱스 정기 유지보수
- ✅ 백업 스케줄 설정 (일일 백업 권장)
- ✅ 로그 모니터링

**쿼리 최적화**:
- ✅ EXPLAIN ANALYZE로 쿼리 성능 분석
- ✅ 적절한 인덱스 사용
- ✅ JSONB 쿼리 시 GIN 인덱스 활용

### 11.2. ❌ 하지 말아야 할 것

**데이터 무결성**:
- ❌ 원시 데이터 테이블에서 UPDATE/DELETE 실행
- ❌ 트리거 비활성화 (운영 환경)
- ❌ 외래키 제약조건 삭제

**성능**:
- ❌ 불필요한 SELECT * 사용
- ❌ 대용량 JSONB 데이터에 대한 비효율적 쿼리
- ❌ 인덱스 없는 대용량 테이블 조인

### 11.3. ⚠️ 관리자가 직접 제어해야 할 것

**시스템 관리자 책임**:
- ⚠️ 백업 및 복구 테스트
- ⚠️ 디스크 공간 모니터링
- ⚠️ 데이터베이스 성능 튜닝
- ⚠️ 역할 및 권한 관리
- ⚠️ 분석용 뷰 및 함수 생성/수정

---

## 12. 다음 단계

PostgreSQL 스키마 구축 완료 후:
1. Phase 4 (FastAPI 서버 개발) 참조
2. Phase 5 (DXnavis 개발) 참조
3. 통합 테스트 수행

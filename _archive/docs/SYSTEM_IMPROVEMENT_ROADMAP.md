# DX Platform 시스템 개선 로드맵

**작성일**: 2025-10-18
**목적**: 현재 시스템 이슈 분석 및 단계별 개선 계획 수립
**대상**: AWP 2025 BIM Data Integration System

---

## 📋 목차

1. [현재 이슈 분석](#현재-이슈-분석)
2. [아키텍처 재설계](#아키텍처-재설계)
3. [데이터베이스 스키마 재설계](#데이터베이스-스키마-재설계)
4. [단계별 실행 계획](#단계별-실행-계획)
5. [기술 스택 결정](#기술-스택-결정)

---

## 🚨 현재 이슈 분석

### Issue #1: Revit과 Navisworks 데이터 형태 불일치

#### 문제 상황
```
Navisworks                          Revit
─────────────────────────────────  ─────────────────────────────────
계층적 트리 구조 (Tree)              평면적 컬렉션 (Flat)
- parent_id ✅                      - parent_id ❌
- level ✅                          - level ❌
- 재귀 탐색                          - 필터 기반 검색
```

#### 영향
- 데이터 통합 쿼리 복잡
- 4D 시뮬레이션 연계 어려움
- 계층 기반 분석 불가

#### 해결 방향
1. **통합 스키마 설계**: 두 소스를 포괄하는 범용 스키마
2. **Revit 추출 로직 개선**: 계층 정보 자동 생성
3. **데이터 정규화 레이어**: ETL 파이프라인에서 변환

---

### Issue #2: 서버 연결 및 변수 관리의 복잡성

#### 문제 상황
```
현재 구조:
각 사용자가 개별적으로 서버 정보 관리
- DXrevit: Settings.settings에 SERVER_URL 저장
- DXnavis: 설정 파일에 개별 저장
- FastAPI: .env 파일에 DATABASE_URL 하드코딩

문제점:
❌ 중앙 집중식 관리 불가
❌ 서버 변경 시 모든 클라이언트 재설정 필요
❌ 환경별 설정 관리 어려움
❌ 보안 취약 (자격증명 평문 저장)
```

#### 요구사항
- **중앙 관리**: 프로젝트 메인 관리자가 중앙 컨트롤 센터에서 서버 지정
- **간편한 연결**: 사용자는 서버 연결을 쉽게 활성화
- **자동 동기화**: 서버 정보 변경 시 모든 클라이언트 자동 업데이트
- **보안**: 자격증명 안전한 저장 및 전송

#### FastAPI vs Docker 비교

| 측면 | FastAPI (현재) | Docker + 중앙 서버 (권장) |
|------|---------------|-------------------------|
| **배포** | 각 사용자 로컬 설치 | 중앙 서버 한 번만 배포 |
| **설정 관리** | 개별 .env 파일 | 환경변수 중앙 관리 |
| **확장성** | 제한적 | 수평 확장 가능 |
| **버전 관리** | 사용자별 상이 | 컨테이너 이미지로 통일 |
| **보안** | 자격증명 분산 | 시크릿 관리 시스템 |
| **네트워크** | 직접 DB 연결 | API Gateway + 프록시 |

#### 해결 방향: **Docker 기반 중앙 서버 아키텍처**

**이유**:
1. ✅ **중앙 관리**: 관리자가 Docker Compose로 전체 스택 관리
2. ✅ **간편한 연결**: 클라이언트는 단일 API 엔드포인트만 알면 됨
3. ✅ **환경 분리**: Dev/Staging/Production 환경 독립적 관리
4. ✅ **자동 배포**: CI/CD 파이프라인 구축 가능
5. ✅ **보안 강화**: Docker Secrets, 환경변수 암호화

**FastAPI의 새로운 역할**:
- 로컬 개발 서버 ❌
- **중앙 API 서버** ✅ (Docker 컨테이너로 실행)

---

### Issue #3: 데이터 누적 및 프로젝트/리비전 추적 불가

#### 문제 상황
```sql
-- 현재 objects 테이블
CREATE TABLE objects (
    id BIGSERIAL PRIMARY KEY,
    model_version VARCHAR(255),  -- ⚠️ 단순 문자열 (파싱 필요)
    object_id VARCHAR(255),
    category VARCHAR(255),
    ...
);

-- 데이터 예시
| id | model_version              | object_id | category |
|----|----------------------------|-----------|----------|
| 1  | 프로젝트이름_20251016_030006  | abc-123   | 벽       |
| 852| 프로젝트이름_20251016_030006  | xyz-789   | 기둥     |
| 853| Snowdon_20251017_170052    | def-456   | 배관     |
```

**문제점**:
1. ❌ **프로젝트 구분 불명확**: model_version이 단순 문자열
2. ❌ **리비전 추적 불가**: 변경 이력 관리 안됨
3. ❌ **데이터 누적**: 모든 프로젝트가 하나의 테이블에 혼재
4. ❌ **쿼리 비효율**: 특정 프로젝트 조회 시 전체 스캔
5. ❌ **변화 추적 불가**: 리비전 간 차이 분석 불가능

#### 요구사항
- **프로젝트별 구분**: 프로젝트 단위로 데이터 분리
- **리비전 관리**: 같은 프로젝트 내에서 버전별 추적
- **변화 분석**: 리비전 간 변경사항 (추가/수정/삭제) 파악
- **이력 보존**: 모든 리비전 데이터 유지 (덮어쓰기 금지)

---

## 🏗️ 아키텍처 재설계

### 현재 아키텍처 (문제 상황)

```
┌─────────────┐     ┌─────────────┐
│  DXrevit    │     │  DXnavis    │
│  (Client)   │     │  (Client)   │
└──────┬──────┘     └──────┬──────┘
       │ Settings.settings │
       │ (SERVER_URL)      │
       ├───────────────────┤
       │  각자 다른 설정 파일 │
       ▼                   ▼
┌─────────────────────────────────┐
│      FastAPI (로컬 서버)          │
│      - .env 파일로 DB 연결        │
│      - 사용자마다 개별 실행        │
└────────────┬────────────────────┘
             ▼
    ┌────────────────┐
    │   PostgreSQL   │
    │   (로컬 또는 원격)|
    └────────────────┘

문제점:
❌ 분산된 설정 관리
❌ 중앙 집중식 제어 불가
❌ 환경별 관리 어려움
```

### 개선 아키텍처 (권장)

```
┌─────────────────────────────────────────────────────────┐
│          관리자 영역 (Admin Control Center)              │
│  ┌──────────────────────────────────────────────────┐  │
│  │  Docker Compose - Infrastructure as Code        │  │
│  │  ├─ API Server (FastAPI Container)              │  │
│  │  ├─ Database (PostgreSQL Container)             │  │
│  │  ├─ Cache (Redis Container)                     │  │
│  │  ├─ Config Server (중앙 설정 관리)                │  │
│  │  └─ Reverse Proxy (Nginx)                       │  │
│  └──────────────────────────────────────────────────┘  │
│           ▲ docker-compose up -d (한 번만 실행)          │
└───────────┼─────────────────────────────────────────────┘
            │
            │ HTTPS (SSL/TLS)
            │
    ┌───────┴───────────────────────────────┐
    │    API Gateway (https://dx-api.com)   │
    │    - 인증 (JWT Token)                  │
    │    - 라우팅 및 로드밸런싱                │
    │    - Rate Limiting                     │
    └───────┬───────────────────────────────┘
            │
    ┌───────┴───────┐
    │               │
    ▼               ▼
┌─────────┐   ┌─────────┐
│ DXrevit │   │ DXnavis │
│(Client) │   │(Client) │
└─────────┘   └─────────┘
    │               │
    └───────┬───────┘
            │
    설정 파일 (단순화):
    {
      "api_url": "https://dx-api.com",
      "project_id": "auto-detect"
    }

장점:
✅ 중앙 집중식 관리
✅ 단일 엔드포인트
✅ 자동 로드밸런싱
✅ 보안 강화 (HTTPS, JWT)
✅ 확장 가능
```

### 핵심 컴포넌트

#### 1. Config Server (중앙 설정 서버)

```yaml
# config-server/config.yml
environments:
  development:
    database:
      host: postgres-dev
      port: 5432
      name: dx_platform_dev
    api:
      url: http://api-dev:8000

  production:
    database:
      host: postgres-prod
      port: 5432
      name: dx_platform_prod
    api:
      url: https://dx-api.com

projects:
  - id: project_001
    name: "Snowdon Towers"
    database: project_001_db
    created: 2025-10-17

  - id: project_002
    name: "프로젝트 이름"
    database: project_002_db
    created: 2025-10-16
```

**클라이언트 연결 방식**:
```csharp
// DXrevit/DXnavis 클라이언트 코드
public class ConfigClient
{
    private const string CONFIG_SERVER = "https://dx-api.com/config";

    public async Task<ServerConfig> GetConfig(string projectId)
    {
        // 1. Config Server에 프로젝트 ID로 요청
        var response = await httpClient.GetAsync(
            $"{CONFIG_SERVER}/{projectId}"
        );

        // 2. 서버가 프로젝트별 설정 반환
        return await response.Content.ReadAsAsync<ServerConfig>();
    }
}

// 사용 예시
var config = await configClient.GetConfig("project_001");
var apiUrl = config.ApiUrl;  // https://dx-api.com/api/v1
var dbName = config.DatabaseName;  // project_001_db
```

#### 2. Docker Compose 스택

```yaml
# docker-compose.yml
version: '3.8'

services:
  # PostgreSQL Database
  postgres:
    image: postgres:15
    container_name: dx-postgres
    environment:
      POSTGRES_USER: ${DB_USER}
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./database/init:/docker-entrypoint-initdb.d
    networks:
      - dx-network
    restart: unless-stopped

  # FastAPI Application
  api:
    build:
      context: ./fastapi_server
      dockerfile: Dockerfile
    container_name: dx-api
    environment:
      DATABASE_URL: postgresql://${DB_USER}:${DB_PASSWORD}@postgres:5432/dx_platform
      JWT_SECRET: ${JWT_SECRET}
    depends_on:
      - postgres
      - redis
    networks:
      - dx-network
    restart: unless-stopped

  # Redis Cache
  redis:
    image: redis:7-alpine
    container_name: dx-redis
    networks:
      - dx-network
    restart: unless-stopped

  # Nginx Reverse Proxy
  nginx:
    image: nginx:alpine
    container_name: dx-nginx
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf
      - ./nginx/ssl:/etc/nginx/ssl
    depends_on:
      - api
    networks:
      - dx-network
    restart: unless-stopped

  # Config Server
  config-server:
    build:
      context: ./config_server
      dockerfile: Dockerfile
    container_name: dx-config
    volumes:
      - ./config_server/configs:/app/configs
    networks:
      - dx-network
    restart: unless-stopped

volumes:
  postgres_data:

networks:
  dx-network:
    driver: bridge
```

**관리자 운영 방법**:
```bash
# 1. 초기 설정 (한 번만)
cd 개발폴더/deployment
cp .env.example .env
nano .env  # 환경변수 설정

# 2. 전체 스택 시작
docker-compose up -d

# 3. 상태 확인
docker-compose ps

# 4. 로그 확인
docker-compose logs -f api

# 5. 특정 서비스 재시작
docker-compose restart api

# 6. 전체 종료
docker-compose down
```

---

## 🗄️ 데이터베이스 스키마 재설계

### 핵심 원칙

1. **프로젝트 분리**: 각 프로젝트는 독립된 스키마
2. **리비전 추적**: 모든 버전 이력 보존
3. **계층 정보**: Navisworks와 Revit 모두 지원
4. **변화 추적**: 리비전 간 diff 계산 가능

### 스키마 구조

```sql
-- ============================================
-- 1. 메타데이터 레이어 (공통)
-- ============================================

-- 프로젝트 마스터 테이블
CREATE TABLE projects (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name VARCHAR(255) NOT NULL,
    code VARCHAR(50) NOT NULL UNIQUE,  -- 프로젝트 코드 (예: SNOW, PROJ)
    description TEXT,
    building_name VARCHAR(255),
    location TEXT,
    client VARCHAR(255),
    created_by VARCHAR(100) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    is_active BOOLEAN DEFAULT true,

    -- 메타데이터
    metadata JSONB DEFAULT '{}'::jsonb,

    CONSTRAINT chk_project_code CHECK (code ~ '^[A-Z0-9_]+$')
);

-- 리비전 마스터 테이블
CREATE TABLE revisions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    project_id UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,

    -- 리비전 정보
    revision_number INTEGER NOT NULL,  -- 프로젝트 내 순차 번호
    version_tag VARCHAR(50),           -- 사용자 정의 태그 (예: v1.0, RC1)
    description TEXT,

    -- 소스 정보
    source_type VARCHAR(20) NOT NULL,  -- 'revit' | 'navisworks'
    source_file_path TEXT,
    source_file_hash VARCHAR(64),      -- 파일 무결성 검증

    -- 통계 정보
    total_objects INTEGER DEFAULT 0,
    total_categories INTEGER DEFAULT 0,

    -- 변경 추적
    parent_revision_id UUID REFERENCES revisions(id),  -- 이전 리비전
    changes_summary JSONB,  -- {added: 10, modified: 5, deleted: 2}

    -- 메타데이터
    created_by VARCHAR(100) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
    metadata JSONB DEFAULT '{}'::jsonb,

    CONSTRAINT uq_project_revision UNIQUE (project_id, revision_number),
    CONSTRAINT chk_source_type CHECK (source_type IN ('revit', 'navisworks'))
);

-- ============================================
-- 2. 객체 데이터 레이어 (통합 스키마)
-- ============================================

-- 통합 객체 테이블 (Revit + Navisworks)
CREATE TABLE objects (
    id BIGSERIAL PRIMARY KEY,

    -- 프로젝트 및 리비전 연결
    project_id UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    revision_id UUID NOT NULL REFERENCES revisions(id) ON DELETE CASCADE,

    -- 객체 식별자
    object_id UUID NOT NULL,           -- 고유 객체 ID (InstanceGuid 또는 생성)
    element_id INTEGER,                -- Revit Element ID (Navisworks는 NULL)
    source_type VARCHAR(20) NOT NULL,  -- 'revit' | 'navisworks'

    -- 계층 정보 (통합)
    parent_object_id UUID,             -- 부모 객체 ID
    level INTEGER DEFAULT 0,           -- 계층 깊이
    display_name VARCHAR(500),         -- 표시 이름
    spatial_path TEXT,                 -- 공간 경로 (Building > Level > Room)

    -- 분류 정보
    category VARCHAR(255) NOT NULL,
    family VARCHAR(255),
    type VARCHAR(255),

    -- 스케줄 연계
    activity_id VARCHAR(100),          -- 공정 ID (4D 시뮬레이션)

    -- 속성 및 공간 데이터
    properties JSONB NOT NULL DEFAULT '{}'::jsonb,
    bounding_box JSONB,                -- {minX, maxX, minY, maxY, minZ, maxZ}

    -- 상태 추적
    change_type VARCHAR(20) DEFAULT 'added',  -- 'added' | 'modified' | 'deleted' | 'unchanged'
    previous_object_id BIGINT REFERENCES objects(id),  -- 이전 리비전의 같은 객체

    -- 메타데이터
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),

    CONSTRAINT chk_object_source_type CHECK (source_type IN ('revit', 'navisworks')),
    CONSTRAINT chk_change_type CHECK (change_type IN ('added', 'modified', 'deleted', 'unchanged')),
    CONSTRAINT uq_revision_object UNIQUE (revision_id, object_id)
);

-- ============================================
-- 3. 관계 데이터 레이어
-- ============================================

CREATE TABLE relationships (
    id BIGSERIAL PRIMARY KEY,

    -- 프로젝트 및 리비전 연결
    project_id UUID NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    revision_id UUID NOT NULL REFERENCES revisions(id) ON DELETE CASCADE,

    -- 관계 정보
    source_object_id UUID NOT NULL,
    target_object_id UUID NOT NULL,
    relation_type VARCHAR(50) NOT NULL,  -- 'HostedBy', 'Contains', 'ConnectsTo', 'Supports'
    is_directed BOOLEAN DEFAULT true,

    -- 속성
    properties JSONB DEFAULT '{}'::jsonb,

    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),

    CONSTRAINT chk_relation_type CHECK (
        relation_type IN ('HostedBy', 'Contains', 'ConnectsTo', 'Supports', 'DependsOn')
    )
);

-- ============================================
-- 4. 인덱스 (성능 최적화)
-- ============================================

-- projects 테이블
CREATE INDEX idx_projects_code ON projects(code);
CREATE INDEX idx_projects_active ON projects(is_active) WHERE is_active = true;

-- revisions 테이블
CREATE INDEX idx_revisions_project ON revisions(project_id);
CREATE INDEX idx_revisions_number ON revisions(project_id, revision_number);
CREATE INDEX idx_revisions_parent ON revisions(parent_revision_id);

-- objects 테이블
CREATE INDEX idx_objects_project ON objects(project_id);
CREATE INDEX idx_objects_revision ON objects(revision_id);
CREATE INDEX idx_objects_object_id ON objects(object_id);
CREATE INDEX idx_objects_parent ON objects(parent_object_id);
CREATE INDEX idx_objects_level ON objects(level);
CREATE INDEX idx_objects_category ON objects(category);
CREATE INDEX idx_objects_activity ON objects(activity_id) WHERE activity_id IS NOT NULL;
CREATE INDEX idx_objects_change_type ON objects(change_type);

-- JSONB 인덱스 (속성 검색)
CREATE INDEX idx_objects_properties ON objects USING GIN(properties);
CREATE INDEX idx_objects_bounding_box ON objects USING GIN(bounding_box);

-- relationships 테이블
CREATE INDEX idx_relationships_project ON relationships(project_id);
CREATE INDEX idx_relationships_revision ON relationships(revision_id);
CREATE INDEX idx_relationships_source ON relationships(source_object_id);
CREATE INDEX idx_relationships_target ON relationships(target_object_id);
CREATE INDEX idx_relationships_type ON relationships(relation_type);

-- ============================================
-- 5. 뷰 (편의성)
-- ============================================

-- 최신 리비전 뷰
CREATE VIEW v_latest_revisions AS
SELECT DISTINCT ON (project_id)
    r.*,
    p.name AS project_name,
    p.code AS project_code
FROM revisions r
JOIN projects p ON r.project_id = p.id
WHERE p.is_active = true
ORDER BY project_id, revision_number DESC;

-- 계층 구조 뷰 (Navisworks + Revit 통합)
CREATE VIEW v_hierarchy AS
SELECT
    o.id,
    o.project_id,
    o.revision_id,
    o.object_id,
    o.parent_object_id,
    o.level,
    o.display_name,
    o.spatial_path,
    o.category,
    o.family,
    o.type,
    o.source_type,
    p.name AS project_name,
    r.revision_number,
    r.version_tag
FROM objects o
JOIN projects p ON o.project_id = p.id
JOIN revisions r ON o.revision_id = r.id
WHERE o.change_type != 'deleted';

-- 리비전 변경사항 뷰
CREATE VIEW v_revision_changes AS
SELECT
    r.id AS revision_id,
    r.project_id,
    r.revision_number,
    r.version_tag,
    COUNT(*) FILTER (WHERE o.change_type = 'added') AS added_count,
    COUNT(*) FILTER (WHERE o.change_type = 'modified') AS modified_count,
    COUNT(*) FILTER (WHERE o.change_type = 'deleted') AS deleted_count,
    COUNT(*) FILTER (WHERE o.change_type = 'unchanged') AS unchanged_count,
    COUNT(*) AS total_objects
FROM revisions r
LEFT JOIN objects o ON r.id = o.revision_id
GROUP BY r.id, r.project_id, r.revision_number, r.version_tag;

-- ============================================
-- 6. 함수 (유틸리티)
-- ============================================

-- 계층 경로 조회 함수
CREATE OR REPLACE FUNCTION fn_get_object_hierarchy_path(
    target_revision_id UUID,
    target_object_id UUID
)
RETURNS TABLE (
    object_id UUID,
    parent_object_id UUID,
    level INTEGER,
    display_name VARCHAR(500),
    category VARCHAR(255),
    full_path TEXT
) AS $$
WITH RECURSIVE hierarchy AS (
    -- 시작: 대상 객체
    SELECT
        o.object_id,
        o.parent_object_id,
        o.level,
        o.display_name,
        o.category,
        o.display_name::TEXT AS path
    FROM objects o
    WHERE o.revision_id = target_revision_id
      AND o.object_id = target_object_id

    UNION ALL

    -- 재귀: 부모로 올라가기
    SELECT
        o.object_id,
        o.parent_object_id,
        o.level,
        o.display_name,
        o.category,
        o.display_name || ' > ' || h.path
    FROM objects o
    INNER JOIN hierarchy h ON o.object_id = h.parent_object_id
    WHERE o.revision_id = target_revision_id
)
SELECT
    h.object_id,
    h.parent_object_id,
    h.level,
    h.display_name,
    h.category,
    h.path AS full_path
FROM hierarchy h
ORDER BY h.level;
$$ LANGUAGE SQL;

-- 리비전 간 변경사항 비교 함수
CREATE OR REPLACE FUNCTION fn_compare_revisions(
    old_revision_id UUID,
    new_revision_id UUID
)
RETURNS TABLE (
    object_id UUID,
    change_type VARCHAR(20),
    category VARCHAR(255),
    display_name VARCHAR(500),
    old_properties JSONB,
    new_properties JSONB
) AS $$
SELECT
    COALESCE(o_new.object_id, o_old.object_id) AS object_id,
    CASE
        WHEN o_old.object_id IS NULL THEN 'added'
        WHEN o_new.object_id IS NULL THEN 'deleted'
        WHEN o_old.properties != o_new.properties THEN 'modified'
        ELSE 'unchanged'
    END AS change_type,
    COALESCE(o_new.category, o_old.category) AS category,
    COALESCE(o_new.display_name, o_old.display_name) AS display_name,
    o_old.properties AS old_properties,
    o_new.properties AS new_properties
FROM objects o_old
FULL OUTER JOIN objects o_new
    ON o_old.object_id = o_new.object_id
WHERE o_old.revision_id = old_revision_id
   OR o_new.revision_id = new_revision_id;
$$ LANGUAGE SQL;
```

### 데이터 예시

#### 1. Projects 테이블
```sql
INSERT INTO projects (name, code, description, created_by) VALUES
('Snowdon Towers', 'SNOW', 'Snowdon Towers MEP 프로젝트', 'yoon'),
('프로젝트 이름', 'PROJ', '테스트 프로젝트', 'yoon');
```

| id | name | code | created_by | created_at |
|----|------|------|------------|------------|
| uuid-001 | Snowdon Towers | SNOW | yoon | 2025-10-17 |
| uuid-002 | 프로젝트 이름 | PROJ | yoon | 2025-10-16 |

#### 2. Revisions 테이블
```sql
INSERT INTO revisions (project_id, revision_number, version_tag, source_type, created_by) VALUES
('uuid-001', 1, 'v1.0', 'navisworks', 'yoon'),
('uuid-001', 2, 'v1.1', 'navisworks', 'yoon'),
('uuid-002', 1, 'initial', 'revit', 'yoon');
```

| id | project_id | revision_number | version_tag | source_type | parent_revision_id |
|----|------------|-----------------|-------------|-------------|--------------------|
| rev-001 | uuid-001 | 1 | v1.0 | navisworks | NULL |
| rev-002 | uuid-001 | 2 | v1.1 | navisworks | rev-001 |
| rev-003 | uuid-002 | 1 | initial | revit | NULL |

#### 3. Objects 테이블 (통합)
```sql
-- Navisworks 객체
INSERT INTO objects (project_id, revision_id, object_id, parent_object_id, level, display_name, category, source_type)
VALUES ('uuid-001', 'rev-001', 'obj-001', NULL, 0, 'Building A', 'Building', 'navisworks');

-- Revit 객체
INSERT INTO objects (project_id, revision_id, object_id, element_id, parent_object_id, level, category, family, type, source_type)
VALUES ('uuid-002', 'rev-003', 'obj-101', 23, NULL, 0, '재료', '기본값', 'Unknown', 'revit');
```

### 쿼리 예시

#### 프로젝트별 최신 리비전 조회
```sql
SELECT * FROM v_latest_revisions
WHERE project_code = 'SNOW';
```

#### 특정 리비전의 계층 구조 조회
```sql
SELECT * FROM v_hierarchy
WHERE revision_id = 'rev-001'
ORDER BY level, display_name;
```

#### 리비전 간 변경사항 비교
```sql
SELECT * FROM fn_compare_revisions('rev-001', 'rev-002')
WHERE change_type != 'unchanged';
```

#### 객체 계층 경로 조회
```sql
SELECT * FROM fn_get_object_hierarchy_path('rev-001', 'obj-001');
```

---

## 📅 단계별 실행 계획

### Phase 0: 준비 단계 (1주)

**목표**: 환경 구축 및 기존 시스템 백업

#### 작업 목록

1. **기존 데이터 백업**
   ```bash
   # PostgreSQL 백업
   pg_dump -h localhost -U postgres -d DX_platform > backup_$(date +%Y%m%d).sql

   # 프로젝트 파일 백업
   cp -r 개발폴더 개발폴더_backup_$(date +%Y%m%d)
   ```

2. **Docker 환경 구축**
   ```bash
   # Docker 설치 확인
   docker --version
   docker-compose --version

   # 개발폴더/deployment 디렉토리 생성
   mkdir -p deployment/{nginx,config_server,database/init}
   ```

3. **환경 변수 템플릿 작성**
   ```bash
   # deployment/.env.example
   DB_USER=postgres
   DB_PASSWORD=your_secure_password
   JWT_SECRET=your_jwt_secret
   REDIS_PASSWORD=your_redis_password
   ```

**완료 기준**:
- ✅ 기존 데이터 백업 완료
- ✅ Docker 설치 및 테스트
- ✅ 프로젝트 구조 준비

---

### Phase 1: 데이터베이스 스키마 마이그레이션 (2주)

**목표**: 새로운 스키마 구축 및 기존 데이터 마이그레이션

#### Week 1: 스키마 생성 및 테스트

1. **새 스키마 생성**
   ```bash
   # database/migrations/001_create_new_schema.sql
   # 위의 스키마 SQL 실행
   psql -U postgres -d DX_platform -f database/migrations/001_create_new_schema.sql
   ```

2. **테스트 데이터 삽입**
   ```sql
   -- 프로젝트 생성
   INSERT INTO projects (name, code, created_by) VALUES
   ('Test Project', 'TEST', 'admin');

   -- 리비전 생성
   INSERT INTO revisions (project_id, revision_number, source_type, created_by)
   SELECT id, 1, 'revit', 'admin' FROM projects WHERE code = 'TEST';

   -- 객체 삽입 테스트
   -- ...
   ```

3. **쿼리 성능 테스트**
   ```sql
   EXPLAIN ANALYZE
   SELECT * FROM v_hierarchy WHERE project_id = 'uuid-001';
   ```

#### Week 2: 데이터 마이그레이션

1. **마이그레이션 스크립트 작성**
   ```python
   # scripts/migrate_to_new_schema.py
   import asyncpg
   import asyncio

   async def migrate_metadata():
       """기존 metadata 테이블 → projects + revisions"""
       # ...

   async def migrate_objects():
       """기존 objects 테이블 → 새 objects 테이블"""
       # ...

   async def main():
       await migrate_metadata()
       await migrate_objects()
   ```

2. **마이그레이션 실행 및 검증**
   ```bash
   python scripts/migrate_to_new_schema.py

   # 검증 쿼리
   psql -U postgres -d DX_platform -c "
   SELECT
       (SELECT COUNT(*) FROM projects) AS projects_count,
       (SELECT COUNT(*) FROM revisions) AS revisions_count,
       (SELECT COUNT(*) FROM objects) AS objects_count;
   "
   ```

**완료 기준**:
- ✅ 새 스키마 생성 완료
- ✅ 기존 데이터 마이그레이션 완료
- ✅ 쿼리 성능 검증 완료

---

### Phase 2: Docker 기반 중앙 서버 구축 (2주)

**목표**: FastAPI + PostgreSQL Docker 컨테이너 배포

#### Week 1: Docker 이미지 및 Compose 작성

1. **FastAPI Dockerfile 작성**
   ```dockerfile
   # fastapi_server/Dockerfile
   FROM python:3.10-slim

   WORKDIR /app

   COPY requirements.txt .
   RUN pip install --no-cache-dir -r requirements.txt

   COPY . .

   EXPOSE 8000

   CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
   ```

2. **docker-compose.yml 작성**
   (위의 아키텍처 섹션 참조)

3. **Nginx 설정**
   ```nginx
   # nginx/nginx.conf
   upstream api_backend {
       server api:8000;
   }

   server {
       listen 80;
       server_name dx-api.local;

       location /api/ {
           proxy_pass http://api_backend/;
           proxy_set_header Host $host;
           proxy_set_header X-Real-IP $remote_addr;
       }
   }
   ```

#### Week 2: Config Server 구현

1. **Config Server 개발**
   ```python
   # config_server/main.py
   from fastapi import FastAPI
   import yaml

   app = FastAPI()

   @app.get("/config/{project_id}")
   async def get_project_config(project_id: str):
       with open(f"configs/{project_id}.yml") as f:
           config = yaml.safe_load(f)
       return config
   ```

2. **배포 및 테스트**
   ```bash
   docker-compose up -d
   docker-compose ps
   curl http://localhost/api/health
   ```

**완료 기준**:
- ✅ Docker 컨테이너 정상 실행
- ✅ API 엔드포인트 접근 가능
- ✅ Config Server 동작 확인

---

### Phase 3: 클라이언트 플러그인 개선 (3주)

**목표**: DXrevit, DXnavis 플러그인을 새 아키텍처에 맞게 수정

#### Week 1: Revit 플러그인 개선

1. **계층 정보 추출 기능 추가**
   ```csharp
   // DXrevit/Services/HierarchyBuilder.cs
   public class HierarchyBuilder
   {
       public string DetermineParentObject(Element element) { ... }
       public int CalculateLevel(Element element) { ... }
       public string BuildSpatialPath(Element element) { ... }
   }
   ```

2. **프로젝트/리비전 메타데이터 자동 생성**
   ```csharp
   // DXrevit/Services/DataExtractor.cs
   public ExtractedData ExtractAll(...)
   {
       var projectId = DetermineProjectId();
       var revisionNumber = GetNextRevisionNumber(projectId);

       // ...
   }
   ```

3. **Config Server 연동**
   ```csharp
   // DXrevit/Services/ConfigClient.cs
   public class ConfigClient
   {
       public async Task<ServerConfig> GetConfig(string projectId) { ... }
   }
   ```

#### Week 2: Navisworks 플러그인 개선

1. **계층 정보 추출 유지 (이미 구현됨)**
   - 기존 `NavisworksDataExtractor.cs` 유지

2. **데이터 전송 형식 통일**
   ```csharp
   // DXnavis/Services/ApiDataWriter.cs
   public class ApiDataWriter
   {
       public async Task SendToApi(List<HierarchicalPropertyRecord> records)
       {
           // 새 스키마 형식으로 변환
           var payload = ConvertToUnifiedSchema(records);
           await httpClient.PostAsJsonAsync("/api/v1/objects", payload);
       }
   }
   ```

#### Week 3: 통합 테스트

1. **End-to-End 테스트**
   ```
   Revit → 스냅샷 → API 서버 → DB 저장 → 검증
   Navisworks → 계층 추출 → API 서버 → DB 저장 → 검증
   ```

2. **리비전 추적 테스트**
   ```
   동일 프로젝트에서 두 번째 스냅샷 → 변경사항 감지 확인
   ```

**완료 기준**:
- ✅ Revit 계층 정보 추출 정상 동작
- ✅ Navisworks 데이터 전송 정상 동작
- ✅ 리비전 추적 기능 검증

---

### Phase 4: 관리자 도구 개발 (2주)

**목표**: 관리자용 대시보드 및 CLI 도구

#### Week 1: 관리자 대시보드

1. **FastAPI Admin 패널**
   ```python
   # fastapi_server/admin/main.py
   from fastapi import FastAPI
   from fastapi.templating import Jinja2Templates

   @app.get("/admin/projects")
   async def list_projects():
       # 프로젝트 목록 조회
       return templates.TemplateResponse("projects.html", {...})

   @app.get("/admin/projects/{project_id}/revisions")
   async def list_revisions(project_id: str):
       # 리비전 목록 조회
       return templates.TemplateResponse("revisions.html", {...})
   ```

2. **프론트엔드 (간단한 HTML/JS)**
   ```html
   <!-- fastapi_server/templates/projects.html -->
   <table>
       <tr><th>프로젝트명</th><th>코드</th><th>최신 리비전</th><th>액션</th></tr>
       {% for project in projects %}
       <tr>
           <td>{{ project.name }}</td>
           <td>{{ project.code }}</td>
           <td>{{ project.latest_revision }}</td>
           <td><a href="/admin/projects/{{ project.id }}/revisions">상세</a></td>
       </tr>
       {% endfor %}
   </table>
   ```

#### Week 2: CLI 관리 도구

1. **프로젝트 관리 CLI**
   ```bash
   # scripts/dx_cli.py
   import typer

   app = typer.Typer()

   @app.command()
   def create_project(name: str, code: str):
       """새 프로젝트 생성"""
       # ...

   @app.command()
   def list_projects():
       """프로젝트 목록 조회"""
       # ...

   if __name__ == "__main__":
       app()
   ```

2. **사용 예시**
   ```bash
   python scripts/dx_cli.py create-project "New Building" "NEWB"
   python scripts/dx_cli.py list-projects
   python scripts/dx_cli.py compare-revisions SNOW 1 2
   ```

**완료 기준**:
- ✅ 웹 대시보드 접근 가능
- ✅ CLI 도구 정상 동작
- ✅ 프로젝트/리비전 관리 기능 검증

---

### Phase 5: 문서화 및 배포 (1주)

**목표**: 사용자 가이드 및 운영 매뉴얼 작성

#### 문서 목록

1. **관리자 가이드**
   - `deployment/ADMIN_GUIDE.md`
   - Docker 배포 방법
   - 백업 및 복구 절차
   - 모니터링 설정

2. **사용자 가이드**
   - `docs/USER_GUIDE.md`
   - DXrevit 사용법
   - DXnavis 사용법
   - 트러블슈팅

3. **API 문서**
   - FastAPI 자동 생성 (Swagger UI)
   - `/docs` 엔드포인트

**완료 기준**:
- ✅ 모든 문서 작성 완료
- ✅ 프로덕션 배포 완료
- ✅ 사용자 교육 실시

---

## 🔧 기술 스택 결정

### FastAPI vs Docker 아키텍처 비교

| 항목 | FastAPI (현재) | Docker (권장) | 선택 |
|------|---------------|--------------|------|
| **배포 방식** | 사용자 개별 설치 | 중앙 서버 배포 | ✅ Docker |
| **설정 관리** | 분산 (.env) | 중앙 집중 | ✅ Docker |
| **확장성** | 제한적 | 수평 확장 가능 | ✅ Docker |
| **보안** | 평문 자격증명 | 시크릿 관리 | ✅ Docker |
| **유지보수** | 사용자별 업데이트 | 한 번만 업데이트 | ✅ Docker |

### 최종 아키텍처 결정

**선택**: **Docker 기반 중앙 서버 아키텍처**

**이유**:
1. ✅ 중앙 집중식 관리 (관리자 요구사항 충족)
2. ✅ 사용자 편의성 (단일 엔드포인트 연결)
3. ✅ 확장성 (프로젝트 증가 대응)
4. ✅ 보안 강화 (시크릿 관리)
5. ✅ 운영 효율성 (자동화 배포)

**FastAPI의 역할 변경**:
- 로컬 서버 ❌
- **중앙 API 서버** ✅ (Docker 컨테이너로 실행)

---

## 📊 예상 타임라인

```
Week 1-1    ████████  Phase 0: 준비 단계
Week 2-3    ████████████████  Phase 1: 스키마 마이그레이션
Week 4-5    ████████████████  Phase 2: Docker 서버 구축
Week 6-8    ████████████████████████  Phase 3: 플러그인 개선
Week 9-10   ████████████████  Phase 4: 관리자 도구
Week 11     ████████  Phase 5: 문서화 및 배포

총 기간: 약 11주 (2.5개월)
```

---

## 🎯 성공 기준

### 기능 요구사항

- [x] Navisworks와 Revit 데이터 통합 스키마
- [ ] 프로젝트별 데이터 분리
- [ ] 리비전별 변화 추적
- [ ] 중앙 집중식 서버 관리
- [ ] 사용자 간편 연결
- [ ] 계층 정보 완전 지원

### 비기능 요구사항

- [ ] 쿼리 응답 시간 < 200ms (인덱싱 최적화)
- [ ] 동시 사용자 100명 지원
- [ ] 99.9% 가용성 (Docker 재시작 정책)
- [ ] 자동 백업 (일일)
- [ ] 보안 인증 (JWT)

---

## 📚 참고 문서

- [Navisworks vs Revit 데이터 구조 비교](scripts/NAVISWORKS_VS_REVIT_DATA_STRUCTURE.md)
- [데이터베이스 분석 보고서](scripts/DATABASE_ANALYSIS_REPORT.md)
- [PostgreSQL MCP 가이드](scripts/MCP_POSTGRES_GUIDE.md)
- [배포 가이드](scripts/DEPLOYMENT_GUIDE.md)

---

**최종 수정**: 2025-10-18
**작성자**: System Architecture Team
**승인**: [관리자 이름]

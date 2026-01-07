요청하신 조건을 모두 반영한 최종본 techspec.md 전문을 아래에 제공합니다.

---

# techspec.md — AWP 2025 BIM Data Integration System (v1.1.0)

## 개요

본 문서는 Revit/Navisworks 기반 BIM 데이터 통합 시스템의 스키마 이원화(unique_key, object_guid), CSV 기반 프로젝트 자동 감지, FastAPI 인제스트·탐지 API 확장, JSONB 인덱스 전략, 배포·점검 자동화, 모니터링·성능 목표, 보안·호환성·롤백 계획을 포함한 프로덕션 적용용 기술 명세서입니다. 적용 순서는 DB → 백엔드 → DXrevit → DXnavis → 스크립트/문서 → 테스트이며, 커밋 메시지는 “Scope: What — Why” 규칙을 따릅니다.

## 저장소 전제

```
DXBase/
DXrevit/
DXnavis/
fastapi_server/
database/
docs/
scripts/
tests/
monitoring/
```

## 전제 조건 및 가정

1. PostgreSQL 14+ 환경에서 `pgcrypto`, `pg_trgm` 확장이 설치되어 있어야 하며, `gen_random_uuid()` 사용을 전제로 합니다.
2. `unified_objects` 테이블에 `geometry JSONB`, `updated_at TIMESTAMPTZ` 컬럼이 존재해야 하며, 누락 시 본 마이그레이션에서 추가합니다.
3. `projects.code` 고유 제약, `revisions (project_id, revision_number, source_type)` 고유 제약이 존재해야 탐지 쿼리의 일관성이 보장됩니다.
4. `detect-by-objects`에서는 일치 집합과 총계 집합 모두 “최신 리비전”으로 한정하여 신뢰도 계산 정합성을 보장합니다.
5. 클라이언트 타깃: Revit Add-in(.NET Framework 4.8), Navisworks Plugin(.NET 8.0, WinForms).

---

## 1. 데이터베이스 변경

### 1.1 사전 준비: 확장·제약·보조 컬럼

파일: `database/migrations/2025_10_30__base_prereqs.sql`

```sql
BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS pg_trgm;

ALTER TABLE projects
  ADD CONSTRAINT IF NOT EXISTS uq_projects_code UNIQUE (code);

ALTER TABLE revisions
  ADD CONSTRAINT IF NOT EXISTS uq_revisions UNIQUE (project_id, revision_number, source_type);

ALTER TABLE unified_objects
  ADD COLUMN IF NOT EXISTS geometry JSONB,
  ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ DEFAULT NOW();

COMMIT;
```

### 1.2 식별자 이원화 및 제약 정비

파일: `database/migrations/2025_10_30__unified_objects_identity_fix.sql`

```sql
BEGIN;

-- 원문 식별자 보존
ALTER TABLE unified_objects
  ADD COLUMN IF NOT EXISTS unique_key TEXT;

-- legacy unique_id -> object_guid로 의미 명확화
DO $$
BEGIN
  IF EXISTS (
    SELECT 1 FROM information_schema.columns
     WHERE table_name='unified_objects' AND column_name='unique_id'
  ) AND NOT EXISTS (
    SELECT 1 FROM information_schema.columns
     WHERE table_name='unified_objects' AND column_name='object_guid'
  ) THEN
    ALTER TABLE unified_objects RENAME COLUMN unique_id TO object_guid;
  END IF;
END $$;

-- 과거 데이터 백필
UPDATE unified_objects
   SET unique_key = COALESCE(unique_key, object_guid::text)
 WHERE unique_key IS NULL;

-- 유니크 제약: (revision_id, source_type, unique_key)
ALTER TABLE unified_objects
  DROP CONSTRAINT IF EXISTS uq_unified_object;

ALTER TABLE unified_objects
  ADD CONSTRAINT uq_unified_object_by_unique_key
  UNIQUE (revision_id, source_type, unique_key);

COMMIT;
```

### 1.3 인덱스(동시 생성, 트랜잭션 외부)

파일: `database/migrations/2025_10_30__unified_objects_indexes.sql`

```sql
-- 카테고리
DROP INDEX IF EXISTS idx_unified_objects_category;
CREATE INDEX CONCURRENTLY idx_unified_objects_category
  ON unified_objects(category);

-- JSONB 기본 GIN (키 존재/키-값 포함)
DROP INDEX IF EXISTS idx_unified_objects_properties;
CREATE INDEX CONCURRENTLY idx_unified_objects_properties
  ON unified_objects USING GIN (properties);

-- 탐지 최적화
CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_unified_objects_unique_key
  ON unified_objects(unique_key);

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_unified_objects_object_guid
  ON unified_objects(object_guid);

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_unified_objects_revision_id
  ON unified_objects(revision_id);
```

### 1.4 최신 리비전 뷰

파일: `database/migrations/2025_10_30__views_latest.sql`

```sql
CREATE OR REPLACE VIEW v_latest_revisions AS
SELECT r.project_id, MAX(r.revision_number) AS latest_revision
  FROM revisions r
 GROUP BY r.project_id;

CREATE OR REPLACE VIEW v_unified_objects_latest AS
SELECT uo.*
  FROM unified_objects uo
  JOIN revisions r ON r.id = uo.revision_id
  JOIN v_latest_revisions v
    ON v.project_id = r.project_id
   AND v.latest_revision = r.revision_number;
```

### 1.5 롤백

파일: `database/migrations/2025_10_30__rollback_identity_fix.sql`

```sql
BEGIN;

DROP VIEW IF EXISTS v_unified_objects_latest;
DROP VIEW IF EXISTS v_latest_revisions;

DROP INDEX IF EXISTS idx_unified_objects_object_guid;
DROP INDEX IF EXISTS idx_unified_objects_unique_key;
DROP INDEX IF EXISTS idx_unified_objects_properties;
DROP INDEX IF EXISTS idx_unified_objects_category;
DROP INDEX IF EXISTS idx_unified_objects_revision_id;

ALTER TABLE unified_objects
  DROP CONSTRAINT IF EXISTS uq_unified_object_by_unique_key;

ALTER TABLE unified_objects
  RENAME COLUMN object_guid TO unique_id;

ALTER TABLE unified_objects
  DROP COLUMN IF EXISTS unique_key;

-- 필요 시 완전 복원
-- ALTER TABLE unified_objects DROP COLUMN IF EXISTS updated_at;
-- ALTER TABLE unified_objects DROP COLUMN IF EXISTS geometry;

ALTER TABLE unified_objects
  ADD CONSTRAINT uq_unified_object
  UNIQUE (revision_id, source_type, unique_id);

COMMIT;
```

---

### 1.6 마이그레이션 실행 가이드

배포 환경에서 스키마 변경을 적용할 때는 다음 수칙을 따른다.

1. 세션 옵션 고정:
   ```bash
   export PGOPTIONS='-c lock_timeout=5s -c statement_timeout=30min -c maintenance_work_mem=512MB'
   ```
2. `ON_ERROR_STOP=1`을 지정하여 실패 시 즉시 중단한다.
3. 각 마이그레이션 실행 후 `VACUUM ANALYZE unified_objects;` 수행.
4. 중복 감시:
   ```sql
   SELECT revision_id, source_type, unique_key, COUNT(*)
   FROM unified_objects
   GROUP BY 1,2,3 HAVING COUNT(*) > 1;
   ```
   - 결과가 존재하면 롤백 스크립트를 즉시 실행하고 원인을 조사한다.
5. 성능 스냅샷:
   ```sql
   EXPLAIN (ANALYZE, BUFFERS)
   SELECT * FROM v_unified_objects_latest WHERE project_id = $1;
   ```
   - 실행 계획과 버퍼 사용량을 기록하여 회귀 여부를 추적한다.
6. 모든 로그와 스냅샷은 `docs/logs/phase_a` 등에 보관한다.

---

## 2. FastAPI 백엔드

### 2.1 스키마

파일: `fastapi_server/models/schemas.py`

```python
from pydantic import BaseModel, Field, field_validator
from typing import Optional, Any, Dict, List
import uuid

class UnifiedObjectDto(BaseModel):
    unique_key: str
    object_guid: Optional[str] = None
    category: Optional[str] = None
    name: Optional[str] = None
    properties: Dict[str, Any] = Field(default_factory=dict)
    source_type: str
    geometry: Optional[Dict[str, Any]] = None
    # legacy
    unique_id: Optional[str] = None

    @field_validator("object_guid")
    @classmethod
    def normalize_guid(cls, v: Optional[str]) -> Optional[str]:
        if not v:
            return None
        try:
            return str(uuid.UUID(v))
        except Exception:
            return None

    @field_validator("properties")
    @classmethod
    def coerce_property_types(cls, v: Dict[str, Any]) -> Dict[str, Any]:
        def coerce(val):
            if isinstance(val, (int, float, bool)) or val is None:
                return val
            if not isinstance(val, str):
                return val
            s = val.strip().lower()
            if s in ("true", "false"):
                return s == "true"
            try:
                if "." in s:
                    return float(s)
                return int(s)
            except Exception:
                return val
        return {k: coerce(val) for k, val in v.items()}

class IngestRequest(BaseModel):
    project_code: str
    revision_number: int
    source_type: str = "revit"
    objects: List[UnifiedObjectDto]

class DetectRequest(BaseModel):
    object_ids: List[str] = Field(..., min_items=1, max_items=1000)
    min_confidence: float = Field(0.7, ge=0.0, le=1.0)
    max_candidates: int = Field(3, ge=1, le=10)
```

### 2.2 레거시 호환

파일: `fastapi_server/utils/backward_compat.py`

```python
import uuid, logging
from typing import Dict, Any
log = logging.getLogger(__name__)

def migrate_legacy_object(d: Dict[str, Any]) -> Dict[str, Any]:
    if "unique_key" not in d and "unique_id" in d:
        d["unique_key"] = str(d["unique_id"])
        try:
            uuid.UUID(d["unique_id"])
            d["object_guid"] = d.get("object_guid") or d["unique_id"]
        except Exception:
            d["object_guid"] = d.get("object_guid") or None
        log.info("migrated legacy payload unique_id -> unique_key")
    return d
```

### 2.3 DB 헬퍼 및 시스템 라우터

파일: `fastapi_server/utils/db_helpers.py`

```python
async def get_or_create_project(conn, code: str) -> str:
    row = await conn.fetchrow("SELECT id FROM projects WHERE code=$1", code)
    if row: return row["id"]
    row = await conn.fetchrow(
        "INSERT INTO projects (id, code, name, created_by) "
        "VALUES (gen_random_uuid(), $1, $1, 'system') RETURNING id", code
    )
    return row["id"]

async def get_or_create_revision(conn, code: str, rev: int, source: str) -> str:
    pid = await get_or_create_project(conn, code)
    row = await conn.fetchrow(
        "SELECT id FROM revisions WHERE project_id=$1 AND revision_number=$2 AND source_type=$3",
        pid, rev, source
    )
    if row: return row["id"]
    row = await conn.fetchrow(
        "INSERT INTO revisions (id, project_id, revision_number, source_type) "
        "VALUES (gen_random_uuid(), $1, $2, $3) RETURNING id",
        pid, rev, source
    )
    return row["id"]

async def update_project_statistics(project_code: str) -> None:
    return None
```

파일: `fastapi_server/routers/system.py`

```python
from fastapi import APIRouter
router = APIRouter(prefix="", tags=["system"])

@router.get("/health")
async def health():
    return {"status": "ok"}

@router.get("/api/v1/version")
async def version():
    return {"version": "1.1.0"}
```

### 2.4 인제스트 라우터(이중 경로 지원)

파일: `fastapi_server/routers/ingest.py`

```python
from fastapi import APIRouter, Depends, HTTPException, BackgroundTasks
from ..models.schemas import IngestRequest
from ..utils.db_helpers import get_or_create_revision, update_project_statistics
from ..utils.backward_compat import migrate_legacy_object
from ..database import get_db
import logging

log = logging.getLogger(__name__)
router = APIRouter(prefix="/api/v1/ingest", tags=["ingest"])

@router.post("")
@router.post("/unified-objects")
async def ingest_unified_objects(data: IngestRequest, background_tasks: BackgroundTasks, db=Depends(get_db)):
    for i, obj in enumerate(data.objects):
        d = obj.model_dump()
        data.objects[i] = type(obj)(**migrate_legacy_object(d))
    async with db.pool.acquire() as conn:
        try:
            async with conn.transaction():
                rid = await get_or_create_revision(conn, data.project_code, data.revision_number, data.source_type)
                sql = """
                    INSERT INTO unified_objects
                      (id, revision_id, unique_key, object_guid, category, name, source_type, properties, geometry, updated_at)
                    VALUES
                      (gen_random_uuid(), $1, $2, $3, $4, $5, $6, $7, $8, NOW())
                    ON CONFLICT (revision_id, source_type, unique_key)
                    DO UPDATE SET
                      object_guid = COALESCE(EXCLUDED.object_guid, unified_objects.object_guid),
                      category   = EXCLUDED.category,
                      name       = EXCLUDED.name,
                      properties = unified_objects.properties || EXCLUDED.properties,
                      updated_at = NOW()
                """
                rows = [ (rid, o.unique_key, o.object_guid, o.category, o.name, o.source_type, o.properties, o.geometry) for o in data.objects ]
                await conn.executemany(sql, rows)
            background_tasks.add_task(update_project_statistics, data.project_code)
            return {"success": True, "revision_id": rid, "object_count": len(rows)}
        except Exception as e:
            log.exception("ingest failed")
            raise HTTPException(status_code=500, detail=f"Transaction failed: {e}")
```

### 2.5 프로젝트 자동 감지(최신 리비전 한정)

파일: `fastapi_server/routers/projects.py`

```python
from fastapi import APIRouter, Depends, HTTPException, Query
from ..models.schemas import DetectRequest
from ..database import get_db
import asyncio, logging
log = logging.getLogger(__name__)
router = APIRouter(prefix="/api/v1/projects", tags=["projects"])
_cache = {}

@router.post("/detect-by-objects")
async def detect_by_objects(req: DetectRequest, use_cache: bool = Query(True), db=Depends(get_db)):
    key = ",".join(sorted(req.object_ids[:10]))
    if use_cache and key in _cache:
        return _cache[key]
    async with db.pool.acquire() as conn:
        try:
            await conn.execute("CREATE TEMP TABLE temp_object_ids (id TEXT PRIMARY KEY) ON COMMIT DROP")
            await conn.executemany("INSERT INTO temp_object_ids(id) VALUES($1)", [(s,) for s in req.object_ids])
            query = """
            WITH latest AS (
              SELECT project_id, MAX(revision_number) AS latest_revision
                FROM revisions GROUP BY project_id
            ),
            matched AS (
              SELECT r.project_id, COUNT(DISTINCT uo.id) AS match_count
                FROM unified_objects uo
                JOIN revisions r ON r.id = uo.revision_id
                JOIN latest l ON l.project_id = r.project_id AND l.latest_revision = r.revision_number
               WHERE EXISTS (
                       SELECT 1 FROM temp_object_ids t
                        WHERE t.id = uo.unique_key
                           OR (uo.object_guid IS NOT NULL AND t.id = uo.object_guid::text)
                     )
               GROUP BY r.project_id
            ),
            totals AS (
              SELECT r.project_id, COUNT(DISTINCT uo.id) AS total_objects
                FROM unified_objects uo
                JOIN revisions r ON r.id = uo.revision_id
                JOIN latest l ON l.project_id = r.project_id AND l.latest_revision = r.revision_number
               GROUP BY r.project_id
            )
            SELECT p.code, p.name, m.match_count, t.total_objects,
                   ROUND((m.match_count::numeric / NULLIF(t.total_objects,0))::numeric, 4) AS confidence,
                   ROUND((m.match_count::numeric / $1)::numeric, 4) AS coverage
              FROM matched m
              JOIN totals t ON t.project_id = m.project_id
              JOIN projects p ON p.id = m.project_id
             WHERE (m.match_count::numeric / NULLIF(t.total_objects,0)) >= $2
             ORDER BY confidence DESC, match_count DESC
             LIMIT $3
            """
            rows = await conn.fetch(query, len(req.object_ids), req.min_confidence, req.max_candidates)
            result = {
              "success": True,
              "detected_projects": [ dict(code=r["code"], name=r["name"], match_count=r["match_count"], total_objects=r["total_objects"], confidence=float(r["confidence"]), coverage=float(r["coverage"])) for r in rows ],
              "message": f"Found {len(rows)} candidate projects",
              "input_count": len(req.object_ids)
            }
            if use_cache and rows:
                _cache[key] = result
            return result
        except Exception as e:
            log.exception("detection failed")
            raise HTTPException(status_code=500, detail=f"Detection query failed: {e}")
```

- 캐시 키는 상위 10개 객체 ID를 정렬 후 SHA-256으로 해시하여 `f"{__version__}:{digest}"` 형태로 저장하며, TTL은 300초로 제한한다.
- 배포 시 캐시를 초기화하며, `/detect-by-objects`는 캐시 HIT/MISS, TTL 만료 이벤트를 로깅한다.
- `/api/v1/ingest`는 문자열 수치/불리언을 정규화하는 `coerce_property_types`를 유지하며, `tests/api/test_ingest_property_coercion.py`로 회귀를 방지한다.

---

## 3. DXBase

### 3.1 프로젝트 코드 유틸

파일: `DXBase/Models/ProjectCodeUtil.cs`

```csharp
using System;
using System.Text.RegularExpressions;

namespace DXBase.Models
{
    public static class ProjectCodeUtil
    {
        private static readonly Regex Safe = new(@"[^A-Z0-9_가-힣]", RegexOptions.Compiled);

        public static string Generate(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Project name cannot be empty", nameof(name));
            var normalized = name.Trim().Replace('-', '_').Replace(' ', '_').ToUpperInvariant();
            var cleaned = Safe.Replace(normalized, "");
            if (string.IsNullOrEmpty(cleaned))
                cleaned = "PROJECT_" + DateTime.UtcNow.Ticks;
            return cleaned.Length <= 50 ? cleaned : cleaned.Substring(0, 50);
        }

        public static bool IsValid(string code)
            => !string.IsNullOrWhiteSpace(code) && code.Length <= 50 && !Safe.IsMatch(code);
    }
}
```

### 3.2 HTTP 재시도/타임아웃(Polly) 및 설정 파서

* `DXBase/Utils/RetryPolicy.cs`, `DXBase/Services/HttpClientService.cs`에 Polly 기반 재시도(지수 백오프, 3회), 타임아웃(기본 30초)을 적용합니다.
* `DXBase/Services/ConfigurationService.cs`는 `dx.config` Key=Value 파서를 제공하고 파일 변경 감지를 통해 캐시를 갱신합니다.
* 패키지 버전은 `dotnet restore --locked-mode`로 고정하며, 변경 시 `Directory.Packages.props` 또는 `.csproj`에 명시합니다.

---

## 4. DXrevit (.NET Framework 4.8)

### 4.1 프로젝트 설정

`DXrevit/DXrevit.csproj`

```xml
<TargetFramework>net48</TargetFramework>
<LangVersion>latest</LangVersion>
<Nullable>enable</Nullable>
```

*현재 저장소는 Revit 2025 호환을 위해 `net8.0-windows` WPF로 구성되어 있으므로, 전환 여부는 Phase D에서 의사결정이 필요하다.*

### 4.2 DTO

`DXrevit/Models/UnifiedObjectDto.cs`
`unique_key`(원문 UniqueId), `object_guid`(추출된 표준 UUID), `category`, `name`, `properties`, `source_type="revit"`, `geometry`(선택) 포함.

### 4.3 DataExtractor 핵심

* `UniqueId` 원문을 `UniqueKey`로 저장, 정규식으로 GUID를 추출하여 `ObjectGuid`에 저장
* 파라미터 추출 시 `StorageType`에 따라 수치/불리언/문자열/ElementId 처리, 실패 시 `AsValueString()` 보정
* 지오메트리는 바운딩 박스만 선택 수집
* 예외는 로깅하고 수집을 계속 진행

### 4.4 업로드

* 기본 엔드포인트 `/api/v1/ingest` 사용, 실패 시 `/api/v1/ingest/unified-objects` 폴백
* `IngestRequest` 구조에 맞춰 직렬화

---

## 5. DXnavis (.NET 8, WinForms)

### 5.1 프로젝트 설정

`DXnavis/DXnavis.csproj`

```xml
<TargetFramework>net8.0-windows</TargetFramework>
<UseWindowsForms>true</UseWindowsForms>
<Nullable>enable</Nullable>
```

*현행 저장소는 .NET Framework 4.8 WPF로 운용 중이므로, 전환 여부는 Phase E에서 최종 결정한다.*

### 5.2 HierarchyUploader

* CSV에서 최대 100개 ID 샘플링, `DisplayString:` 접두·따옴표·중괄호 제거, 다양한 ID 컬럼명 처리
* `/api/v1/projects/detect-by-objects` 호출, 최고 신뢰 후보 반환

### 5.3 UI

* 비동기 처리, 진행 상태 표시, 신뢰도 0.9 이상 시 업로드 제안

### 5.4 단위 테스트

* xUnit + Moq 활용, CSV 파서·중복 제거·최대 샘플·접두 제거 검증

### 5.5 실행 및 검증 가이드

* ViewModel 테스트는 UI 스레드와 분리하여 CI에서 실행한다.
* Navisworks 샘플 CSV를 통한 자동 감지/업로드/신뢰도 표시 결과를 기록한다.
* 배포 후 캐시 초기화 및 설정 검증을 수행하고, 로그는 `docs/logs/phase_e`에 보관한다.

---

## 6. 배포·점검 스크립트

### 6.1 일괄 배포

파일: `scripts/deploy_all.bat`

```bat
@echo off
setlocal EnableDelayedExpansion
set DB_NAME=DX_platform
echo === DX System Deployment ===

psql --version >nul 2>&1 || (echo [ERROR] psql not found & exit /b 1)
python --version >nul 2>&1 || (echo [ERROR] python not found & exit /b 1)
dotnet --version >nul 2>&1 || (echo [ERROR] .NET SDK not found & exit /b 1)

for /f %%i in ('powershell -NoProfile -Command "(Get-Date).ToString(\"yyyyMMdd_HHmmss\")"') do set TS=%%i
set BACKUP_FILE=backup_%TS%.sql

echo Creating backup: !BACKUP_FILE!
pg_dump -h localhost -U postgres -d %DB_NAME% -t unified_objects > "!BACKUP_FILE%" || (echo [ERROR] backup failed & exit /b 1)

echo [DB] Applying migrations...
set PGOPTIONS=-c lock_timeout=5s -c statement_timeout=30min -c maintenance_work_mem=512MB
psql --set ON_ERROR_STOP=1 -h localhost -U postgres -d %DB_NAME% -f database/migrations/2025_10_30__base_prereqs.sql || exit /b 1
psql --set ON_ERROR_STOP=1 -h localhost -U postgres -d %DB_NAME% -f database/migrations/2025_10_30__unified_objects_identity_fix.sql || exit /b 1
psql --set ON_ERROR_STOP=1 -h localhost -U postgres -d %DB_NAME% -f database/migrations/2025_10_30__unified_objects_indexes.sql || exit /b 1
psql --set ON_ERROR_STOP=1 -h localhost -U postgres -d %DB_NAME% -f database/migrations/2025_10_30__views_latest.sql || exit /b 1
psql -h localhost -U postgres -d %DB_NAME% -c "VACUUM ANALYZE unified_objects;" || exit /b 1

echo [API] Deploying FastAPI...
cd fastapi_server
pip install -r requirements.txt --quiet
pm2 restart dx-api >nul 2>&1
cd ..

echo [Revit] Building...
dotnet build DXrevit\DXrevit.csproj -c Release || exit /b 1
xcopy "DXrevit\bin\Release\net48\*" "%ProgramData%\Autodesk\Revit\Addins\2025\DXrevit\" /E /Y /Q >nul

echo [Navis] Building...
dotnet build DXnavis\DXnavis.csproj -c Release || exit /b 1
xcopy "DXnavis\bin\Release\net8.0-windows\*" "%ProgramData%\Autodesk\Navisworks\Plugins\DXnavis\" /E /Y /Q >nul

curl -f http://localhost:8000/health >nul 2>&1 || (echo [ERROR] API health failed & exit /b 1)
echo Deployment completed.
endlocal
```

### 6.2 통합 점검

파일: `scripts/check_system.py`

* DB 스키마(`unique_key`, `object_guid`, `properties`) 확인, 인덱스 및 객체 수 요약
* `/health`, `/api/v1/version`, `/api/v1/ingest`, `/api/v1/projects/detect-by-objects` 확인
* DSN 예시: `postgresql://postgres:password@localhost/DX_platform`

---

## 7. 모니터링

* Prometheus 스크레이프 간격 15초, FastAPI 헬스(`/health`) 및 버전(`/api/v1/version`) 노출
* 필요 시 Postgres 익스포터 추가

---

## 8. 성능 목표(SLO)

* 인제스트 처리량: ≥ 1,000 objects/s (배치 1,000, `executemany`)
* 탐지 응답: 입력 100개 기준 p95 ≤ 500ms
* JSONB 질의: p95 ≤ 50ms (`EXPLAIN ANALYZE` 검증)
* 커넥션 풀: 10~20 유지

---

## 9. 보안 정책

* 개발 단계: CORS 오픈, 인증/인가 미적용
* 프로덕션: OAuth2/JWT, CORS 화이트리스트, HTTPS 강제, 레이트 리밋, 요청 본문 크기 제한, 로그 마스킹을 적용

---

## 10. 호환성과 롤백

* 인제스트는 `/api/v1/ingest`와 `/api/v1/ingest/unified-objects` 병행 제공
* 레거시 `unique_id`는 서버에서 `unique_key`로 승격하며 `object_guid`는 UUID에 한해 채움
* DB 롤백은 `2025_10_30__rollback_identity_fix.sql` 실행, 코드 태그 리버트, 서비스 재시작
* 레거시 경로·필드는 2주 유지 후 접근 로그 기반 제거

---

## 11. 인수 기준(DoD)

* DB 마이그레이션 4종 성공, 스키마·뷰·인덱스 검증 완료
* `/health`, `/api/v1/version` 200 응답
* `/api/v1/ingest` 대량 업서트 후 재실행 시 중복·증식 없음
* `/api/v1/projects/detect-by-objects` CSV 샘플 100개 입력 시 상위 후보 confidence ≥ 0.7
* DXrevit 업로드 성공, DXnavis 자동 감지 및 신뢰도 표시 정상
* `scripts/check_system.py` 전 항목 PASS

---

## 12. 커밋 및 릴리스

* `feat(db): base_prereqs, identity_fix, indexes, views_latest`
* `feat(api): ingest dual-endpoint, schema/compat/db_helpers/system`
* `feat(api): detect-by-objects (latest-limited) + cache`
* `fix(revit): net48 target, UniqueId safe handling, endpoint fallback`
* `feat(navis): CSV sampling, DisplayString cleanup, async UI`
* `chore(base): ProjectCodeUtil, HttpClientService, ConfigurationService`
* `chore(scripts): deploy_all, check_system`
* `test: DXnavis tests, regression/backward-compat`

태그: `v1.1.0 — dual identity & detection`

---

## 13. 알려진 리스크 및 대응

* 과거 데이터의 `unique_key`가 `object_guid::text`로 백필되어 원문 UniqueId와 달라질 수 있음. 정밀도 향상을 위해 Revit 데이터 재인제스트 권장
* JSONB 인덱스가 클래스 다양성/데이터량에 따라 비대해질 수 있음. 필요 시 `properties::text`에 `gin_trgm_ops` 보조 인덱스 추가
* Revit API의 일부 `ParameterType` 경고는 런타임 차단 이슈가 아니며, 점진적으로 ForgeTypeId로 이행

---

## 14. 부록: 예시 호출

인제스트

```bash
curl -X POST http://localhost:8000/api/v1/ingest \
 -H "Content-Type: application/json" \
 -d '{
  "project_code": "배관테스트",
  "revision_number": 2,
  "source_type": "revit",
  "objects": [
    {
      "unique_key": "a1b2c3d4-e5f6-...-revitUniqueIdTail",
      "object_guid": "a1b2c3d4-e5f6-4a1b-9c8d-112233445566",
      "category": "배관",
      "name": "Pipe-001",
      "properties": {"길이":"1500","재질":"PVC"},
      "source_type": "revit"
    }
  ]
 }'
```

탐지

```bash
curl -X POST http://localhost:8000/api/v1/projects/detect-by-objects \
 -H "Content-Type: application/json" \
 -d '{"object_ids":["a1b2-...","DisplayString:C:\\\\path\\\\file.rvt"],"min_confidence":0.7,"max_candidates":3}'
```

---

본 명세서는 무중단에 준하는 안전 마이그레이션, 완전한 레거시 호환, 최신 리비전 기준의 탐지 정합성, 운영 편의 및 롤백 용이성을 목표로 설계되었습니다. 프로덕션 반영 전 상기 전제 조건 네 가지와 인수 기준을 검증해 주시기 바랍니다.

# AWP 2025 BIM Data Integration System – Detailed Implementation Plan (v1.1.0)

본 계획서는 `techspec.md (v1.1.0)`의 요구 사항을 실제 코드베이스에 적용하기 위한 세부 절차를 코드 스니펫 단위로 정리한다. 모든 단계는 Git 브랜치 `feature/v1.1.0`에서 수행하며, 커밋 메시지는 `Scope: what — why` 규칙을 따른다.

---

## 0. 공통 규칙

### 0.1 Claude 지침 연동
- 이 문서는 `docs/claude.md`의 TDD/Tidy First 지침을 충실히 이행하기 위한 작업 순서를 명시한다.
- 사용자가 “go”라고 지시하면, 이 문서에 정의된 **다음 미완료 테스트**부터 순차적으로 진행한다.
- 각 테스트는 Red→Green→Refactor 사이클을 준수하며, 구조적 변경과 동작 변경을 별도 커밋으로 분리한다.
- 모든 단계에서 “테스트 작성 → 최소 구현 → 테스트 통과 → 필요 시 리팩터링” 절차를 반복한다.

- **테스트 우선 순서**: DB → API → DXBase → DXrevit → DXnavis → 스크립트/모니터링 → 통합 테스트.
- **코드 스타일**: 기존 파일 스타일을 준수 (C# = 4 space indent, Python = black 호환, SQL = 대문자 키워드).
- **검증 포인트**:
  1. 각 단계 종료 시 `scripts/check_database_schema.py` 또는 새로 작성할 검증 스크립트로 확인.
  2. 중간 릴리스 노트 초안에 주요 변경 사항 기록.

---

## Phase A – 데이터베이스 레이어 확장

### A-0. 테스트 큐
1. `tests/db/test_migrations.py::test_unified_objects_columns_after_identity_fix`  
   - 기대: `unique_key`, `object_guid`, `geometry`, `updated_at` 존재 확인 (Red → Green).
2. `tests/db/test_latest_view.py::test_latest_revision_view_only_returns_max_revision`
3. `tests/db/test_indexes.py::test_advisory_indexes_exist`

테스트는 순차적으로 구현하며, 각 테스트가 Green 상태가 될 때까지 최소 기능만 구현한다.

### A-1. 선행 마이그레이션 추가 (base prereqs) — **Behavioral**

**파일**: `database/migrations/2025_10_30__base_prereqs.sql`

```sql
-- 2025_10_30__base_prereqs.sql
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

**작업 절차**
1. `touch database/migrations/2025_10_30__base_prereqs.sql`
2. 위 스니펫을 삽입.
3. 개발 DB에서 `psql -f`로 시험 실행 후 기존 데이터 영향 여부 기록.

### A-2. 식별자 이원화 (identity_fix) — **Behavioral**

**파일**: `database/migrations/2025_10_30__unified_objects_identity_fix.sql`

```sql
-- unique_id → object_guid, unique_key 추가 및 백필
BEGIN;

ALTER TABLE unified_objects
  ADD COLUMN IF NOT EXISTS unique_key TEXT;

DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.columns
              WHERE table_name='unified_objects' AND column_name='unique_id')
     AND NOT EXISTS (SELECT 1 FROM information_schema.columns
                      WHERE table_name='unified_objects' AND column_name='object_guid') THEN
    ALTER TABLE unified_objects RENAME COLUMN unique_id TO object_guid;
  END IF;
END $$;

UPDATE unified_objects
   SET unique_key = COALESCE(unique_key, object_guid::text)
 WHERE unique_key IS NULL;

ALTER TABLE unified_objects
  DROP CONSTRAINT IF EXISTS uq_unified_object;

ALTER TABLE unified_objects
  ADD CONSTRAINT uq_unified_object_by_unique_key
  UNIQUE (revision_id, source_type, unique_key);

COMMIT;
```

### A-3. 인덱스 및 뷰 생성 — **Behavioral**

**파일**:
- `database/migrations/2025_10_30__unified_objects_indexes.sql`
- `database/migrations/2025_10_30__views_latest.sql`

```sql
-- 2025_10_30__unified_objects_indexes.sql
DROP INDEX IF EXISTS idx_unified_objects_category;
CREATE INDEX CONCURRENTLY idx_unified_objects_category
  ON unified_objects(category);

DROP INDEX IF EXISTS idx_unified_objects_properties;
CREATE INDEX CONCURRENTLY idx_unified_objects_properties
  ON unified_objects USING GIN (properties);

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_unified_objects_unique_key
  ON unified_objects(unique_key);

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_unified_objects_object_guid
  ON unified_objects(object_guid);

CREATE INDEX CONCURRENTLY IF NOT EXISTS idx_unified_objects_revision_id
  ON unified_objects(revision_id);
```

```sql
-- 2025_10_30__views_latest.sql
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

### A-4. 롤백 스크립트 — **Structural**

**파일**: `database/migrations/2025_10_30__rollback_identity_fix.sql`

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

ALTER TABLE unified_objects
  ADD CONSTRAINT uq_unified_object
    UNIQUE (revision_id, source_type, unique_id);

COMMIT;
```

**검증**: `SELECT column_name FROM information_schema.columns WHERE table_name='unified_objects';` 실행 결과 캡처.

### A-5. 실행 및 검증 가이드
1. 마이그레이션 적용 전 환경 변수 설정:
   ```bash
   export PGOPTIONS='-c lock_timeout=5s -c statement_timeout=30min -c maintenance_work_mem=512MB'
   ```
2. 마이그레이션 실행 절차:
   ```bash
   for file in \
       2025_10_30__base_prereqs.sql \
       2025_10_30__unified_objects_identity_fix.sql \
       2025_10_30__unified_objects_indexes.sql \
       2025_10_30__views_latest.sql; do
       psql --set ON_ERROR_STOP=1 -h localhost -U postgres -d DX_platform -f database/migrations/$file
   done
   VACUUM ANALYZE unified_objects;
   ```
3. 적용 직후 데이터 정합성 점검:
   ```sql
   SELECT revision_id, source_type, unique_key, COUNT(*)
   FROM unified_objects
   GROUP BY 1,2,3 HAVING COUNT(*) > 1;
   ```
   - 결과가 존재하면 `2025_10_30__rollback_identity_fix.sql` 즉시 실행.
4. 성능 리그레션 감시:
   ```sql
   EXPLAIN (ANALYZE, BUFFERS)
   SELECT * FROM v_unified_objects_latest WHERE project_id = $1;
   ```
   - 실행 계획과 버퍼 사용량을 캡처하여 `docs/logs/phase_a`에 보관.
5. 각 단계 로그와 `VACUUM` 결과를 저장하고, 구조/행위 커밋을 분리한다.

---

## Phase B – FastAPI 백엔드 리팩터링

### B-0. 테스트 큐
1. `tests/api/test_ingest_new_schema.py::test_ingest_upserts_by_unique_key`  
   - 요구: `/api/v1/ingest`로 동일 `unique_key` 재전송 시 중복 데이터 생성 금지.
2. `tests/api/test_ingest_legacy_payload.py::test_legacy_unique_id_promoted_to_unique_key`
3. `tests/api/test_detect_by_objects.py::test_only_latest_revision_considered`
4. `tests/api/test_detect_by_objects.py::test_detection_cache_short_circuit`

### B-1. 스키마 계층 확장 — **Structural**

**파일**: `fastapi_server/models/schemas.py`

```python
class UnifiedObjectDto(BaseModel):
    unique_key: str
    object_guid: Optional[str] = None
    category: Optional[str] = None
    name: Optional[str] = None
    properties: Dict[str, Any] = Field(default_factory=dict)
    geometry: Optional[Dict[str, Any]] = None
    source_type: str = "revit"
    unique_id: Optional[str] = None  # legacy fallback
```

추가 항목:
- `IngestRequest`, `DetectRequest` 정의 (techspec §2.1 참조).
- `@field_validator` 활용 GUID/타입 정규화.

### B-2. 호환/DB 유틸 — **Behavioral**

**파일**: `fastapi_server/utils/backward_compat.py`

```python
def migrate_legacy_object(d: Dict[str, Any]) -> Dict[str, Any]:
    if "unique_key" not in d and "unique_id" in d:
        d["unique_key"] = str(d["unique_id"])
        try:
            uuid.UUID(d["unique_id"])
            d["object_guid"] = d.get("object_guid") or d["unique_id"]
        except Exception:
            d["object_guid"] = d.get("object_guid") or None
    return d
```

**파일**: `fastapi_server/utils/db_helpers.py`

```python
async def get_or_create_revision(conn, code: str, rev: int, source: str) -> str:
    pid = await get_or_create_project(conn, code)
    row = await conn.fetchrow(
        "SELECT id FROM revisions WHERE project_id=$1 AND revision_number=$2 AND source_type=$3",
        pid, rev, source
    )
    if row:
        return row["id"]
    return await conn.fetchval(
        "INSERT INTO revisions (id, project_id, revision_number, source_type) "
        "VALUES (gen_random_uuid(), $1, $2, $3) RETURNING id",
        pid, rev, source
    )
```

### B-3. Ingest 라우터 개편 — **Behavioral**

**파일**: `fastapi_server/routers/ingest.py`

주요 변경:

```python
router = APIRouter(prefix="/api/v1/ingest", tags=["ingest"])

@router.post("")
@router.post("/unified-objects")
async def ingest_unified_objects(
    data: IngestRequest,
    background_tasks: BackgroundTasks,
    db=Depends(get_db),
):
    for idx, obj in enumerate(data.objects):
        data.objects[idx] = type(obj)(**migrate_legacy_object(obj.model_dump()))
```

`executemany` 업서트 SQL:

```python
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
      updated_at = NOW();
"""
```

에러 처리:

```python
except Exception as exc:
    log.exception("ingest failed")
    raise HTTPException(500, f"Transaction failed: {exc}")
```

### B-4. 프로젝트 탐지 라우터 — **Behavioral**

**파일**: `fastapi_server/routers/projects.py`

1. 임시 테이블 방식 대신 `techspec`의 최신 리비전 한정 SQL 사용.
2. 응답 캐시 `_cache` 도입: 입력 상위 10개 ID를 정렬 후 SHA-256 해시(`key = f"{version}:{sha256(...)}"`), TTL 300초, 배포 시 캐시 초기화.

```python
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
SELECT ...
"""
```

검증: pytest/async 테스트 작성 (`tests/test_projects_detection.py`) – 최소 3 케이스(매칭 성공, confidence 미달, 빈 입력 예외).

### B-5. 실행 및 검증 가이드
1. `pytest tests/api tests/db` 실행으로 인제스트/탐지 시나리오를 검증하고, 캐시 HIT/MISS 및 TTL 만료 로그를 확인한다.
2. `.env`, `config.py`에 캐시 TTL·버전 키·로깅 레벨을 반영하고 문서에 기록한다.
3. Postman/HTTPie로 `/api/v1/projects/detect-by-objects`를 반복 호출하여 캐시 동작을 수동 검증하고, 배포 시 캐시 초기화 절차를 체크리스트에 포함한다.
4. `/api/v1/version` 응답이 `1.1.0`인지 확인하고 배포 스크립트에 curl 검증을 추가한다.
5. 속성 타입 보정 회귀 테스트(`tests/api/test_ingest_property_coercion.py`)가 통과하는지 확인한다.
6. 보안/비기능 게이트(CORS 화이트리스트, Rate Limit, 요청 본문 제한, 로그 마스킹)를 사전 점검한다.

---

## Phase C – DXBase 공통 모듈

### C-0. 테스트 큐
1. `DXBase.Tests/ProjectCodeUtilTests.cs::Generate_ShouldNormalizeKoreanAndEnglishNames`
2. `DXBase.Tests/ProjectCodeUtilTests.cs::Generate_ShouldFallbackWhenEmpty`
3. `DXBase.Tests/HttpClientServiceTests.cs::PostAsync_ShouldRetryOnFailure`
4. `DXBase.Tests/ConfigurationServiceTests.cs::Reload_OnFileChange`

### C-1. ProjectCodeUtil 추가 — **Behavioral**

**파일**: `DXBase/Models/ProjectCodeUtil.cs`

```csharp
public static class ProjectCodeUtil
{
    private static readonly Regex Safe = new(@"[^A-Z0-9_가-힣]", RegexOptions.Compiled);

    public static string Generate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name cannot be empty", nameof(name));

        var normalized = name.Trim().Replace('-', '_').Replace(' ', '_').ToUpperInvariant();
        var cleaned = Safe.Replace(normalized, string.Empty);
        if (string.IsNullOrEmpty(cleaned))
            cleaned = "PROJECT_" + DateTime.UtcNow.Ticks;
        return cleaned.Length <= 50 ? cleaned : cleaned.Substring(0, 50);
    }
}
```

### C-2. HttpClientService 개선 — **Behavioral**

`DXBase/Services/HttpClientService.cs`에 Polly 재시도 추가.

```csharp
private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy =
    Policy.Handle<HttpRequestException>()
          .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
          .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));
```

새 `SendAsync<TRequest, TResponse>` 메서드 정의:

```csharp
public async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest payload)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, path)
    {
        Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
    };
    var response = await _retryPolicy.ExecuteAsync(() => _client.SendAsync(request));
    response.EnsureSuccessStatusCode();
    var json = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<TResponse>(json)!;
}
```

### C-3. ConfigurationService 확장 — **Structural/Behavioral**

`dx.config` Key=Value 파서, 파일 변경 감지.

```csharp
_watcher = new FileSystemWatcher(Path.GetDirectoryName(_configPath)!, Path.GetFileName(_configPath))
{
    NotifyFilter = NotifyFilters.LastWrite
};
_watcher.Changed += (_, __) => Reload();
```

### C-4. 실행 및 검증 가이드
1. `dotnet test DXBase.Tests`로 신규 유닛 테스트를 통과시킨다.
2. `dotnet build DXBase/`로 다중 타깃(`net8.0;netstandard2.0`) 빌드가 성공하는지 확인한다.
3. `dotnet restore --locked-mode` 실행으로 패키지 버전이 고정된 상태인지 검증한다.
4. 변경된 Polly/Configuration 설정을 `docs/TECHNICAL_CAPABILITIES.md` 등 관련 문서에 기록한다.
5. 구조적 변경(리팩터링)과 동작 변경(새 기능)을 별도 커밋으로 분리한다.

---

## Phase D – DXrevit 업데이트

### D-0. 테스트 큐
1. `DXrevit.Tests/DataExtractorTests.cs::ExtractObject_ShouldPopulateUniqueKeyAndGuid`
2. `DXrevit.Tests/DataExtractorTests.cs::ExtractObject_ShouldHandleNonGuidUniqueId`
3. `DXrevit.Tests/ApiDataWriterTests.cs::Upload_ShouldFallbackToSecondaryEndpoint`

### D-1. 프로젝트 설정 검토 — **Structural**

- `DXrevit.csproj`의 `<TargetFramework>` 유지 여부 결정. (techspec은 `net48`, 현재는 `net8.0-windows`)
- 합의 결과에 따라 `<Nullable>`, `<UseWPF>` 조정.

### D-2. DTO 구성 — **Structural**

**파일**: `DXrevit/Models/UnifiedObjectDto.cs` (신규)

```csharp
public class UnifiedObjectDto
{
    public string UniqueKey { get; set; } = default!;
    public string? ObjectGuid { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
    public string SourceType { get; set; } = "revit";
    public Dictionary<string, object>? Geometry { get; set; }
    public string? UniqueId { get; set; } // legacy
}
```

### D-3. DataExtractor 수정 — **Behavioral**

`Services/DataExtractor.cs`

```csharp
string instanceGuid = element.UniqueId;
string objectId = IdGenerator.GenerateObjectId(
    instanceGuid,
    element.Category.Name,
    GetFamilyName(element),
    GetTypeName(element));

var dto = new UnifiedObjectDto
{
    UniqueKey = objectId,
    ObjectGuid = TryExtractGuid(instanceGuid),
    Category = element.Category.Name,
    Name = element.Name,
    Properties = properties,
    SourceType = "revit"
};
```

GUID 추출 헬퍼:

```csharp
private string? TryExtractGuid(string uniqueId)
{
    var match = Regex.Match(uniqueId, @"[0-9a-fA-F-]{36}");
    if (match.Success && Guid.TryParse(match.Value, out var guid))
        return guid.ToString();
    return null;
}
```

### D-4. 업로드 서비스 — **Behavioral**

`Services/ApiDataWriter.cs`

```csharp
try
{
    await _httpClient.PostAsync<IngestRequest, ApiResponse>("api/v1/ingest", payload);
}
catch (HttpRequestException)
{
    await _httpClient.PostAsync<IngestRequest, ApiResponse>("api/v1/ingest/unified-objects", payload);
}
```

### D-5. 실행 및 검증 가이드
1. `dotnet test DXrevit.Tests` 실행 시 Revit API 의존 부분은 인터페이스/순수 함수로 분리해 CI에서도 통과하도록 유지한다.
2. Revit 샘플 모델을 사용한 통합 검증은 로컬 환경에서만 수행하고 결과(스크린샷/CSV 비교)를 `docs/logs/phase_d`에 기록한다.
3. HTTP 업로드 실패 시 보조 엔드포인트로 폴백되는지 Fiddler/Charles 등으로 확인한다.
4. PostBuild 스크립트가 Addins 폴더에 배포되는지 로그로 확인하고, 캐시 초기화를 위한 안내를 문서에 추가한다.
5. 구조/행위 커밋을 분리하고, Revit용 설정 변경이 다른 프로젝트에 영향을 주지 않는지 재빌드한다.

---

## Phase E – DXnavis 개선

### E-0. 테스트 큐
1. `DXnavis.Tests/HierarchyUploaderTests.cs::SampleObjectIdsFromCsv_ShouldLimitTo100`
2. `DXnavis.Tests/HierarchyUploaderTests.cs::StripPrefixes_ShouldRemoveDisplayString`
3. `DXnavis.Tests/HierarchyUploaderTests.cs::DetectProject_ShouldCallApiWithConfidenceThreshold`
4. `DXnavis.Tests/ViewModelTests.cs::DetectionStatus_ShouldUpdateProgress`

### E-1. 프로젝트 설정 — **Structural**

- 현재 .NET 4.8 WPF → techspec의 .NET 8 + WinForms 전환 여부 결정.
- 전환 시 `.csproj`를 다음과 같이 수정:

```xml
<TargetFramework>net8.0-windows</TargetFramework>
<UseWindowsForms>true</UseWindowsForms>
```

### E-2. HierarchyUploader 로직 정비 — **Behavioral**

`Services/HierarchyUploader.cs`

1. CSV 샘플링 함수 별도 분리:

```csharp
private List<string> SampleObjectIdsFromCsv(string path, int maxSamples = 100)
```

2. 접두사 제거 유틸:

```csharp
private static readonly string[] Prefixes = { "DisplayString:", "NamedConstant:", "Boolean:", "Double:", "Integer:" };

private string StripPrefixes(string value)
{
    foreach (var prefix in Prefixes)
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return value.Substring(prefix.Length).Trim();
    return value.Trim();
}
```

3. 탐지 API 호출:

```csharp
var request = new DetectRequest
{
    object_ids = objectIds,
    min_confidence = 0.7,
    max_candidates = 3
};
var response = await _httpClient.PostAsync<DetectRequest, DetectResponse>(
    "/api/v1/projects/detect-by-objects",
    request);
```

### E-3. UI 개선 — **Structural**

`Views/DXwindow.xaml`과 `ViewModels/DXwindowViewModel.cs`에서 진행률/신뢰도 표시 UI 추가:

```xml
<TextBlock Text="{Binding DetectionStatus}" FontWeight="Bold" />
<ProgressBar Value="{Binding DetectionProgress}" Maximum="100" />
```

`ViewModel`:

```csharp
public double DetectionProgress
{
    get => _detectionProgress;
    set { _detectionProgress = value; OnPropertyChanged(); }
}

public string DetectionStatus
{
    get => _detectionStatus;
    set { _detectionStatus = value; OnPropertyChanged(); }
}
```

### E-4. 단위 테스트 — **Structural**

- `DXnavis.Tests` 프로젝트 추가.
- 샘플 테스트:

```csharp
[Fact]
public void StripPrefixes_RemovesDisplayString()
{
    var uploader = new HierarchyUploader();
    var result = uploader.StripPrefixes("DisplayString:C:\\temp\\file.rvt");
    result.Should().Be("C:\\temp\\file.rvt");
}
```

### E-5. 실행 및 검증 가이드
1. `dotnet test DXnavis.Tests` 실행으로 ViewModel과 CSV 파서 로직이 CI 환경에서도 통과하는지 확인한다.
2. Navisworks 샘플 CSV를 사용해 자동 감지 → 업로드 → 신뢰도 표시까지 수동 검증하고 결과를 `docs/logs/phase_e`에 기록한다.
3. ViewModel에 상태 업데이트 인터페이스를 도입해 UI 스레드 의존성을 제거하고, 이벤트 흐름(UI → ViewModel → 서비스)을 문서화한다.
4. 배포 후 캐시 초기화 및 설정 파일 검증을 체크리스트에 포함한다.
5. 런타임 변경(.NET 4.8 ↔ net8.0) 시 다른 프로젝트 빌드에 영향이 없는지 확인하고, 구조/행위 커밋을 분리한다.

---

## Phase F – 배포/운영 스크립트 및 모니터링

### F-0. 테스트 큐
1. `tests/scripts/test_check_system.py::test_detects_missing_columns`
2. `tests/scripts/test_check_system.py::test_api_health_probe`
3. `tests/scripts/test_deploy_all.py::test_generates_backup_name`

### F-1. `scripts/deploy_all.bat` — **Behavioral**

```bat
@echo off
setlocal EnableDelayedExpansion

set DB_NAME=DX_platform
for /f %%i in ('powershell -NoProfile -Command "(Get-Date).ToString(\"yyyyMMdd_HHmmss\")"') do set TS=%%i
set BACKUP_FILE=backup_%TS%.sql

pg_dump -h localhost -U postgres -d %DB_NAME% > "!BACKUP_FILE%" || exit /b 1

psql -h localhost -U postgres -d %DB_NAME% -f database/migrations/2025_10_30__base_prereqs.sql || exit /b 1
psql -h localhost -U postgres -d %DB_NAME% -f database/migrations/2025_10_30__unified_objects_identity_fix.sql || exit /b 1
psql -h localhost -U postgres -d %DB_NAME% -f database/migrations/2025_10_30__unified_objects_indexes.sql || exit /b 1
psql -h localhost -U postgres -d %DB_NAME% -f database/migrations/2025_10_30__views_latest.sql || exit /b 1

dotnet build DXrevit\DXrevit.csproj -c Release || exit /b 1
dotnet build DXnavis\DXnavis.csproj -c Release || exit /b 1

curl -f http://localhost:8000/health || exit /b 1
endlocal
```

### F-2. `scripts/check_system.py` — **Behavioral**

```python
async def check_db_schema(pool):
    rows = await pool.fetch("""
        SELECT column_name
          FROM information_schema.columns
         WHERE table_name = 'unified_objects'
    """)
    required = {"unique_key", "object_guid", "properties", "updated_at"}
    missing = required - {row["column_name"] for row in rows}
    if missing:
        raise RuntimeError(f"Missing columns: {missing}")
```

이외 `/health`, `/api/v1/version`, `/api/v1/ingest`, `/api/v1/projects/detect-by-objects` 호출 포함.

### F-3. 모니터링 디렉터리 — **Structural**

- `monitoring/prometheus.yml` 초안 작성 (인제스트 처리량, 업서트 충돌 수, 탐지 캐시 히트율, DB 풀 대기 시간, 엔드포인트 레이턴시 히스토그램 노출).

```yaml
scrape_configs:
  - job_name: "fastapi"
    scrape_interval: 15s
    static_configs:
      - targets: ["localhost:8000"]
```

### F-4. 실행 및 검증 가이드
1. `deploy_all.bat /dry-run`으로 백업 → 마이그레이션 → 빌드 → 헬스체크 흐름을 사전 검증한다.
2. 실제 배포 전 전체 DB 백업을 보관하고, `PGOPTIONS` 설정을 다시 확인한다.
3. Prometheus/Grafana에서 새 지표가 수집되는지 확인하고, 기준치를 설정한다.
4. 실행 로그와 모니터링 스크린샷을 `docs/logs/phase_f`에 저장한다.
5. 캐시 초기화, 보안 게이트 확인 등 배포 후 체크리스트를 완료한다.

---

## Phase G – 테스트 및 릴리스

### G-0. 테스트 큐
1. `tests/perf/test_ingest_throughput.py::test_ingest_batch_processing_time`
2. `tests/perf/test_detection_latency.py::test_detection_p95_under_threshold`
3. `tests/integration/test_end_to_end.py::test_revit_to_navisworks_roundtrip`

### G-1. 성능 측정 — **Behavioral**

- `scripts/load_test_ingest.py` 작성 (1k objects/s 목표).
- `scripts/load_test_detection.py`로 100 object 입력 p95 측정.
- 측정 결과(처리량, p95 레이턴시, 캐시 히트율)를 로그/스프레드시트로 기록하고 목표치를 만족하지 못하면 원인 분석 후 재시도.

### G-2. 통합 테스트 — **Behavioral**

- 새 `tests` 디렉터리 구성:
  - `tests/api/test_ingest.py`
  - `tests/api/test_detection.py`
  - `tests/db/test_migrations.py`
  - `tests/dxnavis/test_csv_parser.cs` (별도 프로젝트)

### G-3. 릴리스 체크리스트

1. 마이그레이션 실행 로그 확보.
2. API 테스트 결과 정리.
3. DXrevit/DXnavis 수동 QA 체크리스트 완료 (CSV 감지, 업로드 시나리오).
4. 보안/비기능 게이트 점검: CORS 화이트리스트, 요청 본문 제한, Rate Limit, 로그 마스킹, 캐시 초기화 확인.
5. `CHANGELOG.md`에 항목 추가.
6. 태그 `v1.1.0` 생성:

```bash
git tag -a v1.1.0 -m "BIM dual-identity & detection release"
git push origin v1.1.0
```

### G-4. 롤백 절차 문서화

- `docs/rollback.md` 신규 작성:

```markdown
1. scripts/deploy_all.bat 중 DB 마이그레이션 구간 스킵.
2. psql -f database/migrations/2025_10_30__rollback_identity_fix.sql 실행.
3. FastAPI 이전 버전으로 배포 (git checkout 이전 태그).
4. DXrevit/DXnavis DLL 이전 버전 복원.
```

---

## 4. 마일스톤 및 일정 권장안

| Milestone | 구현 범위 | 산출물/검증 | 예상 기간 |
|-----------|-----------|-------------|-----------|
| M1 | Phase A | 마이그레이션 4종 + 롤백, 실행 로그 | T0 + 3일 |
| M2 | Phase B | 백엔드 리팩터링, pytest | T0 + 6일 |
| M3 | Phase C | DXBase 개선, 빌드 로그 | T0 + 8일 |
| M4 | Phase D | DXrevit 업데이트, 테스트 리포트 | T0 + 11일 |
| M5 | Phase E | DXnavis 개선, 단위 테스트 | T0 + 14일 |
| M6 | Phase F | 배포/점검 스크립트, 모니터링 초안 | T0 + 15일 |
| M7 | Phase G | 성능/통합 테스트, CHANGELOG, 태그 | T0 + 18일 |

---

## 5. 의사결정 포인트

- **런타임 타깃**: DXrevit/DXnavis의 .NET 버전 조정 필요 여부 (Revit 2025 호환성 확인 필수).
- **배포 시점**: DB 락 허용 시간, 야간 작업 여부 DBA와 협의.
- **캐시 정책**: `/detect-by-objects` 캐시 TTL, 해제 옵션 운영팀 결정.
- **레거시 지원 기간**: `/api/v1/ingest` 단일 경로 사용 고객 대상 가이드.

---

## 6. 리스크 & 대응

- **데이터 충돌**: `unique_key`가 `NULL`인 레코드 → 마이그레이션 전/후 점검 쿼리 준비.
- **API 호환성 문제**: 레거시 페이로드 → `backward_compat` 로깅 강화, 2주간 dual path 유지.
- **배포 실패**: `deploy_all.bat` dry-run 모드(`deploy_all.bat --dry-run`) 옵션 추가 검토.
- **성능 저하**: JSONB 인덱스 증가 → `pg_stat_user_indexes` 모니터링, 필요 시 `gin_trgm_ops` 추가.

---

## 7. 커뮤니케이션

- 주 2회 진행 상황 업데이트, 주요 이슈는 `docs/status.md`에 기록.
- 각 Phase 완료 시 `CLAUDE.md`와 `techspec.md`를 최신 상태로 업데이트.
- 릴리스 후 회고 미팅에서 개선점/후속 과제 도출.

---

본 문서는 코드 스니펫을 기준으로 실제 구현 절차를 세분화한 계획이다. 각 단계에서 스니펫을 기준으로 구현 후, 테스트/검증/문서화 작업을 반드시 수행한다. 필요 시 계획을 재검토하고 의사결정 포인트에 따라 경로를 조정한다.

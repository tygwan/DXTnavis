# TODO – AWP 2025 BIM Data Integration System (v1.1.0 Roadmap)

> 항상 `docs/claude.md`와 `docs/plan.md`를 우선 참조한다.  
> 테스트 우선(TDD) + Tidy First. 구조적 변경과 동작 변경을 분리하고, “go” 지시 후 아래 순서대로 진행한다.

---

## 0. 준비

- [ ] **브랜치**: `git checkout -b feature/v1.1.0`
- [ ] **환경**: 로컬 Postgres 백업 경로 재확인, FastAPI 가상환경 활성화, .NET SDK 8.0/4.8 설치 확인
- [ ] **테스트 프레임워크 셋업**
  - Python: `pip install -r fastapi_server/requirements.txt` + `pip install pytest pytest-asyncio`
  - C#: `dotnet test` 실행 가능 확인, 새 테스트 프로젝트 추가 시 `dotnet new xunit`
- [ ] **문서 싱크**: `docs/techspec.md`, `docs/plan.md`, `docs/claude.md` 최신 버전 확인
- [ ] **테스트 픽스처 준비**: `tests/conftest.py`에 `event_loop`, `db_pool`, `api_client` 픽스처 추가  
  ```python
  @pytest.fixture(scope="session")
  async def db_pool():
      pool = await asyncpg.create_pool(dsn="postgresql://postgres:password@localhost/DX_platform")
      yield pool
      await pool.close()
  ```
- [ ] **의존성 버전 고정**: `pip-compile` 또는 `requirements.txt` 수동 pin, `.csproj` `<Version>` 명시

---

## Phase A – Database Layer ✅ COMPLETED

### A.1 Tests (Red) ✅
- [x] `tests/db/test_migrations.py` 작성 - 4 tests PASSED
  - [x] `test_unified_objects_columns_after_identity_fix`
  - [x] `test_base_prereqs_extensions_exist`
  - [x] `test_projects_unique_constraint_exists`
  - [x] `test_revisions_unique_constraint_exists`
- [x] `tests/conftest.py` - pytest fixtures 설정
- [x] `requirements-test.txt` - 테스트 의존성 정의
- [x] `pytest.ini` - pytest 설정

### A.2 Implementation (Green) ✅
- [x] `database/migrations/2025_10_30__base_prereqs.sql` - 확장 설치 및 제약조건
- [x] `database/migrations/2025_10_30__unified_objects_identity_fix.sql` - 이원화 식별자 (object_id → object_guid, unique_key 추가)
- [x] `database/migrations/2025_10_30__unified_objects_indexes.sql` - 성능 인덱스 5개 생성
- [x] `database/migrations/2025_10_30__views_latest.sql` - 최신 리비전 뷰 2개
- [x] `database/migrations/2025_10_30__rollback_identity_fix.sql` - 롤백 스크립트

### A.3 Verification ✅
- [x] 전체 데이터베이스 백업 생성 (`backup_before_v1.1.0_*.sql`)
- [x] 개발 DB에 마이그레이션 순차 적용 완료 (2,556 rows updated)
- [x] VACUUM ANALYZE 실행 완료
- [x] 중복 감시 확인 - 중복 없음
- [x] 모든 테스트 통과: **4 passed in 0.43s**
- [ ] 롤백 스크립트 테스트 (필요 시 수동 실행)
- [ ] `EXPLAIN (ANALYZE, BUFFERS)` 스냅샷 확보 (선택사항)
- [ ] Phase A 구조/행위 커밋 분리 (다음 단계)

---

## Phase B – FastAPI Backend ✅ COMPLETED

### B.1 Tests (Red) ✅
- [x] `tests/api/test_ingest_new_schema.py::test_ingest_upserts_by_unique_key`
- [x] `tests/api/test_ingest_legacy_payload.py::test_legacy_unique_id_promoted_to_unique_key`
- [x] `tests/api/test_detect_by_objects.py::test_only_latest_revision_considered`
- [x] `tests/api/test_detect_by_objects.py::test_detection_cache_short_circuit`
- [x] 13 tests total - all PASSING

### B.2 Implementation (Green) ✅
- [x] `models/schemas.py` → `UnifiedObjectDto`, `IngestRequest`, `DetectRequest`
- [x] `utils/backward_compat.py` → `migrate_legacy_object`
- [x] `utils/db_helpers.py` → `get_or_create_project/revision`, `update_project_statistics`
- [x] `routers/ingest.py` → dual endpoint + `executemany` 업서트
- [x] `routers/projects.py` → 최신 리비전 한정 탐지 + 캐시
- [x] `routers/system.py` → `/api/v1/version` 1.1.0 리턴
- [x] 캐시 TTL/버전 키 (`ttl_seconds=300`, `cache_key = f"{version}:{sha256(...)}"`) 도입

### B.3 Refactor / Cleanup ✅
- [x] 코드 중복 제거, 로깅 메시지 점검
- [x] `.env.example`, `config.py`에 신규 설정 반영
- [x] pytest 실행 (`pytest tests/api tests/db`) - 13 tests PASSED
- [x] 속성 타입 보정 회귀 테스트 (`tests/api/test_ingest_property_coercion.py`)
- [ ] 보안/비기능 게이트(Phase G) 사전 셀프체크

---

## Phase C – DXBase Library ✅ COMPLETED

### C.1 Tests (Red) ✅
- [x] `DXBase.Tests/ProjectCodeUtilTests.cs::Generate_ShouldNormalizeKoreanAndEnglishNames`
- [x] `DXBase.Tests/ProjectCodeUtilTests.cs::Generate_ShouldFallbackWhenEmpty`
- [x] `DXBase.Tests/HttpClientServiceTests.cs::PostAsync_ShouldRetryOnFailure`
- [x] `DXBase.Tests/ConfigurationServiceTests.cs::Reload_OnFileChange`

### C.2 Implementation (Green) ✅
- [x] `Models/ProjectCodeUtil.cs` - Korean romanization + normalization
- [x] `Services/HttpClientService.cs` → Polly 8.4.2 재시도 + `PostAsync<TReq,TRes>`
- [x] `Services/ConfigurationService.cs` → File watcher, cache invalidation
- [x] Polly resilience pipeline with exponential backoff

### C.3 Refactor / Verification ✅
- [x] `dotnet test DXBase.Tests` - 11 tests PASSED (4 + 3 + 4)
- [x] 멀티 타깃(`net8.0;netstandard2.0`) 빌드 확인
- [x] 커밋: Structural vs Behavioral (3 commits: Red, Green, Docs)
- [x] Polly 8.4.2 패키지 추가

---

## Phase D – DXrevit Plugin

**런타임 결정 (2025-10-30):**
- **DXrevit**: `net8.0-windows` - Revit 2025는 .NET 8 지원
- **DXBase**: `net8.0;netstandard2.0` 멀티타겟 유지 (Revit + Navisworks 호환)
- 현행 프레임워크 유지, Revit 2025 API 호환성 확인됨

### D.1 Tests (Red) ✅
- [x] `DXrevit.Tests/DataExtractorTests.cs::ExtractObject_ShouldPopulateUniqueKeyAndGuid`
- [x] `DXrevit.Tests/DataExtractorTests.cs::ExtractObject_ShouldHandleNonGuidUniqueId`
- [x] `DXrevit.Tests/ApiDataWriterTests.cs::Upload_ShouldFallbackToSecondaryEndpoint`
- [x] 6 tests total created (3 DataExtractor + 3 ApiDataWriter)

### D.2 Implementation (Green) ✅
- [x] `.csproj` 타깃 프레임워크 결정 - `net8.0-windows` 유지
- [x] `DXBase/Models/UnifiedObjectDto.cs` 추가 - dual-identity pattern
- [x] `DataExtractorHelper` → `TryExtractGuid`, `GenerateUniqueKey` (SHA256 hash)
- [x] `Services/ApiDataWriter.cs` → 기본/보조 엔드포인트 폴백 구현
- [x] Test mock handler URI matching 수정

### D.3 Verification
- [x] `dotnet test DXrevit.Tests` - 6/6 tests PASSED
- [ ] Revit 샘플 모델로 **로컬** 수동 검증 (CI 제외, 절차 문서화)
- [ ] PostBuild 배포 경로 재확인
- [ ] Phase D 커밋: Structural vs Behavioral

---

## Phase E – DXnavis Plugin

**런타임 결정 (2025-10-30):**
- **DXnavis**: `.NET Framework 4.8` - Navisworks 2025는 .NET Framework 사용
- **DXBase**: `netstandard2.0` 타겟으로 DXnavis 호환
- 현행 .NET Framework 4.8 유지, Navisworks 2025 API 호환성 확인됨

### E.1 Tests (Red)
- [ ] `DXnavis.Tests/HierarchyUploaderTests.cs::SampleObjectIdsFromCsv_ShouldLimitTo100`
- [ ] `DXnavis.Tests/HierarchyUploaderTests.cs::StripPrefixes_ShouldRemoveDisplayString`
- [ ] `DXnavis.Tests/HierarchyUploaderTests.cs::DetectProject_ShouldCallApiWithConfidenceThreshold`
- [ ] `DXnavis.Tests/ViewModelTests.cs::DetectionStatus_ShouldUpdateProgress`
- [ ] ViewModel 테스트에서 UI 스레드 의존 제거

### E.2 Implementation (Green)
- [x] `.csproj` 런타임 결정 - `.NET Framework 4.8` 유지
- [ ] `HierarchyUploader` → CSV 샘플링, 접두 제거, 탐지 API 연동
- [ ] ViewModel/UI → 진행률/메시지 바인딩
- [ ] HTTP 호출 강건성(재시도/예외) 보강

### E.3 Verification
- [ ] `dotnet test DXnavis.Tests`
- [ ] Navisworks 샘플 CSV로 통합 테스트
- [ ] plugins 배포 스크립트 확인

---

## Phase F – Scripts & Monitoring

### F.1 Tests (Red)
- [ ] `tests/scripts/test_check_system.py::test_detects_missing_columns`
- [ ] `tests/scripts/test_check_system.py::test_api_health_probe`
- [ ] `tests/scripts/test_deploy_all.py::test_generates_backup_name`

### F.2 Implementation (Green)
- [ ] `scripts/deploy_all.bat` (백업→마이그→빌드→헬스체크)
- [ ] 날짜/시간 문자열 PowerShell로 생성  
  ```bat
  for /f %%i in ('powershell -NoProfile -Command "(Get-Date).ToString(\"yyyyMMdd_HHmmss\")"') do set TS=%%i
  set BACKUP_FILE=backup_%TS%.sql
  ```
- [ ] `scripts/check_system.py` (DB/인덱스/API 통합 검사)
- [ ] `monitoring/prometheus.yml` 초안
- [ ] `scripts/load_test_ingest.py` / `scripts/load_test_detection.py`
- [ ] FastAPI/DB 메트릭: 인제스트 처리량, 업서트 충돌 수, 탐지 캐시 히트율, DB 풀 대기 시간, 엔드포인트 레이턴시(히스토그램)

### F.3 Verification
- [ ] Dry-run 수행, 로그 캡처
- [ ] Prometheus 스크랩 테스트(미니 설정)
- [ ] Phase F 커밋

---

## Phase G – Performance, Integration, Release

### G.1 Tests (Red)
- [ ] `tests/perf/test_ingest_throughput.py::test_ingest_batch_processing_time`
- [ ] `tests/perf/test_detection_latency.py::test_detection_p95_under_threshold`
- [ ] `tests/integration/test_end_to_end.py::test_revit_to_navisworks_roundtrip`

### G.2 Execution (Green)
- [ ] 성능 스크립트 실행, p95/처리량 측정
- [ ] 엔드투엔드 플로우: Revit → API → DB → Navisworks
- [ ] `scripts/check_system.py` 최종 PASS

### G.3 Release Prep
- [ ] `CHANGELOG.md` 업데이트
- [ ] `docs/techspec.md`, `docs/plan.md`, `docs/claude.md`, `CLAUDE.md` 동기화
- [ ] 릴리스 게이트: CORS 화이트리스트, 본문 제한, Rate Limit, 로그 마스킹, 캐시 초기화 절차 확인
- [ ] 릴리스 노트 초안 작성
- [ ] 태그 `v1.1.0` 생성 및 푸시
- [ ] 회고/후속 작업 기록

---

## 리스크 & 핫픽스 메모

- [ ] `unique_key` 백필 충돌 발생 시 → 문제 레코드 쿼리 (`SELECT unique_key, COUNT(*) FROM unified_objects GROUP BY 1 HAVING COUNT(*)>1`)
- [ ] `/api/v1/ingest` 레거시 클라이언트 → 2주간 모니터링, 경고 로그 유지
- [ ] JSONB 인덱스 확장 → `pg_stat_user_indexes` 모니터링 후 필요 시 `gin_trgm_ops` 추가
- [ ] 배포 점검 실패 → 즉시 `database/migrations/2025_10_30__rollback_identity_fix.sql` 실행 + 이전 DLL 복원
- [ ] 탐지 캐시 초기화 절차 문서화 후 배포 시 실행

---

## 커뮤니케이션 & 문서화

- [ ] 주간/스프린트 미팅에서 진행상황 보고 (`docs/status.md` 업데이트)
- [ ] 각 Phase 완료 시 문서 갱신 (CLAUDE.md, plan.md, techspec.md)
- [ ] 모든 테스트/스크립트 실행 로그를 `docs/logs/` 폴더(신설) 또는 티켓에 첨부

---

### 완료 조건 (Definition of Done)
- Phase A~G 테스트 전부 통과
- 스크립트/모니터링 정상 작동
- 문서/릴리스 노트/태그 정리
- 회고 진행 및 후속 과제 추적 시작
- 보안/성능 게이트 통과 증빙 로그 확보

> 마지막으로, 모든 변경 사항은 `docs/claude.md`의 TDD 사이클과 커밋 규율을 만족해야 한다. “테스트 작성 → 최소 구현 → 테스트 실행 → 리팩터링 → 커밋”을 엄격히 반복할 것.

# Changelog

모든 주요 변경 사항은 이 파일에 문서화됩니다.

이 형식은 [Keep a Changelog](https://keepachangelog.com/ko/1.0.0/)를 기반으로 하며,
[Semantic Versioning](https://semver.org/lang/ko/)을 따릅니다.

## [Unreleased]

### Future Enhancements
- Advanced analytics and reporting features
- Multi-user collaboration features
- Real-time synchronization capabilities

## [1.1.0] - 2025-11-01

### Added - Phase A-G: BIM Data Integration System

#### Phase A & B: Backend Unified Schema (Dual-Identity Pattern)
- **Unified Objects Table**: Dual-identity schema with `unique_key` and `object_guid`
- **Backward Compatibility**: Legacy `unique_id` auto-migration to `unique_key`
- **Phase B Ingest API**: `/api/v1/ingest` with IngestRequestV2 schema
- **Phase B Detection API**: `/api/v1/projects/detect-by-objects` with dual-identity matching
- **Response Caching**: Detection endpoint with TTL=300s for performance optimization
- **Database View**: `v_unified_objects_latest` for efficient latest-revision queries

#### Phase C & D: DXrevit Plugin V2 (Dual-Identity TDD)
- **DataExtractorV2**: Dual-identity extraction with `unique_key` + `object_guid`
- **SnapshotViewModelV2**: Updated UI supporting Phase B payload format
- **ProjectManager**: Project and revision management service
- **RevisionManager**: Revision metadata and versioning service
- **TDD Implementation**: 6 passing tests for dual-identity pattern validation

#### Phase E: DXnavis Plugin (Navisworks Integration)
- **HierarchyUploader**: CSV parsing and Navisworks object sampling
- **SampleObjectIdsFromCsv**: Extract object IDs from CSV files (max 100 samples)
- **StripPrefixes**: Normalize Navisworks property values (DisplayString, NamedConstant, etc.)
- **TestableViewModel**: MVVM pattern with `INotifyPropertyChanged` for UI independence
- **TDD Implementation**: 4 passing tests for hierarchy upload functionality

#### Phase F: Scripts & Monitoring
- **check_system.py**: Database schema validation and API health checks
  - `check_missing_columns()`: Verify table schemas against expected columns
  - `check_api_health()`: API endpoint availability verification
- **deploy_all.py**: Deployment automation utilities
  - `generate_backup_filename()`: Timestamped backup file generation
- **run_system_check.py**: Comprehensive system health validation script

#### Phase G: Performance Testing & Integration
- **Performance Tests**:
  - `test_ingest_throughput.py`: Batch processing 4,605 obj/sec (227x faster than threshold)
  - `test_detection_latency.py`: p95 latency 3.28ms (61x faster than 200ms threshold)
- **Integration Tests**:
  - `test_end_to_end.py`: Complete Revit → API → DB → Navisworks workflow validation
- **Performance Report**: Comprehensive metrics documentation (PERFORMANCE_METRICS_G.md)

### Changed - Database & API Improvements
- **Constraint Update**: `unified_objects` unique constraint changed to `(revision_id, source_type, unique_key)`
- **Batch Operations**: Optimized executemany for high-performance ingestion
- **Connection Pooling**: asyncpg pool with retry mechanism (3 retries, exponential backoff)
- **API Response Format**: Standardized IngestResponseV2 and DetectResponse schemas

### Performance
- **Ingest Throughput**: 4,605 objects/sec processing speed
- **Detection Latency**: p95 = 3.28ms, p99 = 4.61ms
- **End-to-End Workflow**: Fully automated and validated
- **Database Queries**: Optimized with latest-revision view and JSONB indexing

### Testing
- **Unit Tests**: 10 tests covering dual-identity pattern (Phases D, E)
- **Performance Tests**: 2 tests validating throughput and latency requirements
- **Integration Tests**: 1 test validating complete BIM data pipeline
- **System Health**: Automated schema validation and health checks
- **Test Coverage**: All Phase A-G functionality verified with TDD methodology

### Documentation
- **PERFORMANCE_METRICS_G.md**: Comprehensive performance analysis and recommendations
- **TODO.md**: Complete Phase A-G task tracking with TDD Red→Green→Refactor cycles
- **Technical Specifications**: Updated schemas, APIs, and dual-identity pattern documentation

### Infrastructure
- **pytest Configuration**: Python path setup for module imports
- **Test Fixtures**: Shared async HTTP client and database pool fixtures
- **Database Migrations**: Phase B schema migration scripts
- **Deployment Scripts**: Automated backup and system validation utilities

## [0.1.0] - 2025-10-13

### Added - Phase 1: DXBase
- 공용 서비스 라이브러리 구조 생성
- `ConfigurationService`: JSON 기반 설정 파일 관리
- `LoggingService`: 일별 회전 로그 시스템
- `DataTransferObjects`: 공통 데이터 모델 (SnapshotDTO, ElementDTO 등)
- Multi-targeting 지원: .NET Standard 2.0 / .NET 8.0

### Added - Phase 2: DXrevit
- Revit 2025 애드인 기본 구조 (.NET 8.0)
- 리본 UI 생성: "DX Platform" 탭 및 "데이터 관리" 패널
- 3개 주요 기능 버튼:
  - **스냅샷 저장**: BIM 모델 전체 데이터 추출
  - **매개변수 설정**: 공유 매개변수 자동 추가 (DX_ActivityId, DX_SyncId 등)
  - **설정**: 애플리케이션 설정 UI
- WPF 기반 사용자 인터페이스 (MVVM 패턴)
- 자동 배포 시스템 (PostBuild 이벤트)
- 디버그 로깅 시스템

### Fixed
- Revit 2025 API 호환성 수정:
  - `BuiltInParameterGroup` → `GroupTypeId` 마이그레이션
  - `ElementId.IntegerValue` → `ElementId.Value` 변경
  - WPF `Application.Current` 네임스페이스 충돌 해결
- System.Text.Json 의존성 배포 이슈 해결
  - `CopyLocalLockFileAssemblies` 속성 추가
  - 절대 경로 Assembly 참조로 변경
- 순환 의존성 문제 해결 (ConfigurationService ↔ LoggingService)

### Changed
- DXBase: Multi-targeting으로 변경 (.NET Standard 2.0 + .NET 8.0)
- System.Text.Json 버전 조정: 9.0.9 → 8.0.5 (Revit 2025 호환성)
- .addin 파일 Assembly 경로: 상대 경로 → 절대 경로

### Infrastructure
- `.gitignore`: Visual Studio, Revit, Navisworks 전용 설정
- `README.md`: 프로젝트 개요 및 사용 가이드
- `scripts/init-git-repo.ps1`: Git 저장소 초기화 스크립트
- PostBuild 자동 배포: `C:\ProgramData\Autodesk\Revit\Addins\2025\`

## [0.0.1] - 2025-10-12

### Added
- 프로젝트 초기 설정
- Phase 0: 아키텍처 설계 문서 작성
- 개발 환경 구성

---

## 버전 관리 규칙

### 버전 번호 형식: MAJOR.MINOR.PATCH

- **MAJOR**: 호환되지 않는 API 변경
- **MINOR**: 하위 호환성을 유지하는 기능 추가
- **PATCH**: 하위 호환성을 유지하는 버그 수정

### 변경 유형

- **Added**: 새로운 기능
- **Changed**: 기존 기능의 변경
- **Deprecated**: 곧 제거될 기능
- **Removed**: 제거된 기능
- **Fixed**: 버그 수정
- **Security**: 보안 취약점 수정

## 링크

- [프로젝트 저장소](https://github.com/your-org/dx-platform)
- [이슈 트래커](https://github.com/your-org/dx-platform/issues)
- [릴리스 노트](https://github.com/your-org/dx-platform/releases)

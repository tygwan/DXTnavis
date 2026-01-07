# DX Platform PostgreSQL Database

BIM 데이터 파이프라인을 위한 PostgreSQL 데이터베이스 스키마 및 초기화 스크립트

## 📁 디렉토리 구조

```
database/
├── tables/                     # 원시 데이터 테이블
│   ├── metadata.sql            # 버전 메타데이터
│   ├── objects.sql             # BIM 객체 데이터
│   └── relationships.sql       # 객체 간 관계
├── views/                      # 분석용 뷰
│   ├── analytics_version_summary.sql
│   └── analytics_4d_link_data.sql
├── functions/                  # 저장 함수
│   ├── fn_compare_versions.sql
│   └── fn_get_object_history.sql
├── triggers/                   # 트리거
│   └── prevent_raw_data_modification.sql
├── security/                   # 보안 설정
│   └── roles_and_permissions.sql
└── init_database.sql          # 전체 초기화 스크립트
```

## 🚀 빠른 시작

### 1. PostgreSQL 설치

**Windows:**
```bash
# PostgreSQL 15+ 다운로드 및 설치
# https://www.postgresql.org/download/windows/
```

**Linux (Ubuntu/Debian):**
```bash
sudo apt update
sudo apt install postgresql-15
```

### 2. 데이터베이스 생성

```bash
# PostgreSQL 서버 접속
psql -U postgres

# 데이터베이스 생성
CREATE DATABASE dx_platform WITH ENCODING='UTF8';

# 데이터베이스 연결
\c dx_platform
```

### 3. 스키마 초기화

**방법 1: psql 명령줄에서**
```bash
cd database
psql -U postgres -d dx_platform -f init_database.sql
```

**방법 2: psql 내에서**
```sql
\c dx_platform
\i /path/to/database/init_database.sql
```

## 🔐 보안 설정

### 비밀번호 변경 (필수!)

```sql
-- API 서버용 역할 비밀번호 변경
ALTER ROLE dx_api_role PASSWORD 'YourStrongPassword123!';

-- 읽기 전용 역할 비밀번호 변경
ALTER ROLE dx_readonly_role PASSWORD 'YourReadOnlyPassword456!';
```

### 연결 문자열 예시

**FastAPI 서버 (Python):**
```python
DATABASE_URL = "postgresql://dx_api_role:YourStrongPassword123!@localhost:5432/dx_platform"
```

**Power BI (OData):**
```
Server: localhost
Database: dx_platform
Username: dx_readonly_role
Password: YourReadOnlyPassword456!
```

## 📊 데이터 구조

### 원시 데이터 테이블

#### metadata
버전 메타데이터 저장
- Primary Key: `model_version`
- 주요 컬럼: `timestamp`, `project_name`, `created_by`

#### objects
BIM 객체 데이터 저장
- Primary Key: `id` (BIGSERIAL)
- Unique Key: (`model_version`, `object_id`)
- JSONB 컬럼: `properties`, `bounding_box`

#### relationships

#### revision_versions
Revit/Navisworks 소스에서 추출된 `model_version` 값과 실제 `revisions` 레코드를 연결하는 매핑 테이블입니다.
- Primary Key: `model_version`
- 주요 컬럼: `revision_id`, `source_type` (`revit|navisworks|both`), `source_file_path`, `extracted_at`
- 용도: DXrevit/DXnavis ingest 시 동일 리비전을 재사용하고, 소스별 스냅샷 추적

객체 간 관계 저장
- Primary Key: `id` (BIGSERIAL)
- Unique Key: (`model_version`, `source_object_id`, `target_object_id`, `relation_type`)

### 분석용 뷰

#### analytics_version_summary
버전별 요약 정보 (카테고리별 객체 수, 관계 수 등)

#### analytics_4d_link_data
TimeLiner 자동화를 위한 매핑 데이터

### 주요 함수

#### fn_compare_versions(v1, v2)
두 버전 간 변경 사항 계산 (ADDED, DELETED, MODIFIED)

#### fn_get_object_history(object_id)
특정 객체의 전체 버전 이력 조회

## 🧪 테스트 쿼리

### 1. 테스트 데이터 삽입

```sql
-- 메타데이터 삽입
INSERT INTO metadata (model_version, project_name, created_by, description)
VALUES ('v1.0.0', 'Test Project', 'John Doe', 'Initial version');

-- 객체 삽입
INSERT INTO objects (model_version, object_id, element_id, category, family, type, activity_id, properties)
VALUES
    ('v1.0.0', 'obj-001', 12345, 'Walls', 'Basic Wall', 'Generic - 200mm', 'A1010',
     '{"Length": 5000, "Height": 3000, "Volume": 3.0}'::jsonb),
    ('v1.0.0', 'obj-002', 12346, 'Doors', 'Single Door', '900x2100mm', 'A1020',
     '{"Width": 900, "Height": 2100}'::jsonb);
```

### 2. 분석용 뷰 조회

```sql
-- 버전 요약 조회
SELECT * FROM analytics_version_summary WHERE model_version = 'v1.0.0';

-- 4D 링크 데이터 조회
SELECT * FROM analytics_4d_link_data WHERE model_version = 'v1.0.0';
```

### 3. 함수 실행

```sql
-- 객체 이력 조회
SELECT * FROM fn_get_object_history('obj-001');

-- 버전 비교 (두 번째 버전 생성 후)
SELECT * FROM fn_compare_versions('v1.0.0', 'v1.0.1');
```

## 🛡️ 불변성 보장

데이터 무결성을 위해 다음 트리거가 활성화되어 있습니다:

- ❌ UPDATE 작업 금지
- ❌ DELETE 작업 금지 (CASCADE DELETE 제외)
- ✅ INSERT만 허용

**개발 환경에서 트리거 비활성화:**
```sql
ALTER TABLE metadata DISABLE TRIGGER trg_prevent_metadata_update;
ALTER TABLE objects DISABLE TRIGGER trg_prevent_objects_update;
```

**운영 환경에서 트리거 재활성화:**
```sql
ALTER TABLE metadata ENABLE TRIGGER trg_prevent_metadata_update;
ALTER TABLE objects ENABLE TRIGGER trg_prevent_objects_update;
```

## 🔧 유지보수

### 데이터베이스 크기 확인

```sql
SELECT
    pg_size_pretty(pg_database_size('dx_platform')) AS total_size,
    pg_size_pretty(pg_total_relation_size('metadata')) AS metadata_size,
    pg_size_pretty(pg_total_relation_size('objects')) AS objects_size,
    pg_size_pretty(pg_total_relation_size('relationships')) AS relationships_size;
```

### 인덱스 재구성

```sql
REINDEX TABLE objects;
REINDEX TABLE relationships;
ANALYZE;
```

### 백업

```bash
# 전체 데이터베이스 백업
pg_dump -U postgres -d dx_platform -F c -f dx_platform_backup.dump

# 압축 백업
pg_dump -U postgres -d dx_platform | gzip > dx_platform_backup.sql.gz
```

### 복구

```bash
# dump 파일 복구
pg_restore -U postgres -d dx_platform dx_platform_backup.dump

# 압축 파일 복구
gunzip -c dx_platform_backup.sql.gz | psql -U postgres -d dx_platform
```

## 📖 참고 문서

- [Phase 3: PostgreSQL Database 상세 문서](../0.PJTprompt/Phase3_PostgreSQL_Database.md)
- [Phase 0: Architecture Overview](../0.PJTprompt/Phase0_Architecture_Overview.md)

## ⚠️ 주의사항

1. **비밀번호 관리**: 기본 비밀번호를 반드시 변경하세요
2. **백업**: 정기적인 백업 스케줄을 설정하세요
3. **모니터링**: 디스크 사용량과 쿼리 성능을 모니터링하세요
4. **트리거**: 운영 환경에서는 불변성 트리거를 활성화하세요

## 🤝 다음 단계

데이터베이스 설정 완료 후:
1. Phase 4: FastAPI 서버 개발
2. Phase 2: DXrevit 데이터 추출 완성
3. 통합 테스트 수행

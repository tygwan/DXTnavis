# SQL 워크플로 가이드

프로젝트의 PostgreSQL 환경에서 자주 사용하는 명령과 체크 리스트를 정리했습니다. 데이터베이스 초기화부터 마이그레이션, 검증 쿼리까지 상황별로 참고하세요.

---

## 1. 기본 정보
- **DB 이름**: `dx_platform`
- **주요 역할**
  - `dx_api_role`: API/인입용 RW 계정
  - `dx_readonly_role`: BI/조회용 RO 계정
- **필수 확장**: `pgvector` (벡터 검색)

## 2. 초기 세팅
```bash
# 데이터베이스 생성 (postgres 슈퍼유저)
createdb dx_platform

# 초기 스크립트 실행
psql -U postgres -d dx_platform -f database/init_database.sql
```

```sql
-- 필수 확장 설치 (init_database.sql 실행 시 포함되지만 수동 실행 시 참고)
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "vector";
```

## 3. 마이그레이션 적용
```bash
# 새 마이그레이션 적용 (예: 004_unified_revision_patch.sql)
psql -U postgres -d dx_platform -f database/migrations/004_unified_revision_patch.sql
```

```bash
# 전체 마이그레이션 순차 적용
for file in database/migrations/*.sql; do
  psql -U postgres -d dx_platform -f "$file"
done
```

> **Tip**: 운영 반영 전에는 스테이징 DB에 먼저 적용하고 `SELECT` 쿼리로 스키마 변경 내역을 확인하세요.

## 4. 스키마 확인 & 점검
```sql
-- 테이블 목록 보기
\dt

-- 특정 테이블 스키마 확인
\d+ unified_objects
\d+ navisworks_hierarchy
\d+ revision_versions

-- 컬럼 존재 여부 체크
SELECT column_name
FROM information_schema.columns
WHERE table_name = 'unified_objects'
  AND column_name IN ('ifc_guid', 'navisworks_object_id');
```

## 5. 공통 조회 쿼리
```sql
-- 최신 프로젝트별 리비전 현황
SELECT p.code, p.name, r.revision_number, r.source_type, r.model_version
FROM projects p
JOIN revisions r ON r.project_id = p.id
ORDER BY p.code, r.revision_number DESC;

-- 모델 버전 ↔ 리비전 매핑
SELECT model_version, revision_id, source_type, extracted_at
FROM revision_versions
ORDER BY extracted_at DESC
LIMIT 20;

-- IFC GUID로 통합 객체 매칭
SELECT project_id, revision_id, object_id, ifc_guid, source_type
FROM unified_objects
WHERE ifc_guid = '04dQjqlczA491jode2Ug8G';

-- Navisworks 계층에서 프로젝트/리비전 연결 확인
SELECT DISTINCT project_id, revision_id, model_version
FROM navisworks_hierarchy
WHERE ifc_guid IS NOT NULL
ORDER BY model_version DESC;
```

## 6. 데이터 검증 체크리스트
- Revit 인입 직후
  - `revision_versions`에 `model_version`이 등록되었는지 확인
  - `unified_objects`에 `source_type='revit'` 레코드가 생성되었는지 확인
- Navisworks 인입 후
  - 동일 리비전(`revision_id`)에 `source_type='navisworks'` 레코드가 추가되었는지 확인
  - `navisworks_hierarchy`에 `project_id`, `revision_id`, `ifc_guid`가 채워졌는지 확인
- Timeliner 준비
  - `v_hierarchy_objects` 뷰에서 `ifc_guid`와 `revision_id`가 함께 나오는지 검증

## 7. 백업 & 복구
```bash
# 전체 백업
pg_dump -U postgres -Fc dx_platform > backup/dx_platform_$(date +%Y%m%d).dump

# 복구 (새 DB에)
createdb dx_platform_restore
pg_restore -U postgres -d dx_platform_restore backup/dx_platform_YYYYMMDD.dump
```

## 8. 자주 하는 실수 & 예방
- `model_version` 없이 Navisworks 데이터만 먼저 적재하면 프로젝트 매칭이 실패합니다. → Revit 스냅샷 인입 후 Navisworks CSV를 업로드하세요.
- `psql` 실행 시 DB/사용자 지정 누락 → `-d`, `-U` 옵션을 습관화하십시오.
- 운영/테스트 DB를 혼동하지 않도록 `.env`에 `DATABASE_URL`을 명확히 구분해 두세요.

## 9. 참고 자료
- `database/README.md`: 테이블별 상세 설명
- `database/migrations/004_unified_revision_patch.sql`: 리비전 통합 마이그레이션
- `docs/BACKEND_REVIT_NAV_UNIFICATION_PLAN.md`: Revit/Navis 연동 전략

---

필요한 명령이 추가되면 이 문서를 계속 업데이트하세요. (예: 자동 배포 스크립트, Timeliner 전용 쿼리 등)*** End Patch

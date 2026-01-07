# Backend Remediation Plan: Revit & Navisworks Unified Revisions

## 1. 문제 요약
- Navisworks 플러그인이 CSV 기반으로 프로젝트를 감지할 때, 백엔드에 동일한 프로젝트/리비전 정보가 존재하지 않아 `404`가 발생한다.[^issue-summary]
- Revit과 Navisworks가 각각 서로 다른 파이프라인(직접 SQL vs CSV 업로드)을 사용하면서 동일한 모델 스냅샷임에도 리비전이 분리되어 저장된다.
- Timeliner 자동화를 위해서는 두 소스가 동일한 리비전/객체 식별자를 공유해야 하는데, 현재 스키마에서는 이를 보장할 수 없다.

## 2. 원인 및 요인 분석
1. **프로젝트 및 리비전 생성 경로 불일치**  
   - Revit 스냅샷은 `metadata`, `objects` 테이블로 직접 적재되지만, `projects`, `revisions` 등 통합 스키마에는 연결되지 않는다.
   - Navisworks는 CSV만 참조하고 DB와 연동하지 않아 신규 프로젝트/리비전을 생성하지 못한다.
2. **동일 리비전을 식별할 키 부재**  
   - `metadata.model_version` 문자열(Revit 기준)과 Navisworks CSV 속성(예: 소스 Revit 파일 경로)이 따로 관리되어 매칭 정보가 없다.
3. **데이터 형태 차이**  
   - Revit: 객체별 레코드 + JSON 속성 (`objects` 테이블)  
   - Navisworks: EAV(Entity-Attribute-Value) 구조 (`navisworks_hierarchy` 테이블)  
   - 공통 식별자(IfcGUID)는 존재하지만 별도 컬럼으로 노출되지 않아 활용이 어렵다.

## 3. CSV 데이터 검토 결과
| 구분 | 주요 필드 | 비고 |
|------|----------|------|
| `revitHierachy.csv` | `object_id`, `element_id`, `properties.IfcGUID` | Revit UniqueId 기반, JSON 속성 내부에 IfcGUID 포함 |
| `navisHierarchy_20251021_142245.csv` | `ObjectId`, `ParentId`, `PropertyName/Value` | `PropertyName='IfcGUID'` 존재, `PropertyName='GUID'`는 Navisworks Instance GUID |

→ IfcGUID를 기반으로 두 소스 간 동일 객체를 매칭 가능. 두 파일 모두 유지하여 **Revit=정규 모델 스냅샷**, **Navisworks=Timeliner용 계층+속성 보강** 역할을 부여하는 것이 타당.

## 4. SQL 통합 설계

### 4.1 핵심 설계 원칙
1. **단일 리비전 레코드**: Revit과 Navisworks 스냅샷이 동일한 `revisions.id`를 바라보도록 강제한다.
2. **IfcGUID 등 공통 식별자 승격**: `unified_objects` 및 Navisworks 계층 테이블에 명시적 컬럼을 추가해 조인 및 인덱싱을 용이하게 한다.
3. **모델 버전 매핑 테이블**: `metadata.model_version`과 실제 `revisions`를 연결하는 교차 테이블을 도입하여 소스별 버전을 통제한다.

### 4.2 제안 DDL (요약)
```sql
-- 1) revisions 테이블에 model_version 컬럼 추가
ALTER TABLE revisions
    ADD COLUMN model_version VARCHAR(255),
    ADD CONSTRAINT uq_revisions_model_version UNIQUE (model_version);

-- 2) unified_objects에 ifc_guid 및 navisworks_object_id 추가
ALTER TABLE unified_objects
    ADD COLUMN ifc_guid VARCHAR(64),
    ADD COLUMN navisworks_object_id UUID,
    ADD CONSTRAINT uq_revision_ifc_guid UNIQUE (revision_id, ifc_guid),
    ADD CONSTRAINT fk_unified_nav_object
        FOREIGN KEY (navisworks_object_id)
        REFERENCES navisworks_hierarchy(object_id)
        DEFERRABLE INITIALLY DEFERRED;

CREATE INDEX IF NOT EXISTS idx_unified_ifc_guid ON unified_objects(ifc_guid);

-- 3) navisworks_hierarchy에 project/revision 외래키 및 ifc_guid 노출
ALTER TABLE navisworks_hierarchy
    ADD COLUMN project_id UUID REFERENCES projects(id) ON DELETE CASCADE,
    ADD COLUMN revision_id UUID REFERENCES revisions(id) ON DELETE CASCADE,
    ADD COLUMN ifc_guid VARCHAR(64),
    ADD COLUMN source_file_path TEXT;

CREATE INDEX IF NOT EXISTS idx_hierarchy_revision ON navisworks_hierarchy(revision_id);
CREATE INDEX IF NOT EXISTS idx_hierarchy_ifc_guid ON navisworks_hierarchy(ifc_guid);

-- 4) 모델 버전-리비전 매핑 테이블
CREATE TABLE IF NOT EXISTS revision_versions (
    model_version VARCHAR(255) PRIMARY KEY,
    revision_id UUID NOT NULL REFERENCES revisions(id) ON DELETE CASCADE,
    source_type VARCHAR(20) NOT NULL CHECK (source_type IN ('revit', 'navisworks')),
    extracted_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    source_file_path TEXT
);
```

### 4.3 데이터 동기화 규칙
- Revit ingest 시:
  1. 프로젝트 코드/이름으로 `projects` 조회, 없으면 생성.
  2. `revisions`에 `source_type='revit'`, `model_version`, `revision_number`(자동 증가) 저장.
  3. `revision_versions`에 Revit `model_version` 등록.
  4. `unified_objects`에 `ifc_guid`, `element_id`, `properties` 저장.
- Navisworks ingest 시:
  1. CSV 내 `PropertyName='Source File Name'`/`'DisplayString'` 등을 파싱해 Revit 파일명 확보.
  2. `projects`에서 해당 파일명과 일치하는 레코드 조회 (없으면 사용자 승인 후 생성).
  3. `revision_versions`에서 동일 `model_version` 혹은 `source_file_path`로 리비전 매칭. 실패 시 새 리비전 생성(`source_type='navisworks'`) 후 `parent_revision_id`를 Revit 리비전으로 설정.
  4. `navisworks_hierarchy`에 `project_id`, `revision_id`, `ifc_guid` 저장.
  5. `v_hierarchy_objects` 뷰를 활용해 `unified_objects`를 upsert (`source_type='navisworks'`, 동일 `revision_id`).

## 5. Timeliner 연계 전략
Navisworks 힌트 문서를 반영해 다음 2단계 접근을 제안한다.
1. **Selection Set 기반 Task 생성 (Part-1)**  
   - `v_hierarchy_objects`에서 `level`/`parent_id`를 이용해 공간/구조 그룹을 구성하고, IfcGUID를 통해 Revit 객체와 매핑된 Selection Set을 자동 생성.
   - `activities` 테이블에서 `activity_id`를 가져와 Selection Set과 매핑, Timeliner Task 생성.
2. **CSV 스케줄 데이터 자동 동기화 (Part-2)**  
   - `revision_versions`로 관리되는 동일 리비전에 대해 외부 일정 CSV(예: `planning.csv`)를 파싱해 `timeliner_tasks`(신규) 테이블에 저장.
   - Navisworks DataSourceProvider 구현 시, 위 테이블을 참조하여 Task를 구성하고 Selection Set을 첨부.

## 6. 백엔드 구현 로드맵
| 우선순위 | 작업 | 세부 내용 |
|----------|------|-----------|
| High | 스키마 확장 | 위 DDL 적용, 마이그레이션 파일 작성 |
| High | Revit ingest 리팩터링 | `/api/v1/ingest` 흐름을 `projects`→`revisions`→`unified_objects` 순으로 정리 |
| High | Navisworks ingest API | `POST /api/v1/navisworks/hierarchy` (예시) 추가, CSV 업로드 후 DB upsert |
| High | 프로젝트 자동 감지 API 개선 | `POST /api/v1/projects/detect-by-objects` 구현 (문서 내 샘플 코드 기반) |
| Medium | Selection Set 매핑 API | Timeliner 용 Selection Set/Task 준비용 엔드포인트 |
| Medium | 데이터 검증 배치 | Revit vs Navisworks IfcGUID 불일치 리포트 생성 |
| Low | 자동 리비전 생성 선택지 | Navisworks에서 신규 프로젝트 감지 시 사용자 승인 흐름 |

## 7. 테스트 및 검증
1. **동일 리비전 보장 테스트**  
   - Revit ingest 후 `revision_versions`와 `unified_objects` 확인.  
   - Navisworks ingest 후 동일 `revision_id`에 Navisworks 객체가 upsert 되었는지 확인.
2. **IfcGUID 매칭 검증**  
   - 두 CSV에서 추출한 IfcGUID 100개 샘플을 비교하여 일치율 측정 (자동화 스크립트 작성).
3. **Timeliner 시뮬레이션**  
   - Selection Set → Task 생성 루틴을 Sandbox에서 실행, Navisworks API 응답 확인.

## 8. 결론 및 활용 방안
- **두 CSV 모두 활용**: Revit 스냅샷은 요소별 정규 속성, Navisworks CSV는 공간 계층 및 Timeliner 특화 속성을 제공하므로 상호 보완적이다. IfcGUID 매칭과 통합 리비전 스키마로 두 데이터를 결합하면 Timeliner 자동화, 변경 추적, 4D 시뮬레이션 데이터 정합성이 확보된다.
- **단계별 도입**: 우선 DB/ingest를 정비해 장애를 제거하고, 이후 Selection Set-Task 자동화 및 일정 연동까지 확장한다.

---

[^issue-summary]: docs/ISSUE_ANALYSIS_FOR_BACKEND.md

# DX_platform 데이터베이스 분석 보고서

**분석 일시**: 2025-10-18
**데이터베이스**: DX_platform (PostgreSQL)
**분석 도구**: [query_database.py](query_database.py), [analyze_database.py](analyze_database.py)

---

## 📊 데이터베이스 개요

### 테이블 구조

| 테이블명 | 크기 | 레코드 수 | 용도 |
|---------|------|----------|------|
| **objects** | 41 MB | 14,562개 | Revit 객체 정보 저장 |
| **metadata** | 80 KB | 2개 | 모델 버전 메타데이터 |
| **relationships** | 168 KB | 70개 | 객체 간 관계 정보 |

---

## 🏗️ 모델 버전 정보

### 현재 저장된 모델 버전

| 프로젝트명 | 모델 버전 | 객체 수 | 카테고리 수 | 생성일시 |
|----------|---------|--------|-----------|---------|
| **Snowdon Towers** | Snowdon Towers_20251017_170052 | 13,710개 | 54개 | 2025-10-17 17:00:52 |
| **프로젝트 이름** | 프로젝트 이름_20251016_030006 | 852개 | 38개 | 2025-10-16 03:00:07 |

**총 객체 수**: 14,562개

---

## 📁 카테고리별 객체 분포 (상위 20개)

| 순위 | 카테고리 | 객체 수 | 패밀리 수 | 타입 수 | 비율 |
|-----|---------|--------|---------|--------|------|
| 1 | 중심선 | 6,043 | 1 | 1 | 41.5% |
| 2 | 배관 | 3,051 | 3 | 3 | 21.0% |
| 3 | 배관 부속류 | 2,660 | 14 | 3 | 18.3% |
| 4 | 전기 부하 분류 매개변수 요소 | 366 | 363 | 1 | 2.5% |
| 5 | 공간 유형 설정 | 250 | 250 | 1 | 1.7% |
| 6 | 범례 구성요소 | 248 | 247 | 1 | 1.7% |
| 7 | 재료 | 195 | 180 | 1 | 1.3% |
| 8 | 덕트 | 181 | 1 | 1 | 1.2% |
| 9 | 위생기구 | 179 | 9 | 17 | 1.2% |
| 10 | 재질 에셋 | 155 | 135 | 1 | 1.1% |
| 11 | 덕트 부속 | 150 | 1 | 2 | 1.0% |
| 12 | 뷰 | 130 | 99 | 14 | 0.9% |
| 13 | 배관 밸브류 | 126 | 4 | 2 | 0.9% |
| 14 | HVAC 부하 일람표 | 100 | 50 | 1 | 0.7% |
| 15 | 작업 기준면 그리드 | 75 | 14 | 1 | 0.5% |
| 16 | 건물 유형 설정 | 66 | 66 | 1 | 0.5% |
| 17 | 전기 부하 분류 | 61 | 59 | 1 | 0.4% |
| 18 | 전기 수용률 정의 | 59 | 57 | 1 | 0.4% |
| 19 | 배관 시스템 | 46 | 46 | 5 | 0.3% |
| 20 | 공간 | 43 | 43 | 1 | 0.3% |

**주요 인사이트**:
- **MEP 중심**: 배관(21%), 배관 부속류(18.3%), 덕트(1.2%) 등 MEP 요소가 전체의 약 40% 차지
- **중심선 비율 높음**: 41.5%로 가장 높은 비율 (Snowdon Towers 프로젝트 특성)
- **다양한 카테고리**: 54개의 서로 다른 카테고리 존재

---

## 🔗 객체 관계 분석

### 관계 유형별 통계

| 관계 유형 | 관계 수 | 고유 소스 객체 | 고유 타겟 객체 |
|---------|--------|-------------|-------------|
| **HostedBy** | 70개 | 2개 | 70개 |

**분석**:
- 현재는 `HostedBy` 관계만 기록됨
- 2개의 호스트 객체가 70개의 다른 객체를 호스팅
- 추가 관계 유형 확장 가능 (Contains, ConnectsTo, DependsOn 등)

---

## 📦 데이터 품질 분석

### NULL 값 통계

| 필드 | NULL 개수 | 비율 | 상태 |
|-----|----------|------|------|
| **family** | 0 | 0.0% | ✅ 양호 |
| **type** | 0 | 0.0% | ✅ 양호 |
| **activity_id** | 14,562 | 100.0% | ⚠️ 미할당 |
| **properties** | 0 | 0.0% | ✅ 양호 |
| **bounding_box** | 1,927 | 13.2% | ✅ 양호 |

### 데이터 무결성

| 검사 항목 | 결과 | 상태 |
|---------|------|------|
| **Object ID 중복** | 0개 | ✅ 중복 없음 |
| **Properties JSONB** | 100% 보유 | ✅ 모든 객체가 속성 데이터 보유 |
| **Bounding Box** | 86.8% 보유 | ✅ 대부분의 객체가 공간 정보 보유 |

---

## 🎯 주요 발견사항

### 1. 활동 ID 미할당
- **현황**: 전체 14,562개 객체 중 활동 ID가 할당된 객체 없음 (100% NULL)
- **영향**: 4D 시뮬레이션 및 스케줄 연계 불가
- **권장사항**:
  - CSV 스케줄 데이터와 매핑하여 활동 ID 할당 필요
  - SyncID 또는 Element ID 기반 자동 매칭 로직 개발

### 2. Bounding Box 데이터
- **보유율**: 86.8% (12,635개 / 14,562개)
- **미보유 객체**: 1,927개 (주로 재료, 뷰, 시스템 설정 등)
- **샘플 구조**:
  ```json
  {
    "MinX": -100, "MaxX": 100,
    "MinY": -100, "MaxY": 100,
    "MinZ": -100, "MaxZ": 100
  }
  ```
- **활용 가능성**: 3D 시각화, 공간 분석, 충돌 감지

### 3. Properties JSONB 데이터
- **보유율**: 100% (모든 객체가 속성 데이터 보유)
- **평균 속성 수**: 약 15-20개 (샘플 분석 기준)
- **주요 속성 예시**:
  - URL, 빛, 단가, 마크, 모델
  - Revit 매개변수 값들
- **활용 가능성**:
  - 고급 필터링 및 검색
  - 비용 분석 (단가 정보 활용)
  - 사양 관리

### 4. 관계 데이터 부족
- **현재**: HostedBy 관계만 70개 기록
- **부족한 관계 유형**:
  - Contains (포함 관계)
  - ConnectsTo (연결 관계 - 배관, 덕트)
  - DependsOn (의존 관계)
  - Supports (지지 관계)
- **권장사항**: Revit API에서 추가 관계 유형 추출 필요

---

## 🔧 객체 속성 샘플

### 재료 카테고리 객체 (기본값)
```json
{
  "Object ID": "e3e052f9-0156-11d5-9301-0000863f27ad-00000017",
  "Category": "재료",
  "Family": "기본값",
  "Type": "Unknown",
  "Properties": {
    "URL": "",
    "빛": 0,
    "단가": 0,
    "마크": "",
    "모델": "",
    "... 총 17개 속성 ..."
  }
}
```

---

## 📈 데이터베이스 성능

### 테이블 크기 및 성능 지표

| 지표 | 값 | 평가 |
|-----|-----|------|
| **총 데이터베이스 크기** | ~42 MB | ✅ 경량 |
| **최대 테이블 크기** | 41 MB (objects) | ✅ 적정 |
| **평균 레코드 크기** | ~3 KB | ✅ 효율적 |
| **쿼리 응답 시간** | < 100ms | ✅ 빠름 |

---

## 💡 개선 권장사항

### 1. 스키마 최적화

#### 인덱스 추가 권장
```sql
-- 자주 조회되는 필드에 인덱스 추가
CREATE INDEX idx_objects_category ON objects(category);
CREATE INDEX idx_objects_element_id ON objects(element_id);
CREATE INDEX idx_objects_model_version ON objects(model_version);
CREATE INDEX idx_objects_activity_id ON objects(activity_id) WHERE activity_id IS NOT NULL;

-- JSONB 속성 검색을 위한 GIN 인덱스
CREATE INDEX idx_objects_properties ON objects USING GIN(properties);
CREATE INDEX idx_objects_bounding_box ON objects USING GIN(bounding_box);
```

#### 파티셔닝 고려 (대용량 데이터 대비)
```sql
-- model_version 기준 파티셔닝
CREATE TABLE objects_partitioned (
    LIKE objects INCLUDING ALL
) PARTITION BY LIST (model_version);
```

### 2. 데이터 보강

#### Activity ID 할당 프로세스
1. CSV 스케줄 데이터 로드
2. SyncID 또는 Element ID 기반 매칭
3. Activity ID 업데이트
4. 검증 및 로그 기록

#### 관계 데이터 확장
- Revit API에서 추가 관계 유형 추출
- 배관/덕트 연결 정보 수집
- 구조적 지지 관계 기록

### 3. 데이터 검증 자동화

#### 정기 품질 검사 스크립트
```python
# 일일 실행 권장
- NULL 값 모니터링
- 중복 데이터 감지
- 참조 무결성 검증
- 속성 값 범위 검증
```

### 4. 백업 및 복구

#### 백업 전략
```bash
# 일일 백업
pg_dump -h localhost -U postgres -d DX_platform > backup_$(date +%Y%m%d).sql

# 테이블별 백업 (objects 테이블 우선)
pg_dump -h localhost -U postgres -d DX_platform -t objects > objects_backup.sql
```

---

## 📊 활용 시나리오

### 1. 4D 시뮬레이션 준비
```sql
-- Activity ID 할당 후 시뮬레이션 가능 객체 조회
SELECT
    category,
    COUNT(*) as ready_objects
FROM objects
WHERE activity_id IS NOT NULL
GROUP BY category
ORDER BY ready_objects DESC;
```

### 2. 비용 분석
```sql
-- Properties에서 단가 정보 활용
SELECT
    category,
    COUNT(*) as count,
    AVG((properties->>'단가')::numeric) as avg_unit_price
FROM objects
WHERE properties->>'단가' IS NOT NULL
    AND (properties->>'단가')::numeric > 0
GROUP BY category
ORDER BY avg_unit_price DESC;
```

### 3. 공간 분석
```sql
-- Bounding Box 데이터로 객체 크기 분석
SELECT
    category,
    AVG(
        (bounding_box->>'MaxX')::numeric - (bounding_box->>'MinX')::numeric
    ) as avg_width,
    AVG(
        (bounding_box->>'MaxY')::numeric - (bounding_box->>'MinY')::numeric
    ) as avg_depth,
    AVG(
        (bounding_box->>'MaxZ')::numeric - (bounding_box->>'MinZ')::numeric
    ) as avg_height
FROM objects
WHERE bounding_box IS NOT NULL
GROUP BY category
ORDER BY avg_width DESC;
```

### 4. 관계 네트워크 분석
```sql
-- 호스팅 관계 분석
SELECT
    o1.category as host_category,
    o2.category as hosted_category,
    COUNT(*) as relationship_count
FROM relationships r
JOIN objects o1 ON r.target_object_id = o1.object_id
JOIN objects o2 ON r.source_object_id = o2.object_id
WHERE r.relation_type = 'HostedBy'
GROUP BY o1.category, o2.category
ORDER BY relationship_count DESC;
```

---

## 🎯 다음 단계

### 즉시 실행 가능
- [x] 데이터베이스 조회 스크립트 작성 완료
- [x] 상세 분석 스크립트 작성 완료
- [ ] 인덱스 추가로 쿼리 성능 최적화
- [ ] 백업 스크립트 자동화 설정

### 단기 목표 (1-2주)
- [ ] Activity ID 할당 로직 개발
- [ ] CSV 스케줄 데이터 연계
- [ ] 관계 데이터 추출 확장
- [ ] 데이터 검증 자동화

### 중기 목표 (1개월)
- [ ] 4D 시뮬레이션 파이프라인 구축
- [ ] FastAPI 엔드포인트 고도화
- [ ] 실시간 데이터 동기화
- [ ] 대시보드 개발

---

## 📚 관련 문서

- [데이터베이스 연결 가이드](DB_CONNECTION_GUIDE.md)
- [CSV 임포트 가이드](README_IMPORT.md)
- [PostgreSQL MCP 사용 가이드](MCP_POSTGRES_GUIDE.md)
- [FastAPI 설정](../fastapi_server/config.py)

---

**보고서 생성**: 2025-10-18
**분석 도구**: Python 3.10, asyncpg, PostgreSQL
**작성자**: Database Analysis Script

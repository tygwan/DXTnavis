# Navisworks vs Revit 데이터 구조 비교 분석

**작성일**: 2025-10-18
**목적**: 두 플랫폼의 데이터 구조 차이 이해 및 통합 전략 수립

---

## 🏗️ 핵심 차이점 요약

| 특성 | Navisworks | Revit |
|------|-----------|-------|
| **데이터 구조** | **계층적 트리 구조** (Tree) | **평면적 Element 컬렉션** (Flat) |
| **부모-자식 관계** | ✅ 명시적 (parent_id, level) | ❌ 암시적 (Host 관계만) |
| **계층 탐색** | 재귀적 트리 순회 | 필터 기반 검색 |
| **ID 체계** | InstanceGuid (객체별 고유) | UniqueId + Element ID |
| **속성 저장** | 카테고리 > 속성 > 값 | Parameter 컬렉션 |
| **관계 표현** | 계층 구조 내장 | 별도 Relationship 추출 필요 |

---

## 📂 Navisworks 데이터 구조 (계층적)

### 핵심 특징: 트리 기반 계층 구조

Navisworks는 **건물을 계층적 트리로 표현**합니다:

```
프로젝트 (Level 0)
├─ 건물 A (Level 1)
│  ├─ 1층 (Level 2)
│  │  ├─ 벽 001 (Level 3)
│  │  │  ├─ 속성: 높이=3m (Level 4)
│  │  │  └─ 속성: 재료=콘크리트 (Level 4)
│  │  └─ 기둥 001 (Level 3)
│  └─ 2층 (Level 2)
└─ 건물 B (Level 1)
```

### 데이터 모델: HierarchicalPropertyRecord

```csharp
public class HierarchicalPropertyRecord
{
    public Guid ObjectId { get; set; }      // 현재 객체 ID
    public Guid ParentId { get; set; }      // 부모 객체 ID (계층 구조 핵심!)
    public int Level { get; set; }          // 계층 깊이 (0부터 시작)
    public string DisplayName { get; set; } // 표시 이름
    public string Category { get; set; }    // 속성 카테고리
    public string PropertyName { get; set; }
    public string PropertyValue { get; set; }
}
```

### 추출 방식: 재귀적 트리 순회

```csharp
// DXnavis/Services/NavisworksDataExtractor.cs
public void TraverseAndExtractProperties(
    ModelItem currentItem,
    Guid parentId,        // ⭐ 부모 ID를 전달받음
    int level,            // ⭐ 현재 계층 레벨
    List<HierarchicalPropertyRecord> results)
{
    Guid currentId = currentItem.InstanceGuid;
    string displayName = GetDisplayName(currentItem);

    // 현재 객체의 모든 속성 추출
    foreach (var category in currentItem.PropertyCategories)
    {
        foreach (DataProperty property in category.Properties)
        {
            results.Add(new HierarchicalPropertyRecord(
                objectId: currentId,
                parentId: parentId,     // ⭐ 부모 ID 저장
                level: level,           // ⭐ 계층 레벨 저장
                displayName: displayName,
                category: categoryName,
                propertyName: propertyName,
                propertyValue: propertyValue
            ));
        }
    }

    // ⭐ 재귀 호출: 모든 자식에 대해 반복
    foreach (ModelItem child in currentItem.Children)
    {
        TraverseAndExtractProperties(child, currentId, level + 1, results);
    }
}
```

### 데이터베이스 스키마 설계

```sql
-- database/tables/navisworks_hierarchy.sql
CREATE TABLE navisworks_hierarchy (
    id BIGSERIAL PRIMARY KEY,

    -- ⭐ 계층구조 정보 (핵심!)
    object_id UUID NOT NULL,      -- 현재 객체
    parent_id UUID NOT NULL,      -- 부모 객체
    level INTEGER NOT NULL,       -- 계층 깊이
    display_name VARCHAR(500),

    -- 속성 정보 (EAV 패턴)
    category VARCHAR(255),
    property_name VARCHAR(255),
    property_value TEXT,

    model_version VARCHAR(255),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
);

-- 계층 경로 조회 함수 (재귀 CTE)
CREATE FUNCTION fn_get_hierarchy_path(target_object_id UUID)
RETURNS TABLE (...) AS $$
WITH RECURSIVE hierarchy_path AS (
    -- 시작: 대상 객체
    SELECT object_id, parent_id, level, display_name
    FROM navisworks_hierarchy
    WHERE object_id = target_object_id

    UNION ALL

    -- 재귀: 부모로 올라가기
    SELECT h.object_id, h.parent_id, h.level, h.display_name
    FROM navisworks_hierarchy h
    INNER JOIN hierarchy_path hp ON h.object_id = hp.parent_id
)
SELECT * FROM hierarchy_path ORDER BY level;
$$;
```

### 장점

✅ **자연스러운 건물 표현**: 실제 건물의 계층 구조 반영
✅ **쉬운 탐색**: 부모-자식 관계가 명확
✅ **경로 추적**: 루트부터 현재 객체까지의 경로 쉽게 추출
✅ **그룹핑**: 층별, 건물별 자동 그룹화

### 단점

⚠️ **복잡한 쿼리**: 재귀 CTE 필요
⚠️ **중복 데이터**: 같은 객체가 여러 속성으로 여러 행
⚠️ **성능 이슈**: 깊은 계층의 경우 조인 비용 증가

---

## 🏢 Revit 데이터 구조 (평면적)

### 핵심 특징: Element 컬렉션 기반

Revit은 **모든 객체를 동일 레벨의 Element로 관리**합니다:

```
Element Collection (모두 Level 0)
- Wall (id=001, category=벽, element_id=23)
- Column (id=002, category=기둥, element_id=45)
- Door (id=003, category=문, element_id=67, host=001)  ← Host 관계
- Window (id=004, category=창, element_id=89, host=001)
```

### 데이터 모델: ObjectRecord

```csharp
public class ObjectRecord
{
    public string ObjectId { get; set; }       // 고유 ID (생성)
    public int ElementId { get; set; }         // Revit Element ID
    public string Category { get; set; }       // 카테고리 (벽, 문, 창 등)
    public string Family { get; set; }         // 패밀리
    public string Type { get; set; }           // 타입
    public string ActivityId { get; set; }     // 공정 ID (스케줄 연계)
    public string Properties { get; set; }     // JSON: 모든 Parameter
    public string BoundingBox { get; set; }    // JSON: 공간 좌표

    // ❌ 계층 정보 없음 (parent_id, level 없음)
}
```

### 추출 방식: 필터 기반 순회

```csharp
// DXrevit/Services/DataExtractor.cs
public ExtractedData ExtractAll(...)
{
    // ⭐ FilteredElementCollector: 모든 Element를 평면적으로 수집
    var collector = new FilteredElementCollector(_document)
        .WhereElementIsNotElementType()
        .WhereElementIsViewIndependent();

    foreach (Element element in collector)
    {
        // 객체 데이터 추출 (계층 정보 없음)
        var objectRecord = new ObjectRecord
        {
            ObjectId = GenerateObjectId(...),
            ElementId = element.Id.Value,
            Category = element.Category.Name,
            Family = GetFamilyName(element),
            Type = GetTypeName(element),
            Properties = ExtractProperties(element)  // JSON 직렬화
        };

        // ⭐ 관계는 별도 추출 (Host 관계만)
        if (element is FamilyInstance familyInstance && familyInstance.Host != null)
        {
            relationships.Add(new RelationshipRecord
            {
                SourceObjectId = familyInstance.Host.ObjectId,
                TargetObjectId = element.ObjectId,
                RelationType = "HostedBy"
            });
        }
    }
}
```

### 데이터베이스 스키마 설계

```sql
-- 현재 objects 테이블
CREATE TABLE objects (
    id BIGSERIAL PRIMARY KEY,
    model_version VARCHAR(255) NOT NULL,
    object_id VARCHAR(255) NOT NULL,
    element_id INTEGER NOT NULL,
    category VARCHAR(255) NOT NULL,
    family VARCHAR(255),
    type VARCHAR(255),
    activity_id VARCHAR(100),      -- ⭐ 4D 시뮬레이션용
    properties JSONB,               -- ⭐ 모든 Parameter
    bounding_box JSONB,             -- ⭐ 공간 좌표
    created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()

    -- ❌ parent_id, level 컬럼 없음
);

-- 관계는 별도 테이블
CREATE TABLE relationships (
    id BIGSERIAL PRIMARY KEY,
    source_object_id VARCHAR(255),  -- 호스트 객체
    target_object_id VARCHAR(255),  -- 호스팅되는 객체
    relation_type VARCHAR(50),      -- 'HostedBy'
    is_directed BOOLEAN DEFAULT true
);
```

### 장점

✅ **단순한 구조**: 조인 없이 직접 조회 가능
✅ **빠른 검색**: 인덱스 기반 필터링
✅ **유연한 속성**: JSONB로 모든 Parameter 저장
✅ **확장성**: 새로운 관계 유형 쉽게 추가

### 단점

⚠️ **계층 정보 부족**: 층별, 건물별 구조 파악 어려움
⚠️ **복잡한 탐색**: 특정 층의 모든 객체 찾기 복잡
⚠️ **관계 제한**: HostedBy 외 다른 관계 미흡

---

## 🔄 통합 전략 및 개선 방안

### 문제점 분석

| 문제 | 설명 |
|------|------|
| **계층 정보 손실** | Revit 데이터에는 부모-자식, 레벨 정보 없음 |
| **불일치 스키마** | Navisworks용 테이블과 Revit용 테이블이 다름 |
| **4D 연계 어려움** | Activity ID는 있지만 계층 기반 그룹핑 불가 |
| **쿼리 복잡도** | 두 데이터 소스의 쿼리 방식이 완전히 다름 |

### 해결 방안 1: Revit 데이터에 계층 정보 추가

#### 1-1. objects 테이블 스키마 확장

```sql
-- objects 테이블에 계층 정보 추가
ALTER TABLE objects
ADD COLUMN parent_object_id VARCHAR(255),  -- 부모 객체 ID (Host 또는 Level)
ADD COLUMN level INTEGER DEFAULT 0,        -- 계층 레벨
ADD COLUMN spatial_structure VARCHAR(100); -- 공간 구조 (Building > Level > Room)

-- 인덱스 추가
CREATE INDEX idx_objects_parent ON objects(parent_object_id);
CREATE INDEX idx_objects_level ON objects(level);
CREATE INDEX idx_objects_spatial ON objects(spatial_structure);
```

#### 1-2. Revit 추출 로직 개선

```csharp
// DXrevit/Services/DataExtractor.cs 개선안
private ObjectRecord ExtractObjectData(Element element, string modelVersion)
{
    // 부모 객체 ID 결정
    string parentObjectId = DetermineParentObject(element);

    // 계층 레벨 계산
    int level = CalculateHierarchyLevel(element);

    // 공간 구조 경로 생성
    string spatialStructure = BuildSpatialPath(element);
    // 예: "Building_A > Level_1 > Room_101"

    return new ObjectRecord
    {
        // 기존 필드
        ObjectId = objectId,
        ElementId = element.Id.Value,
        Category = element.Category.Name,

        // ⭐ 새로운 계층 정보 필드
        ParentObjectId = parentObjectId,
        Level = level,
        SpatialStructure = spatialStructure,

        // 나머지 필드...
    };
}

private string DetermineParentObject(Element element)
{
    // 1. Host 관계 확인
    if (element is FamilyInstance fi && fi.Host != null)
        return fi.Host.UniqueId;

    // 2. Level 관계 확인
    if (element.LevelId != ElementId.InvalidElementId)
    {
        Level level = _document.GetElement(element.LevelId) as Level;
        if (level != null)
            return level.UniqueId;
    }

    // 3. Room 관계 확인
    Room room = GetElementRoom(element);
    if (room != null)
        return room.UniqueId;

    // 4. 최상위 (Building)
    return Guid.Empty.ToString();
}

private int CalculateHierarchyLevel(Element element)
{
    int level = 0;
    string parentId = DetermineParentObject(element);

    while (parentId != Guid.Empty.ToString())
    {
        level++;
        Element parent = FindElementById(parentId);
        if (parent == null) break;
        parentId = DetermineParentObject(parent);
    }

    return level;
}

private string BuildSpatialPath(Element element)
{
    var path = new List<string>();
    Element current = element;

    while (current != null)
    {
        // Level 정보 추가
        if (current is Level)
            path.Insert(0, $"Level_{current.Name}");

        // Room 정보 추가
        Room room = GetElementRoom(current);
        if (room != null)
            path.Insert(0, $"Room_{room.Number}");

        // 다음 부모로
        string parentId = DetermineParentObject(current);
        if (parentId == Guid.Empty.ToString()) break;
        current = FindElementById(parentId);
    }

    // Building 정보 추가 (최상위)
    path.Insert(0, $"Building_{_document.ProjectInformation.BuildingName ?? "Main"}");

    return string.Join(" > ", path);
}
```

### 해결 방안 2: 통합 뷰 생성

```sql
-- Navisworks와 Revit 데이터를 통합하는 뷰
CREATE OR REPLACE VIEW v_unified_hierarchy AS
-- Navisworks 데이터 (계층 구조 유지)
SELECT
    'navisworks' AS source,
    object_id,
    parent_id,
    level,
    display_name AS name,
    category,
    property_name,
    property_value,
    NULL AS activity_id,
    model_version
FROM navisworks_hierarchy

UNION ALL

-- Revit 데이터 (평면 구조를 계층으로 변환)
SELECT
    'revit' AS source,
    object_id,
    parent_object_id AS parent_id,
    level,
    family || ' - ' || type AS name,
    category,
    jsonb_each_text.key AS property_name,
    jsonb_each_text.value AS property_value,
    activity_id,
    model_version
FROM objects,
LATERAL jsonb_each_text(properties) AS jsonb_each_text;
```

### 해결 방안 3: 계층 경로 함수 통합

```sql
-- 통합 계층 경로 조회 함수
CREATE OR REPLACE FUNCTION fn_get_unified_hierarchy_path(
    target_object_id VARCHAR,
    source_type VARCHAR  -- 'navisworks' or 'revit'
)
RETURNS TABLE (
    object_id VARCHAR,
    parent_id VARCHAR,
    level INTEGER,
    display_name VARCHAR,
    full_path TEXT
) AS $$
BEGIN
    IF source_type = 'navisworks' THEN
        -- Navisworks 계층 경로
        RETURN QUERY
        WITH RECURSIVE path AS (
            SELECT h.object_id::VARCHAR, h.parent_id::VARCHAR, h.level, h.display_name
            FROM navisworks_hierarchy h
            WHERE h.object_id::VARCHAR = target_object_id

            UNION ALL

            SELECT h.object_id::VARCHAR, h.parent_id::VARCHAR, h.level, h.display_name
            FROM navisworks_hierarchy h
            INNER JOIN path p ON h.object_id::VARCHAR = p.parent_id
        )
        SELECT
            p.object_id,
            p.parent_id,
            p.level,
            p.display_name,
            string_agg(p.display_name, ' > ' ORDER BY p.level) OVER () AS full_path
        FROM path p
        ORDER BY p.level;

    ELSIF source_type = 'revit' THEN
        -- Revit 계층 경로 (parent_object_id 사용)
        RETURN QUERY
        WITH RECURSIVE path AS (
            SELECT o.object_id, o.parent_object_id, o.level,
                   o.family || ' - ' || o.type AS display_name
            FROM objects o
            WHERE o.object_id = target_object_id

            UNION ALL

            SELECT o.object_id, o.parent_object_id, o.level,
                   o.family || ' - ' || o.type AS display_name
            FROM objects o
            INNER JOIN path p ON o.object_id = p.parent_object_id
        )
        SELECT
            p.object_id,
            p.parent_object_id AS parent_id,
            p.level,
            p.display_name,
            string_agg(p.display_name, ' > ' ORDER BY p.level) OVER () AS full_path
        FROM path p
        ORDER BY p.level;
    END IF;
END;
$$ LANGUAGE plpgsql;
```

---

## 📊 권장 통합 아키텍처

### Phase 1: 단기 (즉시 구현 가능)

```yaml
현재_상태_유지:
  - navisworks_hierarchy: 계층 구조 데이터 (변경 없음)
  - objects: Revit 평면 데이터 (변경 없음)

통합_뷰_생성:
  - v_unified_hierarchy: 두 소스 통합
  - v_spatial_structure: 공간 구조 뷰
  - fn_get_unified_hierarchy_path: 통합 경로 함수
```

### Phase 2: 중기 (1-2주 개발)

```yaml
Revit_스키마_확장:
  objects_테이블_수정:
    - ADD: parent_object_id
    - ADD: level
    - ADD: spatial_structure
    - ADD: building_name
    - ADD: level_name
    - ADD: room_number

  추출_로직_개선:
    - DetermineParentObject() 구현
    - CalculateHierarchyLevel() 구현
    - BuildSpatialPath() 구현
```

### Phase 3: 장기 (1개월 개발)

```yaml
완전_통합_스키마:
  unified_objects_테이블:
    - source: 'navisworks' | 'revit'
    - object_id: UUID
    - parent_id: UUID
    - level: INTEGER
    - name: VARCHAR
    - category: VARCHAR
    - properties: JSONB (통합)
    - spatial_path: VARCHAR
    - activity_id: VARCHAR
    - model_version: VARCHAR

  마이그레이션:
    - Navisworks 데이터 변환
    - Revit 데이터 변환
    - 기존 테이블 백업
```

---

## 🎯 실행 계획

### 즉시 실행

```sql
-- 1. 통합 뷰 생성
\i create_unified_views.sql

-- 2. 테스트 쿼리
SELECT * FROM v_unified_hierarchy
WHERE object_id = 'TARGET_ID'
ORDER BY level;

-- 3. 경로 조회 테스트
SELECT * FROM fn_get_unified_hierarchy_path(
    'TARGET_ID',
    'revit'
);
```

### 다음 스프린트

```bash
# 1. Revit 추출 로직 개선
# - DataExtractor.cs 수정
# - parent_object_id, level, spatial_structure 추가

# 2. 데이터베이스 마이그레이션
ALTER TABLE objects ADD COLUMN parent_object_id VARCHAR(255);
ALTER TABLE objects ADD COLUMN level INTEGER DEFAULT 0;
ALTER TABLE objects ADD COLUMN spatial_structure VARCHAR(100);

# 3. 재추출 및 검증
# - 기존 데이터 백업
# - 새 스키마로 재추출
# - 통합 뷰로 검증
```

---

## 📝 결론

### 핵심 인사이트

1. **Navisworks = 트리**, **Revit = 평면**: 근본적으로 다른 데이터 모델
2. **Navisworks가 더 풍부**: 계층 구조가 자연스럽게 표현됨
3. **Revit 보완 필요**: parent_id, level, spatial_structure 추가로 계층 정보 확보
4. **통합 가능**: 뷰와 함수로 두 소스 통합 가능

### 권장사항

**즉시**: 통합 뷰 생성하여 현재 데이터로 작업
**단기**: Revit 스키마 확장하여 계층 정보 추가
**장기**: 완전 통합 스키마로 마이그레이션

---

**작성자**: Database Analysis Team
**최종 수정**: 2025-10-18

# DXTnavis Geometry Export System - Integration Guide

> **Version**: v1.4.0
> **Date**: 2026-02-06
> **Target**: bim-ontology 프로젝트 연동

---

## 1. Overview

DXTnavis v1.4.0에서 **Geometry Export System**이 구현되었습니다. 이 시스템은 Navisworks BIM 모델에서 기하학 정보(BoundingBox, Mesh)를 추출하여 외부 3D 뷰어 및 Knowledge Graph와 연동할 수 있는 형식으로 내보냅니다.

### 목표
- **Palantir-style 3D + Ontology 시각화** 구현
- BIM 객체의 기하학 정보와 의미론적 정보(RDF) 통합
- External 3D Viewer (Three.js, CesiumJS, deck.gl) 연동

### 핵심 개념
```
┌─────────────────┐      ┌─────────────────┐      ┌─────────────────┐
│   Navisworks    │ ───► │    DXTnavis     │ ───► │  External       │
│   BIM Model     │      │  Geometry Export│      │  3D Viewer +    │
│                 │      │                 │      │  Knowledge Graph│
└─────────────────┘      └─────────────────┘      └─────────────────┘
```

---

## 2. Output Structure

DXTnavis Geometry Export는 다음 파일들을 생성합니다:

```
export/
├── manifest.json      # 객체별 BoundingBox + Centroid + MeshUri
├── geometry.csv       # 스프레드시트 호환 포맷
├── geometry.ttl       # RDF/TTL (GeoSPARQL 포함)
└── mesh/
    ├── {ObjectId}.glb # GLB 3D 메시 파일
    └── {ObjectId}.obj # OBJ 대안 포맷
```

---

## 3. ObjectId - 공통 Join Key

**ObjectId (GUID)**는 모든 데이터 형식에서 동일한 값을 사용하여 연결 고리 역할을 합니다.

### ObjectId 생성 규칙 (GetStableObjectId)

```
우선순위:
1. Navisworks InstanceGuid (if not Empty)
2. Item 카테고리의 GUID 속성
3. Authoring ID (Revit Element ID, AutoCAD Handle, IFC GlobalId)
4. Hierarchy Path 기반 Synthetic ID (MD5 해시)
```

### 연동 예시

| 데이터 소스 | ObjectId 사용 |
|-------------|---------------|
| **manifest.json** | `"objectId": "a1b2c3d4e5f6..."` |
| **RDF Subject** | `<bso:Object_a1b2c3d4e5f6...>` |
| **Mesh 파일** | `mesh/a1b2c3d4e5f6....glb` |
| **SPARQL 쿼리** | `?obj bso:objectId "a1b2c3d4e5f6..."` |

---

## 4. manifest.json 구조

### 전체 구조

```json
{
  "metadata": {
    "version": "1.0.0",
    "generator": "DXTnavis v1.4.0",
    "exportDate": "2026-02-06T12:00:00Z",
    "projectName": "Sample Project",
    "objectCount": 5000,
    "globalBoundingBox": {
      "min": { "x": -100.0, "y": -50.0, "z": 0.0 },
      "max": { "x": 200.0, "y": 150.0, "z": 50.0 }
    },
    "meshCount": 500
  },
  "objects": [
    {
      "objectId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "displayName": "Column-001",
      "category": "Structural Columns",
      "bbox": {
        "min": { "x": 10.5, "y": 20.3, "z": 0.0 },
        "max": { "x": 11.0, "y": 21.0, "z": 3.5 }
      },
      "centroid": { "x": 10.75, "y": 20.65, "z": 1.75 },
      "hasMesh": true,
      "meshUri": "mesh/a1b2c3d4e5f67890abcdef1234567890.glb"
    }
  ]
}
```

### 필드 설명

| 필드 | 타입 | 설명 |
|------|------|------|
| `objectId` | GUID | 객체 고유 식별자 (Join Key) |
| `displayName` | string | 객체 표시 이름 |
| `category` | string | Navisworks 카테고리 |
| `bbox.min/max` | Point3D | World 좌표계 AABB |
| `centroid` | Point3D | BBox 중심점 |
| `hasMesh` | boolean | Mesh 파일 존재 여부 |
| `meshUri` | string | Mesh 파일 상대 경로 |

---

## 5. RDF/TTL 형식 (geometry.ttl)

### Prefix 선언

```turtle
@prefix rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#> .
@prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .
@prefix xsd: <http://www.w3.org/2001/XMLSchema#> .
@prefix bso: <http://example.org/bso#> .
@prefix geo: <http://www.opengis.net/ont/geosparql#> .
```

### Triple 구조

```turtle
# Object: Column-001
bso:Object_a1b2c3d4e5f67890abcdef1234567890 bso:hasBoundingBox bso:BBox_a1b2c3d4e5f67890abcdef1234567890 .
bso:BBox_a1b2c3d4e5f67890abcdef1234567890 a bso:BoundingBox ;
    bso:minX "10.5"^^xsd:double ;
    bso:minY "20.3"^^xsd:double ;
    bso:minZ "0.0"^^xsd:double ;
    bso:maxX "11.0"^^xsd:double ;
    bso:maxY "21.0"^^xsd:double ;
    bso:maxZ "3.5"^^xsd:double .

# GeoSPARQL WKT (2D footprint)
bso:BBox_a1b2c3d4e5f67890abcdef1234567890 geo:asWKT "POLYGON((10.5 20.3, 11.0 20.3, 11.0 21.0, 10.5 21.0, 10.5 20.3))"^^geo:wktLiteral .

# Centroid
bso:Object_a1b2c3d4e5f67890abcdef1234567890 bso:centroidX "10.75"^^xsd:double .
bso:Object_a1b2c3d4e5f67890abcdef1234567890 bso:centroidY "20.65"^^xsd:double .
bso:Object_a1b2c3d4e5f67890abcdef1234567890 bso:centroidZ "1.75"^^xsd:double .
bso:Object_a1b2c3d4e5f67890abcdef1234567890 bso:centroidWKT "POINT(10.75 20.65 1.75)"^^geo:wktLiteral .

# Volume & Mesh
bso:Object_a1b2c3d4e5f67890abcdef1234567890 bso:volume "1.225"^^xsd:double .
bso:Object_a1b2c3d4e5f67890abcdef1234567890 bso:hasMesh "true"^^xsd:boolean .
bso:Object_a1b2c3d4e5f67890abcdef1234567890 bso:meshUri "mesh/a1b2c3d4e5f67890abcdef1234567890.glb" .

# Metadata
bso:Object_a1b2c3d4e5f67890abcdef1234567890 rdfs:label "Column-001" .
bso:Object_a1b2c3d4e5f67890abcdef1234567890 bso:category "Structural Columns" .
```

---

## 6. BSO Ontology Properties

### Geometry 관련 속성

| Property | Range | 설명 |
|----------|-------|------|
| `bso:hasBoundingBox` | `bso:BoundingBox` | BBox 연결 |
| `bso:minX`, `minY`, `minZ` | `xsd:double` | BBox 최소 좌표 |
| `bso:maxX`, `maxY`, `maxZ` | `xsd:double` | BBox 최대 좌표 |
| `bso:centroidX`, `centroidY`, `centroidZ` | `xsd:double` | 중심점 좌표 |
| `bso:centroidWKT` | `geo:wktLiteral` | 중심점 WKT |
| `bso:volume` | `xsd:double` | BBox 부피 (m³) |
| `bso:hasMesh` | `xsd:boolean` | Mesh 존재 여부 |
| `bso:meshUri` | `xsd:string` | Mesh 파일 경로 |

### GeoSPARQL 호환

- `geo:asWKT` - BBox를 2D POLYGON으로 표현 (XY 평면)
- 공간 쿼리 지원: `geo:sfContains`, `geo:sfIntersects` 등

---

## 7. SPARQL 쿼리 예시

### 특정 영역 내 객체 조회

```sparql
PREFIX bso: <http://example.org/bso#>
PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>

SELECT ?obj ?name ?volume
WHERE {
  ?obj a bso:BIMObject ;
       rdfs:label ?name ;
       bso:volume ?volume ;
       bso:hasBoundingBox ?bbox .

  ?bbox bso:minX ?minX ;
        bso:minY ?minY ;
        bso:maxX ?maxX ;
        bso:maxY ?maxY .

  FILTER (?minX >= 0 && ?maxX <= 100)
  FILTER (?minY >= 0 && ?maxY <= 100)
}
ORDER BY DESC(?volume)
```

### Geometry + Schedule 조인

```sparql
PREFIX bso: <http://example.org/bso#>

SELECT ?obj ?name ?plannedStart ?centroidX ?centroidY ?centroidZ
WHERE {
  ?obj rdfs:label ?name ;
       bso:plannedStart ?plannedStart ;
       bso:centroidX ?centroidX ;
       bso:centroidY ?centroidY ;
       bso:centroidZ ?centroidZ .
}
ORDER BY ?plannedStart
```

---

## 8. External 3D Viewer 연동

### Three.js 예시

```javascript
// manifest.json 로드
const response = await fetch('export/manifest.json');
const manifest = await response.json();

// BoundingBox 시각화
manifest.objects.forEach(obj => {
  const { min, max } = obj.bbox;
  const size = {
    x: max.x - min.x,
    y: max.y - min.y,
    z: max.z - min.z
  };

  const geometry = new THREE.BoxGeometry(size.x, size.y, size.z);
  const material = new THREE.MeshBasicMaterial({
    color: 0x0078D4,
    wireframe: true
  });

  const cube = new THREE.Mesh(geometry, material);
  cube.position.set(
    obj.centroid.x,
    obj.centroid.y,
    obj.centroid.z
  );
  cube.userData.objectId = obj.objectId; // Join Key 저장

  scene.add(cube);
});

// GLB Mesh 로드 (선택적)
const loader = new THREE.GLTFLoader();
manifest.objects
  .filter(obj => obj.hasMesh)
  .forEach(obj => {
    loader.load(`export/${obj.meshUri}`, (gltf) => {
      gltf.scene.userData.objectId = obj.objectId;
      scene.add(gltf.scene);
    });
  });
```

### Knowledge Graph 연동

```javascript
// 3D 객체 클릭 시 SPARQL 쿼리
function onObjectClick(objectId) {
  const query = `
    PREFIX bso: <http://example.org/bso#>
    SELECT ?property ?value
    WHERE {
      bso:Object_${objectId} ?property ?value .
    }
  `;

  // SPARQL 엔드포인트 호출
  fetch(`/sparql?query=${encodeURIComponent(query)}`)
    .then(res => res.json())
    .then(data => showPropertyPanel(data));
}
```

---

## 9. dxtnavis-rules.yaml 업데이트

Geometry Export와 함께 `dxtnavis-rules.yaml`의 네임스페이스가 통일되었습니다:

- **변경**: `dxt:` → `bso:` (54개 항목)
- **이유**: BIM-Schedule Ontology (BSO) 표준 네임스페이스 사용

### 분류 규칙 예시

```yaml
classification_rules:
  - name: "Column Classification"
    keywords: ["Column", "기둥", "COL"]
    target_class: "bso:Column"
    enabled: true
```

---

## 10. 파일 목록

### 신규 생성 파일

| 경로 | 설명 |
|------|------|
| `Models/Geometry/Point3D.cs` | 3D 좌표 구조체 |
| `Models/Geometry/BBox3D.cs` | Bounding Box 모델 |
| `Models/Geometry/GeometryRecord.cs` | Geometry 레코드 |
| `Services/Geometry/GeometryExtractor.cs` | BBox 추출 서비스 |
| `Services/Geometry/GeometryFileWriter.cs` | 파일 출력 서비스 |
| `Services/Geometry/MeshExtractor.cs` | COM API Mesh 추출 |
| `Services/Geometry/GeometryRdfIntegrator.cs` | RDF 변환 서비스 |

### 수정된 파일

| 경로 | 변경 내용 |
|------|----------|
| `DXTnavis.csproj` | Phase 15 파일 참조 추가 |
| `ViewModels/DXwindowViewModel.cs` | Geometry Export Commands |
| `ViewModels/DXwindowViewModel.Export.cs` | Export 메서드 추가 |
| `Views/DXwindow.xaml` | Geometry Export 버튼 추가 |
| `Resources/Ontology/dxtnavis-rules.yaml` | bso: 네임스페이스 |

---

## 11. 다음 단계

### bim-ontology 프로젝트에서 할 일

1. **BSO Schema 업데이트** - Geometry 관련 클래스/속성 추가
2. **SPARQL 쿼리 템플릿** - 공간 쿼리 지원
3. **3D Viewer 프로토타입** - manifest.json 시각화
4. **RDF Store 연동** - geometry.ttl 임포트 테스트

### 테스트 데이터

DXTnavis에서 테스트 모델로 Export한 샘플 데이터가 필요하면 요청해주세요.

---

## Contact

- **DXTnavis Repository**: https://github.com/tygwan/DXTnavis
- **Version**: v1.4.0
- **Last Updated**: 2026-02-06

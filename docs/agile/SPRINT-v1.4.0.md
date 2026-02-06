# Sprint v1.4.0 - Geometry Export System

> **Sprint Goal**: Geometry 추출 및 외부 3D 뷰어 연동을 위한 Hybrid Export 시스템 구축
> **Duration**: 2 Sprints (4 weeks)
> **Target Release**: v1.4.0
> **Research Target**: Palantir-style 3D + Ontology 시각화

---

## Sprint Overview

### Objective
Navisworks BIM 모델에서 기하학 정보(BoundingBox, Mesh)를 추출하여 외부 3D 뷰어(Three.js, CesiumJS, deck.gl)에서 Knowledge Graph와 통합 시각화 가능하게 함

### Data Flow
```
Navisworks Model → BoundingBox/Mesh → manifest.json + GLB → External 3D Viewer
     (Source)         (Extract)           (Export)              (Visualize)
         ↓                                    ↓                      ↓
  HierarchicalRecord ───────────────────▶ RDF + geometry ◀───── ObjectId Join
```

### Architecture
```
┌─────────────────────────────────────────────────────────────────────┐
│                    Palantir-style 3D + Ontology                     │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌─────────────────┐    ┌─────────────────┐    ┌────────────────┐  │
│  │ GeometryExtract │───▶│ GeometryFile    │───▶│ External 3D    │  │
│  │ (BBox + Mesh)   │    │ Writer          │    │ Viewer         │  │
│  └─────────────────┘    └─────────────────┘    └────────────────┘  │
│           │                     │                      │           │
│           └─────────────────────┼──────────────────────┘           │
│                                 │                                  │
│                          ┌──────▼──────┐                           │
│                          │  ObjectId   │                           │
│                          │ (Join Key)  │                           │
│                          └──────┬──────┘                           │
│                                 │                                  │
│  ┌─────────────────┐    ┌──────▼──────┐    ┌────────────────┐     │
│  │ HierarchyToRdf  │◀──▶│ Knowledge   │◀──▶│ Neo4j/SPARQL   │     │
│  │ + Geometry      │    │ Graph       │    │ Query          │     │
│  └─────────────────┘    └─────────────┘    └────────────────┘     │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### Success Criteria
| Metric | Target | Measurement |
|--------|--------|-------------|
| BBox 추출 속도 | < 5초 | 5,000 objects |
| Mesh 추출 속도 | < 30초 | 100 selected objects |
| GLB 파일 크기 | < 50MB | 전체 모델 |
| manifest.json | < 5MB | 메타데이터 |
| Three.js 로딩 | < 3초 | manifest + mesh |

### Tech Stack
| Component | Package/Format | Version | Purpose |
|-----------|---------------|---------|---------|
| Geometry Model | Point3D, BBox3D | - | 좌표/경계 모델 |
| Mesh Format | glTF/GLB | 2.0 | 3D 메시 직렬화 |
| Manifest | JSON | - | 객체 메타데이터 |
| RDF Extension | dotNetRDF | 3.4.1 | geometry 속성 추가 |
| COM Interop | Navisworks ComAPI | 2025 | Mesh primitive 추출 |

---

## Phase 15: Geometry Export System

### Epic 15.1: GeometryRecord 모델 (4h)

#### Story 15.1.1: Point3D 구조체
| Task | Priority | Estimate | Status |
|------|----------|----------|--------|
| Point3D.cs 생성 (X, Y, Z) | P0 | 30m | 📋 TODO |
| Equals, GetHashCode 구현 | P1 | 30m | 📋 TODO |
| ToString() (디버깅용) | P2 | 15m | 📋 TODO |

#### Story 15.1.2: BBox3D 클래스
| Task | Priority | Estimate | Status |
|------|----------|----------|--------|
| BBox3D.cs 생성 (Min, Max) | P0 | 30m | 📋 TODO |
| Contains(Point3D) 메서드 | P1 | 30m | 📋 TODO |
| Intersects(BBox3D) 메서드 | P1 | 30m | 📋 TODO |
| GetCentroid() 메서드 | P0 | 15m | 📋 TODO |

#### Story 15.1.3: GeometryRecord 클래스
| Task | Priority | Estimate | Status |
|------|----------|----------|--------|
| GeometryRecord.cs 생성 | P0 | 30m | 📋 TODO |
| ObjectId, BBox, Centroid 속성 | P0 | 15m | 📋 TODO |
| HasMesh, MeshUri 속성 | P0 | 15m | 📋 TODO |
| ToJson() 직렬화 메서드 | P1 | 30m | 📋 TODO |

**Acceptance Criteria:**
- [ ] Models/Geometry/ 폴더에 3개 파일 생성
- [ ] 빌드 성공
- [ ] JSON 직렬화 테스트 통과

---

### Epic 15.2: BoundingBox 추출 서비스 (6h)

#### Story 15.2.1: GeometryExtractor 기본
| Task | Priority | Estimate | Status |
|------|----------|----------|--------|
| Services/Geometry/ 폴더 생성 | P0 | 10m | 📋 TODO |
| GeometryExtractor.cs 생성 | P0 | 1h | 📋 TODO |
| ExtractBoundingBox(ModelItem) | P0 | 2h | 📋 TODO |
| World coordinate 변환 | P0 | 1h | 📋 TODO |

#### Story 15.2.2: 배치 추출
| Task | Priority | Estimate | Status |
|------|----------|----------|--------|
| ExtractAllBoundingBoxes() | P0 | 1h | 📋 TODO |
| ProgressChanged 이벤트 | P1 | 30m | 📋 TODO |
| Cancellation 지원 | P1 | 30m | 📋 TODO |

**Navisworks API 코드:**
```csharp
var bb = modelItem.BoundingBox();
var min = new Point3D(bb.Min.X, bb.Min.Y, bb.Min.Z);
var max = new Point3D(bb.Max.X, bb.Max.Y, bb.Max.Z);
var centroid = new Point3D(
    (bb.Min.X + bb.Max.X) * 0.5,
    (bb.Min.Y + bb.Max.Y) * 0.5,
    (bb.Min.Z + bb.Max.Z) * 0.5);
```

**Acceptance Criteria:**
- [ ] 모든 ModelItem에서 BBox 추출 성공
- [ ] World coordinate 변환 정확성 확인
- [ ] 5,000 objects < 5초

---

### Epic 15.3: COM Mesh 추출 (12h) - Optional

#### Story 15.3.1: SimplePrimitivesCB 콜백
| Task | Priority | Estimate | Status |
|------|----------|----------|--------|
| MeshExtractor.cs 생성 | P0 | 1h | 📋 TODO |
| SimplePrimitivesCB 구현 | P0 | 3h | 📋 TODO |
| Vertex deduplication | P0 | 2h | 📋 TODO |
| Triangle index 수집 | P0 | 2h | 📋 TODO |

#### Story 15.3.2: GLB 직렬화
| Task | Priority | Estimate | Status |
|------|----------|----------|--------|
| MeshData 모델 정의 | P0 | 1h | 📋 TODO |
| GLB 바이너리 작성 | P0 | 2h | 📋 TODO |
| Transform 적용 | P1 | 1h | 📋 TODO |

**COM Interop 코드:**
```csharp
InwOaPath comPath = ComApiBridge.ToInwOaPath(item);
foreach (InwOaFragment3 frag in comPath.Fragments())
{
    frag.GenerateSimplePrimitives(
        nwEVertexProperty.eNORMAL,
        callback);
}
```

**Acceptance Criteria:**
- [ ] 선택된 객체에서 mesh 추출 성공
- [ ] GLB 파일 생성 및 Three.js 로드 확인
- [ ] 100 objects < 30초

---

### Epic 15.4: GeometryFileWriter (8h)

#### Story 15.4.1: manifest.json 작성
| Task | Priority | Estimate | Status |
|------|----------|----------|--------|
| GeometryFileWriter.cs 생성 | P0 | 1h | 📋 TODO |
| WriteManifest() 구현 | P0 | 2h | 📋 TODO |
| JSON 스키마 정의 | P1 | 1h | 📋 TODO |

#### Story 15.4.2: mesh/*.glb 작성
| Task | Priority | Estimate | Status |
|------|----------|----------|--------|
| CreateExportFolder() | P0 | 30m | 📋 TODO |
| WriteMeshGlb() 구현 | P0 | 2h | 📋 TODO |
| 파일명 ObjectId 매핑 | P0 | 30m | 📋 TODO |
| 진행률 추적 | P1 | 1h | 📋 TODO |

**manifest.json 구조:**
```json
{
  "version": "1.0",
  "generator": "DXTnavis 1.4.0",
  "coordinateSystem": "navisworks-world",
  "objects": [
    {
      "objectId": "c6b7fbe1-7f8c-4f8f-a61e-3f6e7f9a3d7a",
      "bbox": { "min": { "x": 1.0, "y": 2.0, "z": 3.0 }, "max": { "x": 4.0, "y": 5.0, "z": 6.0 } },
      "centroid": { "x": 2.5, "y": 3.5, "z": 4.5 },
      "hasMesh": true,
      "meshUri": "mesh/c6b7fbe17f8c4f8fa61e3f6e7f9a3d7a.glb"
    }
  ]
}
```

**Acceptance Criteria:**
- [ ] export/ 폴더 구조 생성
- [ ] manifest.json 유효한 JSON
- [ ] GLB 파일 Three.js 로드 성공

---

### Epic 15.5: RDF Geometry 통합 (4h)

#### Story 15.5.1: HierarchyToRdfConverter 확장
| Task | Priority | Estimate | Status |
|------|----------|----------|--------|
| Convert() 오버로드 추가 | P0 | 1h | 📋 TODO |
| AddGeometryTriples() 구현 | P0 | 2h | 📋 TODO |
| BSO namespace 속성 정의 | P0 | 30m | 📋 TODO |

#### Story 15.5.2: OntologyService 통합
| Task | Priority | Estimate | Status |
|------|----------|----------|--------|
| LoadFromHierarchy() 오버로드 | P0 | 30m | 📋 TODO |

**RDF 속성:**
```turtle
bso:element_xxx
    bso:geometryUri "mesh/xxx.glb" ;
    bso:bboxMinX 1.0 ;
    bso:bboxMinY 2.0 ;
    bso:bboxMinZ 3.0 ;
    bso:bboxMaxX 4.0 ;
    bso:bboxMaxY 5.0 ;
    bso:bboxMaxZ 6.0 ;
    bso:centroidX 2.5 ;
    bso:centroidY 3.5 ;
    bso:centroidZ 4.5 .
```

**Acceptance Criteria:**
- [ ] RDF에 geometry 속성 포함
- [ ] SPARQL 쿼리로 geometry 조회 가능
- [ ] 기존 기능 호환성 유지

---

### Epic 15.6: Geometry Export UI (4h)

#### Story 15.6.1: OntologyViewModel 확장
| Task | Priority | Estimate | Status |
|------|----------|----------|--------|
| ExportGeometryCommand 추가 | P0 | 1h | 📋 TODO |
| IncludeMeshForSelected 속성 | P0 | 30m | 📋 TODO |
| GeometryExportProgress 속성 | P1 | 30m | 📋 TODO |

#### Story 15.6.2: XAML UI
| Task | Priority | Estimate | Status |
|------|----------|----------|--------|
| Export Geometry 버튼 | P0 | 30m | 📋 TODO |
| Include Mesh 체크박스 | P0 | 30m | 📋 TODO |
| 진행률 ProgressBar | P1 | 30m | 📋 TODO |
| 출력 경로 선택 | P1 | 30m | 📋 TODO |

**Acceptance Criteria:**
- [ ] [Ontology] 탭에 Export 버튼 표시
- [ ] 폴더 선택 다이얼로그 동작
- [ ] 진행률 표시 정상

---

## Sprint Backlog Summary

| Epic | Story Points | Hours | Status |
|------|--------------|-------|--------|
| 15.1 GeometryRecord 모델 | 4 SP | 4h | 📋 TODO |
| 15.2 BoundingBox 추출 | 6 SP | 6h | 📋 TODO |
| 15.3 COM Mesh 추출 | 12 SP | 12h | 📋 TODO (Optional) |
| 15.4 GeometryFileWriter | 8 SP | 8h | 📋 TODO |
| 15.5 RDF Geometry 통합 | 4 SP | 4h | 📋 TODO |
| 15.6 Geometry Export UI | 4 SP | 4h | 📋 TODO |
| **Total** | **38 SP** | **38h** | |

---

## External Viewer Integration (Reference)

### Three.js Example
```javascript
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';

// Load manifest
const manifest = await fetch('export/manifest.json').then(r => r.json());

// Load meshes
const loader = new GLTFLoader();
for (const obj of manifest.objects) {
  if (obj.hasMesh) {
    const gltf = await loader.loadAsync(`export/${obj.meshUri}`);
    gltf.scene.userData.objectId = obj.objectId;
    scene.add(gltf.scene);
  }
}

// Query Knowledge Graph by objectId
async function queryKG(objectId) {
  const sparql = `SELECT ?prop ?value WHERE { bso:element_${objectId} ?prop ?value }`;
  return await fetch(`/sparql?query=${encodeURIComponent(sparql)}`).then(r => r.json());
}
```

### CesiumJS Example
```javascript
const viewer = new Cesium.Viewer('cesiumContainer');

// Load manifest
const manifest = await Cesium.Resource.fetchJson({ url: 'export/manifest.json' });

// Add 3D Tiles or individual GLTFs
for (const obj of manifest.objects) {
  if (obj.hasMesh) {
    const model = await Cesium.Model.fromGltfAsync({
      url: `export/${obj.meshUri}`,
      modelMatrix: Cesium.Matrix4.fromTranslation(
        Cesium.Cartesian3.fromDegrees(obj.centroid.x, obj.centroid.y, obj.centroid.z)
      )
    });
    viewer.scene.primitives.add(model);
  }
}
```

---

## Dependencies

### Blocking
- v1.3.0 완료 (RDF/Ontology 시스템) ✅

### Non-Blocking
- dxtnavis-rules.yaml namespace 마이그레이션 (dxt: → bso:)
- README.md 버전 업데이트

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| COM Mesh 추출 성능 | High | Optional 처리, 선택적 추출만 |
| GLB 파일 크기 | Medium | Vertex deduplication, LOD 고려 |
| Navisworks API 제한 | Medium | BBox 기본, Mesh 선택적 |
| Three.js 호환성 | Low | 표준 glTF 2.0 사용 |

---

**Last Updated**: 2026-02-06

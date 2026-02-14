# Phase 18: 3D Mesh GLB Export

> **Version:** v1.6.0
> **Status:** IN PROGRESS
> **Started:** 2026-02-14
> **Source:** [bim-ontology/dxtnavis-mesh-strategy.md](../../../bim-ontology/docs/dxtnavis-mesh-strategy.md)

---

## 목표

NWD 모델에서 개별 객체의 3D Mesh를 GLB(glTF 2.0 binary) 파일로 추출하여
bim-ontology Dashboard에서 Three.js 3D 시각화를 지원.

## 기존 코드 상태 (Phase 15에서 구현)

| 파일 | 상태 | 설명 |
|------|------|------|
| `Services/Geometry/MeshExtractor.cs` | 코드 존재 | COM API `GenerateSimplePrimitives()` 콜백 |
| `MeshExtractor.MeshCallbackSink` | 코드 존재 | `InwSimplePrimitivesCB` 구현 (vertex/triangle 수집) |
| `MeshExtractor.SaveToGlb()` | 코드 존재 | 최소 glTF 2.0 binary writer |
| `MeshExtractor.SaveToObj()` | 코드 존재 | OBJ 포맷 대안 출력 |
| `Models/Geometry/GeometryRecord.cs` | 코드 존재 | `HasMesh`, `MeshUri` 필드 |
| `Services/Geometry/GeometryFileWriter.cs` | 코드 존재 | `mesh/` 폴더 + manifest.json 출력 |

## Phase 18 변경사항

### A. GeometryExtractor 확장
- `LastModelItemMap` 프로퍼티 추가 (ObjectId → ModelItem 매핑)
- 추출 시 자동으로 ModelItem 참조 보존 → mesh 추출에 필요

### B. GLB Writer 수정
- glTF spec 준수: POSITION accessor에 `min`/`max` bounds 추가
- generator 문자열 업데이트

### C. MeshUri 형식 통일
- `ObjectId:N` (하이픈 없음) → `ObjectId:D` (하이픈 UUID)
- 파일명 패턴: `mesh/{uuid}.glb`

### D. Full Pipeline 통합 (4-stage → 5-stage)
1. Unified CSV
2. Geometry BBox 추출
3. **Mesh GLB 추출** (NEW)
4. Adjacency 검출
5. 파일 출력 (CSV + manifest, geometry CSV는 mesh 정보 반영)

### E. Test Mesh 버튼
- 선택 객체 → GLB + OBJ 동시 출력
- online viewer 검증용: https://gltf-viewer.donmccurdy.com/

## 출력 파일 구조

```
dxtnavis_export_YYYYMMDD_HHmmss/
├── unified.csv
├── geometry.csv              (HasMesh=true, MeshUri 반영)
├── manifest.json             (meshCount 포함)
├── adjacency.csv
├── connected_groups.csv
├── spatial_relationships.ttl
├── spatial_summary.txt
└── mesh/                     (NEW)
    ├── 8dd55e0a-2aee-5612-8465-b8f7ff0e7da3.glb
    ├── 550e8400-e29b-41d4-a716-446655440000.glb
    └── ... (HasMesh=true인 객체별 1파일)
```

## Gate Checklist

- [x] GeometryExtractor: LastModelItemMap 프로퍼티
- [x] MeshExtractor: GLB min/max bounds
- [x] GeometryRecord: MeshUri 하이픈 UUID
- [x] Full Pipeline: 5-stage 통합
- [x] UI: Test Mesh 버튼
- [x] Build 성공
- [ ] 실제 Navisworks에서 단일 객체 테스트
- [ ] GLB online viewer 확인
- [ ] 배치 export (B01 Area)
- [ ] bim-ontology Dashboard 연동 확인

## 좌표계 주의사항

```
Navisworks: Y-up 또는 Z-up (원본 CAD에 따라 다름)
Three.js:   Y-up

확인: BBox centroid와 mesh centroid 비교
보정: mesh.rotation.x = -Math.PI / 2 (Z-up → Y-up)
```

## 향후 최적화 (Phase C)

- SharpGLTF NuGet 도입 (Draco 압축, Material 지원)
- LOD 제어: faceting factor 조절
- 크기 필터: BBox volume < threshold → skip
- 카테고리 필터: Structure/Pipe 등 우선순위 선택
- 증분 export: 기존 GLB 있으면 skip

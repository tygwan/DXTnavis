# Phase 17: Spatial Connectivity & Adjacency Export

> **Status**: IN PROGRESS
> **Version Target**: v1.5.0
> **Created**: 2026-02-12
> **Source Plan**: bim-ontology/docs/dxtnavis-phase18-plan.md
> **Dependencies**: Phase 15 (Geometry Export), Phase 16 (Unified CSV)

---

## 1. 개요

### 1.1 목표
DXTnavis에서 **공간 인접성(Adjacency) 및 연결성(Connectivity)** 데이터를 추출하여
bim-ontology의 Task Scheduling + 3D Visualization 파이프라인에 공급한다.

### 1.2 선행 Phase 상태

| Phase | 구현 내용 | 상태 |
|-------|----------|------|
| Phase 15 | Geometry Export System (BBox + Mesh) | DONE |
| Phase 16 | Unified CSV Export (계층 + 기하 병합) | DONE |
| **Phase 17** | **Spatial Connectivity + Adjacency** | **IN PROGRESS** |

### 1.3 핵심 접근

- Navisworks API에 명시적 "연결" 정보 없음
- **BBox 기반 인접성 검출**이 유일하게 확실한 접근
- 기존 `BBox3D.Intersects()` 활용 + tolerance 기반 인접 판정
- Spatial Hash Grid로 O(n²) → O(n) 최적화

---

## 2. 구현 파일 구조

```
DXTnavis/
├── Models/Spatial/
│   ├── AdjacencyRecord.cs            # 인접 관계 레코드
│   └── ConnectedGroup.cs             # 연결 그룹 모델
├── Services/Spatial/
│   ├── AdjacencyDetector.cs          # BBox 인접성 검출
│   ├── ConnectedComponentFinder.cs   # Union-Find 연결 컴포넌트
│   └── SpatialRelationshipWriter.cs  # CSV/TTL 출력
└── Models/Geometry/
    └── BBox3D.cs                     # 확장: DistanceTo, IsAdjacentTo, OverlapVolume
```

---

## 3. BBox3D 확장 메서드

```csharp
public double DistanceTo(BBox3D other);                      // 최소 거리 계산
public bool IsAdjacentTo(BBox3D other, double tolerance);    // tolerance 이내 근접
public double OverlapVolume(BBox3D other);                   // 겹침 체적
```

---

## 4. AdjacencyDetector

### 카테고리별 Tolerance (미터)
| 카테고리 | Tolerance | 비고 |
|---------|-----------|------|
| Pipe | 0.05 (50mm) | 파이프 연결부 |
| Equipment | 0.10 (100mm) | 장비 접근 |
| Structure | 0.20 (200mm) | 구조물 |
| Insulation | 0.01 (10mm) | 보온재 |
| Default | 0.15 (150mm) | 기본 |

### 알고리즘
1. **BBox Expansion**: tolerance만큼 각 축으로 BBox 확장
2. **Intersection Test**: 확장된 BBox 간 교차 검사
3. **Semantic Filter**: 카테고리 호환성 확인
4. **Connected Component**: Union-Find 그래프 탐색

### 성능 추정
| 요소 수 | Brute Force O(n²) | Spatial Hash Grid |
|---------|-------------------|-------------------|
| 1,000 | 1s | 50ms |
| 10,000 | 100s | 500ms |
| 50,000 | timeout | 2.5s |

---

## 5. 출력 형식

### adjacency.csv
```csv
SourceObjectId,TargetObjectId,Distance,OverlapVolume,RelationType
a1b2c3d4,e5f67890,0.03,0.15,overlap
a1b2c3d4,f6g78901,0.08,0.00,near_touch
```

### connected_groups.csv
```csv
GroupId,ElementCount,TotalVolume,BBoxMinX,...,BBoxMaxZ,CentroidX,CentroidY,CentroidZ
G001,15,45.3,10.5,20.3,0.0,25.0,35.0,12.0,17.75,27.65,6.0
```

### spatial_relationships.ttl
```turtle
@prefix spatial: <http://example.org/bim-ontology/spatial#> .
@prefix bso: <http://example.org/bso#> .

bso:Object_a1b2c3d4 spatial:adjacentTo bso:Object_e5f67890 .
bso:Object_a1b2c3d4 spatial:inGroup spatial:Group_001 .
```

---

## 6. Gate 체크리스트

- [ ] BBox3D 확장 메서드 (DistanceTo, IsAdjacentTo, OverlapVolume)
- [ ] AdjacencyDetector 구현 (Brute Force + Spatial Hash Grid)
- [ ] ConnectedComponentFinder 구현 (Union-Find)
- [ ] SpatialRelationshipWriter 구현 (CSV + TTL)
- [ ] UI 커맨드 추가 (Export Adjacency)
- [ ] 빌드 성공
- [ ] bim-ontology 연동 검증 (adjacency.csv → navis_to_rdf.py)

---

## 7. Codex 리뷰 이슈 (Phase 17 전 해결 권장)

### CRITICAL
- **C-1**: NavisworksDataExtractor의 Synthetic ID 미적용 → Guid.Empty 조인 무결성
- **C-2**: UnifiedCsvExporter의 Guid.Empty 병합 문제

### HIGH
- **H-1**: Geometry ↔ Hierarchy ID 전략 불일치
- **H-4**: 네임스페이스 3중 분리 (dxt:/bso:/example.org)

### MEDIUM
- **M-1**: BBox가 자식 포함 AABB → leaf 노드만 인접성 검출 대상으로 제한

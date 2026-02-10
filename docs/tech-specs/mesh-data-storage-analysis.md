# 3D 메쉬 데이터 저장 형식 분석

> **작성일**: 2026-02-10
> **Phase**: 15 (Geometry Export System)
> **목적**: 3D 메쉬 데이터의 형태와 CSV 보관 가능성 검토

---

## 1. 현재 구현된 MeshData 구조

```csharp
public class MeshData
{
    public Guid ObjectId { get; set; }
    public List<float> Vertices { get; set; }   // [x1,y1,z1, x2,y2,z2, ...]
    public List<float> Normals { get; set; }    // [nx1,ny1,nz1, ...]
    public List<int> Indices { get; set; }      // [i1,i2,i3, ...] (삼각형)
    public int VertexCount { get; set; }        // Vertices.Count / 3
    public int TriangleCount { get; set; }      // Indices.Count / 3
}
```

### 데이터 추출 방식

- **COM API 사용**: `InwOaFragment3.GenerateSimplePrimitives()`
- **콜백 패턴**: `MeshCallbackSink` 클래스가 삼각형 데이터 수집
- **노말 포함**: `nwEVertexProperty.eNORMAL` 옵션으로 노말 벡터 추출

---

## 2. 데이터 규모 분석

### 객체 복잡도별 데이터 크기

| 객체 복잡도 | 정점 수 | 삼각형 수 | 데이터 크기 |
|------------|--------|----------|-------------|
| 단순 (벽, 슬라브) | 100~500 | 50~200 | 2~8 KB |
| 중간 (문, 창) | 500~2K | 200~1K | 8~40 KB |
| 복잡 (가구, 설비) | 2K~20K | 1K~10K | 40~400 KB |
| 매우 복잡 (MEP) | 20K~100K | 10K~50K | 400 KB~2 MB |

### 메모리 계산

```
정점 1개 = 3 × float(4 bytes) = 12 bytes
노말 1개 = 3 × float(4 bytes) = 12 bytes
인덱스 1개 = 1 × int(4 bytes) = 4 bytes

삼각형 1개 = 3 정점 + 3 노말 + 3 인덱스
           = 36 + 36 + 12 = 84 bytes
```

---

## 3. CSV 보관 가능성 검토

### ❌ CSV 저장 - 권장하지 않음

#### 문제점

| 문제점 | 설명 |
|--------|------|
| **데이터 크기** | 정점 10K개 = float 30K개 = 단일 셀에 ~300KB 텍스트 |
| **정밀도 손실** | float→텍스트→float 변환 시 부동소수점 오차 |
| **파싱 비효율** | JSON 배열 파싱 시 메모리/CPU 과부하 |
| **도구 비호환** | Excel, 데이터베이스 등 CSV 도구에서 처리 불가 |

#### CSV 저장 예시 (비권장)

```csv
ObjectId,VertexCount,TriangleCount,VerticesJson,IndicesJson
abc123,500,200,"[0.1,0.2,0.3,...30000개]","[0,1,2,...600개]"
```

→ 단일 행이 **수 MB**가 될 수 있음

---

## 4. 권장 저장 형식

### 4.1 GLB (glTF Binary) - 기본 권장 ✅

```
mesh/
├── {ObjectId1}.glb  # 바이너리 3D 파일
├── {ObjectId2}.glb
└── ...
```

| 장점 | 설명 |
|------|------|
| 산업 표준 | Khronos 공식 표준, 모든 3D 뷰어 지원 |
| 크기 효율 | 바이너리 압축으로 텍스트 대비 70% 절약 |
| 정밀도 유지 | float 데이터 그대로 저장 |
| 웹 호환 | Three.js, Babylon.js 등 웹 3D에서 직접 로드 |

#### 현재 구현

```csharp
// MeshExtractor.cs
public bool SaveToGlb(MeshData meshData, string outputPath)
{
    // GLB Magic: glTF
    bw.Write(0x46546C67); // "glTF"
    bw.Write((uint)2);    // Version 2
    // ... JSON chunk + Binary chunk
}
```

### 4.2 OBJ (Wavefront) - 텍스트 대안

```obj
# DXTnavis Mesh Export
# ObjectId: abc123
# Vertices: 500
# Triangles: 200

v 0.100000 0.200000 0.300000
v 0.150000 0.250000 0.350000
vn 0.000000 0.000000 1.000000
f 1//1 2//2 3//3
```

| 장점 | 설명 |
|------|------|
| 텍스트 기반 | 사람이 읽을 수 있음 |
| 범용성 | 거의 모든 3D 소프트웨어 지원 |
| 디버깅 용이 | 텍스트 에디터로 검증 가능 |

---

## 5. 하이브리드 접근법 (현재 구현)

### 구조

```
export_2026-02-10/
├── unified.csv           # 메타데이터 + 속성 + 메쉬 참조
├── geometry.csv          # BBox, Centroid (상세)
├── manifest.json         # 객체 목록 (JSON)
└── mesh/
    ├── abc123.glb        # 실제 3D 메쉬
    ├── def456.glb
    └── ...
```

### CSV에서 메쉬 참조

**UnifiedObjectRecord (Phase 16)**:
```csv
ObjectId,HasMesh,MeshUri,BBoxMinX,BBoxMinY,...
abc123-...,true,mesh/abc123.glb,0.0,0.0,...
def456-...,true,mesh/def456.glb,10.5,5.2,...
```

→ CSV는 **메타데이터와 참조**만, 실제 메쉬는 **바이너리 파일**로 분리

---

## 6. 결론

### 데이터 유형별 권장 형식

| 데이터 유형 | 권장 형식 | 이유 |
|------------|----------|------|
| 메타데이터 (ObjectId, Name) | CSV ✅ | 구조화된 행/열 데이터 |
| 속성 (Properties) | CSV + JSON ✅ | 가변 필드 지원 |
| BBox/Centroid | CSV ✅ | 고정 숫자 6~9개 |
| **메쉬 (Vertices, Indices)** | **GLB/OBJ** ❌CSV | 대용량 바이너리 |

### 현재 플러그인 구현 상태

| 기능 | 파일 | 상태 |
|------|------|------|
| GLB 저장 | `MeshExtractor.SaveToGlb()` | ✅ 구현 완료 |
| OBJ 저장 | `MeshExtractor.SaveToObj()` | ✅ 구현 완료 |
| 메쉬 참조 | `UnifiedObjectRecord.MeshUri` | ✅ 구현 완료 |
| 일괄 추출 | `MeshExtractor.ExtractMeshes()` | ✅ 구현 완료 |

---

## 7. 향후 고려사항

### 7.1 압축 최적화

- **Draco 압축**: glTF 2.0 확장으로 메쉬 크기 90% 감소 가능
- **LOD 생성**: Level of Detail로 다중 해상도 메쉬 제공

### 7.2 스트리밍 지원

- **3D Tiles**: 대규모 BIM 모델용 타일 기반 스트리밍
- **Cesium 연동**: GIS + BIM 통합 시각화

### 7.3 온톨로지 연동

```turtle
@prefix bim: <http://example.org/bim#> .
@prefix geo: <http://www.opengis.net/ont/geosparql#> .

bim:Element_abc123
    geo:hasGeometry [
        a geo:Geometry ;
        geo:asGLB <mesh/abc123.glb> ;
        geo:hasBoundingBox [
            geo:minX "0.0"^^xsd:double ;
            geo:maxX "10.5"^^xsd:double ;
            # ...
        ]
    ] .
```

---

## 참고 자료

- [glTF 2.0 Specification](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html)
- [Navisworks COM API - GenerateSimplePrimitives](https://help.autodesk.com/view/NAV/2025/ENU/)
- [OBJ File Format](https://en.wikipedia.org/wiki/Wavefront_.obj_file)

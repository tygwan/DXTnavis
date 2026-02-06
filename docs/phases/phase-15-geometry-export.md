# Phase 15: Geometry Export System

> **Status**: 📋 Planning
> **Sprint**: v1.4.0
> **Estimated Hours**: 38h
> **Codex Analysis**: gpt-5.3-codex xhigh (완료)

---

## Overview

BIM 모델의 기하학 정보를 추출하여 외부 3D 뷰어와 Knowledge Graph 통합 시각화를 가능하게 하는 Hybrid Export 시스템.

### Hybrid Approach
1. **BoundingBox (필수)**: 모든 객체에 대해 BBox + Centroid 추출
2. **Mesh (선택)**: 사용자가 선택한 객체에 대해 COM API로 vertices/triangles 추출

### Output Structure
```
export/
├── manifest.json      # 전체 객체 메타데이터 (bbox, centroid, meshUri)
└── mesh/
    ├── {objectId1}.glb
    ├── {objectId2}.glb
    └── ...
```

---

## Sub-Phases

| Phase | Document | Status | Est. |
|-------|----------|--------|------|
| **15.1** | GeometryRecord 모델 | 📋 TODO | 4h |
| **15.2** | BoundingBox 추출 서비스 | 📋 TODO | 6h |
| **15.3** | COM Mesh 추출 (Optional) | 📋 TODO | 12h |
| **15.4** | GeometryFileWriter | 📋 TODO | 8h |
| **15.5** | RDF Geometry 통합 | 📋 TODO | 4h |
| **15.6** | Geometry Export UI | 📋 TODO | 4h |

---

## Phase 15.1: GeometryRecord 모델

### Files to Create
```
Models/Geometry/
├── Point3D.cs         # X, Y, Z 좌표 구조체
├── BBox3D.cs          # Min, Max 경계 상자
└── GeometryRecord.cs  # 통합 기하학 레코드
```

### Point3D.cs
```csharp
using System;
using System.Globalization;

namespace DXTnavis.Models.Geometry
{
    public struct Point3D : IEquatable<Point3D>
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Point3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public bool Equals(Point3D other)
        {
            const double epsilon = 1e-9;
            return Math.Abs(X - other.X) < epsilon &&
                   Math.Abs(Y - other.Y) < epsilon &&
                   Math.Abs(Z - other.Z) < epsilon;
        }

        public override bool Equals(object obj) => obj is Point3D other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + X.GetHashCode();
                hash = hash * 31 + Y.GetHashCode();
                hash = hash * 31 + Z.GetHashCode();
                return hash;
            }
        }

        public override string ToString() =>
            $"({X.ToString("F6", CultureInfo.InvariantCulture)}, " +
            $"{Y.ToString("F6", CultureInfo.InvariantCulture)}, " +
            $"{Z.ToString("F6", CultureInfo.InvariantCulture)})";

        public static Point3D operator +(Point3D a, Point3D b) =>
            new Point3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Point3D operator -(Point3D a, Point3D b) =>
            new Point3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Point3D operator *(Point3D p, double scalar) =>
            new Point3D(p.X * scalar, p.Y * scalar, p.Z * scalar);

        public double DistanceTo(Point3D other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            var dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
```

### BBox3D.cs
```csharp
using System;

namespace DXTnavis.Models.Geometry
{
    public class BBox3D
    {
        public Point3D Min { get; set; }
        public Point3D Max { get; set; }

        public BBox3D() { }

        public BBox3D(Point3D min, Point3D max)
        {
            Min = min;
            Max = max;
        }

        public Point3D GetCentroid()
        {
            return new Point3D(
                (Min.X + Max.X) * 0.5,
                (Min.Y + Max.Y) * 0.5,
                (Min.Z + Max.Z) * 0.5);
        }

        public Point3D GetSize()
        {
            return new Point3D(
                Max.X - Min.X,
                Max.Y - Min.Y,
                Max.Z - Min.Z);
        }

        public double GetVolume()
        {
            var size = GetSize();
            return size.X * size.Y * size.Z;
        }

        public bool Contains(Point3D point)
        {
            return point.X >= Min.X && point.X <= Max.X &&
                   point.Y >= Min.Y && point.Y <= Max.Y &&
                   point.Z >= Min.Z && point.Z <= Max.Z;
        }

        public bool Intersects(BBox3D other)
        {
            return !(other.Max.X < Min.X || other.Min.X > Max.X ||
                     other.Max.Y < Min.Y || other.Min.Y > Max.Y ||
                     other.Max.Z < Min.Z || other.Min.Z > Max.Z);
        }

        public BBox3D Union(BBox3D other)
        {
            return new BBox3D(
                new Point3D(
                    Math.Min(Min.X, other.Min.X),
                    Math.Min(Min.Y, other.Min.Y),
                    Math.Min(Min.Z, other.Min.Z)),
                new Point3D(
                    Math.Max(Max.X, other.Max.X),
                    Math.Max(Max.Y, other.Max.Y),
                    Math.Max(Max.Z, other.Max.Z)));
        }

        public override string ToString() => $"BBox[{Min} → {Max}]";
    }
}
```

### GeometryRecord.cs
```csharp
using System;
using System.Text;

namespace DXTnavis.Models.Geometry
{
    public class GeometryRecord
    {
        public Guid ObjectId { get; set; }
        public BBox3D BBox { get; set; }
        public Point3D Centroid { get; set; }
        public bool HasMesh { get; set; }
        public string MeshUri { get; set; }

        public GeometryRecord() { }

        public GeometryRecord(Guid objectId, BBox3D bbox)
        {
            ObjectId = objectId;
            BBox = bbox;
            Centroid = bbox?.GetCentroid() ?? new Point3D();
            HasMesh = false;
            MeshUri = null;
        }

        public void SetMeshUri(string basePath)
        {
            if (HasMesh && ObjectId != Guid.Empty)
            {
                MeshUri = $"{basePath}/{ObjectId:N}.glb";
            }
        }

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"objectId\": \"{ObjectId}\",");
            sb.AppendLine("  \"bbox\": {");
            sb.AppendLine($"    \"min\": {{ \"x\": {BBox?.Min.X ?? 0}, \"y\": {BBox?.Min.Y ?? 0}, \"z\": {BBox?.Min.Z ?? 0} }},");
            sb.AppendLine($"    \"max\": {{ \"x\": {BBox?.Max.X ?? 0}, \"y\": {BBox?.Max.Y ?? 0}, \"z\": {BBox?.Max.Z ?? 0} }}");
            sb.AppendLine("  },");
            sb.AppendLine($"  \"centroid\": {{ \"x\": {Centroid.X}, \"y\": {Centroid.Y}, \"z\": {Centroid.Z} }},");
            sb.AppendLine($"  \"hasMesh\": {HasMesh.ToString().ToLower()},");
            sb.AppendLine($"  \"meshUri\": {(string.IsNullOrEmpty(MeshUri) ? "null" : $"\"{MeshUri}\"")}");
            sb.Append("}");
            return sb.ToString();
        }
    }
}
```

---

## Phase 15.2: BoundingBox 추출 서비스

### GeometryExtractor.cs (Codex 분석 기반)
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using Autodesk.Navisworks.Api;
using DXTnavis.Models.Geometry;

namespace DXTnavis.Services.Geometry
{
    public class GeometryExtractor
    {
        public event EventHandler<int> ProgressChanged;

        public GeometryRecord ExtractBoundingBox(ModelItem item)
        {
            if (item == null) return null;

            try
            {
                var bb = item.BoundingBox();
                if (bb == null || bb.IsEmpty) return null;

                var min = new Point3D(bb.Min.X, bb.Min.Y, bb.Min.Z);
                var max = new Point3D(bb.Max.X, bb.Max.Y, bb.Max.Z);
                var bbox = new BBox3D(min, max);

                var objectId = GetStableObjectId(item);
                return new GeometryRecord(objectId, bbox);
            }
            catch
            {
                return null;
            }
        }

        public Dictionary<Guid, GeometryRecord> ExtractAllBoundingBoxes(
            IEnumerable<ModelItem> items,
            CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<Guid, GeometryRecord>();
            var itemList = new List<ModelItem>(items);
            int total = itemList.Count;
            int processed = 0;

            foreach (var item in itemList)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var record = ExtractBoundingBox(item);
                if (record != null && record.ObjectId != Guid.Empty)
                {
                    result[record.ObjectId] = record;
                }

                processed++;
                if (processed % 100 == 0)
                {
                    ProgressChanged?.Invoke(this, (int)(100.0 * processed / total));
                }
            }

            ProgressChanged?.Invoke(this, 100);
            return result;
        }

        private Guid GetStableObjectId(ModelItem item)
        {
            // 기존 NavisworksDataExtractor의 GetStableObjectId() 로직 재사용
            if (item.InstanceGuid != Guid.Empty)
                return item.InstanceGuid;

            // Fallback: Hierarchy path hash
            var pathHash = item.GetHashCode();
            return CreateDeterministicGuid(pathHash.ToString());
        }

        private Guid CreateDeterministicGuid(string input)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(input);
                var hash = md5.ComputeHash(bytes);
                return new Guid(hash);
            }
        }
    }
}
```

---

## Phase 15.3: COM Mesh 추출 (Optional)

### MeshExtractor.cs (Codex 분석 기반)
```csharp
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;

namespace DXTnavis.Services.Geometry
{
    public class MeshData
    {
        public List<float> Vertices { get; } = new List<float>();
        public List<int> Triangles { get; } = new List<int>();
        public Guid ObjectId { get; set; }
    }

    public class MeshExtractor
    {
        public MeshData ExtractMesh(ModelItem item)
        {
            if (item == null) return null;

            var meshData = new MeshData();
            var callback = new SimplePrimitivesCB(meshData);

            try
            {
                ComApi.InwOaPath comPath = ComApiBridge.ToInwOaPath(item);
                foreach (ComApi.InwOaFragment3 frag in comPath.Fragments())
                {
                    frag.GenerateSimplePrimitives(
                        ComApi.nwEVertexProperty.eNORMAL,
                        callback);
                }
            }
            catch
            {
                return null;
            }

            return meshData.Vertices.Count > 0 ? meshData : null;
        }

        private class SimplePrimitivesCB : ComApi.InwSimplePrimitivesCB
        {
            private readonly MeshData _mesh;
            private readonly List<float> _vertices;
            private readonly List<int> _triangles;
            private readonly Dictionary<VertexKey, int> _vertexToIndex = new Dictionary<VertexKey, int>();

            public SimplePrimitivesCB(MeshData mesh)
            {
                _mesh = mesh;
                _vertices = mesh.Vertices;
                _triangles = mesh.Triangles;
            }

            public void Line(ComApi.InwSimpleVertex v1, ComApi.InwSimpleVertex v2) { }
            public void Point(ComApi.InwSimpleVertex v1) { }
            public void SnapPoint(ComApi.InwSimpleVertex v1) { }

            public void Triangle(
                ComApi.InwSimpleVertex v1,
                ComApi.InwSimpleVertex v2,
                ComApi.InwSimpleVertex v3)
            {
                _triangles.Add(AddVertex(v1));
                _triangles.Add(AddVertex(v2));
                _triangles.Add(AddVertex(v3));
            }

            private int AddVertex(ComApi.InwSimpleVertex v)
            {
                var (x, y, z) = ReadCoord(v);
                var key = VertexKey.From(x, y, z);

                if (_vertexToIndex.TryGetValue(key, out int index)) return index;

                index = _vertices.Count / 3;
                _vertices.Add((float)x);
                _vertices.Add((float)y);
                _vertices.Add((float)z);
                _vertexToIndex.Add(key, index);
                return index;
            }

            private static (double x, double y, double z) ReadCoord(ComApi.InwSimpleVertex v)
            {
                var arr = (Array)(object)v.coord;
                int lb = arr.GetLowerBound(0);
                return (
                    Convert.ToDouble(arr.GetValue(lb + 0)),
                    Convert.ToDouble(arr.GetValue(lb + 1)),
                    Convert.ToDouble(arr.GetValue(lb + 2)));
            }
        }

        private struct VertexKey : IEquatable<VertexKey>
        {
            private const double Scale = 1_000_000d;
            private readonly long _x, _y, _z;

            private VertexKey(long x, long y, long z) { _x = x; _y = y; _z = z; }

            public static VertexKey From(double x, double y, double z) =>
                new VertexKey(
                    (long)Math.Round(x * Scale),
                    (long)Math.Round(y * Scale),
                    (long)Math.Round(z * Scale));

            public bool Equals(VertexKey other) => _x == other._x && _y == other._y && _z == other._z;
            public override bool Equals(object obj) => obj is VertexKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int h = 17;
                    h = (h * 31) + _x.GetHashCode();
                    h = (h * 31) + _y.GetHashCode();
                    h = (h * 31) + _z.GetHashCode();
                    return h;
                }
            }
        }
    }
}
```

---

## Phase 15.5: RDF Geometry 통합

### HierarchyToRdfConverter.cs 수정
```csharp
// 기존 메서드 오버로드
public ConversionResult Convert(
    IEnumerable<HierarchicalPropertyRecord> records,
    IReadOnlyDictionary<Guid, GeometryRecord> geometryByObjectId = null)
{
    // ... 기존 로직 ...

    foreach (var kvp in groups)
    {
        var objectId = kvp.Key;
        var props = kvp.Value;

        ConvertObject(objectId, props, result, geometryByObjectId);
        // ...
    }
}

private void AddGeometryTriples(IUriNode objUri, GeometryRecord g)
{
    if (g == null || g.BBox == null) return;

    if (!string.IsNullOrWhiteSpace(g.MeshUri))
    {
        AssertLiteral(objUri, "geometryUri", g.MeshUri);
    }

    AssertTypedLiteral(objUri, "bboxMinX", g.BBox.Min.X, "decimal");
    AssertTypedLiteral(objUri, "bboxMinY", g.BBox.Min.Y, "decimal");
    AssertTypedLiteral(objUri, "bboxMinZ", g.BBox.Min.Z, "decimal");
    AssertTypedLiteral(objUri, "bboxMaxX", g.BBox.Max.X, "decimal");
    AssertTypedLiteral(objUri, "bboxMaxY", g.BBox.Max.Y, "decimal");
    AssertTypedLiteral(objUri, "bboxMaxZ", g.BBox.Max.Z, "decimal");
    AssertTypedLiteral(objUri, "centroidX", g.Centroid.X, "decimal");
    AssertTypedLiteral(objUri, "centroidY", g.Centroid.Y, "decimal");
    AssertTypedLiteral(objUri, "centroidZ", g.Centroid.Z, "decimal");
}
```

---

## Acceptance Criteria

- [ ] Models/Geometry/ 폴더에 3개 모델 파일 생성
- [ ] Services/Geometry/ 폴더에 2개 서비스 파일 생성
- [ ] BBox 추출 5,000 objects < 5초
- [ ] manifest.json 유효한 JSON 형식
- [ ] GLB 파일 Three.js 로드 성공
- [ ] RDF에 geometry 속성 포함
- [ ] UI에서 Export 버튼 동작

---

**Last Updated**: 2026-02-06

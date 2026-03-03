using System;
using System.Collections.Generic;
using System.Diagnostics;
using DXTnavis.Models.Geometry;

namespace DXTnavis.Services.Geometry
{
    /// <summary>
    /// Phase 23: BBox 기반 Parametric Mesh 생성기
    /// gap_supplemented 객체의 mesh를 BBox 종횡비 분석 → Cylinder/Disc/Box로 교체
    /// </summary>
    public static class ParametricMeshGenerator
    {
        private const int DefaultSegments = 24;

        #region Public API

        /// <summary>
        /// gap_supplemented 객체에 대해 parametric mesh 생성 시도
        /// </summary>
        /// <param name="objectId">객체 GUID</param>
        /// <param name="bbox">BBox3D (WCS 좌표)</param>
        /// <param name="displayName">객체 이름 (형상 힌트용)</param>
        /// <returns>MeshData (교체 불가 시 null)</returns>
        public static MeshData TryGenerate(Guid objectId, BBox3D bbox, string displayName)
        {
            if (bbox == null || !bbox.IsValid || bbox.IsEmpty)
            {
                Debug.WriteLine(string.Format("[ParametricMesh] Skip {0}: invalid BBox", objectId));
                return null;
            }

            // Aspects는 복합 visual mesh → parametric 교체 불가
            if (IsAspectsType(displayName))
            {
                Debug.WriteLine(string.Format("[ParametricMesh] Skip Aspects: {0}", displayName));
                return null;
            }

            var shape = DetectShape(bbox, displayName);
            var analysis = AnalyzeCylindricalFit(bbox);
            var center = bbox.GetCentroid();

            MeshData mesh;
            switch (shape)
            {
                case ShapeHint.Disc:
                case ShapeHint.Cylinder:
                    mesh = GenerateCylinder(objectId, center,
                        analysis.AxisIndex, analysis.Radius, analysis.Height / 2.0,
                        DefaultSegments);
                    break;

                case ShapeHint.Plate:
                case ShapeHint.Box:
                default:
                    mesh = MeshExtractor.GenerateBoxMesh(bbox, objectId);
                    break;
            }

            if (mesh != null)
            {
                mesh.Quality = "parametric_fallback";
                Debug.WriteLine(string.Format(
                    "[ParametricMesh] {0} → {1} | Name={2} | Verts={3} Tris={4}",
                    objectId.ToString("D").Substring(0, 8), shape,
                    displayName ?? "(null)", mesh.VertexCount, mesh.TriangleCount));
            }

            return mesh;
        }

        /// <summary>
        /// Cylinder/Disc mesh 생성
        /// Caps(상/하면 fan) + Side wall(quad strip) 구조
        /// </summary>
        /// <param name="objectId">객체 GUID</param>
        /// <param name="center">BBox 중심점 (WCS)</param>
        /// <param name="axisIndex">회전축 (0=X, 1=Y, 2=Z)</param>
        /// <param name="radius">반지름</param>
        /// <param name="halfHeight">반높이 (center에서 cap까지)</param>
        /// <param name="segments">원주 분할 수 (기본 24)</param>
        public static MeshData GenerateCylinder(
            Guid objectId, Point3D center,
            int axisIndex, double radius, double halfHeight,
            int segments = 24)
        {
            if (radius <= 1e-9 || halfHeight <= 1e-9) return null;

            // 예상 vertex/index 크기
            // Top cap: 1 center + segments ring = segments+1 vertices, segments triangles
            // Bottom cap: same
            // Side wall: segments*2 vertices (top ring + bottom ring), segments*2 triangles
            int capVerts = segments + 1;
            int wallVerts = segments * 2;
            int totalVerts = capVerts * 2 + wallVerts;
            int totalTris = segments * 2 + segments * 2; // 2 caps + wall

            var mesh = new MeshData
            {
                ObjectId = objectId,
                Vertices = new List<float>(totalVerts * 3),
                Normals = new List<float>(totalVerts * 3),
                Indices = new List<int>(totalTris * 3)
            };

            float cx = (float)center.X;
            float cy = (float)center.Y;
            float cz = (float)center.Z;
            float r = (float)radius;
            float hh = (float)halfHeight;

            // 사전 계산: 각도별 cos/sin
            float[] cosArr = new float[segments];
            float[] sinArr = new float[segments];
            for (int i = 0; i < segments; i++)
            {
                double angle = 2.0 * Math.PI * i / segments;
                cosArr[i] = (float)Math.Cos(angle);
                sinArr[i] = (float)Math.Sin(angle);
            }

            // 축에 따른 좌표 매핑 함수
            // axis=0(X): 축방향=X, 원주면=Y,Z
            // axis=1(Y): 축방향=Y, 원주면=X,Z
            // axis=2(Z): 축방향=Z, 원주면=X,Y

            // ── Top Cap (positive axis direction) ──
            int topCenterIdx = mesh.Vertices.Count / 3;
            AddVertex(mesh, cx, cy, cz, hh, axisIndex, 0, 0, r, true);  // center

            for (int i = 0; i < segments; i++)
            {
                AddVertex(mesh, cx, cy, cz, hh, axisIndex, cosArr[i], sinArr[i], r, true);
            }

            // Top cap triangles (fan)
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                mesh.Indices.Add(topCenterIdx);
                mesh.Indices.Add(topCenterIdx + 1 + i);
                mesh.Indices.Add(topCenterIdx + 1 + next);
            }

            // ── Bottom Cap (negative axis direction) ──
            int botCenterIdx = mesh.Vertices.Count / 3;
            AddVertex(mesh, cx, cy, cz, -hh, axisIndex, 0, 0, r, false);  // center

            for (int i = 0; i < segments; i++)
            {
                AddVertex(mesh, cx, cy, cz, -hh, axisIndex, cosArr[i], sinArr[i], r, false);
            }

            // Bottom cap triangles (fan, reversed winding)
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                mesh.Indices.Add(botCenterIdx);
                mesh.Indices.Add(botCenterIdx + 1 + next);
                mesh.Indices.Add(botCenterIdx + 1 + i);
            }

            // ── Side Wall (quad strip) ──
            int wallBaseIdx = mesh.Vertices.Count / 3;

            for (int i = 0; i < segments; i++)
            {
                // Top ring vertex (with radial normal)
                AddWallVertex(mesh, cx, cy, cz, hh, axisIndex, cosArr[i], sinArr[i], r);
                // Bottom ring vertex (with radial normal)
                AddWallVertex(mesh, cx, cy, cz, -hh, axisIndex, cosArr[i], sinArr[i], r);
            }

            // Wall triangles (2 per quad segment)
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int topA = wallBaseIdx + i * 2;
                int botA = wallBaseIdx + i * 2 + 1;
                int topB = wallBaseIdx + next * 2;
                int botB = wallBaseIdx + next * 2 + 1;

                // Triangle 1: topA, botA, topB
                mesh.Indices.Add(topA);
                mesh.Indices.Add(botA);
                mesh.Indices.Add(topB);

                // Triangle 2: topB, botA, botB
                mesh.Indices.Add(topB);
                mesh.Indices.Add(botA);
                mesh.Indices.Add(botB);
            }

            mesh.VertexCount = mesh.Vertices.Count / 3;
            mesh.TriangleCount = mesh.Indices.Count / 3;

            return mesh;
        }

        #endregion

        #region Shape Detection

        /// <summary>
        /// BBox 종횡비 + DisplayName으로 형상 유형 감지
        /// </summary>
        private static ShapeHint DetectShape(BBox3D bbox, string displayName)
        {
            // DisplayName 힌트 우선
            if (IsFlangeType(displayName)) return ShapeHint.Disc;
            if (IsPlateType(displayName)) return ShapeHint.Plate;

            // BBox 종횡비 분석
            double dx = bbox.Max.X - bbox.Min.X;
            double dy = bbox.Max.Y - bbox.Min.Y;
            double dz = bbox.Max.Z - bbox.Min.Z;

            // 3차원을 크기순 정렬
            double[] dims = { dx, dy, dz };
            Array.Sort(dims); // small, mid, large

            double small = dims[0];
            double mid = dims[1];
            double large = dims[2];

            if (large < 1e-9) return ShapeHint.Box; // degenerate

            double ratioMidLarge = mid / large;
            double ratioSmallMid = (mid > 1e-9) ? small / mid : 0;

            // 두 축이 비슷한 크기 → 원형 단면 가능
            if (ratioMidLarge > 0.6)
            {
                if (ratioSmallMid < 0.5)
                    return ShapeHint.Disc;     // 얇은 원판 (Flange류)
                else
                    return ShapeHint.Cylinder;  // 두꺼운 원기둥
            }

            // 하나가 매우 얇음 → 판
            if (ratioSmallMid < 0.3)
                return ShapeHint.Plate;

            return ShapeHint.Box;
        }

        /// <summary>
        /// BBox에서 원기둥 파라미터 추출
        /// 가장 짧은 축 = 높이(축) 방향, 나머지 두 축의 평균 = 반지름
        /// </summary>
        private static CylindricalFit AnalyzeCylindricalFit(BBox3D bbox)
        {
            double dx = bbox.Max.X - bbox.Min.X;
            double dy = bbox.Max.Y - bbox.Min.Y;
            double dz = bbox.Max.Z - bbox.Min.Z;

            double[] dims = { dx, dy, dz };
            int[] order = { 0, 1, 2 };

            // 크기순 정렬 (bubble sort for 3 elements)
            for (int i = 0; i < 2; i++)
            {
                for (int j = i + 1; j < 3; j++)
                {
                    if (dims[order[i]] > dims[order[j]])
                    {
                        int tmp = order[i];
                        order[i] = order[j];
                        order[j] = tmp;
                    }
                }
            }

            // 가장 짧은 축 = 높이 방향 (cylinder axis)
            int axisIndex = order[0];
            double height = dims[axisIndex];

            // 나머지 두 축의 평균 = 직경 → 반지름
            double wide1 = dims[order[1]];
            double wide2 = dims[order[2]];
            double radius = (wide1 + wide2) / 4.0; // (w1+w2)/2 = 직경평균, /2 = 반지름

            return new CylindricalFit
            {
                AxisIndex = axisIndex,
                Radius = radius,
                Height = height
            };
        }

        #endregion

        #region DisplayName Helpers

        private static bool IsFlangeType(string name)
        {
            return name != null && name.StartsWith("Flange", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPlateType(string name)
        {
            return name != null && name.IndexOf("PLATE", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsAspectsType(string name)
        {
            return name != null && name.StartsWith("Aspects", StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Vertex Helpers

        /// <summary>
        /// Cap vertex 추가 (center 또는 ring)
        /// </summary>
        /// <param name="mesh">대상 MeshData</param>
        /// <param name="cx">중심 X (WCS)</param>
        /// <param name="cy">중심 Y (WCS)</param>
        /// <param name="cz">중심 Z (WCS)</param>
        /// <param name="axisOffset">축 방향 오프셋 (+halfHeight 또는 -halfHeight)</param>
        /// <param name="axisIndex">축 인덱스 (0=X, 1=Y, 2=Z)</param>
        /// <param name="cosA">cos(angle), center일 때 0</param>
        /// <param name="sinA">sin(angle), center일 때 0</param>
        /// <param name="radius">반지름</param>
        /// <param name="isTop">상면(true) 또는 하면(false)</param>
        private static void AddVertex(MeshData mesh,
            float cx, float cy, float cz,
            float axisOffset, int axisIndex,
            float cosA, float sinA, float radius,
            bool isTop)
        {
            float u = cosA * radius;
            float v = sinA * radius;

            float px, py, pz;
            float nx, ny, nz;

            switch (axisIndex)
            {
                case 0: // X axis
                    px = cx + axisOffset;
                    py = cy + u;
                    pz = cz + v;
                    nx = isTop ? 1f : -1f; ny = 0f; nz = 0f;
                    break;
                case 1: // Y axis
                    px = cx + u;
                    py = cy + axisOffset;
                    pz = cz + v;
                    nx = 0f; ny = isTop ? 1f : -1f; nz = 0f;
                    break;
                default: // Z axis (2)
                    px = cx + u;
                    py = cy + v;
                    pz = cz + axisOffset;
                    nx = 0f; ny = 0f; nz = isTop ? 1f : -1f;
                    break;
            }

            mesh.Vertices.Add(px);
            mesh.Vertices.Add(py);
            mesh.Vertices.Add(pz);
            mesh.Normals.Add(nx);
            mesh.Normals.Add(ny);
            mesh.Normals.Add(nz);
        }

        /// <summary>
        /// Side wall vertex 추가 (radial normal)
        /// </summary>
        private static void AddWallVertex(MeshData mesh,
            float cx, float cy, float cz,
            float axisOffset, int axisIndex,
            float cosA, float sinA, float radius)
        {
            float u = cosA * radius;
            float v = sinA * radius;

            float px, py, pz;
            float nx, ny, nz;

            switch (axisIndex)
            {
                case 0: // X axis
                    px = cx + axisOffset;
                    py = cy + u;
                    pz = cz + v;
                    nx = 0f; ny = cosA; nz = sinA;
                    break;
                case 1: // Y axis
                    px = cx + u;
                    py = cy + axisOffset;
                    pz = cz + v;
                    nx = cosA; ny = 0f; nz = sinA;
                    break;
                default: // Z axis (2)
                    px = cx + u;
                    py = cy + v;
                    pz = cz + axisOffset;
                    nx = cosA; ny = sinA; nz = 0f;
                    break;
            }

            mesh.Vertices.Add(px);
            mesh.Vertices.Add(py);
            mesh.Vertices.Add(pz);
            mesh.Normals.Add(nx);
            mesh.Normals.Add(ny);
            mesh.Normals.Add(nz);
        }

        #endregion

        #region Internal Types

        internal enum ShapeHint
        {
            Cylinder,   // 원기둥 (두꺼운)
            Disc,       // 원판/디스크 (얇은, Flange류)
            Plate,      // 직사각형 판
            Box         // 직방체 (기본)
        }

        private struct CylindricalFit
        {
            public int AxisIndex;    // 0=X, 1=Y, 2=Z
            public double Radius;    // 평균 반지름
            public double Height;    // 축 방향 높이
        }

        #endregion
    }
}

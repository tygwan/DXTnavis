using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using Autodesk.Navisworks.Api.Interop.ComApi;
using DXTnavis.Models.Geometry;

namespace DXTnavis.Services.Geometry
{
    /// <summary>
    /// Navisworks COM API를 통해 Mesh 데이터(vertices, triangles)를 추출하는 서비스
    /// Phase 15.3: Optional Mesh Export
    ///
    /// 참고: COM API InwOaFragment3.GenerateSimplePrimitives() 사용
    /// </summary>
    public class MeshExtractor : IDisposable
    {
        #region Events

        /// <summary>
        /// 진행률 변경 이벤트 (0-100%)
        /// </summary>
        public event EventHandler<int> ProgressChanged;

        /// <summary>
        /// 상태 메시지 이벤트
        /// </summary>
        public event EventHandler<string> StatusChanged;

        #endregion

        #region Private Fields

        private bool _disposed = false;

        #endregion

        #region Public Methods

        /// <summary>
        /// 단일 ModelItem에서 Mesh 데이터 추출
        /// </summary>
        /// <param name="item">Navisworks ModelItem</param>
        /// <param name="objectId">객체 고유 ID (파일명용)</param>
        /// <returns>MeshData (실패 시 null)</returns>
        public MeshData ExtractMesh(ModelItem item, Guid objectId)
        {
            if (item == null || objectId == Guid.Empty)
                return null;

            try
            {
                // COM API 상태 가져오기
                var comState = ComApiBridge.State;
                if (comState == null)
                {
                    Debug.WriteLine("[MeshExtractor] COM API State is null");
                    return null;
                }

                // ModelItem을 COM Path로 변환
                var comPath = ComApiBridge.ToInwOaPath(item);
                if (comPath == null)
                {
                    Debug.WriteLine($"[MeshExtractor] Failed to convert to COM path: {item.DisplayName}");
                    return null;
                }

                // Geometry 컬렉션 가져오기
                var meshData = new MeshData
                {
                    ObjectId = objectId,
                    Vertices = new List<float>(),
                    Normals = new List<float>(),
                    Indices = new List<int>()
                };

                // Fragment Callback을 통해 Mesh 추출
                var callback = new MeshCallbackSink(meshData);

                foreach (InwOaFragment3 fragment in comPath.Fragments())
                {
                    if (fragment == null) continue;

                    try
                    {
                        // GenerateSimplePrimitives로 삼각형 데이터 추출
                        fragment.GenerateSimplePrimitives(
                            nwEVertexProperty.eNORMAL,  // 노말 포함
                            callback);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MeshExtractor] Fragment error: {ex.Message}");
                    }
                }

                // 최소 삼각형이 있는지 확인
                if (meshData.Vertices.Count < 9 || meshData.Indices.Count < 3)
                {
                    Debug.WriteLine($"[MeshExtractor] No valid mesh data for: {item.DisplayName}");
                    return null;
                }

                meshData.VertexCount = meshData.Vertices.Count / 3;
                meshData.TriangleCount = meshData.Indices.Count / 3;

                return meshData;
            }
            catch (COMException comEx)
            {
                Debug.WriteLine($"[MeshExtractor] COM Error: {comEx.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MeshExtractor] Error extracting mesh: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 여러 ModelItem에서 Mesh 데이터 일괄 추출
        /// </summary>
        /// <param name="items">ModelItem과 ObjectId 쌍</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>ObjectId → MeshData 딕셔너리</returns>
        public Dictionary<Guid, MeshData> ExtractMeshes(
            IEnumerable<(ModelItem item, Guid objectId)> items,
            CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<Guid, MeshData>();
            var itemList = items.ToList();
            int total = itemList.Count;
            int processed = 0;
            int successful = 0;

            OnStatusChanged($"Mesh 추출 시작: {total:N0}개 객체");
            var sw = Stopwatch.StartNew();

            foreach (var (item, objectId) in itemList)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    OnStatusChanged("Mesh 추출 취소됨");
                    break;
                }

                var meshData = ExtractMesh(item, objectId);
                if (meshData != null)
                {
                    result[objectId] = meshData;
                    successful++;
                }

                processed++;

                // 50개마다 진행률 보고
                if (processed % 50 == 0 || processed == total)
                {
                    int percentage = (int)(100.0 * processed / total);
                    OnProgressChanged(percentage);
                    OnStatusChanged($"Mesh 추출 중: {processed:N0}/{total:N0} ({successful:N0}개 성공)");
                }
            }

            sw.Stop();
            OnProgressChanged(100);
            OnStatusChanged($"Mesh 추출 완료: {successful:N0}개 ({sw.Elapsed.TotalSeconds:F1}초)");

            return result;
        }

        /// <summary>
        /// MeshData를 GLB 파일로 저장
        /// </summary>
        /// <param name="meshData">Mesh 데이터</param>
        /// <param name="outputPath">출력 파일 경로 (.glb)</param>
        /// <returns>저장 성공 여부</returns>
        public bool SaveToGlb(MeshData meshData, string outputPath)
        {
            if (meshData == null || string.IsNullOrEmpty(outputPath))
                return false;

            try
            {
                // GLB 파일 형식으로 저장 (간단한 구현)
                // 실제로는 glTF-CSharp-Loader 또는 SharpGLTF 라이브러리 사용 권장
                using (var fs = new FileStream(outputPath, FileMode.Create))
                using (var bw = new BinaryWriter(fs))
                {
                    WriteGlbHeader(bw, meshData);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MeshExtractor] GLB save error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// MeshData를 OBJ 파일로 저장 (간단한 대안 포맷)
        /// </summary>
        /// <param name="meshData">Mesh 데이터</param>
        /// <param name="outputPath">출력 파일 경로 (.obj)</param>
        /// <returns>저장 성공 여부</returns>
        public bool SaveToObj(MeshData meshData, string outputPath)
        {
            if (meshData == null || string.IsNullOrEmpty(outputPath))
                return false;

            try
            {
                using (var sw = new StreamWriter(outputPath))
                {
                    sw.WriteLine($"# DXTnavis Mesh Export");
                    sw.WriteLine($"# ObjectId: {meshData.ObjectId}");
                    sw.WriteLine($"# Vertices: {meshData.VertexCount}");
                    sw.WriteLine($"# Triangles: {meshData.TriangleCount}");
                    sw.WriteLine();

                    // Vertices
                    for (int i = 0; i < meshData.Vertices.Count; i += 3)
                    {
                        sw.WriteLine($"v {meshData.Vertices[i]:F6} {meshData.Vertices[i + 1]:F6} {meshData.Vertices[i + 2]:F6}");
                    }

                    sw.WriteLine();

                    // Normals
                    if (meshData.Normals.Count > 0)
                    {
                        for (int i = 0; i < meshData.Normals.Count; i += 3)
                        {
                            sw.WriteLine($"vn {meshData.Normals[i]:F6} {meshData.Normals[i + 1]:F6} {meshData.Normals[i + 2]:F6}");
                        }
                        sw.WriteLine();
                    }

                    // Faces (1-indexed)
                    for (int i = 0; i < meshData.Indices.Count; i += 3)
                    {
                        int i1 = meshData.Indices[i] + 1;
                        int i2 = meshData.Indices[i + 1] + 1;
                        int i3 = meshData.Indices[i + 2] + 1;

                        if (meshData.Normals.Count > 0)
                        {
                            sw.WriteLine($"f {i1}//{i1} {i2}//{i2} {i3}//{i3}");
                        }
                        else
                        {
                            sw.WriteLine($"f {i1} {i2} {i3}");
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MeshExtractor] OBJ save error: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 간단한 GLB 헤더 작성 (최소 구현)
        /// </summary>
        private void WriteGlbHeader(BinaryWriter bw, MeshData meshData)
        {
            // GLB Magic: glTF
            bw.Write(0x46546C67); // "glTF"

            // Version 2
            bw.Write((uint)2);

            // 전체 길이 (나중에 업데이트)
            long lengthPos = bw.BaseStream.Position;
            bw.Write((uint)0);

            // JSON chunk
            string json = CreateMinimalGltfJson(meshData);
            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);

            // Padding to 4-byte alignment
            int jsonPadding = (4 - (jsonBytes.Length % 4)) % 4;

            bw.Write((uint)(jsonBytes.Length + jsonPadding));
            bw.Write(0x4E4F534A); // "JSON"
            bw.Write(jsonBytes);
            for (int i = 0; i < jsonPadding; i++) bw.Write((byte)0x20);

            // Binary chunk (vertices + indices)
            var binData = CreateBinaryBuffer(meshData);
            int binPadding = (4 - (binData.Length % 4)) % 4;

            bw.Write((uint)(binData.Length + binPadding));
            bw.Write(0x004E4942); // "BIN\0"
            bw.Write(binData);
            for (int i = 0; i < binPadding; i++) bw.Write((byte)0x00);

            // Update total length
            long endPos = bw.BaseStream.Position;
            bw.BaseStream.Position = lengthPos;
            bw.Write((uint)endPos);
        }

        /// <summary>
        /// 최소한의 glTF JSON 생성
        /// </summary>
        private string CreateMinimalGltfJson(MeshData meshData)
        {
            int vertexByteLength = meshData.Vertices.Count * sizeof(float);
            int indexByteLength = meshData.Indices.Count * sizeof(int);

            // glTF spec: POSITION accessor MUST have min/max
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;

            for (int i = 0; i < meshData.Vertices.Count; i += 3)
            {
                float x = meshData.Vertices[i];
                float y = meshData.Vertices[i + 1];
                float z = meshData.Vertices[i + 2];
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
                if (z < minZ) minZ = z; if (z > maxZ) maxZ = z;
            }

            // G9 format: float round-trip precision to avoid ACCESSOR_MIN_MISMATCH
            string sMinX = minX.ToString("G9", System.Globalization.CultureInfo.InvariantCulture);
            string sMinY = minY.ToString("G9", System.Globalization.CultureInfo.InvariantCulture);
            string sMinZ = minZ.ToString("G9", System.Globalization.CultureInfo.InvariantCulture);
            string sMaxX = maxX.ToString("G9", System.Globalization.CultureInfo.InvariantCulture);
            string sMaxY = maxY.ToString("G9", System.Globalization.CultureInfo.InvariantCulture);
            string sMaxZ = maxZ.ToString("G9", System.Globalization.CultureInfo.InvariantCulture);

            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
@"{{
  ""asset"": {{ ""version"": ""2.0"", ""generator"": ""DXTnavis Phase 18"" }},
  ""buffers"": [{{ ""byteLength"": {0} }}],
  ""bufferViews"": [
    {{ ""buffer"": 0, ""byteOffset"": 0, ""byteLength"": {1}, ""target"": 34962 }},
    {{ ""buffer"": 0, ""byteOffset"": {1}, ""byteLength"": {2}, ""target"": 34963 }}
  ],
  ""accessors"": [
    {{ ""bufferView"": 0, ""componentType"": 5126, ""count"": {3}, ""type"": ""VEC3"", ""min"": [{4}, {5}, {6}], ""max"": [{7}, {8}, {9}] }},
    {{ ""bufferView"": 1, ""componentType"": 5125, ""count"": {10}, ""type"": ""SCALAR"" }}
  ],
  ""meshes"": [{{ ""primitives"": [{{ ""attributes"": {{ ""POSITION"": 0 }}, ""indices"": 1 }}] }}],
  ""nodes"": [{{ ""mesh"": 0 }}],
  ""scenes"": [{{ ""nodes"": [0] }}],
  ""scene"": 0
}}",
                vertexByteLength + indexByteLength,
                vertexByteLength,
                indexByteLength,
                meshData.VertexCount,
                sMinX, sMinY, sMinZ,
                sMaxX, sMaxY, sMaxZ,
                meshData.Indices.Count);
        }

        /// <summary>
        /// 바이너리 버퍼 생성
        /// </summary>
        private byte[] CreateBinaryBuffer(MeshData meshData)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                // Vertices
                foreach (var v in meshData.Vertices)
                    bw.Write(v);

                // Indices
                foreach (var i in meshData.Indices)
                    bw.Write(i);

                return ms.ToArray();
            }
        }

        #endregion

        #region Event Helpers

        private void OnProgressChanged(int percentage)
        {
            ProgressChanged?.Invoke(this, percentage);
        }

        private void OnStatusChanged(string message)
        {
            StatusChanged?.Invoke(this, message);
            Debug.WriteLine($"[MeshExtractor] {message}");
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }

        #endregion
    }

    /// <summary>
    /// Mesh 데이터 컨테이너
    /// </summary>
    public class MeshData
    {
        public Guid ObjectId { get; set; }
        public List<float> Vertices { get; set; }
        public List<float> Normals { get; set; }
        public List<int> Indices { get; set; }
        public int VertexCount { get; set; }
        public int TriangleCount { get; set; }
    }

    /// <summary>
    /// COM API Mesh Callback Sink
    /// GenerateSimplePrimitives() 결과를 수집
    /// </summary>
    internal class MeshCallbackSink : InwSimplePrimitivesCB
    {
        private readonly MeshData _meshData;
        private int _currentVertexIndex = 0;

        public MeshCallbackSink(MeshData meshData)
        {
            _meshData = meshData;
        }

        public void Line(InwSimpleVertex v1, InwSimpleVertex v2)
        {
            // 라인은 무시 (삼각형만 처리)
        }

        public void Point(InwSimpleVertex v1)
        {
            // 포인트는 무시
        }

        public void SnapPoint(InwSimpleVertex v1)
        {
            // 스냅 포인트는 무시
        }

        public void Triangle(InwSimpleVertex v1, InwSimpleVertex v2, InwSimpleVertex v3)
        {
            // Vertex 1
            AddVertex(v1);
            int idx1 = _currentVertexIndex++;

            // Vertex 2
            AddVertex(v2);
            int idx2 = _currentVertexIndex++;

            // Vertex 3
            AddVertex(v3);
            int idx3 = _currentVertexIndex++;

            // Triangle indices
            _meshData.Indices.Add(idx1);
            _meshData.Indices.Add(idx2);
            _meshData.Indices.Add(idx3);
        }

        private void AddVertex(InwSimpleVertex v)
        {
            // Navisworks COM API: coord/normal return 1-based SAFEARRAY (Single[*])
            // Direct cast to float[] fails with InvalidCastException
            // Fix: use reflection late-binding to bypass PIA typed marshaling
            var coord = GetComArray(v, "coord");
            if (coord == null || coord.Length < 3) return;

            _meshData.Vertices.Add(coord[0]);
            _meshData.Vertices.Add(coord[1]);
            _meshData.Vertices.Add(coord[2]);

            // Normal (if available)
            var normal = GetComArray(v, "normal");
            if (normal != null && normal.Length >= 3)
            {
                _meshData.Normals.Add(normal[0]);
                _meshData.Normals.Add(normal[1]);
                _meshData.Normals.Add(normal[2]);
            }
        }

        /// <summary>
        /// COM SAFEARRAY (Single[*]) → float[] 변환
        /// PIA typed property가 Single[*]→Single[] 캐스트 실패하므로
        /// InvokeMember로 late-bound 접근하여 Array 기반으로 읽음
        /// </summary>
        private static float[] GetComArray(object comObject, string propertyName)
        {
            try
            {
                var raw = comObject.GetType().InvokeMember(
                    propertyName,
                    System.Reflection.BindingFlags.GetProperty,
                    null, comObject, null);

                if (raw == null) return null;

                var arr = raw as Array;
                if (arr == null) return null;

                int len = arr.Length;
                var result = new float[len];
                int lb = arr.GetLowerBound(0);
                for (int i = 0; i < len; i++)
                {
                    result[i] = Convert.ToSingle(arr.GetValue(lb + i));
                }
                return result;
            }
            catch
            {
                return null;
            }
        }
    }
}

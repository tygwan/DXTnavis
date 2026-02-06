using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DXTnavis.Models.Geometry;

namespace DXTnavis.Services.Geometry
{
    /// <summary>
    /// Geometry 데이터를 manifest.json 파일로 내보내는 서비스
    /// Phase 15: Geometry Export System
    /// </summary>
    public class GeometryFileWriter
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

        #region Public Methods

        /// <summary>
        /// GeometryRecord 컬렉션을 manifest.json 파일로 저장
        /// </summary>
        /// <param name="records">GeometryRecord 딕셔너리 (ObjectId → Record)</param>
        /// <param name="exportPath">출력 폴더 경로</param>
        /// <param name="projectName">프로젝트 이름 (manifest 메타데이터용)</param>
        /// <returns>생성된 manifest.json 파일 경로</returns>
        public string WriteManifest(
            Dictionary<Guid, GeometryRecord> records,
            string exportPath,
            string projectName = "DXTnavis Export")
        {
            if (records == null || records.Count == 0)
            {
                OnStatusChanged("저장할 데이터가 없습니다.");
                return null;
            }

            try
            {
                // 출력 폴더 생성
                if (!Directory.Exists(exportPath))
                {
                    Directory.CreateDirectory(exportPath);
                }

                var manifestPath = Path.Combine(exportPath, "manifest.json");
                OnStatusChanged($"manifest.json 생성 중: {records.Count:N0}개 객체");

                var sw = Stopwatch.StartNew();

                using (var writer = new StreamWriter(manifestPath, false, Encoding.UTF8))
                {
                    WriteManifestContent(writer, records, projectName);
                }

                sw.Stop();
                OnProgressChanged(100);
                OnStatusChanged($"manifest.json 저장 완료: {records.Count:N0}개 객체 ({sw.Elapsed.TotalSeconds:F1}초)");

                return manifestPath;
            }
            catch (Exception ex)
            {
                OnStatusChanged($"manifest.json 저장 실패: {ex.Message}");
                Debug.WriteLine($"[GeometryFileWriter] Error: {ex}");
                return null;
            }
        }

        /// <summary>
        /// GeometryRecord 컬렉션을 CSV 파일로 저장 (대안 포맷)
        /// </summary>
        /// <param name="records">GeometryRecord 딕셔너리</param>
        /// <param name="csvPath">CSV 파일 경로</param>
        /// <returns>저장 성공 여부</returns>
        public bool WriteCsv(Dictionary<Guid, GeometryRecord> records, string csvPath)
        {
            if (records == null || records.Count == 0)
            {
                OnStatusChanged("저장할 데이터가 없습니다.");
                return false;
            }

            try
            {
                OnStatusChanged($"Geometry CSV 생성 중: {records.Count:N0}개 객체");
                var sw = Stopwatch.StartNew();

                using (var writer = new StreamWriter(csvPath, false, Encoding.UTF8))
                {
                    // CSV 헤더
                    writer.WriteLine("ObjectId,DisplayName,Category,MinX,MinY,MinZ,MaxX,MaxY,MaxZ,CentroidX,CentroidY,CentroidZ,Volume,HasMesh,MeshUri");

                    int processed = 0;
                    int total = records.Count;

                    foreach (var kvp in records)
                    {
                        var r = kvp.Value;
                        var line = string.Format(CultureInfo.InvariantCulture,
                            "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14}",
                            r.ObjectId,
                            EscapeCsvField(r.DisplayName ?? ""),
                            EscapeCsvField(r.Category ?? ""),
                            r.BBox?.Min.X ?? 0,
                            r.BBox?.Min.Y ?? 0,
                            r.BBox?.Min.Z ?? 0,
                            r.BBox?.Max.X ?? 0,
                            r.BBox?.Max.Y ?? 0,
                            r.BBox?.Max.Z ?? 0,
                            r.Centroid.X,
                            r.Centroid.Y,
                            r.Centroid.Z,
                            r.GetVolume(),
                            r.HasMesh,
                            EscapeCsvField(r.MeshUri ?? ""));

                        writer.WriteLine(line);

                        processed++;
                        if (processed % 1000 == 0 || processed == total)
                        {
                            int percentage = (int)(100.0 * processed / total);
                            OnProgressChanged(percentage);
                        }
                    }
                }

                sw.Stop();
                OnProgressChanged(100);
                OnStatusChanged($"Geometry CSV 저장 완료: {records.Count:N0}개 객체 ({sw.Elapsed.TotalSeconds:F1}초)");
                return true;
            }
            catch (Exception ex)
            {
                OnStatusChanged($"CSV 저장 실패: {ex.Message}");
                Debug.WriteLine($"[GeometryFileWriter] CSV Error: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Export 폴더 구조 생성 및 검증
        /// </summary>
        /// <param name="basePath">기본 export 폴더 경로</param>
        /// <returns>생성된 폴더 구조 정보</returns>
        public ExportFolderStructure CreateExportStructure(string basePath)
        {
            try
            {
                var structure = new ExportFolderStructure
                {
                    BasePath = basePath,
                    MeshFolder = Path.Combine(basePath, "mesh"),
                    ManifestPath = Path.Combine(basePath, "manifest.json"),
                    CsvPath = Path.Combine(basePath, "geometry.csv")
                };

                // 폴더 생성
                if (!Directory.Exists(basePath))
                    Directory.CreateDirectory(basePath);

                if (!Directory.Exists(structure.MeshFolder))
                    Directory.CreateDirectory(structure.MeshFolder);

                structure.IsValid = true;
                OnStatusChanged($"Export 폴더 구조 생성 완료: {basePath}");

                return structure;
            }
            catch (Exception ex)
            {
                OnStatusChanged($"폴더 구조 생성 실패: {ex.Message}");
                return new ExportFolderStructure { IsValid = false };
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// manifest.json 내용 작성
        /// </summary>
        private void WriteManifestContent(
            StreamWriter writer,
            Dictionary<Guid, GeometryRecord> records,
            string projectName)
        {
            writer.WriteLine("{");

            // 메타데이터
            writer.WriteLine("  \"metadata\": {");
            writer.WriteLine($"    \"version\": \"1.0.0\",");
            writer.WriteLine($"    \"generator\": \"DXTnavis v1.4.0\",");
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "    \"exportDate\": \"{0}\",", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")));
            writer.WriteLine($"    \"projectName\": \"{EscapeJsonString(projectName)}\",");
            writer.WriteLine($"    \"objectCount\": {records.Count},");

            // 전체 BoundingBox 계산
            var globalBBox = CalculateGlobalBoundingBox(records.Values);
            if (globalBBox != null)
            {
                writer.WriteLine("    \"globalBoundingBox\": {");
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "      \"min\": {{ \"x\": {0}, \"y\": {1}, \"z\": {2} }},",
                    globalBBox.Min.X, globalBBox.Min.Y, globalBBox.Min.Z));
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "      \"max\": {{ \"x\": {0}, \"y\": {1}, \"z\": {2} }}",
                    globalBBox.Max.X, globalBBox.Max.Y, globalBBox.Max.Z));
                writer.WriteLine("    },");
            }

            // Mesh 통계
            int meshCount = records.Values.Count(r => r.HasMesh);
            writer.WriteLine($"    \"meshCount\": {meshCount}");
            writer.WriteLine("  },");

            // 객체 배열
            writer.WriteLine("  \"objects\": [");

            int processed = 0;
            int total = records.Count;
            var recordList = records.Values.ToList();

            for (int i = 0; i < recordList.Count; i++)
            {
                var r = recordList[i];
                WriteObjectEntry(writer, r, i < recordList.Count - 1);

                processed++;
                if (processed % 500 == 0 || processed == total)
                {
                    int percentage = (int)(90.0 * processed / total); // 90%까지 객체 작성
                    OnProgressChanged(percentage);
                }
            }

            writer.WriteLine("  ]");
            writer.WriteLine("}");
        }

        /// <summary>
        /// 단일 객체 항목 작성
        /// </summary>
        private void WriteObjectEntry(StreamWriter writer, GeometryRecord r, bool hasMore)
        {
            writer.WriteLine("    {");

            // ObjectId
            writer.WriteLine($"      \"objectId\": \"{r.ObjectId}\",");

            // DisplayName (선택적)
            if (!string.IsNullOrEmpty(r.DisplayName))
            {
                writer.WriteLine($"      \"displayName\": \"{EscapeJsonString(r.DisplayName)}\",");
            }

            // Category (선택적)
            if (!string.IsNullOrEmpty(r.Category))
            {
                writer.WriteLine($"      \"category\": \"{EscapeJsonString(r.Category)}\",");
            }

            // BoundingBox
            writer.WriteLine("      \"bbox\": {");
            if (r.BBox != null)
            {
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "        \"min\": {{ \"x\": {0}, \"y\": {1}, \"z\": {2} }},",
                    r.BBox.Min.X, r.BBox.Min.Y, r.BBox.Min.Z));
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "        \"max\": {{ \"x\": {0}, \"y\": {1}, \"z\": {2} }}",
                    r.BBox.Max.X, r.BBox.Max.Y, r.BBox.Max.Z));
            }
            writer.WriteLine("      },");

            // Centroid
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "      \"centroid\": {{ \"x\": {0}, \"y\": {1}, \"z\": {2} }},",
                r.Centroid.X, r.Centroid.Y, r.Centroid.Z));

            // HasMesh
            writer.WriteLine($"      \"hasMesh\": {r.HasMesh.ToString().ToLowerInvariant()},");

            // MeshUri
            if (string.IsNullOrEmpty(r.MeshUri))
            {
                writer.WriteLine("      \"meshUri\": null");
            }
            else
            {
                writer.WriteLine($"      \"meshUri\": \"{EscapeJsonString(r.MeshUri)}\"");
            }

            writer.WriteLine(hasMore ? "    }," : "    }");
        }

        /// <summary>
        /// 전체 객체의 Global BoundingBox 계산
        /// </summary>
        private BBox3D CalculateGlobalBoundingBox(IEnumerable<GeometryRecord> records)
        {
            BBox3D global = null;

            foreach (var r in records)
            {
                if (r.BBox == null) continue;

                if (global == null)
                {
                    global = new BBox3D(
                        new Point3D(r.BBox.Min.X, r.BBox.Min.Y, r.BBox.Min.Z),
                        new Point3D(r.BBox.Max.X, r.BBox.Max.Y, r.BBox.Max.Z));
                }
                else
                {
                    global = global.Union(r.BBox);
                }
            }

            return global;
        }

        /// <summary>
        /// JSON 문자열 이스케이프
        /// </summary>
        private static string EscapeJsonString(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;

            return str
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        /// <summary>
        /// CSV 필드 이스케이프
        /// </summary>
        private static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";

            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
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
            Debug.WriteLine($"[GeometryFileWriter] {message}");
        }

        #endregion
    }

    /// <summary>
    /// Export 폴더 구조 정보
    /// </summary>
    public class ExportFolderStructure
    {
        /// <summary>기본 export 폴더 경로</summary>
        public string BasePath { get; set; }

        /// <summary>mesh 폴더 경로</summary>
        public string MeshFolder { get; set; }

        /// <summary>manifest.json 파일 경로</summary>
        public string ManifestPath { get; set; }

        /// <summary>geometry.csv 파일 경로</summary>
        public string CsvPath { get; set; }

        /// <summary>구조 유효성</summary>
        public bool IsValid { get; set; }
    }
}

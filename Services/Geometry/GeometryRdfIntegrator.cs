using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using DXTnavis.Models.Geometry;

namespace DXTnavis.Services.Geometry
{
    /// <summary>
    /// Geometry 데이터를 RDF/TTL 형식으로 통합하는 서비스
    /// Phase 15.5: RDF Geometry Integration
    ///
    /// BSO (BIM-Schedule Ontology) 네임스페이스 사용:
    /// - bso:hasBoundingBox, bso:hasCentroid, bso:hasMesh
    /// - geo:asWKT (GeoSPARQL for spatial data)
    /// </summary>
    public class GeometryRdfIntegrator
    {
        #region Constants

        // RDF Namespaces
        private const string BSO_PREFIX = "bso";
        private const string BSO_NAMESPACE = "http://example.org/bso#";

        private const string GEO_PREFIX = "geo";
        private const string GEO_NAMESPACE = "http://www.opengis.net/ont/geosparql#";

        private const string XSD_PREFIX = "xsd";
        private const string XSD_NAMESPACE = "http://www.w3.org/2001/XMLSchema#";

        private const string RDF_PREFIX = "rdf";
        private const string RDF_NAMESPACE = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

        private const string RDFS_PREFIX = "rdfs";
        private const string RDFS_NAMESPACE = "http://www.w3.org/2000/01/rdf-schema#";

        #endregion

        #region Events

        /// <summary>
        /// 진행률 변경 이벤트
        /// </summary>
        public event EventHandler<int> ProgressChanged;

        /// <summary>
        /// 상태 메시지 이벤트
        /// </summary>
        public event EventHandler<string> StatusChanged;

        #endregion

        #region Public Methods

        /// <summary>
        /// GeometryRecord 컬렉션을 TTL (Turtle) 형식으로 변환
        /// </summary>
        /// <param name="records">Geometry 레코드 딕셔너리</param>
        /// <param name="outputPath">출력 TTL 파일 경로</param>
        /// <returns>생성 성공 여부</returns>
        public bool WriteGeometryTtl(Dictionary<Guid, GeometryRecord> records, string outputPath)
        {
            if (records == null || records.Count == 0)
            {
                OnStatusChanged("변환할 Geometry 데이터가 없습니다.");
                return false;
            }

            try
            {
                OnStatusChanged($"RDF 변환 시작: {records.Count:N0}개 객체");
                var sw = Stopwatch.StartNew();

                using (var writer = new StreamWriter(outputPath, false, Encoding.UTF8))
                {
                    // Prefixes
                    WritePrefixes(writer);

                    writer.WriteLine();
                    writer.WriteLine("# ============================================");
                    writer.WriteLine("# DXTnavis Geometry Export");
                    writer.WriteLine($"# Generated: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
                    writer.WriteLine($"# Objects: {records.Count:N0}");
                    writer.WriteLine("# ============================================");
                    writer.WriteLine();

                    int processed = 0;
                    int total = records.Count;

                    foreach (var kvp in records)
                    {
                        WriteGeometryTriples(writer, kvp.Value);

                        processed++;
                        if (processed % 500 == 0 || processed == total)
                        {
                            int percentage = (int)(100.0 * processed / total);
                            OnProgressChanged(percentage);
                        }
                    }
                }

                sw.Stop();
                OnProgressChanged(100);
                OnStatusChanged($"RDF 변환 완료: {records.Count:N0}개 객체 ({sw.Elapsed.TotalSeconds:F1}초)");

                return true;
            }
            catch (Exception ex)
            {
                OnStatusChanged($"RDF 변환 실패: {ex.Message}");
                Debug.WriteLine($"[GeometryRdfIntegrator] Error: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 기존 TTL 파일에 Geometry 정보 추가 (Append 모드)
        /// </summary>
        /// <param name="records">Geometry 레코드</param>
        /// <param name="existingTtlPath">기존 TTL 파일 경로</param>
        /// <returns>추가 성공 여부</returns>
        public bool AppendGeometryToTtl(Dictionary<Guid, GeometryRecord> records, string existingTtlPath)
        {
            if (records == null || records.Count == 0)
                return false;

            try
            {
                OnStatusChanged($"기존 TTL에 Geometry 추가: {records.Count:N0}개 객체");

                using (var writer = new StreamWriter(existingTtlPath, true, Encoding.UTF8))
                {
                    writer.WriteLine();
                    writer.WriteLine("# ============================================");
                    writer.WriteLine("# Geometry Properties (Appended by DXTnavis)");
                    writer.WriteLine($"# Appended: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
                    writer.WriteLine("# ============================================");
                    writer.WriteLine();

                    foreach (var kvp in records)
                    {
                        WriteGeometryTriples(writer, kvp.Value);
                    }
                }

                OnStatusChanged($"Geometry 추가 완료: {records.Count:N0}개 객체");
                return true;
            }
            catch (Exception ex)
            {
                OnStatusChanged($"Geometry 추가 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 단일 GeometryRecord를 SPARQL INSERT 문으로 변환
        /// </summary>
        /// <param name="record">Geometry 레코드</param>
        /// <returns>SPARQL INSERT DATA 문</returns>
        public string ToSparqlInsert(GeometryRecord record)
        {
            if (record == null || record.ObjectId == Guid.Empty)
                return null;

            var sb = new StringBuilder();
            sb.AppendLine("INSERT DATA {");

            var subjectUri = $"<{BSO_NAMESPACE}Object_{record.ObjectId:N}>";

            // BoundingBox
            if (record.BBox != null)
            {
                var bboxUri = $"<{BSO_NAMESPACE}BBox_{record.ObjectId:N}>";
                sb.AppendLine($"  {subjectUri} bso:hasBoundingBox {bboxUri} .");
                sb.AppendLine($"  {bboxUri} a bso:BoundingBox ;");
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "    bso:minX \"{0}\"^^xsd:double ;", record.BBox.Min.X));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "    bso:minY \"{0}\"^^xsd:double ;", record.BBox.Min.Y));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "    bso:minZ \"{0}\"^^xsd:double ;", record.BBox.Min.Z));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "    bso:maxX \"{0}\"^^xsd:double ;", record.BBox.Max.X));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "    bso:maxY \"{0}\"^^xsd:double ;", record.BBox.Max.Y));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "    bso:maxZ \"{0}\"^^xsd:double .", record.BBox.Max.Z));
            }

            // Centroid
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  {0} bso:centroidX \"{1}\"^^xsd:double .", subjectUri, record.Centroid.X));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  {0} bso:centroidY \"{1}\"^^xsd:double .", subjectUri, record.Centroid.Y));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  {0} bso:centroidZ \"{1}\"^^xsd:double .", subjectUri, record.Centroid.Z));

            // HasMesh
            sb.AppendLine($"  {subjectUri} bso:hasMesh \"{record.HasMesh.ToString().ToLowerInvariant()}\"^^xsd:boolean .");

            // MeshUri
            if (!string.IsNullOrEmpty(record.MeshUri))
            {
                sb.AppendLine($"  {subjectUri} bso:meshUri \"{EscapeString(record.MeshUri)}\" .");
            }

            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// Geometry 데이터에서 공간 쿼리를 위한 WKT (Well-Known Text) 생성
        /// BoundingBox를 WKT POLYGON으로 변환 (2D XY 평면)
        /// </summary>
        /// <param name="bbox">BoundingBox</param>
        /// <returns>WKT POLYGON 문자열</returns>
        public string BBoxToWkt(BBox3D bbox)
        {
            if (bbox == null) return null;

            // 2D Polygon (XY 평면, Z 무시)
            return string.Format(CultureInfo.InvariantCulture,
                "POLYGON(({0} {1}, {2} {1}, {2} {3}, {0} {3}, {0} {1}))",
                bbox.Min.X, bbox.Min.Y,
                bbox.Max.X, bbox.Max.Y);
        }

        /// <summary>
        /// Centroid를 WKT POINT로 변환
        /// </summary>
        /// <param name="centroid">중심점</param>
        /// <returns>WKT POINT 문자열</returns>
        public string CentroidToWkt(Point3D centroid)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "POINT({0} {1} {2})",
                centroid.X, centroid.Y, centroid.Z);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// TTL 파일에 Prefix 선언 작성
        /// </summary>
        private void WritePrefixes(StreamWriter writer)
        {
            writer.WriteLine($"@prefix {RDF_PREFIX}: <{RDF_NAMESPACE}> .");
            writer.WriteLine($"@prefix {RDFS_PREFIX}: <{RDFS_NAMESPACE}> .");
            writer.WriteLine($"@prefix {XSD_PREFIX}: <{XSD_NAMESPACE}> .");
            writer.WriteLine($"@prefix {BSO_PREFIX}: <{BSO_NAMESPACE}> .");
            writer.WriteLine($"@prefix {GEO_PREFIX}: <{GEO_NAMESPACE}> .");
        }

        /// <summary>
        /// 단일 GeometryRecord의 RDF Triple 작성
        /// </summary>
        private void WriteGeometryTriples(StreamWriter writer, GeometryRecord record)
        {
            if (record == null || record.ObjectId == Guid.Empty)
                return;

            var subjectUri = $"bso:Object_{record.ObjectId:N}";

            writer.WriteLine($"# Object: {record.DisplayName ?? record.ObjectId.ToString()}");

            // BoundingBox
            if (record.BBox != null)
            {
                var bboxUri = $"bso:BBox_{record.ObjectId:N}";

                writer.WriteLine($"{subjectUri} bso:hasBoundingBox {bboxUri} .");
                writer.WriteLine($"{bboxUri} a bso:BoundingBox ;");
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    bso:minX \"{0}\"^^xsd:double ;", record.BBox.Min.X));
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    bso:minY \"{0}\"^^xsd:double ;", record.BBox.Min.Y));
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    bso:minZ \"{0}\"^^xsd:double ;", record.BBox.Min.Z));
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    bso:maxX \"{0}\"^^xsd:double ;", record.BBox.Max.X));
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    bso:maxY \"{0}\"^^xsd:double ;", record.BBox.Max.Y));
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "    bso:maxZ \"{0}\"^^xsd:double .", record.BBox.Max.Z));

                // GeoSPARQL WKT (2D footprint)
                var wkt = BBoxToWkt(record.BBox);
                writer.WriteLine($"{bboxUri} geo:asWKT \"{wkt}\"^^geo:wktLiteral .");
            }

            // Centroid
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0} bso:centroidX \"{1}\"^^xsd:double .", subjectUri, record.Centroid.X));
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0} bso:centroidY \"{1}\"^^xsd:double .", subjectUri, record.Centroid.Y));
            writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0} bso:centroidZ \"{1}\"^^xsd:double .", subjectUri, record.Centroid.Z));

            // Centroid as WKT POINT
            var centroidWkt = CentroidToWkt(record.Centroid);
            writer.WriteLine($"{subjectUri} bso:centroidWKT \"{centroidWkt}\"^^geo:wktLiteral .");

            // Volume
            var volume = record.GetVolume();
            if (volume > 0)
            {
                writer.WriteLine(string.Format(CultureInfo.InvariantCulture,
                    "{0} bso:volume \"{1}\"^^xsd:double .", subjectUri, volume));
            }

            // HasMesh
            writer.WriteLine($"{subjectUri} bso:hasMesh \"{record.HasMesh.ToString().ToLowerInvariant()}\"^^xsd:boolean .");

            // MeshUri
            if (!string.IsNullOrEmpty(record.MeshUri))
            {
                writer.WriteLine($"{subjectUri} bso:meshUri \"{EscapeString(record.MeshUri)}\" .");
            }

            // DisplayName
            if (!string.IsNullOrEmpty(record.DisplayName))
            {
                writer.WriteLine($"{subjectUri} rdfs:label \"{EscapeString(record.DisplayName)}\" .");
            }

            // Category
            if (!string.IsNullOrEmpty(record.Category))
            {
                writer.WriteLine($"{subjectUri} bso:category \"{EscapeString(record.Category)}\" .");
            }

            writer.WriteLine();
        }

        /// <summary>
        /// TTL 문자열 이스케이프
        /// </summary>
        private static string EscapeString(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;

            return str
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
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
            Debug.WriteLine($"[GeometryRdfIntegrator] {message}");
        }

        #endregion
    }
}

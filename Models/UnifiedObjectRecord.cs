using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DXTnavis.Models.Geometry;

namespace DXTnavis.Models
{
    /// <summary>
    /// 통합 객체 레코드 - Hierarchy + Geometry + Manifest 데이터 통합
    /// Phase 16: Unified CSV Export for Ontology Conversion
    ///
    /// 목적: bim-ontology 프로젝트에 전달할 단일 종합 CSV 생성
    /// - 1 row = 1 object (속성은 JSON 배열로 집계)
    /// - Geometry 정보 (BBox, Centroid, Volume) 포함
    /// - Mesh URI 포함
    /// </summary>
    public class UnifiedObjectRecord
    {
        #region Metadata

        /// <summary>스키마 버전</summary>
        public string SchemaVersion { get; set; } = "1.0.0";

        /// <summary>내보내기 시각 (UTC)</summary>
        public DateTime ExportedAtUtc { get; set; } = DateTime.UtcNow;

        #endregion

        #region Identity

        /// <summary>객체 고유 ID (Navisworks InstanceGuid 또는 Synthetic ID)</summary>
        public Guid ObjectId { get; set; }

        /// <summary>부모 객체 ID (최상위는 Guid.Empty)</summary>
        public Guid ParentId { get; set; }

        /// <summary>계층 깊이 (0부터 시작)</summary>
        public int Level { get; set; }

        /// <summary>객체 표시 이름</summary>
        public string DisplayName { get; set; }

        /// <summary>객체 카테고리 (Navisworks ClassDisplayName)</summary>
        public string ObjectCategory { get; set; }

        /// <summary>계층 경로 (예: "Project > Building > Level 1 > Wall")</summary>
        public string HierarchyPath { get; set; }

        #endregion

        #region Properties (Aggregated)

        /// <summary>속성 개수</summary>
        public int PropertyCount { get; set; }

        /// <summary>속성 목록 (JSON 직렬화용)</summary>
        public List<PropertyEntry> Properties { get; set; } = new List<PropertyEntry>();

        #endregion

        #region Geometry

        /// <summary>BoundingBox 존재 여부</summary>
        public bool HasBBox { get; set; }

        /// <summary>BBox 최소 X</summary>
        public double? BBoxMinX { get; set; }

        /// <summary>BBox 최소 Y</summary>
        public double? BBoxMinY { get; set; }

        /// <summary>BBox 최소 Z</summary>
        public double? BBoxMinZ { get; set; }

        /// <summary>BBox 최대 X</summary>
        public double? BBoxMaxX { get; set; }

        /// <summary>BBox 최대 Y</summary>
        public double? BBoxMaxY { get; set; }

        /// <summary>BBox 최대 Z</summary>
        public double? BBoxMaxZ { get; set; }

        /// <summary>중심점 X</summary>
        public double? CentroidX { get; set; }

        /// <summary>중심점 Y</summary>
        public double? CentroidY { get; set; }

        /// <summary>중심점 Z</summary>
        public double? CentroidZ { get; set; }

        /// <summary>BBox 부피 (m³)</summary>
        public double? BBoxVolume { get; set; }

        #endregion

        #region Mesh

        /// <summary>Mesh 파일 존재 여부</summary>
        public bool HasMesh { get; set; }

        /// <summary>Mesh 파일 URI (상대 경로)</summary>
        public string MeshUri { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// GeometryRecord에서 Geometry 정보 설정
        /// </summary>
        public void SetGeometry(GeometryRecord geometry)
        {
            if (geometry == null)
            {
                HasBBox = false;
                return;
            }

            HasBBox = geometry.BBox != null && !geometry.BBox.IsEmpty;

            if (HasBBox)
            {
                BBoxMinX = geometry.BBox.Min.X;
                BBoxMinY = geometry.BBox.Min.Y;
                BBoxMinZ = geometry.BBox.Min.Z;
                BBoxMaxX = geometry.BBox.Max.X;
                BBoxMaxY = geometry.BBox.Max.Y;
                BBoxMaxZ = geometry.BBox.Max.Z;
                BBoxVolume = geometry.BBox.GetVolume();
            }

            if (geometry.Centroid != null)
            {
                CentroidX = geometry.Centroid.X;
                CentroidY = geometry.Centroid.Y;
                CentroidZ = geometry.Centroid.Z;
            }

            HasMesh = geometry.HasMesh;
            MeshUri = geometry.MeshUri;
        }

        /// <summary>
        /// HierarchicalPropertyRecord 목록에서 속성 추가
        /// </summary>
        public void AddProperties(IEnumerable<HierarchicalPropertyRecord> records)
        {
            foreach (var record in records)
            {
                Properties.Add(new PropertyEntry
                {
                    Category = record.Category ?? string.Empty,
                    Name = record.PropertyName ?? string.Empty,
                    Value = record.PropertyValue ?? string.Empty,
                    RawValue = record.RawValue ?? string.Empty,
                    DataType = record.DataType ?? string.Empty,
                    NumericValue = record.NumericValue,
                    Unit = record.Unit ?? string.Empty
                });
            }
            PropertyCount = Properties.Count;
        }

        /// <summary>
        /// Properties를 JSON 문자열로 변환
        /// </summary>
        public string GetPropertiesJson()
        {
            if (Properties == null || Properties.Count == 0)
                return "[]";

            var sb = new StringBuilder();
            sb.Append('[');

            for (int i = 0; i < Properties.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Properties[i].ToJson());
            }

            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>
        /// CSV 헤더 문자열
        /// </summary>
        public static string GetCsvHeader()
        {
            return "SchemaVersion,ExportedAtUtc,ObjectId,ParentId,Level,DisplayName,ObjectCategory,HierarchyPath,PropertyCount,PropertiesJson,BBoxMinX,BBoxMinY,BBoxMinZ,BBoxMaxX,BBoxMaxY,BBoxMaxZ,CentroidX,CentroidY,CentroidZ,BBoxVolume,HasMesh,MeshUri";
        }

        /// <summary>
        /// CSV 행 문자열로 변환
        /// </summary>
        public string ToCsvRow()
        {
            var culture = CultureInfo.InvariantCulture;

            return string.Format(culture,
                "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19},{20},{21}",
                EscapeCsv(SchemaVersion),
                ExportedAtUtc.ToString("o"),  // ISO 8601
                ObjectId,
                ParentId,
                Level,
                EscapeCsv(DisplayName ?? string.Empty),
                EscapeCsv(ObjectCategory ?? string.Empty),
                EscapeCsv(HierarchyPath ?? string.Empty),
                PropertyCount,
                EscapeCsv(GetPropertiesJson()),
                FormatDouble(BBoxMinX),
                FormatDouble(BBoxMinY),
                FormatDouble(BBoxMinZ),
                FormatDouble(BBoxMaxX),
                FormatDouble(BBoxMaxY),
                FormatDouble(BBoxMaxZ),
                FormatDouble(CentroidX),
                FormatDouble(CentroidY),
                FormatDouble(CentroidZ),
                FormatDouble(BBoxVolume),
                HasMesh.ToString().ToLowerInvariant(),
                EscapeCsv(MeshUri ?? string.Empty));
        }

        private static string FormatDouble(double? value)
        {
            if (!value.HasValue) return string.Empty;
            return value.Value.ToString("F6", CultureInfo.InvariantCulture);
        }

        private static string EscapeCsv(string field)
        {
            if (string.IsNullOrEmpty(field)) return string.Empty;

            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                field = field.Replace("\"", "\"\"");
                return $"\"{field}\"";
            }

            return field;
        }

        #endregion
    }

    /// <summary>
    /// 속성 항목 (JSON 직렬화용)
    /// </summary>
    public class PropertyEntry
    {
        public string Category { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public string RawValue { get; set; }
        public string DataType { get; set; }
        public double? NumericValue { get; set; }
        public string Unit { get; set; }

        /// <summary>
        /// JSON 문자열로 변환 (수동 직렬화 - Newtonsoft 의존성 최소화)
        /// </summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append('{');
            sb.AppendFormat("\"category\":\"{0}\"", EscapeJson(Category));
            sb.AppendFormat(",\"name\":\"{0}\"", EscapeJson(Name));
            sb.AppendFormat(",\"value\":\"{0}\"", EscapeJson(Value));
            sb.AppendFormat(",\"rawValue\":\"{0}\"", EscapeJson(RawValue));
            sb.AppendFormat(",\"dataType\":\"{0}\"", EscapeJson(DataType));

            if (NumericValue.HasValue)
                sb.AppendFormat(CultureInfo.InvariantCulture, ",\"numericValue\":{0}", NumericValue.Value);
            else
                sb.Append(",\"numericValue\":null");

            sb.AppendFormat(",\"unit\":\"{0}\"", EscapeJson(Unit));
            sb.Append('}');

            return sb.ToString();
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}

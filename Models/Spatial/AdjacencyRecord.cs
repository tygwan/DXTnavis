using System;
using System.Globalization;
using System.Text;

namespace DXTnavis.Models.Spatial
{
    /// <summary>
    /// Phase 17: 두 BIM 객체 간의 공간 인접 관계 레코드
    /// </summary>
    public class AdjacencyRecord
    {
        /// <summary>소스 객체 ID</summary>
        public Guid SourceObjectId { get; set; }

        /// <summary>타겟 객체 ID</summary>
        public Guid TargetObjectId { get; set; }

        /// <summary>소스 객체 이름</summary>
        public string SourceName { get; set; }

        /// <summary>타겟 객체 이름</summary>
        public string TargetName { get; set; }

        /// <summary>두 BBox 간 최소 거리 (미터)</summary>
        public double Distance { get; set; }

        /// <summary>겹침 체적 (겹치는 경우, 세제곱미터)</summary>
        public double OverlapVolume { get; set; }

        /// <summary>관계 유형</summary>
        public SpatialRelationType RelationType { get; set; }

        /// <summary>소스 카테고리</summary>
        public string SourceCategory { get; set; }

        /// <summary>타겟 카테고리</summary>
        public string TargetCategory { get; set; }

        /// <summary>적용된 tolerance 값</summary>
        public double Tolerance { get; set; }

        /// <summary>
        /// CSV 헤더 문자열
        /// </summary>
        public static string CsvHeader =>
            "SourceObjectId,TargetObjectId,SourceName,TargetName,Distance,OverlapVolume,RelationType,SourceCategory,TargetCategory,Tolerance";

        /// <summary>
        /// CSV 행 문자열로 변환
        /// </summary>
        public string ToCsvRow()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4:F6},{5:F6},{6},{7},{8},{9:F4}",
                SourceObjectId.ToString("N"),
                TargetObjectId.ToString("N"),
                EscapeCsv(SourceName),
                EscapeCsv(TargetName),
                Distance,
                OverlapVolume,
                RelationType.ToString().ToLowerInvariant(),
                EscapeCsv(SourceCategory),
                EscapeCsv(TargetCategory),
                Tolerance);
        }

        /// <summary>
        /// TTL 트리플 문자열로 변환
        /// </summary>
        public string ToTtl(string prefix = "bso")
        {
            var sb = new StringBuilder();
            sb.AppendFormat("{0}:Object_{1} spatial:adjacentTo {0}:Object_{2} .",
                prefix, SourceObjectId.ToString("N"), TargetObjectId.ToString("N"));
            sb.AppendLine();

            sb.AppendFormat("{0}:Object_{1} spatial:distanceTo [ spatial:target {0}:Object_{2} ; spatial:distance \"{3:F6}\"^^xsd:double ] .",
                prefix, SourceObjectId.ToString("N"), TargetObjectId.ToString("N"), Distance);
            sb.AppendLine();

            if (OverlapVolume > 0)
            {
                sb.AppendFormat("{0}:Object_{1} spatial:overlapsWith [ spatial:target {0}:Object_{2} ; spatial:overlapVolume \"{3:F6}\"^^xsd:double ] .",
                    prefix, SourceObjectId.ToString("N"), TargetObjectId.ToString("N"), OverlapVolume);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public override string ToString() =>
            string.Format("{0} --[{1}, d={2:F3}m]--> {3}",
                SourceName ?? SourceObjectId.ToString("N").Substring(0, 8),
                RelationType,
                Distance,
                TargetName ?? TargetObjectId.ToString("N").Substring(0, 8));

        private static string EscapeCsv(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            return field;
        }
    }

    /// <summary>
    /// 공간 관계 유형
    /// </summary>
    public enum SpatialRelationType
    {
        /// <summary>BBox가 겹침 (Distance = 0, OverlapVolume > 0)</summary>
        Overlap,

        /// <summary>BBox가 접촉 (Distance ~= 0)</summary>
        Touch,

        /// <summary>tolerance 이내 근접 (Distance > 0, Distance <= tolerance)</summary>
        NearTouch,

        /// <summary>tolerance 초과 (인접하지 않음)</summary>
        Distant
    }
}

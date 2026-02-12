using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DXTnavis.Models.Geometry;

namespace DXTnavis.Models.Spatial
{
    /// <summary>
    /// Phase 17: 공간적으로 연결된 BIM 객체 그룹
    /// Union-Find 알고리즘으로 생성
    /// </summary>
    public class ConnectedGroup
    {
        /// <summary>그룹 ID (1-based)</summary>
        public int GroupId { get; set; }

        /// <summary>그룹에 포함된 객체 ID 목록</summary>
        public List<Guid> ElementIds { get; set; } = new List<Guid>();

        /// <summary>그룹에 포함된 객체 이름 목록</summary>
        public List<string> ElementNames { get; set; } = new List<string>();

        /// <summary>그룹 내 요소 수</summary>
        public int ElementCount => ElementIds.Count;

        /// <summary>그룹 내 총 BBox 부피 합</summary>
        public double TotalVolume { get; set; }

        /// <summary>그룹 전체를 감싸는 BBox (Union)</summary>
        public BBox3D GroupBBox { get; set; }

        /// <summary>그룹 BBox의 중심점</summary>
        public Point3D Centroid => GroupBBox?.GetCentroid() ?? Point3D.Origin;

        /// <summary>그룹 내 인접 관계 수</summary>
        public int EdgeCount { get; set; }

        /// <summary>그룹의 주된 카테고리 (최빈값)</summary>
        public string DominantCategory { get; set; }

        /// <summary>
        /// GeometryRecord 딕셔너리로부터 그룹 통계를 계산
        /// </summary>
        public void ComputeStatistics(Dictionary<Guid, GeometryRecord> geometries)
        {
            if (geometries == null || ElementIds.Count == 0) return;

            BBox3D union = null;
            double totalVol = 0;
            var categories = new Dictionary<string, int>();

            foreach (var id in ElementIds)
            {
                if (!geometries.TryGetValue(id, out var geo)) continue;

                if (geo.BBox != null)
                {
                    union = union == null ? geo.BBox : union.Union(geo.BBox);
                    totalVol += geo.GetVolume();
                }

                if (!string.IsNullOrEmpty(geo.Category))
                {
                    if (!categories.ContainsKey(geo.Category))
                        categories[geo.Category] = 0;
                    categories[geo.Category]++;
                }
            }

            GroupBBox = union;
            TotalVolume = totalVol;

            if (categories.Count > 0)
                DominantCategory = categories.OrderByDescending(kv => kv.Value).First().Key;
        }

        /// <summary>
        /// CSV 헤더 문자열
        /// </summary>
        public static string CsvHeader =>
            "GroupId,ElementCount,EdgeCount,TotalVolume,DominantCategory," +
            "BBoxMinX,BBoxMinY,BBoxMinZ,BBoxMaxX,BBoxMaxY,BBoxMaxZ," +
            "CentroidX,CentroidY,CentroidZ";

        /// <summary>
        /// CSV 행 문자열로 변환
        /// </summary>
        public string ToCsvRow()
        {
            var c = Centroid;
            return string.Format(CultureInfo.InvariantCulture,
                "G{0:D3},{1},{2},{3:F4},{4},{5:F4},{6:F4},{7:F4},{8:F4},{9:F4},{10:F4},{11:F4},{12:F4},{13:F4}",
                GroupId,
                ElementCount,
                EdgeCount,
                TotalVolume,
                EscapeCsv(DominantCategory),
                GroupBBox?.Min.X ?? 0, GroupBBox?.Min.Y ?? 0, GroupBBox?.Min.Z ?? 0,
                GroupBBox?.Max.X ?? 0, GroupBBox?.Max.Y ?? 0, GroupBBox?.Max.Z ?? 0,
                c.X, c.Y, c.Z);
        }

        /// <summary>
        /// TTL 형식으로 변환
        /// </summary>
        public string ToTtl(string prefix = "spatial")
        {
            var sb = new StringBuilder();
            var groupUri = string.Format("{0}:Group_{1:D3}", prefix, GroupId);

            sb.AppendFormat("{0} a {1}:ConnectedGroup ;", groupUri, prefix);
            sb.AppendLine();
            sb.AppendFormat("    {0}:groupSize {1} ;", prefix, ElementCount);
            sb.AppendLine();
            sb.AppendFormat(CultureInfo.InvariantCulture,
                "    {0}:groupVolume \"{1:F4}\"^^xsd:double .", prefix, TotalVolume);
            sb.AppendLine();
            sb.AppendLine();

            // 각 요소를 그룹에 연결
            foreach (var id in ElementIds)
            {
                sb.AppendFormat("bso:Object_{0} {1}:inGroup {2} .",
                    id.ToString("N"), prefix, groupUri);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        public override string ToString() =>
            string.Format("Group G{0:D3}: {1} elements, {2:F2}m3, {3}",
                GroupId, ElementCount, TotalVolume, DominantCategory ?? "mixed");

        private static string EscapeCsv(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            return field;
        }
    }
}

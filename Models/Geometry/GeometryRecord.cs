using System;
using System.Globalization;
using System.Text;

namespace DXTnavis.Models.Geometry
{
    /// <summary>
    /// BIM 객체의 기하학 정보 레코드
    /// BoundingBox + Centroid (필수) + Mesh URI (선택)
    /// </summary>
    public class GeometryRecord
    {
        /// <summary>객체 고유 ID (Navisworks InstanceGuid 또는 Synthetic ID)</summary>
        public Guid ObjectId { get; set; }

        /// <summary>객체의 경계 상자</summary>
        public BBox3D BBox { get; set; }

        /// <summary>객체의 중심점</summary>
        public Point3D Centroid { get; set; }

        /// <summary>Mesh 데이터 존재 여부</summary>
        public bool HasMesh { get; set; }

        /// <summary>Mesh 파일 상대 경로 (예: "mesh/xxx.glb")</summary>
        public string MeshUri { get; set; }

        /// <summary>객체 이름 (선택적)</summary>
        public string DisplayName { get; set; }

        /// <summary>객체 카테고리 (선택적)</summary>
        public string Category { get; set; }

        /// <summary>
        /// 기본 생성자
        /// </summary>
        public GeometryRecord() { }

        /// <summary>
        /// ObjectId와 BBox로 GeometryRecord 생성
        /// Centroid는 자동 계산
        /// </summary>
        public GeometryRecord(Guid objectId, BBox3D bbox)
        {
            ObjectId = objectId;
            BBox = bbox;
            Centroid = bbox?.GetCentroid() ?? Point3D.Origin;
            HasMesh = false;
            MeshUri = null;
        }

        /// <summary>
        /// ObjectId와 BBox, DisplayName으로 GeometryRecord 생성
        /// </summary>
        public GeometryRecord(Guid objectId, BBox3D bbox, string displayName)
            : this(objectId, bbox)
        {
            DisplayName = displayName;
        }

        #region Mesh 관련

        /// <summary>
        /// Mesh URI 설정 (mesh 폴더 경로 기준)
        /// </summary>
        /// <param name="meshFolderPath">mesh 폴더 상대 경로 (예: "mesh")</param>
        public void SetMeshUri(string meshFolderPath = "mesh")
        {
            if (ObjectId != Guid.Empty)
            {
                MeshUri = $"{meshFolderPath}/{ObjectId:N}.glb";
                HasMesh = true;
            }
        }

        /// <summary>
        /// Mesh 파일의 절대 경로 가져오기
        /// </summary>
        /// <param name="exportBasePath">export 폴더의 절대 경로</param>
        public string GetMeshAbsolutePath(string exportBasePath)
        {
            if (string.IsNullOrEmpty(MeshUri)) return null;
            return System.IO.Path.Combine(exportBasePath, MeshUri);
        }

        #endregion

        #region 공간 쿼리 지원

        /// <summary>
        /// 객체의 부피 반환
        /// </summary>
        public double GetVolume() => BBox?.GetVolume() ?? 0;

        /// <summary>
        /// 다른 객체와의 중심점 거리
        /// </summary>
        public double DistanceTo(GeometryRecord other)
        {
            if (other == null) return double.MaxValue;
            return Centroid.DistanceTo(other.Centroid);
        }

        /// <summary>
        /// 이 객체의 BBox가 다른 객체의 BBox와 교차하는지
        /// </summary>
        public bool Intersects(GeometryRecord other)
        {
            if (other?.BBox == null || BBox == null) return false;
            return BBox.Intersects(other.BBox);
        }

        #endregion

        #region 직렬화

        /// <summary>
        /// JSON 형식 문자열로 변환 (manifest.json 객체 항목용)
        /// </summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  \"objectId\": \"{0}\",", ObjectId));

            // BBox
            sb.AppendLine("  \"bbox\": {");
            if (BBox != null)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "    \"min\": {{ \"x\": {0}, \"y\": {1}, \"z\": {2} }},",
                    BBox.Min.X, BBox.Min.Y, BBox.Min.Z));
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "    \"max\": {{ \"x\": {0}, \"y\": {1}, \"z\": {2} }}",
                    BBox.Max.X, BBox.Max.Y, BBox.Max.Z));
            }
            sb.AppendLine("  },");

            // Centroid
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  \"centroid\": {{ \"x\": {0}, \"y\": {1}, \"z\": {2} }},",
                Centroid.X, Centroid.Y, Centroid.Z));

            // HasMesh
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  \"hasMesh\": {0},", HasMesh.ToString().ToLowerInvariant()));

            // MeshUri
            if (string.IsNullOrEmpty(MeshUri))
            {
                sb.AppendLine("  \"meshUri\": null");
            }
            else
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  \"meshUri\": \"{0}\"", MeshUri));
            }

            // DisplayName (선택적)
            if (!string.IsNullOrEmpty(DisplayName))
            {
                sb.Insert(sb.Length - 1, ",\n");
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  \"displayName\": \"{0}\"", EscapeJsonString(DisplayName)));
            }

            sb.Append("}");
            return sb.ToString();
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

        public override string ToString() =>
            $"GeometryRecord[{ObjectId:N}] {DisplayName ?? "(no name)"} HasMesh={HasMesh}";

        #endregion
    }
}

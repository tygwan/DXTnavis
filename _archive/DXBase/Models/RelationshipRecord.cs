namespace DXBase.Models
{
    /// <summary>
    /// BIM 객체 간 관계를 표현하는 DTO
    /// </summary>
    public class RelationshipRecord
    {
        public string ModelVersion { get; set; }

        /// <summary>
        /// 관계의 출발 객체 (예: 벽)
        /// </summary>
        public string SourceObjectId { get; set; }

        /// <summary>
        /// 관계의 도착 객체 (예: 문)
        /// </summary>
        public string TargetObjectId { get; set; }

        /// <summary>
        /// 관계 유형 (예: "Contains", "ConnectsTo", "HostedBy")
        /// </summary>
        public string RelationType { get; set; }

        /// <summary>
        /// 관계 방향성 (true: 단방향, false: 양방향)
        /// </summary>
        public bool IsDirected { get; set; }
    }
}

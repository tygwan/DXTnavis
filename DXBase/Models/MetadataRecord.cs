using System;

namespace DXBase.Models
{
    /// <summary>
    /// BIM 모델 버전의 메타데이터를 표현하는 DTO
    /// </summary>
    public class MetadataRecord
    {
        /// <summary>
        /// 버전 식별자 (예: "v1.0.0", "2024-01-15_Rev01")
        /// </summary>
        public string ModelVersion { get; set; }

        /// <summary>
        /// 스냅샷 생성 시각 (UTC)
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// 프로젝트 이름
        /// </summary>
        public string ProjectName { get; set; }

        /// <summary>
        /// 생성자 (BIM 엔지니어 이름)
        /// </summary>
        public string CreatedBy { get; set; }

        /// <summary>
        /// 변경 사유 또는 설명
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 총 객체 수 (성능 최적화용)
        /// </summary>
        public int TotalObjectCount { get; set; }

        /// <summary>
        /// Revit 파일 경로 (추적용)
        /// </summary>
        public string RevitFilePath { get; set; }
    }
}

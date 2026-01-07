namespace DXBase.Models
{
    /// <summary>
    /// BIM 객체(Element)의 속성을 표현하는 DTO
    /// </summary>
    public class ObjectRecord
    {
        /// <summary>
        /// 어떤 버전에 속하는지
        /// </summary>
        public string ModelVersion { get; set; }

        /// <summary>
        /// Revit의 고유 식별자 (InstanceGuid 또는 생성된 해시)
        /// </summary>
        public string ObjectId { get; set; }

        /// <summary>
        /// Revit ElementId (정수형, 세션 종속적)
        /// </summary>
        public int ElementId { get; set; }

        /// <summary>
        /// 카테고리 (예: "Walls", "Doors", "Windows")
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// 패밀리 이름 (예: "Basic Wall", "M_Door-Single")
        /// </summary>
        public string Family { get; set; }

        /// <summary>
        /// 타입 이름 (예: "Generic - 200mm")
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 공정 ID (사용자 정의 공유 매개변수)
        /// TimeLiner 자동화의 핵심 필드
        /// </summary>
        public string ActivityId { get; set; }

        /// <summary>
        /// 모든 매개변수를 JSON 형태로 저장
        /// 유연성과 확장성을 위해
        /// </summary>
        public string Properties { get; set; }

        /// <summary>
        /// 객체의 3D 바운딩 박스 (JSON)
        /// 시각화 및 검색 최적화용
        /// </summary>
        public string BoundingBox { get; set; }
    }
}

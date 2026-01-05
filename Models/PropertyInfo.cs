namespace DXTnavis.Models
{
    /// <summary>
    /// Navisworks 객체의 단일 속성을 나타내는 데이터 모델
    /// UI 바인딩을 위한 간단한 POCO 클래스입니다.
    /// </summary>
    public class PropertyInfo
    {
        /// <summary>
        /// 속성이 속한 카테고리 (예: "Item", "Element", "기본정보" 등)
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// 속성의 이름 (예: "Name", "레벨", "재질" 등)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 속성의 값 (문자열로 변환된 값)
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// 객체 ID (전체 모델 내보내기 시 사용)
        /// </summary>
        public int ObjectId { get; set; }

        /// <summary>
        /// 객체 이름 (전체 모델 내보내기 시 사용)
        /// </summary>
        public string ObjectName { get; set; }
    }
}

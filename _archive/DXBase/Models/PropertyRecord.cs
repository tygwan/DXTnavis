namespace DXBase.Models
{
    /// <summary>
    /// 객체 속성을 정규화된 형태로 표현하는 DTO (선택사항)
    /// ObjectRecord.Properties JSON 대신 별도 테이블로 관리 시 사용
    /// </summary>
    public class PropertyRecord
    {
        public string ModelVersion { get; set; }
        public string ObjectId { get; set; }

        /// <summary>
        /// 속성 이름 (예: "Length", "Volume", "Cost")
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// 속성 값 (문자열로 저장)
        /// </summary>
        public string PropertyValue { get; set; }

        /// <summary>
        /// 속성 데이터 타입 (예: "Double", "String", "Integer")
        /// </summary>
        public string PropertyType { get; set; }

        /// <summary>
        /// 속성 단위 (예: "m", "m³", "USD")
        /// </summary>
        public string PropertyUnit { get; set; }
    }
}

using System;

namespace DXBase.Models
{
    /// <summary>
    /// API 응답의 표준 포맷
    /// </summary>
    /// <typeparam name="T">응답 데이터 타입</typeparam>
    public class ApiResponse<T>
    {
        /// <summary>
        /// 성공 여부
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 응답 데이터 (제네릭)
        /// </summary>
        public T Data { get; set; }

        /// <summary>
        /// 오류 메시지 (실패 시)
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// HTTP 상태 코드
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// 응답 타임스탬프
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}

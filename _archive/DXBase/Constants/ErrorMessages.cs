namespace DXBase.Constants
{
    /// <summary>
    /// 오류 메시지 상수
    /// </summary>
    public static class ErrorMessages
    {
        public const string ConnectionFailed = "API 서버 연결 실패. 서버 주소를 확인하세요.";
        public const string InvalidData = "유효하지 않은 데이터 형식입니다.";
        public const string Timeout = "요청 시간 초과. 네트워크 상태를 확인하세요.";
        public const string Unauthorized = "인증이 필요합니다.";
        public const string ServerError = "서버 오류가 발생했습니다.";
    }
}

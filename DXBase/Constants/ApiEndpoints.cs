namespace DXBase.Constants
{
    /// <summary>
    /// API 엔드포인트 상수
    /// </summary>
    public static class ApiEndpoints
    {
        // 데이터 수집
        public const string Ingest = "/api/v1/ingest";

        // 버전 관리
        public const string GetVersions = "/api/v1/models/versions";
        public const string GetVersionSummary = "/api/v1/models/{version}/summary";

        // 버전 비교
        public const string CompareVersions = "/api/v1/models/compare";

        // TimeLiner 연동
        public const string GetTimelinerMapping = "/api/v1/timeliner/{version}/mapping";
    }
}

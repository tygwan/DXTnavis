using System;
using System.Collections.Generic;
using System.Linq;

namespace DXTnavis.Models
{
    /// <summary>
    /// AWP 4D 자동화 검증 결과
    /// 실행 전/후 검증 결과를 저장합니다.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// 검증 통과 여부
        /// </summary>
        public bool IsValid => !Errors.Any();

        /// <summary>
        /// 검증 시간
        /// </summary>
        public DateTime ValidatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// 검증 유형 (Pre/Post)
        /// </summary>
        public ValidationType ValidationType { get; set; }

        /// <summary>
        /// 오류 목록
        /// </summary>
        public List<ValidationError> Errors { get; set; } = new List<ValidationError>();

        /// <summary>
        /// 경고 목록
        /// </summary>
        public List<ValidationWarning> Warnings { get; set; } = new List<ValidationWarning>();

        /// <summary>
        /// 정보 메시지 목록
        /// </summary>
        public List<string> InfoMessages { get; set; } = new List<string>();

        /// <summary>
        /// 검증된 스케줄 데이터 수
        /// </summary>
        public int ScheduleDataCount { get; set; }

        /// <summary>
        /// 유효한 스케줄 데이터 수
        /// </summary>
        public int ValidScheduleCount { get; set; }

        /// <summary>
        /// 무효한 스케줄 데이터 수
        /// </summary>
        public int InvalidScheduleCount => ScheduleDataCount - ValidScheduleCount;

        /// <summary>
        /// 오류 추가
        /// </summary>
        public void AddError(ValidationErrorCode code, string message, string affectedItem = null)
        {
            Errors.Add(new ValidationError
            {
                Code = code,
                Message = message,
                AffectedItem = affectedItem
            });
        }

        /// <summary>
        /// 경고 추가
        /// </summary>
        public void AddWarning(ValidationWarningCode code, string message, string affectedItem = null)
        {
            Warnings.Add(new ValidationWarning
            {
                Code = code,
                Message = message,
                AffectedItem = affectedItem
            });
        }

        /// <summary>
        /// 정보 메시지 추가
        /// </summary>
        public void AddInfo(string message)
        {
            InfoMessages.Add(message);
        }

        /// <summary>
        /// 요약 문자열 반환
        /// </summary>
        public string GetSummary()
        {
            var summary = new List<string>
            {
                $"검증 결과: {(IsValid ? "✅ 통과" : "❌ 실패")}",
                $"검증 시간: {ValidatedAt:yyyy-MM-dd HH:mm:ss}",
                $"검증 유형: {ValidationType}",
                $"스케줄 데이터: {ValidScheduleCount}/{ScheduleDataCount} 유효"
            };

            if (Errors.Any())
            {
                summary.Add($"오류: {Errors.Count}건");
                foreach (var error in Errors.Take(5))
                {
                    summary.Add($"  - [{error.Code}] {error.Message}");
                }
                if (Errors.Count > 5)
                    summary.Add($"  ... 외 {Errors.Count - 5}건");
            }

            if (Warnings.Any())
            {
                summary.Add($"경고: {Warnings.Count}건");
                foreach (var warning in Warnings.Take(3))
                {
                    summary.Add($"  - [{warning.Code}] {warning.Message}");
                }
                if (Warnings.Count > 3)
                    summary.Add($"  ... 외 {Warnings.Count - 3}건");
            }

            return string.Join(Environment.NewLine, summary);
        }

        /// <summary>
        /// 빈 유효한 결과 생성
        /// </summary>
        public static ValidationResult Valid(ValidationType type = ValidationType.Pre)
        {
            return new ValidationResult { ValidationType = type };
        }

        /// <summary>
        /// 오류 결과 생성
        /// </summary>
        public static ValidationResult WithError(ValidationErrorCode code, string message, ValidationType type = ValidationType.Pre)
        {
            var result = new ValidationResult { ValidationType = type };
            result.AddError(code, message);
            return result;
        }
    }

    /// <summary>
    /// 검증 유형
    /// </summary>
    public enum ValidationType
    {
        /// <summary>실행 전 검증</summary>
        Pre,
        /// <summary>실행 후 검증</summary>
        Post,
        /// <summary>CSV 파일 검증</summary>
        CsvFile,
        /// <summary>옵션 검증</summary>
        Options
    }

    /// <summary>
    /// 검증 오류
    /// </summary>
    public class ValidationError
    {
        public ValidationErrorCode Code { get; set; }
        public string Message { get; set; }
        public string AffectedItem { get; set; }

        public override string ToString()
        {
            var item = string.IsNullOrEmpty(AffectedItem) ? "" : $" [{AffectedItem}]";
            return $"[{Code}]{item} {Message}";
        }
    }

    /// <summary>
    /// 검증 오류 코드
    /// </summary>
    public enum ValidationErrorCode
    {
        // 파일 관련 (1xx)
        FileNotFound = 100,
        FileReadError = 101,
        InvalidFileFormat = 102,
        EmptyFile = 103,

        // CSV 관련 (2xx)
        MissingSyncIdColumn = 200,
        MissingRequiredColumn = 201,
        InvalidDateFormat = 202,
        InvalidDataFormat = 203,
        DuplicateSyncId = 204,

        // 스케줄 데이터 관련 (3xx)
        InvalidScheduleData = 300,
        NoValidScheduleData = 301,
        DateRangeError = 302,
        MissingTaskName = 303,

        // 매칭 관련 (4xx)
        LowMatchRate = 400,
        NoMatchesFound = 401,
        MatchingError = 402,

        // Navisworks 관련 (5xx)
        NoActiveDocument = 500,
        NoModelsLoaded = 501,
        ApiError = 502,
        ComApiError = 503,

        // 옵션 관련 (6xx)
        InvalidOptions = 600,
        MissingRequiredOption = 601,

        // 기타 (9xx)
        UnknownError = 999
    }

    /// <summary>
    /// 검증 경고
    /// </summary>
    public class ValidationWarning
    {
        public ValidationWarningCode Code { get; set; }
        public string Message { get; set; }
        public string AffectedItem { get; set; }

        public override string ToString()
        {
            var item = string.IsNullOrEmpty(AffectedItem) ? "" : $" [{AffectedItem}]";
            return $"[{Code}]{item} {Message}";
        }
    }

    /// <summary>
    /// 검증 경고 코드
    /// </summary>
    public enum ValidationWarningCode
    {
        // 데이터 관련 (1xx)
        MissingOptionalData = 100,
        DataTruncated = 101,
        DefaultValueUsed = 102,

        // 매칭 관련 (2xx)
        PartialMatchUsed = 200,
        MultipleMatchesFound = 201,
        LowConfidenceMatch = 202,

        // 성능 관련 (3xx)
        LargeDataSet = 300,
        SlowOperation = 301,

        // 기타 (9xx)
        UnknownWarning = 999
    }
}

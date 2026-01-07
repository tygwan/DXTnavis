using System;
using System.Collections.Generic;
using System.Linq;
using DXBase.Models;

namespace DXBase.Utils
{
    /// <summary>
    /// 데이터 검증 헬퍼
    /// </summary>
    public static class ValidationHelper
    {
        /// <summary>
        /// 문자열이 비어있지 않은지 검증
        /// </summary>
        public static bool IsNotEmpty(string value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        /// <summary>
        /// ModelVersion 형식 검증 (영문, 숫자, -, _ 만 허용)
        /// </summary>
        public static bool IsValidModelVersion(string modelVersion)
        {
            if (string.IsNullOrWhiteSpace(modelVersion))
                return false;

            return modelVersion.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
        }

        /// <summary>
        /// URL 형식 검증
        /// </summary>
        public static bool IsValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// 필수 필드 검증
        /// </summary>
        public static List<string> ValidateMetadata(MetadataRecord metadata)
        {
            var errors = new List<string>();

            if (!IsNotEmpty(metadata.ModelVersion))
                errors.Add("ModelVersion은 필수 입력 항목입니다.");

            if (!IsValidModelVersion(metadata.ModelVersion))
                errors.Add("ModelVersion 형식이 올바르지 않습니다.");

            if (!IsNotEmpty(metadata.ProjectName))
                errors.Add("ProjectName은 필수 입력 항목입니다.");

            if (!IsNotEmpty(metadata.CreatedBy))
                errors.Add("CreatedBy는 필수 입력 항목입니다.");

            return errors;
        }
    }
}

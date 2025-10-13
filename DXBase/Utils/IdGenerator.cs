using System;
using System.Security.Cryptography;
using System.Text;

namespace DXBase.Utils
{
    /// <summary>
    /// 고유 ID 생성 유틸리티
    /// </summary>
    public static class IdGenerator
    {
        /// <summary>
        /// InstanceGuid 또는 경로 기반 해시로 고유 ID 생성
        /// </summary>
        public static string GenerateObjectId(string instanceGuid, string category, string family, string type)
        {
            // InstanceGuid가 유효하면 그대로 사용
            if (!string.IsNullOrEmpty(instanceGuid) &&
                instanceGuid != Guid.Empty.ToString())
            {
                return instanceGuid;
            }

            // InstanceGuid가 없으면 경로 기반 해시 생성
            string path = $"{category}|{family}|{type}";
            return GenerateSHA256Hash(path);
        }

        /// <summary>
        /// SHA256 해시 생성
        /// </summary>
        private static string GenerateSHA256Hash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                byte[] hash = sha256.ComputeHash(bytes);

                // 바이트 배열을 16진수 문자열로 변환
                var builder = new StringBuilder();
                foreach (byte b in hash)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        /// <summary>
        /// ModelVersion 생성 (타임스탬프 기반)
        /// </summary>
        public static string GenerateModelVersion(string projectName)
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            return $"{projectName}_{timestamp}";
        }
    }
}

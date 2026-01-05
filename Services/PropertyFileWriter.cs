using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using DXTnavis.Models;

namespace DXTnavis.Services
{
    /// <summary>
    /// 파일 타입 열거형
    /// </summary>
    public enum FileType
    {
        CSV,
        JSON
    }

    /// <summary>
    /// 속성 데이터를 파일로 저장하는 서비스
    /// CSV 및 JSON 형식을 지원합니다.
    /// </summary>
    public class PropertyFileWriter
    {
        /// <summary>
        /// 속성 목록을 지정된 형식의 파일로 저장
        /// </summary>
        /// <param name="filePath">저장할 파일 경로</param>
        /// <param name="properties">저장할 속성 목록</param>
        /// <param name="fileType">파일 형식 (CSV 또는 JSON)</param>
        public void WriteFile(string filePath, List<PropertyInfo> properties, FileType fileType)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("파일 경로가 유효하지 않습니다.", nameof(filePath));

            if (properties == null || properties.Count == 0)
                throw new ArgumentException("저장할 속성이 없습니다.", nameof(properties));

            switch (fileType)
            {
                case FileType.CSV:
                    WriteCsvFile(filePath, properties);
                    break;

                case FileType.JSON:
                    WriteJsonFile(filePath, properties);
                    break;

                default:
                    throw new ArgumentException("지원하지 않는 파일 형식입니다.", nameof(fileType));
            }
        }

        /// <summary>
        /// CSV 형식으로 파일 저장
        /// </summary>
        private void WriteCsvFile(string filePath, List<PropertyInfo> properties)
        {
            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                // 헤더 작성
                writer.WriteLine("카테고리,속성 이름,값");

                // 데이터 작성
                foreach (var prop in properties)
                {
                    // CSV 이스케이프 처리 (쉼표, 따옴표, 줄바꿈 포함 시)
                    var category = EscapeCsvField(prop.Category);
                    var name = EscapeCsvField(prop.Name);
                    var value = EscapeCsvField(prop.Value);

                    writer.WriteLine($"{category},{name},{value}");
                }
            }
        }

        /// <summary>
        /// JSON 형식으로 파일 저장
        /// </summary>
        private void WriteJsonFile(string filePath, List<PropertyInfo> properties)
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,  // 사람이 읽기 좋은 형식
                StringEscapeHandling = StringEscapeHandling.Default  // 한글 지원
            };

            var json = JsonConvert.SerializeObject(properties, settings);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }

        /// <summary>
        /// CSV 필드 이스케이프 처리
        /// 쉼표, 따옴표, 줄바꿈이 포함된 경우 따옴표로 감싸고 내부 따옴표는 이중으로 처리
        /// </summary>
        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;

            // 쉼표, 따옴표, 줄바꿈이 포함된 경우 이스케이프 필요
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                // 내부 따옴표는 두 개로 치환
                field = field.Replace("\"", "\"\"");
                // 전체를 따옴표로 감쌈
                return $"\"{field}\"";
            }

            return field;
        }
    }
}

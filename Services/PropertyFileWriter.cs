using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        JSON,
        RefinedCSV  // DisplayString 파싱된 Refined CSV
    }

    /// <summary>
    /// 속성 데이터를 파일로 저장하는 서비스
    /// CSV, JSON, RefinedCSV 형식을 지원합니다.
    /// v0.4.0: Verbose 로깅 기능 추가
    /// </summary>
    public class PropertyFileWriter
    {
        private readonly DisplayStringParser _displayStringParser = new DisplayStringParser();
        private bool _verboseLogging = true;  // v0.4.0: 기본 활성화
        private StringBuilder _logBuffer = new StringBuilder();

        /// <summary>
        /// 속성 목록을 지정된 형식의 파일로 저장
        /// </summary>
        /// <param name="filePath">저장할 파일 경로</param>
        /// <param name="properties">저장할 속성 목록</param>
        /// <param name="fileType">파일 형식 (CSV, JSON, RefinedCSV)</param>
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

                case FileType.RefinedCSV:
                    WriteRefinedCsvFile(filePath, properties);
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
        /// Refined CSV 형식으로 파일 저장 (DisplayString 파싱 적용)
        /// 헤더: 카테고리,속성 이름,데이터 타입,원본 값,숫자 값,단위
        /// </summary>
        private void WriteRefinedCsvFile(string filePath, List<PropertyInfo> properties)
        {
            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
            {
                // 헤더 작성 (확장된 컬럼)
                writer.WriteLine("카테고리,속성 이름,데이터 타입,원본 값,숫자 값,단위,DisplayString");

                // 데이터 작성
                foreach (var prop in properties)
                {
                    // DisplayString 파싱
                    var parsed = _displayStringParser.Parse(prop.Value);

                    // CSV 이스케이프 처리
                    var category = EscapeCsvField(prop.Category);
                    var name = EscapeCsvField(prop.Name);
                    var dataType = EscapeCsvField(parsed.DataType);
                    var rawValue = EscapeCsvField(parsed.RawValue);
                    var numericValue = parsed.NumericValue.HasValue
                        ? parsed.NumericValue.Value.ToString("G")
                        : string.Empty;
                    var unit = EscapeCsvField(parsed.Unit);
                    var originalString = EscapeCsvField(parsed.OriginalString);

                    writer.WriteLine($"{category},{name},{dataType},{rawValue},{numericValue},{unit},{originalString}");
                }
            }
        }

        /// <summary>
        /// Raw CSV와 Refined CSV를 동시에 저장 (v0.4.0)
        /// filename.csv → Raw CSV
        /// filename_refined.csv → Refined CSV (DisplayString 파싱)
        /// </summary>
        /// <param name="basePath">기본 파일 경로 (확장자 포함)</param>
        /// <param name="properties">저장할 속성 목록</param>
        /// <returns>(rawPath, refinedPath) 저장된 파일 경로 튜플</returns>
        public (string rawPath, string refinedPath) WriteDualCsv(string basePath, List<PropertyInfo> properties)
        {
            if (string.IsNullOrWhiteSpace(basePath))
                throw new ArgumentException("파일 경로가 유효하지 않습니다.", nameof(basePath));

            if (properties == null || properties.Count == 0)
                throw new ArgumentException("저장할 속성이 없습니다.", nameof(properties));

            var stopwatch = Stopwatch.StartNew();
            _logBuffer.Clear();

            LogInfo("═══════════════════════════════════════════════════════════════");
            LogInfo($"DXTnavis CSV Export - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            LogInfo("═══════════════════════════════════════════════════════════════");

            // 파일 경로 생성: filename.csv → filename_refined.csv
            var directory = Path.GetDirectoryName(basePath);
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(basePath);
            var extension = Path.GetExtension(basePath);

            string rawPath = basePath;  // 원본 경로 그대로
            string refinedPath = Path.Combine(directory ?? "", $"{fileNameWithoutExt}_refined{extension}");
            string logPath = Path.Combine(directory ?? "", $"{fileNameWithoutExt}_export.log");

            LogInfo($"Input Properties: {properties.Count:N0} items");
            LogInfo($"Output Directory: {directory}");
            LogInfo("");

            // 카테고리별 통계
            var categoryStats = properties.GroupBy(p => p.Category)
                .Select(g => new { Category = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            LogInfo("Category Statistics:");
            foreach (var stat in categoryStats.Take(10))  // 상위 10개만 표시
            {
                LogInfo($"  • {stat.Category}: {stat.Count:N0}");
            }
            if (categoryStats.Count > 10)
            {
                LogInfo($"  ... and {categoryStats.Count - 10} more categories");
            }
            LogInfo("");

            // Raw CSV 저장
            var rawStopwatch = Stopwatch.StartNew();
            WriteCsvFile(rawPath, properties);
            rawStopwatch.Stop();
            var rawFileInfo = new FileInfo(rawPath);
            LogInfo($"[1/2] Raw CSV: {Path.GetFileName(rawPath)}");
            LogInfo($"      Size: {rawFileInfo.Length:N0} bytes ({rawFileInfo.Length / 1024.0:F1} KB)");
            LogInfo($"      Time: {rawStopwatch.ElapsedMilliseconds} ms");
            LogInfo("");

            // Refined CSV 저장
            var refinedStopwatch = Stopwatch.StartNew();
            WriteRefinedCsvFile(refinedPath, properties);
            refinedStopwatch.Stop();
            var refinedFileInfo = new FileInfo(refinedPath);
            LogInfo($"[2/2] Refined CSV: {Path.GetFileName(refinedPath)}");
            LogInfo($"      Size: {refinedFileInfo.Length:N0} bytes ({refinedFileInfo.Length / 1024.0:F1} KB)");
            LogInfo($"      Time: {refinedStopwatch.ElapsedMilliseconds} ms");
            LogInfo("");

            stopwatch.Stop();
            LogInfo("───────────────────────────────────────────────────────────────");
            LogInfo($"Total Time: {stopwatch.ElapsedMilliseconds} ms");
            LogInfo($"Total Size: {(rawFileInfo.Length + refinedFileInfo.Length):N0} bytes");
            LogInfo("Export completed successfully!");
            LogInfo("═══════════════════════════════════════════════════════════════");

            // 로그 파일 저장
            if (_verboseLogging)
            {
                try
                {
                    File.WriteAllText(logPath, _logBuffer.ToString(), Encoding.UTF8);
                }
                catch
                {
                    // 로그 저장 실패 시 무시
                }
            }

            return (rawPath, refinedPath);
        }

        /// <summary>
        /// 로그 메시지 기록 (v0.4.0)
        /// </summary>
        private void LogInfo(string message)
        {
            _logBuffer.AppendLine(message);
            System.Diagnostics.Debug.WriteLine($"[DXTnavis] {message}");
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

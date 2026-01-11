using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DXTnavis.Models;

namespace DXTnavis.Services
{
    /// <summary>
    /// 공정 일정 CSV 파싱 서비스
    /// Phase 8: AWP 4D Automation - CSV Parsing
    /// </summary>
    public class ScheduleCsvParser
    {
        /// <summary>
        /// 한영 컬럼 매핑 (한글 → 영문)
        /// </summary>
        private static readonly Dictionary<string, string> ColumnMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // SyncID
            { "SyncID", "SyncID" },
            { "동기화ID", "SyncID" },
            { "동기화 ID", "SyncID" },
            { "Sync ID", "SyncID" },
            { "ID", "SyncID" },
            { "객체ID", "SyncID" },
            { "ElementId", "SyncID" },
            { "Element ID", "SyncID" },
            { "UniqueId", "SyncID" },

            // TaskName
            { "TaskName", "TaskName" },
            { "작업명", "TaskName" },
            { "작업 이름", "TaskName" },
            { "Task Name", "TaskName" },
            { "Name", "TaskName" },
            { "이름", "TaskName" },
            { "공정명", "TaskName" },

            // PlannedStartDate
            { "PlannedStartDate", "PlannedStartDate" },
            { "계획시작일", "PlannedStartDate" },
            { "계획 시작일", "PlannedStartDate" },
            { "Planned Start", "PlannedStartDate" },
            { "Start Date", "PlannedStartDate" },
            { "StartDate", "PlannedStartDate" },
            { "시작일", "PlannedStartDate" },
            { "시작", "PlannedStartDate" },

            // PlannedEndDate
            { "PlannedEndDate", "PlannedEndDate" },
            { "계획종료일", "PlannedEndDate" },
            { "계획 종료일", "PlannedEndDate" },
            { "Planned End", "PlannedEndDate" },
            { "End Date", "PlannedEndDate" },
            { "EndDate", "PlannedEndDate" },
            { "종료일", "PlannedEndDate" },
            { "종료", "PlannedEndDate" },

            // ActualStartDate
            { "ActualStartDate", "ActualStartDate" },
            { "실제시작일", "ActualStartDate" },
            { "실제 시작일", "ActualStartDate" },
            { "Actual Start", "ActualStartDate" },

            // ActualEndDate
            { "ActualEndDate", "ActualEndDate" },
            { "실제종료일", "ActualEndDate" },
            { "실제 종료일", "ActualEndDate" },
            { "Actual End", "ActualEndDate" },

            // Cost
            { "Cost", "Cost" },
            { "비용", "Cost" },
            { "금액", "Cost" },
            { "예산", "Cost" },

            // TaskType
            { "TaskType", "TaskType" },
            { "작업유형", "TaskType" },
            { "작업 유형", "TaskType" },
            { "Task Type", "TaskType" },
            { "Type", "TaskType" },
            { "유형", "TaskType" },
            { "SimulationType", "TaskType" },

            // SetLevel
            { "SetLevel", "SetLevel" },
            { "그룹레벨", "SetLevel" },
            { "그룹 레벨", "SetLevel" },
            { "Level", "SetLevel" },
            { "레벨", "SetLevel" },
            { "GroupLevel", "SetLevel" },

            // ParentSet
            { "ParentSet", "ParentSet" },
            { "상위그룹", "ParentSet" },
            { "상위 그룹", "ParentSet" },
            { "Parent", "ParentSet" },
            { "부모", "ParentSet" },
            { "Zone", "ParentSet" },
            { "영역", "ParentSet" },

            // Progress
            { "Progress", "Progress" },
            { "진행률", "Progress" },
            { "진행", "Progress" },
            { "완료율", "Progress" },
            { "Percent", "Progress" },
        };

        /// <summary>
        /// 상세 로깅 활성화
        /// </summary>
        public bool VerboseLogging { get; set; } = true;

        /// <summary>
        /// 파싱 이벤트
        /// </summary>
        public event EventHandler<CsvParseEventArgs> ParseProgress;

        /// <summary>
        /// CSV 파일 파싱
        /// </summary>
        /// <param name="filePath">CSV 파일 경로</param>
        /// <returns>파싱된 스케줄 데이터 목록</returns>
        public List<ScheduleData> ParseFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"CSV 파일을 찾을 수 없습니다: {filePath}");

            // 인코딩 자동 감지
            var encoding = DetectEncoding(filePath);
            if (VerboseLogging)
                System.Diagnostics.Debug.WriteLine($"[ScheduleCsvParser] 인코딩 감지: {encoding.EncodingName}");

            var lines = File.ReadAllLines(filePath, encoding);

            if (lines.Length < 2)
                throw new InvalidDataException("CSV 파일에 데이터가 없습니다.");

            // 헤더 파싱
            var headers = ParseCsvLine(lines[0]);
            var columnIndices = MapColumns(headers);

            if (!columnIndices.ContainsKey("SyncID"))
                throw new InvalidDataException("SyncID 컬럼이 필요합니다.");

            // 데이터 파싱
            var schedules = new List<ScheduleData>();
            int errorCount = 0;

            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                try
                {
                    var values = ParseCsvLine(lines[i]);
                    var schedule = ParseRow(values, columnIndices, headers);

                    if (schedule != null)
                    {
                        schedules.Add(schedule);

                        OnParseProgress(new CsvParseEventArgs
                        {
                            CurrentRow = i,
                            TotalRows = lines.Length - 1,
                            SyncId = schedule.SyncID,
                            Success = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    if (VerboseLogging)
                        System.Diagnostics.Debug.WriteLine($"[ScheduleCsvParser] 행 {i} 파싱 오류: {ex.Message}");

                    OnParseProgress(new CsvParseEventArgs
                    {
                        CurrentRow = i,
                        TotalRows = lines.Length - 1,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            if (VerboseLogging)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ScheduleCsvParser] 파싱 완료: {schedules.Count}개 성공, {errorCount}개 오류");
            }

            return schedules;
        }

        /// <summary>
        /// CSV 라인 파싱 (쌍따옴표 처리)
        /// </summary>
        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // 이스케이프된 쌍따옴표
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString().Trim());
            return result.ToArray();
        }

        /// <summary>
        /// 컬럼 인덱스 매핑
        /// </summary>
        private Dictionary<string, int> MapColumns(string[] headers)
        {
            var mapping = new Dictionary<string, int>();

            for (int i = 0; i < headers.Length; i++)
            {
                string header = headers[i].Trim();

                if (ColumnMapping.TryGetValue(header, out string standardName))
                {
                    if (!mapping.ContainsKey(standardName))
                    {
                        mapping[standardName] = i;
                        if (VerboseLogging)
                            System.Diagnostics.Debug.WriteLine($"[ScheduleCsvParser] 컬럼 매핑: '{header}' → '{standardName}' (인덱스 {i})");
                    }
                }
            }

            return mapping;
        }

        /// <summary>
        /// 행 데이터 파싱
        /// </summary>
        private ScheduleData ParseRow(string[] values, Dictionary<string, int> columnIndices, string[] headers)
        {
            var schedule = new ScheduleData();

            // SyncID (필수)
            if (columnIndices.TryGetValue("SyncID", out int syncIdIdx) && syncIdIdx < values.Length)
            {
                schedule.SyncID = values[syncIdIdx]?.Trim();
            }

            if (string.IsNullOrEmpty(schedule.SyncID))
                return null; // SyncID가 없으면 건너뛰기

            // TaskName
            if (columnIndices.TryGetValue("TaskName", out int taskNameIdx) && taskNameIdx < values.Length)
            {
                schedule.TaskName = values[taskNameIdx]?.Trim();
            }

            // PlannedStartDate
            if (columnIndices.TryGetValue("PlannedStartDate", out int startIdx) && startIdx < values.Length)
            {
                schedule.PlannedStartDate = ParseDate(values[startIdx]);
            }

            // PlannedEndDate
            if (columnIndices.TryGetValue("PlannedEndDate", out int endIdx) && endIdx < values.Length)
            {
                schedule.PlannedEndDate = ParseDate(values[endIdx]);
            }

            // ActualStartDate
            if (columnIndices.TryGetValue("ActualStartDate", out int actualStartIdx) && actualStartIdx < values.Length)
            {
                schedule.ActualStartDate = ParseDate(values[actualStartIdx]);
            }

            // ActualEndDate
            if (columnIndices.TryGetValue("ActualEndDate", out int actualEndIdx) && actualEndIdx < values.Length)
            {
                schedule.ActualEndDate = ParseDate(values[actualEndIdx]);
            }

            // Cost
            if (columnIndices.TryGetValue("Cost", out int costIdx) && costIdx < values.Length)
            {
                schedule.Cost = ParseDecimal(values[costIdx]);
            }

            // TaskType
            if (columnIndices.TryGetValue("TaskType", out int typeIdx) && typeIdx < values.Length)
            {
                schedule.TaskType = ParseTaskType(values[typeIdx]?.Trim());
            }

            // SetLevel
            if (columnIndices.TryGetValue("SetLevel", out int levelIdx) && levelIdx < values.Length)
            {
                schedule.SetLevel = values[levelIdx]?.Trim();
            }

            // ParentSet
            if (columnIndices.TryGetValue("ParentSet", out int parentIdx) && parentIdx < values.Length)
            {
                schedule.ParentSet = values[parentIdx]?.Trim();
            }

            // Progress
            if (columnIndices.TryGetValue("Progress", out int progressIdx) && progressIdx < values.Length)
            {
                schedule.Progress = ParseDouble(values[progressIdx]) ?? 0;
            }

            // Custom Properties (매핑되지 않은 컬럼들)
            for (int i = 0; i < values.Length && i < headers.Length; i++)
            {
                string header = headers[i].Trim();

                // 이미 매핑된 컬럼은 건너뛰기
                if (ColumnMapping.ContainsKey(header))
                    continue;

                string value = values[i]?.Trim();
                if (!string.IsNullOrEmpty(value))
                {
                    schedule.CustomProperties[header] = value;
                }
            }

            return schedule;
        }

        /// <summary>
        /// 날짜 파싱 (다양한 형식 지원)
        /// </summary>
        private DateTime? ParseDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            // 지원하는 날짜 형식들
            string[] formats = new[]
            {
                "yyyy-MM-dd",
                "yyyy/MM/dd",
                "yyyy.MM.dd",
                "MM/dd/yyyy",
                "dd/MM/yyyy",
                "dd-MM-yyyy",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy/MM/dd HH:mm:ss",
                "yyyyMMdd",
            };

            if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateTime result))
            {
                return result;
            }

            // 기본 파싱 시도
            if (DateTime.TryParse(value, out result))
            {
                return result;
            }

            return null;
        }

        /// <summary>
        /// Decimal 파싱
        /// </summary>
        private decimal? ParseDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            // 통화 기호 및 천단위 구분자 제거
            value = value.Replace("₩", "").Replace("$", "").Replace(",", "").Trim();

            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal result))
            {
                return result;
            }

            return null;
        }

        /// <summary>
        /// Double 파싱
        /// </summary>
        private double? ParseDouble(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            value = value.Replace("%", "").Trim();

            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
            {
                return result;
            }

            return null;
        }

        /// <summary>
        /// TaskType 파싱 (다양한 표현 지원)
        /// </summary>
        private string ParseTaskType(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Construct";

            value = value.ToLowerInvariant();

            // Construct
            if (value.Contains("construct") || value.Contains("build") ||
                value.Contains("시공") || value.Contains("설치") || value.Contains("조립"))
                return "Construct";

            // Demolish
            if (value.Contains("demolish") || value.Contains("remove") ||
                value.Contains("철거") || value.Contains("해체") || value.Contains("제거"))
                return "Demolish";

            // Temporary
            if (value.Contains("temp") || value.Contains("temporary") ||
                value.Contains("임시") || value.Contains("가설"))
                return "Temporary";

            return "Construct"; // 기본값
        }

        /// <summary>
        /// 인코딩 자동 감지
        /// </summary>
        private Encoding DetectEncoding(string filePath)
        {
            using (var reader = new StreamReader(filePath, Encoding.Default, true))
            {
                reader.Peek(); // 인코딩 감지 트리거
                return reader.CurrentEncoding;
            }
        }

        /// <summary>
        /// 검증 결과 반환
        /// </summary>
        public ValidationResult ValidateFile(string filePath)
        {
            var result = new ValidationResult { ValidationType = ValidationType.CsvFile };

            if (!File.Exists(filePath))
            {
                result.AddError(ValidationErrorCode.FileNotFound, $"파일을 찾을 수 없습니다: {filePath}");
                return result;
            }

            try
            {
                var schedules = ParseFile(filePath);
                result.ScheduleDataCount = schedules.Count;

                int validCount = 0;
                foreach (var schedule in schedules)
                {
                    if (schedule.IsValid())
                    {
                        validCount++;
                    }
                    else
                    {
                        result.AddWarning(ValidationWarningCode.MissingOptionalData,
                            schedule.GetValidationErrors(), schedule.SyncID);
                    }
                }

                result.ValidScheduleCount = validCount;

                if (result.ScheduleDataCount == 0)
                {
                    result.AddError(ValidationErrorCode.EmptyFile, "유효한 스케줄 데이터가 없습니다.");
                }
                else
                {
                    result.AddInfo($"총 {result.ScheduleDataCount}개 레코드 중 {result.ValidScheduleCount}개 유효");
                }
            }
            catch (Exception ex)
            {
                result.AddError(ValidationErrorCode.FileReadError, ex.Message);
            }

            return result;
        }

        protected virtual void OnParseProgress(CsvParseEventArgs e)
        {
            ParseProgress?.Invoke(this, e);
        }
    }

    /// <summary>
    /// CSV 파싱 이벤트 인자
    /// </summary>
    public class CsvParseEventArgs : EventArgs
    {
        public int CurrentRow { get; set; }
        public int TotalRows { get; set; }
        public string SyncId { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public double Progress => TotalRows > 0 ? (double)CurrentRow / TotalRows * 100 : 0;
    }
}

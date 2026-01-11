using System;
using System.Collections.Generic;
using System.Linq;

namespace DXTnavis.Models
{
    /// <summary>
    /// AWP 4D 자동화 실행 결과
    /// 전체 파이프라인 실행 결과를 저장합니다.
    /// </summary>
    public class AutomationResult
    {
        /// <summary>
        /// 실행 시작 시간
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 실행 종료 시간
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// 총 실행 시간
        /// </summary>
        public TimeSpan ElapsedTime => EndTime - StartTime;

        /// <summary>
        /// 전체 성공 여부
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 실패 시 오류 메시지
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// 드라이런 모드 여부
        /// </summary>
        public bool IsDryRun { get; set; }

        #region Phase Results

        /// <summary>
        /// Phase 1: Property Write 결과
        /// </summary>
        public PropertyWriteResult PropertyWriteResult { get; set; } = new PropertyWriteResult();

        /// <summary>
        /// Phase 2: Object Matching 결과
        /// </summary>
        public ObjectMatchResult ObjectMatchResult { get; set; } = new ObjectMatchResult();

        /// <summary>
        /// Phase 3: Selection Set 생성 결과
        /// </summary>
        public SelectionSetResult SelectionSetResult { get; set; } = new SelectionSetResult();

        /// <summary>
        /// Phase 4: TimeLiner Task 생성 결과
        /// </summary>
        public TimeLinerResult TimeLinerResult { get; set; } = new TimeLinerResult();

        #endregion

        /// <summary>
        /// 상세 로그 메시지
        /// </summary>
        public List<LogEntry> Logs { get; set; } = new List<LogEntry>();

        /// <summary>
        /// 전체 처리 항목 수
        /// </summary>
        public int TotalItemsProcessed => PropertyWriteResult.TotalCount;

        /// <summary>
        /// 전체 성공 항목 수
        /// </summary>
        public int TotalItemsSucceeded => PropertyWriteResult.SuccessCount;

        /// <summary>
        /// 전체 실패 항목 수
        /// </summary>
        public int TotalItemsFailed => PropertyWriteResult.FailedCount;

        /// <summary>
        /// 전체 성공률
        /// </summary>
        public double OverallSuccessRate
        {
            get
            {
                if (TotalItemsProcessed == 0) return 0;
                return (double)TotalItemsSucceeded / TotalItemsProcessed * 100;
            }
        }

        /// <summary>
        /// 로그 추가
        /// </summary>
        public void AddLog(LogLevel level, string message, string phase = null)
        {
            Logs.Add(new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Phase = phase,
                Message = message
            });
        }

        /// <summary>
        /// 요약 보고서 생성
        /// </summary>
        public string GenerateSummary()
        {
            var lines = new List<string>
            {
                "═══════════════════════════════════════════════════════════════",
                "                    AWP 4D Automation Report                    ",
                "═══════════════════════════════════════════════════════════════",
                $"실행 시간: {StartTime:yyyy-MM-dd HH:mm:ss} ~ {EndTime:HH:mm:ss}",
                $"소요 시간: {ElapsedTime.TotalSeconds:F1}초",
                $"드라이런: {(IsDryRun ? "예" : "아니오")}",
                $"결과: {(Success ? "✅ 성공" : "❌ 실패")}",
                "",
                "───────────────────────────────────────────────────────────────",
                "                        Phase 결과 요약                         ",
                "───────────────────────────────────────────────────────────────",
                "",
                "📊 Object Matching:",
                $"   - 전체: {ObjectMatchResult.TotalCount}",
                $"   - 성공: {ObjectMatchResult.MatchedCount} ({ObjectMatchResult.MatchRate:F1}%)",
                $"   - 실패: {ObjectMatchResult.NotFoundCount}",
                "",
                "📝 Property Write:",
                $"   - 전체: {PropertyWriteResult.TotalCount}",
                $"   - 성공: {PropertyWriteResult.SuccessCount}",
                $"   - 실패: {PropertyWriteResult.FailedCount}",
                "",
                "📁 Selection Sets:",
                $"   - 생성된 폴더: {SelectionSetResult.FolderCount}",
                $"   - 생성된 세트: {SelectionSetResult.SetCount}",
                "",
                "⏱️ TimeLiner Tasks:",
                $"   - 생성된 폴더: {TimeLinerResult.FolderCount}",
                $"   - 생성된 태스크: {TimeLinerResult.TaskCount}",
                $"   - 연결된 Selection: {TimeLinerResult.LinkedCount}",
                "",
                "═══════════════════════════════════════════════════════════════"
            };

            if (!Success && !string.IsNullOrEmpty(ErrorMessage))
            {
                lines.Add($"오류: {ErrorMessage}");
                lines.Add("═══════════════════════════════════════════════════════════════");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>
    /// Property Write 결과
    /// </summary>
    public class PropertyWriteResult
    {
        public int TotalCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public int SkippedCount { get; set; }
        public List<string> FailedItems { get; set; } = new List<string>();
        public double SuccessRate => TotalCount > 0 ? (double)SuccessCount / TotalCount * 100 : 0;
    }

    /// <summary>
    /// Object Matching 결과
    /// </summary>
    public class ObjectMatchResult
    {
        public int TotalCount { get; set; }
        public int MatchedCount { get; set; }
        public int NotFoundCount { get; set; }
        public int MultipleMatchCount { get; set; }
        public int ErrorCount { get; set; }
        public List<string> NotFoundSyncIds { get; set; } = new List<string>();
        public double MatchRate => TotalCount > 0 ? (double)MatchedCount / TotalCount * 100 : 0;
    }

    /// <summary>
    /// Selection Set 생성 결과
    /// </summary>
    public class SelectionSetResult
    {
        public int FolderCount { get; set; }
        public int SetCount { get; set; }
        public int TotalItemCount { get; set; }
        public List<string> CreatedSets { get; set; } = new List<string>();
        public List<string> FailedSets { get; set; } = new List<string>();
    }

    /// <summary>
    /// TimeLiner Task 생성 결과
    /// </summary>
    public class TimeLinerResult
    {
        public int FolderCount { get; set; }
        public int TaskCount { get; set; }
        public int LinkedCount { get; set; }
        public int UnlinkedCount { get; set; }
        public List<string> CreatedTasks { get; set; } = new List<string>();
        public List<string> FailedTasks { get; set; } = new List<string>();
    }

    /// <summary>
    /// 로그 항목
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Phase { get; set; }
        public string Message { get; set; }

        public override string ToString()
        {
            string levelStr;
            switch (Level)
            {
                case LogLevel.Debug: levelStr = "DEBUG"; break;
                case LogLevel.Info: levelStr = "INFO "; break;
                case LogLevel.Warning: levelStr = "WARN "; break;
                case LogLevel.Error: levelStr = "ERROR"; break;
                default: levelStr = "     "; break;
            }

            string phaseStr = string.IsNullOrEmpty(Phase) ? "" : $"[{Phase}] ";
            return $"[{Timestamp:HH:mm:ss}] {levelStr} {phaseStr}{Message}";
        }
    }

    /// <summary>
    /// 로그 레벨
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}

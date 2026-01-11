using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autodesk.Navisworks.Api;
using DXTnavis.Models;

namespace DXTnavis.Services
{
    /// <summary>
    /// AWP 4D 자동화 통합 파이프라인 서비스
    /// Phase 8: AWP 4D Automation - Integration Pipeline
    /// CSV → Property → Set → Task 전체 워크플로우를 관리합니다.
    /// </summary>
    public class AWP4DAutomationService
    {
        private readonly ScheduleCsvParser _csvParser;
        private readonly ObjectMatcher _objectMatcher;
        private readonly PropertyWriteService _propertyWriter;
        private readonly SelectionSetService _selectionSetService;
        private readonly TimeLinerService _timeLinerService;
        private readonly AWP4DValidator _validator;

        /// <summary>
        /// 파이프라인 진행 이벤트
        /// </summary>
        public event EventHandler<PipelineProgressEventArgs> ProgressChanged;

        /// <summary>
        /// 상세 로깅
        /// </summary>
        public bool VerboseLogging { get; set; } = true;

        public AWP4DAutomationService()
        {
            _csvParser = new ScheduleCsvParser { VerboseLogging = VerboseLogging };
            _objectMatcher = new ObjectMatcher { VerboseLogging = VerboseLogging };
            _propertyWriter = new PropertyWriteService { VerboseLogging = VerboseLogging };
            _selectionSetService = new SelectionSetService { VerboseLogging = VerboseLogging };
            _timeLinerService = new TimeLinerService(_selectionSetService) { VerboseLogging = VerboseLogging };
            _validator = new AWP4DValidator { VerboseLogging = VerboseLogging };
        }

        /// <summary>
        /// 전체 자동화 파이프라인 실행
        /// </summary>
        /// <param name="csvPath">CSV 파일 경로</param>
        /// <param name="options">자동화 옵션</param>
        /// <param name="cancellationToken">취소 토큰</param>
        /// <returns>실행 결과</returns>
        public AutomationResult ExecutePipeline(string csvPath, AWP4DOptions options, CancellationToken cancellationToken = default)
        {
            var result = new AutomationResult
            {
                StartTime = DateTime.Now,
                IsDryRun = options.DryRun
            };

            try
            {
                // ═══════════════════════════════════════════════════════════
                // Pre-Validation
                // ═══════════════════════════════════════════════════════════
                result.AddLog(LogLevel.Info, "파이프라인 시작", "Pipeline");

                if (options.EnablePreValidation)
                {
                    OnProgress(PipelinePhase.Validation, 0, "사전 검증 중...");

                    var preValidation = _validator.ValidatePreConditions(csvPath, options);
                    if (!preValidation.IsValid)
                    {
                        result.Success = false;
                        result.ErrorMessage = "사전 검증 실패: " + string.Join("; ",
                            preValidation.Errors.Select(e => e.Message));
                        result.AddLog(LogLevel.Error, result.ErrorMessage, "Validation");
                        return FinalizeResult(result);
                    }

                    result.AddLog(LogLevel.Info, "사전 검증 통과", "Validation");
                }

                cancellationToken.ThrowIfCancellationRequested();

                // ═══════════════════════════════════════════════════════════
                // Phase 1: CSV Parsing
                // ═══════════════════════════════════════════════════════════
                OnProgress(PipelinePhase.CsvParsing, 5, "CSV 파싱 중...");
                result.AddLog(LogLevel.Info, $"CSV 파일 로드: {csvPath}", "CSV");

                List<ScheduleData> schedules;
                try
                {
                    schedules = _csvParser.ParseFile(csvPath);
                    result.AddLog(LogLevel.Info, $"{schedules.Count}개 스케줄 데이터 로드됨", "CSV");
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = $"CSV 파싱 실패: {ex.Message}";
                    result.AddLog(LogLevel.Error, result.ErrorMessage, "CSV");
                    return FinalizeResult(result);
                }

                cancellationToken.ThrowIfCancellationRequested();

                // ═══════════════════════════════════════════════════════════
                // Phase 2: Object Matching
                // ═══════════════════════════════════════════════════════════
                OnProgress(PipelinePhase.ObjectMatching, 15, "객체 매칭 중...");
                result.AddLog(LogLevel.Info, "객체 매칭 시작", "Matching");

                // 캐시 구축 (대량 데이터 시 성능 향상)
                if (schedules.Count > 100)
                {
                    result.AddLog(LogLevel.Debug, "매칭 캐시 구축 중...", "Matching");
                    _objectMatcher.BuildPropertyCache(options);
                }

                var matchResult = _objectMatcher.MatchAll(schedules, options);
                result.ObjectMatchResult = matchResult;

                result.AddLog(LogLevel.Info,
                    $"매칭 완료: {matchResult.MatchedCount}/{matchResult.TotalCount} ({matchResult.MatchRate:F1}%)",
                    "Matching");

                // 최소 매칭률 검사
                if (matchResult.MatchRate < options.MinMatchSuccessRate)
                {
                    result.Success = false;
                    result.ErrorMessage = $"매칭률 부족: {matchResult.MatchRate:F1}% (최소 {options.MinMatchSuccessRate}%)";
                    result.AddLog(LogLevel.Error, result.ErrorMessage, "Matching");

                    if (!options.ContinueOnError)
                        return FinalizeResult(result);
                }

                cancellationToken.ThrowIfCancellationRequested();

                // 드라이런이면 여기서 종료
                if (options.DryRun)
                {
                    result.Success = true;
                    result.AddLog(LogLevel.Info, "드라이런 완료 - 실제 변경 없음", "Pipeline");
                    return FinalizeResult(result);
                }

                // ═══════════════════════════════════════════════════════════
                // Phase 3: Property Write
                // ═══════════════════════════════════════════════════════════
                if (options.EnablePropertyWrite)
                {
                    OnProgress(PipelinePhase.PropertyWrite, 30, "속성 기입 중...");
                    result.AddLog(LogLevel.Info, "Property Write 시작", "PropertyWrite");

                    try
                    {
                        var writeResult = _propertyWriter.WriteBatch(schedules, options);
                        result.PropertyWriteResult = writeResult;

                        result.AddLog(LogLevel.Info,
                            $"Property Write 완료: {writeResult.SuccessCount}/{writeResult.TotalCount}",
                            "PropertyWrite");
                    }
                    catch (Exception ex)
                    {
                        result.AddLog(LogLevel.Error, $"Property Write 오류: {ex.Message}", "PropertyWrite");

                        if (!options.ContinueOnError)
                        {
                            result.Success = false;
                            result.ErrorMessage = ex.Message;
                            return FinalizeResult(result);
                        }
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // ═══════════════════════════════════════════════════════════
                // Phase 4: Selection Set Creation
                // ═══════════════════════════════════════════════════════════
                Dictionary<string, string> syncIdToSetName = new Dictionary<string, string>();

                if (options.EnableSelectionSetCreation)
                {
                    OnProgress(PipelinePhase.SelectionSetCreation, 50, "Selection Set 생성 중...");
                    result.AddLog(LogLevel.Info, "Selection Set 생성 시작", "SelectionSet");

                    try
                    {
                        var setResult = _selectionSetService.CreateHierarchicalSets(schedules, options);
                        result.SelectionSetResult = setResult;

                        // SyncID → SetName 매핑 구축
                        foreach (var schedule in schedules.Where(s => s.MatchStatus == MatchStatus.Matched))
                        {
                            // ParentSet 또는 그룹화 기준에 따라 SetName 결정
                            string setKey = schedule.ParentSet ?? schedule.SyncID;
                            var parts = setKey.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                            string setName = parts.Length > 0 ? parts[parts.Length - 1] : setKey;

                            if (!syncIdToSetName.ContainsKey(schedule.SyncID))
                            {
                                syncIdToSetName[schedule.SyncID] = setName;
                            }
                        }

                        result.AddLog(LogLevel.Info,
                            $"Selection Set 완료: {setResult.SetCount}개 세트, {setResult.FolderCount}개 폴더",
                            "SelectionSet");
                    }
                    catch (Exception ex)
                    {
                        result.AddLog(LogLevel.Error, $"Selection Set 오류: {ex.Message}", "SelectionSet");

                        if (!options.ContinueOnError)
                        {
                            result.Success = false;
                            result.ErrorMessage = ex.Message;
                            return FinalizeResult(result);
                        }
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // ═══════════════════════════════════════════════════════════
                // Phase 5: TimeLiner Task Creation
                // ═══════════════════════════════════════════════════════════
                if (options.EnableTimeLinerTaskCreation)
                {
                    OnProgress(PipelinePhase.TimeLinerTaskCreation, 70, "TimeLiner Task 생성 중...");
                    result.AddLog(LogLevel.Info, "TimeLiner Task 생성 시작", "TimeLiner");

                    try
                    {
                        var timelineResult = _timeLinerService.CreateTasks(schedules, syncIdToSetName, options);
                        result.TimeLinerResult = timelineResult;

                        result.AddLog(LogLevel.Info,
                            $"TimeLiner 완료: {timelineResult.TaskCount}개 Task, {timelineResult.LinkedCount}개 연결됨",
                            "TimeLiner");
                    }
                    catch (Exception ex)
                    {
                        result.AddLog(LogLevel.Error, $"TimeLiner 오류: {ex.Message}", "TimeLiner");

                        if (!options.ContinueOnError)
                        {
                            result.Success = false;
                            result.ErrorMessage = ex.Message;
                            return FinalizeResult(result);
                        }
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // ═══════════════════════════════════════════════════════════
                // Post-Validation
                // ═══════════════════════════════════════════════════════════
                if (options.EnablePostValidation)
                {
                    OnProgress(PipelinePhase.PostValidation, 90, "결과 검증 중...");

                    var postValidation = _validator.ValidatePostConditions(result, options);
                    if (!postValidation.IsValid)
                    {
                        result.AddLog(LogLevel.Warning, "사후 검증 경고: " +
                            string.Join("; ", postValidation.Warnings.Select(w => w.Message)), "Validation");
                    }
                    else
                    {
                        result.AddLog(LogLevel.Info, "사후 검증 통과", "Validation");
                    }
                }

                // ═══════════════════════════════════════════════════════════
                // Complete
                // ═══════════════════════════════════════════════════════════
                result.Success = true;
                OnProgress(PipelinePhase.Complete, 100, "완료");
                result.AddLog(LogLevel.Info, "파이프라인 완료", "Pipeline");
            }
            catch (OperationCanceledException)
            {
                result.Success = false;
                result.ErrorMessage = "사용자에 의해 취소됨";
                result.AddLog(LogLevel.Warning, result.ErrorMessage, "Pipeline");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.AddLog(LogLevel.Error, $"예상치 못한 오류: {ex.Message}", "Pipeline");
            }

            return FinalizeResult(result);
        }

        /// <summary>
        /// 결과 마무리
        /// </summary>
        private AutomationResult FinalizeResult(AutomationResult result)
        {
            result.EndTime = DateTime.Now;

            if (VerboseLogging)
            {
                System.Diagnostics.Debug.WriteLine(result.GenerateSummary());
            }

            return result;
        }

        /// <summary>
        /// 기존 AWP 데이터 정리
        /// </summary>
        /// <param name="options">옵션</param>
        /// <returns>삭제된 항목 수</returns>
        public (int deletedSets, int deletedTasks) ClearExistingData(AWP4DOptions options)
        {
            int deletedSets = 0;
            int deletedTasks = 0;

            try
            {
                // Selection Set 삭제
                deletedSets = _selectionSetService.ClearSelectionSets(options.SelectionSetRootFolder);

                // TimeLiner Task 삭제
                deletedTasks = _timeLinerService.ClearTasks(options.TimeLinerRootFolder);

                if (VerboseLogging)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[AWP4DAutomation] 정리 완료: {deletedSets}개 세트, {deletedTasks}개 태스크 삭제됨");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AWP4DAutomation] 정리 오류: {ex.Message}");
            }

            return (deletedSets, deletedTasks);
        }

        /// <summary>
        /// CSV 파일 미리보기
        /// </summary>
        public List<ScheduleData> PreviewCsvFile(string csvPath, int maxRows = 10)
        {
            var schedules = _csvParser.ParseFile(csvPath);
            return schedules.Take(maxRows).ToList();
        }

        /// <summary>
        /// CSV 파일 검증
        /// </summary>
        public ValidationResult ValidateCsvFile(string csvPath)
        {
            return _csvParser.ValidateFile(csvPath);
        }

        /// <summary>
        /// 매칭 테스트 (드라이런)
        /// </summary>
        public ObjectMatchResult TestMatching(string csvPath, AWP4DOptions options)
        {
            var schedules = _csvParser.ParseFile(csvPath);
            return _objectMatcher.MatchAll(schedules, options);
        }

        /// <summary>
        /// 캐시 초기화
        /// </summary>
        public void ClearAllCaches()
        {
            _objectMatcher.ClearCache();
            _selectionSetService.ClearCache();
            _timeLinerService.ClearCache();
        }

        protected virtual void OnProgress(PipelinePhase phase, int progress, string message)
        {
            ProgressChanged?.Invoke(this, new PipelineProgressEventArgs
            {
                Phase = phase,
                Progress = progress,
                Message = message
            });
        }
    }

    /// <summary>
    /// 파이프라인 단계
    /// </summary>
    public enum PipelinePhase
    {
        Validation,
        CsvParsing,
        ObjectMatching,
        PropertyWrite,
        SelectionSetCreation,
        TimeLinerTaskCreation,
        PostValidation,
        Complete
    }

    /// <summary>
    /// 파이프라인 진행 이벤트 인자
    /// </summary>
    public class PipelineProgressEventArgs : EventArgs
    {
        public PipelinePhase Phase { get; set; }
        public int Progress { get; set; }
        public string Message { get; set; }
    }
}

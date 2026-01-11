using System;
using System.IO;
using System.Linq;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Timeliner;
using DXTnavis.Models;

namespace DXTnavis.Services
{
    /// <summary>
    /// AWP 4D 자동화 검증 서비스
    /// Phase 8: AWP 4D Automation - Validation
    /// </summary>
    public class AWP4DValidator
    {
        /// <summary>
        /// 상세 로깅
        /// </summary>
        public bool VerboseLogging { get; set; } = true;

        /// <summary>
        /// 실행 전 검증 (Pre-Validation)
        /// </summary>
        /// <param name="csvPath">CSV 파일 경로</param>
        /// <param name="options">옵션</param>
        /// <returns>검증 결과</returns>
        public ValidationResult ValidatePreConditions(string csvPath, AWP4DOptions options)
        {
            var result = new ValidationResult { ValidationType = ValidationType.Pre };

            // 1. Navisworks 문서 검증
            var doc = Application.ActiveDocument;
            if (doc == null)
            {
                result.AddError(ValidationErrorCode.NoActiveDocument, "활성화된 Navisworks 문서가 없습니다.");
                return result;
            }

            // 모델 로드 확인
            if (doc.Models == null || doc.Models.Count == 0)
            {
                result.AddError(ValidationErrorCode.NoModelsLoaded, "로드된 모델이 없습니다.");
                return result;
            }

            result.AddInfo($"문서: {doc.Title ?? "Untitled"}");
            result.AddInfo($"모델 수: {doc.Models.Count}");

            // 2. CSV 파일 검증
            if (string.IsNullOrEmpty(csvPath))
            {
                result.AddError(ValidationErrorCode.FileNotFound, "CSV 파일 경로가 지정되지 않았습니다.");
                return result;
            }

            if (!File.Exists(csvPath))
            {
                result.AddError(ValidationErrorCode.FileNotFound, $"CSV 파일을 찾을 수 없습니다: {csvPath}");
                return result;
            }

            // 파일 읽기 가능 여부
            try
            {
                using (var stream = File.OpenRead(csvPath))
                {
                    // 파일 접근 가능
                }
            }
            catch (Exception ex)
            {
                result.AddError(ValidationErrorCode.FileReadError, $"CSV 파일을 읽을 수 없습니다: {ex.Message}");
                return result;
            }

            // 파일 크기 확인
            var fileInfo = new FileInfo(csvPath);
            result.AddInfo($"CSV 파일: {fileInfo.Name} ({fileInfo.Length / 1024:N0} KB)");

            if (fileInfo.Length == 0)
            {
                result.AddError(ValidationErrorCode.EmptyFile, "CSV 파일이 비어있습니다.");
                return result;
            }

            // 대용량 파일 경고
            if (fileInfo.Length > 10 * 1024 * 1024) // 10MB
            {
                result.AddWarning(ValidationWarningCode.LargeDataSet,
                    $"대용량 파일입니다 ({fileInfo.Length / 1024 / 1024:N1} MB). 처리 시간이 오래 걸릴 수 있습니다.");
            }

            // 3. CSV 내용 검증
            try
            {
                var parser = new ScheduleCsvParser { VerboseLogging = false };
                var schedules = parser.ParseFile(csvPath);

                result.ScheduleDataCount = schedules.Count;
                result.ValidScheduleCount = schedules.Count(s => s.IsValid());

                if (result.ScheduleDataCount == 0)
                {
                    result.AddError(ValidationErrorCode.NoValidScheduleData, "유효한 스케줄 데이터가 없습니다.");
                    return result;
                }

                result.AddInfo($"스케줄 데이터: {result.ValidScheduleCount}/{result.ScheduleDataCount} 유효");

                // SyncID 중복 검사
                var duplicates = schedules
                    .Where(s => !string.IsNullOrEmpty(s.SyncID))
                    .GroupBy(s => s.SyncID)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                if (duplicates.Any())
                {
                    result.AddWarning(ValidationWarningCode.MissingOptionalData,
                        $"중복 SyncID 발견: {string.Join(", ", duplicates.Take(5))}" +
                        (duplicates.Count > 5 ? $" 외 {duplicates.Count - 5}건" : ""));
                }

                // 날짜 범위 검사
                var validDates = schedules.Where(s => s.PlannedStartDate.HasValue && s.PlannedEndDate.HasValue);
                var invalidDates = validDates.Where(s => s.PlannedStartDate > s.PlannedEndDate).ToList();

                if (invalidDates.Any())
                {
                    result.AddWarning(ValidationWarningCode.MissingOptionalData,
                        $"날짜 범위 오류: {invalidDates.Count}건 (시작일 > 종료일)");
                }
            }
            catch (Exception ex)
            {
                result.AddError(ValidationErrorCode.InvalidDataFormat, $"CSV 파싱 오류: {ex.Message}");
                return result;
            }

            // 4. 옵션 검증
            if (!options.Validate(out string optionError))
            {
                result.AddError(ValidationErrorCode.InvalidOptions, optionError);
                return result;
            }

            // 5. TimeLiner 사용 가능 여부
            if (options.EnableTimeLinerTaskCreation)
            {
                try
                {
                    var timeliner = doc.GetTimeliner();
                    if (timeliner == null)
                    {
                        result.AddWarning(ValidationWarningCode.MissingOptionalData,
                            "TimeLiner를 사용할 수 없습니다. Task 생성이 비활성화됩니다.");
                    }
                }
                catch (Exception ex)
                {
                    result.AddWarning(ValidationWarningCode.MissingOptionalData,
                        $"TimeLiner 확인 실패: {ex.Message}");
                }
            }

            if (VerboseLogging)
            {
                System.Diagnostics.Debug.WriteLine($"[AWP4DValidator] Pre-Validation: {(result.IsValid ? "통과" : "실패")}");
            }

            return result;
        }

        /// <summary>
        /// 실행 후 검증 (Post-Validation)
        /// </summary>
        /// <param name="automationResult">실행 결과</param>
        /// <param name="options">옵션</param>
        /// <returns>검증 결과</returns>
        public ValidationResult ValidatePostConditions(AutomationResult automationResult, AWP4DOptions options)
        {
            var result = new ValidationResult { ValidationType = ValidationType.Post };

            // 1. 매칭 결과 검증
            if (automationResult.ObjectMatchResult != null)
            {
                var matchResult = automationResult.ObjectMatchResult;

                if (matchResult.MatchRate < options.MinMatchSuccessRate)
                {
                    result.AddWarning(ValidationWarningCode.LowConfidenceMatch,
                        $"매칭률이 목표치 미달: {matchResult.MatchRate:F1}% (목표: {options.MinMatchSuccessRate}%)");
                }

                if (matchResult.NotFoundCount > 0)
                {
                    result.AddInfo($"매칭 실패: {matchResult.NotFoundCount}개 항목");
                }
            }

            // 2. Property Write 결과 검증
            if (options.EnablePropertyWrite && automationResult.PropertyWriteResult != null)
            {
                var writeResult = automationResult.PropertyWriteResult;

                if (writeResult.FailedCount > 0)
                {
                    result.AddWarning(ValidationWarningCode.MissingOptionalData,
                        $"Property Write 실패: {writeResult.FailedCount}개");
                }

                result.AddInfo($"Property Write: {writeResult.SuccessCount}/{writeResult.TotalCount} 성공");
            }

            // 3. Selection Set 결과 검증
            if (options.EnableSelectionSetCreation && automationResult.SelectionSetResult != null)
            {
                var setResult = automationResult.SelectionSetResult;

                if (setResult.FailedSets.Any())
                {
                    result.AddWarning(ValidationWarningCode.MissingOptionalData,
                        $"Selection Set 실패: {setResult.FailedSets.Count}개");
                }

                result.AddInfo($"Selection Set: {setResult.SetCount}개 생성, {setResult.TotalItemCount}개 객체 포함");
            }

            // 4. TimeLiner 결과 검증
            if (options.EnableTimeLinerTaskCreation && automationResult.TimeLinerResult != null)
            {
                var timelineResult = automationResult.TimeLinerResult;

                if (timelineResult.FailedTasks.Any())
                {
                    result.AddWarning(ValidationWarningCode.MissingOptionalData,
                        $"TimeLiner Task 실패: {timelineResult.FailedTasks.Count}개");
                }

                if (timelineResult.UnlinkedCount > 0)
                {
                    result.AddWarning(ValidationWarningCode.MissingOptionalData,
                        $"Selection 미연결 Task: {timelineResult.UnlinkedCount}개");
                }

                result.AddInfo($"TimeLiner: {timelineResult.TaskCount}개 Task 생성, {timelineResult.LinkedCount}개 연결됨");
            }

            // 5. 전체 성공률 계산
            int totalItems = automationResult.TotalItemsProcessed;
            int successItems = automationResult.TotalItemsSucceeded;

            if (totalItems > 0)
            {
                double successRate = (double)successItems / totalItems * 100;
                result.AddInfo($"전체 성공률: {successRate:F1}% ({successItems}/{totalItems})");
            }

            // 6. 소요 시간 검증
            if (automationResult.ElapsedTime.TotalMinutes > 5)
            {
                result.AddWarning(ValidationWarningCode.SlowOperation,
                    $"처리 시간이 오래 걸렸습니다: {automationResult.ElapsedTime.TotalMinutes:F1}분");
            }

            if (VerboseLogging)
            {
                System.Diagnostics.Debug.WriteLine($"[AWP4DValidator] Post-Validation: {(result.IsValid ? "통과" : "실패")}");
            }

            return result;
        }

        /// <summary>
        /// 옵션 검증
        /// </summary>
        public ValidationResult ValidateOptions(AWP4DOptions options)
        {
            var result = new ValidationResult { ValidationType = ValidationType.Options };

            if (options == null)
            {
                result.AddError(ValidationErrorCode.InvalidOptions, "옵션이 null입니다.");
                return result;
            }

            if (!options.Validate(out string errorMessage))
            {
                result.AddError(ValidationErrorCode.InvalidOptions, errorMessage);
            }

            return result;
        }

        /// <summary>
        /// 단일 스케줄 데이터 검증
        /// </summary>
        public (bool IsValid, string ErrorMessage) ValidateScheduleData(ScheduleData schedule)
        {
            if (schedule == null)
                return (false, "스케줄 데이터가 null입니다.");

            if (!schedule.IsValid())
                return (false, schedule.GetValidationErrors());

            return (true, null);
        }

        /// <summary>
        /// Navisworks 환경 검증
        /// </summary>
        public ValidationResult ValidateEnvironment()
        {
            var result = new ValidationResult { ValidationType = ValidationType.Pre };

            // 문서 확인
            var doc = Application.ActiveDocument;
            if (doc == null)
            {
                result.AddError(ValidationErrorCode.NoActiveDocument, "활성화된 문서가 없습니다.");
                return result;
            }

            result.AddInfo($"Navisworks 버전: {Application.Version}");
            result.AddInfo($"문서: {doc.Title ?? "Untitled"}");

            // 모델 확인
            if (doc.Models.Count == 0)
            {
                result.AddError(ValidationErrorCode.NoModelsLoaded, "로드된 모델이 없습니다.");
                return result;
            }

            result.AddInfo($"로드된 모델: {doc.Models.Count}개");

            // 객체 수 확인
            int totalItems = 0;
            foreach (var model in doc.Models)
            {
                totalItems += CountModelItems(model.RootItem);
            }
            result.AddInfo($"총 객체 수: {totalItems:N0}");

            // TimeLiner 확인
            try
            {
                var timeliner = doc.GetTimeliner();
                if (timeliner != null)
                {
                    result.AddInfo("TimeLiner: 사용 가능");
                }
                else
                {
                    result.AddWarning(ValidationWarningCode.MissingOptionalData, "TimeLiner를 사용할 수 없습니다.");
                }
            }
            catch
            {
                result.AddWarning(ValidationWarningCode.MissingOptionalData, "TimeLiner 상태를 확인할 수 없습니다.");
            }

            // Selection Sets 확인
            var selectionSets = doc.SelectionSets;
            if (selectionSets != null)
            {
                int setCount = CountSelectionSets(selectionSets.Value);
                result.AddInfo($"기존 Selection Set: {setCount}개");
            }

            return result;
        }

        /// <summary>
        /// ModelItem 수 카운트 (재귀)
        /// </summary>
        private int CountModelItems(ModelItem item)
        {
            if (item == null)
                return 0;

            int count = item.HasGeometry ? 1 : 0;
            foreach (ModelItem child in item.Children)
            {
                count += CountModelItems(child);
            }
            return count;
        }

        /// <summary>
        /// Selection Set 수 카운트 (재귀)
        /// </summary>
        private int CountSelectionSets(SavedItemCollection items)
        {
            int count = 0;
            foreach (SavedItem item in items)
            {
                if (item is SelectionSet)
                    count++;
                else if (item is FolderItem folder)
                    count += CountSelectionSets(folder.Children);
            }
            return count;
        }
    }
}

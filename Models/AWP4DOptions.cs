using System;
using System.Collections.Generic;

namespace DXTnavis.Models
{
    /// <summary>
    /// AWP 4D 자동화 옵션
    /// 자동화 파이프라인의 동작 방식을 설정합니다.
    /// </summary>
    public class AWP4DOptions
    {
        #region Property Write Options

        /// <summary>
        /// Property 기입 활성화 여부
        /// </summary>
        public bool EnablePropertyWrite { get; set; } = true;

        /// <summary>
        /// 기입할 Property 카테고리 이름
        /// </summary>
        public string PropertyCategoryName { get; set; } = "AWP Schedule";

        /// <summary>
        /// 기입할 Property 내부 이름
        /// </summary>
        public string PropertyCategoryInternalName { get; set; } = "AWP_Schedule";

        /// <summary>
        /// 기존 Property 덮어쓰기 여부
        /// </summary>
        public bool OverwriteExistingProperties { get; set; } = true;

        #endregion

        #region Object Matching Options

        /// <summary>
        /// 매칭에 사용할 속성 카테고리
        /// </summary>
        public string MatchPropertyCategory { get; set; } = "Element";

        /// <summary>
        /// 매칭에 사용할 속성 이름
        /// </summary>
        public string MatchPropertyName { get; set; } = "Id";

        /// <summary>
        /// 대소문자 무시 매칭
        /// </summary>
        public bool IgnoreCaseInMatching { get; set; } = true;

        /// <summary>
        /// 부분 매칭 허용 (Contains)
        /// </summary>
        public bool AllowPartialMatching { get; set; } = false;

        /// <summary>
        /// 중복 매칭 허용 (첫 번째 결과 사용)
        /// </summary>
        public bool AllowMultipleMatches { get; set; } = false;

        #endregion

        #region Selection Set Options

        /// <summary>
        /// Selection Set 생성 활성화 여부
        /// </summary>
        public bool EnableSelectionSetCreation { get; set; } = true;

        /// <summary>
        /// Selection Set 그룹화 전략
        /// </summary>
        public GroupingStrategy GroupingStrategy { get; set; } = GroupingStrategy.ByParentSet;

        /// <summary>
        /// Selection Set 루트 폴더 이름
        /// </summary>
        public string SelectionSetRootFolder { get; set; } = "AWP 4D Sets";

        /// <summary>
        /// 빈 Selection Set 생성 건너뛰기
        /// </summary>
        public bool SkipEmptySelectionSets { get; set; } = true;

        #endregion

        #region TimeLiner Options

        /// <summary>
        /// TimeLiner Task 생성 활성화 여부
        /// </summary>
        public bool EnableTimeLinerTaskCreation { get; set; } = true;

        /// <summary>
        /// TimeLiner 루트 폴더 이름 (Tasks 내)
        /// </summary>
        public string TimeLinerRootFolder { get; set; } = "AWP 4D Tasks";

        /// <summary>
        /// 기본 Task Type
        /// </summary>
        public string DefaultTaskType { get; set; } = "Construct";

        /// <summary>
        /// 계층적 Task 구조 생성 (폴더 구조 반영)
        /// </summary>
        public bool CreateHierarchicalTasks { get; set; } = true;

        /// <summary>
        /// Task에 Selection Set 연결 방식
        /// </summary>
        public TaskSelectionMode TaskSelectionMode { get; set; } = TaskSelectionMode.SelectionSource;

        #endregion

        #region Processing Options

        /// <summary>
        /// 배치 처리 크기 (한 번에 처리할 항목 수)
        /// </summary>
        public int BatchSize { get; set; } = 100;

        /// <summary>
        /// 오류 발생 시 계속 진행 여부
        /// </summary>
        public bool ContinueOnError { get; set; } = true;

        /// <summary>
        /// 재시도 횟수
        /// </summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// 재시도 간격 (밀리초)
        /// </summary>
        public int RetryDelayMs { get; set; } = 500;

        /// <summary>
        /// 상세 로깅 활성화
        /// </summary>
        public bool VerboseLogging { get; set; } = true;

        /// <summary>
        /// 드라이런 모드 (실제 변경 없이 시뮬레이션)
        /// </summary>
        public bool DryRun { get; set; } = false;

        #endregion

        #region Validation Options

        /// <summary>
        /// 실행 전 유효성 검증 활성화
        /// </summary>
        public bool EnablePreValidation { get; set; } = true;

        /// <summary>
        /// 실행 후 결과 검증 활성화
        /// </summary>
        public bool EnablePostValidation { get; set; } = true;

        /// <summary>
        /// 최소 매칭 성공률 (0-100)
        /// </summary>
        public double MinMatchSuccessRate { get; set; } = 80.0;

        #endregion

        /// <summary>
        /// 기본 옵션 생성
        /// </summary>
        public static AWP4DOptions Default => new AWP4DOptions();

        /// <summary>
        /// 드라이런 옵션 생성
        /// </summary>
        public static AWP4DOptions CreateDryRun()
        {
            return new AWP4DOptions
            {
                DryRun = true,
                VerboseLogging = true
            };
        }

        /// <summary>
        /// 프로덕션 옵션 생성 (안전 모드)
        /// </summary>
        public static AWP4DOptions CreateProduction()
        {
            return new AWP4DOptions
            {
                EnablePreValidation = true,
                EnablePostValidation = true,
                ContinueOnError = false,
                OverwriteExistingProperties = false,
                MinMatchSuccessRate = 90.0
            };
        }

        /// <summary>
        /// 옵션 유효성 검증
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(PropertyCategoryName))
                errors.Add("PropertyCategoryName이 비어있습니다.");

            if (string.IsNullOrWhiteSpace(SelectionSetRootFolder))
                errors.Add("SelectionSetRootFolder가 비어있습니다.");

            if (BatchSize < 1 || BatchSize > 10000)
                errors.Add("BatchSize는 1~10000 사이여야 합니다.");

            if (MinMatchSuccessRate < 0 || MinMatchSuccessRate > 100)
                errors.Add("MinMatchSuccessRate는 0~100 사이여야 합니다.");

            if (RetryCount < 0 || RetryCount > 10)
                errors.Add("RetryCount는 0~10 사이여야 합니다.");

            errorMessage = string.Join("; ", errors);
            return errors.Count == 0;
        }
    }

    /// <summary>
    /// Selection Set 그룹화 전략
    /// </summary>
    public enum GroupingStrategy
    {
        /// <summary>ParentSet 경로에 따라 그룹화</summary>
        ByParentSet,
        /// <summary>Zone별 그룹화</summary>
        ByZone,
        /// <summary>Zone + Level별 그룹화</summary>
        ByZoneAndLevel,
        /// <summary>작업명별 그룹화</summary>
        ByTaskName,
        /// <summary>시작일(주) 별 그룹화</summary>
        ByStartWeek,
        /// <summary>TaskType별 그룹화</summary>
        ByTaskType,
        /// <summary>그룹화 없음 (개별 Set)</summary>
        None
    }

    /// <summary>
    /// TimeLiner Task에 Selection 연결 방식
    /// </summary>
    public enum TaskSelectionMode
    {
        /// <summary>SelectionSource (저장된 Set 참조)</summary>
        SelectionSource,
        /// <summary>Explicit (ModelItem 직접 참조)</summary>
        Explicit,
        /// <summary>Search (검색 조건)</summary>
        Search
    }
}

using System;
using System.Collections.Generic;

namespace DXTnavis.Models
{
    /// <summary>
    /// 공정 일정 데이터 모델
    /// CSV/Excel에서 가져온 스케줄 정보를 저장합니다.
    /// </summary>
    public class ScheduleData
    {
        /// <summary>
        /// 객체 매칭용 동기화 ID (필수)
        /// Navisworks ModelItem과 매칭하는 고유 식별자
        /// </summary>
        public string SyncID { get; set; }

        /// <summary>
        /// 작업명
        /// </summary>
        public string TaskName { get; set; }

        /// <summary>
        /// 계획 시작일
        /// </summary>
        public DateTime? PlannedStartDate { get; set; }

        /// <summary>
        /// 계획 종료일
        /// </summary>
        public DateTime? PlannedEndDate { get; set; }

        /// <summary>
        /// 실제 시작일 (선택)
        /// </summary>
        public DateTime? ActualStartDate { get; set; }

        /// <summary>
        /// 실제 종료일 (선택)
        /// </summary>
        public DateTime? ActualEndDate { get; set; }

        /// <summary>
        /// 작업 기간 (일)
        /// </summary>
        public int Duration
        {
            get
            {
                if (PlannedStartDate.HasValue && PlannedEndDate.HasValue)
                {
                    return (int)(PlannedEndDate.Value - PlannedStartDate.Value).TotalDays;
                }
                return 0;
            }
        }

        /// <summary>
        /// 비용 (선택)
        /// </summary>
        public decimal? Cost { get; set; }

        /// <summary>
        /// 작업 유형 (Construct, Demolish, Temporary)
        /// TimeLiner SimulationTaskTypeName에 사용
        /// </summary>
        public string TaskType { get; set; } = "Construct";

        /// <summary>
        /// 그룹화 레벨 (Zone, Level, Category, etc.)
        /// Selection Set 그룹화 기준
        /// </summary>
        public string SetLevel { get; set; }

        /// <summary>
        /// 상위 그룹 경로 (예: "Zone-A/Level-1")
        /// Selection Set 폴더 구조 생성에 사용
        /// </summary>
        public string ParentSet { get; set; }

        /// <summary>
        /// 진행률 (0-100)
        /// </summary>
        public double Progress { get; set; }

        /// <summary>
        /// 추가 속성 딕셔너리
        /// CSV의 추가 컬럼을 저장
        /// </summary>
        public Dictionary<string, string> CustomProperties { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 매칭된 Navisworks ModelItem의 InstanceGuid
        /// ObjectMatcher에서 설정
        /// </summary>
        public Guid? MatchedObjectId { get; set; }

        /// <summary>
        /// 매칭 상태
        /// </summary>
        public MatchStatus MatchStatus { get; set; } = MatchStatus.Pending;

        /// <summary>
        /// 매칭 실패 시 오류 메시지
        /// </summary>
        public string MatchError { get; set; }

        /// <summary>
        /// 데이터 유효성 검증
        /// </summary>
        public bool IsValid()
        {
            // SyncID 필수
            if (string.IsNullOrWhiteSpace(SyncID))
                return false;

            // 최소한 계획 시작일 또는 종료일 필요
            if (!PlannedStartDate.HasValue && !PlannedEndDate.HasValue)
                return false;

            // 시작일이 종료일보다 늦으면 안됨
            if (PlannedStartDate.HasValue && PlannedEndDate.HasValue)
            {
                if (PlannedStartDate.Value > PlannedEndDate.Value)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 유효성 검증 오류 메시지 반환
        /// </summary>
        public string GetValidationErrors()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(SyncID))
                errors.Add("SyncID가 비어있습니다.");

            if (!PlannedStartDate.HasValue && !PlannedEndDate.HasValue)
                errors.Add("시작일과 종료일이 모두 비어있습니다.");

            if (PlannedStartDate.HasValue && PlannedEndDate.HasValue && PlannedStartDate.Value > PlannedEndDate.Value)
                errors.Add($"시작일({PlannedStartDate:yyyy-MM-dd})이 종료일({PlannedEndDate:yyyy-MM-dd})보다 늦습니다.");

            return string.Join("; ", errors);
        }

        public override string ToString()
        {
            return $"[{SyncID}] {TaskName} ({PlannedStartDate:yyyy-MM-dd} ~ {PlannedEndDate:yyyy-MM-dd})";
        }
    }

    /// <summary>
    /// 객체 매칭 상태
    /// </summary>
    public enum MatchStatus
    {
        /// <summary>매칭 대기중</summary>
        Pending,
        /// <summary>매칭 성공</summary>
        Matched,
        /// <summary>매칭 실패</summary>
        NotFound,
        /// <summary>중복 매칭 (다수 객체 발견)</summary>
        MultipleMatches,
        /// <summary>매칭 오류</summary>
        Error
    }
}

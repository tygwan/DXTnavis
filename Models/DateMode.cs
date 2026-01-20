using System.ComponentModel;

namespace DXTnavis.Models
{
    /// <summary>
    /// Schedule Builder 날짜 모드
    /// Phase 13: TimeLiner 연동 시 Planned/Actual 날짜 처리 방식
    /// </summary>
    public enum DateMode
    {
        /// <summary>
        /// 계획일만 설정 (Planned Start/End만 TimeLiner에 적용)
        /// </summary>
        [Description("계획일만 (Planned Only)")]
        PlannedOnly,

        /// <summary>
        /// 계획일을 실제일에도 복사 (권장)
        /// Planned → Actual로 복사하여 TimeLiner 4D 시뮬레이션 즉시 시작 가능
        /// </summary>
        [Description("계획일 → 실제일 복사 (권장)")]
        ActualFromPlanned,

        /// <summary>
        /// 계획/실제 별도 입력
        /// 사용자가 Actual Start/End를 별도로 입력해야 함
        /// </summary>
        [Description("계획/실제 별도 입력")]
        BothSeparate
    }

    /// <summary>
    /// DateMode 확장 메서드
    /// </summary>
    public static class DateModeExtensions
    {
        /// <summary>
        /// DateMode의 Description 속성 반환
        /// </summary>
        public static string GetDescription(this DateMode mode)
        {
            var field = mode.GetType().GetField(mode.ToString());
            var attribute = (DescriptionAttribute)System.Attribute.GetCustomAttribute(
                field, typeof(DescriptionAttribute));
            return attribute?.Description ?? mode.ToString();
        }

        /// <summary>
        /// 모든 DateMode 옵션 목록 (UI 바인딩용)
        /// </summary>
        public static System.Collections.Generic.List<DateModeOption> GetAllOptions()
        {
            return new System.Collections.Generic.List<DateModeOption>
            {
                new DateModeOption { Mode = DateMode.PlannedOnly, DisplayName = "계획일만 (Planned Only)" },
                new DateModeOption { Mode = DateMode.ActualFromPlanned, DisplayName = "계획일 → 실제일 복사 (권장)" },
                new DateModeOption { Mode = DateMode.BothSeparate, DisplayName = "계획/실제 별도 입력" }
            };
        }
    }

    /// <summary>
    /// DateMode UI 바인딩용 옵션 클래스
    /// </summary>
    public class DateModeOption
    {
        public DateMode Mode { get; set; }
        public string DisplayName { get; set; }

        public override string ToString() => DisplayName;
    }
}

using System;

namespace DXTnavis.Models
{
    /// <summary>
    /// Load Hierarchy 진행률 모델 (Phase 10)
    /// IProgress&lt;LoadProgress&gt; 인터페이스와 함께 사용
    /// </summary>
    public class LoadProgress
    {
        /// <summary>
        /// 처리된 노드 수
        /// </summary>
        public int ProcessedNodes { get; set; }

        /// <summary>
        /// 전체 노드 수
        /// </summary>
        public int TotalNodes { get; set; }

        /// <summary>
        /// 현재 처리 중인 항목 이름
        /// </summary>
        public string CurrentItem { get; set; }

        /// <summary>
        /// 현재 단계 (Counting, Extracting, Updating)
        /// </summary>
        public LoadPhase Phase { get; set; }

        /// <summary>
        /// 진행률 (0-100)
        /// </summary>
        public double Percentage => TotalNodes > 0
            ? Math.Min(100, (double)ProcessedNodes / TotalNodes * 100)
            : 0;

        /// <summary>
        /// 진행률 텍스트
        /// </summary>
        public string ProgressText
        {
            get
            {
                switch (Phase)
                {
                    case LoadPhase.Counting:
                        return $"Counting nodes... ({ProcessedNodes})";
                    case LoadPhase.ExtractingTree:
                        return $"Building tree: {ProcessedNodes:N0} / {TotalNodes:N0} ({Percentage:F0}%)";
                    case LoadPhase.ExtractingProperties:
                        return $"Extracting properties: {ProcessedNodes:N0} / {TotalNodes:N0} ({Percentage:F0}%)";
                    case LoadPhase.UpdatingUI:
                        return $"Updating UI... ({CurrentItem})";
                    case LoadPhase.Complete:
                        return $"Complete: {TotalNodes:N0} nodes loaded";
                    case LoadPhase.Cancelled:
                        return "Cancelled";
                    default:
                        return $"{ProcessedNodes:N0} / {TotalNodes:N0}";
                }
            }
        }

        /// <summary>
        /// 기본 생성자
        /// </summary>
        public LoadProgress()
        {
            CurrentItem = string.Empty;
            Phase = LoadPhase.Counting;
        }

        /// <summary>
        /// 파라미터 생성자
        /// </summary>
        public LoadProgress(int processed, int total, string currentItem, LoadPhase phase)
        {
            ProcessedNodes = processed;
            TotalNodes = total;
            CurrentItem = currentItem ?? string.Empty;
            Phase = phase;
        }
    }

    /// <summary>
    /// 로딩 단계 열거형
    /// </summary>
    public enum LoadPhase
    {
        /// <summary>노드 수 카운팅</summary>
        Counting,
        /// <summary>트리 구조 추출</summary>
        ExtractingTree,
        /// <summary>속성 데이터 추출</summary>
        ExtractingProperties,
        /// <summary>UI 업데이트</summary>
        UpdatingUI,
        /// <summary>완료</summary>
        Complete,
        /// <summary>취소됨</summary>
        Cancelled
    }
}

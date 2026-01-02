using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DXTnavis.Models
{
    /// <summary>
    /// TreeView에 표시될 계층 구조 노드 모델
    /// </summary>
    public class TreeNodeModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isExpanded;

        // Level별 색상 팔레트
        private static readonly string[] LevelColors = new[]
        {
            "#0078D4", // L0 - Blue
            "#28A745", // L1 - Green
            "#FFC107", // L2 - Yellow/Orange
            "#DC3545", // L3 - Red
            "#6F42C1", // L4 - Purple
            "#20C997", // L5 - Teal
            "#FD7E14", // L6 - Orange
            "#E83E8C", // L7 - Pink
            "#17A2B8", // L8 - Cyan
            "#6C757D"  // L9+ - Gray
        };

        /// <summary>
        /// Navisworks ModelItem의 GUID
        /// </summary>
        public Guid ObjectId { get; set; }

        /// <summary>
        /// 표시 이름
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 계층 레벨 (0부터 시작)
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// Level 기반 접두사 (예: "L0", "L1", "L2")
        /// TreeView에서 계층 레벨을 시각적으로 표시
        /// </summary>
        public string LevelPrefix => $"L{Level}";

        /// <summary>
        /// Level 기반 배경 색상
        /// </summary>
        public string LevelColor => LevelColors[Math.Min(Level, LevelColors.Length - 1)];

        /// <summary>
        /// 노드 아이콘 (자식 유무에 따라)
        /// </summary>
        public string NodeIcon => Children.Count > 0 ? "📁" : (HasGeometry ? "🔷" : "📄");

        /// <summary>
        /// 자식 개수 텍스트 (자식이 있을 때만 표시)
        /// </summary>
        public string ChildCountText => Children.Count > 0 ? $"({Children.Count})" : "";

        /// <summary>
        /// 형상 존재 여부
        /// </summary>
        public bool HasGeometry { get; set; }

        /// <summary>
        /// 자식 노드 컬렉션
        /// </summary>
        public ObservableCollection<TreeNodeModel> Children { get; set; }

        /// <summary>
        /// 선택 상태
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        /// <summary>
        /// 확장 상태
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
            }
        }

        public TreeNodeModel()
        {
            Children = new ObservableCollection<TreeNodeModel>();
        }

        /// <summary>
        /// 지정된 레벨까지 확장
        /// </summary>
        /// <param name="targetLevel">확장할 최대 레벨</param>
        public void ExpandToLevel(int targetLevel)
        {
            if (Level < targetLevel)
            {
                IsExpanded = true;
                foreach (var child in Children)
                {
                    child.ExpandToLevel(targetLevel);
                }
            }
            else
            {
                IsExpanded = false;
            }
        }

        /// <summary>
        /// 모든 노드 축소
        /// </summary>
        public void CollapseAll()
        {
            IsExpanded = false;
            foreach (var child in Children)
            {
                child.CollapseAll();
            }
        }

        /// <summary>
        /// 모든 노드 확장
        /// </summary>
        public void ExpandAll()
        {
            IsExpanded = true;
            foreach (var child in Children)
            {
                child.ExpandAll();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

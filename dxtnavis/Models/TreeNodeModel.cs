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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DXTnavis.ViewModels
{
    /// <summary>
    /// TreeView에 표시될 계층 구조 노드의 ViewModel
    /// 재귀적 구조로 Children을 포함하여 트리 구조를 표현합니다.
    /// </summary>
    public class HierarchyNodeViewModel : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isSelected;

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
        /// 형상 존재 여부
        /// </summary>
        public bool HasGeometry { get; set; }

        /// <summary>
        /// 자식 노드 컬렉션 (재귀적 구조)
        /// </summary>
        public ObservableCollection<HierarchyNodeViewModel> Children { get; set; }

        /// <summary>
        /// TreeView 노드 확장 상태
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }
            }
        }

        /// <summary>
        /// TreeView 노드 선택 상태
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public HierarchyNodeViewModel()
        {
            Children = new ObservableCollection<HierarchyNodeViewModel>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

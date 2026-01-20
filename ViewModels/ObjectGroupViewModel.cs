using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using DXTnavis.Models;

namespace DXTnavis.ViewModels
{
    /// <summary>
    /// Phase 11: Object Grouping ViewModel
    /// Groups HierarchicalPropertyRecord items by ObjectId for better visualization
    /// MVP: Object-level grouping only (no category sub-grouping)
    /// </summary>
    public class ObjectGroupViewModel : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isSelected;

        /// <summary>
        /// Object ID (그룹 키)
        /// </summary>
        public Guid ObjectId { get; set; }

        /// <summary>
        /// 객체 표시 이름
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 객체 레벨
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// 시스템 경로
        /// </summary>
        public string SysPath { get; set; }

        /// <summary>
        /// 속성 개수 (하위 아이템 수)
        /// </summary>
        public int PropertyCount => Properties.Count;

        /// <summary>
        /// 카테고리 개수
        /// </summary>
        public int CategoryCount { get; set; }

        /// <summary>
        /// 그룹 확장 상태
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
        /// 그룹 선택 상태 (전체 속성 선택/해제)
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

                    // 하위 속성들 모두 선택/해제 (부모 → 자식 전파)
                    foreach (var prop in Properties)
                    {
                        prop.IsSelected = value;
                    }
                }
            }
        }

        /// <summary>
        /// 선택된 속성 개수
        /// </summary>
        public int SelectedPropertyCount => Properties.Count(p => p.IsSelected);

        /// <summary>
        /// 선택 상태 텍스트 (예: "3/10 selected")
        /// </summary>
        public string SelectionStatus => $"{SelectedPropertyCount}/{PropertyCount} selected";

        /// <summary>
        /// 그룹 헤더 텍스트 (예: "Wall-1 (10 properties)")
        /// </summary>
        public string HeaderText => $"{DisplayName} ({PropertyCount} properties, {CategoryCount} categories)";

        /// <summary>
        /// 하위 속성 목록
        /// </summary>
        public ObservableCollection<HierarchicalPropertyRecord> Properties { get; }

        /// <summary>
        /// 기본 생성자
        /// </summary>
        public ObjectGroupViewModel()
        {
            Properties = new ObservableCollection<HierarchicalPropertyRecord>();
            _isExpanded = false;
            _isSelected = false;
        }

        /// <summary>
        /// HierarchicalPropertyRecord 목록으로부터 그룹 생성
        /// </summary>
        /// <param name="objectId">객체 ID</param>
        /// <param name="records">속성 레코드 목록</param>
        public ObjectGroupViewModel(Guid objectId, IEnumerable<HierarchicalPropertyRecord> records)
        {
            Properties = new ObservableCollection<HierarchicalPropertyRecord>();
            ObjectId = objectId;

            var recordList = records.ToList();
            if (recordList.Any())
            {
                var first = recordList.First();
                DisplayName = first.DisplayName;
                Level = first.Level;
                SysPath = first.SysPath;
                CategoryCount = recordList.Select(r => r.Category).Distinct().Count();

                foreach (var record in recordList)
                {
                    Properties.Add(record);
                    // 속성 변경 이벤트 구독
                    record.PropertyChanged += OnPropertyRecordChanged;
                }
            }
        }

        /// <summary>
        /// 하위 속성 선택 상태 변경 시 호출
        /// </summary>
        private void OnPropertyRecordChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HierarchicalPropertyRecord.IsSelected))
            {
                // 선택 상태 UI 업데이트
                OnPropertyChanged(nameof(SelectedPropertyCount));
                OnPropertyChanged(nameof(SelectionStatus));

                // 모든 속성이 선택되면 그룹도 선택 상태로 변경 (자식 → 부모 전파)
                var allSelected = Properties.All(p => p.IsSelected);
                var noneSelected = Properties.All(p => !p.IsSelected);

                if (allSelected && !_isSelected)
                {
                    _isSelected = true;
                    OnPropertyChanged(nameof(IsSelected));
                }
                else if (noneSelected && _isSelected)
                {
                    _isSelected = false;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        /// <summary>
        /// 정적 메서드: HierarchicalPropertyRecord 목록을 ObjectGroupViewModel 목록으로 변환
        /// </summary>
        /// <param name="records">속성 레코드 목록</param>
        /// <returns>그룹화된 ViewModel 목록</returns>
        public static ObservableCollection<ObjectGroupViewModel> CreateGroups(
            IEnumerable<HierarchicalPropertyRecord> records)
        {
            var groups = new ObservableCollection<ObjectGroupViewModel>();

            var grouped = records
                .GroupBy(r => r.ObjectId)
                .OrderBy(g => g.First().Level)
                .ThenBy(g => g.First().DisplayName);

            foreach (var group in grouped)
            {
                groups.Add(new ObjectGroupViewModel(group.Key, group));
            }

            return groups;
        }
    }
}

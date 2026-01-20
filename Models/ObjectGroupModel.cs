using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace DXTnavis.Models
{
    /// <summary>
    /// Phase 12: 객체 그룹 모델
    /// 1 Object = 1 Group으로 속성들을 그룹화하여 관리
    /// 445K 개별 레코드 → ~5K 그룹으로 성능 최적화
    /// </summary>
    public class ObjectGroupModel : INotifyPropertyChanged
    {
        #region Private Fields

        private bool _isSelected;
        private bool _isExpanded;
        private List<PropertyRecord> _filteredProperties;

        #endregion

        #region Object Identification

        /// <summary>
        /// 객체의 고유 ID
        /// </summary>
        public Guid ObjectId { get; set; }

        /// <summary>
        /// 부모 객체의 고유 ID (최상위 객체는 Guid.Empty)
        /// </summary>
        public Guid ParentId { get; set; }

        /// <summary>
        /// 객체의 표시 이름
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 선택된 객체로부터의 깊이 (0부터 시작)
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// 시스템 경로 (계층 구조 전체 경로)
        /// </summary>
        public string SysPath { get; set; }

        #endregion

        #region Selection & UI State

        /// <summary>
        /// 그룹 전체 선택 상태
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
                    OnPropertyChanged(nameof(SelectedPropertyCount));
                }
            }
        }

        /// <summary>
        /// UI 확장 상태
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

        #endregion

        #region Properties Collection

        /// <summary>
        /// 모든 속성 목록
        /// </summary>
        public List<PropertyRecord> Properties { get; set; }

        /// <summary>
        /// 필터링된 속성 목록 (필터 적용 시)
        /// </summary>
        public List<PropertyRecord> FilteredProperties
        {
            get => _filteredProperties ?? Properties;
            set
            {
                _filteredProperties = value;
                OnPropertyChanged(nameof(FilteredProperties));
                OnPropertyChanged(nameof(FilteredPropertyCount));
            }
        }

        #endregion

        #region Computed Properties

        /// <summary>
        /// 전체 속성 개수
        /// </summary>
        public int PropertyCount => Properties?.Count ?? 0;

        /// <summary>
        /// 필터링된 속성 개수
        /// </summary>
        public int FilteredPropertyCount => FilteredProperties?.Count ?? 0;

        /// <summary>
        /// 선택된 속성 개수 (개별 속성 선택 시)
        /// </summary>
        public int SelectedPropertyCount => FilteredProperties?.Count(p => p.IsSelected) ?? 0;

        /// <summary>
        /// 카테고리 개수
        /// </summary>
        public int CategoryCount => Categories?.Count ?? 0;

        /// <summary>
        /// 빠른 필터링용 카테고리 집합
        /// </summary>
        public HashSet<string> Categories { get; set; }

        /// <summary>
        /// UI 표시용 헤더 텍스트
        /// </summary>
        public string HeaderText => $"{DisplayName} ({FilteredPropertyCount} props)";

        /// <summary>
        /// 레벨 배지 텍스트
        /// </summary>
        public string LevelBadge => $"L{Level}";

        #endregion

        #region Constructor

        public ObjectGroupModel()
        {
            Properties = new List<PropertyRecord>();
            Categories = new HashSet<string>();
            IsSelected = false;
            IsExpanded = false;
        }

        public ObjectGroupModel(Guid objectId, string displayName, int level, string sysPath = "", Guid parentId = default)
            : this()
        {
            ObjectId = objectId;
            DisplayName = displayName;
            Level = level;
            SysPath = sysPath;
            ParentId = parentId;
        }

        #endregion

        #region Methods

        /// <summary>
        /// 속성 추가 및 카테고리 집합 업데이트
        /// </summary>
        public void AddProperty(PropertyRecord property)
        {
            Properties.Add(property);
            if (!string.IsNullOrEmpty(property.Category))
            {
                Categories.Add(property.Category);
            }
        }

        /// <summary>
        /// 필터 초기화 (모든 속성 표시)
        /// </summary>
        public void ResetFilter()
        {
            _filteredProperties = null;
            OnPropertyChanged(nameof(FilteredProperties));
            OnPropertyChanged(nameof(FilteredPropertyCount));
        }

        /// <summary>
        /// 카테고리별 속성 필터링
        /// </summary>
        public void ApplyFilter(HashSet<string> selectedCategories, string propertyNameFilter = null, string propertyValueFilter = null)
        {
            _filteredProperties = Properties.Where(p =>
                // 카테고리 필터
                (selectedCategories == null || selectedCategories.Count == 0 || selectedCategories.Contains(p.Category)) &&
                // 속성 이름 필터
                (string.IsNullOrEmpty(propertyNameFilter) ||
                 (p.PropertyName?.IndexOf(propertyNameFilter, StringComparison.OrdinalIgnoreCase) >= 0)) &&
                // 속성 값 필터
                (string.IsNullOrEmpty(propertyValueFilter) ||
                 (p.PropertyValue?.IndexOf(propertyValueFilter, StringComparison.OrdinalIgnoreCase) >= 0))
            ).ToList();

            OnPropertyChanged(nameof(FilteredProperties));
            OnPropertyChanged(nameof(FilteredPropertyCount));
            OnPropertyChanged(nameof(HeaderText));
        }

        /// <summary>
        /// HierarchicalPropertyRecord로 변환 (TimeLiner 호환성)
        /// </summary>
        public IEnumerable<HierarchicalPropertyRecord> ToHierarchicalRecords()
        {
            foreach (var prop in FilteredProperties ?? Properties)
            {
                yield return new HierarchicalPropertyRecord
                {
                    ObjectId = this.ObjectId,
                    ParentId = this.ParentId,
                    Level = this.Level,
                    DisplayName = this.DisplayName,
                    SysPath = this.SysPath,
                    Category = prop.Category,
                    PropertyName = prop.PropertyName,
                    PropertyValue = prop.PropertyValue,
                    DataType = prop.DataType,
                    RawValue = prop.RawValue,
                    NumericValue = prop.NumericValue,
                    Unit = prop.Unit,
                    ReadWriteStatus = prop.ReadWriteStatus,
                    IsSelected = this.IsSelected || prop.IsSelected
                };
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}

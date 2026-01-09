using System;
using System.Linq;

namespace DXTnavis.ViewModels
{
    /// <summary>
    /// DXwindowViewModel - Filter 관련 메서드
    /// v0.5.0: Partial Class 분리
    /// </summary>
    public partial class DXwindowViewModel
    {
        #region Filter Methods

        /// <summary>
        /// 필터 적용
        /// </summary>
        private void ApplyFilter()
        {
            FilteredHierarchicalProperties.Clear();

            foreach (var prop in AllHierarchicalProperties)
            {
                // Level 필터 (예: "L0", "L1", "L2"...)
                bool matchLevel = string.IsNullOrEmpty(SelectedLevelFilter) ||
                                 SelectedLevelFilter == "(All)" ||
                                 $"L{prop.Level}" == SelectedLevelFilter;

                // SysPath 필터 (부분 문자열 매칭)
                bool matchSysPath = string.IsNullOrEmpty(SysPathFilter) ||
                                   (prop.SysPath?.IndexOf(SysPathFilter, StringComparison.OrdinalIgnoreCase) >= 0);

                bool matchCategory = string.IsNullOrEmpty(SelectedCategoryFilter) ||
                                    SelectedCategoryFilter == "(All)" ||
                                    prop.Category == SelectedCategoryFilter;

                bool matchPropertyName = string.IsNullOrEmpty(PropertyNameFilter) ||
                                        (prop.PropertyName?.IndexOf(PropertyNameFilter, StringComparison.OrdinalIgnoreCase) >= 0);

                bool matchPropertyValue = string.IsNullOrEmpty(PropertyValueFilter) ||
                                         (prop.PropertyValue?.IndexOf(PropertyValueFilter, StringComparison.OrdinalIgnoreCase) >= 0);

                if (matchLevel && matchSysPath && matchCategory && matchPropertyName && matchPropertyValue)
                {
                    FilteredHierarchicalProperties.Add(prop);
                }
            }

            StatusMessage = $"Filtered: {FilteredHierarchicalProperties.Count} / {AllHierarchicalProperties.Count} items";
            OnPropertyChanged(nameof(SelectedPropertiesCount));
            ((RelayCommand)CreateSearchSetCommand).RaiseCanExecuteChanged();
            RefreshSelectionCommands();
        }

        /// <summary>
        /// v0.4.3: 필터 디바운스 트리거
        /// 필터 값 변경 시 호출되어 200ms 후 자동 필터 적용
        /// </summary>
        private void TriggerFilterDebounce()
        {
            // 데이터가 없으면 필터 적용 안함
            if (AllHierarchicalProperties.Count == 0)
                return;

            _filterDebounceTimer?.Stop();
            _filterDebounceTimer?.Start();
        }

        /// <summary>
        /// 필터 초기화
        /// </summary>
        private void ClearFilter()
        {
            SelectedLevelFilter = null;
            SysPathFilter = string.Empty;
            SelectedCategoryFilter = null;
            PropertyNameFilter = string.Empty;
            PropertyValueFilter = string.Empty;

            FilteredHierarchicalProperties.Clear();
            foreach (var prop in AllHierarchicalProperties)
            {
                FilteredHierarchicalProperties.Add(prop);
            }

            StatusMessage = $"Total: {AllHierarchicalProperties.Count} items";
            OnPropertyChanged(nameof(SelectedPropertiesCount));
            ((RelayCommand)CreateSearchSetCommand).RaiseCanExecuteChanged();
            RefreshSelectionCommands();
        }

        /// <summary>
        /// AllHierarchicalProperties가 업데이트될 때 AvailableCategories, AvailableLevels, FilteredHierarchicalProperties 동기화
        /// </summary>
        private void SyncFilteredProperties()
        {
            // Level 목록 업데이트
            AvailableLevels.Clear();
            AvailableLevels.Add("(All)");
            var levels = AllHierarchicalProperties
                .Select(p => p.Level)
                .Distinct()
                .OrderBy(l => l)
                .Select(l => $"L{l}");

            foreach (var level in levels)
            {
                AvailableLevels.Add(level);
            }

            // 카테고리 목록 업데이트
            AvailableCategories.Clear();
            AvailableCategories.Add("(All)");
            var categories = AllHierarchicalProperties
                .Select(p => p.Category)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .OrderBy(c => c);

            foreach (var category in categories)
            {
                AvailableCategories.Add(category);
            }

            // 필터링된 목록 초기화 (전체 표시)
            FilteredHierarchicalProperties.Clear();
            foreach (var prop in AllHierarchicalProperties)
            {
                FilteredHierarchicalProperties.Add(prop);
            }

            // 필터 초기화
            SelectedLevelFilter = null;
            SysPathFilter = string.Empty;
            SelectedCategoryFilter = null;
            PropertyNameFilter = string.Empty;
            PropertyValueFilter = string.Empty;

            StatusMessage = $"Loaded: {AllHierarchicalProperties.Count} items";
            RefreshSelectionCommands();
        }

        #endregion
    }
}

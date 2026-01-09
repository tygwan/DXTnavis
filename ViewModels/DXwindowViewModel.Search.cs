using System;
using System.Linq;

namespace DXTnavis.ViewModels
{
    /// <summary>
    /// DXwindowViewModel - Search 관련 메서드
    /// v0.5.0: Partial Class 분리
    /// </summary>
    public partial class DXwindowViewModel
    {
        #region Search Methods

        /// <summary>
        /// 객체 검색 (v0.4.0)
        /// 이름 또는 속성 값으로 객체를 검색하고 결과를 필터링합니다.
        /// </summary>
        private void SearchObjects()
        {
            if (string.IsNullOrWhiteSpace(ObjectSearchQuery))
                return;

            FilteredHierarchicalProperties.Clear();
            string query = ObjectSearchQuery.ToLowerInvariant();

            foreach (var prop in AllHierarchicalProperties)
            {
                // 객체 이름 검색
                bool matchName = prop.DisplayName?.ToLowerInvariant().Contains(query) ?? false;

                // 속성 값 검색
                bool matchValue = prop.PropertyValue?.ToLowerInvariant().Contains(query) ?? false;

                // 속성 이름 검색
                bool matchPropertyName = prop.PropertyName?.ToLowerInvariant().Contains(query) ?? false;

                // SysPath 검색
                bool matchPath = prop.SysPath?.ToLowerInvariant().Contains(query) ?? false;

                if (matchName || matchValue || matchPropertyName || matchPath)
                {
                    FilteredHierarchicalProperties.Add(prop);
                }
            }

            // 검색 결과가 있으면 해당 객체들을 3D에서 선택 (옵션)
            if (FilteredHierarchicalProperties.Count > 0 && FilteredHierarchicalProperties.Count <= 100)
            {
                SelectFilteredIn3D();
            }

            StatusMessage = $"Search '{ObjectSearchQuery}': {FilteredHierarchicalProperties.Count} results found";
            OnPropertyChanged(nameof(SelectedPropertiesCount));
            ((RelayCommand)CreateSearchSetCommand).RaiseCanExecuteChanged();
            RefreshSelectionCommands();
        }

        /// <summary>
        /// 검색 결과 초기화 (v0.4.0)
        /// </summary>
        private void ClearSearch()
        {
            ObjectSearchQuery = string.Empty;

            // 기존 필터 조건 유지하면서 검색 초기화
            FilteredHierarchicalProperties.Clear();
            foreach (var prop in AllHierarchicalProperties)
            {
                FilteredHierarchicalProperties.Add(prop);
            }

            StatusMessage = $"Search cleared. Total: {AllHierarchicalProperties.Count} items";
            OnPropertyChanged(nameof(SelectedPropertiesCount));
            ((RelayCommand)CreateSearchSetCommand).RaiseCanExecuteChanged();
            RefreshSelectionCommands();
        }

        /// <summary>
        /// 검색 결과 객체로 줌 (v0.4.0)
        /// </summary>
        private void ZoomToSearchResult()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null) return;

                // 필터링된 레코드에서 고유 ObjectId 추출
                var objectIds = FilteredHierarchicalProperties
                    .Where(p => p.ObjectId != Guid.Empty)
                    .Select(p => p.ObjectId)
                    .Distinct()
                    .ToList();

                if (objectIds.Count > 0)
                {
                    // 선택 후 줌
                    int count = _selectionService.SelectAndZoomByIds(objectIds);
                    StatusMessage = $"Zoomed to {count} objects";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Zoom failed: {ex.Message}";
            }
        }

        #endregion
    }
}

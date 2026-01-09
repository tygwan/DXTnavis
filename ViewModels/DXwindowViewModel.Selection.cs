using System;
using System.Linq;
using System.Windows;
using DXTnavis.Helpers;

namespace DXTnavis.ViewModels
{
    /// <summary>
    /// DXwindowViewModel - 3D Object Selection 관련 메서드
    /// v0.5.0: Partial Class 분리
    /// </summary>
    public partial class DXwindowViewModel
    {
        #region 3D Object Selection Methods

        /// <summary>
        /// 필터링된 객체들을 Navisworks 3D 뷰에서 선택합니다.
        /// 체크박스로 선택된 항목이 있으면 해당 항목만, 없으면 전체 필터링된 항목 선택
        /// </summary>
        private void SelectIn3D()
        {
            try
            {
                int selectedCount;
                var checkedCount = _selectionService.GetCheckedObjectCount(FilteredHierarchicalProperties);

                if (checkedCount > 0)
                {
                    // 체크된 항목만 선택
                    selectedCount = _selectionService.SelectCheckedObjects(FilteredHierarchicalProperties);
                    StatusMessage = $"Selected {selectedCount} checked objects in 3D view";
                }
                else
                {
                    // 전체 필터링된 항목 선택
                    selectedCount = _selectionService.SelectFilteredObjects(FilteredHierarchicalProperties);
                    StatusMessage = $"Selected {selectedCount} filtered objects in 3D view";
                }

                if (selectedCount == 0)
                {
                    MessageBox.Show(
                        "No objects could be selected.\n\n" +
                        "Possible reasons:\n" +
                        "- ObjectId is empty for filtered items\n" +
                        "- Objects no longer exist in the model",
                        "Selection",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show(
                    $"Error selecting objects:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 필터링된 객체를 3D에서 선택 (내부 헬퍼)
        /// </summary>
        private void SelectFilteredIn3D()
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
                    _selectionService.SelectByIds(objectIds);
                }
            }
            catch
            {
                // 3D 선택 실패 시 조용히 무시
            }
        }

        /// <summary>
        /// v0.4.3: Show Only 토글 (On/Off)
        /// 필터링된 객체만 표시하거나 모든 객체를 표시합니다.
        /// </summary>
        private void ShowOnlyFiltered()
        {
            try
            {
                // 토글 상태 전환
                if (_isShowOnlyActive)
                {
                    // OFF: 모든 객체 표시
                    _selectionService.ShowAllObjects();
                    IsShowOnlyActive = false;
                    StatusMessage = "All objects visible (Show Only: OFF)";
                }
                else
                {
                    // ON: 필터링된 객체만 표시
                    int visibleCount;
                    var checkedCount = _selectionService.GetCheckedObjectCount(FilteredHierarchicalProperties);

                    if (checkedCount > 0)
                    {
                        // 체크된 항목만 표시
                        visibleCount = _selectionService.ShowOnlyCheckedObjects(FilteredHierarchicalProperties);
                        StatusMessage = $"Showing {visibleCount} checked objects only (Show Only: ON)";
                    }
                    else
                    {
                        // 전체 필터링된 항목 표시
                        visibleCount = _selectionService.ShowOnlyFilteredObjects(FilteredHierarchicalProperties);
                        StatusMessage = $"Showing {visibleCount} filtered objects only (Show Only: ON)";
                    }

                    if (visibleCount == 0)
                    {
                        MessageBox.Show(
                            "No objects could be shown.\n\n" +
                            "Please check the filter settings.",
                            "Visibility",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        return; // 상태 변경 안함
                    }

                    IsShowOnlyActive = true;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show(
                    $"Error setting visibility:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 모든 객체를 표시합니다 (숨김 해제).
        /// </summary>
        private void ShowAllObjects()
        {
            try
            {
                _selectionService.ShowAllObjects();
                StatusMessage = "All objects are now visible";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show(
                    $"Error showing all objects:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 필터링된 객체로 카메라 줌을 수행합니다.
        /// </summary>
        private void ZoomToFiltered()
        {
            try
            {
                var checkedCount = _selectionService.GetCheckedObjectCount(FilteredHierarchicalProperties);

                if (checkedCount > 0)
                {
                    // 체크된 항목으로 줌
                    var checkedRecords = FilteredHierarchicalProperties.Where(r => r.IsSelected);
                    _selectionService.ZoomToFilteredObjects(checkedRecords);
                    StatusMessage = $"Zoomed to {checkedCount} checked objects";
                }
                else
                {
                    // 전체 필터링된 항목으로 줌
                    _selectionService.ZoomToFilteredObjects(FilteredHierarchicalProperties);
                    var uniqueCount = _selectionService.GetUniqueObjectCount(FilteredHierarchicalProperties);
                    StatusMessage = $"Zoomed to {uniqueCount} filtered objects";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show(
                    $"Error zooming to objects:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 필터 적용 후 3D 선택 관련 Command의 CanExecute를 갱신합니다.
        /// </summary>
        private void RefreshSelectionCommands()
        {
            ((RelayCommand)SelectIn3DCommand)?.RaiseCanExecuteChanged();
            ((RelayCommand)ShowOnlyFilteredCommand)?.RaiseCanExecuteChanged();
            ((RelayCommand)ZoomToFilteredCommand)?.RaiseCanExecuteChanged();
            ((RelayCommand)BatchCaptureCommand)?.RaiseCanExecuteChanged();
        }

        #endregion
    }
}

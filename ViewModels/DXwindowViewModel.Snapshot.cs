using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using DXTnavis.Services;
using Microsoft.Win32;

namespace DXTnavis.ViewModels
{
    /// <summary>
    /// DXwindowViewModel - Snapshot 관련 메서드
    /// v0.5.0: Partial Class 분리
    /// </summary>
    public partial class DXwindowViewModel
    {
        #region Snapshot Methods

        /// <summary>
        /// 현재 뷰를 이미지로 캡처합니다.
        /// </summary>
        private void CaptureCurrentView()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "PNG 이미지|*.png|JPEG 이미지|*.jpg|BMP 이미지|*.bmp",
                    DefaultExt = "png",
                    FileName = $"Snapshot_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() != true) return;

                // 확장자에 따른 이미지 포맷 설정
                string extension = System.IO.Path.GetExtension(saveDialog.FileName).ToLower();
                if (extension == ".jpg" || extension == ".jpeg")
                {
                    _snapshotService.ImageFormat = System.Drawing.Imaging.ImageFormat.Jpeg;
                }
                else if (extension == ".bmp")
                {
                    _snapshotService.ImageFormat = System.Drawing.Imaging.ImageFormat.Bmp;
                }
                else
                {
                    _snapshotService.ImageFormat = System.Drawing.Imaging.ImageFormat.Png;
                }

                string directory = System.IO.Path.GetDirectoryName(saveDialog.FileName);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(saveDialog.FileName);

                string savedPath = _snapshotService.CaptureCurrentView(directory, fileName);
                StatusMessage = $"Snapshot saved: {savedPath}";

                MessageBox.Show(
                    $"Snapshot saved successfully!\n\n{savedPath}",
                    "Capture Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show(
                    $"Error capturing snapshot:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 현재 ViewPoint를 저장합니다.
        /// </summary>
        private void SaveCurrentViewPoint()
        {
            try
            {
                string viewpointName = $"DXTnavis_{DateTime.Now:yyyyMMdd_HHmmss}";

                // 간단한 입력 다이얼로그 대신 기본 이름 사용
                var result = MessageBox.Show(
                    $"Save current ViewPoint as:\n\n'{viewpointName}'\n\nThis will be saved in 'DXTnavis Snapshots' folder.",
                    "Save ViewPoint",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.OK) return;

                _snapshotService.SaveCurrentViewPoint(viewpointName, "DXTnavis Snapshots");
                StatusMessage = $"ViewPoint saved: {viewpointName}";

                MessageBox.Show(
                    $"ViewPoint saved successfully!\n\nName: {viewpointName}\nFolder: DXTnavis Snapshots",
                    "ViewPoint Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show(
                    $"Error saving ViewPoint:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 관측점을 초기 상태(Home)로 리셋합니다.
        /// Home 뷰포인트가 있으면 해당 뷰로 이동, 없으면 Zoom Extents 수행
        /// </summary>
        private void ResetToHome()
        {
            try
            {
                string resetMethod = _snapshotService.ResetToHome();
                StatusMessage = resetMethod.StartsWith("SavedViewpoint:")
                    ? $"View reset to: {resetMethod.Replace("SavedViewpoint: ", "")}"
                    : "View reset to Zoom Extents (no Home viewpoint found)";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show(
                    $"Error resetting view:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 현재 필터 조건으로 스냅샷과 ViewPoint를 함께 저장합니다.
        /// </summary>
        private void CaptureWithViewPoint()
        {
            try
            {
                // 현재 필터 조건을 기반으로 이름 생성
                string filterCondition = GenerateFilterConditionName();

                var saveDialog = new SaveFileDialog
                {
                    Filter = "PNG 이미지|*.png",
                    DefaultExt = "png",
                    FileName = $"{filterCondition}_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() != true) return;

                string directory = System.IO.Path.GetDirectoryName(saveDialog.FileName);
                string baseName = System.IO.Path.GetFileNameWithoutExtension(saveDialog.FileName);

                var snapshotResult = _snapshotService.CaptureWithViewPoint(baseName, directory);

                if (snapshotResult.Success)
                {
                    StatusMessage = $"Captured: {snapshotResult.ImagePath} + ViewPoint";

                    MessageBox.Show(
                        $"Snapshot and ViewPoint saved successfully!\n\n" +
                        $"Image: {snapshotResult.ImagePath}\n" +
                        $"ViewPoint: {snapshotResult.ViewPointName}",
                        "Capture Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"Capture failed:\n\n{snapshotResult.ErrorMessage}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show(
                    $"Error capturing with ViewPoint:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 체크된 객체들을 각각 격리하여 배치 캡처합니다.
        /// </summary>
        private void BatchCaptureFiltered()
        {
            try
            {
                var selectedRecords = FilteredHierarchicalProperties.Where(p => p.IsSelected).ToList();
                if (selectedRecords.Count == 0)
                {
                    MessageBox.Show(
                        "Please select (check) objects to capture in batch.",
                        "Batch Capture",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var uniqueObjectIds = selectedRecords
                    .Select(r => r.ObjectId)
                    .Where(id => id != Guid.Empty)
                    .Distinct()
                    .Count();

                var result = MessageBox.Show(
                    $"Batch capture {uniqueObjectIds} unique objects?\n\n" +
                    $"Each object will be isolated and captured separately.\n" +
                    $"This may take some time for large selections.",
                    "Batch Capture Confirmation",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.OK) return;

                // 저장 폴더 선택
                var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Select output folder for batch captures",
                    ShowNewFolderButton = true
                };

                if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                StatusMessage = "Batch capture in progress...";
                ExportStatusMessage = "Starting batch capture...";

                // 진행률 이벤트 구독
                _snapshotService.SnapshotProgress += OnSnapshotProgress;

                try
                {
                    var capturedFiles = _snapshotService.BatchCaptureFilteredObjects(
                        selectedRecords,
                        folderDialog.SelectedPath,
                        isolateEach: true);

                    StatusMessage = $"Batch capture complete: {capturedFiles.Count} files";

                    MessageBox.Show(
                        $"Batch capture complete!\n\n" +
                        $"Captured: {capturedFiles.Count} images\n" +
                        $"Output folder: {folderDialog.SelectedPath}",
                        "Batch Capture Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                finally
                {
                    _snapshotService.SnapshotProgress -= OnSnapshotProgress;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show(
                    $"Error during batch capture:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 스냅샷 진행률 이벤트 핸들러
        /// </summary>
        private void OnSnapshotProgress(object sender, SnapshotProgressEventArgs e)
        {
            // UI 스레드에서 업데이트
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                ExportProgressPercentage = (int)e.Progress;
                ExportStatusMessage = e.Status == SnapshotStatus.Failed
                    ? $"Failed: {e.ErrorMessage}"
                    : $"Capturing {e.CurrentIndex}/{e.TotalCount}: {e.CurrentItem}";
            });
        }

        /// <summary>
        /// 현재 필터 조건을 기반으로 파일명용 문자열 생성
        /// </summary>
        private string GenerateFilterConditionName()
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(SelectedLevelFilter) && SelectedLevelFilter != "(All)")
                parts.Add(SelectedLevelFilter);

            if (!string.IsNullOrEmpty(SelectedCategoryFilter) && SelectedCategoryFilter != "(All)")
                parts.Add(SelectedCategoryFilter.Replace(" ", "_"));

            if (!string.IsNullOrEmpty(PropertyNameFilter))
                parts.Add(PropertyNameFilter.Replace(" ", "_"));

            if (!string.IsNullOrEmpty(PropertyValueFilter))
                parts.Add(PropertyValueFilter.Replace(" ", "_"));

            return parts.Count > 0 ? string.Join("_", parts) : "Snapshot";
        }

        #endregion
    }
}

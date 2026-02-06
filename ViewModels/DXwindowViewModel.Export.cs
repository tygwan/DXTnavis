using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DXTnavis.Models;
using DXTnavis.Services;
using DXTnavis.Services.Geometry;
using Microsoft.Win32;
using FolderBrowser = System.Windows.Forms.FolderBrowserDialog;

namespace DXTnavis.ViewModels
{
    /// <summary>
    /// DXwindowViewModel - Export 관련 메서드
    /// v0.5.0: Partial Class 분리
    /// </summary>
    public partial class DXwindowViewModel
    {
        #region Export Methods

        /// <summary>
        /// 현재 UI에 표시된 속성을 파일로 저장
        /// v0.4.0: CSV 저장 시 Raw/Refined 두 파일 동시 생성
        /// </summary>
        private void SaveToFile(FileType fileType)
        {
            try
            {
                var saveDialog = new SaveFileDialog();

                if (fileType == FileType.CSV)
                {
                    saveDialog.Filter = "CSV 파일|*.csv";
                    saveDialog.DefaultExt = "csv";
                }
                else
                {
                    saveDialog.Filter = "JSON 파일|*.json";
                    saveDialog.DefaultExt = "json";
                }

                saveDialog.FileName = $"Properties_{DateTime.Now:yyyyMMdd_HHmmss}";

                if (saveDialog.ShowDialog() == true)
                {
                    var properties = new List<PropertyInfo>(SelectedObjectProperties);
                    var writer = new PropertyFileWriter();

                    if (fileType == FileType.CSV)
                    {
                        // v0.4.0: Raw CSV + Refined CSV 동시 저장
                        var (rawPath, refinedPath) = writer.WriteDualCsv(saveDialog.FileName, properties);
                        MessageBox.Show(
                            $"2개 파일이 저장되었습니다:\n\n• Raw: {System.IO.Path.GetFileName(rawPath)}\n• Refined: {System.IO.Path.GetFileName(refinedPath)}",
                            "저장 완료",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    else
                    {
                        writer.WriteFile(saveDialog.FileName, properties, fileType);
                        MessageBox.Show(
                            "파일이 성공적으로 저장되었습니다.",
                            "저장 완료",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"파일 저장 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 모델 전체 속성을 CSV로 내보내기 (All × Properties)
        /// </summary>
        private async Task ExportAllPropertiesAsync()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV 파일|*.csv",
                    DefaultExt = "csv",
                    FileName = $"AllProperties_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() != true) return;

                // UI 상태 초기화
                IsExporting = true;
                ExportProgressPercentage = 0;
                ExportStatusMessage = "내보내기 시작 중...";

                // 진행률 보고를 위한 Progress 인스턴스
                var progress = new Progress<(int percentage, string message)>(report =>
                {
                    ExportProgressPercentage = report.percentage;
                    ExportStatusMessage = report.message;
                });

                // 백그라운드 스레드에서 실행
                await Task.Run(() =>
                {
                    var exporter = new FullModelExporterService();
                    exporter.ExportAllPropertiesToCsv(saveDialog.FileName, progress);
                });

                ExportStatusMessage = "✅ 내보내기 완료!";

                MessageBox.Show(
                    "전체 모델 속성이 성공적으로 저장되었습니다.",
                    "내보내기 완료",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"❌ 오류 발생: {ex.Message}";

                MessageBox.Show(
                    $"전체 내보내기 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        /// <summary>
        /// 선택된 객체의 계층 구조를 CSV/JSON으로 내보내기
        /// </summary>
        private async Task ExportSelectionHierarchyAsync()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV 파일|*.csv|JSON (Flat)|*.json|JSON (Tree)|*.json",
                    DefaultExt = "csv",
                    FileName = $"Hierarchy_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() != true) return;

                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("활성 문서가 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedItems = doc.CurrentSelection.SelectedItems;
                if (selectedItems == null || selectedItems.Count == 0)
                {
                    MessageBox.Show("먼저 객체를 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                ExportStatusMessage = "계층 구조 데이터 추출 중...";

                // UI 스레드에서 Navisworks API 데이터 추출
                List<HierarchicalPropertyRecord> hierarchicalData = null;
                var extractor = new NavisworksDataExtractor();
                hierarchicalData = extractor.ExtractHierarchicalRecordsFromSelection(selectedItems);

                // 데이터 검증
                if (hierarchicalData == null || hierarchicalData.Count == 0)
                {
                    ExportStatusMessage = "";
                    MessageBox.Show(
                        "선택된 객체에서 추출할 수 있는 속성 데이터가 없습니다.\n\n" +
                        "가능한 원인:\n" +
                        "- 선택된 객체가 숨겨진 상태이거나 형상이 없습니다.\n" +
                        "- 선택된 객체에 속성이 없습니다.",
                        "데이터 없음",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                // UI 스레드에서 AllHierarchicalProperties 업데이트
                AllHierarchicalProperties.Clear();
                foreach (var record in hierarchicalData)
                {
                    AllHierarchicalProperties.Add(record);
                }

                // FilteredHierarchicalProperties 동기화
                SyncFilteredProperties();

                // 파일로 저장
                await Task.Run(() =>
                {
                    var writer = new HierarchyFileWriter();
                    int filterIndex = saveDialog.FilterIndex;

                    if (filterIndex == 1) // CSV
                    {
                        writer.WriteToCsv(saveDialog.FileName, hierarchicalData);
                    }
                    else if (filterIndex == 2) // JSON Flat
                    {
                        writer.WriteToJsonFlat(saveDialog.FileName, hierarchicalData);
                    }
                    else // JSON Tree
                    {
                        writer.WriteToJsonTree(saveDialog.FileName, hierarchicalData);
                    }
                });

                ExportStatusMessage = "Export completed!";
                StatusMessage = $"Exported: {hierarchicalData.Count} items to file";

                MessageBox.Show(
                    "Hierarchy exported successfully.",
                    "Export Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"❌ 오류: {ex.Message}";
                MessageBox.Show(
                    $"계층 구조 내보내기 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 현재 선택된 객체의 속성을 CSV로 내보내기 (Selection × Properties)
        /// </summary>
        private async Task ExportSelectionPropertiesAsync()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("활성 문서가 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedItems = doc.CurrentSelection.SelectedItems;
                if (selectedItems == null || selectedItems.Count == 0)
                {
                    MessageBox.Show("먼저 객체를 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV 파일|*.csv",
                    DefaultExt = "csv",
                    FileName = $"SelectionProperties_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() != true) return;

                IsExporting = true;
                ExportStatusMessage = "선택 객체 속성 추출 중...";

                // UI 스레드에서 Navisworks API 호출
                var extractor = new NavisworksDataExtractor();
                var properties = extractor.ExtractPropertiesFromSelection(selectedItems);

                if (properties == null || properties.Count == 0)
                {
                    ExportStatusMessage = "";
                    MessageBox.Show("선택된 객체에서 속성을 추출할 수 없습니다.", "데이터 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 파일 저장
                await Task.Run(() =>
                {
                    var writer = new PropertyFileWriter();
                    writer.WriteFile(saveDialog.FileName, properties, FileType.CSV);
                });

                ExportStatusMessage = "✅ Selection Properties 내보내기 완료!";
                StatusMessage = $"Exported: {properties.Count} properties";

                MessageBox.Show(
                    $"선택 객체 속성이 저장되었습니다.\n\n속성 개수: {properties.Count}",
                    "내보내기 완료",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"❌ 오류: {ex.Message}";
                MessageBox.Show(
                    $"Selection Properties 내보내기 중 오류:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        /// <summary>
        /// 전체 모델의 계층 구조를 CSV/JSON으로 내보내기 (All × Hierarchy)
        /// </summary>
        private async Task ExportAllHierarchyAsync()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("활성 문서가 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV 파일|*.csv|JSON (Flat)|*.json|JSON (Tree)|*.json",
                    DefaultExt = "csv",
                    FileName = $"AllHierarchy_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() != true) return;

                IsExporting = true;
                ExportStatusMessage = "전체 모델 계층 구조 추출 중...";

                // UI 스레드에서 전체 모델 계층 추출
                var extractor = new NavisworksDataExtractor();
                var hierarchicalData = extractor.ExtractAllHierarchicalRecords();

                if (hierarchicalData == null || hierarchicalData.Count == 0)
                {
                    ExportStatusMessage = "";
                    MessageBox.Show("모델에서 계층 데이터를 추출할 수 없습니다.", "데이터 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 파일 저장
                await Task.Run(() =>
                {
                    var writer = new HierarchyFileWriter();
                    int filterIndex = saveDialog.FilterIndex;

                    if (filterIndex == 1) // CSV
                    {
                        writer.WriteToCsv(saveDialog.FileName, hierarchicalData);
                    }
                    else if (filterIndex == 2) // JSON Flat
                    {
                        writer.WriteToJsonFlat(saveDialog.FileName, hierarchicalData);
                    }
                    else // JSON Tree
                    {
                        writer.WriteToJsonTree(saveDialog.FileName, hierarchicalData);
                    }
                });

                ExportStatusMessage = "✅ All Hierarchy 내보내기 완료!";
                StatusMessage = $"Exported: {hierarchicalData.Count} items";

                MessageBox.Show(
                    $"전체 모델 계층 구조가 저장되었습니다.\n\n항목 개수: {hierarchicalData.Count}",
                    "내보내기 완료",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"❌ 오류: {ex.Message}";
                MessageBox.Show(
                    $"All Hierarchy 내보내기 중 오류:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        #endregion

        #region Geometry Export Methods (Phase 15)

        /// <summary>
        /// 전체 모델의 Geometry (BoundingBox) 데이터를 manifest.json으로 내보내기
        /// Phase 15: Geometry Export System
        /// </summary>
        private async Task ExportGeometryAsync()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("활성 문서가 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 폴더 선택 다이얼로그
                using (var folderDialog = new FolderBrowser())
                {
                    folderDialog.Description = "Geometry Export 폴더 선택";
                    folderDialog.ShowNewFolderButton = true;

                    if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                        return;

                    var exportPath = folderDialog.SelectedPath;

                    IsExporting = true;
                    ExportProgressPercentage = 0;
                    ExportStatusMessage = "Geometry 추출 준비 중...";

                    // 서비스 인스턴스 생성
                    var extractor = new GeometryExtractor();
                    var writer = new GeometryFileWriter();

                    // 진행률 이벤트 연결
                    extractor.ProgressChanged += (s, p) =>
                    {
                        ExportProgressPercentage = (int)(p * 0.8); // 80%까지 추출
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() => { });
                    };
                    extractor.StatusChanged += (s, msg) =>
                    {
                        ExportStatusMessage = msg;
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() => { });
                    };

                    writer.ProgressChanged += (s, p) =>
                    {
                        ExportProgressPercentage = 80 + (int)(p * 0.2); // 80-100% 저장
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() => { });
                    };
                    writer.StatusChanged += (s, msg) =>
                    {
                        ExportStatusMessage = msg;
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() => { });
                    };

                    // UI 스레드에서 Navisworks API 호출 (BoundingBox 추출)
                    ExportStatusMessage = "BoundingBox 데이터 추출 중...";
                    var geometryRecords = extractor.ExtractFromDocument(doc);

                    if (geometryRecords == null || geometryRecords.Count == 0)
                    {
                        ExportStatusMessage = "";
                        MessageBox.Show("모델에서 Geometry 데이터를 추출할 수 없습니다.", "데이터 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 폴더 구조 생성
                    var structure = writer.CreateExportStructure(exportPath);
                    if (!structure.IsValid)
                    {
                        MessageBox.Show("Export 폴더 구조 생성에 실패했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 파일 저장 (백그라운드)
                    string manifestPath = null;
                    await Task.Run(() =>
                    {
                        // manifest.json 저장
                        manifestPath = writer.WriteManifest(geometryRecords, exportPath, doc.Title ?? "Navisworks Model");

                        // geometry.csv도 함께 저장 (대안 포맷)
                        writer.WriteCsv(geometryRecords, structure.CsvPath);
                    });

                    ExportProgressPercentage = 100;
                    ExportStatusMessage = "✅ Geometry Export 완료!";
                    StatusMessage = $"Geometry Exported: {geometryRecords.Count} objects";

                    MessageBox.Show(
                        $"Geometry Export 완료!\n\n" +
                        $"• 객체 수: {geometryRecords.Count:N0}\n" +
                        $"• manifest.json: {System.IO.Path.GetFileName(manifestPath)}\n" +
                        $"• geometry.csv: {System.IO.Path.GetFileName(structure.CsvPath)}\n\n" +
                        $"폴더: {exportPath}",
                        "Geometry Export 완료",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"❌ Geometry Export 오류: {ex.Message}";
                MessageBox.Show(
                    $"Geometry Export 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        /// <summary>
        /// 선택된 객체의 Geometry 데이터를 내보내기
        /// </summary>
        private async Task ExportSelectionGeometryAsync()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("활성 문서가 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedItems = doc.CurrentSelection.SelectedItems;
                if (selectedItems == null || selectedItems.Count == 0)
                {
                    MessageBox.Show("먼저 객체를 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 폴더 선택
                using (var folderDialog = new FolderBrowser())
                {
                    folderDialog.Description = "Selection Geometry Export 폴더 선택";
                    folderDialog.ShowNewFolderButton = true;

                    if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                        return;

                    var exportPath = folderDialog.SelectedPath;

                    IsExporting = true;
                    ExportProgressPercentage = 0;
                    ExportStatusMessage = "선택 객체 Geometry 추출 중...";

                    var extractor = new GeometryExtractor();
                    var writer = new GeometryFileWriter();

                    // 진행률 연결
                    extractor.ProgressChanged += (s, p) => ExportProgressPercentage = (int)(p * 0.8);
                    extractor.StatusChanged += (s, msg) => ExportStatusMessage = msg;
                    writer.ProgressChanged += (s, p) => ExportProgressPercentage = 80 + (int)(p * 0.2);

                    // UI 스레드에서 추출
                    var geometryRecords = extractor.ExtractFromSelection(selectedItems);

                    if (geometryRecords == null || geometryRecords.Count == 0)
                    {
                        ExportStatusMessage = "";
                        MessageBox.Show("선택된 객체에서 Geometry를 추출할 수 없습니다.", "데이터 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 저장
                    var structure = writer.CreateExportStructure(exportPath);
                    await Task.Run(() =>
                    {
                        writer.WriteManifest(geometryRecords, exportPath, "Selection Export");
                        writer.WriteCsv(geometryRecords, structure.CsvPath);
                    });

                    ExportProgressPercentage = 100;
                    ExportStatusMessage = "✅ Selection Geometry Export 완료!";

                    MessageBox.Show(
                        $"Selection Geometry Export 완료!\n\n객체 수: {geometryRecords.Count:N0}",
                        "Export 완료",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"❌ 오류: {ex.Message}";
                MessageBox.Show($"Selection Geometry Export 오류:\n\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        #endregion

        #region Unified CSV Export Methods (Phase 16)

        /// <summary>
        /// 전체 모델의 통합 CSV 내보내기 (Hierarchy + Geometry + Manifest)
        /// Phase 16: Unified CSV Export for Ontology Conversion
        /// </summary>
        private async Task ExportUnifiedCsvAsync()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("활성 문서가 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV 파일|*.csv",
                    DefaultExt = "csv",
                    FileName = $"UnifiedExport_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() != true) return;

                IsExporting = true;
                ExportProgressPercentage = 0;
                ExportStatusMessage = "통합 CSV 내보내기 준비 중...";

                var exporter = new UnifiedCsvExporter();

                // 진행률 이벤트 연결
                exporter.ProgressChanged += (s, p) =>
                {
                    ExportProgressPercentage = p;
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() => { });
                };
                exporter.StatusChanged += (s, msg) =>
                {
                    ExportStatusMessage = msg;
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() => { });
                };

                // UI 스레드에서 Navisworks API 호출이 필요하므로 동기 실행
                int exportedCount = exporter.ExportFromDocument(saveDialog.FileName);

                ExportProgressPercentage = 100;
                ExportStatusMessage = "✅ 통합 CSV 내보내기 완료!";
                StatusMessage = $"Unified CSV Exported: {exportedCount} objects";

                MessageBox.Show(
                    $"통합 CSV 내보내기 완료!\n\n" +
                    $"• 객체 수: {exportedCount:N0}\n" +
                    $"• 파일: {System.IO.Path.GetFileName(saveDialog.FileName)}\n\n" +
                    $"이 파일을 bim-ontology 프로젝트에서 사용할 수 있습니다.",
                    "Unified CSV Export 완료",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"❌ Unified CSV 오류: {ex.Message}";
                MessageBox.Show(
                    $"통합 CSV 내보내기 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        /// <summary>
        /// 선택된 객체의 통합 CSV 내보내기
        /// </summary>
        private async Task ExportSelectionUnifiedCsvAsync()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("활성 문서가 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedItems = doc.CurrentSelection.SelectedItems;
                if (selectedItems == null || selectedItems.Count == 0)
                {
                    MessageBox.Show("먼저 객체를 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV 파일|*.csv",
                    DefaultExt = "csv",
                    FileName = $"UnifiedSelection_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveDialog.ShowDialog() != true) return;

                IsExporting = true;
                ExportProgressPercentage = 0;
                ExportStatusMessage = "선택 객체 통합 CSV 내보내기 중...";

                var exporter = new UnifiedCsvExporter();

                exporter.ProgressChanged += (s, p) => ExportProgressPercentage = p;
                exporter.StatusChanged += (s, msg) => ExportStatusMessage = msg;

                int exportedCount = exporter.ExportFromSelection(selectedItems, saveDialog.FileName);

                ExportProgressPercentage = 100;
                ExportStatusMessage = "✅ Selection Unified CSV 완료!";

                MessageBox.Show(
                    $"선택 객체 통합 CSV 내보내기 완료!\n\n객체 수: {exportedCount:N0}",
                    "Export 완료",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"❌ 오류: {ex.Message}";
                MessageBox.Show($"Selection Unified CSV 오류:\n\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        #endregion
    }
}


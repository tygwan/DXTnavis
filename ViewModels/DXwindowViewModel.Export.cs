using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.Navisworks.Api;
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

        #region Spatial Adjacency Export Methods (Phase 17)

        /// <summary>
        /// Phase 17: 전체 모델 Spatial Adjacency Export
        /// </summary>
        private async System.Threading.Tasks.Task ExportAdjacencyAsync()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("Navisworks 문서가 열려있지 않습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Spatial 분석 결과를 저장할 폴더를 선택하세요",
                    ShowNewFolderButton = true
                };

                if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                string outputDir = System.IO.Path.Combine(folderDialog.SelectedPath,
                    string.Format("spatial_{0:yyyyMMdd_HHmmss}", DateTime.Now));

                IsExporting = true;
                ExportProgressPercentage = 0;
                ExportStatusMessage = "Geometry 추출 중...";

                var sw = System.Diagnostics.Stopwatch.StartNew();

                // 1) Geometry 추출
                var geoExtractor = new Services.Geometry.GeometryExtractor();
                geoExtractor.ProgressChanged += (s, p) => ExportProgressPercentage = p / 3;
                geoExtractor.StatusChanged += (s, msg) => ExportStatusMessage = msg;

                var geometries = geoExtractor.ExtractFromDocument(doc);

                // 2) Adjacency 검출
                ExportStatusMessage = string.Format("Adjacency 검출 중... ({0:N0}개 객체)", geometries.Count);
                var detector = new Services.Spatial.AdjacencyDetector();
                detector.ProgressChanged += (s, p) => ExportProgressPercentage = 33 + p / 3;
                detector.StatusChanged += (s, msg) => ExportStatusMessage = msg;

                var adjacencies = detector.Detect(geometries);

                // 3) Connected Components
                ExportStatusMessage = "연결 그룹 분석 중...";
                ExportProgressPercentage = 66;
                var componentFinder = new Services.Spatial.ConnectedComponentFinder();
                var groups = componentFinder.FindAndCompute(adjacencies, geometries);

                // 4) 파일 출력
                ExportStatusMessage = "파일 저장 중...";
                var writer = new Services.Spatial.SpatialRelationshipWriter();
                writer.StatusChanged += (s, msg) => ExportStatusMessage = msg;

                writer.WriteAdjacencyCsv(adjacencies, outputDir);
                writer.WriteGroupsCsv(groups, outputDir);
                writer.WriteTtl(adjacencies, groups, outputDir);

                sw.Stop();
                writer.WriteSummary(adjacencies, groups, outputDir, sw.Elapsed.TotalSeconds);

                ExportProgressPercentage = 100;
                ExportStatusMessage = "Spatial 분석 완료!";

                MessageBox.Show(
                    string.Format("Spatial 분석 완료!\n\n"
                        + "객체 수: {0:N0}\n"
                        + "인접 관계: {1:N0}\n"
                        + "연결 그룹: {2:N0}\n"
                        + "처리 시간: {3:F1}초\n\n"
                        + "저장 위치: {4}",
                        geometries.Count, adjacencies.Count, groups.Count,
                        sw.Elapsed.TotalSeconds, outputDir),
                    "Spatial Analysis",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = string.Format("오류: {0}", ex.Message);
                MessageBox.Show(string.Format("Spatial 분석 오류:\n\n{0}", ex.Message), "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        /// <summary>
        /// Phase 17: 선택 객체 Spatial Adjacency Export
        /// </summary>
        private async System.Threading.Tasks.Task ExportSelectionAdjacencyAsync()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("Navisworks 문서가 열려있지 않습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedItems = doc.CurrentSelection.SelectedItems;
                if (selectedItems == null || selectedItems.Count == 0)
                {
                    MessageBox.Show("먼저 객체를 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "Spatial 분석 결과를 저장할 폴더를 선택하세요",
                    ShowNewFolderButton = true
                };

                if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                string outputDir = System.IO.Path.Combine(folderDialog.SelectedPath,
                    string.Format("spatial_sel_{0:yyyyMMdd_HHmmss}", DateTime.Now));

                IsExporting = true;
                ExportProgressPercentage = 0;
                ExportStatusMessage = "선택 객체 Geometry 추출 중...";

                var sw = System.Diagnostics.Stopwatch.StartNew();

                // 1) 선택 객체 Geometry 추출
                var geoExtractor = new Services.Geometry.GeometryExtractor();
                geoExtractor.ProgressChanged += (s, p) => ExportProgressPercentage = p / 3;
                geoExtractor.StatusChanged += (s, msg) => ExportStatusMessage = msg;

                var geometries = geoExtractor.ExtractFromSelection(selectedItems);

                // 2) Adjacency 검출
                ExportStatusMessage = string.Format("Adjacency 검출 중... ({0:N0}개 객체)", geometries.Count);
                var detector = new Services.Spatial.AdjacencyDetector();
                detector.ProgressChanged += (s, p) => ExportProgressPercentage = 33 + p / 3;
                detector.StatusChanged += (s, msg) => ExportStatusMessage = msg;

                var adjacencies = detector.Detect(geometries);

                // 3) Connected Components
                ExportStatusMessage = "연결 그룹 분석 중...";
                ExportProgressPercentage = 66;
                var componentFinder = new Services.Spatial.ConnectedComponentFinder();
                var groups = componentFinder.FindAndCompute(adjacencies, geometries);

                // 4) 파일 출력
                ExportStatusMessage = "파일 저장 중...";
                var writer = new Services.Spatial.SpatialRelationshipWriter();
                writer.StatusChanged += (s, msg) => ExportStatusMessage = msg;

                writer.WriteAdjacencyCsv(adjacencies, outputDir);
                writer.WriteGroupsCsv(groups, outputDir);
                writer.WriteTtl(adjacencies, groups, outputDir);

                sw.Stop();
                writer.WriteSummary(adjacencies, groups, outputDir, sw.Elapsed.TotalSeconds);

                ExportProgressPercentage = 100;
                ExportStatusMessage = "Selection Spatial 분석 완료!";

                MessageBox.Show(
                    string.Format("Selection Spatial 분석 완료!\n\n"
                        + "객체 수: {0:N0}\n"
                        + "인접 관계: {1:N0}\n"
                        + "연결 그룹: {2:N0}\n"
                        + "처리 시간: {3:F1}초\n\n"
                        + "저장 위치: {4}",
                        geometries.Count, adjacencies.Count, groups.Count,
                        sw.Elapsed.TotalSeconds, outputDir),
                    "Spatial Analysis",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = string.Format("오류: {0}", ex.Message);
                MessageBox.Show(string.Format("Selection Spatial 분석 오류:\n\n{0}", ex.Message), "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        #endregion

        #region Test Mesh Export (Phase 18: 단일 객체 GLB 검증)

        /// <summary>
        /// 선택된 단일 객체의 Mesh를 GLB로 추출하여 검증
        /// </summary>
        private async System.Threading.Tasks.Task ExportTestMeshAsync()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("Navisworks 문서가 열려있지 않습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selection = doc.CurrentSelection.SelectedItems;
                if (selection.Count == 0)
                {
                    MessageBox.Show("Mesh를 추출할 객체를 선택하세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "GLB 파일을 저장할 폴더를 선택하세요",
                    ShowNewFolderButton = true
                };
                if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                IsExporting = true;
                ExportProgressPercentage = 0;
                ExportStatusMessage = "Mesh 추출 준비 중...";

                var geoExtractor = new Services.Geometry.GeometryExtractor();
                var meshExtractor = new Services.Geometry.MeshExtractor();
                meshExtractor.StatusChanged += (s, msg) => ExportStatusMessage = msg;

                // Resolve to leaf items: parent/group nodes include all descendant geometry
                var leafItems = new List<ModelItem>();
                foreach (var item in selection)
                {
                    if (item.Children != null && item.Children.Any())
                    {
                        // Parent node → collect leaf descendants
                        foreach (var desc in item.Descendants)
                        {
                            if (desc.Children == null || !desc.Children.Any())
                                leafItems.Add(desc);
                        }
                    }
                    else
                    {
                        leafItems.Add(item);
                    }
                }

                int total = leafItems.Count;
                int meshSuccess = 0;
                int processed = 0;
                string outputDir = folderDialog.SelectedPath;

                ExportStatusMessage = string.Format("Leaf 객체 {0}개 발견 (선택 {1}개에서)", total, selection.Count);

                foreach (var item in leafItems)
                {
                    var record = geoExtractor.ExtractBoundingBox(item);
                    if (record == null || record.ObjectId == System.Guid.Empty)
                    {
                        processed++;
                        continue;
                    }

                    ExportStatusMessage = string.Format("Mesh 추출 중: {0} ({1}/{2})",
                        item.DisplayName ?? "unknown", processed + 1, total);

                    var meshData = meshExtractor.ExtractMesh(item, record.ObjectId);
                    if (meshData != null && meshData.VertexCount > 0)
                    {
                        var glbPath = System.IO.Path.Combine(outputDir,
                            string.Format("{0}.glb", record.ObjectId.ToString("D")));
                        meshExtractor.SaveToGlb(meshData, glbPath);

                        var objPath = System.IO.Path.Combine(outputDir,
                            string.Format("{0}.obj", record.ObjectId.ToString("D")));
                        meshExtractor.SaveToObj(meshData, objPath);

                        meshSuccess++;
                    }

                    processed++;
                    ExportProgressPercentage = (int)(100.0 * processed / total);
                }

                meshExtractor.Dispose();

                ExportProgressPercentage = 100;
                ExportStatusMessage = "Test Mesh Export 완료!";

                MessageBox.Show(
                    string.Format("Test Mesh Export 완료!\n\n"
                        + "선택 객체: {0}개\n"
                        + "Mesh 성공: {1}개\n"
                        + "저장 위치: {2}\n\n"
                        + "GLB 확인: https://gltf-viewer.donmccurdy.com/",
                        total, meshSuccess, outputDir),
                    "Test Mesh Export",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = string.Format("오류: {0}", ex.Message);
                MessageBox.Show(string.Format("Test Mesh Export 오류:\n\n{0}", ex.Message), "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        #endregion

        #region Full Pipeline Export (Unified CSV + Spatial + Mesh)

        /// <summary>
        /// 전체 파이프라인: Unified CSV + Geometry + Mesh GLB + Spatial Adjacency 통합 출력
        /// Phase 18: Mesh GLB export 추가
        /// </summary>
        private async System.Threading.Tasks.Task ExportFullPipelineAsync()
        {
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("Navisworks 문서가 열려있지 않습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "전체 Export 결과를 저장할 폴더를 선택하세요",
                    ShowNewFolderButton = true
                };

                if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                string outputDir = System.IO.Path.Combine(folderDialog.SelectedPath,
                    string.Format("dxtnavis_export_{0:yyyyMMdd_HHmmss}", DateTime.Now));
                System.IO.Directory.CreateDirectory(outputDir);

                IsExporting = true;
                ExportProgressPercentage = 0;
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // ──── Stage 1/5: Unified CSV (Hierarchy + Properties + BBox) ────
                ExportStatusMessage = "[1/5] Unified CSV 추출 중...";

                var unifiedPath = System.IO.Path.Combine(outputDir, "unified.csv");
                var exporter = new UnifiedCsvExporter();
                exporter.ProgressChanged += (s, p) => ExportProgressPercentage = p / 5;
                exporter.StatusChanged += (s, msg) => ExportStatusMessage = "[1/5] " + msg;

                int unifiedCount = exporter.ExportFromDocument(unifiedPath);
                ExportProgressPercentage = 20;

                // ──── Stage 2/5: Geometry 추출 ────
                ExportStatusMessage = "[2/5] Geometry 추출 중...";

                var geoExtractor = new Services.Geometry.GeometryExtractor();
                geoExtractor.ProgressChanged += (s, p) => ExportProgressPercentage = 20 + p / 5;
                geoExtractor.StatusChanged += (s, msg) => ExportStatusMessage = "[2/5] " + msg;

                var geometries = geoExtractor.ExtractFromDocument(doc);

                // Geometry CSV (HasMesh/MeshUri는 mesh 추출 후 갱신)
                ExportProgressPercentage = 40;

                // ──── Stage 3/5: Mesh GLB 추출 ────
                ExportStatusMessage = string.Format("[3/5] Mesh GLB 추출 중... ({0:N0}개 객체)", geometries.Count);

                var meshDir = System.IO.Path.Combine(outputDir, "mesh");
                System.IO.Directory.CreateDirectory(meshDir);

                int meshCount = 0;
                using (var meshExtractor = new Services.Geometry.MeshExtractor())
                {
                    meshExtractor.StatusChanged += (s, msg) => ExportStatusMessage = "[3/5] " + msg;

                    // Leaf items only: parent/group nodes aggregate all descendant fragments,
                    // causing duplicated & inflated vertex counts
                    var modelItemMap = geoExtractor.LastModelItemMap;
                    var leafItems = new List<KeyValuePair<Guid, ModelItem>>();
                    foreach (var kvp in modelItemMap)
                    {
                        if (kvp.Value.Children == null || !kvp.Value.Children.Any())
                            leafItems.Add(kvp);
                    }

                    int meshProcessed = 0;
                    int meshTotal = leafItems.Count;

                    ExportStatusMessage = string.Format("[3/5] Mesh GLB 추출 중... ({0:N0}개 leaf 객체, 전체 {1:N0}개 중)", meshTotal, modelItemMap.Count);

                    foreach (var kvp in leafItems)
                    {
                        var objectId = kvp.Key;
                        var item = kvp.Value;

                        try
                        {
                            var meshData = meshExtractor.ExtractMesh(item, objectId);
                            if (meshData != null && meshData.VertexCount > 0)
                            {
                                var glbPath = System.IO.Path.Combine(meshDir,
                                    string.Format("{0}.glb", objectId.ToString("D")));
                                meshExtractor.SaveToGlb(meshData, glbPath);

                                // GeometryRecord 업데이트
                                if (geometries.ContainsKey(objectId))
                                {
                                    geometries[objectId].HasMesh = true;
                                    geometries[objectId].MeshUri = string.Format("mesh/{0}.glb", objectId.ToString("D"));
                                }
                                meshCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                string.Format("[FullPipeline] Mesh skip {0}: {1}", objectId, ex.Message));
                        }

                        meshProcessed++;
                        if (meshProcessed % 50 == 0 || meshProcessed == meshTotal)
                        {
                            ExportProgressPercentage = 40 + (int)(20.0 * meshProcessed / meshTotal);
                        }
                    }
                }
                ExportProgressPercentage = 60;

                // Geometry CSV + Manifest (mesh 정보 반영된 상태)
                var geoWriter = new Services.Geometry.GeometryFileWriter();
                geoWriter.WriteCsv(geometries, System.IO.Path.Combine(outputDir, "geometry.csv"));
                geoWriter.WriteManifest(geometries, outputDir);

                // ──── Stage 4/5: Adjacency 검출 ────
                ExportStatusMessage = string.Format("[4/5] Adjacency 검출 중... ({0:N0}개 객체)", geometries.Count);

                var detector = new Services.Spatial.AdjacencyDetector();
                detector.ProgressChanged += (s, p) => ExportProgressPercentage = 60 + p / 5;
                detector.StatusChanged += (s, msg) => ExportStatusMessage = "[4/5] " + msg;

                var adjacencies = detector.Detect(geometries);

                // Connected Components
                var componentFinder = new Services.Spatial.ConnectedComponentFinder();
                var groups = componentFinder.FindAndCompute(adjacencies, geometries);
                ExportProgressPercentage = 80;

                // ──── Stage 5/5: 파일 출력 ────
                ExportStatusMessage = "[5/5] 파일 저장 중...";

                var spatialWriter = new Services.Spatial.SpatialRelationshipWriter();
                spatialWriter.StatusChanged += (s, msg) => ExportStatusMessage = "[5/5] " + msg;

                spatialWriter.WriteAdjacencyCsv(adjacencies, outputDir);
                spatialWriter.WriteGroupsCsv(groups, outputDir);
                spatialWriter.WriteTtl(adjacencies, groups, outputDir);

                sw.Stop();
                spatialWriter.WriteSummary(adjacencies, groups, outputDir, sw.Elapsed.TotalSeconds);

                ExportProgressPercentage = 100;
                ExportStatusMessage = "Full Pipeline Export 완료!";

                MessageBox.Show(
                    string.Format("Full Pipeline Export 완료!\n\n"
                        + "── Unified CSV ──\n"
                        + "  객체 수: {0:N0}\n\n"
                        + "── Geometry ──\n"
                        + "  BBox 추출: {1:N0}개\n\n"
                        + "── Mesh GLB ──\n"
                        + "  GLB 생성: {2:N0}개\n\n"
                        + "── Spatial ──\n"
                        + "  인접 관계: {3:N0}\n"
                        + "  연결 그룹: {4:N0}\n\n"
                        + "처리 시간: {5:F1}초\n"
                        + "저장 위치: {6}",
                        unifiedCount, geometries.Count, meshCount,
                        adjacencies.Count, groups.Count,
                        sw.Elapsed.TotalSeconds, outputDir),
                    "Full Pipeline Export",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ExportStatusMessage = string.Format("오류: {0}", ex.Message);
                MessageBox.Show(string.Format("Full Pipeline Export 오류:\n\n{0}", ex.Message), "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsExporting = false;
            }
        }

        #endregion
    }
}


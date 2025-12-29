using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Autodesk.Navisworks.Api;
using DXTnavis.Helpers;
using DXTnavis.Models;
using DXTnavis.Services;
using Microsoft.Win32;

namespace DXTnavis.ViewModels
{
    /// <summary>
    /// DXwindow의 ViewModel - v2.0 API 통합
    /// MVVM 패턴을 적용하여 UI와 비즈니스 로직을 분리합니다.
    /// </summary>
    public class DXwindowViewModel : INotifyPropertyChanged, IDisposable
    {
        #region Fields

        private bool _isMonitoring;
        private bool _isExporting;
        private int _exportProgressPercentage;
        private string _exportStatusMessage;
        private HierarchicalPropertyRecord _selectedProperty;
        private string _folderName = "My DX Sets";
        private string _newSetName = "New Search Set";
        private System.Windows.Threading.DispatcherTimer _debounceTimer;
        private bool _isLoadingProperties;

        #endregion

        #region Properties

        /// <summary>
        /// UI에 표시될 속성 목록
        /// ObservableCollection을 사용하여 자동 UI 업데이트
        /// </summary>
        public ObservableCollection<PropertyInfo> SelectedObjectProperties { get; }

        /// <summary>
        /// 내보내기 진행 중 여부
        /// </summary>
        public bool IsExporting
        {
            get => _isExporting;
            set
            {
                _isExporting = value;
                OnPropertyChanged(nameof(IsExporting));
            }
        }

        /// <summary>
        /// 내보내기 진행률 (0-100)
        /// </summary>
        public int ExportProgressPercentage
        {
            get => _exportProgressPercentage;
            set
            {
                _exportProgressPercentage = value;
                OnPropertyChanged(nameof(ExportProgressPercentage));
            }
        }

        /// <summary>
        /// 내보내기 상태 메시지
        /// </summary>
        public string ExportStatusMessage
        {
            get => _exportStatusMessage;
            set
            {
                _exportStatusMessage = value;
                OnPropertyChanged(nameof(ExportStatusMessage));
            }
        }

        /// <summary>
        /// 선택된 속성 (SearchSet 생성용)
        /// </summary>
        public HierarchicalPropertyRecord SelectedProperty
        {
            get => _selectedProperty;
            set
            {
                _selectedProperty = value;
                OnPropertyChanged(nameof(SelectedProperty));
                OnPropertyChanged(nameof(SelectedPropertyInfo));
            }
        }

        /// <summary>
        /// 선택된 속성 정보 텍스트 (체크박스로 선택된 모든 속성)
        /// </summary>
        public string SelectedPropertyInfo
        {
            get
            {
                var selectedProps = AllHierarchicalProperties.Where(p => p.IsSelected).ToList();
                if (selectedProps.Count == 0)
                    return "(중간 패널에서 속성을 체크하세요)";

                var lines = selectedProps.Select(p =>
                    $"• {p.Category} | {p.PropertyName} | {p.PropertyValue}");
                return string.Join("\n", lines);
            }
        }

        /// <summary>
        /// 세트 폴더 이름
        /// </summary>
        public string FolderName
        {
            get => _folderName;
            set
            {
                _folderName = value;
                OnPropertyChanged(nameof(FolderName));
            }
        }

        /// <summary>
        /// 새로 생성할 세트 이름
        /// </summary>
        public string NewSetName
        {
            get => _newSetName;
            set
            {
                _newSetName = value;
                OnPropertyChanged(nameof(NewSetName));
            }
        }

        /// <summary>
        /// 계층 구조 속성 목록 (전체)
        /// </summary>
        public ObservableCollection<HierarchicalPropertyRecord> AllHierarchicalProperties { get; }

        /// <summary>
        /// 객체 계층 구조 TreeView 루트 노드
        /// </summary>
        public ObservableCollection<TreeNodeModel> ObjectHierarchyRoot { get; }

        /// <summary>
        /// 선택된 속성 개수 (체크박스 선택된 항목)
        /// </summary>
        public string SelectedPropertiesCount
        {
            get
            {
                int count = AllHierarchicalProperties.Count(p => p.IsSelected);
                return $"{count}개 선택됨";
            }
        }

        #endregion

        #region Commands

        public ICommand SaveAsCsvCommand { get; }
        public ICommand SaveAsJsonCommand { get; }
        public ICommand ExportAllToCsvCommand { get; }
        public ICommand ExportSelectionHierarchyCommand { get; }
        public ICommand CreateSearchSetCommand { get; }
        public ICommand LoadHierarchyCommand { get; }

        #endregion

        #region Constructor

        public DXwindowViewModel()
        {
            SelectedObjectProperties = new ObservableCollection<PropertyInfo>();
            AllHierarchicalProperties = new ObservableCollection<HierarchicalPropertyRecord>();
            ObjectHierarchyRoot = new ObservableCollection<TreeNodeModel>();

            // 속성 변경 이벤트 구독 (SelectedPropertiesCount 업데이트용)
            AllHierarchicalProperties.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (HierarchicalPropertyRecord item in e.NewItems)
                    {
                        item.PropertyChanged += OnPropertyRecordChanged;
                    }
                }
                if (e.OldItems != null)
                {
                    foreach (HierarchicalPropertyRecord item in e.OldItems)
                    {
                        item.PropertyChanged -= OnPropertyRecordChanged;
                    }
                }
            };

            // Debounce 타이머 초기화 (300ms 지연)
            _debounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                LoadSelectedObjectPropertiesSafe();
            };

            // Command 초기화
            SaveAsCsvCommand = new RelayCommand(
                execute: _ => SaveToFile(FileType.CSV),
                canExecute: _ => SelectedObjectProperties.Count > 0);

            SaveAsJsonCommand = new RelayCommand(
                execute: _ => SaveToFile(FileType.JSON),
                canExecute: _ => SelectedObjectProperties.Count > 0);

            ExportAllToCsvCommand = new AsyncRelayCommand(
                execute: async _ => await ExportAllToCsvAsync());

            ExportSelectionHierarchyCommand = new AsyncRelayCommand(
                execute: async _ => await ExportSelectionHierarchyAsync());

            CreateSearchSetCommand = new RelayCommand(
                execute: _ => CreateSearchSetFromSelectedProperty(),
                canExecute: _ => AllHierarchicalProperties.Any(p => p.IsSelected));

            LoadHierarchyCommand = new AsyncRelayCommand(
                execute: async _ => await LoadModelHierarchyAsync());
        }

        private void OnPropertyRecordChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HierarchicalPropertyRecord.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedPropertiesCount));
                OnPropertyChanged(nameof(SelectedPropertyInfo));

                // *** PRD v6 핵심 수정 ***
                // 체크박스 선택이 변경되었으니, CreateSearchSetCommand의 CanExecute를 다시 평가
                ((RelayCommand)CreateSearchSetCommand).RaiseCanExecuteChanged();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Navisworks 선택 변경 모니터링 시작
        /// </summary>
        public void StartMonitoring()
        {
            if (_isMonitoring) return;

            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (doc == null) return;

            // 선택 변경 이벤트 구독
            doc.CurrentSelection.Changed += OnSelectionChanged;
            _isMonitoring = true;

            // 초기 선택 항목 로드
            LoadSelectedObjectProperties();
        }

        /// <summary>
        /// Navisworks 선택 변경 모니터링 중지
        /// </summary>
        public void StopMonitoring()
        {
            if (!_isMonitoring) return;

            var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
            if (doc != null)
            {
                doc.CurrentSelection.Changed -= OnSelectionChanged;
            }

            _isMonitoring = false;
        }

        /// <summary>
        /// 선택 변경 이벤트 핸들러
        /// Debouncing 패턴을 사용하여 빠른 연속 선택 시 마지막 선택만 처리
        /// </summary>
        private void OnSelectionChanged(object sender, EventArgs e)
        {
            // 타이머 재시작 (이전 대기 중인 로드 취소)
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        /// <summary>
        /// 속성 로드 (Debounce 후 실행)
        /// </summary>
        private void LoadSelectedObjectPropertiesSafe()
        {
            // 이미 로딩 중이면 중복 실행 방지
            if (_isLoadingProperties) return;

            _isLoadingProperties = true;
            try
            {
                LoadSelectedObjectProperties();
            }
            finally
            {
                _isLoadingProperties = false;
            }
        }

        /// <summary>
        /// 선택된 객체의 속성을 로드하여 UI에 표시
        /// </summary>
        private void LoadSelectedObjectProperties()
        {
            try
            {
                // 기존 항목 제거
                SelectedObjectProperties.Clear();

                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null) return;

                var selection = doc.CurrentSelection;
                if (selection == null) return;

                var selectedItems = selection.SelectedItems;
                if (selectedItems == null || selectedItems.Count == 0) return;

                // 첫 번째 선택 항목의 속성만 표시 (여러 개 선택 시)
                var firstItem = selectedItems.First();
                if (firstItem == null) return;

                // 속성 계층 구조 탐색
                var categories = firstItem.PropertyCategories;
                if (categories == null) return;

                foreach (var category in categories)
                {
                    if (category == null) continue;

                    DataPropertyCollection properties = null;
                    try
                    {
                        // AccessViolationException 방지: Properties 접근을 try-catch로 보호
                        properties = category.Properties;
                    }
                    catch (System.AccessViolationException)
                    {
                        // Navisworks API 내부 오류 - 이 카테고리는 건너뜀
                        System.Diagnostics.Debug.WriteLine($"AccessViolationException in LoadSelectedObjectProperties: {category.DisplayName}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error accessing properties: {ex.Message}");
                        continue;
                    }

                    if (properties == null) continue;

                    foreach (DataProperty property in properties)
                    {
                        if (property == null) continue;

                        try
                        {
                            var propInfo = new PropertyInfo
                            {
                                Category = category.DisplayName ?? string.Empty,
                                Name = property.DisplayName ?? string.Empty,
                                Value = property.Value?.ToString() ?? string.Empty
                            };

                            SelectedObjectProperties.Add(propInfo);
                        }
                        catch
                        {
                            // 개별 속성 로드 실패는 건너뛰기
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 심각한 오류만 사용자에게 표시
                System.Diagnostics.Debug.WriteLine($"속성 로드 중 오류: {ex.Message}");
                // MessageBox는 표시하지 않음 - 사용자 경험 개선
            }
        }

        /// <summary>
        /// 현재 UI에 표시된 속성을 파일로 저장
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
                    var properties = SelectedObjectProperties.ToList();
                    var writer = new PropertyFileWriter();

                    writer.WriteFile(saveDialog.FileName, properties, fileType);

                    MessageBox.Show(
                        "파일이 성공적으로 저장되었습니다.",
                        "저장 완료",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
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
        /// 모델 전체 속성을 CSV로 내보내기
        /// </summary>
        private async Task ExportAllToCsvAsync()
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

                // *** Error7 최적화: Navisworks API 호출은 반드시 UI 스레드에서 실행 ***
                // selectedItems를 백그라운드로 넘기면 AccessViolationException 발생!
                List<HierarchicalPropertyRecord> hierarchicalData = null;
                var extractor = new NavisworksDataExtractor();

                // UI 스레드에서 Navisworks API 데이터 추출
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

                ExportStatusMessage = "✅ 계층 구조 저장 완료!";

                MessageBox.Show(
                    "계층 구조가 성공적으로 저장되었습니다.",
                    "저장 완료",
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
        /// 선택된 속성으로 검색 세트 생성 (체크박스로 선택된 속성들 사용)
        /// </summary>
        private void CreateSearchSetFromSelectedProperty()
        {
            try
            {
                var selectedProps = AllHierarchicalProperties.Where(p => p.IsSelected).ToList();
                if (selectedProps.Count == 0)
                {
                    MessageBox.Show("중간 패널에서 속성을 먼저 체크해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 첫 번째 선택된 속성으로 SearchSet 생성
                var firstProp = selectedProps.First();
                var setService = new SetCreationService();
                setService.CreateSearchSetFromProperty(
                    FolderName,
                    NewSetName,
                    firstProp.Category,
                    firstProp.PropertyName,
                    firstProp.PropertyValue
                );

                MessageBox.Show(
                    $"검색 세트 '{NewSetName}'가 '{FolderName}' 폴더에 성공적으로 생성되었습니다.\n\n" +
                    $"기준 속성: {firstProp.Category} | {firstProp.PropertyName} | {firstProp.PropertyValue}",
                    "성공",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"세트 생성 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 모델 전체 계층 구조를 TreeView로 로드
        /// Error7 최적화로 인해 비동기 호출이 제거되었지만, Command 호환성을 위해 Task 반환 유지
        /// </summary>
        private Task LoadModelHierarchyAsync()
        {
            try
            {
                ExportStatusMessage = "모델 계층 구조 로딩 중...";

                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null)
                {
                    MessageBox.Show("활성 문서가 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return Task.CompletedTask;
                }

                // *** Error7 최적화: Navisworks API 호출은 반드시 UI 스레드에서 실행 ***
                // doc.Models와 model.RootItem은 Navisworks API 객체이므로 UI 스레드에서만 접근 가능
                var extractor = new NavisworksDataExtractor();
                var allData = new List<HierarchicalPropertyRecord>();

                // UI 스레드에서 모든 Navisworks API 데이터 추출
                foreach (var model in doc.Models)
                {
                    extractor.TraverseAndExtractProperties(model.RootItem, Guid.Empty, 0, allData);
                }

                if (allData == null || allData.Count == 0)
                {
                    MessageBox.Show("모델에서 데이터를 추출할 수 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return Task.CompletedTask;
                }

                // TreeView 구조 생성
                ObjectHierarchyRoot.Clear();
                var nodeMap = new Dictionary<Guid, TreeNodeModel>();

                // 모든 노드 생성
                foreach (var record in allData.GroupBy(r => r.ObjectId))
                {
                    var firstRecord = record.First();
                    var node = new TreeNodeModel
                    {
                        ObjectId = firstRecord.ObjectId,
                        DisplayName = firstRecord.DisplayName,
                        Level = firstRecord.Level,
                        HasGeometry = true
                    };
                    nodeMap[firstRecord.ObjectId] = node;
                }

                // 계층 구조 연결
                foreach (var record in allData.GroupBy(r => r.ObjectId))
                {
                    var firstRecord = record.First();
                    if (nodeMap.TryGetValue(firstRecord.ObjectId, out var node))
                    {
                        if (firstRecord.ParentId == Guid.Empty)
                        {
                            // 루트 노드
                            ObjectHierarchyRoot.Add(node);
                        }
                        else if (nodeMap.TryGetValue(firstRecord.ParentId, out var parentNode))
                        {
                            // 자식 노드
                            parentNode.Children.Add(node);
                        }
                    }
                }

                // TreeView 선택 이벤트 구독 (IsSelected 속성 변경 감지)
                foreach (var node in nodeMap.Values)
                {
                    node.PropertyChanged += OnTreeNodeSelectionChanged;
                }

                ExportStatusMessage = $"✅ 계층 구조 로드 완료! (총 {nodeMap.Count}개 객체)";

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                ExportStatusMessage = $"❌ 오류: {ex.Message}";
                MessageBox.Show(
                    $"계층 구조 로드 중 오류가 발생했습니다:\n\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// TreeView 노드 선택 시 AllHierarchicalProperties 업데이트
        /// Error7 최적화: UI 스레드에서 Navisworks API 호출 보장
        /// </summary>
        private void OnTreeNodeSelectionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(TreeNodeModel.IsSelected))
                return;

            var node = sender as TreeNodeModel;
            if (node == null || !node.IsSelected)
                return;

            // *** Error7 최적화: Navisworks API는 반드시 UI 스레드에서 호출 ***
            // Task.Run 사용 금지! Application.ActiveDocument는 UI 스레드에서만 안전
            try
            {
                var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
                if (doc == null) return;

                var extractor = new NavisworksDataExtractor();
                var hierarchicalData = new List<HierarchicalPropertyRecord>();

                // UI 스레드에서 직접 실행 (백그라운드 금지)
                foreach (var model in doc.Models)
                {
                    extractor.TraverseAndExtractProperties(model.RootItem, Guid.Empty, 0, hierarchicalData);
                }

                // 선택된 노드의 속성만 필터링
                var selectedNodeProps = hierarchicalData.Where(r => r.ObjectId == node.ObjectId).ToList();

                // 이미 UI 스레드이므로 Dispatcher 불필요
                AllHierarchicalProperties.Clear();
                foreach (var prop in selectedNodeProps)
                {
                    AllHierarchicalProperties.Add(prop);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TreeNode 선택 처리 중 오류: {ex.Message}");
            }
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            StopMonitoring();

            // 타이머 정리
            if (_debounceTimer != null)
            {
                _debounceTimer.Stop();
                _debounceTimer = null;
            }
        }

        #endregion
    }
}

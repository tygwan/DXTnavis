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

        // Filter fields
        private string _selectedCategoryFilter;
        private string _selectedLevelFilter;
        private string _sysPathFilter;
        private string _propertyNameFilter;
        private string _propertyValueFilter;
        private string _statusMessage;

        // Show Only Toggle state (v0.4.3)
        private bool _isShowOnlyActive;

        // Filter debounce timer (v0.4.3)
        private System.Windows.Threading.DispatcherTimer _filterDebounceTimer;

        // 3D Selection Service (Phase 3)
        private readonly NavisworksSelectionService _selectionService;

        // Snapshot Service (Phase 4)
        private readonly SnapshotService _snapshotService;

        // Tree Expand/Collapse (Phase 2)
        private int _selectedExpandLevel;

        // Object Search (v0.4.0)
        private string _objectSearchQuery;

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
                var selectedProps = FilteredHierarchicalProperties.Where(p => p.IsSelected).ToList();
                if (selectedProps.Count == 0)
                    return "(Select properties from the center panel)";

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
                int count = FilteredHierarchicalProperties.Count(p => p.IsSelected);
                return $"{count} selected";
            }
        }

        /// <summary>
        /// 필터링된 계층 구조 속성 목록
        /// </summary>
        public ObservableCollection<HierarchicalPropertyRecord> FilteredHierarchicalProperties { get; }

        /// <summary>
        /// 사용 가능한 카테고리 목록 (필터 ComboBox용)
        /// </summary>
        public ObservableCollection<string> AvailableCategories { get; }

        /// <summary>
        /// 사용 가능한 레벨 목록 (필터 ComboBox용)
        /// </summary>
        public ObservableCollection<string> AvailableLevels { get; }

        /// <summary>
        /// 선택된 레벨 필터 (v0.4.3: 자동 필터 적용)
        /// </summary>
        public string SelectedLevelFilter
        {
            get => _selectedLevelFilter;
            set
            {
                _selectedLevelFilter = value;
                OnPropertyChanged(nameof(SelectedLevelFilter));
                TriggerFilterDebounce();
            }
        }

        /// <summary>
        /// 선택된 카테고리 필터 (v0.4.3: 자동 필터 적용)
        /// </summary>
        public string SelectedCategoryFilter
        {
            get => _selectedCategoryFilter;
            set
            {
                _selectedCategoryFilter = value;
                OnPropertyChanged(nameof(SelectedCategoryFilter));
                TriggerFilterDebounce();
            }
        }

        /// <summary>
        /// 시스템 경로 필터 (예: "Project > Building") (v0.4.3: 자동 필터 적용)
        /// </summary>
        public string SysPathFilter
        {
            get => _sysPathFilter;
            set
            {
                _sysPathFilter = value;
                OnPropertyChanged(nameof(SysPathFilter));
                TriggerFilterDebounce();
            }
        }

        /// <summary>
        /// 속성 이름 필터 (v0.4.3: 자동 필터 적용)
        /// </summary>
        public string PropertyNameFilter
        {
            get => _propertyNameFilter;
            set
            {
                _propertyNameFilter = value;
                OnPropertyChanged(nameof(PropertyNameFilter));
                TriggerFilterDebounce();
            }
        }

        /// <summary>
        /// 속성 값 필터 (v0.4.3: 자동 필터 적용)
        /// </summary>
        public string PropertyValueFilter
        {
            get => _propertyValueFilter;
            set
            {
                _propertyValueFilter = value;
                OnPropertyChanged(nameof(PropertyValueFilter));
                TriggerFilterDebounce();
            }
        }

        /// <summary>
        /// Show Only 토글 상태 (v0.4.3: On/Off 토글)
        /// </summary>
        public bool IsShowOnlyActive
        {
            get => _isShowOnlyActive;
            set
            {
                _isShowOnlyActive = value;
                OnPropertyChanged(nameof(IsShowOnlyActive));
                OnPropertyChanged(nameof(ShowOnlyButtonText));
                OnPropertyChanged(nameof(ShowOnlyButtonColor));
            }
        }

        /// <summary>
        /// Show Only 버튼 텍스트 (토글 상태에 따라 변경)
        /// </summary>
        public string ShowOnlyButtonText => _isShowOnlyActive ? "👁️ Show Only: ON" : "👁️ Show Only: OFF";

        /// <summary>
        /// Show Only 버튼 배경색 (토글 상태에 따라 변경)
        /// </summary>
        public string ShowOnlyButtonColor => _isShowOnlyActive ? "#FF5722" : "#0078D4";

        /// <summary>
        /// 상태 메시지 (StatusBar 표시용)
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        /// <summary>
        /// 사용 가능한 확장 레벨 목록 (Tree 확장/축소용)
        /// </summary>
        public ObservableCollection<int> AvailableExpandLevels { get; }

        /// <summary>
        /// 선택된 확장 레벨
        /// </summary>
        public int SelectedExpandLevel
        {
            get => _selectedExpandLevel;
            set
            {
                _selectedExpandLevel = value;
                OnPropertyChanged(nameof(SelectedExpandLevel));
            }
        }

        /// <summary>
        /// 객체 검색 쿼리 (v0.4.0)
        /// </summary>
        public string ObjectSearchQuery
        {
            get => _objectSearchQuery;
            set
            {
                _objectSearchQuery = value;
                OnPropertyChanged(nameof(ObjectSearchQuery));
            }
        }

        #endregion

        #region Commands

        public ICommand SaveAsCsvCommand { get; }
        public ICommand SaveAsJsonCommand { get; }
        // CSV Export Commands (4종 버튼 체계)
        public ICommand ExportAllPropertiesCommand { get; }      // All × Properties
        public ICommand ExportSelectionPropertiesCommand { get; } // Selection × Properties
        public ICommand ExportAllHierarchyCommand { get; }        // All × Hierarchy
        public ICommand ExportSelectionHierarchyCommand { get; }  // Selection × Hierarchy
        public ICommand CreateSearchSetCommand { get; }
        public ICommand LoadHierarchyCommand { get; }
        public ICommand ApplyFilterCommand { get; }
        public ICommand ClearFilterCommand { get; }

        // 3D Object Selection Commands (Phase 3)
        public ICommand SelectIn3DCommand { get; }
        public ICommand ShowOnlyFilteredCommand { get; }
        public ICommand ShowAllObjectsCommand { get; }
        public ICommand ZoomToFilteredCommand { get; }

        // Tree Expand/Collapse Commands (Phase 2)
        public ICommand ExpandToLevelCommand { get; }
        public ICommand CollapseAllCommand { get; }
        public ICommand ExpandAllCommand { get; }

        // 레벨별 개별 확장 Command (P1 Feature)
        public ICommand ExpandLevelCommand { get; }

        // Snapshot Commands (Phase 4)
        public ICommand CaptureViewCommand { get; }
        public ICommand SaveViewPointCommand { get; }
        public ICommand CaptureWithViewPointCommand { get; }
        public ICommand BatchCaptureCommand { get; }
        public ICommand ResetToHomeCommand { get; }  // v0.4.0: 관측점 초기화

        // Object Search Command (v0.4.0)
        public ICommand SearchObjectCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand ZoomToSearchResultCommand { get; }

        #endregion

        #region Constructor

        public DXwindowViewModel()
        {
            SelectedObjectProperties = new ObservableCollection<PropertyInfo>();
            AllHierarchicalProperties = new ObservableCollection<HierarchicalPropertyRecord>();
            ObjectHierarchyRoot = new ObservableCollection<TreeNodeModel>();
            FilteredHierarchicalProperties = new ObservableCollection<HierarchicalPropertyRecord>();
            AvailableCategories = new ObservableCollection<string>();
            AvailableLevels = new ObservableCollection<string>();

            // Tree Expand Levels 초기화 (Phase 2)
            AvailableExpandLevels = new ObservableCollection<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            SelectedExpandLevel = 2; // 기본값: Level 2까지 확장

            // 3D Selection Service 초기화 (Phase 3)
            _selectionService = new NavisworksSelectionService();

            // Snapshot Service 초기화 (Phase 4)
            _snapshotService = new SnapshotService();

            // 초기 상태 메시지
            StatusMessage = "Ready - Select objects to view hierarchy";

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

            // v0.4.3: 필터 자동 적용 디바운스 타이머 (200ms 지연)
            _filterDebounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _filterDebounceTimer.Tick += (s, e) =>
            {
                _filterDebounceTimer.Stop();
                ApplyFilter();
            };

            // Command 초기화
            SaveAsCsvCommand = new RelayCommand(
                execute: _ => SaveToFile(FileType.CSV),
                canExecute: _ => SelectedObjectProperties.Count > 0);

            SaveAsJsonCommand = new RelayCommand(
                execute: _ => SaveToFile(FileType.JSON),
                canExecute: _ => SelectedObjectProperties.Count > 0);

            // CSV Export Commands (4종 버튼 체계)
            ExportAllPropertiesCommand = new AsyncRelayCommand(
                execute: async _ => await ExportAllPropertiesAsync());

            ExportSelectionPropertiesCommand = new AsyncRelayCommand(
                execute: async _ => await ExportSelectionPropertiesAsync(),
                canExecute: _ => Autodesk.Navisworks.Api.Application.ActiveDocument?.CurrentSelection?.SelectedItems?.Count > 0);

            ExportAllHierarchyCommand = new AsyncRelayCommand(
                execute: async _ => await ExportAllHierarchyAsync());

            ExportSelectionHierarchyCommand = new AsyncRelayCommand(
                execute: async _ => await ExportSelectionHierarchyAsync(),
                canExecute: _ => Autodesk.Navisworks.Api.Application.ActiveDocument?.CurrentSelection?.SelectedItems?.Count > 0);

            CreateSearchSetCommand = new RelayCommand(
                execute: _ => CreateSearchSetFromSelectedProperty(),
                canExecute: _ => FilteredHierarchicalProperties.Any(p => p.IsSelected));

            LoadHierarchyCommand = new AsyncRelayCommand(
                execute: async _ => await LoadModelHierarchyAsync());

            ApplyFilterCommand = new RelayCommand(
                execute: _ => ApplyFilter());

            ClearFilterCommand = new RelayCommand(
                execute: _ => ClearFilter());

            // 3D Object Selection Commands (Phase 3)
            SelectIn3DCommand = new RelayCommand(
                execute: _ => SelectIn3D(),
                canExecute: _ => FilteredHierarchicalProperties.Count > 0);

            ShowOnlyFilteredCommand = new RelayCommand(
                execute: _ => ShowOnlyFiltered(),
                canExecute: _ => FilteredHierarchicalProperties.Count > 0);

            ShowAllObjectsCommand = new RelayCommand(
                execute: _ => ShowAllObjects());

            ZoomToFilteredCommand = new RelayCommand(
                execute: _ => ZoomToFiltered(),
                canExecute: _ => FilteredHierarchicalProperties.Count > 0);

            // Tree Expand/Collapse Commands (Phase 2)
            ExpandToLevelCommand = new RelayCommand(
                execute: _ => ExpandTreeToLevel(SelectedExpandLevel),
                canExecute: _ => ObjectHierarchyRoot.Count > 0);

            CollapseAllCommand = new RelayCommand(
                execute: _ => CollapseAllTreeNodes(),
                canExecute: _ => ObjectHierarchyRoot.Count > 0);

            ExpandAllCommand = new RelayCommand(
                execute: _ => ExpandAllTreeNodes(),
                canExecute: _ => ObjectHierarchyRoot.Count > 0);

            // 레벨별 개별 확장 Command (P1 Feature)
            // Parameter: int level (클릭한 레벨 번호)
            ExpandLevelCommand = new RelayCommand(
                execute: param => ExpandToSpecificLevel(Convert.ToInt32(param)),
                canExecute: _ => ObjectHierarchyRoot.Count > 0);

            // Snapshot Commands (Phase 4)
            CaptureViewCommand = new RelayCommand(
                execute: _ => CaptureCurrentView(),
                canExecute: _ => Autodesk.Navisworks.Api.Application.ActiveDocument != null);

            SaveViewPointCommand = new RelayCommand(
                execute: _ => SaveCurrentViewPoint(),
                canExecute: _ => Autodesk.Navisworks.Api.Application.ActiveDocument != null);

            CaptureWithViewPointCommand = new RelayCommand(
                execute: _ => CaptureWithViewPoint(),
                canExecute: _ => Autodesk.Navisworks.Api.Application.ActiveDocument != null);

            BatchCaptureCommand = new RelayCommand(
                execute: _ => BatchCaptureFiltered(),
                canExecute: _ => FilteredHierarchicalProperties.Any(p => p.IsSelected));

            // v0.4.0: 관측점 초기화 명령
            ResetToHomeCommand = new RelayCommand(
                execute: _ => ResetToHome(),
                canExecute: _ => Autodesk.Navisworks.Api.Application.ActiveDocument != null);

            // v0.4.0: 객체 검색 명령
            SearchObjectCommand = new RelayCommand(
                execute: _ => SearchObjects(),
                canExecute: _ => !string.IsNullOrWhiteSpace(ObjectSearchQuery) && AllHierarchicalProperties.Count > 0);

            ClearSearchCommand = new RelayCommand(
                execute: _ => ClearSearch(),
                canExecute: _ => !string.IsNullOrWhiteSpace(ObjectSearchQuery) || FilteredHierarchicalProperties.Count != AllHierarchicalProperties.Count);

            ZoomToSearchResultCommand = new RelayCommand(
                execute: _ => ZoomToSearchResult(),
                canExecute: _ => FilteredHierarchicalProperties.Count > 0 && FilteredHierarchicalProperties.Count <= 100);
        }

        private void OnPropertyRecordChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HierarchicalPropertyRecord.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedPropertiesCount));
                OnPropertyChanged(nameof(SelectedPropertyInfo));

                // 체크박스 선택이 변경되었으니, CreateSearchSetCommand의 CanExecute를 다시 평가
                ((RelayCommand)CreateSearchSetCommand).RaiseCanExecuteChanged();
            }
        }

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
                    var properties = SelectedObjectProperties.ToList();
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

        /// <summary>
        /// 선택된 속성으로 검색 세트 생성 (체크박스로 선택된 속성들 사용)
        /// </summary>
        private void CreateSearchSetFromSelectedProperty()
        {
            try
            {
                var selectedProps = FilteredHierarchicalProperties.Where(p => p.IsSelected).ToList();
                if (selectedProps.Count == 0)
                {
                    MessageBox.Show("Please select properties from the center panel first.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
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

                StatusMessage = $"Search Set '{NewSetName}' created successfully";
                MessageBox.Show(
                    $"Search Set '{NewSetName}' created in folder '{FolderName}'.\n\n" +
                    $"Based on: {firstProp.Category} | {firstProp.PropertyName} | {firstProp.PropertyValue}",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show(
                    $"Error creating search set:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 모델 전체 계층 구조를 TreeView로 로드
        /// v0.4.1: ModelItem 계층 구조를 직접 사용하여 모든 레벨 노드 포함
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

                // *** v0.4.1: ModelItem에서 직접 TreeView 구조 생성 ***
                // 컨테이너 노드(속성 없음)도 포함하여 완전한 계층 구조 유지
                ObjectHierarchyRoot.Clear();
                var allNodes = new List<TreeNodeModel>();
                int totalNodeCount = 0;

                foreach (var model in doc.Models)
                {
                    if (model?.RootItem == null) continue;

                    var rootNode = BuildTreeFromModelItem(model.RootItem, 0, allNodes);
                    if (rootNode != null)
                    {
                        ObjectHierarchyRoot.Add(rootNode);
                    }
                }

                totalNodeCount = allNodes.Count;

                if (totalNodeCount == 0)
                {
                    MessageBox.Show("모델에서 데이터를 추출할 수 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                    return Task.CompletedTask;
                }

                // TreeView 선택 이벤트 구독 (IsSelected 속성 변경 감지)
                foreach (var node in allNodes)
                {
                    node.PropertyChanged += OnTreeNodeSelectionChanged;
                }

                // *** 중앙 패널용 HierarchicalPropertyRecord 데이터 추출 ***
                var extractor = new NavisworksDataExtractor();
                var allData = new List<HierarchicalPropertyRecord>();

                foreach (var model in doc.Models)
                {
                    extractor.TraverseAndExtractProperties(model.RootItem, Guid.Empty, 0, allData);
                }

                AllHierarchicalProperties.Clear();
                foreach (var record in allData)
                {
                    AllHierarchicalProperties.Add(record);
                }

                // FilteredHierarchicalProperties 동기화 (필터링 기능 활성화)
                SyncFilteredProperties();

                // 트리 명령 갱신 (Phase 2)
                RefreshTreeCommands();

                // 기본적으로 Level 2까지 확장
                ExpandTreeToLevel(SelectedExpandLevel);

                ExportStatusMessage = $"Hierarchy loaded!";
                StatusMessage = $"Loaded: {allData.Count} properties from {totalNodeCount} nodes (including containers)";

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
        /// v0.4.1: ModelItem에서 직접 TreeNodeModel 트리를 재귀적으로 구축
        /// 속성 유무와 관계없이 모든 노드를 포함하여 완전한 계층 구조 생성
        /// </summary>
        /// <param name="item">현재 ModelItem</param>
        /// <param name="level">현재 레벨 (0부터 시작)</param>
        /// <param name="allNodes">모든 노드 수집용 리스트</param>
        /// <returns>TreeNodeModel (숨김 상태면 null)</returns>
        private TreeNodeModel BuildTreeFromModelItem(ModelItem item, int level, List<TreeNodeModel> allNodes)
        {
            if (item == null || item.IsHidden)
                return null;

            // 현재 노드 생성
            var node = new TreeNodeModel
            {
                ObjectId = item.InstanceGuid,
                DisplayName = GetDisplayNameFromModelItem(item),
                Level = level,
                HasGeometry = item.HasGeometry
            };

            allNodes.Add(node);

            // 재귀적으로 모든 자식 노드 추가 (속성 유무 무관)
            foreach (ModelItem child in item.Children)
            {
                var childNode = BuildTreeFromModelItem(child, level + 1, allNodes);
                if (childNode != null)
                {
                    node.Children.Add(childNode);
                }
            }

            return node;
        }

        /// <summary>
        /// ModelItem에서 표시 이름 추출
        /// </summary>
        private string GetDisplayNameFromModelItem(ModelItem item)
        {
            try
            {
                // 먼저 DisplayName 속성 시도
                if (!string.IsNullOrWhiteSpace(item.DisplayName))
                    return item.DisplayName;

                // "Item" 카테고리의 "Name" 속성 찾기
                foreach (var category in item.PropertyCategories)
                {
                    if (category?.DisplayName == "Item")
                    {
                        try
                        {
                            var properties = category.Properties;
                            foreach (DataProperty property in properties)
                            {
                                if (property?.DisplayName == "Name")
                                {
                                    return property.Value?.ToString() ?? item.InstanceGuid.ToString();
                                }
                            }
                        }
                        catch { continue; }
                    }
                }

                // 이름을 찾지 못하면 GUID 사용
                return item.InstanceGuid.ToString();
            }
            catch
            {
                return "Unknown";
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

                // FilteredHierarchicalProperties 동기화
                SyncFilteredProperties();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TreeNode 선택 처리 중 오류: {ex.Message}");
            }
        }

        #endregion

        #region Tree Expand/Collapse Methods (Phase 2)

        /// <summary>
        /// 지정된 레벨까지 트리 확장
        /// </summary>
        private void ExpandTreeToLevel(int targetLevel)
        {
            try
            {
                foreach (var node in ObjectHierarchyRoot)
                {
                    node.ExpandToLevel(targetLevel);
                }
                StatusMessage = $"Tree expanded to Level {targetLevel}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// 모든 트리 노드 축소
        /// </summary>
        private void CollapseAllTreeNodes()
        {
            try
            {
                foreach (var node in ObjectHierarchyRoot)
                {
                    node.CollapseAll();
                }
                StatusMessage = "Tree collapsed";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// 모든 트리 노드 확장
        /// </summary>
        private void ExpandAllTreeNodes()
        {
            try
            {
                foreach (var node in ObjectHierarchyRoot)
                {
                    node.ExpandAll();
                }
                StatusMessage = "Tree fully expanded";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// 트리 확장/축소 명령의 CanExecute 갱신
        /// </summary>
        private void RefreshTreeCommands()
        {
            ((RelayCommand)ExpandToLevelCommand)?.RaiseCanExecuteChanged();
            ((RelayCommand)CollapseAllCommand)?.RaiseCanExecuteChanged();
            ((RelayCommand)ExpandAllCommand)?.RaiseCanExecuteChanged();
            ((RelayCommand)ExpandLevelCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// 특정 레벨까지 정확히 확장 (P1 Feature)
        /// 클릭한 레벨까지 확장하고, 그 이후 레벨은 축소
        /// </summary>
        /// <param name="targetLevel">확장할 레벨 (0부터 시작)</param>
        private void ExpandToSpecificLevel(int targetLevel)
        {
            try
            {
                foreach (var node in ObjectHierarchyRoot)
                {
                    node.ExpandExactlyToLevel(targetLevel);
                }
                StatusMessage = $"Expanded to L{targetLevel} (children collapsed)";
                SelectedExpandLevel = targetLevel; // ComboBox도 동기화
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        #endregion

        #region 3D Object Selection Methods (Phase 3)

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

        #region Snapshot Methods (Phase 4)

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

            // v0.4.3: 필터 타이머 정리
            if (_filterDebounceTimer != null)
            {
                _filterDebounceTimer.Stop();
                _filterDebounceTimer = null;
            }
        }

        #endregion
    }
}

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
// using DXTnavis.Services.Ontology; // Phase 14: 임시 비활성화
using Microsoft.Win32;

namespace DXTnavis.ViewModels
{
    /// <summary>
    /// DXwindow의 ViewModel - v2.0 API 통합
    /// MVVM 패턴을 적용하여 UI와 비즈니스 로직을 분리합니다.
    /// </summary>
    public partial class DXwindowViewModel : INotifyPropertyChanged, IDisposable
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

        // Select All (v0.6.1)
        private bool _isAllSelected;
        private bool _isUpdatingSelectAll;

        // Grouped View (Phase 11 → Phase 12: 기본 그룹 뷰)
        private bool _isGroupedViewEnabled = true;  // Phase 12: 기본값 true
        private bool _isGroupedViewAvailable = true;

        // Phase 12: Grouped Data Structure
        private bool _isGroupSelectAll;
        private bool _isUpdatingGroupSelectAll;

        // Phase 14: Ontology ViewModel (타입을 object로 변경하여 지연 로딩)
        private object _ontologyVM;

        #endregion

        #region Properties

        /// <summary>
        /// CSV Viewer ViewModel (v0.5.0)
        /// </summary>
        public CsvViewerViewModel CsvViewer { get; }

        /// <summary>
        /// AWP 4D Automation ViewModel (Phase 8)
        /// </summary>
        public AWP4DViewModel AWP4D { get; }

        /// <summary>
        /// Schedule Builder ViewModel (Phase 10)
        /// </summary>
        public ScheduleBuilderViewModel ScheduleBuilder { get; }

        /// <summary>
        /// Ontology ViewModel (Phase 14)
        /// 타입을 object로 변경하여 OntologyViewModel 어셈블리 지연 로딩
        /// XAML 바인딩은 FallbackValue로 처리됨
        /// </summary>
        public object OntologyVM
        {
            get => _ontologyVM;
            set
            {
                _ontologyVM = value;
                OnPropertyChanged(nameof(OntologyVM));
            }
        }

        /// <summary>
        /// 전체 선택 상태 (v0.6.1)
        /// </summary>
        public bool IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                if (_isAllSelected != value)
                {
                    _isAllSelected = value;
                    OnPropertyChanged(nameof(IsAllSelected));

                    // Avoid infinite loop when updating from individual checkbox changes
                    if (!_isUpdatingSelectAll)
                    {
                        SelectAllProperties(value);
                    }
                }
            }
        }

        /// <summary>
        /// Select All 사용 가능 여부 (v0.9.0: 10,000개 이하일 때만 활성화)
        /// </summary>
        public bool IsSelectAllAvailable => FilteredHierarchicalProperties.Count <= 10000;

        /// <summary>
        /// Select All 툴팁 (사용 불가 시 이유 표시)
        /// </summary>
        public string SelectAllTooltip => IsSelectAllAvailable
            ? $"전체 선택/해제 ({FilteredHierarchicalProperties.Count}개)"
            : $"Select All은 10,000개 이하에서만 사용 가능합니다 (현재: {FilteredHierarchicalProperties.Count:N0}개). 필터를 적용해주세요.";

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
        /// Phase 12: 그룹 기반 선택 반영
        /// </summary>
        public string SelectedPropertiesCount
        {
            get
            {
                // Phase 12: 그룹 기반 카운팅
                if (FilteredObjectGroups.Count > 0)
                {
                    int groupCount = FilteredObjectGroups.Count(g => g.IsSelected);
                    int propCount = FilteredObjectGroups
                        .Where(g => g.IsSelected)
                        .Sum(g => g.FilteredPropertyCount);
                    return $"{groupCount} groups ({propCount} props)";
                }
                else
                {
                    int count = FilteredHierarchicalProperties.Count(p => p.IsSelected);
                    return $"{count} selected";
                }
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

        /// <summary>
        /// 그룹화 뷰 활성화 여부 (Phase 11)
        /// </summary>
        public bool IsGroupedViewEnabled
        {
            get => _isGroupedViewEnabled;
            set
            {
                if (_isGroupedViewEnabled != value)
                {
                    _isGroupedViewEnabled = value;
                    OnPropertyChanged(nameof(IsGroupedViewEnabled));

                    if (value)
                    {
                        RefreshGroupedProperties();
                    }
                }
            }
        }

        /// <summary>
        /// 그룹화 뷰 사용 가능 여부 (필터링된 아이템 &lt; 10,000일 때만 true)
        /// </summary>
        public bool IsGroupedViewAvailable
        {
            get => _isGroupedViewAvailable;
            private set
            {
                if (_isGroupedViewAvailable != value)
                {
                    _isGroupedViewAvailable = value;
                    OnPropertyChanged(nameof(IsGroupedViewAvailable));
                    OnPropertyChanged(nameof(GroupedViewTooltip));

                    // 사용 불가능해지면 그룹화 뷰 해제
                    if (!value && _isGroupedViewEnabled)
                    {
                        _isGroupedViewEnabled = false;
                        OnPropertyChanged(nameof(IsGroupedViewEnabled));
                    }
                }
            }
        }

        /// <summary>
        /// 그룹화 뷰 툴팁 (사용 불가 시 이유 표시)
        /// </summary>
        public string GroupedViewTooltip => IsGroupedViewAvailable
            ? "객체별 그룹화 보기 활성화 (현재 아이템 수: " + FilteredHierarchicalProperties.Count + ")"
            : "그룹화 뷰는 10,000개 미만의 아이템에서만 사용 가능합니다 (현재: " + FilteredHierarchicalProperties.Count + ")";

        /// <summary>
        /// 그룹화된 속성 목록 (Phase 11)
        /// </summary>
        public ObservableCollection<ObjectGroupViewModel> GroupedProperties { get; }

        /// <summary>
        /// 그룹 개수
        /// </summary>
        public int GroupCount => GroupedProperties?.Count ?? 0;

        #region Phase 12: Grouped Data Structure

        /// <summary>
        /// Phase 12: 전체 객체 그룹 목록
        /// </summary>
        public ObservableCollection<ObjectGroupModel> AllObjectGroups { get; }

        /// <summary>
        /// Phase 12: 필터링된 객체 그룹 목록
        /// </summary>
        public ObservableCollection<ObjectGroupModel> FilteredObjectGroups { get; }

        /// <summary>
        /// Phase 12: 레벨 필터 옵션 (체크박스 기반)
        /// </summary>
        public ObservableCollection<FilterOption> LevelFilterOptions { get; }

        /// <summary>
        /// Phase 12: 카테고리 필터 옵션 (체크박스 기반)
        /// </summary>
        public ObservableCollection<FilterOption> CategoryFilterOptions { get; }

        /// <summary>
        /// Phase 12: 필터링된 그룹 수
        /// </summary>
        public int FilteredGroupCount => FilteredObjectGroups?.Count ?? 0;

        /// <summary>
        /// Phase 12: 필터링된 총 속성 수
        /// </summary>
        public int FilteredPropertyCount => FilteredObjectGroups?.Sum(g => g.FilteredPropertyCount) ?? 0;

        /// <summary>
        /// Phase 12: 그룹 전체 선택
        /// </summary>
        public bool IsGroupSelectAll
        {
            get => _isGroupSelectAll;
            set
            {
                if (_isGroupSelectAll != value)
                {
                    _isGroupSelectAll = value;
                    OnPropertyChanged(nameof(IsGroupSelectAll));

                    if (!_isUpdatingGroupSelectAll)
                    {
                        SelectAllGroups(value);
                    }
                }
            }
        }

        /// <summary>
        /// Phase 12: 선택된 그룹 수
        /// </summary>
        public int SelectedGroupCount => FilteredObjectGroups?.Count(g => g.IsSelected) ?? 0;

        /// <summary>
        /// Phase 12: 선택 상태 요약
        /// </summary>
        public string SelectionSummary
        {
            get
            {
                int selectedGroups = SelectedGroupCount;
                int totalGroups = FilteredGroupCount;
                return $"{selectedGroups:N0} / {totalGroups:N0} groups selected";
            }
        }

        #endregion

        #endregion

        #region Commands

        public ICommand SaveAsCsvCommand { get; }
        public ICommand SaveAsJsonCommand { get; }
        // CSV Export Commands (4종 버튼 체계)
        public ICommand ExportAllPropertiesCommand { get; }      // All × Properties
        public ICommand ExportSelectionPropertiesCommand { get; } // Selection × Properties
        public ICommand ExportAllHierarchyCommand { get; }        // All × Hierarchy
        public ICommand ExportSelectionHierarchyCommand { get; }  // Selection × Hierarchy
        // Geometry Export Commands (Phase 15)
        public ICommand ExportGeometryCommand { get; }             // All × Geometry (BoundingBox)
        public ICommand ExportSelectionGeometryCommand { get; }    // Selection × Geometry
        // Unified CSV Export Commands (Phase 16)
        public ICommand ExportUnifiedCsvCommand { get; }           // All × Unified (Hierarchy + Geometry)
        public ICommand ExportSelectionUnifiedCsvCommand { get; }  // Selection × Unified
        // Spatial Adjacency Export Commands (Phase 17)
        public ICommand ExportAdjacencyCommand { get; }            // All × Adjacency
        public ICommand ExportSelectionAdjacencyCommand { get; }   // Selection × Adjacency
        // Full Pipeline Export Command (Unified + Geometry + Spatial)
        public ICommand ExportFullPipelineCommand { get; }         // All-in-One Export
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

            // Grouped Properties 초기화 (Phase 11)
            GroupedProperties = new ObservableCollection<ObjectGroupViewModel>();

            // Phase 12: Grouped Data Structure 초기화
            AllObjectGroups = new ObservableCollection<ObjectGroupModel>();
            FilteredObjectGroups = new ObservableCollection<ObjectGroupModel>();
            LevelFilterOptions = new ObservableCollection<FilterOption>();
            CategoryFilterOptions = new ObservableCollection<FilterOption>();

            // Tree Expand Levels 초기화 (Phase 2)
            AvailableExpandLevels = new ObservableCollection<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            SelectedExpandLevel = 2; // 기본값: Level 2까지 확장

            // 3D Selection Service 초기화 (Phase 3)
            _selectionService = new NavisworksSelectionService();

            // Snapshot Service 초기화 (Phase 4)
            _snapshotService = new SnapshotService();

            // CSV Viewer 초기화 (v0.5.0)
            CsvViewer = new CsvViewerViewModel();

            // AWP 4D Automation 초기화 (Phase 8)
            AWP4D = new AWP4DViewModel();

            // Schedule Builder 초기화 (Phase 10)
            // Phase 12: 그룹 기반 선택 지원
            ScheduleBuilder = new ScheduleBuilderViewModel(
                () => GetSelectedHierarchicalRecords(),
                null);

            // Phase 14: Ontology ViewModel 초기화 (임시 비활성화 - 플러그인 로딩 테스트)
            // TODO: dotNetRdf/Neo4j 어셈블리 로딩 문제 해결 후 활성화
            OntologyVM = null;
            /*
            try
            {
                OntologyVM = new OntologyViewModel(() => GetAllHierarchicalRecords());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DXwindowViewModel] OntologyVM init failed: {ex.Message}");
                OntologyVM = null;
            }
            */

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

            // Geometry Export Commands (Phase 15)
            ExportGeometryCommand = new AsyncRelayCommand(
                execute: async _ => await ExportGeometryAsync());

            ExportSelectionGeometryCommand = new AsyncRelayCommand(
                execute: async _ => await ExportSelectionGeometryAsync(),
                canExecute: _ => Autodesk.Navisworks.Api.Application.ActiveDocument?.CurrentSelection?.SelectedItems?.Count > 0);

            // Unified CSV Export Commands (Phase 16)
            ExportUnifiedCsvCommand = new AsyncRelayCommand(
                execute: async _ => await ExportUnifiedCsvAsync());

            ExportSelectionUnifiedCsvCommand = new AsyncRelayCommand(
                execute: async _ => await ExportSelectionUnifiedCsvAsync(),
                canExecute: _ => Autodesk.Navisworks.Api.Application.ActiveDocument?.CurrentSelection?.SelectedItems?.Count > 0);

            // Spatial Adjacency Export Commands (Phase 17)
            ExportAdjacencyCommand = new AsyncRelayCommand(
                execute: async _ => await ExportAdjacencyAsync());

            ExportSelectionAdjacencyCommand = new AsyncRelayCommand(
                execute: async _ => await ExportSelectionAdjacencyAsync(),
                canExecute: _ => Autodesk.Navisworks.Api.Application.ActiveDocument?.CurrentSelection?.SelectedItems?.Count > 0);

            // Full Pipeline Export Command
            ExportFullPipelineCommand = new AsyncRelayCommand(
                execute: async _ => await ExportFullPipelineAsync());

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
                // v0.9.0: Select All 대량 업데이트 중에는 개별 이벤트 처리 건너뛰기 (성능 최적화)
                if (_isUpdatingSelectAll) return;

                OnPropertyChanged(nameof(SelectedPropertiesCount));
                OnPropertyChanged(nameof(SelectedPropertyInfo));

                // 체크박스 선택이 변경되었으니, CreateSearchSetCommand의 CanExecute를 다시 평가
                ((RelayCommand)CreateSearchSetCommand).RaiseCanExecuteChanged();

                // v0.6.1: Update IsAllSelected state based on current selections
                UpdateSelectAllState();
            }
        }

        /// <summary>
        /// 전체 선택/해제 (v0.6.1)
        /// </summary>
        private void SelectAllProperties(bool selectAll)
        {
            _isUpdatingSelectAll = true;
            try
            {
                foreach (var prop in FilteredHierarchicalProperties)
                {
                    prop.IsSelected = selectAll;
                }

                _isAllSelected = selectAll;
                OnPropertyChanged(nameof(IsAllSelected));
                OnPropertyChanged(nameof(SelectedPropertiesCount));
                OnPropertyChanged(nameof(SelectedPropertyInfo));

                // Update command states
                ((RelayCommand)CreateSearchSetCommand).RaiseCanExecuteChanged();
            }
            finally
            {
                _isUpdatingSelectAll = false;
            }
        }

        /// <summary>
        /// IsAllSelected 상태 업데이트 (개별 체크박스 변경 시)
        /// </summary>
        private void UpdateSelectAllState()
        {
            if (_isUpdatingSelectAll) return;

            _isUpdatingSelectAll = true;
            try
            {
                var totalCount = FilteredHierarchicalProperties.Count;
                var selectedCount = FilteredHierarchicalProperties.Count(p => p.IsSelected);

                // All selected → true, none selected → false, partial → false
                _isAllSelected = totalCount > 0 && selectedCount == totalCount;
                OnPropertyChanged(nameof(IsAllSelected));
            }
            finally
            {
                _isUpdatingSelectAll = false;
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
        /// Phase 12: 그룹화된 데이터 구조로 로드 (445K → ~5K 그룹)
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

                // TreeView 선택 이벤트 구독
                foreach (var node in allNodes)
                {
                    node.PropertyChanged += OnTreeNodeSelectionChanged;
                }

                // *** Phase 12: 그룹화된 데이터로 직접 로드 ***
                var extractor = new NavisworksDataExtractor();
                var allGroups = extractor.ExtractAllAsGroups();

                // ObjectGroups 초기화
                AllObjectGroups.Clear();
                foreach (var group in allGroups)
                {
                    // 그룹 선택 이벤트 구독
                    group.PropertyChanged += OnGroupPropertyChangedHandler;
                    AllObjectGroups.Add(group);
                }

                // Phase 12: 필터 옵션 초기화
                InitializeFilterOptions();

                // Phase 12: 필터링된 그룹 동기화
                SyncFilteredGroups();

                // *** 기존 호환성: HierarchicalPropertyRecord도 유지 (TimeLiner 등) ***
                AllHierarchicalProperties.Clear();
                foreach (var group in allGroups)
                {
                    foreach (var record in group.ToHierarchicalRecords())
                    {
                        AllHierarchicalProperties.Add(record);
                    }
                }
                SyncFilteredProperties();

                // 트리 명령 갱신 (Phase 2)
                RefreshTreeCommands();

                // 기본적으로 Level 2까지 확장
                ExpandTreeToLevel(SelectedExpandLevel);

                // Phase 12: 그룹 뷰를 기본으로 활성화
                _isGroupedViewEnabled = true;
                _isGroupedViewAvailable = true;
                OnPropertyChanged(nameof(IsGroupedViewEnabled));
                OnPropertyChanged(nameof(IsGroupedViewAvailable));

                int totalProps = allGroups.Sum(g => g.PropertyCount);
                ExportStatusMessage = $"Hierarchy loaded!";
                StatusMessage = $"Loaded: {allGroups.Count:N0} groups ({totalProps:N0} properties)";

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
        /// Phase 12: 그룹 PropertyChanged 이벤트 핸들러
        /// </summary>
        private void OnGroupPropertyChangedHandler(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ObjectGroupModel.IsSelected))
            {
                if (_isUpdatingGroupSelectAll) return;

                OnPropertyChanged(nameof(SelectedGroupCount));
                OnPropertyChanged(nameof(SelectionSummary));
                OnPropertyChanged(nameof(SelectedPropertiesCount));

                // 선택 상태에 따라 IsGroupSelectAll 업데이트
                UpdateGroupSelectAllState();

                // CreateSearchSetCommand 갱신
                ((RelayCommand)CreateSearchSetCommand).RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Phase 12: 그룹 전체 선택/해제
        /// </summary>
        private void SelectAllGroups(bool selectAll)
        {
            _isUpdatingGroupSelectAll = true;
            try
            {
                foreach (var group in FilteredObjectGroups)
                {
                    group.IsSelected = selectAll;
                }

                OnPropertyChanged(nameof(SelectedGroupCount));
                OnPropertyChanged(nameof(SelectionSummary));
                OnPropertyChanged(nameof(SelectedPropertiesCount));
                ((RelayCommand)CreateSearchSetCommand).RaiseCanExecuteChanged();
            }
            finally
            {
                _isUpdatingGroupSelectAll = false;
            }
        }

        /// <summary>
        /// Phase 12: IsGroupSelectAll 상태 업데이트
        /// </summary>
        private void UpdateGroupSelectAllState()
        {
            if (_isUpdatingGroupSelectAll) return;

            _isUpdatingGroupSelectAll = true;
            try
            {
                var totalCount = FilteredObjectGroups.Count;
                var selectedCount = FilteredObjectGroups.Count(g => g.IsSelected);
                _isGroupSelectAll = totalCount > 0 && selectedCount == totalCount;
                OnPropertyChanged(nameof(IsGroupSelectAll));
            }
            finally
            {
                _isUpdatingGroupSelectAll = false;
            }
        }

        /// <summary>
        /// Phase 12: 필터 옵션 초기화
        /// </summary>
        private void InitializeFilterOptions()
        {
            // Level 필터 옵션
            LevelFilterOptions.Clear();
            var levelCounts = AllObjectGroups
                .GroupBy(g => g.Level)
                .OrderBy(g => g.Key)
                .Select(g => new FilterOption($"L{g.Key}", g.Key, g.Count(), true));

            foreach (var opt in levelCounts)
            {
                opt.PropertyChanged += OnFilterOptionChanged;
                LevelFilterOptions.Add(opt);
            }

            // Category 필터 옵션
            CategoryFilterOptions.Clear();
            var categoryCounts = AllObjectGroups
                .SelectMany(g => g.Categories)
                .GroupBy(c => c)
                .OrderBy(g => g.Key)
                .Select(g => new FilterOption(g.Key, g.Count(), true));

            foreach (var opt in categoryCounts)
            {
                opt.PropertyChanged += OnFilterOptionChanged;
                CategoryFilterOptions.Add(opt);
            }
        }

        /// <summary>
        /// Phase 12: 필터 옵션 변경 이벤트 핸들러
        /// </summary>
        private void OnFilterOptionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FilterOption.IsChecked))
            {
                // 디바운스 적용
                TriggerFilterDebounce();
            }
        }

        /// <summary>
        /// Phase 14: 모든 HierarchicalPropertyRecord 반환 (Ontology 변환용)
        /// </summary>
        public IEnumerable<HierarchicalPropertyRecord> GetAllHierarchicalRecords()
        {
            // Phase 12: 모든 ObjectGroups에서 HierarchicalRecords 변환
            if (AllObjectGroups.Count > 0)
            {
                foreach (var group in AllObjectGroups)
                {
                    foreach (var record in group.ToHierarchicalRecords())
                    {
                        yield return record;
                    }
                }
            }
            else
            {
                // Fallback: 기존 AllHierarchicalProperties 사용
                foreach (var record in AllHierarchicalProperties)
                {
                    yield return record;
                }
            }
        }

        /// <summary>
        /// Phase 12: 선택된 그룹에서 HierarchicalPropertyRecord 반환 (TimeLiner/ScheduleBuilder 호환)
        /// </summary>
        public IEnumerable<HierarchicalPropertyRecord> GetSelectedHierarchicalRecords()
        {
            // Phase 12: 그룹 기반
            if (FilteredObjectGroups.Count > 0)
            {
                foreach (var group in FilteredObjectGroups.Where(g => g.IsSelected))
                {
                    foreach (var record in group.ToHierarchicalRecords())
                    {
                        yield return record;
                    }
                }
            }
            else
            {
                // Fallback: 기존 방식
                foreach (var record in FilteredHierarchicalProperties.Where(p => p.IsSelected))
                {
                    yield return record;
                }
            }
        }

        /// <summary>
        /// Phase 12: 선택된 ObjectId 목록 반환 (TimeLiner용)
        /// </summary>
        public IEnumerable<Guid> GetSelectedObjectIds()
        {
            return FilteredObjectGroups
                .Where(g => g.IsSelected)
                .Select(g => g.ObjectId);
        }

        /// <summary>
        /// Phase 12: 필터링된 그룹 동기화
        /// </summary>
        private void SyncFilteredGroups()
        {
            FilteredObjectGroups.Clear();

            // 선택된 레벨 집합
            var selectedLevels = new HashSet<int>(
                LevelFilterOptions
                    .Where(o => o.IsChecked)
                    .Select(o => (int)o.Value));

            // 선택된 카테고리 집합
            var selectedCategories = new HashSet<string>(
                CategoryFilterOptions
                    .Where(o => o.IsChecked)
                    .Select(o => o.Name));

            foreach (var group in AllObjectGroups)
            {
                // 레벨 필터 (전체 선택 또는 일치)
                if (selectedLevels.Count > 0 && !selectedLevels.Contains(group.Level))
                    continue;

                // 카테고리 필터: 그룹 내 속성 필터링
                if (selectedCategories.Count > 0)
                {
                    group.ApplyFilter(selectedCategories, PropertyNameFilter, PropertyValueFilter);
                    if (group.FilteredPropertyCount == 0)
                        continue;
                }
                else
                {
                    group.ApplyFilter(null, PropertyNameFilter, PropertyValueFilter);
                    if (group.FilteredPropertyCount == 0)
                        continue;
                }

                FilteredObjectGroups.Add(group);
            }

            OnPropertyChanged(nameof(FilteredGroupCount));
            OnPropertyChanged(nameof(FilteredPropertyCount));
            OnPropertyChanged(nameof(SelectedGroupCount));
            OnPropertyChanged(nameof(SelectionSummary));

            StatusMessage = $"Filtered: {FilteredGroupCount:N0} groups ({FilteredPropertyCount:N0} properties)";
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

            // Phase 14: OntologyVM 정리
            (_ontologyVM as IDisposable)?.Dispose();
        }

        #endregion
    }
}

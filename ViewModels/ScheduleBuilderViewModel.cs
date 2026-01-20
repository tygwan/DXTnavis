using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using DXTnavis.Helpers;
using DXTnavis.Models;
using DXTnavis.Services;
using Microsoft.Win32;

namespace DXTnavis.ViewModels
{
    /// <summary>
    /// Schedule Builder ViewModel
    /// Phase 10: Refined 데이터에서 Schedule CSV 자동 생성
    /// Phase 13: TaskType 한글화, DateMode, 직접 TimeLiner 실행
    /// </summary>
    public class ScheduleBuilderViewModel : INotifyPropertyChanged
    {
        #region TaskType Localization (Phase 13)

        /// <summary>
        /// TaskType 한글 → 영문 매핑
        /// </summary>
        public static readonly Dictionary<string, string> TaskTypeKorToEng =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "구성", "Construct" },
            { "철거", "Demolish" },
            { "임시", "Temporary" },
            // 역방향 호환성
            { "Construct", "Construct" },
            { "Demolish", "Demolish" },
            { "Temporary", "Temporary" }
        };

        /// <summary>
        /// TaskType 영문 → 한글 매핑
        /// </summary>
        public static readonly Dictionary<string, string> TaskTypeEngToKor =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Construct", "구성" },
            { "Demolish", "철거" },
            { "Temporary", "임시" }
        };

        /// <summary>
        /// 한글 TaskType을 영문으로 변환 (CSV/API용)
        /// </summary>
        public static string ToEnglishTaskType(string korean)
        {
            return TaskTypeKorToEng.TryGetValue(korean, out var eng) ? eng : korean;
        }

        /// <summary>
        /// 영문 TaskType을 한글로 변환 (UI 표시용)
        /// </summary>
        public static string ToKoreanTaskType(string english)
        {
            return TaskTypeEngToKor.TryGetValue(english, out var kor) ? kor : english;
        }

        #endregion

        #region Fields

        private readonly Func<IEnumerable<HierarchicalPropertyRecord>> _getSelectedItems;
        private readonly AWP4DAutomationService _automationService;
        private readonly ObjectMatcher _objectMatcher;
        private readonly SelectionSetService _selectionSetService;
        private readonly TimeLinerService _timeLinerService;

        private string _taskNamePrefix = "Task";
        private string _selectedTaskType = "구성";  // Phase 13: 기본값 한글
        private int _durationPerTask = 1;
        private DateTime _startDate = DateTime.Today.AddDays(1);
        private string _parentSetStrategy = "ByLevel";
        private int _parentSetLevel = 2;
        private string _customParentSet = "";
        private DateMode _selectedDateMode = DateMode.ActualFromPlanned;  // Phase 13: 기본값 권장

        private ObservableCollection<SchedulePreviewItem> _previewItems;
        private int _selectedCount;
        private int _totalDuration;
        private string _statusMessage;

        // Phase 13: Direct Execution 관련 필드
        private bool _isDryRunMode = false;
        private bool _isExecuting = false;
        private int _executionProgress = 0;

        #endregion

        #region Constructor

        public ScheduleBuilderViewModel(
            Func<IEnumerable<HierarchicalPropertyRecord>> getSelectedItems,
            AWP4DAutomationService automationService = null)
        {
            _getSelectedItems = getSelectedItems;
            _automationService = automationService;
            _previewItems = new ObservableCollection<SchedulePreviewItem>();

            // Phase 13: 직접 실행 서비스 초기화
            _objectMatcher = new ObjectMatcher();
            _selectionSetService = new SelectionSetService();
            _timeLinerService = new TimeLinerService(_selectionSetService);

            // Commands
            RefreshPreviewCommand = new RelayCommand(RefreshPreview);
            GenerateCsvCommand = new RelayCommand(GenerateCsv, () => SelectedCount > 0);
            ExecuteToTimeLinerCommand = new RelayCommand(ExecuteToTimeLiner, () => SelectedCount > 0 && _automationService != null);
            DirectExecuteCommand = new RelayCommand(ExecuteDirectToTimeLiner, () => CanExecute);

            // Task Types (Phase 13: 한글화)
            TaskTypes = new List<string> { "구성", "철거", "임시" };

            // ParentSet Strategies (Phase 13: 확장)
            ParentSetStrategies = new List<string>
            {
                "ByLevel",        // 트리 레벨 (depth)
                "ByFloorLevel",   // 건축 층 (Element.Level)
                "ByCategory",     // Element 카테고리
                "ByArea",         // 구역 (Element.Area/Zone)
                "Composite",      // Level + Category 조합
                "ByProperty",     // SysPath 기반
                "Custom"          // 사용자 입력
            };
        }

        #endregion

        #region Properties

        /// <summary>
        /// Task 이름 접두사
        /// </summary>
        public string TaskNamePrefix
        {
            get => _taskNamePrefix;
            set
            {
                _taskNamePrefix = value;
                OnPropertyChanged(nameof(TaskNamePrefix));
                RefreshPreview();
            }
        }

        /// <summary>
        /// Task 유형 목록
        /// </summary>
        public List<string> TaskTypes { get; }

        /// <summary>
        /// 선택된 Task 유형
        /// </summary>
        public string SelectedTaskType
        {
            get => _selectedTaskType;
            set
            {
                _selectedTaskType = value;
                OnPropertyChanged(nameof(SelectedTaskType));
                RefreshPreview();
            }
        }

        /// <summary>
        /// Task당 일수
        /// </summary>
        public int DurationPerTask
        {
            get => _durationPerTask;
            set
            {
                _durationPerTask = Math.Max(1, value);
                OnPropertyChanged(nameof(DurationPerTask));
                RefreshPreview();
            }
        }

        /// <summary>
        /// 시작일
        /// </summary>
        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                _startDate = value;
                OnPropertyChanged(nameof(StartDate));
                RefreshPreview();
            }
        }

        /// <summary>
        /// ParentSet 전략 목록
        /// </summary>
        public List<string> ParentSetStrategies { get; }

        /// <summary>
        /// 선택된 ParentSet 전략
        /// </summary>
        public string ParentSetStrategy
        {
            get => _parentSetStrategy;
            set
            {
                _parentSetStrategy = value;
                OnPropertyChanged(nameof(ParentSetStrategy));
                OnPropertyChanged(nameof(IsLevelStrategySelected));
                OnPropertyChanged(nameof(IsCustomStrategySelected));
                RefreshPreview();
            }
        }

        /// <summary>
        /// ParentSet Level (ByLevel 전략 시)
        /// </summary>
        public int ParentSetLevel
        {
            get => _parentSetLevel;
            set
            {
                _parentSetLevel = Math.Max(0, value);
                OnPropertyChanged(nameof(ParentSetLevel));
                RefreshPreview();
            }
        }

        /// <summary>
        /// Custom ParentSet 경로
        /// </summary>
        public string CustomParentSet
        {
            get => _customParentSet;
            set
            {
                _customParentSet = value;
                OnPropertyChanged(nameof(CustomParentSet));
                RefreshPreview();
            }
        }

        /// <summary>
        /// Level 전략 선택 여부
        /// </summary>
        public bool IsLevelStrategySelected => ParentSetStrategy == "ByLevel";

        /// <summary>
        /// Custom 전략 선택 여부
        /// </summary>
        public bool IsCustomStrategySelected => ParentSetStrategy == "Custom";

        #region DateMode Properties (Phase 13)

        /// <summary>
        /// DateMode 옵션 목록
        /// </summary>
        public List<DateModeOption> DateModeOptions { get; } = DateModeExtensions.GetAllOptions();

        /// <summary>
        /// 선택된 DateMode
        /// </summary>
        public DateMode SelectedDateMode
        {
            get => _selectedDateMode;
            set
            {
                _selectedDateMode = value;
                OnPropertyChanged(nameof(SelectedDateMode));
                RefreshPreview();
            }
        }

        /// <summary>
        /// 현재 DateMode 설명
        /// </summary>
        public string DateModeDescription => _selectedDateMode.GetDescription();

        #endregion

        /// <summary>
        /// 미리보기 아이템 목록
        /// </summary>
        public ObservableCollection<SchedulePreviewItem> PreviewItems
        {
            get => _previewItems;
            set
            {
                _previewItems = value;
                OnPropertyChanged(nameof(PreviewItems));
            }
        }

        /// <summary>
        /// 선택된 아이템 개수
        /// </summary>
        public int SelectedCount
        {
            get => _selectedCount;
            set
            {
                _selectedCount = value;
                OnPropertyChanged(nameof(SelectedCount));
                ((RelayCommand)GenerateCsvCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ExecuteToTimeLinerCommand).RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 총 기간 (일)
        /// </summary>
        public int TotalDuration
        {
            get => _totalDuration;
            set
            {
                _totalDuration = value;
                OnPropertyChanged(nameof(TotalDuration));
            }
        }

        /// <summary>
        /// 상태 메시지
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

        #region Direct Execution Properties (Phase 13)

        /// <summary>
        /// DryRun 모드 (실제 실행 없이 미리보기만)
        /// </summary>
        public bool IsDryRunMode
        {
            get => _isDryRunMode;
            set
            {
                _isDryRunMode = value;
                OnPropertyChanged(nameof(IsDryRunMode));
            }
        }

        /// <summary>
        /// 실행 중 여부
        /// </summary>
        public bool IsExecuting
        {
            get => _isExecuting;
            set
            {
                _isExecuting = value;
                OnPropertyChanged(nameof(IsExecuting));
                OnPropertyChanged(nameof(CanExecute));
                ((RelayCommand)DirectExecuteCommand)?.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// 실행 진행률 (0-100)
        /// </summary>
        public int ExecutionProgress
        {
            get => _executionProgress;
            set
            {
                _executionProgress = value;
                OnPropertyChanged(nameof(ExecutionProgress));
            }
        }

        /// <summary>
        /// 직접 실행 가능 여부
        /// </summary>
        public bool CanExecute => SelectedCount > 0 && !IsExecuting;

        #endregion

        #endregion

        #region Commands

        public ICommand RefreshPreviewCommand { get; }
        public ICommand GenerateCsvCommand { get; }
        public ICommand ExecuteToTimeLinerCommand { get; }
        public ICommand DirectExecuteCommand { get; }  // Phase 13: 직접 실행

        #endregion

        #region Methods

        /// <summary>
        /// 미리보기 새로고침
        /// </summary>
        public void RefreshPreview()
        {
            try
            {
                var selectedItems = _getSelectedItems?.Invoke()?.ToList();
                if (selectedItems == null || !selectedItems.Any())
                {
                    PreviewItems.Clear();
                    SelectedCount = 0;
                    TotalDuration = 0;
                    StatusMessage = "선택된 항목이 없습니다. 중앙 패널에서 항목을 선택하세요.";
                    return;
                }

                // 고유 ObjectId 기준으로 그룹화 (동일 객체의 여러 속성 제거)
                var uniqueObjects = selectedItems
                    .GroupBy(x => x.ObjectId)
                    .Select(g => g.First())
                    .ToList();

                PreviewItems.Clear();
                var currentDate = StartDate;
                int taskNumber = 1;

                foreach (var item in uniqueObjects)
                {
                    var endDate = currentDate.AddDays(DurationPerTask);
                    var parentSet = DetermineParentSet(item);

                    // Phase 13: DateMode에 따라 Actual 날짜 설정
                    DateTime? actualStart = null;
                    DateTime? actualEnd = null;

                    switch (SelectedDateMode)
                    {
                        case DateMode.ActualFromPlanned:
                            // 권장: Planned를 Actual에도 복사
                            actualStart = currentDate;
                            actualEnd = endDate;
                            break;
                        case DateMode.PlannedOnly:
                            // Actual 없음
                            break;
                        case DateMode.BothSeparate:
                            // 사용자가 별도 입력 (현재는 null)
                            break;
                    }

                    PreviewItems.Add(new SchedulePreviewItem
                    {
                        SyncID = item.ObjectId.ToString(),
                        TaskName = $"{TaskNamePrefix} {taskNumber}",
                        PlannedStart = currentDate,
                        PlannedEnd = endDate,
                        ActualStart = actualStart,
                        ActualEnd = actualEnd,
                        TaskType = SelectedTaskType,
                        ParentSet = parentSet,
                        OriginalDisplayName = item.DisplayName
                    });

                    currentDate = endDate;
                    taskNumber++;
                }

                SelectedCount = uniqueObjects.Count;
                TotalDuration = SelectedCount * DurationPerTask;
                StatusMessage = $"{SelectedCount}개 객체 → {TotalDuration}일 일정 생성 준비됨";
            }
            catch (Exception ex)
            {
                StatusMessage = $"미리보기 오류: {ex.Message}";
            }
        }

        /// <summary>
        /// ParentSet 결정
        /// Phase 13: 확장된 전략 지원
        /// </summary>
        private string DetermineParentSet(HierarchicalPropertyRecord item)
        {
            switch (ParentSetStrategy)
            {
                case "ByLevel":
                    // 트리 레벨 (depth) 기반: "Level-{n}" 형식
                    return $"Level-{Math.Min(item.Level, ParentSetLevel)}";

                case "ByFloorLevel":
                    // 건축 층 (Element.Level 속성에서 추출)
                    var floorLevel = FindPropertyValue(item, "Element", "Level")
                                  ?? FindPropertyValue(item, "Item", "Level")
                                  ?? FindPropertyValue(item, "Element", "층");
                    return !string.IsNullOrEmpty(floorLevel) ? $"Floor-{floorLevel}" : $"Level-{item.Level}";

                case "ByCategory":
                    // Element 카테고리
                    var category = FindPropertyValue(item, "Element", "Category")
                                ?? FindPropertyValue(item, "Item", "Type")
                                ?? ExtractCategoryFromDisplayName(item.DisplayName);
                    return !string.IsNullOrEmpty(category) ? category : "Unknown";

                case "ByArea":
                    // 구역 (Element.Area 또는 Zone 속성)
                    var area = FindPropertyValue(item, "Element", "Area")
                            ?? FindPropertyValue(item, "Element", "Zone")
                            ?? FindPropertyValue(item, "Element", "구역")
                            ?? FindPropertyValue(item, "Element", "공구");
                    return !string.IsNullOrEmpty(area) ? $"Area-{area}" : "Default-Area";

                case "Composite":
                    // Level + Category 조합
                    var level = FindPropertyValue(item, "Element", "Level")
                             ?? FindPropertyValue(item, "Item", "Level")
                             ?? $"L{item.Level}";
                    var cat = FindPropertyValue(item, "Element", "Category")
                           ?? ExtractCategoryFromDisplayName(item.DisplayName)
                           ?? "Unknown";
                    return $"{level}/{cat}";

                case "ByProperty":
                    // SysPath에서 추출 (기존 로직)
                    if (!string.IsNullOrEmpty(item.SysPath))
                    {
                        var parts = item.SysPath.Split(new[] { " > ", "/", "\\" }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1)
                            return parts[Math.Min(1, parts.Length - 1)];
                    }
                    return "Default";

                case "Custom":
                    return string.IsNullOrEmpty(CustomParentSet) ? "Custom" : CustomParentSet;

                default:
                    return "Default";
            }
        }

        /// <summary>
        /// 객체의 속성에서 특정 카테고리/이름의 값 찾기
        /// Phase 13: 확장 ParentSet 전략 지원
        /// </summary>
        private string FindPropertyValue(HierarchicalPropertyRecord item, string category, string propertyName)
        {
            // HierarchicalPropertyRecord 자체가 단일 속성이므로
            // 현재 아이템의 Category/PropertyName 일치 여부 확인
            if (item.Category?.Equals(category, StringComparison.OrdinalIgnoreCase) == true &&
                item.PropertyName?.Equals(propertyName, StringComparison.OrdinalIgnoreCase) == true)
            {
                return item.PropertyValue;
            }

            // 속성을 찾지 못함 - null 반환
            return null;
        }

        /// <summary>
        /// DisplayName에서 카테고리 추출 (첫 단어 또는 타입 부분)
        /// 예: "Footing-001" → "Footing", "Beam 1A" → "Beam"
        /// </summary>
        private string ExtractCategoryFromDisplayName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
                return null;

            // 숫자/하이픈/공백 앞부분 추출
            var match = System.Text.RegularExpressions.Regex.Match(displayName, @"^([A-Za-z가-힣]+)");
            if (match.Success)
                return match.Groups[1].Value;

            // 공백으로 분리하여 첫 단어 반환
            var parts = displayName.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 0 ? parts[0] : displayName;
        }

        /// <summary>
        /// Schedule CSV 생성
        /// </summary>
        private void GenerateCsv()
        {
            if (PreviewItems.Count == 0)
            {
                RefreshPreview();
                if (PreviewItems.Count == 0)
                {
                    MessageBox.Show("선택된 항목이 없습니다.", "Schedule Builder", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                DefaultExt = "csv",
                FileName = $"schedule_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                Title = "Schedule CSV 저장"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var sb = new StringBuilder();

                    // Header (Phase 13: DateMode에 따라 Actual 컬럼 포함)
                    if (SelectedDateMode == DateMode.PlannedOnly)
                    {
                        sb.AppendLine("SyncID,TaskName,PlannedStart,PlannedEnd,TaskType,ParentSet,Progress");
                    }
                    else
                    {
                        sb.AppendLine("SyncID,TaskName,PlannedStart,PlannedEnd,ActualStart,ActualEnd,TaskType,ParentSet,Progress");
                    }

                    // Data (Phase 13: TaskType 영문 변환, Actual 날짜 포함)
                    foreach (var item in PreviewItems)
                    {
                        var engTaskType = ToEnglishTaskType(item.TaskType);  // 한글 → 영문

                        if (SelectedDateMode == DateMode.PlannedOnly)
                        {
                            sb.AppendLine($"{item.SyncID},{item.TaskName},{item.PlannedStart:yyyy-MM-dd},{item.PlannedEnd:yyyy-MM-dd},{engTaskType},{item.ParentSet},0");
                        }
                        else
                        {
                            var actualStart = item.ActualStart?.ToString("yyyy-MM-dd") ?? "";
                            var actualEnd = item.ActualEnd?.ToString("yyyy-MM-dd") ?? "";
                            sb.AppendLine($"{item.SyncID},{item.TaskName},{item.PlannedStart:yyyy-MM-dd},{item.PlannedEnd:yyyy-MM-dd},{actualStart},{actualEnd},{engTaskType},{item.ParentSet},0");
                        }
                    }

                    File.WriteAllText(saveDialog.FileName, sb.ToString(), Encoding.UTF8);
                    StatusMessage = $"CSV 저장 완료: {saveDialog.FileName}";
                    MessageBox.Show($"Schedule CSV가 저장되었습니다.\n\n{saveDialog.FileName}\n\nAWP 4D 탭에서 이 파일을 로드하여 TimeLiner에 적용할 수 있습니다.",
                        "Schedule Builder", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"CSV 저장 오류: {ex.Message}";
                    MessageBox.Show($"CSV 저장 중 오류가 발생했습니다.\n\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// TimeLiner로 직접 실행 (AWP 4D 파이프라인 활용)
        /// </summary>
        private void ExecuteToTimeLiner()
        {
            if (_automationService == null)
            {
                MessageBox.Show("AWP 4D Automation Service가 초기화되지 않았습니다.\n\nAWP 4D 탭에서 직접 CSV를 로드하여 실행하세요.",
                    "Schedule Builder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // CSV를 임시 파일로 저장 후 AWP 4D 파이프라인 실행
            var tempPath = Path.Combine(Path.GetTempPath(), $"schedule_temp_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

            try
            {
                var sb = new StringBuilder();

                // Phase 13: DateMode에 따라 헤더 결정
                if (SelectedDateMode == DateMode.PlannedOnly)
                {
                    sb.AppendLine("SyncID,TaskName,PlannedStart,PlannedEnd,TaskType,ParentSet,Progress");
                }
                else
                {
                    sb.AppendLine("SyncID,TaskName,PlannedStart,PlannedEnd,ActualStart,ActualEnd,TaskType,ParentSet,Progress");
                }

                // Phase 13: TaskType 영문 변환, Actual 날짜 포함
                foreach (var item in PreviewItems)
                {
                    var engTaskType = ToEnglishTaskType(item.TaskType);

                    if (SelectedDateMode == DateMode.PlannedOnly)
                    {
                        sb.AppendLine($"{item.SyncID},{item.TaskName},{item.PlannedStart:yyyy-MM-dd},{item.PlannedEnd:yyyy-MM-dd},{engTaskType},{item.ParentSet},0");
                    }
                    else
                    {
                        var actualStart = item.ActualStart?.ToString("yyyy-MM-dd") ?? "";
                        var actualEnd = item.ActualEnd?.ToString("yyyy-MM-dd") ?? "";
                        sb.AppendLine($"{item.SyncID},{item.TaskName},{item.PlannedStart:yyyy-MM-dd},{item.PlannedEnd:yyyy-MM-dd},{actualStart},{actualEnd},{engTaskType},{item.ParentSet},0");
                    }
                }

                File.WriteAllText(tempPath, sb.ToString(), Encoding.UTF8);
                StatusMessage = $"임시 CSV 생성: {tempPath}";

                MessageBox.Show($"Schedule CSV가 생성되었습니다.\n\nAWP 4D 탭으로 이동하여 다음 파일을 로드하세요:\n{tempPath}",
                    "Schedule Builder", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"실행 오류: {ex.Message}";
                MessageBox.Show($"실행 중 오류가 발생했습니다.\n\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Phase 13: Direct TimeLiner Execution

        /// <summary>
        /// Preview Items를 ScheduleData 리스트로 변환
        /// CSV 중간 단계 없이 직접 TimeLiner API 호출용 데이터 준비
        /// </summary>
        private List<ScheduleData> ConvertPreviewToScheduleData()
        {
            var scheduleDataList = new List<ScheduleData>();

            foreach (var item in PreviewItems)
            {
                var scheduleData = new ScheduleData
                {
                    SyncID = item.SyncID,
                    TaskName = item.TaskName,
                    PlannedStartDate = item.PlannedStart,
                    PlannedEndDate = item.PlannedEnd,
                    ActualStartDate = item.ActualStart,
                    ActualEndDate = item.ActualEnd,
                    TaskType = ToEnglishTaskType(item.TaskType),  // 한글 → 영문 변환
                    ParentSet = item.ParentSet,
                    Progress = 0
                };

                // SyncID로 MatchedObjectId 설정 (GUID 형식인 경우)
                if (Guid.TryParse(item.SyncID, out Guid objectId))
                {
                    scheduleData.MatchedObjectId = objectId;
                }

                scheduleDataList.Add(scheduleData);
            }

            return scheduleDataList;
        }

        /// <summary>
        /// Phase 13: 직접 TimeLiner 실행
        /// CSV 중간 단계 없이 직접 TimeLiner API 호출
        /// </summary>
        private void ExecuteDirectToTimeLiner()
        {
            if (PreviewItems.Count == 0)
            {
                RefreshPreview();
                if (PreviewItems.Count == 0)
                {
                    MessageBox.Show("선택된 항목이 없습니다.", "Schedule Builder", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // DryRun 모드: 미리보기만 표시
            if (IsDryRunMode)
            {
                var scheduleData = ConvertPreviewToScheduleData();
                var dryRunReport = new StringBuilder();
                dryRunReport.AppendLine("=== DryRun 결과 (실제 실행 없음) ===\n");
                dryRunReport.AppendLine($"총 Task 수: {scheduleData.Count}개\n");
                dryRunReport.AppendLine("--- ParentSet 별 Task 수 ---");

                var groups = scheduleData.GroupBy(s => s.ParentSet);
                foreach (var group in groups)
                {
                    dryRunReport.AppendLine($"  {group.Key}: {group.Count()}개");
                }

                dryRunReport.AppendLine("\n--- 상위 5개 Task 미리보기 ---");
                foreach (var task in scheduleData.Take(5))
                {
                    dryRunReport.AppendLine($"  [{task.SyncID}] {task.TaskName}");
                    dryRunReport.AppendLine($"    기간: {task.PlannedStartDate:yyyy-MM-dd} ~ {task.PlannedEndDate:yyyy-MM-dd}");
                    dryRunReport.AppendLine($"    유형: {task.TaskType}, 상위Set: {task.ParentSet}");
                }

                if (scheduleData.Count > 5)
                {
                    dryRunReport.AppendLine($"\n  ... 외 {scheduleData.Count - 5}개");
                }

                StatusMessage = $"DryRun 완료: {scheduleData.Count}개 Task 미리보기";
                MessageBox.Show(dryRunReport.ToString(), "DryRun 결과", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 실제 실행
            IsExecuting = true;
            ExecutionProgress = 0;

            try
            {
                // Step 1: Preview → ScheduleData 변환
                StatusMessage = "1/4: Schedule 데이터 변환 중...";
                ExecutionProgress = 10;
                var scheduleDataList = ConvertPreviewToScheduleData();

                // AWP4DOptions 설정 (매칭 전에 필요)
                var options = new AWP4DOptions
                {
                    SelectionSetRootFolder = "Schedule Builder Sets",
                    TimeLinerRootFolder = "Schedule Builder Tasks",
                    CreateHierarchicalTasks = true,
                    GroupingStrategy = GroupingStrategy.ByParentSet,
                    TaskSelectionMode = TaskSelectionMode.Explicit,
                    EnablePropertyWrite = false,
                    VerboseLogging = true
                };

                // Step 2: Object 매칭 (SyncID → ModelItem)
                StatusMessage = $"2/4: 객체 매칭 중... (0/{scheduleDataList.Count})";
                ExecutionProgress = 20;

                int matchedCount = 0;
                int failedCount = 0;

                foreach (var scheduleData in scheduleDataList)
                {
                    try
                    {
                        // ObjectMatcher로 ModelItem 찾기 (options 전달)
                        var modelItem = _objectMatcher.FindBySyncId(scheduleData.SyncID, options);
                        if (modelItem != null)
                        {
                            scheduleData.MatchedObjectId = modelItem.InstanceGuid;
                            scheduleData.MatchStatus = MatchStatus.Matched;
                            matchedCount++;
                        }
                        else
                        {
                            scheduleData.MatchStatus = MatchStatus.NotFound;
                            scheduleData.MatchError = "ModelItem not found";
                            failedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        scheduleData.MatchStatus = MatchStatus.Error;
                        scheduleData.MatchError = ex.Message;
                        failedCount++;
                    }

                    // 진행률 업데이트
                    var progress = (int)(20 + (30.0 * (matchedCount + failedCount) / scheduleDataList.Count));
                    ExecutionProgress = progress;
                    StatusMessage = $"2/4: 객체 매칭 중... ({matchedCount + failedCount}/{scheduleDataList.Count})";
                }

                // 매칭 결과 확인
                if (matchedCount == 0)
                {
                    throw new InvalidOperationException($"매칭된 객체가 없습니다. (전체: {scheduleDataList.Count}, 실패: {failedCount})");
                }

                // Step 3: Selection Set 생성
                StatusMessage = $"3/4: Selection Set 생성 중...";
                ExecutionProgress = 50;

                var matchedData = scheduleDataList.Where(s => s.MatchStatus == MatchStatus.Matched).ToList();
                var setResults = _selectionSetService.CreateHierarchicalSets(matchedData, options);

                ExecutionProgress = 70;

                // SyncID → SetName 매핑 생성
                var syncIdToSetName = new Dictionary<string, string>();
                foreach (var schedule in matchedData)
                {
                    // ParentSet의 마지막 부분을 SetName으로 사용
                    if (!string.IsNullOrEmpty(schedule.ParentSet))
                    {
                        var parts = schedule.ParentSet.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                        var setName = parts.Length > 0 ? parts[parts.Length - 1] : schedule.ParentSet;
                        syncIdToSetName[schedule.SyncID] = setName;
                    }
                }

                // Step 4: TimeLiner Task 생성
                StatusMessage = $"4/4: TimeLiner Task 생성 중...";
                ExecutionProgress = 80;

                var taskResults = _timeLinerService.CreateTasks(matchedData, syncIdToSetName, options);

                ExecutionProgress = 100;

                // 결과 보고
                var resultMessage = new StringBuilder();
                resultMessage.AppendLine("=== TimeLiner 직접 실행 완료 ===\n");
                resultMessage.AppendLine($"총 처리: {scheduleDataList.Count}개");
                resultMessage.AppendLine($"객체 매칭: {matchedCount}개 성공, {failedCount}개 실패");
                resultMessage.AppendLine($"Selection Set: {setResults?.SetCount ?? 0}개 생성");
                resultMessage.AppendLine($"TimeLiner Task: {taskResults?.TaskCount ?? 0}개 생성");
                resultMessage.AppendLine($"  - 연결됨: {taskResults?.LinkedCount ?? 0}개");
                resultMessage.AppendLine($"  - 미연결: {taskResults?.UnlinkedCount ?? 0}개");

                if (failedCount > 0)
                {
                    resultMessage.AppendLine("\n--- 매칭 실패 목록 (최대 5개) ---");
                    foreach (var failed in scheduleDataList.Where(s => s.MatchStatus != MatchStatus.Matched).Take(5))
                    {
                        resultMessage.AppendLine($"  [{failed.SyncID}] {failed.MatchError}");
                    }
                }

                StatusMessage = $"완료: {taskResults?.TaskCount ?? 0}개 Task 생성됨";
                MessageBox.Show(resultMessage.ToString(), "직접 실행 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"실행 오류: {ex.Message}";
                ExecutionProgress = 0;
                MessageBox.Show($"TimeLiner 직접 실행 중 오류가 발생했습니다.\n\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsExecuting = false;
            }
        }

        #endregion

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// Schedule 미리보기 아이템
    /// Phase 13: ActualStart/End 추가
    /// </summary>
    public class SchedulePreviewItem
    {
        public string SyncID { get; set; }
        public string TaskName { get; set; }
        public DateTime PlannedStart { get; set; }
        public DateTime PlannedEnd { get; set; }
        public DateTime? ActualStart { get; set; }  // Phase 13
        public DateTime? ActualEnd { get; set; }    // Phase 13
        public string TaskType { get; set; }
        public string ParentSet { get; set; }
        public string OriginalDisplayName { get; set; }

        /// <summary>
        /// Actual 날짜 표시 (UI용)
        /// </summary>
        public string ActualDateDisplay =>
            ActualStart.HasValue && ActualEnd.HasValue
                ? $"{ActualStart:yyyy-MM-dd} ~ {ActualEnd:yyyy-MM-dd}"
                : "-";
    }
}

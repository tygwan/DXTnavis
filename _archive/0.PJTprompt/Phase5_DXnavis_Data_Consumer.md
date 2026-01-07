# Phase 5: DXnavis 데이터 소비자 개발

## 문서 목적
Navisworks 애드인인 DXnavis의 구현 가이드입니다. FastAPI 서버에서 데이터를 조회하고 TimeLiner 자동화를 수행하는 모든 기능을 다룹니다.

---

## 1. 개요

### 1.1. DXnavis의 역할
**핵심 책임**: FastAPI → Navisworks (Read Only)

**주요 기능**:
1. API 서버에서 버전 데이터 조회
2. TimeLiner 작업 자동 생성
3. Search Set 자동 생성 및 연결
4. 버전 비교 및 3D 시각화
5. 설정 UI 제공

### 1.2. 기술 스펙
- **프레임워크**: .NET Framework 4.8
- **Navisworks API**: 2024/2025
- **UI**: WPF (Windows Presentation Foundation)
- **의존성**: DXBase, NavisworksAPI.dll

**중요**: Navisworks API와 Revit API는 네임스페이스, 클래스명, 메서드 시그니처가 다릅니다. 혼동하지 마세요!

---

## 2. 프로젝트 구조

### 2.1. 폴더 구조
```
DXnavis/
├── DXnavis.csproj
├── DXnavis.addin                    # Navisworks 매니페스트 파일
├── Commands/
│   ├── TimeLinerAutoCommand.cs      # 4D 자동화 커맨드
│   ├── VersionCompareCommand.cs     # 버전 비교 커맨드
│   └── SettingsCommand.cs           # 설정 창 열기 커맨드
├── Services/
│   ├── ApiDataReader.cs             # API 클라이언트 서비스
│   ├── TimeLinerConnector.cs        # TimeLiner 자동화 서비스
│   ├── SearchSetService.cs          # Search Set 생성 서비스
│   └── VisualizationService.cs      # 3D 시각화 서비스
├── ViewModels/
│   ├── TimeLinerViewModel.cs        # 4D 자동화 UI 로직
│   ├── VersionCompareViewModel.cs   # 버전 비교 UI 로직
│   └── SettingsViewModel.cs         # 설정 UI 로직
├── Views/
│   ├── TimeLinerView.xaml           # 4D 자동화 창
│   ├── TimeLinerView.xaml.cs
│   ├── VersionCompareView.xaml      # 버전 비교 창
│   ├── VersionCompareView.xaml.cs
│   ├── SettingsView.xaml            # 설정 창
│   └── SettingsView.xaml.cs
└── Utils/
    ├── NavisSearchHelper.cs         # Navisworks Search 헬퍼
    └── ColorHelper.cs               # 색상 관리 헬퍼
```

---

## 3. 핵심 기능 구현

### 3.1. 매니페스트 파일 (DXnavis.addin)

**구현해야 할 것**:
```xml
<?xml version="1.0" encoding="utf-8"?>
<ApplicationPlugins>
  <ApplicationPlugin>
    <PluginId>12345678-ABCD-1234-ABCD-123456789DEF</PluginId>
    <RequiredProduct>Navisworks</RequiredProduct>
    <RequiredVersion>2024.0</RequiredVersion>
    <Name>DXnavis</Name>
    <Description>DX Platform - Navisworks 4D Automation</Description>
    <Assembly>DXnavis.dll</Assembly>
    <Developer>DX Platform</Developer>
  </ApplicationPlugin>
</ApplicationPlugins>
```

**배치 위치**:
- Windows: `C:\ProgramData\Autodesk\ApplicationPlugins\DXnavis.bundle\Contents\DXnavis.addin`

### 3.2. Application.cs (애드인 진입점)

**구현해야 할 것**:
```csharp
using Autodesk.Navisworks.Api.Plugins;
using Autodesk.Navisworks.Api;
using System;
using DXBase.Services;

namespace DXnavis
{
    /// <summary>
    /// Navisworks 애드인 진입점
    /// </summary>
    [Plugin("DXnavis", "DX", DisplayName = "DX Platform")]
    [AddInPlugin(AddInLocation.AddIn)]
    public class Application : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            try
            {
                // 설정 및 로깅 초기화
                var settings = ConfigurationService.LoadSettings();
                LoggingService.Initialize(settings.LogFilePath);
                LoggingService.LogInfo("DXnavis 애드인 시작", "DXnavis");

                // 리본 탭 및 버튼 추가는 별도 클래스에서 처리
                // Navisworks는 Revit과 달리 리본 API가 제한적입니다

                return 0; // 성공
            }
            catch (Exception ex)
            {
                LoggingService.LogError("DXnavis 초기화 실패", "DXnavis", ex);
                return 1; // 실패
            }
        }
    }
}
```

**주의사항**:
- ⚠️ Navisworks API는 Revit API와 다른 플러그인 시스템 사용
- ⚠️ 리본 UI 커스터마이징이 Revit보다 제한적
- ✅ Plugin 및 AddInPlugin 어트리뷰트 필수

### 3.3. TimeLinerAutoCommand.cs (4D 자동화 커맨드)

**구현해야 할 것**:
```csharp
using Autodesk.Navisworks.Api.Plugins;
using Autodesk.Navisworks.Api;
using DXnavis.ViewModels;
using DXnavis.Views;
using System;
using System.Windows;

namespace DXnavis.Commands
{
    [Plugin("DXnavis.TimeLinerAuto", "DX")]
    [AddInPlugin(AddInLocation.AddIn, DisplayName = "TimeLiner 자동 구성")]
    public class TimeLinerAutoCommand : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            try
            {
                // 현재 문서 가져오기
                Document doc = Autodesk.Navisworks.Api.Application.ActiveDocument;

                if (doc == null || doc.FileName == null)
                {
                    MessageBox.Show("Navisworks 문서를 먼저 열어주세요.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    return 1;
                }

                // TimeLiner UI 표시
                var viewModel = new TimeLinerViewModel(doc);
                var view = new TimeLinerView { DataContext = viewModel };

                bool? dialogResult = view.ShowDialog();

                return dialogResult == true ? 0 : 1;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("TimeLiner 자동화 실패", "DXnavis", ex);
                MessageBox.Show($"오류가 발생했습니다:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }
        }
    }
}
```

### 3.4. ApiDataReader.cs (API 클라이언트)

**목적**: FastAPI 서버에서 데이터 조회

**구현해야 할 것**:
```csharp
using DXBase.Models;
using DXBase.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DXnavis.Services
{
    /// <summary>
    /// API 서버에서 데이터 조회 서비스
    /// </summary>
    public class ApiDataReader
    {
        private readonly HttpClientService _httpClient;

        public ApiDataReader()
        {
            var settings = ConfigurationService.LoadSettings();
            _httpClient = new HttpClientService(settings.ApiServerUrl, settings.TimeoutSeconds);
        }

        /// <summary>
        /// 모든 버전 목록 조회
        /// </summary>
        public async Task<List<string>> GetVersionsAsync(string projectName = null)
        {
            try
            {
                string endpoint = "/api/v1/models/versions";
                if (!string.IsNullOrEmpty(projectName))
                {
                    endpoint += $"?project_name={Uri.EscapeDataString(projectName)}";
                }

                var response = await _httpClient.GetAsync<List<string>>(endpoint);

                if (response.Success)
                {
                    LoggingService.LogInfo($"버전 목록 조회 성공: {response.Data.Count}개", "DXnavis");
                    return response.Data;
                }
                else
                {
                    LoggingService.LogError($"버전 목록 조회 실패: {response.ErrorMessage}", "DXnavis");
                    return new List<string>();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("버전 목록 조회 중 예외", "DXnavis", ex);
                return new List<string>();
            }
        }

        /// <summary>
        /// TimeLiner 매핑 데이터 조회
        /// </summary>
        public async Task<List<TimelinerMappingData>> GetTimelinerMappingAsync(string version)
        {
            try
            {
                string endpoint = $"/api/v1/timeliner/{Uri.EscapeDataString(version)}/mapping";

                var response = await _httpClient.GetAsync<List<TimelinerMappingData>>(endpoint);

                if (response.Success)
                {
                    LoggingService.LogInfo($"TimeLiner 매핑 조회 성공: {response.Data.Count}개", "DXnavis");
                    return response.Data;
                }
                else
                {
                    LoggingService.LogError($"TimeLiner 매핑 조회 실패: {response.ErrorMessage}", "DXnavis");
                    return new List<TimelinerMappingData>();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("TimeLiner 매핑 조회 중 예외", "DXnavis", ex);
                return new List<TimelinerMappingData>();
            }
        }

        /// <summary>
        /// 버전 비교 데이터 조회
        /// </summary>
        public async Task<List<VersionDelta>> CompareVersionsAsync(string v1, string v2, string changeType = null)
        {
            try
            {
                string endpoint = $"/api/v1/models/compare?v1={Uri.EscapeDataString(v1)}&v2={Uri.EscapeDataString(v2)}";
                if (!string.IsNullOrEmpty(changeType))
                {
                    endpoint += $"&change_type={changeType}";
                }

                var response = await _httpClient.GetAsync<List<VersionDelta>>(endpoint);

                if (response.Success)
                {
                    LoggingService.LogInfo($"버전 비교 성공: {response.Data.Count}개 변경", "DXnavis");
                    return response.Data;
                }
                else
                {
                    LoggingService.LogError($"버전 비교 실패: {response.ErrorMessage}", "DXnavis");
                    return new List<VersionDelta>();
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("버전 비교 중 예외", "DXnavis", ex);
                return new List<VersionDelta>();
            }
        }
    }

    /// <summary>
    /// TimeLiner 매핑 데이터 모델
    /// </summary>
    public class TimelinerMappingData
    {
        public string ModelVersion { get; set; }
        public string ActivityId { get; set; }
        public string ObjectId { get; set; }
        public int ElementId { get; set; }
        public string Category { get; set; }
        public string Family { get; set; }
        public string Type { get; set; }
        public Dictionary<string, object> Properties { get; set; }
    }

    /// <summary>
    /// 버전 변경 데이터 모델
    /// </summary>
    public class VersionDelta
    {
        public string ObjectId { get; set; }
        public string ChangeType { get; set; } // ADDED, DELETED, MODIFIED
        public string Category { get; set; }
        public string Family { get; set; }
        public string Type { get; set; }
        public string ActivityId { get; set; }
    }
}
```

### 3.5. TimeLinerConnector.cs (TimeLiner 자동화)

**목적**: TimeLiner에 작업 및 Search Set 자동 연결

**구현해야 할 것**:
```csharp
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.DocumentParts;
using Autodesk.Navisworks.Api.Timeliner;
using DXBase.Services;
using DXnavis.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DXnavis.Services
{
    /// <summary>
    /// TimeLiner 자동화 서비스
    /// </summary>
    public class TimeLinerConnector
    {
        private readonly Document _document;
        private readonly DocumentTimeliner _timeliner;
        private readonly SearchSetService _searchSetService;

        public TimeLinerConnector(Document document)
        {
            _document = document;
            _timeliner = document.GetTimeliner();
            _searchSetService = new SearchSetService(document);
        }

        /// <summary>
        /// TimeLiner 자동 구성
        /// </summary>
        public bool AutoConfigureTimeliner(List<TimelinerMappingData> mappings, Action<int, string> progressCallback)
        {
            try
            {
                if (mappings == null || mappings.Count == 0)
                {
                    LoggingService.LogWarning("매핑 데이터가 없습니다.", "DXnavis");
                    return false;
                }

                LoggingService.LogInfo($"TimeLiner 자동 구성 시작: {mappings.Count}개 매핑", "DXnavis");

                // ActivityId별로 그룹화
                var groupedByActivity = mappings.GroupBy(m => m.ActivityId);

                int currentCount = 0;
                int totalCount = groupedByActivity.Count();

                foreach (var group in groupedByActivity)
                {
                    currentCount++;
                    string activityId = group.Key;

                    progressCallback?.Invoke((currentCount * 100) / totalCount, $"작업 생성 중: {activityId}");

                    // TimeLiner 작업 찾기 또는 생성
                    TimelinerTask task = FindOrCreateTask(activityId);

                    if (task == null)
                    {
                        LoggingService.LogWarning($"작업 생성 실패: {activityId}", "DXnavis");
                        continue;
                    }

                    // Search Set 생성 및 연결
                    var objectIds = group.Select(m => m.ObjectId).ToList();
                    bool success = CreateAndLinkSearchSet(task, activityId, objectIds);

                    if (success)
                    {
                        LoggingService.LogInfo($"작업 '{activityId}' 연결 완료: {objectIds.Count}개 객체", "DXnavis");
                    }
                    else
                    {
                        LoggingService.LogWarning($"작업 '{activityId}' 연결 실패", "DXnavis");
                    }
                }

                progressCallback?.Invoke(100, "완료");

                LoggingService.LogInfo("TimeLiner 자동 구성 완료", "DXnavis");
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("TimeLiner 자동 구성 중 오류", "DXnavis", ex);
                return false;
            }
        }

        /// <summary>
        /// TimeLiner 작업 찾기 또는 생성
        /// </summary>
        private TimelinerTask FindOrCreateTask(string activityId)
        {
            try
            {
                // 기존 작업 검색
                foreach (TimelinerTask task in _timeliner.Tasks)
                {
                    if (task.DisplayName == activityId)
                    {
                        return task;
                    }
                }

                // 새 작업 생성
                TimelinerTask newTask = new TimelinerTask
                {
                    DisplayName = activityId,
                    TaskType = TimelinerTaskType.Construct
                };

                _timeliner.Tasks.Add(newTask);

                return newTask;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"작업 찾기/생성 실패: {activityId}", "DXnavis", ex);
                return null;
            }
        }

        /// <summary>
        /// Search Set 생성 및 TimeLiner 작업에 연결
        /// </summary>
        private bool CreateAndLinkSearchSet(TimelinerTask task, string activityId, List<string> objectIds)
        {
            try
            {
                // Search Set 생성
                SavedItem searchSet = _searchSetService.CreateSearchSetByObjectIds(
                    activityId,
                    objectIds
                );

                if (searchSet == null)
                {
                    return false;
                }

                // TimeLiner 작업에 Search Set 연결
                task.Selection.Copy(searchSet);

                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Search Set 연결 실패: {activityId}", "DXnavis", ex);
                return false;
            }
        }
    }
}
```

**주의사항**:
- ⚠️ Navisworks API는 스레드 안전하지 않음 (UI 스레드에서만 호출)
- ✅ 대량 작업 시 진행률 콜백으로 사용자 경험 개선
- ❌ TimeLiner API는 트랜잭션 개념 없음 (작업 단위로 처리)

### 3.6. SearchSetService.cs (Search Set 생성)

**목적**: ObjectId 기반 Search Set 자동 생성

**구현해야 할 것**:
```csharp
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.DocumentParts;
using DXBase.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DXnavis.Services
{
    /// <summary>
    /// Search Set 생성 서비스
    /// </summary>
    public class SearchSetService
    {
        private readonly Document _document;
        private readonly DocumentSelectionSets _selectionSets;

        public SearchSetService(Document document)
        {
            _document = document;
            _selectionSets = document.SelectionSets;
        }

        /// <summary>
        /// ObjectId 목록으로 Search Set 생성
        /// </summary>
        public SavedItem CreateSearchSetByObjectIds(string setName, List<string> objectIds)
        {
            try
            {
                LoggingService.LogInfo($"Search Set 생성 시작: {setName} ({objectIds.Count}개)", "DXnavis");

                // Navisworks 모델 아이템 검색
                ModelItemCollection foundItems = FindModelItemsByObjectIds(objectIds);

                if (foundItems == null || foundItems.Count == 0)
                {
                    LoggingService.LogWarning($"매칭되는 객체를 찾을 수 없습니다: {setName}", "DXnavis");
                    return null;
                }

                // Search Set 생성
                SavedItem searchSet = new SavedItem
                {
                    DisplayName = setName
                };

                // ModelItemCollection을 Search Set에 할당
                searchSet.SetValue(foundItems);

                // SelectionSets에 추가
                _selectionSets.AddCopy(searchSet);

                LoggingService.LogInfo($"Search Set 생성 완료: {setName} ({foundItems.Count}개 매칭)", "DXnavis");

                return searchSet;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Search Set 생성 실패: {setName}", "DXnavis", ex);
                return null;
            }
        }

        /// <summary>
        /// ObjectId 목록으로 Navisworks ModelItem 검색
        /// </summary>
        private ModelItemCollection FindModelItemsByObjectIds(List<string> objectIds)
        {
            try
            {
                // 전체 모델 아이템 수집
                ModelItemCollection allItems = _document.Models.RootItems.DescendantsAndSelf;

                var foundItems = new List<ModelItem>();

                foreach (ModelItem item in allItems)
                {
                    // PropertyCategory "Element"에서 InstanceGuid 속성 찾기
                    foreach (PropertyCategory category in item.PropertyCategories)
                    {
                        if (category.DisplayName == "Element")
                        {
                            foreach (DataProperty prop in category.Properties)
                            {
                                if (prop.DisplayName == "InstanceGuid")
                                {
                                    string guid = prop.Value.ToDisplayString();

                                    if (objectIds.Contains(guid))
                                    {
                                        foundItems.Add(item);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }

                LoggingService.LogInfo($"모델 아이템 검색 완료: {foundItems.Count}/{objectIds.Count}", "DXnavis");

                return new ModelItemCollection(foundItems);
            }
            catch (Exception ex)
            {
                LoggingService.LogError("모델 아이템 검색 실패", "DXnavis", ex);
                return null;
            }
        }
    }
}
```

**성능 최적화**:
- ✅ 속성 검색을 캐시하여 중복 작업 방지
- ✅ 배치 처리로 대량 객체 효율적 처리
- ⚠️ DescendantsAndSelf는 느릴 수 있음 (대형 모델 주의)

### 3.7. TimeLinerViewModel.cs (4D 자동화 UI 로직)

**구현해야 할 것**:
```csharp
using Autodesk.Navisworks.Api;
using DXBase.Services;
using DXnavis.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DXnavis.ViewModels
{
    public class TimeLinerViewModel : INotifyPropertyChanged
    {
        private readonly Document _document;
        private readonly ApiDataReader _apiDataReader;
        private readonly TimeLinerConnector _timelinerConnector;

        private List<string> _versions;
        private string _selectedVersion;
        private bool _isProcessing;
        private string _statusMessage;
        private int _progressValue;

        public TimeLinerViewModel(Document document)
        {
            _document = document;
            _apiDataReader = new ApiDataReader();
            _timelinerConnector = new TimeLinerConnector(document);

            Versions = new List<string>();
            StatusMessage = "버전 목록 로딩 중...";

            // 커맨드 초기화
            LoadVersionsCommand = new RelayCommand(async () => await ExecuteLoadVersionsAsync());
            AutoConfigureCommand = new RelayCommand(async () => await ExecuteAutoConfigureAsync(), CanExecuteAutoConfigure);
            CancelCommand = new RelayCommand(ExecuteCancel);

            // 초기 로드
            Task.Run(async () => await ExecuteLoadVersionsAsync());
        }

        #region Properties

        public List<string> Versions
        {
            get => _versions;
            set
            {
                _versions = value;
                OnPropertyChanged();
            }
        }

        public string SelectedVersion
        {
            get => _selectedVersion;
            set
            {
                _selectedVersion = value;
                OnPropertyChanged();
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                _isProcessing = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanInput));
            }
        }

        public bool CanInput => !IsProcessing;

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public int ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Commands

        public ICommand LoadVersionsCommand { get; }
        public ICommand AutoConfigureCommand { get; }
        public ICommand CancelCommand { get; }

        private async Task ExecuteLoadVersionsAsync()
        {
            try
            {
                IsProcessing = true;
                StatusMessage = "버전 목록 로딩 중...";

                var versions = await _apiDataReader.GetVersionsAsync();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Versions = versions;
                    if (Versions.Count > 0)
                    {
                        SelectedVersion = Versions[0];
                        StatusMessage = $"{Versions.Count}개 버전 로드 완료";
                    }
                    else
                    {
                        StatusMessage = "버전이 없습니다.";
                    }
                });
            }
            catch (Exception ex)
            {
                StatusMessage = "버전 목록 로드 실패";
                LoggingService.LogError("버전 목록 로드 실패", "DXnavis", ex);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private bool CanExecuteAutoConfigure()
        {
            return !IsProcessing && !string.IsNullOrEmpty(SelectedVersion);
        }

        private async Task ExecuteAutoConfigureAsync()
        {
            try
            {
                IsProcessing = true;
                ProgressValue = 0;

                // 1. 매핑 데이터 조회
                StatusMessage = "매핑 데이터 조회 중...";
                ProgressValue = 10;

                var mappings = await _apiDataReader.GetTimelinerMappingAsync(SelectedVersion);

                if (mappings == null || mappings.Count == 0)
                {
                    StatusMessage = "매핑 데이터가 없습니다.";
                    MessageBox.Show(
                        "선택한 버전에 ActivityId가 있는 객체가 없습니다.\nRevit에서 공정 ID를 입력한 후 스냅샷을 다시 저장해주세요.",
                        "데이터 없음",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                ProgressValue = 30;

                // 2. TimeLiner 자동 구성
                StatusMessage = "TimeLiner 자동 구성 중...";

                bool success = await Task.Run(() =>
                    _timelinerConnector.AutoConfigureTimeliner(mappings, UpdateProgress));

                ProgressValue = 100;

                if (success)
                {
                    StatusMessage = "TimeLiner 자동 구성 완료!";
                    MessageBox.Show(
                        $"TimeLiner 자동 구성이 완료되었습니다.\n\n" +
                        $"버전: {SelectedVersion}\n" +
                        $"매핑 수: {mappings.Count}\n\n" +
                        $"TimeLiner 탭에서 확인하세요.",
                        "완료",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // 창 닫기
                    Application.Current.Windows[Application.Current.Windows.Count - 1].DialogResult = true;
                }
                else
                {
                    StatusMessage = "자동 구성 실패";
                    MessageBox.Show(
                        "TimeLiner 자동 구성에 실패했습니다.\n로그 파일을 확인해주세요.",
                        "실패",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "오류 발생";
                LoggingService.LogError("TimeLiner 자동 구성 중 오류", "DXnavis", ex);
                MessageBox.Show(
                    $"오류가 발생했습니다:\n{ex.Message}",
                    "오류",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
            }
        }

        private void UpdateProgress(int percentage, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProgressValue = percentage;
                StatusMessage = message;
            });
        }

        private void ExecuteCancel()
        {
            Application.Current.Windows[Application.Current.Windows.Count - 1].DialogResult = false;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    /// <summary>
    /// 간단한 RelayCommand 구현
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object parameter) => _execute();
    }
}
```

### 3.8. TimeLinerView.xaml (4D 자동화 UI)

**구현해야 할 것**:
```xml
<Window x:Class="DXnavis.Views.TimeLinerView"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="TimeLiner 자동 구성 - DX Platform"
        Width="500" Height="350"
        WindowStartupLocation="CenterScreen"
        ResizeMode="NoResize">

    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- 제목 -->
        <TextBlock Grid.Row="0"
                   Text="TimeLiner 자동 구성"
                   FontSize="18"
                   FontWeight="Bold"
                   Margin="0,0,0,20"/>

        <!-- 버전 선택 -->
        <StackPanel Grid.Row="1" Margin="0,0,0,10">
            <TextBlock Text="버전 선택*" FontWeight="Bold"/>
            <ComboBox ItemsSource="{Binding Versions}"
                      SelectedItem="{Binding SelectedVersion}"
                      IsEnabled="{Binding CanInput}"
                      Margin="0,5,0,0" Padding="5"/>
            <Button Content="새로고침"
                    Width="80"
                    HorizontalAlignment="Right"
                    Margin="0,5,0,0"
                    Command="{Binding LoadVersionsCommand}"/>
        </StackPanel>

        <!-- 정보 패널 -->
        <Border Grid.Row="2"
                BorderBrush="LightGray"
                BorderThickness="1"
                Padding="10"
                Margin="0,10,0,10">
            <TextBlock TextWrapping="Wrap">
                <Run Text="이 기능은 선택한 버전의 ActivityId(공정 ID)를 기반으로"/>
                <LineBreak/>
                <Run Text="TimeLiner 작업을 자동으로 생성하고 객체를 연결합니다."/>
                <LineBreak/>
                <LineBreak/>
                <Run Text="• Revit에서 ActivityId 공유 매개변수 입력 필요"/>
                <LineBreak/>
                <Run Text="• 매핑 데이터는 API 서버에서 자동 조회"/>
                <LineBreak/>
                <Run Text="• 기존 TimeLiner 작업이 있으면 재사용"/>
            </TextBlock>
        </Border>

        <!-- 진행률 표시 -->
        <StackPanel Grid.Row="3" Margin="0,0,0,10">
            <TextBlock Text="{Binding StatusMessage}" Margin="0,0,0,5"/>
            <ProgressBar Height="20"
                         Value="{Binding ProgressValue}"
                         Minimum="0" Maximum="100"/>
        </StackPanel>

        <!-- 버튼 -->
        <StackPanel Grid.Row="4"
                    Orientation="Horizontal"
                    HorizontalAlignment="Right">
            <Button Content="자동 구성"
                    Width="100" Height="30"
                    Margin="0,0,10,0"
                    Command="{Binding AutoConfigureCommand}"/>
            <Button Content="취소"
                    Width="80" Height="30"
                    Command="{Binding CancelCommand}"/>
        </StackPanel>
    </Grid>
</Window>
```

---

## 4. 주의사항 및 금지사항

### 4.1. ✅ 해야 할 것

**API 통신**:
- ✅ 비동기 처리 (async/await)
- ✅ 타임아웃 설정
- ✅ 오류 로깅

**Navisworks API**:
- ✅ UI 스레드에서만 API 호출
- ✅ Dispatcher.Invoke 사용
- ✅ 진행률 표시

### 4.2. ❌ 하지 말아야 할 것

**데이터 수정**:
- ❌ API를 통한 데이터 수정/삭제 요청
- ❌ 데이터베이스 직접 접근

**스레딩**:
- ❌ 백그라운드 스레드에서 Navisworks API 호출
- ❌ Task.Run 내부에서 Document 접근

### 4.3. ⚠️ 사용자가 직접 제어해야 할 것

**BIM 엔지니어 책임**:
- ⚠️ Navisworks 모델 열기
- ⚠️ 버전 선택
- ⚠️ TimeLiner 최종 검토 및 조정
- ⚠️ 4D 시뮬레이션 실행

---

## 5. 다음 단계

DXnavis 구현 완료 후:
1. Phase 6 (통합 테스트 및 배포) 참조
2. End-to-End 워크플로우 테스트
3. 사용자 가이드 작성

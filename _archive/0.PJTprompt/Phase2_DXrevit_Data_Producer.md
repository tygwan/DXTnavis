# Phase 2: DXrevit 데이터 생산자 개발

## 문서 목적
Revit 애드인인 DXrevit의 구현 가이드입니다. Revit API를 통해 BIM 데이터를 추출하고 FastAPI 서버로 전송하는 모든 기능을 다룹니다.

---

## 1. 개요

### 1.1. DXrevit의 역할
**핵심 책임**: Revit → FastAPI → PostgreSQL (Write Only)

**주요 기능**:
1. 사용자 정의 공유 매개변수 관리
2. BIM 모델 데이터 추출
3. 스냅샷 생성 및 패키징
4. API 서버로 데이터 전송
5. 설정 UI 제공

### 1.2. 기술 스펙
- **프레임워크**: .NET Core 8.0 (Revit 2025)
- **Revit API**: 2025
- **UI**: WPF (Windows Presentation Foundation)
- **의존성**: DXBase, RevitAPI.dll, RevitAPIUI.dll

---

## 2. 프로젝트 구조

### 2.1. 폴더 구조
```
DXrevit/
├── DXrevit.csproj
├── DXrevit.addin                    # Revit 매니페스트 파일
├── Commands/
│   ├── SnapshotCommand.cs           # 스냅샷 저장 커맨드
│   ├── SettingsCommand.cs           # 설정 창 열기 커맨드
│   └── ParameterSetupCommand.cs     # 공유 매개변수 설정 커맨드
├── Services/
│   ├── DataExtractor.cs             # Revit 데이터 추출 서비스
│   ├── ApiDataWriter.cs             # API 클라이언트 서비스
│   ├── ParameterService.cs          # 공유 매개변수 관리 서비스
│   └── ProgressService.cs           # 진행률 표시 서비스
├── ViewModels/
│   ├── SnapshotViewModel.cs         # 스냅샷 UI 로직
│   └── SettingsViewModel.cs         # 설정 UI 로직
├── Views/
│   ├── SnapshotView.xaml            # 스냅샷 저장 창
│   ├── SnapshotView.xaml.cs
│   ├── SettingsView.xaml            # 설정 창
│   └── SettingsView.xaml.cs
├── Utils/
│   ├── RevitElementHelper.cs        # Revit Element 헬퍼
│   └── GeometryHelper.cs            # 기하학적 계산 헬퍼
└── Resources/
    ├── Icons/                       # 리본 아이콘
    └── SharedParameters.txt         # 공유 매개변수 정의 파일
```

---

## 3. 핵심 기능 구현

### 3.1. 매니페스트 파일 (DXrevit.addin)

**목적**: Revit에 애드인 등록

**구현해야 할 것**:
```xml
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>DXrevit</Name>
    <FullClassName>DXrevit.Application</FullClassName>
    <Text>DX Platform - Revit Data Producer</Text>
    <Description>BIM 데이터를 추출하여 중앙 데이터베이스에 저장</Description>
    <VisibilityMode>AlwaysVisible</VisibilityMode>
    <Assembly>DXrevit.dll</Assembly>
    <AddInId>12345678-1234-1234-1234-123456789ABC</AddInId>
    <VendorId>DX</VendorId>
    <VendorDescription>DX Platform</VendorDescription>
  </AddIn>
</RevitAddIns>
```

**배치 위치**:
- Windows: `C:\ProgramData\Autodesk\Revit\Addins\2025\DXrevit.addin`

### 3.2. Application.cs (애드인 진입점)

**구현해야 할 것**:
```csharp
using Autodesk.Revit.UI;
using System;
using System.Reflection;
using System.Windows.Media.Imaging;
using DXBase.Services;

namespace DXrevit
{
    /// <summary>
    /// Revit 애드인 진입점
    /// </summary>
    public class Application : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // 설정 및 로깅 초기화
                var settings = ConfigurationService.LoadSettings();
                LoggingService.Initialize(settings.LogFilePath);
                LoggingService.LogInfo("DXrevit 애드인 시작", "DXrevit");

                // 리본 탭 생성
                string tabName = "DX Platform";
                application.CreateRibbonTab(tabName);

                // 리본 패널 생성
                RibbonPanel panel = application.CreateRibbonPanel(tabName, "데이터 관리");

                // 스냅샷 저장 버튼 추가
                AddSnapshotButton(panel);

                // 공유 매개변수 설정 버튼 추가
                AddParameterSetupButton(panel);

                // 설정 버튼 추가
                AddSettingsButton(panel);

                LoggingService.LogInfo("DXrevit 리본 UI 생성 완료", "DXrevit");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                LoggingService.LogError("DXrevit 초기화 실패", "DXrevit", ex);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            LoggingService.LogInfo("DXrevit 애드인 종료", "DXrevit");
            return Result.Succeeded;
        }

        private void AddSnapshotButton(RibbonPanel panel)
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            PushButtonData buttonData = new PushButtonData(
                "SnapshotButton",
                "스냅샷 저장",
                assemblyPath,
                "DXrevit.Commands.SnapshotCommand");

            PushButton button = panel.AddItem(buttonData) as PushButton;
            button.ToolTip = "현재 BIM 모델의 스냅샷을 데이터베이스에 저장합니다.";
            button.LongDescription = "모든 객체, 속성, 관계를 추출하여 중앙 데이터베이스에 저장합니다. 설계 변경 이력 추적의 기준점이 됩니다.";

            // 아이콘 설정 (선택사항)
            // button.LargeImage = new BitmapImage(new Uri("path/to/icon.png"));
        }

        private void AddParameterSetupButton(RibbonPanel panel)
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            PushButtonData buttonData = new PushButtonData(
                "ParameterSetupButton",
                "매개변수 설정",
                assemblyPath,
                "DXrevit.Commands.ParameterSetupCommand");

            PushButton button = panel.AddItem(buttonData) as PushButton;
            button.ToolTip = "공유 매개변수를 프로젝트에 추가합니다.";
        }

        private void AddSettingsButton(RibbonPanel panel)
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            PushButtonData buttonData = new PushButtonData(
                "SettingsButton",
                "설정",
                assemblyPath,
                "DXrevit.Commands.SettingsCommand");

            PushButton button = panel.AddItem(buttonData) as PushButton;
            button.ToolTip = "DXrevit 설정을 변경합니다.";
        }
    }
}
```

### 3.3. SnapshotCommand.cs (스냅샷 저장 커맨드)

**구현해야 할 것**:
```csharp
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DXrevit.ViewModels;
using DXrevit.Views;
using System;

namespace DXrevit.Commands
{
    [Transaction(TransactionMode.ReadOnly)]
    public class SnapshotCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;

                // 문서가 저장되어 있는지 확인
                if (!doc.IsValidObject || string.IsNullOrEmpty(doc.PathName))
                {
                    TaskDialog.Show("오류", "문서를 먼저 저장해주세요.");
                    return Result.Cancelled;
                }

                // 스냅샷 UI 표시
                var viewModel = new SnapshotViewModel(doc);
                var view = new SnapshotView { DataContext = viewModel };

                bool? dialogResult = view.ShowDialog();

                if (dialogResult == true)
                {
                    return Result.Succeeded;
                }
                else
                {
                    return Result.Cancelled;
                }
            }
            catch (Exception ex)
            {
                message = ex.Message;
                LoggingService.LogError("스냅샷 커맨드 실행 실패", "DXrevit", ex);
                return Result.Failed;
            }
        }
    }
}
```

### 3.4. DataExtractor.cs (데이터 추출 서비스)

**목적**: Revit API를 통해 모든 BIM 데이터 추출

**구현해야 할 것**:
```csharp
using Autodesk.Revit.DB;
using DXBase.Models;
using DXBase.Services;
using DXBase.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DXrevit.Services
{
    /// <summary>
    /// Revit 데이터 추출 서비스
    /// </summary>
    public class DataExtractor
    {
        private readonly Document _document;

        public DataExtractor(Document document)
        {
            _document = document;
        }

        /// <summary>
        /// 전체 데이터 추출 (메타데이터, 객체, 관계)
        /// </summary>
        public ExtractedData ExtractAll(string modelVersion, string createdBy, string description)
        {
            LoggingService.LogInfo("데이터 추출 시작", "DXrevit");

            var extractedData = new ExtractedData
            {
                Metadata = ExtractMetadata(modelVersion, createdBy, description),
                Objects = new List<ObjectRecord>(),
                Relationships = new List<RelationshipRecord>()
            };

            // 모든 Element 수집 (ViewSpecific 제외)
            var collector = new FilteredElementCollector(_document)
                .WhereElementIsNotElementType()
                .WhereElementIsViewIndependent();

            int totalCount = collector.GetElementCount();
            int currentCount = 0;

            LoggingService.LogInfo($"총 {totalCount}개 객체 추출 시작", "DXrevit");

            foreach (Element element in collector)
            {
                currentCount++;

                // 진행률 보고 (10%마다)
                if (currentCount % (totalCount / 10) == 0)
                {
                    LoggingService.LogInfo($"진행률: {currentCount}/{totalCount}", "DXrevit");
                }

                // 객체 데이터 추출
                var objectRecord = ExtractObjectData(element, modelVersion);
                if (objectRecord != null)
                {
                    extractedData.Objects.Add(objectRecord);
                }

                // 관계 데이터 추출
                var relationships = ExtractRelationships(element, modelVersion);
                extractedData.Relationships.AddRange(relationships);
            }

            extractedData.Metadata.TotalObjectCount = extractedData.Objects.Count;

            LoggingService.LogInfo($"데이터 추출 완료: {extractedData.Objects.Count}개 객체, {extractedData.Relationships.Count}개 관계", "DXrevit");

            return extractedData;
        }

        /// <summary>
        /// 메타데이터 추출
        /// </summary>
        private MetadataRecord ExtractMetadata(string modelVersion, string createdBy, string description)
        {
            return new MetadataRecord
            {
                ModelVersion = modelVersion,
                Timestamp = DateTime.UtcNow,
                ProjectName = _document.ProjectInformation.Name ?? "Unknown",
                CreatedBy = createdBy,
                Description = description,
                RevitFilePath = _document.PathName,
                TotalObjectCount = 0 // 나중에 업데이트됨
            };
        }

        /// <summary>
        /// 객체 데이터 추출
        /// </summary>
        private ObjectRecord ExtractObjectData(Element element, string modelVersion)
        {
            try
            {
                // 카테고리 필터링 (불필요한 카테고리 제외)
                if (element.Category == null || !IsValidCategory(element.Category.Name))
                {
                    return null;
                }

                // InstanceGuid 가져오기 (없으면 경로 기반 해시 생성)
                string instanceGuid = element.UniqueId;
                string objectId = IdGenerator.GenerateObjectId(
                    instanceGuid,
                    element.Category.Name,
                    GetFamilyName(element),
                    GetTypeName(element));

                // ActivityId (공정 ID) 가져오기
                string activityId = GetParameterValue(element, "ActivityId");

                // 모든 매개변수를 JSON으로 변환
                var properties = ExtractProperties(element);
                string propertiesJson = JsonHelper.Serialize(properties);

                // 바운딩 박스 추출
                string boundingBoxJson = ExtractBoundingBox(element);

                return new ObjectRecord
                {
                    ModelVersion = modelVersion,
                    ObjectId = objectId,
                    ElementId = element.Id.IntegerValue,
                    Category = element.Category.Name,
                    Family = GetFamilyName(element),
                    Type = GetTypeName(element),
                    ActivityId = activityId,
                    Properties = propertiesJson,
                    BoundingBox = boundingBoxJson
                };
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"객체 추출 실패: ElementId={element.Id}", "DXrevit", ex);
                return null;
            }
        }

        /// <summary>
        /// 관계 데이터 추출 (호스트 관계)
        /// </summary>
        private List<RelationshipRecord> ExtractRelationships(Element element, string modelVersion)
        {
            var relationships = new List<RelationshipRecord>();

            try
            {
                // FamilyInstance인 경우 호스트 관계 확인
                if (element is FamilyInstance familyInstance && familyInstance.Host != null)
                {
                    string sourceId = IdGenerator.GenerateObjectId(
                        familyInstance.Host.UniqueId,
                        familyInstance.Host.Category?.Name ?? "Unknown",
                        GetFamilyName(familyInstance.Host),
                        GetTypeName(familyInstance.Host));

                    string targetId = IdGenerator.GenerateObjectId(
                        element.UniqueId,
                        element.Category.Name,
                        GetFamilyName(element),
                        GetTypeName(element));

                    relationships.Add(new RelationshipRecord
                    {
                        ModelVersion = modelVersion,
                        SourceObjectId = sourceId,
                        TargetObjectId = targetId,
                        RelationType = "HostedBy",
                        IsDirected = true
                    });
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"관계 추출 실패: ElementId={element.Id}", "DXrevit", ex);
            }

            return relationships;
        }

        /// <summary>
        /// 모든 매개변수 추출
        /// </summary>
        private Dictionary<string, object> ExtractProperties(Element element)
        {
            var properties = new Dictionary<string, object>();

            foreach (Parameter param in element.Parameters)
            {
                try
                {
                    if (!param.HasValue)
                        continue;

                    string paramName = param.Definition.Name;
                    object paramValue = GetParameterValueAsObject(param);

                    if (paramValue != null)
                    {
                        properties[paramName] = paramValue;
                    }
                }
                catch
                {
                    // 매개변수 읽기 실패 무시
                }
            }

            return properties;
        }

        /// <summary>
        /// 바운딩 박스 추출
        /// </summary>
        private string ExtractBoundingBox(Element element)
        {
            try
            {
                BoundingBoxXYZ bbox = element.get_BoundingBox(null);
                if (bbox != null)
                {
                    var bboxData = new
                    {
                        MinX = bbox.Min.X,
                        MinY = bbox.Min.Y,
                        MinZ = bbox.Min.Z,
                        MaxX = bbox.Max.X,
                        MaxY = bbox.Max.Y,
                        MaxZ = bbox.Max.Z
                    };
                    return JsonHelper.Serialize(bboxData);
                }
            }
            catch
            {
                // 바운딩 박스 없음
            }

            return null;
        }

        /// <summary>
        /// 유효한 카테고리 판별
        /// </summary>
        private bool IsValidCategory(string categoryName)
        {
            // 제외할 카테고리 목록
            var excludedCategories = new HashSet<string>
            {
                "Views",
                "Sheets",
                "Schedules",
                "Legends",
                "RVT Links",
                "Imports in Families"
            };

            return !excludedCategories.Contains(categoryName);
        }

        /// <summary>
        /// 패밀리 이름 가져오기
        /// </summary>
        private string GetFamilyName(Element element)
        {
            if (element is FamilyInstance familyInstance)
            {
                return familyInstance.Symbol?.FamilyName ?? "Unknown";
            }
            return element.Name ?? "Unknown";
        }

        /// <summary>
        /// 타입 이름 가져오기
        /// </summary>
        private string GetTypeName(Element element)
        {
            ElementId typeId = element.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                ElementType elementType = _document.GetElement(typeId) as ElementType;
                return elementType?.Name ?? "Unknown";
            }
            return "Unknown";
        }

        /// <summary>
        /// 매개변수 값 가져오기 (문자열)
        /// </summary>
        private string GetParameterValue(Element element, string parameterName)
        {
            Parameter param = element.LookupParameter(parameterName);
            if (param != null && param.HasValue)
            {
                return param.AsValueString() ?? param.AsString();
            }
            return null;
        }

        /// <summary>
        /// 매개변수 값 가져오기 (객체)
        /// </summary>
        private object GetParameterValueAsObject(Parameter param)
        {
            switch (param.StorageType)
            {
                case StorageType.String:
                    return param.AsString();
                case StorageType.Integer:
                    return param.AsInteger();
                case StorageType.Double:
                    return param.AsDouble();
                case StorageType.ElementId:
                    return param.AsElementId().IntegerValue;
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// 추출된 전체 데이터를 담는 컨테이너
    /// </summary>
    public class ExtractedData
    {
        public MetadataRecord Metadata { get; set; }
        public List<ObjectRecord> Objects { get; set; }
        public List<RelationshipRecord> Relationships { get; set; }
    }
}
```

**주의사항**:
- ✅ FilteredElementCollector로 효율적으로 Element 수집
- ✅ 불필요한 카테고리 제외 (성능 최적화)
- ✅ 예외 처리로 일부 객체 실패해도 전체 프로세스 계속
- ❌ 모든 매개변수를 추출하려고 하지 말 것 (시간 소요)
- ❌ 3D 형상(Geometry) 추출은 피할 것 (대용량 데이터)

### 3.5. ApiDataWriter.cs (API 클라이언트)

**목적**: 추출된 데이터를 API 서버로 전송

**구현해야 할 것**:
```csharp
using DXBase.Models;
using DXBase.Services;
using System;
using System.Threading.Tasks;

namespace DXrevit.Services
{
    /// <summary>
    /// API 서버로 데이터 전송 서비스
    /// </summary>
    public class ApiDataWriter
    {
        private readonly HttpClientService _httpClient;

        public ApiDataWriter()
        {
            var settings = ConfigurationService.LoadSettings();
            _httpClient = new HttpClientService(settings.ApiServerUrl, settings.TimeoutSeconds);
        }

        /// <summary>
        /// 추출된 데이터를 API 서버로 전송
        /// </summary>
        public async Task<bool> SendDataAsync(ExtractedData extractedData)
        {
            try
            {
                LoggingService.LogInfo("API 서버로 데이터 전송 시작", "DXrevit");

                var response = await _httpClient.PostAsync<ExtractedData, object>(
                    "/api/v1/ingest",
                    extractedData);

                if (response.Success)
                {
                    LoggingService.LogInfo("데이터 전송 성공", "DXrevit");
                    return true;
                }
                else
                {
                    LoggingService.LogError($"데이터 전송 실패: {response.ErrorMessage}", "DXrevit");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError("데이터 전송 중 예외 발생", "DXrevit", ex);
                return false;
            }
        }
    }
}
```

### 3.6. SnapshotViewModel.cs (스냅샷 UI 로직)

**구현해야 할 것**:
```csharp
using Autodesk.Revit.DB;
using DXBase.Services;
using DXBase.Utils;
using DXrevit.Services;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DXrevit.ViewModels
{
    public class SnapshotViewModel : INotifyPropertyChanged
    {
        private readonly Document _document;
        private readonly DataExtractor _dataExtractor;
        private readonly ApiDataWriter _apiDataWriter;

        private string _modelVersion;
        private string _description;
        private string _createdBy;
        private bool _isProcessing;
        private string _statusMessage;
        private int _progressValue;

        public SnapshotViewModel(Document document)
        {
            _document = document;
            _dataExtractor = new DataExtractor(document);
            _apiDataWriter = new ApiDataWriter();

            // 기본값 설정
            var settings = ConfigurationService.LoadSettings();
            ModelVersion = IdGenerator.GenerateModelVersion(_document.ProjectInformation.Name);
            CreatedBy = settings.DefaultUsername ?? Environment.UserName;
            Description = $"{DateTime.Now:yyyy-MM-dd} 스냅샷";
            StatusMessage = "저장 준비 완료";

            // 커맨드 초기화
            SaveCommand = new RelayCommand(async () => await ExecuteSaveAsync(), CanExecuteSave);
            CancelCommand = new RelayCommand(ExecuteCancel);
        }

        #region Properties

        public string ModelVersion
        {
            get => _modelVersion;
            set
            {
                _modelVersion = value;
                OnPropertyChanged();
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        public string CreatedBy
        {
            get => _createdBy;
            set
            {
                _createdBy = value;
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

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        private bool CanExecuteSave()
        {
            return !IsProcessing &&
                   !string.IsNullOrWhiteSpace(ModelVersion) &&
                   !string.IsNullOrWhiteSpace(CreatedBy);
        }

        private async Task ExecuteSaveAsync()
        {
            try
            {
                IsProcessing = true;
                ProgressValue = 0;

                // 1. 데이터 추출
                StatusMessage = "Revit 데이터 추출 중...";
                ProgressValue = 10;

                var extractedData = await Task.Run(() =>
                    _dataExtractor.ExtractAll(ModelVersion, CreatedBy, Description));

                ProgressValue = 50;

                // 2. 데이터 전송
                StatusMessage = "API 서버로 데이터 전송 중...";
                ProgressValue = 60;

                bool success = await _apiDataWriter.SendDataAsync(extractedData);

                ProgressValue = 100;

                if (success)
                {
                    StatusMessage = "스냅샷 저장 완료!";
                    MessageBox.Show(
                        $"스냅샷이 성공적으로 저장되었습니다.\n\n" +
                        $"버전: {ModelVersion}\n" +
                        $"객체 수: {extractedData.Objects.Count}\n" +
                        $"관계 수: {extractedData.Relationships.Count}",
                        "저장 완료",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // 창 닫기
                    Application.Current.Windows[Application.Current.Windows.Count - 1].DialogResult = true;
                }
                else
                {
                    StatusMessage = "저장 실패";
                    MessageBox.Show(
                        "스냅샷 저장에 실패했습니다.\n로그 파일을 확인해주세요.",
                        "저장 실패",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "오류 발생";
                LoggingService.LogError("스냅샷 저장 중 오류", "DXrevit", ex);
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

### 3.7. SnapshotView.xaml (스냅샷 UI)

**구현해야 할 것**:
```xml
<Window x:Class="DXrevit.Views.SnapshotView"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="스냅샷 저장 - DX Platform"
        Width="500" Height="400"
        WindowStartupLocation="CenterScreen"
        ResizeMode="NoResize">

    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <!-- 제목 -->
        <TextBlock Grid.Row="0"
                   Text="BIM 모델 스냅샷 저장"
                   FontSize="18"
                   FontWeight="Bold"
                   Margin="0,0,0,20"/>

        <!-- 버전 입력 -->
        <StackPanel Grid.Row="1" Margin="0,0,0,10">
            <TextBlock Text="버전 (ModelVersion)*" FontWeight="Bold"/>
            <TextBox Text="{Binding ModelVersion, UpdateSourceTrigger=PropertyChanged}"
                     IsEnabled="{Binding CanInput}"
                     Margin="0,5,0,0" Padding="5"/>
        </StackPanel>

        <!-- 작성자 입력 -->
        <StackPanel Grid.Row="2" Margin="0,0,0,10">
            <TextBlock Text="작성자 (CreatedBy)*" FontWeight="Bold"/>
            <TextBox Text="{Binding CreatedBy, UpdateSourceTrigger=PropertyChanged}"
                     IsEnabled="{Binding CanInput}"
                     Margin="0,5,0,0" Padding="5"/>
        </StackPanel>

        <!-- 설명 입력 -->
        <StackPanel Grid.Row="3" Margin="0,0,0,10">
            <TextBlock Text="설명 (Description)" FontWeight="Bold"/>
            <TextBox Text="{Binding Description, UpdateSourceTrigger=PropertyChanged}"
                     IsEnabled="{Binding CanInput}"
                     Margin="0,5,0,0" Padding="5"
                     Height="60"
                     TextWrapping="Wrap"
                     AcceptsReturn="True"
                     VerticalScrollBarVisibility="Auto"/>
        </StackPanel>

        <!-- 진행률 표시 -->
        <StackPanel Grid.Row="5" Margin="0,0,0,10">
            <TextBlock Text="{Binding StatusMessage}" Margin="0,0,0,5"/>
            <ProgressBar Height="20"
                         Value="{Binding ProgressValue}"
                         Minimum="0" Maximum="100"/>
        </StackPanel>

        <!-- 버튼 -->
        <StackPanel Grid.Row="6"
                    Orientation="Horizontal"
                    HorizontalAlignment="Right">
            <Button Content="저장"
                    Width="80" Height="30"
                    Margin="0,0,10,0"
                    Command="{Binding SaveCommand}"/>
            <Button Content="취소"
                    Width="80" Height="30"
                    Command="{Binding CancelCommand}"/>
        </StackPanel>
    </Grid>
</Window>
```

---

## 4. 공유 매개변수 관리

### 4.1. 공유 매개변수 정의 파일

**SharedParameters.txt**:
```
# This is a Autodesk shared parameter file.
# Do not edit manually.
*META	VERSION	MINVERSION
META	2	1
*GROUP	ID	NAME
GROUP	1	DX_Platform
*PARAM	GUID	NAME	DATATYPE	DATACATEGORY	GROUP	VISIBLE	DESCRIPTION	USERMODIFIABLE	HIDEWHENNOVALUE
PARAM	12345678-1234-1234-1234-123456789001	ActivityId	TEXT		1	1	공정 ID (TimeLiner 연결용)	1	0
PARAM	12345678-1234-1234-1234-123456789002	Cost	NUMBER		1	1	비용	1	0
PARAM	12345678-1234-1234-1234-123456789003	Material	TEXT		1	1	자재명	1	0
```

### 4.2. ParameterSetupCommand.cs

**구현해야 할 것**:
```csharp
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.IO;
using System.Reflection;

namespace DXrevit.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ParameterSetupCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;
                Application app = commandData.Application.Application;

                // SharedParameters.txt 경로
                string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string sharedParamFile = Path.Combine(assemblyPath, "Resources", "SharedParameters.txt");

                if (!File.Exists(sharedParamFile))
                {
                    TaskDialog.Show("오류", "공유 매개변수 파일을 찾을 수 없습니다.");
                    return Result.Failed;
                }

                // 공유 매개변수 파일 설정
                app.SharedParametersFilename = sharedParamFile;
                DefinitionFile defFile = app.OpenSharedParameterFile();

                if (defFile == null)
                {
                    TaskDialog.Show("오류", "공유 매개변수 파일을 열 수 없습니다.");
                    return Result.Failed;
                }

                // 그룹 찾기
                DefinitionGroup defGroup = defFile.Groups.get_Item("DX_Platform");
                if (defGroup == null)
                {
                    TaskDialog.Show("오류", "DX_Platform 그룹을 찾을 수 없습니다.");
                    return Result.Failed;
                }

                // 트랜잭션 시작
                using (Transaction trans = new Transaction(doc, "공유 매개변수 추가"))
                {
                    trans.Start();

                    // 적용할 카테고리 (예: 벽, 문, 창문 등)
                    CategorySet categories = app.Create.NewCategorySet();
                    categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_Walls));
                    categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_Doors));
                    categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_Windows));
                    categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_StructuralColumns));
                    categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_StructuralFraming));
                    categories.Insert(doc.Settings.Categories.get_Item(BuiltInCategory.OST_Floors));
                    // 필요한 카테고리 추가...

                    // ActivityId 매개변수 추가
                    ExternalDefinition activityIdDef = defGroup.Definitions.get_Item("ActivityId") as ExternalDefinition;
                    if (activityIdDef != null)
                    {
                        InstanceBinding binding = app.Create.NewInstanceBinding(categories);
                        doc.ParameterBindings.Insert(activityIdDef, binding, BuiltInParameterGroup.PG_DATA);
                    }

                    // Cost 매개변수 추가
                    ExternalDefinition costDef = defGroup.Definitions.get_Item("Cost") as ExternalDefinition;
                    if (costDef != null)
                    {
                        InstanceBinding binding = app.Create.NewInstanceBinding(categories);
                        doc.ParameterBindings.Insert(costDef, binding, BuiltInParameterGroup.PG_DATA);
                    }

                    // Material 매개변수 추가
                    ExternalDefinition materialDef = defGroup.Definitions.get_Item("Material") as ExternalDefinition;
                    if (materialDef != null)
                    {
                        InstanceBinding binding = app.Create.NewInstanceBinding(categories);
                        doc.ParameterBindings.Insert(materialDef, binding, BuiltInParameterGroup.PG_DATA);
                    }

                    trans.Commit();
                }

                TaskDialog.Show("성공", "공유 매개변수가 프로젝트에 추가되었습니다.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
```

---

## 5. 주의사항 및 금지사항

### 5.1. ✅ 해야 할 것

**데이터 추출**:
- ✅ 모든 예외 상황 처리 (Element 접근 불가 등)
- ✅ 진행률 표시로 사용자 경험 개선
- ✅ 필요한 카테고리만 선택적으로 추출 (성능)
- ✅ ObjectId 생성 시 일관성 유지

**API 통신**:
- ✅ 타임아웃 설정
- ✅ 재시도 로직 (네트워크 일시 장애 대응)
- ✅ 자세한 오류 로깅

**사용자 인터페이스**:
- ✅ 입력 검증 (필수 필드)
- ✅ 진행 중 UI 잠금 (중복 실행 방지)
- ✅ 친절한 오류 메시지

### 5.2. ❌ 하지 말아야 할 것

**데이터베이스 직접 접근**:
- ❌ Npgsql 등 DB 드라이버 사용 금지
- ❌ 데이터베이스 연결 문자열 포함 금지

**데이터 읽기**:
- ❌ API 서버에서 데이터 읽기 금지 (DXrevit은 Write Only)
- ❌ 다른 버전 비교 기능 포함 금지

**성능 저하**:
- ❌ 3D 형상(Geometry) 추출 금지
- ❌ 모든 View에 대한 렌더링 금지
- ❌ 동기식 UI 블로킹 (async/await 사용)

### 5.3. ⚠️ 사용자가 직접 제어해야 할 것

**BIM 엔지니어 책임**:
- ⚠️ ActivityId 등 공유 매개변수 값 입력
- ⚠️ 스냅샷 저장 타이밍 결정 (설계 변경 완료 시점)
- ⚠️ ModelVersion 네이밍 규칙 준수
- ⚠️ 오류 발생 시 로그 확인

---

## 6. 테스트 계획

### 6.1. 단위 테스트
- DataExtractor의 각 메서드 테스트
- IdGenerator의 해시 생성 테스트
- 매개변수 추출 로직 테스트

### 6.2. 통합 테스트
- 실제 Revit 모델로 end-to-end 테스트
- 다양한 모델 크기 (소형/중형/대형)
- 네트워크 장애 시나리오

### 6.3. 성능 테스트
- 10,000 객체 기준 < 30초
- 메모리 사용량 모니터링
- API 서버 응답 시간 측정

---

## 7. 배포 준비

### 7.1. 빌드 설정
- Release 모드로 빌드
- 출력 경로: `bin\Release\`
- 필수 파일:
  - DXrevit.dll
  - DXrevit.addin
  - DXBase.dll
  - SharedParameters.txt

### 7.2. 설치 스크립트
- 애드인 파일을 `%ProgramData%\Autodesk\Revit\Addins\2025\` 복사
- 설정 파일 초기화
- 사용자 가이드 제공

---

## 8. 다음 단계

DXrevit 구현 완료 후:
1. Phase 3 (PostgreSQL 스키마) 참조
2. Phase 4 (FastAPI 서버 개발) 참조
3. 통합 테스트 수행

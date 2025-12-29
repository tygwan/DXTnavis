# 기술 설계서: 3D Snapshot Workflow

## 문서 정보
| 항목 | 내용 |
|------|------|
| 버전 | v1.0 |
| 작성일 | 2025-12-29 |
| PRD 참조 | [3d-snapshot-workflow-prd.md](../prd/3d-snapshot-workflow-prd.md) |

---

## 1. 시스템 아키텍처

### 1.1 컴포넌트 다이어그램

```
┌─────────────────────────────────────────────────────────────────┐
│                        DXTnavis Plugin                          │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐  │
│  │   Views     │  │  ViewModels │  │       Services          │  │
│  │ (WPF XAML)  │◄─┤   (MVVM)    │◄─┤  ┌───────────────────┐  │  │
│  └─────────────┘  └─────────────┘  │  │ PropertyExtractor │  │  │
│                                     │  ├───────────────────┤  │  │
│                                     │  │ ObjectFilter      │  │  │
│                                     │  ├───────────────────┤  │  │
│                                     │  │ SnapshotCapture   │  │  │
│                                     │  ├───────────────────┤  │  │
│                                     │  │ MetadataExporter  │  │  │
│                                     │  └───────────────────┘  │  │
│                                     └─────────────────────────┘  │
├─────────────────────────────────────────────────────────────────┤
│                    Navisworks 2025 API                          │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐  │
│  │ ModelItem   │  │ Selection   │  │    Viewport/Camera      │  │
│  │ Properties  │  │ SearchSet   │  │    SaveRenderedImage    │  │
│  └─────────────┘  └─────────────┘  └─────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2 데이터 흐름

```
[Navisworks Model]
       │
       ▼
┌──────────────────┐
│ 1. CSV 생성      │ ──► all_properties.csv
│ PropertyExtractor│
└──────────────────┘
       │
       ▼
┌──────────────────┐
│ 2. 필터링        │ ──► FilteredObjectCollection
│ ObjectFilter     │
└──────────────────┘
       │
       ▼
┌──────────────────┐
│ 3. 리스트 관리   │ ──► filtered_objects.json
│ ListManager      │
└──────────────────┘
       │
       ▼
┌──────────────────┐
│ 4. 스냅샷 캡처   │ ──► snapshots/*.png
│ SnapshotCapture  │
└──────────────────┘
       │
       ▼
┌──────────────────┐
│ 5. 메타데이터    │ ──► metadata.json
│ MetadataExporter │
└──────────────────┘
```

---

## 2. 클래스 설계

### 2.1 새로운 서비스 클래스

#### ObjectFilterService.cs

```csharp
namespace DXTnavis.Services
{
    /// <summary>
    /// 조건부 필터링으로 3D 객체를 식별하는 서비스
    /// </summary>
    public class ObjectFilterService
    {
        /// <summary>
        /// 필터 조건 정의
        /// </summary>
        public class FilterCriteria
        {
            public bool RequireGeometry { get; set; } = true;
            public bool ExcludeHidden { get; set; } = true;
            public List<PropertyCondition> PropertyConditions { get; set; }
        }

        /// <summary>
        /// 속성 기반 필터 조건
        /// </summary>
        public class PropertyCondition
        {
            public string Category { get; set; }
            public string PropertyName { get; set; }
            public string Value { get; set; }
            public ConditionOperator Operator { get; set; }
        }

        public enum ConditionOperator
        {
            Equals,
            Contains,
            StartsWith,
            NotEquals
        }

        /// <summary>
        /// 모델에서 조건에 맞는 객체 필터링
        /// </summary>
        public List<FilteredObject> FilterObjects(
            Document doc,
            FilterCriteria criteria,
            IProgress<(int, string)> progress);
    }
}
```

#### SnapshotCaptureService.cs

```csharp
namespace DXTnavis.Services
{
    /// <summary>
    /// 3D 객체 스냅샷 캡처 서비스
    /// </summary>
    public class SnapshotCaptureService
    {
        /// <summary>
        /// 캡처 설정
        /// </summary>
        public class CaptureSettings
        {
            public int Width { get; set; } = 1920;
            public int Height { get; set; } = 1080;
            public ImageFormat Format { get; set; } = ImageFormat.Png;
            public Color BackgroundColor { get; set; } = Color.White;
            public bool IsolateObject { get; set; } = true;
            public List<ViewAngle> ViewAngles { get; set; }
        }

        public enum ViewAngle
        {
            Front,
            Back,
            Left,
            Right,
            Top,
            Isometric
        }

        /// <summary>
        /// 단일 객체 스냅샷 캡처
        /// </summary>
        public List<string> CaptureObjectSnapshot(
            ModelItem item,
            string outputFolder,
            CaptureSettings settings);

        /// <summary>
        /// 다중 객체 배치 캡처
        /// </summary>
        public async Task BatchCaptureAsync(
            List<ModelItem> items,
            string outputFolder,
            CaptureSettings settings,
            IProgress<(int, string)> progress,
            CancellationToken cancellationToken);
    }
}
```

#### MetadataExporterService.cs

```csharp
namespace DXTnavis.Services
{
    /// <summary>
    /// 계층화된 메타데이터 내보내기 서비스
    /// </summary>
    public class MetadataExporterService
    {
        /// <summary>
        /// 통합 메타데이터 구조
        /// </summary>
        public class ExportMetadata
        {
            public ExportInfo ExportInfo { get; set; }
            public List<ObjectMetadata> Objects { get; set; }
        }

        public class ExportInfo
        {
            public DateTime Timestamp { get; set; }
            public string ModelPath { get; set; }
            public int TotalObjects { get; set; }
            public int FilteredObjects { get; set; }
        }

        public class ObjectMetadata
        {
            public Guid ObjectId { get; set; }
            public Guid ParentId { get; set; }
            public int Level { get; set; }
            public string DisplayName { get; set; }
            public bool HasGeometry { get; set; }
            public Dictionary<string, string> Properties { get; set; }
            public List<string> Snapshots { get; set; }
        }

        /// <summary>
        /// 전체 워크플로우 결과를 메타데이터로 내보내기
        /// </summary>
        public void ExportMetadata(
            string outputPath,
            List<ObjectMetadata> objects,
            ExportInfo info);
    }
}
```

### 2.2 ViewModel 확장

#### SnapshotWorkflowViewModel.cs

```csharp
namespace DXTnavis.ViewModels
{
    public class SnapshotWorkflowViewModel : INotifyPropertyChanged
    {
        // Phase 상태
        public WorkflowPhase CurrentPhase { get; set; }

        // Phase 1: CSV
        public bool IsCsvExporting { get; set; }
        public int CsvProgress { get; set; }
        public string CsvOutputPath { get; set; }

        // Phase 2: Filter
        public FilterCriteria CurrentFilter { get; set; }
        public int FilteredObjectCount { get; set; }
        public ObservableCollection<FilteredObject> FilteredObjects { get; set; }

        // Phase 3: List
        public ObservableCollection<FilteredObject> SelectedObjects { get; set; }

        // Phase 4: Snapshot
        public CaptureSettings CaptureSettings { get; set; }
        public int CaptureProgress { get; set; }
        public int CapturedCount { get; set; }

        // Commands
        public ICommand ExportCsvCommand { get; }
        public ICommand ApplyFilterCommand { get; }
        public ICommand CaptureSnapshotsCommand { get; }
        public ICommand ExportMetadataCommand { get; }
        public ICommand CancelCommand { get; }
    }

    public enum WorkflowPhase
    {
        Ready,
        ExportingCsv,
        Filtering,
        CapturingSnapshots,
        ExportingMetadata,
        Complete
    }
}
```

---

## 3. API 사용 패턴

### 3.1 Navisworks 뷰포트 조작

```csharp
// 객체로 카메라 이동
public void ZoomToObject(ModelItem item)
{
    var doc = Application.ActiveDocument;
    var view = doc.ActiveView;

    // 선택 후 줌
    doc.CurrentSelection.Clear();
    doc.CurrentSelection.Add(item);
    view.ZoomToSelection();
}

// 객체 격리
public void IsolateObject(ModelItem item)
{
    var doc = Application.ActiveDocument;

    // 모든 객체 숨기기
    doc.Models.SetHidden(doc.Models.RootItems.DescendantsAndSelf, true);

    // 선택한 객체만 표시
    doc.Models.SetHidden(new ModelItemCollection { item }, false);
}

// 격리 해제
public void ResetIsolation()
{
    var doc = Application.ActiveDocument;
    doc.Models.SetHidden(doc.Models.RootItems.DescendantsAndSelf, false);
}
```

### 3.2 화면 캡처 (Navisworks API)

```csharp
public void CaptureViewport(string outputPath, int width, int height)
{
    var doc = Application.ActiveDocument;
    var view = doc.ActiveView;

    // 방법 1: SaveRenderedImage (권장)
    view.SaveRenderedImage(
        outputPath,
        width,
        height,
        ImageFileType.Png);

    // 방법 2: 대안 - COM API 사용
    // var comApi = ComApiBridge.State;
    // comApi.SaveImage(outputPath, width, height);
}
```

### 3.3 스레드 안전성 보장

```csharp
// UI 스레드에서 Navisworks API 호출
private void ExecuteOnUIThread(Action action)
{
    if (Application.Current.Dispatcher.CheckAccess())
    {
        action();
    }
    else
    {
        Application.Current.Dispatcher.Invoke(action);
    }
}

// 배치 캡처 패턴
public async Task BatchCaptureAsync(List<ModelItem> items)
{
    foreach (var item in items)
    {
        // UI 스레드에서 캡처 실행
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            ZoomToObject(item);
            IsolateObject(item);
            CaptureViewport(GetOutputPath(item), 1920, 1080);
            ResetIsolation();
        });

        // 진행률 업데이트
        UpdateProgress(items.IndexOf(item) + 1, items.Count);
    }
}
```

---

## 4. 파일 구조

### 4.1 새로 추가할 파일

```
dxtnavis/
├── Models/
│   ├── FilterCriteria.cs          # 필터 조건 모델
│   ├── FilteredObject.cs          # 필터링된 객체 모델
│   ├── CaptureSettings.cs         # 캡처 설정 모델
│   └── ExportMetadata.cs          # 메타데이터 모델
├── Services/
│   ├── ObjectFilterService.cs     # 객체 필터링 서비스
│   ├── SnapshotCaptureService.cs  # 스냅샷 캡처 서비스
│   └── MetadataExporterService.cs # 메타데이터 내보내기
├── ViewModels/
│   └── SnapshotWorkflowViewModel.cs  # 워크플로우 ViewModel
└── Views/
    └── SnapshotWorkflowTab.xaml   # 워크플로우 탭 UI
```

### 4.2 출력 폴더 구조

```
{UserSelectedFolder}/
├── all_properties.csv             # Phase 1
├── filtered_objects.json          # Phase 2-3
├── snapshots/                     # Phase 4
│   ├── {ObjectId}_front.png
│   ├── {ObjectId}_iso.png
│   └── ...
└── metadata.json                  # 통합 메타데이터
```

---

## 5. 에러 처리

### 5.1 예외 처리 전략

| 예외 유형 | 처리 방법 |
|----------|----------|
| AccessViolationException | Skip & Log |
| OutOfMemoryException | 배치 크기 축소 후 재시도 |
| IOException | 사용자 알림 후 경로 변경 요청 |
| OperationCanceledException | 정상 종료 처리 |

### 5.2 복구 메커니즘

```csharp
public class WorkflowRecovery
{
    // 진행 상태 저장
    public void SaveCheckpoint(WorkflowState state);

    // 체크포인트에서 복구
    public WorkflowState LoadCheckpoint();

    // 실패한 항목 재시도
    public void RetryFailedItems(List<Guid> failedIds);
}
```

---

## 6. 성능 최적화

### 6.1 메모리 관리
- 스트리밍 CSV 작성 (StreamWriter)
- 이미지 즉시 저장 후 메모리 해제
- 배치 크기 동적 조절

### 6.2 병렬 처리 주의사항
- Navisworks API는 UI 스레드 필수
- 파일 I/O는 백그라운드 스레드 가능
- 이미지 후처리는 병렬 가능

---

## 7. 테스트 계획

| 테스트 유형 | 대상 | 기준 |
|------------|------|------|
| 단위 테스트 | FilterService | 필터 정확도 100% |
| 통합 테스트 | 전체 워크플로우 | 정상 완료 |
| 성능 테스트 | 1,000객체 처리 | < 5분 |
| 스트레스 테스트 | 10,000객체 | 크래시 없음 |

---

## 8. 참조

- [PRD](../prd/3d-snapshot-workflow-prd.md)
- [Navisworks API Guide](https://help.autodesk.com/view/NAV/2025/ENU/)
- [CLAUDE.md](../../CLAUDE.md)

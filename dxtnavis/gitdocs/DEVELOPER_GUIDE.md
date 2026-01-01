# DXnavis 개발자 가이드

## 📘 목차
1. [개발 환경 설정](#개발-환경-설정)
2. [프로젝트 구조](#프로젝트-구조)
3. [아키텍처 및 설계 패턴](#아키텍처-및-설계-패턴)
4. [주요 컴포넌트](#주요-컴포넌트)
5. [개발 가이드라인](#개발-가이드라인)
6. [디버깅 및 테스트](#디버깅-및-테스트)
7. [배포 프로세스](#배포-프로세스)
8. [향후 개발 계획](#향후-개발-계획)

---

## 🛠️ 개발 환경 설정

### 필수 소프트웨어

| 소프트웨어 | 버전 | 용도 |
|-----------|------|------|
| Visual Studio | 2022 이상 | IDE |
| .NET Framework | 4.8 | 런타임 |
| Navisworks Manage | 2025 | 테스트 환경 |
| Git | 최신 | 버전 관리 |

### Visual Studio 설정

#### 1. 필수 워크로드 설치
- **.NET 데스크톱 개발**
- **Windows Presentation Foundation (WPF)**

#### 2. NuGet 패키지 복원
```bash
nuget restore DXnavis.sln
```

#### 3. 참조 DLL 경로 확인
프로젝트 속성에서 Navisworks API DLL 경로 확인:
```
C:\Program Files\Autodesk\Navisworks Manage 2025\
```

필요한 DLL:
- `Autodesk.Navisworks.Api.dll`
- `Autodesk.Navisworks.Automation.dll`
- `Autodesk.Navisworks.Controls.dll`
- `Autodesk.Navisworks.Interop.ComApi.dll`
- 기타 (csproj 참조)

#### 4. 빌드 후 이벤트 설정
프로젝트 속성 → 빌드 이벤트:
```bash
xcopy /Y /I "$(TargetDir)*.*" "C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins\$(TargetName)\"
```

---

## 📂 프로젝트 구조

### 폴더 구조
```
DXnavis/
├── Models/                         # 데이터 모델
│   ├── HierarchicalPropertyRecord.cs
│   ├── TreeNodeModel.cs
│   └── PropertyInfo.cs
├── ViewModels/                     # MVVM ViewModel
│   ├── DXwindowViewModel.cs
│   ├── HierarchyNodeViewModel.cs
│   └── PropertyItemViewModel.cs
├── Views/                          # XAML UI
│   ├── DXwindow.xaml
│   └── DXwindow.xaml.cs
├── Services/                       # 비즈니스 로직
│   ├── NavisworksDataExtractor.cs
│   ├── HierarchyFileWriter.cs
│   ├── SetCreationService.cs
│   ├── FullModelExporterService.cs
│   └── PropertyFileWriter.cs
├── Helpers/                        # 유틸리티
│   └── RelayCommand.cs
├── Converters/                     # XAML 컨버터
│   └── BoolToVisibilityConverter.cs
├── Resources/                      # 리소스 파일
│   ├── icon_16x16.png
│   └── icon_32x32.png
├── Properties/                     # 어셈블리 정보
│   └── AssemblyInfo.cs
├── LOG-prd/                        # 개발 요구사항
│   └── prdv1.md ~ prdv8.md
├── LOG-error/                      # 오류 해결 로그
│   └── error.md ~ error8.md
└── gitdocs/                        # 문서
    ├── GIT_COMMIT_GUIDELINES.md
    ├── USER_GUIDE.md
    └── DEVELOPER_GUIDE.md
```

---

## 🏗️ 아키텍처 및 설계 패턴

### MVVM 패턴

#### 개요
**Model-View-ViewModel** 패턴을 사용하여 UI와 비즈니스 로직을 분리합니다.

```
View (XAML)
    ↕ DataBinding
ViewModel (ObservableObject)
    ↕ Data Access
Model (POCO)
```

#### 구성 요소

**Model**:
- 순수한 데이터 구조 (POCO)
- 비즈니스 로직 없음
- 예: `PropertyInfo`, `TreeNodeModel`

**ViewModel**:
- `INotifyPropertyChanged` 구현
- `ICommand` 바인딩
- 예: `DXwindowViewModel`, `HierarchyNodeViewModel`

**View**:
- XAML UI
- Code-behind는 최소화
- 예: `DXwindow.xaml`

### 의존성 주입 (DI)

현재는 직접 인스턴스화를 사용하지만, 향후 DI 컨테이너 도입 예정:
```csharp
// 현재 방식
var extractor = new NavisworksDataExtractor();

// 향후 계획
var extractor = serviceProvider.GetService<INavisworksDataExtractor>();
```

### 비동기 프로그래밍

#### async/await 패턴
긴 작업은 비동기로 처리하여 UI 반응성 유지:
```csharp
public async Task LoadModelHierarchyAsync()
{
    try
    {
        // UI 스레드에서 Navisworks API 호출
        var hierarchy = ExtractHierarchyOnUIThread();

        // ViewModel 업데이트
        HierarchyNodes = hierarchy;
    }
    catch (Exception ex)
    {
        // 오류 처리
    }
}
```

**⚠️ 중요**: Navisworks API는 **반드시 UI 스레드**에서만 호출해야 합니다.

---

## 🔧 주요 컴포넌트

### 1. DXwindowViewModel

**역할**: 메인 창의 ViewModel

**주요 속성**:
- `HierarchyNodes`: TreeView 계층 구조
- `Properties`: 속성 목록
- `SelectedItems`: 선택된 속성
- `StatusMessage`: 상태 메시지
- `ProgressValue`: 진행률

**주요 메서드**:
```csharp
// 계층 구조 로드
public async Task LoadModelHierarchyAsync()

// 선택 항목 내보내기
private async Task ExportSelectionHierarchyAsync()

// 검색 세트 생성
private async Task CreateSearchSetAsync()

// Navisworks 선택 변경 이벤트
private void OnSelectionChanged(object sender, EventArgs e)
```

**Debouncing 패턴**:
빠른 연속 선택 시 마지막 선택만 처리:
```csharp
private CancellationTokenSource _debounceCts;

private void OnSelectionChanged(object sender, EventArgs e)
{
    _debounceCts?.Cancel();
    _debounceCts = new CancellationTokenSource();

    Task.Delay(300, _debounceCts.Token)
        .ContinueWith(t => LoadPropertiesForSelection(),
                      TaskScheduler.FromCurrentSynchronizationContext());
}
```

---

### 2. NavisworksDataExtractor

**역할**: Navisworks 모델 데이터 추출 서비스

**핵심 기능**:
- 재귀적 계층 구조 순회
- 속성 데이터 추출
- ModelItem → TreeNodeModel 변환

**주요 메서드**:
```csharp
// 재귀적 계층 구조 추출
public List<TreeNodeModel> ExtractHierarchy(
    ModelItem parent,
    IProgress<string> progress = null)

// 속성 추출
public List<HierarchicalPropertyRecord> ExtractPropertiesWithHierarchy(
    ModelItemCollection items)

// 전체 모델 속성 추출
public async Task<List<HierarchicalPropertyRecord>> ExtractAllPropertiesAsync(
    IProgress<(int current, int total, string message)> progress)
```

**재귀 알고리즘**:
```csharp
private void ExtractHierarchyRecursive(
    ModelItem current,
    TreeNodeModel parentNode,
    IProgress<string> progress)
{
    // 현재 노드 생성
    var node = new TreeNodeModel
    {
        DisplayName = current.DisplayName,
        InternalId = current.InstanceGuid
    };

    parentNode.Children.Add(node);

    // 자식 노드 재귀 호출
    foreach (var child in current.Children)
    {
        ExtractHierarchyRecursive(child, node, progress);
    }
}
```

---

### 3. HierarchyFileWriter

**역할**: 계층 구조 데이터를 파일로 저장

**지원 형식**:
- CSV (Flat): 평면화된 CSV
- JSON (Flat): 배열 형태 JSON
- JSON (Tree): 재귀 구조 JSON

**주요 메서드**:
```csharp
// CSV 저장
public void SaveHierarchyToCsv(
    List<HierarchicalPropertyRecord> records,
    string filePath)

// JSON (Flat) 저장
public void SaveHierarchyToJsonFlat(
    List<HierarchicalPropertyRecord> records,
    string filePath)

// JSON (Tree) 저장
public void SaveHierarchyToJsonTree(
    List<TreeNodeModel> treeNodes,
    string filePath)
```

**JSON Tree 직렬화**:
```csharp
private void WriteTreeNodeRecursive(
    StreamWriter writer,
    TreeNodeModel node,
    int depth)
{
    // 재귀적으로 자식 노드 직렬화
    if (node.Children.Count > 0)
    {
        foreach (var child in node.Children)
        {
            WriteTreeNodeRecursive(writer, child, depth + 1);
        }
    }
}
```

---

### 4. SetCreationService

**역할**: Navisworks 검색 세트 생성

**주요 메서드**:
```csharp
public void CreateSearchSets(
    List<PropertyInfo> properties,
    string folderName = "")
```

**검색 세트 생성 로직**:
```csharp
// 폴더 생성 또는 가져오기
var folder = GetOrCreateFolder(folderName);

foreach (var prop in properties)
{
    // 검색 조건 생성
    var search = new Search();
    search.SearchConditions.Add(SearchCondition.HasPropertyByDisplayName(
        prop.Category, prop.Name).EqualValue(VariantData.FromDisplayString(prop.Value)));

    // 검색 세트 생성
    var savedItem = new SelectionSet(search);
    savedItem.DisplayName = $"{prop.Name} = {prop.Value}";

    folder.AddCopy(savedItem);
}
```

---

### 5. RelayCommand

**역할**: ICommand 구현체

**사용 예시**:
```csharp
public ICommand LoadHierarchyCommand { get; }

public DXwindowViewModel()
{
    LoadHierarchyCommand = new RelayCommand(
        execute: async _ => await LoadModelHierarchyAsync(),
        canExecute: _ => !IsLoading
    );
}
```

**동적 CanExecute 업데이트**:
```csharp
private bool _isLoading;
public bool IsLoading
{
    get => _isLoading;
    set
    {
        _isLoading = value;
        OnPropertyChanged();
        (LoadHierarchyCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
```

---

## 📐 개발 가이드라인

### 코딩 스타일

#### C# 네이밍 규칙
- **PascalCase**: 클래스, 메서드, 속성, 이벤트
- **camelCase**: 지역 변수, 파라미터
- **_camelCase**: private 필드
- **UPPER_CASE**: 상수

#### 예시
```csharp
public class NavisworksDataExtractor
{
    private const int MAX_DEPTH = 100;
    private readonly IProgress<string> _progress;

    public List<TreeNodeModel> ExtractHierarchy(ModelItem parent)
    {
        var result = new List<TreeNodeModel>();
        return result;
    }
}
```

### 주석 작성

#### XML 문서 주석
공개 API는 반드시 XML 문서 주석 작성:
```csharp
/// <summary>
/// Navisworks 모델의 계층 구조를 재귀적으로 추출합니다.
/// </summary>
/// <param name="parent">추출할 루트 ModelItem</param>
/// <param name="progress">진행률 보고 객체 (선택)</param>
/// <returns>TreeNodeModel 리스트</returns>
public List<TreeNodeModel> ExtractHierarchy(
    ModelItem parent,
    IProgress<string> progress = null)
{
    // 구현
}
```

#### 인라인 주석
복잡한 로직에만 주석 추가:
```csharp
// AccessViolationException 방지: UI 스레드에서만 API 호출
Application.Current.Dispatcher.Invoke(() =>
{
    var item = document.CurrentSelection.First();
    // ...
});
```

### 예외 처리

#### 다층 예외 처리
Navisworks API는 AccessViolationException이 발생할 수 있으므로 다층 try-catch 사용:

```csharp
try
{
    try
    {
        // Navisworks API 호출
        var value = dataProperty.Value.ToDisplayString();
    }
    catch (AccessViolationException ave)
    {
        // API 내부 오류 - 무시하고 계속 진행
        Debug.WriteLine($"AccessViolationException: {ave.Message}");
    }
}
catch (Exception ex)
{
    // 기타 오류 - 로깅 및 사용자 알림
    MessageBox.Show($"오류 발생: {ex.Message}");
}
```

### UI 스레드 보호

**⚠️ 중요**: Navisworks API는 UI 스레드에서만 호출 가능

**올바른 방법**:
```csharp
// UI 스레드에서 직접 호출
public void LoadProperties()
{
    var doc = Autodesk.Navisworks.Api.Application.ActiveDocument;
    var items = doc.CurrentSelection.SelectedItems;
    // ...
}
```

**잘못된 방법** ❌:
```csharp
// Task.Run 내부에서 API 호출 - AccessViolationException 발생!
await Task.Run(() =>
{
    var doc = Autodesk.Navisworks.Api.Application.ActiveDocument; // ❌
});
```

**비동기 처리가 필요할 때**:
```csharp
public async Task LoadPropertiesAsync()
{
    // UI 스레드에서 데이터 추출
    var data = ExtractDataFromNavisworks();

    // 비동기 작업은 비-API 작업만
    await Task.Run(() =>
    {
        ProcessData(data); // Navisworks API 사용 안 함
    });
}
```

---

## 🐛 디버깅 및 테스트

### 디버깅 설정

#### Visual Studio 디버거 연결
1. Navisworks 실행
2. Visual Studio → 디버그 → 프로세스에 연결
3. `Roamer.exe` 선택
4. 플러그인 실행하면 브레이크포인트 작동

#### 빠른 디버깅 워크플로우
1. 빌드 후 이벤트로 자동 배포
2. Navisworks 재시작
3. 플러그인 실행
4. 디버거 연결

### 로깅

#### Debug 출력
```csharp
using System.Diagnostics;

Debug.WriteLine($"Processing item: {item.DisplayName}");
```

#### 파일 로깅
```csharp
private void LogToFile(string message)
{
    File.AppendAllText(
        @"C:\Temp\DXnavis_log.txt",
        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n");
}
```

### 테스트

#### 단위 테스트 (계획)
현재는 수동 테스트만 수행하지만, 향후 단위 테스트 도입 예정:
```csharp
[TestClass]
public class NavisworksDataExtractorTests
{
    [TestMethod]
    public void ExtractHierarchy_ShouldReturnTreeNodes()
    {
        // Arrange
        var extractor = new NavisworksDataExtractor();

        // Act
        var result = extractor.ExtractHierarchy(mockModelItem);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Count > 0);
    }
}
```

#### 통합 테스트
Navisworks 환경에서 수동 테스트:
1. 샘플 모델 로드
2. 각 기능 수동 실행
3. 결과 검증
4. 오류 로그 확인

---

## 📦 배포 프로세스

### 빌드

#### Debug 빌드
```bash
MSBuild.exe DXnavis.csproj -p:Configuration=Debug -p:Platform=AnyCPU
```

#### Release 빌드
```bash
MSBuild.exe DXnavis.csproj -p:Configuration=Release -p:Platform=AnyCPU
```

### 배포

#### 1. 빌드 산출물 확인
```
bin\Release\
├── DXnavis.dll
├── DXnavis.pdb (디버그 심볼, 선택)
└── Newtonsoft.Json.dll (의존성)
```

#### 2. 플러그인 폴더 배포
```
C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins\DXnavis\
├── DXnavis.dll
└── Newtonsoft.Json.dll
```

#### 3. 설치 스크립트 (배치 파일)
```batch
@echo off
set SOURCE=bin\Release
set TARGET=C:\Program Files\Autodesk\Navisworks Manage 2025\Plugins\DXnavis

if not exist "%TARGET%" mkdir "%TARGET%"
xcopy /Y /I "%SOURCE%\*.dll" "%TARGET%\"

echo Deployment completed!
pause
```

### 버전 관리

#### AssemblyInfo.cs 업데이트
```csharp
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]
```

#### Git 태그 생성
```bash
git tag -a v2.0.0 -m "Release v2: TreeView 및 검색 세트 기능 추가"
git push origin v2.0.0
```

---

## 🚀 향후 개발 계획

### v3 계획 기능 (PRD v8)

#### 1. 속성 값 편집 기능
- UI에서 속성 값 직접 수정
- Navisworks 모델에 반영
- Undo/Redo 지원

#### 2. 다중 조건 검색 세트
- AND/OR 논리 연산자 지원
- 복잡한 검색 조건 조합

#### 3. 속성 일괄 수정
- 여러 객체의 속성 동시 수정
- CSV 임포트로 일괄 업데이트

#### 4. 모델 비교 기능
- 두 버전의 모델 비교
- 변경 사항 하이라이트

#### 5. 성능 최적화
- TreeView 가상화 (VirtualizingStackPanel)
- 페이지네이션
- 캐싱 전략

### 기술 부채 해결

#### 1. 의존성 주입 도입
- DI 컨테이너 (Microsoft.Extensions.DependencyInjection)
- 인터페이스 기반 설계

#### 2. 단위 테스트 추가
- MSTest 또는 xUnit
- Mock 라이브러리 (Moq)

#### 3. 로깅 프레임워크
- Serilog 도입
- 구조화된 로깅

---

## 📚 참고 자료

### Navisworks API
- [공식 문서](https://www.autodesk.com/developer-network/platform-technologies/navisworks)
- [API Reference](https://help.autodesk.com/view/NAV/2025/ENU/?guid=GUID-API-Reference)

### WPF
- [Microsoft Docs](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/)
- [MVVM Pattern](https://docs.microsoft.com/en-us/archive/msdn-magazine/2009/february/patterns-wpf-apps-with-the-model-view-viewmodel-design-pattern)

### C# 개발
- [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [Async/Await Best Practices](https://docs.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)

---

## 🤝 기여 가이드

### 기여 방법
1. 이슈 생성 또는 확인
2. 기능 브랜치 생성 (`feature/your-feature`)
3. 코드 작성 및 테스트
4. 커밋 (커밋 규칙 준수)
5. Pull Request 생성

### 코드 리뷰 체크리스트
- [ ] 코딩 스타일 준수
- [ ] XML 문서 주석 작성
- [ ] 예외 처리 적절
- [ ] UI 스레드 보호
- [ ] 빌드 경고 없음
- [ ] 수동 테스트 완료
- [ ] 문서 업데이트

---

## 📞 개발자 연락처

- **개발자**: Yoon taegwan
- **이메일**: [your-email@example.com]
- **GitHub**: [repository-url]

---

**마지막 업데이트**: 2025-01-13
**문서 버전**: v1.0
**프로젝트 버전**: v2.0

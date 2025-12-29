구현 명세 (Implementation Specification)

1. 플러그인 기본 구조 및 정의

1.1. 기반 클래스 상속 (Base Class Inheritance):

애드인의 주 진입점이 될 DX.cs를 지정하고, Autodesk.Navisworks.Api.Plugins.AddInPlugin 클래스를 상속받도록 구현합니다.

1.2. 플러그인 속성 정의 (Plugin Attribute Definition):

MainPlugin 클래스 선언부 위에 다음 두 가지 어트리뷰트를 명확하게 정의합니다.

[Plugin(...)]: PluginId (예: "YourNamespace.MainPlugin"), DeveloperId (예: "YourCompany"), DisplayName (버튼 텍스트, 예: "속성 확인기"), ToolTip (마우스오버 설명)을 포함해야 합니다.

[AddInPlugin(AddInLocation.Ribbon, ...)]: 플러그인을 리본 메뉴에 배치하도록 설정합니다. Tab (예: "My Tools"), Panel (예: "Inspectors"), LargeImage, SmallImage 파라미터를 포함해야 합니다.

plugin, strings, ribbonlayout, ribbontab(displayname), command(icon, displayname)

1.3. 아이콘 및 이미지 준비 (Icon and Image Preparation):

[AddInPlugin] 어트리뷰트에 명시된 아이콘 파일(예: icon_16x16.png, icon_32x32.png)을 프로젝트에 추가합니다.
![alt text](icon_16x16.png)
![alt text](icon_32x32.png)

추가된 아이콘 파일의 속성에서 빌드 작업(Build Action)을 포함 리소스(Embedded Resource)로 설정하여 DLL에 포함되도록 지시합니다.

2. WPF 사용자 인터페이스 (UI)

2.1. XAML 파일 생성 (XAML File Creation):

프로젝트에 DXwindow.xaml이라는 이름의 새로운 "창(WPF)" 항목을 추가합니다.

2.2. XAML 레이아웃 정의 및 구성 (XAML Layout Definition):

DXwindow.xaml 내부에, 속성을 표 형태로 표시하기 위해 ListView 컨트롤을 배치하고, 그 안에 GridView를 구성합니다.

GridView는 "카테고리", "속성 이름", "값" 이라는 세 개의 GridViewColumn을 가져야 합니다.

각 컬럼의 DisplayMemberBinding은 나중에 ViewModel의 속성에 바인딩될 것임을 명시합니다 (예: {Binding Category}).

3. 플러그인 로직 및 UI 연결

3.1. 플러그인-XAML 연결 (Plugin-XAML Connection):

MainPlugin 클래스의 Execute 메서드 내에서, DXwindow.xaml 창의 인스턴스를 생성하고 Show() 또는 ShowDialog() 메서드를 호출하여 화면에 표시하는 코드를 구현합니다.

3.2. 실행 로직 구현 (Execution Logic Implementation):

Execute 메서드에서 창의 인스턴스를 관리하는 로직을 포함합니다. 창이 이미 열려있으면 새로 생성하지 않고 기존 창을 활성화(Focus())하는 싱글톤 패턴을 적용하여 안정성을 높입니다.

4. Navisworks 데이터 상호작용

4.1. 현재 선택 항목 접근 (Accessing Current Selection):

모든 Navisworks 데이터 접근은 Autodesk.Navisworks.Api.Application.ActiveDocument를 통해 시작됨을 명시합니다.

사용자의 현재 선택은 ActiveDocument.CurrentSelection.SelectedItems를 통해 ModelItemCollection으로 가져옵니다.

4.2. 선택 변경 감지 (Selection Change Detection):

실시간 상호작용을 위해 ActiveDocument.CurrentSelection.Changed 이벤트를 구독(Subscribe)하는 로직을 구현합니다.

중요: 메모리 누수를 방지하기 위해, WPF 창이 닫힐 때(OnClosing 이벤트) 해당 이벤트를 반드시 구독 취소(Unsubscribe)하는 Cleanup 로직을 포함해야 합니다.

4.3. 속성 계층 구조 탐색 (Property Hierarchy Navigation):

선택된 ModelItem 객체로부터 속성을 추출하기 위해, item.PropertyCategories를 순회하는 첫 번째 foreach 루프와, 그 안에서 각 category.Properties를 순회하는 두 번째 중첩 foreach 루프를 구현합니다.

4.4. 데이터 형식 변환 및 처리 (Data Type Conversion and Processing):

DataProperty.Value는 VariantData 타입이므로, UI에 표시하기 위해 .ToString() 메서드를 사용하여 모든 값을 문자열로 변환하는 표준 처리 방식을 적용합니다.

4.5. UI 업데이트 (UI Update):

WPF의 데이터 바인딩 시스템이 변경 사항을 자동으로 감지하도록, 추출된 속성 목록은 **ObservableCollection<T>**에 저장해야 합니다.

Navisworks API 이벤트는 메인 스레드에서 발생하지만, UI 업데이트의 스레드 안전성을 보장하기 위해 모든 ObservableCollection 변경 작업은 Application.Current.Dispatcher.Invoke(...) 래퍼(Wrapper) 안에서 수행되어야 합니다.


4.6. 속성 데이터 내보내기 기능 구현

본 절에서는 사용자가 Navisworks 모델의 속성 데이터를 파일로 저장할 수 있는 내보내기(Export) 기능을 구현하는 절차를 기술한다. 기능은 두 단계로 나누어, **4.6.1. 현재 UI에 표시된 속성 저장** 기능과 **4.6.2. 모델 전체 속성 저장** 기능으로 확장한다.


#### **4.6.1. 현재 UI에 표시된 속성 저장 기능 구현**

*   **목표**: 사용자가 실시간 속성 확인기에 표시된 특정 객체의 속성 목록을 즉시 파일(CSV, JSON)로 저장할 수 있도록 한다.



##### **4.6.1.1. View(UI) 확장 (`DXwindow.xaml`)**



1.  **컨트롤 추가**: `ListView` 컨트롤 하단에 수평 `StackPanel`을 추가한다.

2.  **버튼 생성**: `StackPanel` 내부에 "CSV로 저장"과 "JSON으로 저장"이라는 `Content`를 가진 `Button` 두 개를 배치한다.

3.  **Command 바인딩**: 각 버튼의 `Command` 속성을 ViewModel에 정의될 `SaveAsCsvCommand`와 `SaveAsJsonCommand`에 각각 바인딩한다.

    ```xml
    <!-- DXwindow.xaml -->
    <StackPanel Orientation="Horizontal" HorizontalAlignment="Right" Margin="0,10,0,0">
        <Button Content="CSV로 저장" Command="{Binding SaveAsCsvCommand}" Margin="5"/>
        <Button Content="JSON으로 저장" Command="{Binding SaveAsJsonCommand}" Margin="5"/>
    </StackPanel>

    ```
##### **4.6.1.2. ViewModel 확장 (`DXwindowModel.cs`)**

1.  **Command 정의**: `ICommand` 타입의 `SaveAsCsvCommand`와 `SaveAsJsonCommand` 속성을 선언한다.

2.  **Command 초기화**: 생성자에서 `new RelayCommand`를 사용하여 Command를 초기화한다.

    *   `execute` 파라미터에는 파일 저장 로직을 호출하는 람다식을 전달한다.

    *   `canExecute` 파라미터에는 `SelectedObjectProperties.Count > 0` 조건을 전달하여, 표시된 속성이 있을 때만 버튼이 활성화되도록 한다.

3.  **파일 저장 로직 구현**: `private async void SaveToFile(FileType fileType)` 메서드를 구현한다.

    *   `Microsoft.Win32.SaveFileDialog`를 사용하여 사용자로부터 저장 경로를 입력받는다.

    *   사용자가 경로를 확정하면, `SelectedObjectProperties` 컬렉션의 현재 상태를 새 `List`로 복사하여 스레드 안전성을 확보한다.

    *   별도의 서비스 클래스(`PropertyFileWriter`)의 비동기 저장 메서드를 `await`로 호출한다.
    *   `try-catch` 블록으로 파일 저장 중 발생할 수 있는 예외를 처리하고 사용자에게 알린다.
##### **4.6.1.3. 파일 쓰기 서비스 구현 (`Services/PropertyFileWriter.cs`)**
1.  **클래스 및 메서드 생성**: `PropertyFileWriter` 클래스와 `public async Task WriteFileAsync(...)` 메서드를 생성한다.

2.  **로직 구현**:

    *   **CSV 저장**: `Sylvan.Data.Csv.CsvWriter`를 사용하여 전달받은 속성 리스트를 CSV 형식으로 비동기적으로 저장한다.

    *   **JSON 저장**: `System.Text.Json.JsonSerializer.SerializeAsync`를 사용하여 리스트를 사람이 읽기 좋은 형태(Indented)의 JSON으로 비동기적으로 저장한다.
---
#### **4.6.2. 모델 전체 속성 비동기 저장 기능 구현**
*   **목표**: 모델에 존재하는 모든 유효 객체의 모든 속성을 대용량 처리에도 안정적인 비동기 스트리밍 방식으로 파일에 저장한다.
##### **4.6.2.1. View(UI) 확장 (`MainView.xaml`)**
1.  **UI 섹션 분리**: 기존 UI 하단에 `Separator`와 "전체 모델 내보내기"라는 `GroupBox`를 추가한다.
2.  **컨트롤 추가**: `GroupBox` 내부에 다음 컨트롤들을 배치하고 ViewModel의 해당 속성에 바인딩한다.
    *   `Button`: "전체 속성 CSV로 저장", `Command="{Binding ExportAllToCsvCommand}"`
    *   `ProgressBar`: `Value="{Binding ExportProgressPercentage}"`
    *   `TextBlock`: `Text="{Binding ExportStatusMessage}"`
##### **4.6.2.2. ViewModel 확장 (`MainViewModel.cs`)**
1.  **속성 추가**: UI 피드백을 위한 `ExportProgressPercentage` (int)와 `ExportStatusMessage` (string) 속성을 **수동 MVVM 방식(전체 속성 + `SetProperty`)**으로 추가한다.
2.  **Command 추가**: `public IAsyncRelayCommand ExportAllToCsvCommand { get; }`를 선언하고, 생성자에서 `new AsyncRelayCommand(ExportAllToCsvAsync)`로 초기화한다.
3.  **Command 실행 로직 구현 (`ExportAllToCsvAsync`)**:
    *   이 메서드는 전체 프로세스를 조율하는 **오케스트레이터** 역할을 한다.
    *   `SaveFileDialog`로 저장 경로를 받는다.
    *   UI 상태를 초기화하고, 진행률 보고를 위한 `Progress<T>` 인스턴스를 생성한다.
    *   핵심 로직(파일 쓰기)은 `Task.Run`을 사용하여 **백그라운드 스레드에서 실행**하도록 위임한다.
    *   `Task.Run` 내에서 `FullModelExporterService`의 비동기 메서드를 호출한다.
    *   `try-catch-finally` 구문을 사용하여 작업의 성공, 실패, 완료 상태를 `ExportStatusMessage`를 통해 사용자에게 명확히 전달한다.

##### **4.6.2.3. 전체 내보내기 서비스 구현 (`Services/FullModelExporterService.cs`)**
1.  **클래스 및 DTO 생성**: `FullModelExporterService` 클래스와 파일에 쓸 한 줄의 데이터 구조를 정의하는 `ExportedProperty` 레코드(`record`)를 생성한다.
2.  **비동기 스트리밍 메서드 구현 (`ExportAllPropertiesToCsvAsync`)**:
    *   이 메서드는 **메모리 효율성**을 극대화하는 것을 최우선으로 설계한다.
    *   **데이터 로드 방지**: `DescendantsAndSelf`로 모든 객체를 가져오되, 전체 속성을 메모리에 리스트 형태로 저장하지 않는다.
    *   **스트리밍 I/O**: `File.CreateText`와 `Sylvan.Data.Csv.CsvWriter`를 `using` 구문으로 열어 파일 스트림을 준비한다.
    *   **핵심 루프**:
        1.  전체 `ModelItem` 컬렉션을 `foreach`로 순회한다.
        2.  각 `ModelItem`에 대해 필터링(`IsHidden`, `HasGeometry`)을 수행한다.
        3.  객체의 속성들을 중첩 `foreach`로 순회한다.
        4.  속성 하나를 찾을 때마다, `ExportedProperty` DTO를 생성하여 `csvWriter.WriteRecord()`를 통해 **즉시 파일에 쓴다.**
        5.  주기적으로(예: 100개 객체마다) `progress.Report()`를 호출하여 UI에 진행 상황을 보고한다.

    *   이러한 스트리밍 방식은 모델의 크기와 상관없이 거의 일정한 메모리 사용량을 보장하여 대용량 데이터 처리의 안정성을 확보한다.

모든 파일명은 현재 작업중인 폴더 DXnavis와 관련이 있어야 하며, 프로젝트 폴더 구조를 잘 정돈하여야 한다.

5. 지역화 (Localization)

5.1. 언어별 폴더 구조 (Localization Folder Structure):

다국어 지원을 위해 .NET의 표준 지역화 방식을 따릅니다.

프로젝트 속성에 Resources.resx (기본 언어, 예: 영어) 파일을 추가합니다.

한국어 지원을 위해 Resources.ko-KR.resx 파일을 추가합니다.

UI에 표시될 모든 문자열(예: "카테고리", "속성 이름")을 이 리소스 파일에 키-값 쌍으로 정의합니다.

빌드 시, bin/x64/Debug/ko-KR/YourAddin.resources.dll과 같이 언어별 폴더와 위성 어셈블리가 자동으로 생성되는 구조를 설명합니다.

XAML에서는 {x:Static p:Resources.CategoryColumnHeader}와 같이 정적 리소스를 사용하여 문자열을 바인딩합니다.

**아래 개발 내용은 현재 상황에 알맞는 변수를 선택할 수 있어야 합니다.**
---



### **DX 프로젝트 최종 프롬프트: 3-Panel 기반의 계층적 탐색 및 세트 생성**



**역할**: 당신은 Autodesk Navisworks API와 C# WPF/MVVM 패턴에 매우 능숙한 수석 소프트웨어 아키텍트입니다. 당신의 임무는 사용자가 BIM 데이터를 심층적으로 탐색하고, 그 결과를 바탕으로 모델을 다시 구조화(세트 생성)할 수 있는 **3-Panel 레이아웃** 기반의 전문가용 애드인을 설계하고 구현하는 것입니다.



**프로젝트 목표**: 사용자가 Navisworks에서 객체를 선택하면, **(왼쪽)하위 계층 구조를 시각적으로 탐색**하고, **(중간)선택된 노드의 속성을 확인**하며, **(오른쪽)특정 속성을 기준으로 동적인 검색 세트를 생성**할 수 있는 고기능성 WPF 애드인을 개발합니다.



---



### **Phase 1: View(UI) 설계 - 3-Panel 레이아웃 (`MainView.xaml`)**



*   **목표**: 화면을 좌/중/우 세 개의 명확한 역할을 가진 패널로 구성합니다.



1.  **메인 레이아웃**: `Grid` 컨트롤을 사용하여 화면을 세 개의 열(Column)으로 나눕니다. (예: `Width="2*"` | `Width="3*"` | `Width="2*"` 비율)

2.  **왼쪽 패널 (Grid.Column="0") - 객체 계층 구조**:

    *   **`TreeView`**: Navisworks 선택 트리와 유사한 계층 구조를 표시합니다.

        *   `ItemsSource`: ViewModel의 `HierarchyRootNodes`에 바인딩됩니다.

        *   **`HierarchicalDataTemplate`**: 각 노드의 자식(`Children`)과 모양(아이콘 + 텍스트)을 정의하는 데 사용됩니다.

        *   `SelectedItem`: 사용자가 선택한 노드를 ViewModel의 `SelectedHierarchyNode` 속성에 양방향(`TwoWay`)으로 바인딩합니다.

3.  **중간 패널 (Grid.Column="1") - 선택된 객체의 속성**:

    *   **`ListView` with `GridView`**: 왼쪽 `TreeView`에서 선택된 노드의 속성을 표시합니다.

        *   `ItemsSource`: ViewModel의 `SelectedNodeProperties`에 바인딩됩니다.

        *   `SelectedItem`: 사용자가 선택한 속성 행을 ViewModel의 `SelectedPropertyForSetCreation` 속성에 양방향으로 바인딩합니다.

4.  **오른쪽 패널 (Grid.Column="2") - 검색 세트 생성**:

    *   **입력 컨트롤**: `TextBox`들을 사용하여 `FolderName`과 `NewSetName`을 ViewModel에 바인딩합니다.

    *   **선택 정보 표시**: `TextBlock`을 두어, 중간 패널에서 선택된 속성 정보를 사용자에게 명확히 보여줍니다 (`SelectedPropertyInfo`에 바인딩).

    *   **실행 버튼**: "위 속성으로 '검색 세트' 생성" `Button`을 배치하고, `CreateSearchSetFromPropertyCommand`에 바인딩합니다.



---



### **Phase 2: ViewModel(두뇌) 설계 - 3-Panel 상호작용 관리 (`MainViewModel.cs`)**



*   **목표**: 세 패널 간의 데이터 흐름과 상호작용을 완벽하게 제어합니다.



1.  **계층 구조 모델 정의 (`HierarchyNodeViewModel.cs`)**:

    *   `TreeView` 바인딩을 위해 `DisplayName` (string)과 `Children` (`ObservableCollection<HierarchyNodeViewModel>`) 속성을 포함하는 재귀적 구조의 ViewModel을 정의합니다.



2.  **`MainViewModel.cs` 속성 정의 (수동 MVVM 방식)**:

    *   **왼쪽 패널용**: `public ObservableCollection<HierarchyNodeViewModel> HierarchyRootNodes { get; }`

    *   **중간 패널용**: `public ObservableCollection<PropertyItemViewModel> SelectedNodeProperties { get; }`

    *   **오른쪽 패널용**: `FolderName`, `NewSetName`, `SelectedPropertyInfo` (읽기 전용) 속성을 정의합니다.

    *   **패널 간 상호작용용**:

        *   `private HierarchyNodeViewModel _selectedHierarchyNode;` (`TreeView` 선택 항목)

        *   `private PropertyItemViewModel _selectedPropertyForSetCreation;` (`ListView` 선택 항목)



3.  **핵심 상호작용 로직**:

    *   **Navisworks 선택 → 왼쪽 패널 업데이트**:

        *   `OnCurrentSelectionChanged` 이벤트 핸들러가 트리거됩니다.

        *   `NavisworksDataExtractor` 서비스를 호출하여 선택된 객체로부터 **계층적 `HierarchyNodeViewModel` 트리**를 생성합니다.

        *   결과를 `HierarchyRootNodes` 컬렉션에 채워 넣습니다. 이때 중간/오른쪽 패널은 초기화합니다.

    *   **왼쪽 패널 선택 → 중간 패널 업데이트**:

        *   `SelectedHierarchyNode` 속성의 `setter`에서 로직을 구현합니다.

        *   선택된 노드(`_selectedHierarchyNode`)가 변경되면, 해당 노드의 객체 ID를 사용하여 **그 객체만의 속성**을 `SelectedNodeProperties` 컬렉션에 채워 넣습니다.

    *   **중간 패널 선택 → 오른쪽 패널 업데이트**:

        *   `SelectedPropertyForSetCreation` 속성의 `setter`에서 로직을 구현합니다.

        *   선택된 속성(`_selectedPropertyForSetCreation`)이 변경되면, `SelectedPropertyInfo` 텍스트를 업데이트하고 `CreateSearchSetFromPropertyCommand`의 `CanExecute` 상태를 갱신합니다.



4.  **Command 구현**:

    *   `CreateSearchSetFromPropertyCommand`는 `SelectedPropertyForSetCreation`이 `null`이 아닐 때만 활성화됩니다.

    *   실행 시, `SetCreationService`를 호출하여 `FolderName`, `NewSetName`, 그리고 선택된 속성의 `Category`, `Name`, `Value`를 인자로 전달합니다.



---



### **Phase 3: Service(실무자) 계층 설계**



*   **목표**: ViewModel의 지시를 받아 실제 Navisworks API 호출을 수행합니다.



1.  **`NavisworksDataExtractor.cs`**:

    *   **재귀적 변환 메서드**: `public List<HierarchyNodeViewModel> ExtractHierarchy(ModelItem rootItem)`

    *   이 메서드는 Navisworks의 `ModelItem` 트리를 입력받아, `TreeView`에 바인딩할 수 있는 `HierarchyNodeViewModel`의 재귀적 리스트를 생성하여 반환합니다.

    *   **단일 객체 속성 추출 메서드**: `public List<PropertyItemViewModel> ExtractProperties(ModelItem item)`

    *   이 메서드는 객체 하나를 입력받아, 중간 패널에 표시할 속성 리스트를 생성하여 반환합니다.



2.  **`SetCreationService.cs`**:

    *   `public void CreateSearchSetFromProperty(string folderName, string setName, string category, string name, string value)`

    *   전달받은 인자를 사용하여 `SearchCondition`을 만들고, `SearchSet`을 생성하여 지정된 폴더에 추가하는 로직을 구현합니다.



### **전체 워크플로우 요약**



1.  **초기화**: 사용자가 Navisworks에서 객체를 선택하면, 왼쪽 `TreeView`에 해당 객체와 그 하위 계층이 표시됩니다.

2.  **탐색**: 사용자가 왼쪽 `TreeView`에서 특정 노드(예: 'Wall-01')를 클릭합니다.

3.  **속성 확인**: 중간 `ListView`에 'Wall-01' 객체의 모든 속성이 나타납니다.

4.  **기준 선택**: 사용자가 중간 `ListView`에서 특정 속성 행(예: '재료 | 이름 | 콘크리트')을 클릭합니다.

5.  **정보 확인**: 오른쪽 패널에 선택된 속성 정보가 표시되고, '세트 생성' 버튼이 활성화됩니다.

6.  **세트 생성**: 사용자가 폴더/세트 이름을 입력하고 버튼을 클릭하면, "재료 이름이 '콘크리트'인 모든 객체"를 찾는 검색 세트가 Navisworks에 생성됩니다.
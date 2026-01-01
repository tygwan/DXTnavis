역할: 당신은 C# WPF/MVVM 패턴 및 Navisworks API에 정통한 수석 소프트웨어 엔지니어입니다. 현재 개발 중인 DX 애드인에서 발견된 세 가지 주요 문제(계층 구조 미표시, 속성 권한 미표시, UI 입력 오류)를 해결하기 위한 명확한 코드 수정 지침을 제공해야 합니다.

프로젝트 목표: 사용자가 BIM 데이터를 심층적으로 탐색하고 관리할 수 있도록, (1)계층 구조 TreeView를 완벽하게 구현하고, (2)속성의 읽기/쓰기 권한을 명시하며, (3)모든 UI 입력이 정상적으로 작동하도록 애플리케이션의 핵심 기능을 수정하고 안정화합니다.

문제 1 해결: 객체 계층 구조를 확장/축소 가능한 TreeView로 구현

현재 증상: 왼쪽 '객체 계층 구조' 패널의 TreeView에 모든 객체가 들여쓰기나 '+' 버튼 없이 평평한 리스트(직렬)로 표시됩니다.

근본 원인: WPF TreeView가 데이터의 부모-자식 관계를 표현하는 방법을 알지 못합니다. 이를 위해 **계층적 데이터 템플릿(HierarchicalDataTemplate)**과 그에 맞는 재귀적 ViewModel 구조가 필수적입니다.

1.1. ViewModel 수정 (HierarchyNodeViewModel.cs)

지침: TreeView의 각 노드를 나타내는 HierarchyNodeViewModel 클래스에, 자식 노드들을 담을 수 있는 Children 컬렉션을 추가합니다.

code

C#

// 파일: ViewModels/HierarchyNodeViewModel.cs

using CommunityToolkit.Mvvm.ComponentModel;

using System.Collections.ObjectModel;



public partial class HierarchyNodeViewModel : ObservableObject

{

    public string DisplayName { get; set; }

    public Guid ObjectId { get; set; }

    

    // *** 핵심 추가 사항 ***

    // 자식 노드들을 담을 컬렉션입니다. TreeView가 이 속성을 사용하여 하위 항목을 그립니다.

    public ObservableCollection<HierarchyNodeViewModel> Children { get; set; }



    public HierarchyNodeViewModel()

    {

        Children = new ObservableCollection<HierarchyNodeViewModel>();

    }

}

1.2. View 수정 (MainView.xaml)

지침: TreeView가 계층 구조를 이해할 수 있도록 HierarchicalDataTemplate을 정의합니다.

code

Xml

<!-- 파일: Views/MainView.xaml (왼쪽 패널 TreeView 수정) -->

<TreeView ItemsSource="{Binding HierarchyRootNodes}">

    <TreeView.ItemTemplate>

        <!-- HierarchicalDataTemplate은 TreeView를 위한 특별한 템플릿입니다. -->

        <HierarchicalDataTemplate ItemsSource="{Binding Children}">

            <!-- ItemsSource="{Binding Children}": 이 노드의 하위 항목들은 'Children'이라는 속성에서 가져오라고 WPF에 알려줍니다. -->

            <TextBlock Text="{Binding DisplayName}" />

        </HierarchicalDataTemplate>

    </TreeView.ItemTemplate>

</TreeView>

1.3. Service 로직 수정 (NavisworksDataExtractor.cs)

지침: Navisworks의 ModelItem 트리를 우리가 정의한 계층적 HierarchyNodeViewModel 구조로 변환하는 재귀 함수를 구현합니다.

code

C#

// 파일: Services/NavisworksDataExtractor.cs

public List<HierarchyNodeViewModel> ExtractHierarchy(ModelItemCollection rootItems)

{

    var rootNodes = new List<HierarchyNodeViewModel>();

    foreach (var item in rootItems)

    {

        rootNodes.Add(CreateNodeRecursive(item));

    }

    return rootNodes;

}



private HierarchyNodeViewModel CreateNodeRecursive(ModelItem currentItem)

{

    var node = new HierarchyNodeViewModel { DisplayName = currentItem.DisplayName, ObjectId = currentItem.InstanceGuid };

    foreach (ModelItem childItem in currentItem.Children)

    {

        node.Children.Add(CreateNodeRecursive(childItem));

    }

    return node;

}

문제 2 해결: 속성의 읽기/쓰기 권한 표시

현재 증상: 중간 '선택된 객체의 속성' 패널에 속성의 값만 표시될 뿐, 이 속성이 수정 가능한지 알 수 없습니다.

근본 원인: 속성 데이터를 담는 PropertyItemViewModel에 권한 정보를 담을 필드가 없고, UI에도 해당 정보를 표시할 컬럼이 없습니다.

2.1. ViewModel 수정 (PropertyItemViewModel.cs)

지침: DataProperty.IsReadOnly API 속성을 저장할 ReadWriteStatus 속성을 추가합니다.

code

C#

// 파일: ViewModels/PropertyItemViewModel.cs

public partial class PropertyItemViewModel : ObservableObject

{

    // ... 기존 Category, Name, Value 속성 ...

    [ObservableProperty] private string _readWriteStatus; // "읽기 전용" 또는 "쓰기 가능"

}

2.2. ViewModel 로직 수정 (MainViewModel.cs)

지침: OnCurrentSelectionChanged 메서드에서 속성을 추출할 때, DataProperty.IsReadOnly 값을 확인하여 ReadWriteStatus를 설정합니다.

code

C#

// 파일: ViewModels/MainViewModel.cs (OnCurrentSelectionChanged 내부)

// ... 속성 순회 루프 ...

SelectedObjectProperties.Add(new PropertyItemViewModel

{

    Category = category.DisplayName,

    Name = property.DisplayName,

    Value = property.Value.ToString(),

    // *** 핵심 추가 사항 ***

    ReadWriteStatus = property.IsReadOnly ? "읽기 전용" : "쓰기 가능"

});

2.3. View 수정 (MainView.xaml)

지침: 중간 패널의 GridView에 '권한'을 표시할 새로운 GridViewColumn을 추가합니다.

code

Xml

<!-- 파일: Views/MainView.xaml (중간 패널 ListView 수정) -->

<GridView>

    <!-- ... 기존 '선택', '객체', '카테고리', '속성', '값' 컬럼 ... -->

    <GridViewColumn Header="권한" Width="80" DisplayMemberBinding="{Binding ReadWriteStatus}"/>

</GridView>

문제 3 해결: 오른쪽 패널 TextBox 입력 오류

현재 증상: '폴더 이름'과 '세트 이름' TextBox에 숫자, 기호, 영문이 입력되지 않습니다.

근본 원인: TextBox의 특정 이벤트 핸들러(예: PreviewTextInput)가 한글 입력만 허용하도록 잘못 구현되었거나, IME(입력기)와의 충돌이 발생했을 가능성이 높습니다.

3.1. 코드 비하인드 검토 및 수정 (MainView.xaml.cs)

지침: MainView.xaml.cs 파일에 TextBox의 PreviewTextInput 또는 TextChanged와 같은 이벤트 핸들러가 있는지 확인하고, 있다면 해당 이벤트 핸들러를 모두 주석 처리하거나 삭제합니다. MVVM 패턴에서는 이러한 로직을 코드 비하인드에 두는 것을 지양해야 합니다.

code

C#

// 파일: Views/MainView.xaml.cs

// 아래와 같은 코드가 있다면 삭제 또는 주석 처리하세요.

/*

private void FolderNameTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)

{

    // 잘못된 필터링 로직이 있을 가능성이 높음

    ...

}

*/

3.2. XAML 바인딩 확인 (MainView.xaml)

지침: 오른쪽 패널의 TextBox들이 ViewModel의 속성에 정상적으로 바인딩되어 있는지 확인합니다. UpdateSourceTrigger=PropertyChanged를 추가하여 입력 즉시 ViewModel에 값이 반영되도록 합니다.

code

Xml

<!-- 파일: Views/MainView.xaml (오른쪽 패널 TextBox 수정) -->

<TextBox Text="{Binding FolderName, UpdateSourceTrigger=PropertyChanged}"/>

<TextBox Text="{Binding NewSetName, UpdateSourceTrigger=PropertyChanged}"/>
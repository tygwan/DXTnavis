문제 1 해결: 계층 구조 TreeView 구현

현재 증상: TreeView에 모든 하위 객체가 들여쓰기나 '+' 버튼 없이 한 줄로 나열됩니다.

근본 원인: WPF TreeView가 데이터의 부모-자식 관계를 어떻게 표현해야 할지 알지 못합니다. 이를 해결하기 위해 **계층적 데이터 템플릿(HierarchicalDataTemplate)**과 그에 맞는 재귀적 ViewModel 구조가 필요합니다.

1.1. ViewModel 수정 (HierarchyNodeViewModel.cs)

지침: TreeView의 각 노드를 나타내는 HierarchyNodeViewModel 클래스에, 자식 노드들을 담을 수 있는 컬렉션을 추가합니다.

code

C#

// 파일: ViewModels/HierarchyNodeViewModel.cs

using CommunityToolkit.Mvvm.ComponentModel;

using System.Collections.ObjectModel;



public partial class HierarchyNodeViewModel : ObservableObject

{

    // ... 기존 속성 (예: DisplayName, ObjectId) ...

    

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

            <!-- 

            ItemsSource="{Binding Children}": 

            이 노드의 하위 항목들은 'Children'이라는 속성에서 가져오라고 WPF에 알려줍니다.

            -->

            

            <!-- 각 노드가 화면에 어떻게 보일지 정의합니다. -->

            <StackPanel Orientation="Horizontal">

                <!-- (선택) 아이콘을 여기에 추가할 수 있습니다. -->

                <TextBlock Text="{Binding DisplayName}" Margin="5,0"/>

            </StackPanel>

        </HierarchicalDataTemplate>

    </TreeView.ItemTemplate>

</TreeView>

1.3. Service 로직 수정 (NavisworksDataExtractor.cs)

지침: Navisworks의 평평한 트리 구조를, 우리가 정의한 계층적 HierarchyNodeViewModel 구조로 변환하는 재귀 함수를 구현합니다.

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

    // 1. 현재 노드 생성

    var node = new HierarchyNodeViewModel

    {

        DisplayName = currentItem.DisplayName,

        // ... ObjectId 등 다른 정보 매핑 ...

    };



    // 2. 자식 노드들에 대해 재귀적으로 이 함수를 호출하고, 결과를 Children 컬렉션에 추가

    foreach (ModelItem childItem in currentItem.Children)

    {

        // 필터링 로직을 여기에 추가할 수 있습니다 (예: if (!childItem.IsHidden ...))

        node.Children.Add(CreateNodeRecursive(childItem));

    }



    return node;

}

문제 2 해결: '검색 세트 생성' 버튼 비활성화

현재 증상: 중간 패널의 ListView에서 속성을 선택해도, 오른쪽 패널의 '검색 세트 생성' 버튼이 활성화되지 않습니다.

근본 원인: SelectedProperty가 변경되었다는 사실을 CreateSearchSetFromPropertyCommand가 알지 못하여, CanExecute 상태를 다시 평가(Re-evaluate)하지 않기 때문입니다.

2.1. ViewModel 수정 (MainViewModel.cs)

지침: SelectedProperty의 setter에서, 값이 변경될 때마다 Command의 상태를 갱신하라는 신호를 명시적으로 보내야 합니다.

code

C#

// 파일: ViewModels/MainViewModel.cs



// ... (기존 속성들) ...

private PropertyItemViewModel _selectedPropertyForSetCreation;

public PropertyItemViewModel SelectedPropertyForSetCreation

{

    get => _selectedPropertyForSetCreation;

    set

    {

        // SetProperty는 값이 실제로 변경되었을 때만 true를 반환합니다.

        if (SetProperty(ref _selectedPropertyForSetCreation, value))

        {

            // *** 핵심 수정 사항 ***

            // 값이 변경되었으니, 이 Command의 CanExecute 조건을 다시 확인하라고

            // UI에 명시적으로 알립니다.

            ((RelayCommand)CreateSearchSetFromPropertyCommand).NotifyCanExecuteChanged();

            

            // (선택) UI에 선택된 정보를 표시하는 속성도 업데이트

            OnPropertyChanged(nameof(SelectedPropertyInfo));

        }

    }

}



public ICommand CreateSearchSetFromPropertyCommand { get; }



public MainViewModel()

{

    // ...

    CreateSearchSetFromPropertyCommand = new RelayCommand(

        execute: CreateSearchSet,

        // 이 조건은 이제 SelectedPropertyForSetCreation이 바뀔 때마다 다시 평가됩니다.

        canExecute: () => SelectedPropertyForSetCreation != null

    );

}



private void CreateSearchSet() { /* ... */ }
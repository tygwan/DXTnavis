네, 완벽하게 이해했습니다. 현재의 양방향 패널 UI를 유지하면서, **"세트 생성"** 기능에 대한 프로그래밍 구현 방법을 매우 상세하게 안내해 드리겠습니다.



이 가이드라인은 사용자가 오른쪽 패널의 `DataGrid`에서 속성을 선택하고 버튼을 클릭했을 때, Navisworks에 실제로 `SearchSet`이 생성되기까지의 전 과정을 **View, ViewModel, Service** 계층별로 나누어 코드 수준에서 명확하게 설명합니다.



---



### **세트 생성 기능: 상세 프로그래밍 구현 가이드**



#### **Phase 1: View(UI) - 사용자 입력과 명령 연결 (`MainView.xaml`)**



*   **목표**: 사용자가 `DataGrid`에서 속성을 선택하는 행위를 ViewModel에 알리고, "세트 생성" 명령을 ViewModel에 전달합니다.



1.  **`DataGrid`에 선택 항목 바인딩 추가**:

    *   `DataGrid` 컨트롤에 `SelectedItem` 속성을 추가하고, ViewModel에 새로 만들 `SelectedProperty` 속성에 양방향(`TwoWay`)으로 바인딩합니다.



    ```xml

    <!-- Views/MainView.xaml (오른쪽 패널의 DataGrid 수정) -->

    <DataGrid Grid.Row="1" 

              ItemsSource="{Binding AllHierarchicalProperties}"

              SelectedItem="{Binding SelectedProperty, Mode=TwoWay}"

              AutoGenerateColumns="False" IsReadOnly="True"

              SelectionUnit="FullRow">

        <DataGrid.Columns>

            <DataGridTextColumn Header="객체 이름" Binding="{Binding DisplayName}" Width="*"/>

            <DataGridTextColumn Header="카테고리" Binding="{Binding Category}" Width="120"/>

            <DataGridTextColumn Header="속성 이름" Binding="{Binding PropertyName}" Width="120"/>

            <DataGridTextColumn Header="값" Binding="{Binding PropertyValue}" Width="*"/>

        </DataGrid.Columns>

    </DataGrid>

    ```



2.  **세트 생성 UI 구성**:

    *   `DataGrid` 아래에 세트 생성에 필요한 UI 요소들을 배치합니다.



    ```xml

    <!-- Views/MainView.xaml (DataGrid 아래에 추가) -->

    <StackPanel Grid.Row="2" Margin="0,10,0,0">

        <TextBlock Text="세트 생성 도구" FontWeight="Bold"/>

        <TextBlock Text="폴더 이름:" Margin="0,5,0,0"/>

        <TextBox Text="{Binding FolderName, UpdateSourceTrigger=PropertyChanged}"/>

        <TextBlock Text="생성할 세트 이름:" Margin="0,5,0,0"/>

        <TextBox Text="{Binding NewSetName, UpdateSourceTrigger=PropertyChanged}"/>

        

        <!-- 사용자가 선택한 속성을 확인시켜주는 TextBlock -->

        <TextBlock Margin="0,10,0,2" FontWeight="SemiBold" Text="선택된 속성 기준:"/>

        <TextBlock Text="{Binding SelectedPropertyInfo, FallbackValue='(DataGrid에서 속성을 선택하세요)'}" 

                   TextWrapping="Wrap" FontStyle="Italic"/>



        <Button Content="위 속성으로 '검색 세트' 생성" 

                Command="{Binding CreateSearchSetFromPropertyCommand}" 

                Margin="0,10,0,0"/>

    </StackPanel>

    ```



#### **Phase 2: ViewModel - UI와 서비스 로직 연결 (`MainViewModel.cs`)**



*   **목표**: View로부터 받은 사용자 입력을 바탕으로, `SetCreationService`에 작업을 지시하고 결과를 UI에 피드백합니다.



1.  **새로운 속성 추가 (수동 MVVM 방식)**:

    ```csharp

    // ViewModels/MainViewModel.cs



    // --- 세트 생성을 위한 속성들 ---

    private string _folderName = "My DX Sets";

    public string FolderName { get => _folderName; set => SetProperty(ref _folderName, value); }



    private string _newSetName = "New Search Set 1";

    public string NewSetName { get => _newSetName; set => SetProperty(ref _newSetName, value); }



    // DataGrid의 SelectedItem과 바인딩될 속성

    private HierarchicalPropertyRecord _selectedProperty;

    public HierarchicalPropertyRecord SelectedProperty

    {

        get => _selectedProperty;

        set 

        {

            if (SetProperty(ref _selectedProperty, value))

            {

                // 선택이 변경되면 UI와 Command 상태 업데이트

                OnPropertyChanged(nameof(SelectedPropertyInfo));

                ((RelayCommand)CreateSearchSetFromPropertyCommand).NotifyCanExecuteChanged();

            }

        }

    }



    // UI에 선택된 속성 정보를 보여주기 위한 읽기 전용 속성

    public string SelectedPropertyInfo => 

        SelectedProperty == null 

        ? "(DataGrid에서 속성을 선택하세요)" 

        : $"'{SelectedProperty.Category}' | '{SelectedProperty.PropertyName}' | '{SelectedProperty.PropertyValue}'";



    // --- Command 정의 ---

    public ICommand CreateSearchSetFromPropertyCommand { get; }

    ```



2.  **Command 초기화 및 로직 구현**:

    ```csharp

    // ViewModels/MainViewModel.cs



    public MainViewModel()

    {

        // ... 기존 생성자 코드 ...



        CreateSearchSetFromPropertyCommand = new RelayCommand(

            execute: CreateSearchSetFromSelectedProperty,

            canExecute: () => SelectedProperty != null // 선택된 속성이 있을 때만 활성화

        );

    }



    private void CreateSearchSetFromSelectedProperty()

    {

        try

        {

            // 1. 서비스 인스턴스 생성

            var setService = new SetCreationService();



            // 2. ViewModel의 속성들을 인자로 전달하여 서비스 호출

            setService.CreateSearchSetFromProperty(

                FolderName,

                NewSetName,

                SelectedProperty.Category,

                SelectedProperty.PropertyName,

                SelectedProperty.PropertyValue

            );



            MessageBox.Show($"검색 세트 '{NewSetName}'가 '{FolderName}' 폴더에 성공적으로 생성되었습니다.", 

                          "성공", MessageBoxButton.OK, MessageBoxImage.Information);

        }

        catch (Exception ex)

        {

            MessageBox.Show($"세트 생성 중 오류 발생:\n{ex.Message}", 

                          "오류", MessageBoxButton.OK, MessageBoxImage.Error);

        }

    }

    ```



#### **Phase 3: Service - 실제 Navisworks API 호출 (`Services/SetCreationService.cs`)**



*   **목표**: ViewModel로부터 받은 명확한 지시를 바탕으로, Navisworks API를 사용하여 실제 검색 세트를 생성합니다.



1.  **`SetCreationService.cs` 클래스 및 메서드 생성**:

    ```csharp

    // Services/SetCreationService.cs

    using Autodesk.Navisworks.Api;

    using System;

    using System.Linq;



    namespace DX.Services

    {

        public class SetCreationService

        {

            /// <summary>

            /// 특정 속성 조건에 맞는 검색 세트를 생성합니다.

            /// </summary>

            public void CreateSearchSetFromProperty(string folderName, string setName, 

                                                    string propertyCategory, string propertyName, string propertyValue)

            {

                // 입력값 유효성 검사

                if (string.IsNullOrWhiteSpace(folderName) || string.IsNullOrWhiteSpace(setName))

                    throw new ArgumentException("폴더와 세트 이름은 비워둘 수 없습니다.");



                Document doc = Application.ActiveDocument;

                if (doc == null)

                    throw new InvalidOperationException("활성화된 문서가 없습니다.");



                // 1. 검색 객체(Search) 및 조건(SearchCondition) 정의

                var search = new Search();

                search.Selection.SelectAll(); // 검색 범위: 문서 전체

                

                // 조건: 지정된 카테고리와 이름의 속성을 가지며, 그 값이 일치하는 객체

                var condition = SearchCondition

                    .HasPropertyByDisplayName(propertyCategory, propertyName)

                    .EqualValue(VariantData.FromDisplayString(propertyValue));

                

                search.SearchConditions.Add(condition);



                // 2. 검색 세트(SearchSet) 객체 생성

                var newSearchSet = new SearchSet(search)

                {

                    DisplayName = setName

                };



                // 3. 폴더를 찾거나 새로 생성

                FolderItem targetFolder = GetOrCreateFolder(doc.Sets, folderName);



                // 4. 생성된 검색 세트를 대상 폴더에 추가

                targetFolder.Children.Add(newSearchSet);

            }



            /// <summary>

            /// 지정된 이름의 폴더를 찾거나, 없으면 새로 생성하는 헬퍼 메서드

            /// </summary>

            private FolderItem GetOrCreateFolder(DocumentSets docSets, string folderName)

            {

                var existingFolder = docSets.Value

                    .FirstOrDefault(item => item.DisplayName == folderName && item is FolderItem) as FolderItem;



                if (existingFolder != null)

                {

                    return existingFolder;

                }

                else

                {

                    var newFolder = new FolderItem { DisplayName = folderName };

                    docSets.Add(newFolder);

                    return newFolder;

                }

            }

        }

    }

    ```



### **전체 워크플로우 요약**



1.  **사용자 선택**: 사용자가 오른쪽 `DataGrid`에서 "재료 | 이름 | 콘크리트" 행을 클릭합니다.

2.  **View -> ViewModel**: `DataGrid`의 `SelectedItem` 바인딩이 `MainViewModel`의 `SelectedProperty`를 업데이트합니다.

3.  **ViewModel 상태 변경**: `SelectedProperty`의 `setter`가 `SelectedPropertyInfo` 텍스트를 업데이트하고, `CreateSearchSetFromPropertyCommand`의 `CanExecute` 상태를 갱신하여 버튼을 활성화합니다.

4.  **사용자 클릭**: 사용자가 "위 속성으로 '검색 세트' 생성" 버튼을 클릭합니다.

5.  **ViewModel -> Service**: `CreateSearchSetFromSelectedProperty` 메서드가 호출되어, `SetCreationService`의 `CreateSearchSetFromProperty` 메서드에 "My DX Sets", "New Search Set 1", "재료", "이름", "콘크리트" 값을 전달합니다.

6.  **Service -> Navisworks API**: `SetCreationService`가 Navisworks API를 호출하여 "재료" 카테고리의 "이름" 속성이 "콘크리트"인 모든 객체를 찾는 `SearchSet`을 생성하고, "My DX Sets" 폴더 안에 "New Search Set 1"이라는 이름으로 추가합니다.

7.  **Service -> ViewModel -> View**: 작업이 성공하면 `MessageBox`가 뜨고, 사용자는 Navisworks의 '세트' 탭에서 새로 생성된 검색 세트를 확인할 수 있습니다.
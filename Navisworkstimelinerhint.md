여기에 연장선으로 navisworks의 timeliner 컨트롤 하기위한 기초데이터가 된다. revit과 navisworks의 데이터가 refine(filtering)된 후, timeliner의 컨트롤 상태에 따라서 다음 시나리오가 작동하려면 어떻게 해야하는지 전략을 세운다.
1. SQL 서버에 업로드된 계층정보를 이용하여 각 tree에서 revit과 navisworks에서 확인되는 실제 3d object를 파악할 수 있는 필터를 구상한다.
예를들면 업로드된 데이터가 계층이 0~9까지 SQL DB에 저장되고 각 트리의 최하위에는 단일 객체들이 존재한다. (단, 각 트리의 최하위가 항상 단일 객체가 존재하는지 모른다)
하나 더 예를 들어주면, 계층 레벨 0~5까지 있는 트리에서 레벨 4의 이름이 A1이고 A1의 하위 계층인 레벨 5에 object가 4개 있다면, 레벨5를 하나의 세트로 묶어 A1이라고 표현할 수 있고, 또 레벨 3을 구성하는 것이 A1, A2, A3 등이 있다면 레벨3(레벨3의 이름)으로 A1, A2, A3등을 세트화 하여 timeliner에 등록하는 것이다. 이 과정을 반복하고, 각 세트의 duration을 사용자가 navisworks 플러그인 혹은 별도의 효율적인 방법을 통해 설정할 수 있도록 하고, 객체들을 timeliner 정보에 순서대로 세트화 하여 4D simulation을 유도하는 것이다.

```
Navisworks Timeliner API 기능 소개
1. SelectionSet을 활용한 Timeliner Task 생성 (Timeliner Part-1)
미리 정의된 SelectionSet을 사용하여 문서의 Timeliner 객체에 작업을 추가하는 기능이 제공됩니다. 이는 Navisworks의 "Auto-Add Task > For Every Set" 명령과 유사한 기능을 구현할 수 있습니다.

주요 API 객체 및 기능:
1. Timeliner 객체 수집:
    ◦ 현재 문서에서 Timeliner 객체를 수집하기 위해 GetTimeliner 메서드를 사용합니다.
    ◦ 저장된 SelectionSet(저장된 항목 컬렉션)을 수집하기 위해 SelectionSets 속성을 사용할 수 있습니다. 이는 SavedItemCollection을 나타냅니다.
2. Timeliner Task 생성:
    ◦ 수집된 각 SelectionSet을 순회하며 TimelinerTask 생성자를 통해 각 작업을 생성합니다.
    ◦ Timeliner Task의 주요 속성을 설정해야 합니다:
        ▪ DisplayName: SelectionSet의 DisplayName 값을 설정합니다.
        ▪ Start/End Date: 연도, 월, 일 정보가 포함된 DateTime 객체를 설정합니다. (예시에서는 단순화를 위해 월에 1(인덱스)씩 증가 값을 할당합니다).
        ▪ SimulationTaskTypeName: **"Construct"**와 같은 시뮬레이션 작업 유형 이름을 할당합니다. 기본 유형으로는 "Demolish" 또는 **"Temporary"**도 할당할 수 있습니다.
3. SelectionSet 첨부:
    ◦ Timeliner Task의 Attached 속성에 SelectionSet을 첨부하기 위해서는 일련의 유형 변환(TypeConversion) 과정이 필요합니다.
    ◦ SelectionSet을 SelectionSource로 변환하고, 이를 다시 SelectionSourceCollection으로 변환한 다음, Selection 속성을 사용하여 TimelinerTask에 첨부합니다.
4. 작업 추가:
    ◦ 마지막으로, 생성된 TimelinerTask를 Timeliner 객체에 추가합니다.
2. 외부 데이터 소스(CSV)를 통한 Timeliner Task 자동화 (Timeliner Part-2)
CSV 스케줄링 데이터 파일과 같은 외부 데이터 소스로부터 Timeliner Task를 자동화하고 가져오는 기능이 제공됩니다.
주요 API 클래스 및 추상 멤버:
이 기능을 구현하기 위한 메인 클래스는 Navisworks 프레임워크와 연결하기 위해 TimelinerDataSourceProvider 클래스를 상속받아야 합니다. 상속된 클래스는 외부 스케줄링 데이터 소스에 대한 연결을 제공하고 데이터를 Navisworks로 가져오는 역할을 합니다.
프레임워크에 의해 호출되며 반드시 구현해야 하는 **추상 멤버(Abstract Members)**는 다음과 같습니다:
• CreateDataSource: TimelinerDataSource 객체를 생성하는 메서드입니다. 이 객체는 외부 프로젝트 데이터의 세부 정보와 외부 데이터에 다시 연결하는 방법을 포함합니다. 이 메서드에서 ProjectIndentifier는 데이터 소스 파일(예: data-source.csv)과 연결되며, DataSourceProviderId 및 Version, ProviderName 등을 설정합니다.
• ImportTasksCore: 사용자가 데이터 소스에서 Rebuild(재구축) 또는 **Synchronize(동기화)**를 선택할 때 호출됩니다. 선택된 외부 데이터로부터 Timeliner Task를 생성하는 역할을 하며, Task 가져오기 작업 중 발생한 문제에 대한 세부 정보가 포함된 보고서(Report)를 반환합니다.
• UpdateDataSource: 사용자가 DataSource 탭에서 **편집(edit)**을 클릭할 때 프레임워크에 의해 호출됩니다. 현재 프로젝트 식별자에 연결된 데이터 소스 파일을 확인합니다.
• ValidateSettings: TimelinerDataSource 설정을 검증합니다.
• DisposeManagedResources / DisposeUnmanageResources: IDisposable의 일부입니다.
• IsAvailable: 이 TimelinerDataSourceProvider가 작업을 가져오는 데 사용할 수 있는지 여부를 결정하며, 기본값은 true를 반환합니다.
ImportTasksCore 구현 세부 사항:
ImportTasksCore 메서드 내에서 Timeliner Task가 생성됩니다.
• CSV 데이터 스트림을 라인별로 읽고 구분 문자(예: " , ")를 사용하여 분할합니다.
• CSV 데이터 소스 파일에서 생성된 Timeliner Task를 담을 **상위/루트 TimelinerTask**를 생성합니다.
• 각 라인에서 분리된 값(fields 배열)을 사용하여 Timeliner 속성 값을 설정합니다. 설정되는 속성에는 SynchronizationId, DisplayName, PlannedStartDate, PlannedEndDate, SimulationTaskTypeName 등이 있습니다.
• StringToDateTime과 같은 사용자 정의 메서드를 사용하여 문자열/텍스트를 DateTime 형식으로 변환합니다.
• getSelectionSetByName과 같은 사용자 정의 메서드를 사용하여 SelectionSet을 검색하고 시뮬레이션을 위해 Timeliner Task에 첨부합니다.
추가 설정 기능:
• AddAvailableFields 메서드를 사용하여 사용자가 매핑할 수 있도록 AvailableFields를 채울 수 있습니다. (다만, Task가 ImportTasksCore 메서드에서 직접 생성되는 경우 이 매핑 메서드는 생략될 수도 있습니다).
```
이 과정을 모두 사용하는 것은 너의 선택이야.
여기에 추가로 timeliner와 이를 컨트롤하기 위한 어떠한 방법을 동원하여 이 계획을 세워보자.
간단히 설명하면, 나의 의도는 revit 혹은 navisworks로부터 SQL에 업로드된 데이터를 이용하여(cost, duration 추가 방법 모의 필요) DB로부터 데이터를 정리(실제 3d object를 찾아내는 것. raw > 데이터파이프라인 고려)한 뒤, navisworks timeliner에 3d object를 계층적으로 세트화 하여 timeliner에 계층(시간순, 계층순)에 따라 timeliner task를 자동 등록하는 과정이다.
여기서 발생한 cost, duration은 데이터파이프라인에 등록되어있으므로 추후에 3d 모델링에 대하여 구역별, 계층별 기간과 비용을 산정할 수 있도록 하는것이다.
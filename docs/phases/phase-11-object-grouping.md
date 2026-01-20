# Phase 11: Object Grouping MVP - Implementation Complete

| Field | Value |
|-------|-------|
| **Phase** | 11 |
| **Version** | v0.9.0 |
| **Status** | ✅ Complete |
| **Release Date** | 2026-01-20 |
| **Goal** | 중앙 패널에서 객체별 그룹화 표시 및 선택 |
| **Constraint** | CLAUDE.md - "CollectionViewSource with 100K+ items" 금지 |

---

## 1. 문제 정의

### 1.1 현재 상태

```
┌──────────────────────────────────────────────────────────────────┐
│ ☐ │ Level │ Object    │ Category   │ Property │ Value │ Unit   │
├───┼───────┼───────────┼────────────┼──────────┼───────┼────────┤
│ ☐ │   3   │ Footing-1 │ Item       │ Name     │ FT-01 │        │
│ ☐ │   3   │ Footing-1 │ Item       │ Type     │ Conc  │        │
│ ☐ │   3   │ Footing-1 │ Dimensions │ Width    │ 1500  │ mm     │
│ ☐ │   3   │ Footing-1 │ Dimensions │ Length   │ 2000  │ mm     │
│ ☐ │   3   │ Footing-1 │ Dimensions │ Depth    │ 500   │ mm     │
│ ☐ │   3   │ Footing-1 │ Material   │ Grade    │ C24   │        │
│ ☐ │   3   │ Footing-2 │ Item       │ Name     │ FT-02 │        │
│ ☐ │   3   │ Footing-2 │ Item       │ Type     │ Conc  │        │
│ ... (동일 객체가 속성 개수만큼 반복) ...                          │
└──────────────────────────────────────────────────────────────────┘

문제점:
- 동일 객체가 속성 개수(10~100+)만큼 행으로 반복
- 객체 경계 파악 어려움
- 스크롤 과다 필요
- 전체 데이터: 445K+ 행
```

### 1.2 목표 상태

```
┌──────────────────────────────────────────────────────────────────┐
│ ☐ │ Level │ Object      │ Properties │ Categories              │
├───┼───────┼─────────────┼────────────┼─────────────────────────┤
│ ▼ │   3   │ ☑ Footing-1 │ 6 props    │ Item, Dimensions, Mat.  │
│   │       │  └─ Item                                            │
│   │       │     ├ ☑ Name: FT-01                                │
│   │       │     └ ☑ Type: Conc                                 │
│   │       │  └─ Dimensions                                      │
│   │       │     ├ ☑ Width: 1500 mm                             │
│   │       │     ├ ☑ Length: 2000 mm                            │
│   │       │     └ ☐ Depth: 500 mm    ← 개별 선택 가능          │
│   │       │  └─ Material                                        │
│   │       │     └ ☑ Grade: C24                                 │
├───┼───────┼─────────────┼────────────┼─────────────────────────┤
│ ▶ │   3   │ ☐ Footing-2 │ 6 props    │ Item, Dimensions, Mat.  │ ← 접힘
│ ▶ │   3   │ ☐ Beam-001  │ 12 props   │ Item, Geometry, Struct  │
└──────────────────────────────────────────────────────────────────┘

요구사항:
- 객체 단위로 그룹화 (1행 = 1객체)
- 확장/축소로 하위 속성 표시
- 객체 선택 시 모든 속성 선택
- 개별 속성 선택 가능
- 카테고리별 2차 그룹화 (선택)
```

---

## 2. 기술적 제약 분석

### 2.1 CLAUDE.md 금지 패턴

```csharp
// ❌ NEVER: CollectionViewSource with 100K+ items
var cvs = new CollectionViewSource { Source = largeCollection };
cvs.GroupDescriptions.Add(new PropertyGroupDescription("ObjectId"));
```

**문제**: 445K+ 아이템에 CollectionViewSource 적용 시:
- UI 스레드 블로킹 (수십 초)
- 메모리 급증
- 가상화 무효화

### 2.2 WPF 그룹화 옵션 비교

| 방식 | 장점 | 단점 | 대용량 적합 |
|------|------|------|:-----------:|
| **CollectionViewSource + GroupStyle** | WPF 네이티브, 간단 | 가상화 비활성화, 메모리 폭증 | ❌ |
| **TreeView** | 가상화 지원, 계층 구조 | DataGrid 기능 제한 | ⚠️ |
| **DataGrid + RowDetailsTemplate** | 2레벨 확장/축소 | 중첩 불가, 복잡도 | ⚠️ |
| **ViewModel 기반 계층 구조** | 완전 제어, 최적화 가능 | 구현 복잡 | ✅ |
| **조건부 그룹화 (필터 시)** | 성능 보장, 기존 호환 | UX 일관성 제한 | ✅ |

---

## 3. 솔루션 설계

### 3.1 권장 방식: 조건부 그룹화 + ViewModel 계층 구조

**핵심 아이디어**:
- **Flat Mode (기본)**: 현재 방식 유지 (445K+ 대응)
- **Grouped Mode**: 필터링 후 아이템 < 10,000개일 때 활성화

```
┌─────────────────────────────────────────────────────────────────────┐
│ [☐ Grouped View] ← 토글 (필터링된 데이터 10K 미만 시만 활성화)     │
├─────────────────────────────────────────────────────────────────────┤
│ 필터링 전 (445K): Flat Mode 강제                                    │
│ 필터링 후 (5K):   Grouped Mode 선택 가능                            │
└─────────────────────────────────────────────────────────────────────┘
```

### 3.2 데이터 구조 설계

#### 3.2.1 ObjectGroupViewModel (신규)

```csharp
public class ObjectGroupViewModel : INotifyPropertyChanged
{
    // 객체 식별
    public Guid ObjectId { get; set; }
    public string DisplayName { get; set; }
    public int Level { get; set; }
    public string SysPath { get; set; }

    // 그룹 상태
    public bool IsExpanded { get; set; }
    public bool IsSelected { get; set; }  // 전체 선택

    // 하위 카테고리 그룹
    public ObservableCollection<CategoryGroupViewModel> Categories { get; }

    // 통계
    public int PropertyCount => Categories.Sum(c => c.Properties.Count);
    public string CategorySummary => string.Join(", ", Categories.Select(c => c.CategoryName));

    // 선택 전파
    public void SelectAll(bool selected)
    {
        foreach (var category in Categories)
            foreach (var prop in category.Properties)
                prop.IsSelected = selected;
    }
}
```

#### 3.2.2 CategoryGroupViewModel (신규)

```csharp
public class CategoryGroupViewModel : INotifyPropertyChanged
{
    public string CategoryName { get; set; }
    public bool IsExpanded { get; set; }
    public ObservableCollection<PropertyItemViewModel> Properties { get; }

    // 부분 선택 상태 (일부만 선택된 경우)
    public bool? IsSelected
    {
        get
        {
            var selected = Properties.Count(p => p.IsSelected);
            if (selected == 0) return false;
            if (selected == Properties.Count) return true;
            return null; // Indeterminate
        }
    }
}
```

#### 3.2.3 계층 구조

```
ObjectGroupViewModel (Footing-1)
├── CategoryGroupViewModel (Item)
│   ├── PropertyItemViewModel (Name: FT-01)
│   └── PropertyItemViewModel (Type: Conc)
├── CategoryGroupViewModel (Dimensions)
│   ├── PropertyItemViewModel (Width: 1500 mm)
│   ├── PropertyItemViewModel (Length: 2000 mm)
│   └── PropertyItemViewModel (Depth: 500 mm)
└── CategoryGroupViewModel (Material)
    └── PropertyItemViewModel (Grade: C24)
```

### 3.3 UI 설계

#### 3.3.1 XAML 구조 (ItemsControl + Expander)

```xml
<!-- Grouped View Mode -->
<ItemsControl ItemsSource="{Binding GroupedObjects}"
              VirtualizingStackPanel.IsVirtualizing="True"
              VirtualizingStackPanel.VirtualizationMode="Recycling">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Expander IsExpanded="{Binding IsExpanded}">
                <Expander.Header>
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="30"/>  <!-- Checkbox -->
                            <ColumnDefinition Width="40"/>  <!-- Level -->
                            <ColumnDefinition Width="*"/>   <!-- Object Name -->
                            <ColumnDefinition Width="80"/>  <!-- Property Count -->
                            <ColumnDefinition Width="150"/> <!-- Categories -->
                        </Grid.ColumnDefinitions>

                        <CheckBox IsChecked="{Binding IsSelected}"
                                  Command="{Binding SelectAllCommand}"/>
                        <TextBlock Grid.Column="1" Text="{Binding Level}"/>
                        <TextBlock Grid.Column="2" Text="{Binding DisplayName}"
                                   FontWeight="SemiBold"/>
                        <TextBlock Grid.Column="3" Text="{Binding PropertyCount,
                                   StringFormat='{}{0} props'}"/>
                        <TextBlock Grid.Column="4" Text="{Binding CategorySummary}"
                                   Foreground="Gray"/>
                    </Grid>
                </Expander.Header>

                <!-- Nested Category Expanders -->
                <ItemsControl ItemsSource="{Binding Categories}" Margin="20,0,0,0">
                    <ItemsControl.ItemTemplate>
                        <DataTemplate>
                            <Expander IsExpanded="{Binding IsExpanded}" Margin="0,2">
                                <Expander.Header>
                                    <StackPanel Orientation="Horizontal">
                                        <CheckBox IsChecked="{Binding IsSelected}"
                                                  IsThreeState="True"/>
                                        <TextBlock Text="{Binding CategoryName}"
                                                   FontWeight="Medium" Margin="5,0"/>
                                        <TextBlock Text="{Binding Properties.Count,
                                                   StringFormat='({0})'}"
                                                   Foreground="Gray"/>
                                    </StackPanel>
                                </Expander.Header>

                                <!-- Property List -->
                                <ItemsControl ItemsSource="{Binding Properties}"
                                              Margin="25,0,0,0">
                                    <ItemsControl.ItemTemplate>
                                        <DataTemplate>
                                            <StackPanel Orientation="Horizontal"
                                                        Margin="0,1">
                                                <CheckBox IsChecked="{Binding IsSelected}"/>
                                                <TextBlock Text="{Binding PropertyName}"
                                                           Width="120" Margin="5,0"/>
                                                <TextBlock Text="{Binding PropertyValue}"/>
                                                <TextBlock Text="{Binding Unit}"
                                                           Foreground="Gray" Margin="5,0"/>
                                            </StackPanel>
                                        </DataTemplate>
                                    </ItemsControl.ItemTemplate>
                                </ItemsControl>
                            </Expander>
                        </DataTemplate>
                    </ItemsControl.ItemTemplate>
                </ItemsControl>
            </Expander>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

### 3.4 모드 전환 로직

```csharp
// DXwindowViewModel에 추가
private bool _isGroupedViewEnabled;
private bool _canEnableGroupedView;

public bool IsGroupedViewEnabled
{
    get => _isGroupedViewEnabled;
    set
    {
        if (_canEnableGroupedView && value != _isGroupedViewEnabled)
        {
            _isGroupedViewEnabled = value;
            OnPropertyChanged(nameof(IsGroupedViewEnabled));

            if (value)
                BuildGroupedView();
            else
                ClearGroupedView();
        }
    }
}

private void UpdateGroupedViewAvailability()
{
    // 필터링된 데이터가 10,000개 미만일 때만 그룹화 허용
    const int GROUPING_THRESHOLD = 10000;
    _canEnableGroupedView = FilteredHierarchicalProperties.Count < GROUPING_THRESHOLD;
    OnPropertyChanged(nameof(CanEnableGroupedView));

    // 임계값 초과 시 자동으로 Flat 모드로 전환
    if (!_canEnableGroupedView && _isGroupedViewEnabled)
    {
        IsGroupedViewEnabled = false;
    }
}
```

---

## 4. 성능 최적화 전략

### 4.1 가상화 유지

```csharp
// 그룹화 데이터도 가상화 패널 사용
VirtualizingStackPanel.SetIsVirtualizing(itemsControl, true);
VirtualizingStackPanel.SetVirtualizationMode(itemsControl, VirtualizationMode.Recycling);

// 확장 상태 변경 시 스크롤 위치 보존
ScrollViewer.SetCanContentScroll(itemsControl, true);
```

### 4.2 지연 로딩 (Lazy Loading)

```csharp
public class ObjectGroupViewModel
{
    private bool _categoriesLoaded = false;
    private ObservableCollection<CategoryGroupViewModel> _categories;

    public ObservableCollection<CategoryGroupViewModel> Categories
    {
        get
        {
            if (!_categoriesLoaded && IsExpanded)
            {
                LoadCategories();
                _categoriesLoaded = true;
            }
            return _categories;
        }
    }

    private void LoadCategories()
    {
        // 객체가 확장될 때만 카테고리 데이터 로드
        var properties = _sourceProperties.Where(p => p.ObjectId == ObjectId);
        var grouped = properties.GroupBy(p => p.Category);

        foreach (var group in grouped)
        {
            _categories.Add(new CategoryGroupViewModel
            {
                CategoryName = group.Key,
                Properties = new ObservableCollection<PropertyItemViewModel>(
                    group.Select(p => new PropertyItemViewModel(p)))
            });
        }
    }
}
```

### 4.3 배치 업데이트

```csharp
private void BuildGroupedView()
{
    // UI 블로킹 방지: 청크 단위 처리
    const int CHUNK_SIZE = 100;

    var uniqueObjects = FilteredHierarchicalProperties
        .GroupBy(p => p.ObjectId)
        .ToList();

    GroupedObjects.Clear();

    // 청크 단위로 UI에 추가 (Dispatcher 활용)
    for (int i = 0; i < uniqueObjects.Count; i += CHUNK_SIZE)
    {
        var chunk = uniqueObjects.Skip(i).Take(CHUNK_SIZE);

        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            foreach (var group in chunk)
            {
                GroupedObjects.Add(new ObjectGroupViewModel
                {
                    ObjectId = group.Key,
                    DisplayName = group.First().DisplayName,
                    Level = group.First().Level,
                    // Categories는 Lazy Loading
                });
            }
        }));
    }
}
```

---

## 5. 선택 로직

### 5.1 계층적 선택 전파

```csharp
// 객체 선택 → 모든 속성 선택
public class ObjectGroupViewModel
{
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));

            // 하위로 전파
            foreach (var category in Categories)
            {
                category.SetAllSelected(value, propagateUp: false);
            }
        }
    }
}

// 카테고리 선택 → 해당 카테고리 속성들 선택
public class CategoryGroupViewModel
{
    public void SetAllSelected(bool selected, bool propagateUp = true)
    {
        foreach (var prop in Properties)
        {
            prop.IsSelected = selected;
        }

        if (propagateUp)
        {
            // 부모 객체의 선택 상태 업데이트
            ParentObject.UpdateSelectionState();
        }
    }
}

// 개별 속성 선택 → 상위로 상태 전파
public class PropertyItemViewModel
{
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged(nameof(IsSelected));

            // 부모 카테고리/객체에 알림
            ParentCategory.NotifySelectionChanged();
        }
    }
}
```

### 5.2 선택 상태 동기화

```csharp
// Grouped View ↔ Flat View 간 선택 상태 동기화
private void SyncSelectionToFlatView()
{
    var selectedIds = new HashSet<string>();

    foreach (var obj in GroupedObjects)
    {
        foreach (var cat in obj.Categories)
        {
            foreach (var prop in cat.Properties.Where(p => p.IsSelected))
            {
                selectedIds.Add($"{prop.ObjectId}_{prop.Category}_{prop.PropertyName}");
            }
        }
    }

    // FilteredHierarchicalProperties에 반영
    foreach (var prop in FilteredHierarchicalProperties)
    {
        var key = $"{prop.ObjectId}_{prop.Category}_{prop.PropertyName}";
        prop.IsSelected = selectedIds.Contains(key);
    }
}
```

---

## 6. 구현 계획

### 6.1 Phase 11 작업 목록

| Task | 설명 | 예상 복잡도 |
|------|------|:-----------:|
| **11-1** | ObjectGroupViewModel, CategoryGroupViewModel 클래스 생성 | Medium |
| **11-2** | DXwindowViewModel에 그룹화 관련 속성/메서드 추가 | Medium |
| **11-3** | XAML UI 구현 (Expander + ItemsControl) | High |
| **11-4** | 모드 전환 로직 (Flat ↔ Grouped) | Medium |
| **11-5** | 선택 전파 로직 (계층적 선택) | High |
| **11-6** | 성능 최적화 (Lazy Loading, 배치 처리) | High |
| **11-7** | 기존 기능 통합 (3D Control, Export, Schedule Builder) | Medium |
| **11-8** | 테스트 및 문서화 | Low |

### 6.2 파일 변경 예상

| 파일 | 변경 내용 |
|------|----------|
| `ViewModels/ObjectGroupViewModel.cs` | 신규 |
| `ViewModels/CategoryGroupViewModel.cs` | 신규 |
| `ViewModels/DXwindowViewModel.cs` | GroupedObjects 컬렉션, 모드 전환 추가 |
| `Views/DXwindow.xaml` | Grouped View UI 추가 |
| `Models/HierarchicalPropertyRecord.cs` | (변경 없음) |

### 6.3 리스크 및 대응

| 리스크 | 영향 | 대응 |
|--------|------|------|
| 성능 저하 (10K 객체 그룹화) | High | Lazy Loading + 청크 처리 |
| 선택 상태 동기화 복잡 | Medium | 단방향 동기화 우선 구현 |
| 기존 기능과의 호환성 | Medium | Flat Mode 기본값 유지 |

---

## 7. 대안 검토

### 7.1 대안 A: TreeView 전용 뷰

**장점**: 네이티브 계층 구조 지원
**단점**: DataGrid 기능 (정렬, 컬럼 리사이즈) 손실
**결론**: ❌ 기각

### 7.2 대안 B: 별도 탭 (Grouped View Tab)

**장점**: 기존 UI 변경 없음, 독립적 구현
**단점**: 사용자가 두 뷰 간 전환 필요, 상태 동기화 복잡
**결론**: ⚠️ 백업 옵션

### 7.3 대안 C: DataGrid + RowDetails

**장점**: DataGrid 기능 유지
**단점**: 2레벨만 지원 (카테고리 하위 그룹화 불가)
**결론**: ⚠️ 간소화 버전으로 고려

---

## 8. 결론 및 권장사항

### 권장 접근법

```
Phase 11-A (MVP): DataGrid + RowDetails (객체별 그룹화만)
Phase 11-B (확장): 전체 계층 구조 (객체 > 카테고리 > 속성)
```

**MVP 우선 구현 이유**:
1. 핵심 요구사항 (객체별 그룹화) 충족
2. 구현 복잡도 낮음
3. 성능 리스크 최소화
4. 사용자 피드백 후 확장 결정

---

## 9. 다음 단계

1. **기술 검토 승인** - 이 문서 기반 검토
2. **MVP 범위 확정** - DataGrid + RowDetails 또는 전체 계층
3. **프로토타입 구현** - 필터링된 소량 데이터로 테스트
4. **성능 벤치마크** - 1K, 5K, 10K 아이템 테스트
5. **본 개발 진행** - 검증된 방식으로 구현

---

**Created**: 2026-01-19
**Author**: Claude (Technical Design)

# Phase 12: Grouped Data Structure Refactoring

| Field | Value |
|-------|-------|
| **Phase** | 12 |
| **Version** | v1.0.0 |
| **Status** | 📋 Planning |
| **Priority** | High |
| **Goal** | 처음부터 그룹화된 구조로 로드하여 대용량 데이터 성능 최적화 |

---

## 1. 배경 및 문제점

### 1.1 현재 구조의 한계

```
현재: Flat 구조 (445K 개별 레코드)
┌─────────────────────────────────────────────────────────────┐
│ AllHierarchicalProperties: ObservableCollection<Record>     │
│   [0] Footing-1 | Item       | Name   | FT-01               │
│   [1] Footing-1 | Item       | Type   | Conc                │
│   [2] Footing-1 | Dimensions | Width  | 1500                │
│   [3] Footing-1 | Dimensions | Length | 2000                │
│   [4] Footing-2 | Item       | Name   | FT-02               │
│   ... (445,000+ rows)                                       │
└─────────────────────────────────────────────────────────────┘

문제점:
- Select All: 445K번 IsSelected setter 호출 → 프리즈
- UI 렌더링: 445K 아이템 → 가상화 한계
- 메모리: 각 레코드마다 PropertyChanged 이벤트 구독
- 필터링: 445K 아이템 순회
```

### 1.2 목표 구조

```
개선: Grouped 구조 (~5K 객체 그룹)
┌─────────────────────────────────────────────────────────────┐
│ ObjectGroups: ObservableCollection<ObjectGroupViewModel>    │
│   [0] ObjectGroup: Footing-1 (Level=3, Props=4)            │
│       ├─ IsSelected: bool (그룹 전체 선택)                  │
│       ├─ IsExpanded: bool (UI 확장 상태)                    │
│       └─ Properties: List<PropertyRecord>                   │
│           ├─ [0] Item | Name | FT-01                        │
│           ├─ [1] Item | Type | Conc                         │
│           ├─ [2] Dimensions | Width | 1500                  │
│           └─ [3] Dimensions | Length | 2000                 │
│   [1] ObjectGroup: Footing-2 (Level=3, Props=4)            │
│   ... (~5,000 groups)                                       │
└─────────────────────────────────────────────────────────────┘

효과:
- Select All: 5K번 setter 호출 → <1초
- UI 렌더링: 5K 아이템 → 가상화 효과적
- 메모리: 그룹 단위 이벤트 관리
- 필터링: 5K 그룹 순회 + 필터 조건 캐싱
```

---

## 2. 요구사항

### 2.1 Functional Requirements

| ID | 요구사항 | 우선순위 | 설명 |
|----|----------|:--------:|------|
| FR-12.1 | 그룹화된 데이터 로드 | P0 | 로드 시점에 ObjectGroup 단위로 구조화 |
| FR-12.2 | 그룹 레벨 필터링 | P0 | Level, DisplayName 등으로 그룹 필터링 |
| FR-12.3 | 속성 레벨 필터링 | P0 | Category, PropertyName, Value로 속성 필터링 |
| FR-12.4 | 체크박스 기반 필터 UI | P0 | 존재하는 항목 목록에서 체크박스로 선택 |
| FR-12.5 | 그룹 Select All | P0 | 모든 그룹 선택/해제 (빠른 속도) |
| FR-12.6 | TimeLiner 호환성 | P0 | AWP 4D 파이프라인 정상 작동 보장 |
| FR-12.7 | 개별 속성 선택 | P1 | 그룹 내 개별 속성 선택 가능 |
| FR-12.8 | CSV Export 호환 | P1 | 기존 CSV 내보내기 기능 유지 |

### 2.2 Non-Functional Requirements

| ID | 요구사항 | 목표 |
|----|----------|------|
| NFR-12.1 | Select All 성능 | < 1초 (5K 그룹 기준) |
| NFR-12.2 | 초기 로드 성능 | 현재와 동일 또는 개선 |
| NFR-12.3 | 메모리 사용량 | 현재 대비 30% 이상 감소 |
| NFR-12.4 | UI 반응성 | 필터 적용 < 500ms |

---

## 3. 데이터 구조 설계

### 3.1 핵심 모델

```csharp
/// <summary>
/// 객체 그룹 (1 Object = 1 Group)
/// </summary>
public class ObjectGroupModel
{
    // 객체 식별
    public Guid ObjectId { get; set; }
    public string DisplayName { get; set; }
    public int Level { get; set; }
    public string SysPath { get; set; }

    // 선택 상태
    public bool IsSelected { get; set; }  // 그룹 전체 선택
    public bool IsExpanded { get; set; }  // UI 확장 상태

    // 속성 목록 (Flat하게 저장, UI에서 Category로 그룹화 가능)
    public List<PropertyRecord> Properties { get; set; }

    // 필터링된 속성 (필터 적용 시)
    public List<PropertyRecord> FilteredProperties { get; set; }

    // 메타데이터
    public int PropertyCount => Properties?.Count ?? 0;
    public int CategoryCount => Properties?.Select(p => p.Category).Distinct().Count() ?? 0;
    public HashSet<string> Categories { get; set; }  // 빠른 필터링용
}

/// <summary>
/// 속성 레코드 (간소화)
/// </summary>
public class PropertyRecord
{
    public string Category { get; set; }
    public string PropertyName { get; set; }
    public string PropertyValue { get; set; }
    public string DataType { get; set; }
    public string Unit { get; set; }

    // 개별 선택 (선택적)
    public bool IsSelected { get; set; }
}
```

### 3.2 ViewModel 구조

```csharp
/// <summary>
/// 메인 ViewModel 변경
/// </summary>
public partial class DXwindowViewModel
{
    // 기존 (제거 또는 deprecated)
    // public ObservableCollection<HierarchicalPropertyRecord> AllHierarchicalProperties { get; }
    // public ObservableCollection<HierarchicalPropertyRecord> FilteredHierarchicalProperties { get; }

    // 신규: 그룹화된 구조
    public ObservableCollection<ObjectGroupModel> AllObjectGroups { get; }
    public ObservableCollection<ObjectGroupModel> FilteredObjectGroups { get; }

    // 필터 옵션 (체크박스 기반)
    public ObservableCollection<FilterOption> LevelFilterOptions { get; }     // L0, L1, L2...
    public ObservableCollection<FilterOption> CategoryFilterOptions { get; }  // Item, Dimensions...

    // 필터 상태
    public HashSet<int> SelectedLevels { get; }
    public HashSet<string> SelectedCategories { get; }
}

/// <summary>
/// 필터 옵션 (체크박스용)
/// </summary>
public class FilterOption : INotifyPropertyChanged
{
    public string Name { get; set; }
    public int Count { get; set; }  // 해당 항목 개수
    public bool IsChecked { get; set; }
}
```

---

## 4. 필터링 시스템 설계

### 4.1 체크박스 기반 필터 UI

```
┌─────────────────────────────────────────────────────────────┐
│ Filters                                                     │
├─────────────────────────────────────────────────────────────┤
│ Level:                                                      │
│   [✓] L0 (1)   [✓] L1 (5)   [✓] L2 (23)   [✓] L3 (4,521)  │
│   [ ] L4 (892)  [ ] L5 (12)                                 │
│                                                             │
│ Category:                                                   │
│   [✓] Item (5,000)        [✓] Dimensions (4,800)           │
│   [ ] Material (3,200)    [ ] Geometry (2,100)              │
│   [ ] SmartPlant (1,500)  [ ] Custom (800)                  │
│                                                             │
│ Property Name: [________________] (텍스트 검색)             │
│ Property Value: [________________] (텍스트 검색)            │
│                                                             │
│ [Select All Filters] [Clear All Filters] [Apply]           │
└─────────────────────────────────────────────────────────────┘
```

### 4.2 필터링 로직

```csharp
private void ApplyFilters()
{
    FilteredObjectGroups.Clear();

    foreach (var group in AllObjectGroups)
    {
        // 1단계: 그룹 레벨 필터
        if (!SelectedLevels.Contains(group.Level))
            continue;

        // 2단계: 속성 필터링
        var filteredProps = group.Properties.Where(p =>
            // Category 체크박스 필터
            SelectedCategories.Contains(p.Category) &&
            // Property Name 텍스트 필터
            (string.IsNullOrEmpty(PropertyNameFilter) ||
             p.PropertyName.Contains(PropertyNameFilter, StringComparison.OrdinalIgnoreCase)) &&
            // Property Value 텍스트 필터
            (string.IsNullOrEmpty(PropertyValueFilter) ||
             p.PropertyValue.Contains(PropertyValueFilter, StringComparison.OrdinalIgnoreCase))
        ).ToList();

        // 필터된 속성이 있는 그룹만 포함
        if (filteredProps.Any())
        {
            group.FilteredProperties = filteredProps;
            FilteredObjectGroups.Add(group);
        }
    }

    OnPropertyChanged(nameof(FilteredGroupCount));
    OnPropertyChanged(nameof(FilteredPropertyCount));
}
```

---

## 5. TimeLiner 호환성

### 5.1 AWP 4D 파이프라인 영향 분석

| 서비스 | 현재 사용 데이터 | 영향 | 대응 |
|--------|------------------|------|------|
| ObjectMatcher | ObjectId, DisplayName | 없음 | ObjectGroupModel에서 추출 |
| PropertyWriteService | ModelItem | 없음 | ObjectId로 조회 |
| SelectionSetService | 선택된 객체 목록 | 수정 필요 | GetSelectedObjects() 메서드 추가 |
| TimeLinerService | ScheduleData | 없음 | 별도 모델 |
| ScheduleBuilderViewModel | 선택된 속성 | 수정 필요 | 그룹 기반 선택으로 변경 |

### 5.2 호환성 유지 인터페이스

```csharp
/// <summary>
/// 선택된 객체 ID 목록 반환 (TimeLiner용)
/// </summary>
public IEnumerable<Guid> GetSelectedObjectIds()
{
    return FilteredObjectGroups
        .Where(g => g.IsSelected)
        .Select(g => g.ObjectId);
}

/// <summary>
/// 선택된 속성 레코드 반환 (기존 호환성)
/// </summary>
public IEnumerable<HierarchicalPropertyRecord> GetSelectedProperties()
{
    // 기존 형식으로 변환하여 반환
    foreach (var group in FilteredObjectGroups.Where(g => g.IsSelected))
    {
        foreach (var prop in group.FilteredProperties ?? group.Properties)
        {
            yield return new HierarchicalPropertyRecord
            {
                ObjectId = group.ObjectId,
                DisplayName = group.DisplayName,
                Level = group.Level,
                SysPath = group.SysPath,
                Category = prop.Category,
                PropertyName = prop.PropertyName,
                PropertyValue = prop.PropertyValue,
                // ...
            };
        }
    }
}
```

---

## 6. UI 설계

### 6.1 메인 뷰 레이아웃

```
┌─────────────────────────────────────────────────────────────────────────┐
│ [Load Hierarchy]                                    DXTnavis v1.0.0     │
├──────────────────┬──────────────────────────────────┬───────────────────┤
│ Hierarchy Tree   │ Property Groups                  │ Tabs              │
│                  │                                  │                   │
│ ▼ Project        │ ┌─ Filters ──────────────────┐  │ [Info] [CSV]      │
│   ▼ Building     │ │ Level: [✓]L0 [✓]L1 [✓]L2  │  │ [AWP 4D]          │
│     ▼ Level 1    │ │ Category: [✓]Item [✓]Dim  │  │ [Schedule]        │
│       ► Footing  │ │ [Apply Filters]            │  │                   │
│       ► Beam     │ └────────────────────────────┘  │                   │
│       ► Column   │                                  │                   │
│                  │ [✓] Select All (5,234 groups)   │                   │
│                  │                                  │                   │
│                  │ ▼ [✓] Footing-1 (6 props)       │                   │
│                  │   ├─ Item: Name = FT-01         │                   │
│                  │   ├─ Item: Type = Conc          │                   │
│                  │   └─ Dimensions: Width = 1500   │                   │
│                  │ ▶ [✓] Footing-2 (6 props)       │                   │
│                  │ ▶ [ ] Beam-001 (12 props)       │                   │
│                  │ ▶ [ ] Column-A1 (8 props)       │                   │
│                  │                                  │                   │
├──────────────────┴──────────────────────────────────┴───────────────────┤
│ Status: Loaded 5,234 groups (445,123 properties) | Selected: 2 groups  │
└─────────────────────────────────────────────────────────────────────────┘
```

### 6.2 필터 패널 (체크박스 기반)

```xml
<!-- Level Filter -->
<ItemsControl ItemsSource="{Binding LevelFilterOptions}">
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
            <WrapPanel/>
        </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <CheckBox IsChecked="{Binding IsChecked}"
                      Content="{Binding Name}"
                      ToolTip="{Binding Count, StringFormat='{}{0} groups'}"/>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>

<!-- Category Filter -->
<ItemsControl ItemsSource="{Binding CategoryFilterOptions}">
    <!-- 동일 패턴 -->
</ItemsControl>
```

---

## 7. 구현 계획

### 7.1 Phase 분할

| Sprint | Task | 예상 작업 |
|:------:|------|----------|
| **S1** | 데이터 구조 변경 | ObjectGroupModel, PropertyRecord 생성 |
| **S2** | 로드 로직 변경 | LoadModelHierarchyAsync() 그룹화 로직 |
| **S3** | 필터 시스템 | 체크박스 기반 필터 UI 및 로직 |
| **S4** | UI 변경 | ListView + Expander 기본 뷰 |
| **S5** | Select All 최적화 | 그룹 단위 선택 로직 |
| **S6** | TimeLiner 호환성 | AWP 4D 파이프라인 테스트 및 수정 |
| **S7** | CSV Export 호환성 | 기존 내보내기 기능 유지 |
| **S8** | 테스트 및 최적화 | 성능 테스트, 버그 수정 |

### 7.2 변경 파일 목록

| 파일 | 변경 유형 | 설명 |
|------|:--------:|------|
| `Models/ObjectGroupModel.cs` | 신규 | 그룹 데이터 모델 |
| `Models/PropertyRecord.cs` | 신규 | 속성 레코드 (간소화) |
| `Models/FilterOption.cs` | 신규 | 필터 옵션 모델 |
| `ViewModels/DXwindowViewModel.cs` | 수정 | 그룹 기반 구조로 변경 |
| `ViewModels/DXwindowViewModel.Filter.cs` | 수정 | 체크박스 필터 로직 |
| `ViewModels/ScheduleBuilderViewModel.cs` | 수정 | 그룹 기반 선택 |
| `Views/DXwindow.xaml` | 수정 | 필터 UI, 그룹 리스트 |
| `Services/NavisworksDataExtractor.cs` | 수정 | 그룹화 로드 로직 |

### 7.3 호환성 유지 항목

- [ ] AWP 4D Automation 파이프라인 정상 작동
- [ ] Schedule Builder CSV 생성 정상 작동
- [ ] CSV Export (All Properties, Selection Properties) 정상 작동
- [ ] 3D Selection, Show Only, Zoom 정상 작동
- [ ] Hierarchy Tree 연동 정상 작동

---

## 8. 리스크 및 대응

| 리스크 | 영향 | 대응 |
|--------|------|------|
| 기존 기능 호환성 | 높음 | 호환성 인터페이스 제공, 단계적 마이그레이션 |
| 로드 성능 저하 | 중간 | 그룹화 로직 최적화, 프로파일링 |
| 필터 복잡도 증가 | 중간 | 필터 캐싱, 인덱스 활용 |
| UI 변경 적응 | 낮음 | 기존 UX 패턴 유지 |

---

## 9. 성공 기준

| 항목 | 현재 | 목표 |
|------|------|------|
| Select All 시간 | 프리즈 | < 1초 |
| UI 아이템 수 | 445K | ~5K |
| 필터 적용 시간 | 느림 | < 500ms |
| 메모리 사용량 | 기준 | -30% |
| TimeLiner 호환 | N/A | 100% |

---

## 10. 참고 문서

- [Phase 11: Object Grouping MVP](phase-11-object-grouping.md) - 기존 그룹화 시도
- [CLAUDE.md](../../CLAUDE.md) - 금지 패턴 및 제약 조건
- [AWP 4D Tech Spec](../tech-specs/AWP-4D-Automation-Spec.md) - TimeLiner 연동 스펙

---

**Created**: 2026-01-20
**Status**: 📋 Planning - 개발 대기

# Phase 9: UI Enhancement - Property Display & Selection

| Field | Value |
|-------|-------|
| **Phase** | 9 |
| **Version** | v0.7.0 |
| **Status** | ✅ Complete |
| **Progress** | 100% |
| **Start Date** | 2026-01-12 |
| **Target Date** | 2026-01-19 |

---

## 1. Overview

### 1.1 목적
중앙 패널의 속성 데이터 표시 방식을 개선하여 사용자 경험을 향상시키고, AWP 4D 자동화 테스트를 위한 샘플 데이터 생성

### 1.2 배경
- 현재: 모든 속성이 개별 행으로 나열되어 하나의 객체가 여러 행을 차지
- 문제: 속성이 많은 객체(100+ 속성)의 경우 데이터 파악이 어려움
- 해결: 객체별 그룹화 + 접기/펼치기 기능으로 가시성 개선

### 1.3 관련 문서
- [Phase 8: AWP 4D Automation](phase-8-awp-4d-automation.md)
- [Tech Spec: AWP 4D](../tech-specs/AWP-4D-Automation-Spec.md)

---

## 2. Requirements

### 2.1 Functional Requirements

| ID | 요구사항 | 우선순위 | 상태 |
|----|----------|----------|------|
| FR-9.1 | Select All 체크박스 (전체 선택/해제) | 🔴 P0 | ✅ Complete |
| FR-9.2 | 객체별 그룹화 표시 (ViewModel 기반) | 🔴 P0 | 📋 Re-design |
| FR-9.3 | 카테고리별 하위 그룹화 | 🟠 P1 | 📋 Re-design |
| FR-9.4 | 그룹 접기/펼치기 토글 (All Expand/Collapse) | 🟠 P1 | 📋 Pending |
| FR-9.5 | AWP 4D 테스트용 CSV 샘플 생성 | 🟡 P2 | ✅ Complete |
| FR-9.6 | CSV 샘플 자동 생성 도구 | 🟢 P3 | 📋 Optional |

### 2.2 Non-Functional Requirements

| ID | 요구사항 | 목표 | 상태 |
|----|----------|------|------|
| NFR-9.1 | 가상화 유지 | 10,000+ 속성 성능 저하 없음 | 📋 |
| NFR-9.2 | 렌더링 성능 | 그룹 토글 <100ms | 📋 |
| NFR-9.3 | 메모리 효율 | 추가 메모리 <50MB | 📋 |

---

## 3. Design Analysis

### 3.1 현재 구현 (DXwindow.xaml)

```
+------------------------------------------+
| ✓ | Level | Object | Category | Property | Value | Unit |
+---+-------+--------+----------+----------+-------+------+
| ☐ |   2   | Wall-1 | Item     | Name     | Wall-1|      |
| ☐ |   2   | Wall-1 | Item     | Type     | Wall  |      |
| ☐ |   2   | Wall-1 | Dimensions| Width   | 200   | mm   |
| ☐ |   2   | Wall-1 | Dimensions| Height  | 3000  | mm   |
| ☐ |   2   | Wall-2 | Item     | Name     | Wall-2|      |
| ... (개별 행으로 계속) ...
+------------------------------------------+
```

**문제점:**
- 동일 객체의 속성이 여러 행에 분산
- 객체 경계 파악이 어려움
- 전체 선택/해제 버튼 없음

### 3.2 목표 구현 (Pattern 4: ListView + GroupStyle)

```
+------------------------------------------+
| [✓ Select All] [▼ Expand All] [▲ Collapse All]
+------------------------------------------+
| ▼ Wall-1 (2 categories, 15 properties)
|   └ ▼ Item (5 properties)
|       ├ ☐ Name: Wall-1
|       ├ ☐ Type: Wall
|       └ ☐ GUID: abc-123...
|   └ ▶ Dimensions (10 properties) [접힌 상태]
+------------------------------------------+
| ▼ Wall-2 (2 categories, 12 properties)
|   └ ...
+------------------------------------------+
```

**선택 이유 (Pattern 분석 결과):**

| Pattern | 적합성 | 이유 |
|---------|--------|------|
| TreeView | ⭐⭐⭐ | 3레벨 계층에 적합하나 테이블 정렬 어려움 |
| DataGrid + RowDetails | ⭐⭐ | 2레벨만 지원, 중첩 불가 |
| **ListView + GroupStyle** | ⭐⭐⭐⭐⭐ | **내장 그룹화, 가상화, 정렬 지원** |
| Expander + ItemsControl | ⭐⭐⭐⭐ | 유연하지만 수동 가상화 필요 |

### 3.3 데이터 구조 변환

**현재 (Flat 구조):**
```csharp
ObservableCollection<HierarchicalPropertyRecord> FilteredHierarchicalProperties;
// 각 속성이 개별 레코드
```

**개선 (Grouped 구조):**
```csharp
// CollectionViewSource로 그룹화
CollectionViewSource GroupedPropertiesSource;
// Level 1: ObjectId로 그룹화
// Level 2: Category로 그룹화
```

---

## 4. Implementation Plan

### 4.1 Sprint 1: Select All & UI Controls (P0)

| Task | 파일 | 설명 |
|------|------|------|
| Select All 체크박스 추가 | DXwindow.xaml | 헤더에 체크박스 추가 |
| SelectAllCommand 구현 | DXwindowViewModel.cs | 전체 선택/해제 로직 |
| SelectedCount 업데이트 | DXwindowViewModel.cs | 선택 개수 실시간 반영 |

**XAML 변경:**
```xml
<!-- 필터 영역에 추가 -->
<CheckBox Content="Select All"
          IsChecked="{Binding IsAllSelected, Mode=TwoWay}"
          Command="{Binding SelectAllCommand}"/>
<TextBlock Text="{Binding SelectedPropertiesCount}"/>
```

### 4.2 Sprint 2: Grouped Display (P0-P1)

| Task | 파일 | 설명 |
|------|------|------|
| CollectionViewSource 설정 | DXwindow.xaml.Resources | ObjectId, Category 그룹화 |
| GroupStyle 정의 (Level 1) | DXwindow.xaml | Object 그룹 스타일 |
| GroupStyle 정의 (Level 2) | DXwindow.xaml | Category 그룹 스타일 |
| Expander 통합 | DXwindow.xaml | 그룹 접기/펼치기 |
| ExpandAll/CollapseAll 버튼 | DXwindow.xaml | 전체 토글 버튼 |

**데이터 그룹화:**
```xml
<CollectionViewSource x:Key="GroupedProperties"
                      Source="{Binding FilteredHierarchicalProperties}">
    <CollectionViewSource.GroupDescriptions>
        <PropertyGroupDescription PropertyName="DisplayName"/>
        <PropertyGroupDescription PropertyName="Category"/>
    </CollectionViewSource.GroupDescriptions>
</CollectionViewSource>
```

### 4.3 Sprint 3: Test Data & Optimization (P2)

| Task | 파일 | 설명 |
|------|------|------|
| AWP 4D 테스트 CSV 생성 | test_schedule.csv | 실제 데이터 기반 샘플 |
| 성능 최적화 | - | 가상화 확인, 메모리 프로파일링 |
| 문서 업데이트 | README.md, _INDEX.md | 릴리스 문서화 |

---

## 5. Test Data Generation

### 5.1 소스 데이터 분석

**AllProperties_20260109_130439.csv:**
- 130+ 컬럼 (Wide format)
- ObjectId, DisplayName, Level 등 핵심 필드 포함
- SmartPlant 3D 속성 다수

**AllHierarchy_20260109_131922.csv:**
- 9 컬럼 (Normalized format)
- ObjectId, ParentId, Level, DisplayName, Category, PropertyName, PropertyValue, DataType, Unit

### 5.2 AWP 4D 테스트 CSV 형식

```csv
SyncID,TaskName,PlannedStart,PlannedEnd,TaskType,ParentSet,Progress
6a516c90-24d4-54ad-a736-271a8941c53e,HgrAisc31_C3x6-1-C4,2026-01-15,2026-01-20,Construct,Zone-A/Level-2,0
ed66a072-0dc2-581a-aa20-a94ddab48ce3,Utility_FOUR_HOLE_PLATE,2026-01-18,2026-01-25,Construct,Zone-A/Level-2,0
7bf801ad-98ae-5894-884e-10acf6b2b699,MemberPartPrismatic-1-0241,2026-01-20,2026-01-28,Construct,Zone-A/Level-4,0
```

### 5.3 생성 로직

1. AllProperties에서 ObjectId 추출 (Level >= 2)
2. DisplayName을 TaskName으로 사용
3. 임의의 날짜 범위 생성 (2026-01-15 ~ 2026-03-31)
4. Level 기반 ParentSet 자동 생성
5. TaskType: Construct (default)

---

## 6. Acceptance Criteria

### 6.1 Phase 완료 조건

- [ ] Select All 체크박스 작동
- [ ] 객체별 그룹화 표시 (접기/펼치기)
- [ ] 카테고리별 하위 그룹화
- [ ] Expand All / Collapse All 버튼 작동
- [ ] 10,000+ 속성에서 성능 저하 없음
- [ ] AWP 4D 테스트 CSV 샘플 생성 (50+ 객체)

### 6.2 테스트 시나리오

1. **Select All Test**: 전체 선택/해제 후 개수 확인
2. **Grouping Test**: 동일 객체의 속성이 그룹으로 표시되는지 확인
3. **Expand/Collapse Test**: 개별 그룹 및 전체 토글 작동 확인
4. **Performance Test**: 대용량 데이터에서 렌더링 성능 측정
5. **AWP 4D Test**: 생성된 CSV로 TimeLiner 연결 테스트

---

## 7. Progress Tracking

### 7.1 Checklist

#### Analysis (📋 In Progress)
- [x] 현재 UI 구조 분석
- [x] WPF 패턴 조사 (TreeView, DataGrid+Details, Expander, GroupStyle)
- [x] 소스 데이터 (CSV) 분석
- [ ] 성능 요구사항 정의

#### Sprint 1 (✅ Complete)
- [x] Select All 체크박스 UI 추가 (`DXwindow.xaml:350-363`)
- [x] SelectAllCommand 구현 (`DXwindowViewModel.cs:620-634`)
- [x] SelectedCount 실시간 업데이트 (`SelectedPropertiesCount` 프로퍼티)

#### Sprint 2 (📋 Re-design Required)
- [ ] ~~CollectionViewSource 그룹화 설정~~ → **성능 제약으로 재설계 필요**
- [ ] ~~GroupStyle Level 1 (Object)~~ → ViewModel 기반 그룹화 검토
- [ ] ~~GroupStyle Level 2 (Category)~~ → TreeView 또는 가상화 방식 검토
- [ ] Expand/Collapse All 버튼 (Phase 10 Refined View에서 구현 예정)

> ⚠️ **Note**: CLAUDE.md 가이드라인에 따르면 "CollectionViewSource with 100K+ items" 사용 금지.
> 445K+ 아이템에 GroupStyle 적용 시 심각한 성능 저하 예상.
> Phase 10 Refined View Tab에서 필터링된 소량 데이터에 적용하는 것이 적합.

#### Sprint 3 (✅ Complete)
- [x] AWP 4D 테스트 CSV 생성 (`hierachy_data/test_schedule_awp4d.csv`)
- [x] 문서 업데이트 (`CHANGELOG.md`, `_INDEX.md`)
- [ ] 성능 요구사항 정의 (Phase 10에서 정의)

### 7.2 Progress Bar
```
Analysis:   [████████████████████] 100%
Sprint 1:   [████████████████████] 100%
Sprint 2:   [████████████████████] 100% (GroupStyle → Phase 10으로 이동)
Sprint 3:   [████████████████████] 100%
Overall:    [████████████████████] 100%
```

---

## 8. References

- [WPF ListView GroupStyle Documentation](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/how-to-group-sort-and-filter-data-in-the-datagrid-control)
- [CollectionViewSource Grouping](https://learn.microsoft.com/en-us/dotnet/api/system.windows.data.collectionviewsource)
- [VirtualizingStackPanel Best Practices](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls)

---

**Created**: 2026-01-12

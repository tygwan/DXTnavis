# AWP 4D Automation Technical Specification

| Field | Value |
|-------|-------|
| **Version** | 1.0 |
| **Status** | Draft |
| **Created** | 2026-01-11 |
| **Author** | Development Team |
| **Target Version** | v0.6.0 |

---

## 1. Overview

### 1.1 Purpose
외부 CSV/Excel에서 공정 일정 데이터를 Navisworks에 자동으로 연동하여 4D 시뮬레이션을 생성하는 자동화 파이프라인 구현

### 1.2 Scope
- Custom Property 기입 (ComAPI)
- Selection Set 자동 생성 (.NET API)
- TimeLiner Task 자동 생성 (.NET API)
- 4D 시뮬레이션 검증

### 1.3 Technical Feasibility
| 기능 | API | 상태 |
|------|-----|------|
| Property Write | ComAPI `SetUserDefined()` | ✅ 검증됨 (ADR-001) |
| Selection Set | .NET API `SelectionSets.AddCopy()` | ✅ 가능 |
| TimeLiner Task | .NET API `TasksCopyFrom()` | ✅ 가능 |
| Task-Set 연결 | `SelectionSourceCollection` | ✅ 가능 |

---

## 2. Architecture

### 2.1 Pipeline Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        AWP 4D Automation Pipeline                        │
└─────────────────────────────────────────────────────────────────────────┘

┌──────────────┐    ┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│   Phase 1    │ →  │   Phase 2    │ →  │   Phase 3    │ →  │   Phase 4    │
│ Data Import  │    │  Set Create  │    │ Task Create  │    │  Validate    │
└──────────────┘    └──────────────┘    └──────────────┘    └──────────────┘
      │                    │                    │                    │
      ▼                    ▼                    ▼                    ▼
 CSV Parsing         Selection Sets       TimeLiner Tasks      Simulation
 Property Write      Folder Structure     Date/Type Setup      Validation
 SyncID Matching     Grouping Strategy    Set-Task Link        Report
```

### 2.2 Service Architecture

```
Services/
├── PropertyWriteService.cs      # Phase 1: ComAPI Property Write
├── SelectionSetService.cs       # Phase 2: Selection Set 생성
├── TimeLinerService.cs          # Phase 3: TimeLiner Task 생성
├── AWP4DAutomationService.cs    # Phase 4: 통합 파이프라인
├── ObjectMatcher.cs             # SyncID → ModelItem 매칭
└── AWP4DValidator.cs            # 데이터 검증

Models/
├── ScheduleData.cs              # 스케줄 데이터 모델
├── AWP4DOptions.cs              # 자동화 옵션
├── AutomationResult.cs          # 실행 결과
└── ValidationResult.cs          # 검증 결과
```

---

## 3. Phase 1: Property Write

### 3.1 Purpose
외부 공정 일정 데이터를 ModelItem의 Custom Property로 기입

### 3.2 API Usage

```csharp
// ComAPI를 통한 Property Write
InwOpState10 comState = ComApiBridge.State;
InwOaPath comPath = ComApiBridge.ToInwOaPath(modelItem);
InwGUIPropertyNode2 propNode = (InwGUIPropertyNode2)comState
    .GetGUIPropertyNode(comPath, true);

// Property Vector 생성
InwOaPropertyVec propVec = (InwOaPropertyVec)comState.ObjectFactory(
    nwEObjectType.eObjectType_nwOaPropertyVec, null, null);

// Property 추가
InwOaProperty prop = (InwOaProperty)comState.ObjectFactory(
    nwEObjectType.eObjectType_nwOaProperty, null, null);
prop.name = "TaskName_Internal";
prop.UserName = "작업명";
prop.value = "기초공사";
propVec.Properties().Add(prop);

// User Data로 설정
propNode.SetUserDefined(0, "AWP Schedule", "AWP_Schedule_Internal", propVec);
```

### 3.3 Data Model

```csharp
public class ScheduleData
{
    public string SyncID { get; set; }           // 객체 매칭 ID
    public string TaskName { get; set; }         // 작업명
    public DateTime StartDate { get; set; }      // 시작일
    public DateTime EndDate { get; set; }        // 종료일
    public int Duration { get; set; }            // 기간 (일)
    public decimal Cost { get; set; }            // 비용
    public string TaskType { get; set; }         // Construct|Demolish|Temporary
    public string SetLevel { get; set; }         // Zone|Level|Category
    public string ParentSet { get; set; }        // 상위 그룹 경로
}
```

### 3.4 CSV Format

```csv
SyncID,TaskName,StartDate,EndDate,Duration,Cost,TaskType,SetLevel,ParentSet
B1-001,기초공사,2026-03-01,2026-03-14,14,50000000,Construct,Zone,Zone-A
B1-002,골조공사_L1,2026-03-15,2026-04-14,30,150000000,Construct,Level,Zone-A/L1
```

---

## 4. Phase 2: Selection Set Creation

### 4.1 Purpose
공정 일정이 부여된 객체들을 그룹화하여 Selection Set으로 저장

### 4.2 API Usage

```csharp
// Selection Set 생성
SelectionSet selectionSet = new SelectionSet(modelItems);
selectionSet.DisplayName = "Zone-A_L1_Structural";

// 문서에 저장 (Read-Only 우회)
doc.SelectionSets.AddCopy(selectionSet);

// 폴더에 저장
doc.SelectionSets.InsertCopy(targetFolder, 0, selectionSet);
```

### 4.3 Grouping Strategies

```csharp
public enum GroupingStrategy
{
    ByZone,           // Zone별 그룹화
    ByZoneAndLevel,   // Zone + Level별 그룹화
    ByTaskName,       // 작업명별 그룹화
    ByStartDate,      // 시작일(주)별 그룹화
    Custom            // 사용자 정의
}
```

### 4.4 Folder Structure

```
📁 AWP 4D Sets
├── 📁 Zone-A
│   ├── 📁 Level-1
│   │   ├── 🔷 Zone-A_L1_Structural
│   │   └── 🔷 Zone-A_L1_MEP
│   └── 📁 Level-2
└── 📁 Zone-B
```

---

## 5. Phase 3: TimeLiner Task Creation

### 5.1 Purpose
Selection Set을 TimeLiner Task에 연결하여 4D 시뮬레이션 준비

### 5.2 API Usage

```csharp
// DocumentTimeliner 획득
IDocumentTimeliner iTimeliner = doc.GetTimeliner();
DocumentTimeliner timeliner = iTimeliner as DocumentTimeliner;

// Task 생성
TimelinerTask task = new TimelinerTask();
task.DisplayName = "기초공사";
task.PlannedStartDate = new DateTime(2026, 3, 1);
task.PlannedEndDate = new DateTime(2026, 3, 14);
task.SimulationTaskTypeName = "Construct";
task.SynchronizationId = "B1-001";

// Selection Set 연결 (TypeConversion 필수)
SelectionSource selSource = selectionSet as SelectionSource;
SelectionSourceCollection selSourceCol = new SelectionSourceCollection();
selSourceCol.Add(selSource);
task.Selection.CopyFrom(selSourceCol);

// TimeLiner에 추가 (Read-Only 우회)
GroupItem rootCopy = timeliner.TasksRoot.CreateCopy() as GroupItem;
rootCopy.Children.Add(task);
timeliner.TasksCopyFrom(rootCopy.Children);
```

### 5.3 Selection Connection Methods

| 방식 | 용도 | 검증 속성 |
|------|------|----------|
| Explicit Selection | ModelItem 직접 지정 | `HasExplicitSelection` |
| Selection Sources | 저장된 Set 연결 | `HasSelectionSources` |
| Search | 검색 조건 연결 | `HasSearch` |

### 5.4 Task Types

| Type | 설명 | 시뮬레이션 효과 |
|------|------|----------------|
| `Construct` | 시공 | 객체 등장 |
| `Demolish` | 철거 | 객체 소멸 |
| `Temporary` | 임시 | 등장 후 소멸 |

---

## 6. Phase 4: Integration & Validation

### 6.1 Integrated Pipeline

```csharp
public async Task<AutomationResult> ExecutePipelineAsync(
    string csvPath,
    AWP4DOptions options,
    CancellationToken cancellationToken = default)
{
    // Phase 1: Property Write
    var schedules = ParseCsvFile(csvPath);
    foreach (var schedule in schedules)
    {
        var modelItem = _matcher.FindBySyncID(schedule.SyncID);
        _propertyWriter.AddScheduleProperty(modelItem, schedule);
    }

    // Phase 2: Selection Set
    var setResult = _setService.CreateHierarchicalSets(
        schedules, _matcher, options.GroupingStrategy);

    // Phase 3: TimeLiner Task
    var timelineResult = _timelineService.CreateHierarchicalTasks(
        schedules, syncIdToSet);

    // Phase 4: Validation
    var validation = ValidateTimeline(schedules.Count);

    return result;
}
```

### 6.2 Validation Checklist

- [ ] 모든 SyncID 매칭 성공
- [ ] Selection Set 생성 완료
- [ ] TimeLiner Task 생성 완료
- [ ] Task-Set 연결 완료
- [ ] 날짜 유효성 검사 통과

---

## 7. Critical Patterns

### 7.1 Read-Only Collection Bypass

```csharp
// ❌ 직접 추가 불가
collection.Add(item);  // 예외 발생

// ✅ 복사본 방식
doc.SelectionSets.AddCopy(item);
timeliner.TasksCopyFrom(rootCopy.Children);
```

### 7.2 TypeConversion for Selection

```csharp
// SelectionSet → SelectionSource → SelectionSourceCollection
SelectionSource selSource = selectionSet as SelectionSource;
SelectionSourceCollection selSourceCol = new SelectionSourceCollection();
selSourceCol.Add(selSource);
task.Selection.CopyFrom(selSourceCol);
```

### 7.3 Thread Safety

```csharp
// ❌ Background thread 금지
Task.Run(() => Application.ActiveDocument.xxx);

// ✅ UI thread only
Application.ActiveDocument.CurrentSelection.Add(items);
```

---

## 8. Risk Matrix

| Phase | 위험 | 확률 | 영향 | 완화 전략 |
|-------|------|------|------|----------|
| 1 | SyncID 매칭 실패 | 중 | 고 | Fuzzy 매칭 + 사전 검증 |
| 1 | ComAPI 예외 | 저 | 고 | 재시도 + 지수 백오프 |
| 2 | Read-Only 위반 | 중 | 고 | CreateCopy 패턴 |
| 3 | Selection 연결 실패 | 중 | 고 | TypeConversion 검증 |
| 4 | Thread Safety | 고 | 고 | UI Thread 전용 |

---

## 9. Implementation Priority

| Sprint | 항목 | 우선순위 |
|--------|------|----------|
| Sprint 1 | PropertyWriteService | 🔴 P0 |
| Sprint 1 | ObjectMatcher | 🔴 P0 |
| Sprint 2 | SelectionSetService | 🟠 P1 |
| Sprint 2 | TimeLinerService | 🟠 P1 |
| Sprint 3 | AWP4DAutomationService | 🟠 P1 |
| Sprint 3 | UI Integration | 🟡 P2 |

---

## 10. References

- [ADR-001: ComAPI Property Write](../adr/ADR-001-ComAPI-Property-Write.md)
- [ADR-002: TimeLiner API Integration](../adr/ADR-002-TimeLiner-API-Integration.md)
- [TwentyTwo: Navisworks API Timeliner](https://twentytwo.space/2022/07/11/navisworks-api-timeliner-part1/)
- [Autodesk DevBlog: Selection Set to TimeLiner](https://adndevblog.typepad.com/aec/2014/03/add-search-or-selection-set-to-timeliner-task.html)

---

**Created**: 2026-01-11
**Last Updated**: 2026-01-11

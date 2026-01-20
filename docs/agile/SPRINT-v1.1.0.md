# Sprint: DXTnavis v1.1.0 TimeLiner Enhancement

| Field | Value |
|-------|-------|
| **Sprint Name** | DXTnavis TimeLiner Enhancement v1.1.0 → v1.2.0 |
| **Start Date** | 2026-01-20 |
| **End Date** | 2026-01-21 |
| **Status** | ✅ Complete (100%) |
| **Goal** | TimeLiner 직접 연동, TaskType 한글화, DateMode 옵션, 확장된 ParentSet 전략 |

---

## Requirements Summary

```
Total Features: 4
Priority: P0 (2), P1 (2)
Completed: 4 (All Features) ✅
- TaskType 한글화 ✅
- DateMode 옵션 ✅
- ParentSet 확장 ✅
- Direct TimeLiner Execution ✅ (NEW!)
Build: ✅ Verified (2026-01-21)
```

---

## Phase 13 Features

### 13.1 TaskType 한글화 (P0) ✅
| Field | Value |
|-------|-------|
| Priority | 🔴 P0 Critical |
| Type | Enhancement |
| Files | `ScheduleBuilderViewModel.cs`, `TimeLinerService.cs` |
| Status | ✅ Complete |

**Current State:**
```csharp
TaskTypes = new List<string> { "Construct", "Demolish", "Temporary" };
```

**Target State:**
```csharp
TaskTypes = new List<string> { "구성", "철거", "임시" };
TaskTypeMapping = { "구성": "Construct", "철거": "Demolish", "임시": "Temporary" };
```

**Tasks:**
- [x] TaskType 매핑 딕셔너리 생성
- [x] UI 한글 표시로 변경
- [x] CSV 저장 시 영문 변환
- [x] TimeLinerService 역방향 파싱 업데이트

---

### 13.2 DateMode 옵션 추가 (P0) ✅
| Field | Value |
|-------|-------|
| Priority | 🔴 P0 Critical |
| Type | New Feature |
| Files | `ScheduleBuilderViewModel.cs`, `Models/DateMode.cs`, `DXwindow.xaml` |
| Status | ✅ Complete |

**DateMode Options:**
```csharp
public enum DateMode
{
    PlannedOnly,         // 기본: Planned만 설정
    ActualFromPlanned,   // Planned를 Actual에도 복사 (권장)
    BothSeparate         // 사용자가 Actual 별도 입력
}
```

**Default:** `ActualFromPlanned` (TimeLiner 4D 시뮬레이션 호환성)

**Tasks:**
- [x] DateMode enum 생성 (`Models/DateMode.cs`)
- [x] ScheduleBuilderViewModel에 DateMode 프로퍼티 추가
- [x] UI ComboBox 추가
- [x] CSV 출력에 ActualStart/End 컬럼 추가
- [x] SchedulePreviewItem에 ActualStart/End 필드 추가

---

### 13.3 직접 TimeLiner 실행 (P1) ✅
| Field | Value |
|-------|-------|
| Priority | 🟠 P1 High |
| Type | New Feature |
| Files | `ScheduleBuilderViewModel.cs`, `DXwindow.xaml` |
| Status | ✅ Complete |
| Completed | 2026-01-21 |

**Implemented Features:**
- [x] ConvertPreviewToScheduleData() 메서드
- [x] ExecuteDirectToTimeLiner() 메서드
- [x] DryRun 미리보기 모드
- [x] 진행률 표시 UI (ProgressBar)
- [x] ObjectMatcher 직접 연동 (ObjectId = SyncID)
- [x] SelectionSetService 직접 호출
- [x] TimeLinerService.CreateTasks() 직접 호출

---

## 🔬 기술 분석: 직접 TimeLiner 연결 자동화

### 현재 워크플로우 (2-Step)
```
┌─────────────────┐     ┌──────────────┐     ┌──────────────────┐
│ Schedule Builder│ →   │   CSV 저장   │ →   │   AWP 4D 탭      │
│   객체 선택     │     │ schedule.csv │     │   CSV 로드       │
│   설정 구성     │     │              │     │   Execute 실행   │
└─────────────────┘     └──────────────┘     └──────────────────┘
                                                     │
                                                     ▼
                              ┌──────────────────────────────────────┐
                              │ 결과: Selection Set + TimeLiner Task │
                              └──────────────────────────────────────┘
```

### 목표 워크플로우 (1-Step) 🎯
```
┌─────────────────┐     ┌──────────────────────────────────────┐
│ Schedule Builder│ →   │         [직접 실행 버튼]              │
│   객체 선택     │     │   Selection Set + TimeLiner Task     │
│   설정 구성     │     │         자동 생성 완료!              │
└─────────────────┘     └──────────────────────────────────────┘
```

### ✅ 기술적 가능성 분석

| 요소 | 현재 상태 | 직접 실행 가능성 |
|------|----------|-----------------|
| **SyncID** | `HierarchicalPropertyRecord.ObjectId` (GUID) | ✅ 바로 사용 가능 |
| **Object Matching** | `ObjectMatcher.FindBySyncId()` 완성 | ✅ GUID로 ModelItem 검색 |
| **Selection Set** | `SelectionSetService` 완성 | ✅ 직접 호출 가능 |
| **TimeLiner Task** | `TimeLinerService.CreateTasks()` 완성 | ✅ 직접 호출 가능 |
| **Pipeline** | `AWP4DAutomationService` 완성 | ✅ 전체 통합 가능 |

### 🔑 핵심 발견

```
HierarchicalPropertyRecord.ObjectId == ModelItem.InstanceGuid == SyncID
```

**결론**: Schedule Builder에서 선택된 객체의 `ObjectId`를 `SyncID`로 직접 사용하면
CSV 중간 단계 없이 TimeLiner 연결이 가능합니다.

---

## 📋 상세 구현 계획

### Phase 1: 데이터 변환 레이어 (Day 1)
```csharp
// ScheduleBuilderViewModel.cs
private List<ScheduleData> ConvertPreviewToScheduleData()
{
    return PreviewItems.Select(item => new ScheduleData
    {
        SyncID = item.SyncID,  // ObjectId (GUID)
        TaskName = item.TaskName,
        PlannedStart = item.PlannedStart,
        PlannedEnd = item.PlannedEnd,
        ActualStart = item.ActualStart,
        ActualEnd = item.ActualEnd,
        TaskType = ToEnglishTaskType(item.TaskType),  // 한글→영문 변환
        ParentSet = item.ParentSet,
        MatchStatus = MatchStatus.Pending
    }).ToList();
}
```

### Phase 2: Direct Execution 메서드 (Day 2)
```csharp
private async void ExecuteDirectToTimeLiner()
{
    try
    {
        StatusMessage = "TimeLiner 연결 시작...";

        // 1. Preview → ScheduleData 변환
        var schedules = ConvertPreviewToScheduleData();
        if (!schedules.Any())
        {
            StatusMessage = "선택된 객체가 없습니다.";
            return;
        }

        // 2. AWP4DOptions 구성
        var options = new AWP4DOptions
        {
            EnablePropertyWrite = false,  // 직접 실행 시 Property Write 생략
            EnableSelectionSetCreation = true,
            EnableTimeLinerTaskCreation = true,
            GroupingStrategy = GroupingStrategy.ByParentSet,
            SelectionSetRootFolder = "Schedule Builder",
            TimeLinerRootFolder = "Schedule Builder",
            DryRun = IsDryRunMode
        };

        // 3. ObjectMatcher로 매칭 (ObjectId → ModelItem)
        StatusMessage = "객체 매칭 중...";
        var matchResult = _objectMatcher.MatchAll(schedules, options);

        if (matchResult.MatchRate < 50)
        {
            StatusMessage = $"매칭률 부족: {matchResult.MatchRate:F1}%";
            return;
        }

        // 4. Selection Set 생성
        StatusMessage = "Selection Set 생성 중...";
        var setResult = _selectionSetService.CreateHierarchicalSets(schedules, options);

        // SyncID → SetName 매핑
        var syncIdToSetName = BuildSyncIdToSetNameMapping(schedules);

        // 5. TimeLiner Task 생성
        StatusMessage = "TimeLiner Task 생성 중...";
        var timelineResult = _timeLinerService.CreateTasks(schedules, syncIdToSetName, options);

        // 6. 결과 표시
        StatusMessage = $"완료: {timelineResult.TaskCount}개 Task, " +
                       $"{timelineResult.LinkedCount}개 연결됨";
    }
    catch (Exception ex)
    {
        StatusMessage = $"오류: {ex.Message}";
    }
}
```

### Phase 3: UI 통합 (Day 3)
```xml
<!-- DXwindow.xaml - Schedule Builder 탭 -->
<StackPanel Orientation="Horizontal" Margin="5">
    <Button Content="CSV 생성" Command="{Binding GenerateCsvCommand}" Width="80"/>
    <Button Content="직접 실행" Command="{Binding ExecuteToTimeLinerCommand}"
            Width="80" Margin="5,0,0,0"
            ToolTip="CSV 없이 바로 TimeLiner 연결"/>
    <CheckBox Content="Dry Run" IsChecked="{Binding IsDryRunMode}"
              Margin="10,0,0,0" VerticalAlignment="Center"/>
</StackPanel>

<!-- 진행률 표시 -->
<ProgressBar Value="{Binding ExecutionProgress}" Minimum="0" Maximum="100"
             Height="20" Margin="5" Visibility="{Binding IsExecuting}"/>
```

### Phase 4: 검증 및 폴백 (Day 4)
- **DryRun 모드**: 실제 생성 없이 미리보기
- **실패 시 CSV 폴백**: 직접 실행 실패 시 기존 CSV 방식으로 자동 전환
- **부분 성공 처리**: 매칭 실패 객체 목록 표시

---

## 📊 워크플로우 비교

### Before (v1.1.0)
| 단계 | 작업 | 사용자 액션 |
|------|------|-----------|
| 1 | 객체 선택 | 체크박스 선택 |
| 2 | 설정 구성 | TaskType, Duration 등 |
| 3 | CSV 생성 | [Generate CSV] 클릭 |
| 4 | 파일 저장 | 저장 대화상자 |
| 5 | AWP 4D 탭 이동 | 탭 클릭 |
| 6 | CSV 로드 | [Browse] + [Load] |
| 7 | Execute | [Execute] 클릭 |
| **Total** | **7 단계** | **많은 클릭** |

### After (v1.2.0) 🎯
| 단계 | 작업 | 사용자 액션 |
|------|------|-----------|
| 1 | 객체 선택 | 체크박스 선택 |
| 2 | 설정 구성 | TaskType, Duration 등 |
| 3 | 직접 실행 | [직접 실행] 클릭 |
| **Total** | **3 단계** | **원클릭** |

**개선 효과**: 워크플로우 57% 단축!

---

## 🚀 v1.2.0 릴리즈 완료!

**Completed Tasks:**
- [x] SchedulePreviewItem → ScheduleData 변환 메서드 구현
- [x] ObjectMatcher 직접 연동 (ObjectId = SyncID)
- [x] SelectionSetService 직접 호출 통합
- [x] TimeLinerService.CreateTasks() 직접 호출
- [x] 진행률 표시 UI (ProgressBar)
- [x] DryRun 미리보기 모드
- [x] 부분 성공 결과 표시 UI
- [x] User Manual 업데이트

**Future Enhancement (Optional):**
- [ ] 실패 시 CSV 폴백 로직
- [ ] 단위 테스트 작성

---

### 13.4 확장된 ParentSet 전략 (P1) ✅
| Field | Value |
|-------|-------|
| Priority | 🟠 P1 High |
| Type | Enhancement |
| Files | `ScheduleBuilderViewModel.cs` |
| Status | ✅ Complete |

**Current Strategies:**
- ByLevel (트리 레벨)
- ByProperty (SysPath 기반)
- Custom (사용자 입력)

**New Strategies:**
- ByFloorLevel (건축 층 - Element.Level 속성)
- ByCategory (Element 카테고리)
- ByArea (구역 - Element.Area/Zone 속성)
- Composite (복합: Level + Category)

**Tasks:**
- [x] ParentSetStrategies 목록 확장
- [x] DetermineParentSet() 로직 확장
- [x] FindPropertyValue() 헬퍼 메서드 추가
- [x] ExtractCategoryFromDisplayName() 헬퍼 메서드 추가
- [x] UI ComboBox 자동 반영 (기존 바인딩)

---

## Technical Design

### TaskType Mapping

```csharp
// ScheduleBuilderViewModel.cs
public static readonly Dictionary<string, string> TaskTypeKorToEng =
    new Dictionary<string, string>
{
    { "구성", "Construct" },
    { "철거", "Demolish" },
    { "임시", "Temporary" }
};

public static readonly Dictionary<string, string> TaskTypeEngToKor =
    new Dictionary<string, string>
{
    { "Construct", "구성" },
    { "Demolish", "철거" },
    { "Temporary", "임시" }
};

public List<string> TaskTypes { get; } = new List<string> { "구성", "철거", "임시" };
```

### DateMode Implementation

```csharp
// Models/DateMode.cs
public enum DateMode
{
    [Description("계획일만 (Planned Only)")]
    PlannedOnly,

    [Description("계획일 → 실제일 복사 (권장)")]
    ActualFromPlanned,

    [Description("계획/실제 별도 입력")]
    BothSeparate
}

// ScheduleBuilderViewModel.cs
private DateMode _selectedDateMode = DateMode.ActualFromPlanned;
public DateMode SelectedDateMode
{
    get => _selectedDateMode;
    set { _selectedDateMode = value; OnPropertyChanged(); RefreshPreview(); }
}
```

### Direct TimeLiner Execution

```csharp
private async void ExecuteDirectToTimeLiner()
{
    var progress = new Progress<int>(p => StatusMessage = $"진행 중... {p}%");

    // 1. SchedulePreviewItem → ScheduleData 변환
    var schedules = ConvertPreviewToScheduleData();

    // 2. ObjectMatcher로 매칭
    var objectMatcher = new ObjectMatcher();
    var matchResults = await objectMatcher.MatchSchedulesAsync(schedules, options);

    // 3. SelectionSet 생성
    var selectionSetService = new SelectionSetService();
    var syncIdToSetName = selectionSetService.CreateSelectionSets(matchResults, options);

    // 4. TimeLiner Task 생성
    var timeLinerService = new TimeLinerService(selectionSetService);
    var result = timeLinerService.CreateTasks(matchResults, syncIdToSetName, options);

    StatusMessage = $"완료: {result.TaskCount}개 Task, {result.LinkedCount}개 연결됨";
}
```

---

## File Changes

### Modified Files
| File | Change |
|------|--------|
| `ScheduleBuilderViewModel.cs` | TaskType 한글화, DateMode, 직접 실행 |
| `TimeLinerService.cs` | ParseSimulationTaskType 한글 지원 강화 |
| `Models/SchedulePreviewItem.cs` | ActualStart/End 필드 추가 |
| `Views/DXwindow.xaml` | DateMode UI, 직접 실행 버튼 |

### New Files
| File | Description |
|------|-------------|
| `Models/DateMode.cs` | DateMode enum 정의 |

---

## Success Criteria

- [x] TaskType이 UI에서 한글로 표시됨 (구성/철거/임시)
- [x] CSV 출력 시 영문 TaskType으로 변환됨 (Construct/Demolish/Temporary)
- [x] DateMode 옵션이 정상 작동함 (3가지 모드)
- [x] Schedule Builder에서 직접 TimeLiner 실행 가능 ✅
- [x] DryRun 미리보기 모드 지원 ✅
- [x] 진행률 표시 UI 구현 ✅
- [x] 확장된 ParentSet 전략이 정상 작동함 (7가지 전략)
- [x] 기존 AWP 4D 파이프라인 호환성 유지

---

## Dependencies

- Phase 12 (Grouped Data Structure) 완료 필수
- TimeLinerService.cs 기존 기능 유지
- AWP4DAutomationService.cs 호환성

---

## Risk Assessment

| Risk | Impact | Mitigation |
|------|--------|------------|
| SyncID 매칭 실패 | High | 매칭 로직 검증, 실패 시 CSV 폴백 |
| UI 스레드 차단 | Medium | 비동기 처리, 진행률 표시 |
| 기존 기능 호환성 | High | 단계적 마이그레이션, 테스트 |

---

## 🔧 Minor Fix: v1.2.1

### MF-001: TextBox 한글/영어/숫자 입력 오류 수정

| Field | Value |
|-------|-------|
| Priority | 🟡 P2 Medium |
| Type | Bug Fix |
| Files | `Views/DXwindow.xaml.cs` |
| Status | 🚧 Planned |

**증상:**
- Schedule Builder 탭의 Task Prefix, Duration, Custom Set 등 TextBox에서 한글/영어/숫자 입력이 안 됨
- IME 조합 문자 입력 불가

**원인:**
```csharp
// TextBox_PreviewKeyDown 핸들러에서 IME 키 처리 누락
// Key.ImeProcessed가 처리되지 않아 한글 입력 차단
default:
    if (e.Key >= Key.A && e.Key <= Key.Z || ...)  // ❌ IME 키 없음
```

**해결책:**
```csharp
// 방법 1: IME 키 추가
case Key.ImeProcessed:
    e.Handled = false;  // IME가 처리하도록 허용
    break;

// 방법 2: default에서 모든 키 허용
default:
    e.Handled = false;  // 기본적으로 모든 키 TextBox에 전달
    break;
```

**영향 범위:**
- Property Name Filter TextBox
- Task Prefix TextBox
- Duration TextBox
- Level TextBox
- Custom Set TextBox
- 기타 모든 TextBox

**Tasks:**
- [ ] `TextBox_PreviewKeyDown`에 `Key.ImeProcessed` 처리 추가
- [ ] 한글, 영어, 숫자, 특수문자 입력 테스트
- [ ] Navisworks 환경에서 검증

---

**Created**: 2026-01-20
**Last Updated**: 2026-01-21

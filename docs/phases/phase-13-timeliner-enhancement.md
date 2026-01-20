# Phase 13: TimeLiner Enhancement

| Field | Value |
|-------|-------|
| **Phase** | 13 |
| **Version** | v1.1.0 |
| **Status** | 🚧 In Progress |
| **Priority** | High |
| **Goal** | TimeLiner 직접 연동 강화 및 사용자 편의성 개선 |

---

## 1. 배경 및 목표

### 1.1 현재 문제점

1. **TaskType 영문 표기**: UI에 "Construct", "Demolish", "Temporary"로 표시 → 한국 사용자 불편
2. **날짜 옵션 제한**: PlannedStart/End만 지원 → ActualStart/End 미지원
3. **간접 워크플로우**: Schedule Builder → CSV 저장 → AWP 4D 탭 → CSV 로드 → 실행 (5단계)
4. **제한된 ParentSet 전략**: 트리 레벨 기반만 지원 → 건축 층/Area 기반 미지원

### 1.2 목표

| 목표 | 설명 | 기대 효과 |
|------|------|----------|
| TaskType 한글화 | 구성/철거/임시로 표시 | 사용자 친화적 UI |
| DateMode 옵션 | Planned, Actual 모두 지원 | 4D 시뮬레이션 유연성 |
| 직접 TimeLiner 실행 | 1클릭으로 TimeLiner 생성 | 워크플로우 5단계 → 1단계 |
| 확장 ParentSet 전략 | 건축 층, Area 기반 그룹화 | 실제 건설 공정 반영 |

---

## 2. 기능 요구사항

### 2.1 TaskType 한글화 (FR-13.1)

**매핑 테이블:**
| 영문 (Internal) | 한글 (UI) | Navisworks API |
|----------------|----------|----------------|
| Construct | 구성 | SimulationTaskTypeName = "Construct" |
| Demolish | 철거 | SimulationTaskTypeName = "Demolish" |
| Temporary | 임시 | SimulationTaskTypeName = "Temporary" |

**구현 위치:**
- `ScheduleBuilderViewModel.cs`: UI 표시
- `TimeLinerService.cs`: API 연동

### 2.2 DateMode 옵션 (FR-13.2)

**옵션:**
```
┌─────────────────────────────────────────────────────────────┐
│ Date Mode: [▼ 계획일 → 실제일 복사 (권장)]                  │
├─────────────────────────────────────────────────────────────┤
│ ○ 계획일만 (Planned Only)                                   │
│   → PlannedStart/End만 설정                                │
│                                                             │
│ ● 계획일 → 실제일 복사 (권장)                               │
│   → PlannedStart/End를 ActualStart/End에도 복사            │
│   → TimeLiner 4D 시뮬레이션 즉시 시작 가능                 │
│                                                             │
│ ○ 계획/실제 별도 입력                                       │
│   → 사용자가 Actual 날짜 직접 입력                         │
│   → CSV Import 시 별도 컬럼 필요                           │
└─────────────────────────────────────────────────────────────┘
```

### 2.3 직접 TimeLiner 실행 (FR-13.3)

**워크플로우 비교:**

**Before (5단계):**
```
[Schedule Builder] → [Generate CSV] → [Switch to AWP 4D Tab]
                  → [Load CSV] → [Execute to TimeLiner]
```

**After (1단계):**
```
[Schedule Builder] → [To TimeLiner 직접 실행]
```

**실행 과정:**
1. SchedulePreviewItem → ScheduleData 변환
2. ObjectMatcher: SyncID → ModelItem 매칭
3. SelectionSetService: Selection Set 자동 생성
4. TimeLinerService: Task 생성 및 Set 연결
5. 결과 표시

### 2.4 확장 ParentSet 전략 (FR-13.4)

**기존:**
| 전략 | 설명 |
|------|------|
| ByLevel | 트리 레벨 (depth) 기반 |
| ByProperty | SysPath 첫 요소 |
| Custom | 사용자 입력 |

**확장:**
| 전략 | 설명 | Selection Set 예시 |
|------|------|-------------------|
| ByFloorLevel | 건축 층 (Element.Level) | `1F`, `2F`, `B1` |
| ByCategory | Element 카테고리 | `Walls`, `Floors`, `Columns` |
| ByArea | 구역 (Element.Area/Zone) | `Zone-A`, `Zone-B` |
| ByWorkPackage | 작업 패키지 | `WP001`, `WP002` |
| Composite | Level + Category 조합 | `1F/Walls`, `1F/Columns` |

---

## 3. 기술 설계

### 3.1 TaskType 매핑

```csharp
// ScheduleBuilderViewModel.cs
public static class TaskTypeLocalization
{
    public static readonly Dictionary<string, string> KorToEng = new Dictionary<string, string>
    {
        { "구성", "Construct" },
        { "철거", "Demolish" },
        { "임시", "Temporary" }
    };

    public static readonly Dictionary<string, string> EngToKor = new Dictionary<string, string>
    {
        { "Construct", "구성" },
        { "Demolish", "철거" },
        { "Temporary", "임시" }
    };

    public static string ToEnglish(string korean) =>
        KorToEng.TryGetValue(korean, out var eng) ? eng : korean;

    public static string ToKorean(string english) =>
        EngToKor.TryGetValue(english, out var kor) ? kor : english;
}
```

### 3.2 DateMode Enum

```csharp
// Models/DateMode.cs
namespace DXTnavis.Models
{
    public enum DateMode
    {
        /// <summary>계획일만 설정</summary>
        PlannedOnly,

        /// <summary>계획일을 실제일에도 복사 (권장)</summary>
        ActualFromPlanned,

        /// <summary>계획/실제 별도 입력</summary>
        BothSeparate
    }
}
```

### 3.3 직접 실행 아키텍처

```
┌─────────────────────────────────────────────────────────────┐
│                  ScheduleBuilderViewModel                    │
├─────────────────────────────────────────────────────────────┤
│ PreviewItems: List<SchedulePreviewItem>                      │
│                         ↓                                    │
│           ConvertToScheduleData()                            │
│                         ↓                                    │
├─────────────────────────────────────────────────────────────┤
│                    ObjectMatcher                             │
├─────────────────────────────────────────────────────────────┤
│ Input: ScheduleData.SyncID                                   │
│ Output: ScheduleData.MatchedObjectId (Guid)                  │
│                         ↓                                    │
├─────────────────────────────────────────────────────────────┤
│                  SelectionSetService                         │
├─────────────────────────────────────────────────────────────┤
│ Input: Matched ScheduleData + ParentSet                      │
│ Output: Dictionary<SyncID, SetName>                          │
│                         ↓                                    │
├─────────────────────────────────────────────────────────────┤
│                   TimeLinerService                           │
├─────────────────────────────────────────────────────────────┤
│ Input: ScheduleData + SyncIdToSetName                        │
│ Output: TimeLinerResult (TaskCount, LinkedCount)             │
└─────────────────────────────────────────────────────────────┘
```

### 3.4 확장 ParentSet 로직

```csharp
private string DetermineParentSet(HierarchicalPropertyRecord item)
{
    switch (ParentSetStrategy)
    {
        case "ByLevel":
            return $"Level-{Math.Min(item.Level, ParentSetLevel)}";

        case "ByFloorLevel":
            // Element.Level 속성에서 건축 층 추출
            var levelProp = FindProperty(item, "Element", "Level")
                         ?? FindProperty(item, "Item", "Level");
            return levelProp ?? "Unknown-Level";

        case "ByCategory":
            // Element.Category 속성
            var catProp = FindProperty(item, "Element", "Category");
            return catProp ?? item.DisplayName.Split(' ')[0];

        case "ByArea":
            // Element.Area 또는 Zone 속성
            var areaProp = FindProperty(item, "Element", "Area")
                        ?? FindProperty(item, "Element", "Zone");
            return areaProp ?? "Default-Area";

        case "ByWorkPackage":
            // WorkPackage 속성 또는 SysPath 기반
            var wpProp = FindProperty(item, "Element", "WorkPackage");
            return wpProp ?? $"WP-{item.Level}";

        case "Composite":
            // Level + Category 조합
            var level = FindProperty(item, "Element", "Level") ?? $"L{item.Level}";
            var category = FindProperty(item, "Element", "Category") ?? "Unknown";
            return $"{level}/{category}";

        case "Custom":
            return string.IsNullOrEmpty(CustomParentSet) ? "Custom" : CustomParentSet;

        default:
            return "Default";
    }
}

private string FindProperty(HierarchicalPropertyRecord item, string category, string name)
{
    // 객체의 속성 목록에서 해당 카테고리/이름의 값 검색
    // Phase 12 그룹 구조에서는 ObjectGroupModel.Properties 검색
    return null; // 구현 필요
}
```

---

## 4. UI 설계

### 4.1 Schedule Builder 탭 변경

```
┌─────────────────────────────────────────────────────────────┐
│ Schedule Builder                                             │
├─────────────────────────────────────────────────────────────┤
│ Task Settings:                                               │
│   Prefix: [Task________]                                     │
│   Type:   [▼ 구성      ]  ← 한글화                           │
│   Duration: [1] days                                         │
│   Start:    [2026-01-21]                                     │
│                                                              │
│ Date Mode: [▼ 계획일 → 실제일 복사]  ← NEW                   │
│                                                              │
│ ParentSet Strategy:                                          │
│   [▼ ByFloorLevel (건축 층)]  ← 확장                         │
│                                                              │
│ ─────────────────────────────────────────────────────────── │
│ Preview (5 items):                                           │
│ ┌───────────────────────────────────────────────────────┐   │
│ │ SyncID    │ Task    │ Start    │ End      │ ParentSet │   │
│ │───────────────────────────────────────────────────────│   │
│ │ Elem_001  │ Task 1  │ 01-21    │ 01-22    │ 1F/Walls  │   │
│ │ Elem_002  │ Task 2  │ 01-22    │ 01-23    │ 1F/Cols   │   │
│ └───────────────────────────────────────────────────────┘   │
│                                                              │
│ [Generate CSV]  [To TimeLiner 직접 실행]  ← NEW              │
│                                                              │
│ Status: 5개 객체 → 5일 일정 생성 준비됨                      │
└─────────────────────────────────────────────────────────────┘
```

---

## 5. 구현 계획

| 순서 | 작업 | 파일 | 예상 복잡도 |
|:----:|------|------|:-----------:|
| 1 | TaskType 한글화 | ScheduleBuilderViewModel.cs, TimeLinerService.cs | 낮음 |
| 2 | DateMode enum 생성 | Models/DateMode.cs | 낮음 |
| 3 | DateMode UI 추가 | DXwindow.xaml, ScheduleBuilderViewModel.cs | 중간 |
| 4 | 직접 실행 버튼 | ScheduleBuilderViewModel.cs | 높음 |
| 5 | 확장 ParentSet | ScheduleBuilderViewModel.cs | 중간 |
| 6 | 테스트 및 검증 | - | 중간 |

---

## 6. 호환성

### 6.1 기존 기능 유지

- AWP 4D 탭의 CSV → TimeLiner 파이프라인 유지
- 기존 CSV 포맷 호환 (영문 TaskType)
- ScheduleBuilderViewModel의 GenerateCsv() 유지

### 6.2 데이터 변환

```csharp
// CSV 저장 시
var csvTaskType = TaskTypeLocalization.ToEnglish(item.TaskType); // 구성 → Construct

// CSV 로드 시 (AWP 4D Tab)
var displayTaskType = TaskTypeLocalization.ToKorean(csvTaskType); // Construct → 구성
```

---

## 7. 참고 문서

- [Phase 12: Grouped Data Structure](phase-12-grouped-data-structure.md)
- [ADR-002: TimeLiner API Integration](../adr/ADR-002-TimeLiner-API-Integration.md)
- [AWP 4D Automation Spec](../tech-specs/AWP-4D-Automation-Spec.md)
- [CLAUDE.md](../../CLAUDE.md) - 프로젝트 가이드라인

---

**Created**: 2026-01-20
**Status**: 🚧 In Progress

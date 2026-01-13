# DXTnavis - Navisworks 2025 Property Viewer Plugin

> **Context:** Standalone Navisworks plugin for property viewing and 3D control
> **Version:** 0.8.0 (Released 2026-01-13)
> **Docs Index:** [docs/_INDEX.md](docs/_INDEX.md)

## Quick Reference

### Tech Stack
- C# .NET Framework 4.8 (locked)
- WPF MVVM Pattern
- Navisworks API 2025 (x64 only)
- ComAPI (ViewPoint, Property Write, TimeLiner)

### Current Status
| Phase | Task | Status |
|-------|------|--------|
| 1 | Property Filtering | ✅ 100% |
| 2 | UI Enhancement | ✅ 100% |
| 3 | 3D Object Integration | ✅ 100% |
| 4 | CSV Enhancement | ✅ 100% |
| 5 | Data Validation | ✅ 100% |
| 6 | Code Quality | ✅ 100% |
| 7 | CSV Viewer | ✅ 100% |
| 8 | AWP 4D Automation | ✅ 100% |
| 9 | UI Enhancement v2 | ✅ 100% |
| **10** | **Load Optimization** | ✅ 100% |

**→ Changelog:** [CHANGELOG.md](CHANGELOG.md)

---

## v0.8.0 Load Optimization (CURRENT)

### Features ✅
- [x] **비동기 로딩** - IProgress<LoadProgress> 패턴으로 UI 프리징 제거
- [x] **진행률 표시** - ProgressBar + 텍스트 상태 표시
- [x] **취소 기능** - CancellationToken으로 즉시 취소
- [x] **단일 순회 최적화** - TreeNodeModel + HierarchicalPropertyRecord 동시 추출

### New Files (Phase 10)
| File | Description |
|------|-------------|
| LoadProgress.cs | 진행률 모델 및 LoadPhase enum |
| LoadHierarchyService.cs | 최적화된 로딩 서비스 (단일 순회) |

### Key Documents
- [Phase 10: Load Optimization](docs/phases/phase-10-load-optimization.md)

---

## v0.7.0 Data Validation & UI

### Features ✅
- [x] **ValidationService** - 단위/타입/필수속성 검증
- [x] **Select All 체크박스** - 전체 선택/해제
- [x] **그룹화 표시** - 객체별/카테고리별 Expander
- [x] **Expand/Collapse All** - 그룹 일괄 펼침/접기

### New Services (Phase 5, 9)
| Service | Description |
|---------|-------------|
| ValidationService | 속성 검증 (단위, 타입, 필수) |
| PropertyItemViewModel | 그룹화 표시용 ViewModel |

---

## v0.6.0 AWP 4D Automation

### Features ✅
- [x] **CSV → TimeLiner 자동 연결** 파이프라인
- [x] **Property Write** (ComAPI SetUserDefined)
- [x] **Selection Set** 계층 구조 자동 생성
- [x] **TimeLiner Task** 자동 생성 및 Set 연결
- [x] **AWP 4D 탭** UI 통합

### Services (Phase 8)
| Service | Description |
|---------|-------------|
| PropertyWriteService | ComAPI Property Write (재시도 로직) |
| SelectionSetService | Selection Set 계층 구조 생성 |
| TimeLinerService | TimeLiner Task 생성 및 Set 연결 |
| AWP4DAutomationService | 통합 파이프라인 (이벤트 기반) |
| ObjectMatcher | SyncID → ModelItem 매칭 (캐싱) |
| AWP4DValidator | Pre/Post 검증 |
| ScheduleCsvParser | 한영 컬럼 매핑 CSV 파싱 |

---

## Project Structure

```
dxtnavis/
├── Services/              # Business logic
│   ├── NavisworksDataExtractor.cs    # 속성 추출
│   ├── NavisworksSelectionService.cs # 3D 선택
│   ├── DisplayStringParser.cs        # VariantData 타입 파싱
│   ├── SnapshotService.cs            # 뷰포인트/캡처
│   ├── HierarchyFileWriter.cs        # Hierarchy CSV
│   ├── PropertyFileWriter.cs         # Property CSV + Verbose 로깅
│   ├── PropertyWriteService.cs       # ComAPI Property Write (v0.6.0)
│   ├── SelectionSetService.cs        # Selection Set 생성 (v0.6.0)
│   ├── TimeLinerService.cs           # TimeLiner Task 생성 (v0.6.0)
│   ├── AWP4DAutomationService.cs     # 통합 파이프라인 (v0.6.0)
│   ├── ObjectMatcher.cs              # SyncID 매칭 (v0.6.0)
│   ├── AWP4DValidator.cs             # 검증 (v0.6.0)
│   ├── ScheduleCsvParser.cs          # 스케줄 CSV 파싱 (v0.6.0)
│   └── ValidationService.cs          # 속성 검증 (v0.7.0)
├── ViewModels/            # MVVM ViewModels (Partial Class 패턴)
│   ├── DXwindowViewModel.cs          # Core
│   ├── DXwindowViewModel.Filter.cs   # 필터 기능
│   ├── DXwindowViewModel.Search.cs   # 검색 기능
│   ├── DXwindowViewModel.Selection.cs # 3D 선택
│   ├── DXwindowViewModel.Snapshot.cs # 스냅샷
│   ├── DXwindowViewModel.Tree.cs     # 트리
│   ├── DXwindowViewModel.Export.cs   # 내보내기
│   ├── CsvViewerViewModel.cs         # CSV 뷰어 VM
│   ├── AWP4DViewModel.cs             # AWP 4D VM (v0.6.0)
│   ├── PropertyItemViewModel.cs      # 속성 그룹화 VM (v0.7.0)
│   └── HierarchyNodeViewModel.cs     # 트리 노드
├── Views/                 # WPF Views
│   └── DXwindow.xaml                 # 메인 UI + AWP 4D 탭
├── Models/                # Data models
│   ├── ScheduleData.cs               # 스케줄 데이터 (v0.6.0)
│   ├── AWP4DOptions.cs               # 자동화 옵션 (v0.6.0)
│   ├── AutomationResult.cs           # 실행 결과 (v0.6.0)
│   └── ValidationResult.cs           # 검증 결과 (v0.6.0)
└── docs/
    ├── phases/
    │   ├── phase-5-data-validation.md     # v0.7.0
    │   ├── phase-8-awp-4d-automation.md
    │   ├── phase-9-ui-enhancement.md      # v0.7.0
    │   └── phase-10-load-optimization.md  # v0.8.0 Planning
    ├── adr/
    │   ├── ADR-001-ComAPI-Property-Write.md
    │   └── ADR-002-TimeLiner-API-Integration.md
    └── tech-specs/
        └── AWP-4D-Automation-Spec.md
```

---

## Critical Patterns

### Read-Only Collection Bypass
```csharp
// ❌ 직접 추가 불가
collection.Add(item);  // 예외 발생

// ✅ 복사본 방식
doc.SelectionSets.AddCopy(selectionSet);
doc.SelectionSets.InsertCopy(folder, index, item);
timeliner.TasksCopyFrom(rootCopy.Children);
```

### Selection Set → TimeLiner Task 연결
```csharp
// TypeConversion 필수!
SelectionSource selSource = selectionSet as SelectionSource;
SelectionSourceCollection selSourceCol = new SelectionSourceCollection();
selSourceCol.Add(selSource);
task.Selection.CopyFrom(selSourceCol);
```

### Thread Safety
```csharp
// ❌ NEVER: Background thread
Task.Run(() => Application.ActiveDocument.xxx);

// ✅ ALWAYS: UI thread only
Application.ActiveDocument.CurrentSelection.Add(items);
```

### Property Write (ComAPI)
```csharp
InwOpState10 comState = ComApiBridge.State;
InwOaPath comPath = ComApiBridge.ToInwOaPath(modelItem);
InwGUIPropertyNode2 propNode = (InwGUIPropertyNode2)comState
    .GetGUIPropertyNode(comPath, true);
propNode.SetUserDefined(0, "AWP Schedule", "AWP_Internal", propVec);
```

---

## AWP 4D Automation Usage

### CSV 파일 형식 (한영 컬럼 지원)
```csv
SyncID,TaskName,PlannedStart,PlannedEnd,TaskType,ParentSet
Element_001,콘크리트 타설,2026-01-15,2026-01-20,Construct,Zone-A/Level-1
Element_002,철골 설치,2026-01-18,2026-01-25,Construct,Zone-A/Level-2
```

### 지원 컬럼
| English | Korean | Description |
|---------|--------|-------------|
| SyncID | 동기화ID | ModelItem 매칭 키 |
| TaskName | 작업명 | TimeLiner Task 이름 |
| PlannedStart | 계획시작 | 계획 시작일 |
| PlannedEnd | 계획종료 | 계획 종료일 |
| TaskType | 작업유형 | Construct/Demolish/Temporary |
| ParentSet | 상위세트 | Selection Set 계층 경로 |
| Progress | 진행률 | 0-100 |

---

## Key Files

| Task | File | Description |
|------|------|-------------|
| AWP 4D Pipeline | AWP4DAutomationService.cs | 전체 자동화 파이프라인 |
| Property Write | PropertyWriteService.cs | ComAPI 속성 기입 |
| Selection Set | SelectionSetService.cs | 계층 구조 생성 |
| TimeLiner | TimeLinerService.cs | Task 생성 및 Set 연결 |
| Object Match | ObjectMatcher.cs | SyncID → ModelItem |
| Validation | ValidationService.cs | 속성 검증 (v0.7.0) |
| AWP 4D UI | AWP4DViewModel.cs | UI 바인딩 |

---

## Architecture Decision Records

| ADR | Title | Status |
|-----|-------|--------|
| [ADR-001](docs/adr/ADR-001-ComAPI-Property-Write.md) | ComAPI Property Write | ✅ Accepted |
| [ADR-002](docs/adr/ADR-002-TimeLiner-API-Integration.md) | TimeLiner API 4D 자동화 | ✅ Accepted |

---

## Git
- Repo: https://github.com/tygwan/DXTnavis.git
- Branch: main

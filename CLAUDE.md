# DXTnavis - Navisworks 2025 Property Viewer Plugin

> **Context:** Standalone Navisworks plugin for property viewing and 3D control
> **Version:** 1.0.0 (Grouped Data Structure)
> **Docs Index:** [docs/_INDEX.md](docs/_INDEX.md)

---

## ⚠️ CRITICAL: DO NOT MODIFY

### Load Hierarchy 구조 변경 금지

**다음 코드는 절대 수정하지 마세요:**

| 파일 | 메서드 | 이유 |
|------|--------|------|
| `DXwindowViewModel.cs` | `LoadModelHierarchyAsync()` | 445K+ 아이템 안정 처리 검증됨 |
| `DXwindowViewModel.Filter.cs` | `SyncFilteredProperties()` | ObservableCollection 동기화 |
| `NavisworksDataExtractor.cs` | `TraverseAndExtractProperties()` | 속성 추출 로직 |

**금지된 패턴:**
```csharp
// ❌ NEVER: Task.Run with Navisworks API
Task.Run(() => Application.ActiveDocument.xxx);

// ❌ NEVER: CollectionViewSource with 100K+ items
var cvs = new CollectionViewSource { Source = largeCollection };

// ❌ NEVER: 대용량 ObservableCollection 개별 Add
foreach (var item in items) collection.Add(item);  // 445K iterations = UI freeze
```

**안정 버전 태그:** `v0.6.1-stable` (2026-01-12 기준)

---

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
| 5 | ComAPI Research | ✅ 100% |
| 6 | Code Quality | ✅ 100% |
| 7 | CSV Viewer | ✅ 100% |
| 8 | AWP 4D Automation | ✅ 100% |
| 9 | UI Enhancement (Select All) | ✅ 100% |
| 10 | Schedule Builder | ✅ 100% |
| 11 | Object Grouping MVP | ✅ 100% |
| 12 | Grouped Data Structure | ✅ 100% |
| **13** | **TimeLiner Enhancement** | 🚧 20% |

**→ Changelog:** [CHANGELOG.md](CHANGELOG.md)

---

## v1.1.0 TimeLiner Enhancement (Phase 13) 🚧

### 🎯 목표: TimeLiner 직접 연동 강화

### Features (In Progress)
- **TaskType 한글화** - 구성/철거/임시로 UI 표시 (내부 영문 변환)
- **DateMode 옵션** - PlannedOnly, ActualFromPlanned(권장), BothSeparate
- **직접 TimeLiner 실행** - Schedule Builder에서 1클릭으로 TimeLiner 생성
- **확장 ParentSet 전략** - ByFloorLevel, ByCategory, ByArea, Composite

### TaskType 매핑
| 한글 (UI) | 영문 (API) |
|----------|-----------|
| 구성 | Construct |
| 철거 | Demolish |
| 임시 | Temporary |

### Key Documents
- [Phase 13 Document](docs/phases/phase-13-timeliner-enhancement.md)
- [Sprint v1.1.0](docs/agile/SPRINT-v1.1.0.md)

---

## v1.0.0 Grouped Data Structure (Phase 12)

### 🎯 핵심 최적화: 445K records → ~5K groups

### Features
- **그룹화 데이터 구조** - 기본 데이터 구조를 그룹 기반으로 변경
- **체크박스 필터 UI** - Level, Category 필터를 체크박스 다중 선택 방식으로 변경
- **그룹 단위 Select All** - 445K 개별 레코드 대신 ~5K 그룹 단위 처리
- **TimeLiner 호환성 유지** - `ToHierarchicalRecords()` 메서드로 기존 기능 호환

### New Models (Phase 12)
| File | Description |
|------|-------------|
| `ObjectGroupModel.cs` | 객체 그룹화 모델 (1 object = 1 group) |
| `PropertyRecord.cs` | 간소화된 속성 레코드 |
| `FilterOption.cs` | 체크박스 기반 필터 옵션 |

### Architecture Changes
- **기본 뷰**: ListView + Expander (그룹화 토글 제거)
- **필터 시스템**: ComboBox → 체크박스 다중 선택
- **데이터 로딩**: `ExtractAllAsGroups()` 메서드
- **호환성 메서드**: `GetSelectedHierarchicalRecords()`, `GetSelectedObjectIds()`

### Performance
| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| Select All iterations | 445K | ~5K | 99% ↓ |
| 메모리 사용량 | 중복 객체 정보 | 그룹당 1회 | 대폭 감소 |
| UI 응답성 | 지연 | 즉시 | 향상 |

---

## v0.9.0 Object Grouping MVP (Phase 11)

### Features
- **객체별 그룹화 보기** - 동일 객체의 속성들을 Expander UI로 그룹화
- **Flat/Grouped Mode 전환** - 중앙 패널 토글 체크박스로 전환
- **그룹 선택 전파** - 객체 선택 시 하위 속성 모두 선택
- **조건부 활성화** - 10,000개 미만 필터링 데이터에서만 그룹화 활성화

### New Files
| File | Description |
|------|-------------|
| `ObjectGroupViewModel.cs` | 객체 그룹화 ViewModel |

### UI
- **Grouped View 토글** - Select All 옆에 체크박스 추가
- **ListView + Expander** - 그룹화 모드 시 계층적 표시
- **조건부 활성화** - 필터링 결과가 10K 미만일 때만 활성화

### Key Documents
- [Phase 11 Document](docs/phases/phase-11-object-grouping.md)

---

## v0.8.0 Schedule Builder (Phase 10)

### Features
- **Schedule CSV 자동 생성** - 선택된 객체에서 일정 CSV 생성
- **Task 설정** - 이름 접두사, 작업 유형 (Construct/Demolish/Temporary), 기간, 시작일
- **ParentSet 전략** - ByLevel, ByProperty, Custom 지원
- **미리보기 기능** - 생성 전 DataGrid 미리보기
- **AWP 4D 연동** - 생성된 CSV를 AWP 4D 탭에서 TimeLiner에 적용 가능

### New Files
| File | Description |
|------|-------------|
| `ScheduleBuilderViewModel.cs` | Schedule Builder ViewModel |
| `SchedulePreviewItem` | 미리보기 아이템 모델 (ScheduleBuilderViewModel.cs 내 클래스) |

### UI
- **Schedule 탭** 추가 - 우측 패널에 새 탭
- **미리보기 DataGrid** - Task명, 시작일, 종료일, 유형, ParentSet 표시

### Key Documents
- [Phase 10 Document](docs/phases/phase-10-refined-schedule-builder.md)

---

## v0.6.0 AWP 4D Automation

### Features ✅
- [x] **CSV → TimeLiner 자동 연결** 파이프라인
- [x] **Property Write** (ComAPI SetUserDefined)
- [x] **Selection Set** 계층 구조 자동 생성
- [x] **TimeLiner Task** 자동 생성 및 Set 연결
- [x] **AWP 4D 탭** UI 통합

### New Services (Phase 8)
| Service | Description |
|---------|-------------|
| PropertyWriteService | ComAPI Property Write (재시도 로직) |
| SelectionSetService | Selection Set 계층 구조 생성 |
| TimeLinerService | TimeLiner Task 생성 및 Set 연결 |
| AWP4DAutomationService | 통합 파이프라인 (이벤트 기반) |
| ObjectMatcher | SyncID → ModelItem 매칭 (캐싱) |
| AWP4DValidator | Pre/Post 검증 |
| ScheduleCsvParser | 한영 컬럼 매핑 CSV 파싱 |

### Key Documents
- [Phase 8 Document](docs/phases/phase-8-awp-4d-automation.md)
- [Tech Spec: AWP 4D](docs/tech-specs/AWP-4D-Automation-Spec.md)
- [ADR-002: TimeLiner API](docs/adr/ADR-002-TimeLiner-API-Integration.md)

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
│   └── ScheduleCsvParser.cs          # 스케줄 CSV 파싱 (v0.6.0)
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
│   ├── ScheduleBuilderViewModel.cs   # Schedule Builder VM (v0.8.0)
│   ├── ObjectGroupViewModel.cs       # 객체 그룹화 VM (v0.9.0)
│   └── HierarchyNodeViewModel.cs     # 트리 노드
├── Views/                 # WPF Views
│   └── DXwindow.xaml                 # 메인 UI + AWP 4D 탭 + Schedule 탭
├── Models/                # Data models
│   ├── ObjectGroupModel.cs           # 객체 그룹 모델 (v1.0.0)
│   ├── PropertyRecord.cs             # 속성 레코드 (v1.0.0)
│   ├── FilterOption.cs               # 필터 옵션 (v1.0.0)
│   ├── ScheduleData.cs               # 스케줄 데이터 (v0.6.0)
│   ├── AWP4DOptions.cs               # 자동화 옵션 (v0.6.0)
│   ├── AutomationResult.cs           # 실행 결과 (v0.6.0)
│   └── ValidationResult.cs           # 검증 결과 (v0.6.0)
└── docs/
    ├── phases/
    │   └── phase-8-awp-4d-automation.md
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
| Validation | AWP4DValidator.cs | Pre/Post 검증 |
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

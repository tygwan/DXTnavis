# DXTnavis - Navisworks 2025 Property Viewer Plugin

> **Context:** Standalone Navisworks plugin for property viewing and 3D control
> **Version:** 0.5.0 (Released 2026-01-09)
> **Docs Index:** [docs/_INDEX.md](docs/_INDEX.md)

## Quick Reference

### Tech Stack
- C# .NET Framework 4.8 (locked)
- WPF MVVM Pattern
- Navisworks API 2025 (x64 only)
- ComAPI (ViewPoint 저장, Property Write 가능)

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

**→ Changelog:** [CHANGELOG.md](CHANGELOG.md)

---

## v0.5.0 주요 변경사항

### Code Quality ✅
- [x] ViewModel 리팩토링 - 2213줄 → 7개 Partial Class 분리
- [x] 500줄 이하 파일 목표 달성

### New Features ✅
- [x] CSV Viewer UI - 우측 패널 탭으로 추가
- [x] CSV 필터링 및 Export 기능

### Research ✅
- [x] ComAPI Property Write 가능성 조사 → **가능 확인**
- [x] ADR-001 문서 작성 완료

---

## v0.4.x 완료 기능

### Bug Fixes (P0 - Critical) ✅
- [x] 검색창 영어 입력 불가능 오류 (IME + PreviewKeyDown)
- [x] Save ViewPoint 저장 오류 (COM API 기반 구현)

### New Features (P1-P2) ✅
- [x] 트리 레벨별 Expand/Collapse (L0~L5 버튼)
- [x] 4종 CSV 내보내기 (All/Selection × Properties/Hierarchy)
- [x] DisplayString 파싱 (Refined CSV)
- [x] 관측점 초기화 (Reset to Home)
- [x] Object 검색 기능 (이름/속성값/경로)
- [x] Raw/Refined CSV 동시 저장
- [x] CSV Verbose 로깅

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
│   └── PropertyFileWriter.cs         # Property CSV + Verbose 로깅
├── ViewModels/            # MVVM ViewModels (Partial Class 패턴)
│   ├── DXwindowViewModel.cs          # Core (1020줄)
│   ├── DXwindowViewModel.Filter.cs   # 필터 기능 (144줄)
│   ├── DXwindowViewModel.Search.cs   # 검색 기능 (110줄)
│   ├── DXwindowViewModel.Selection.cs # 3D 선택 (219줄)
│   ├── DXwindowViewModel.Snapshot.cs # 스냅샷 (311줄)
│   ├── DXwindowViewModel.Tree.cs     # 트리 (181줄)
│   ├── DXwindowViewModel.Export.cs   # 내보내기 (397줄)
│   ├── CsvViewerViewModel.cs         # CSV 뷰어 VM (v0.5.0)
│   └── HierarchyNodeViewModel.cs     # 트리 노드
├── Views/                 # WPF Views
│   └── DXwindow.xaml                 # 메인 UI + CSV Viewer 탭
├── Models/                # Data models
└── docs/
    ├── agile/
    │   ├── SPRINT-v0.4.0.md
    │   └── SPRINT-v0.5.0.md
    └── adr/
        └── ADR-001-ComAPI-Property-Write.md
```

---

## Completed Features (v0.3.0)

### Phase 1: Property Filtering
- Level Filter (L0~L10)
- SysPath Filter
- TreeView Hierarchy
- Visual Level Badges

### Phase 2: UI Enhancement (70%)
- Level-based Expand/Collapse
- Expand All / Collapse All
- Node Icons (📁/🔷/📄)

### Phase 3: 3D Integration
- Select in 3D
- Show Only / Show All
- Zoom to Selection

---

## Known Issues

| Issue | Priority | Status |
|-------|----------|--------|
| 검색창 영어 입력 불가 | 🔴 Critical | ✅ Fixed |
| ViewPoint 저장 read-only | 🔴 Critical | ✅ Fixed |
| Property Write 불가 (.NET API) | 🟠 High | ✅ Solved (ComAPI)

---

## Critical Constraints

### Thread Safety
```csharp
// ❌ NEVER: Background thread
Task.Run(() => Application.ActiveDocument.xxx);

// ✅ ALWAYS: UI thread only
Application.ActiveDocument.CurrentSelection.Add(items);
```

### COM API
```csharp
// ViewPoint: ComAPI 필요
// Property Write: ComAPI SetUserDefined() 사용 가능
InwGUIPropertyNode2 propNode = comState.GetGUIPropertyNode(comPath, true);
propNode.SetUserDefined(0, "CategoryName", "InternalName", propVec);
```

---

## Key Files

| Task | File | Description |
|------|------|-------------|
| 3D selection | DXwindowViewModel.Selection.cs | SelectIn3D, ShowOnlyFiltered |
| Filter apply | DXwindowViewModel.Filter.cs | ApplyFilter, TriggerFilterDebounce |
| Tree expand | DXwindowViewModel.Tree.cs | ExpandTreeToLevel |
| CSV export | DXwindowViewModel.Export.cs | ExportAllPropertiesAsync |
| CSV viewer | CsvViewerViewModel.cs | LoadCsvFile, ParseCsvFile |
| Snapshot | DXwindowViewModel.Snapshot.cs | CaptureCurrentView |
| Search | DXwindowViewModel.Search.cs | SearchObjects |

---

## Development Workflow

### Agile Skills
```bash
/sprint status              # 진행 현황
/quality-gate pre-commit    # 커밋 전 검증
/feedback learning "내용"    # 학습 기록
/feedback adr "결정사항"     # 아키텍처 결정
```

### Sub-Agents
- `code-reviewer`: 버그 수정 리뷰
- `progress-tracker`: 진행 추적
- `tech-spec-writer`: ComAPI 문서화

---

## Git
- Repo: https://github.com/tygwan/DXTnavis.git
- Branch: main

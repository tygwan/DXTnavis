# DXTnavis - Navisworks 2025 Property Viewer Plugin

> **Context:** Standalone Navisworks plugin for property viewing and 3D control
> **Version:** 0.3.0 → 0.4.0 (In Development)
> **Docs Index:** [docs/_INDEX.md](docs/_INDEX.md)

## Quick Reference

### Tech Stack
- C# .NET Framework 4.8 (locked)
- WPF MVVM Pattern
- Navisworks API 2025 (x64 only)
- ComAPI (Property Write 조사 중)

### Current Status
| Phase | Task | Status |
|-------|------|--------|
| 1 | Property Filtering | ✅ 100% |
| 2 | UI Enhancement | ⚠️ 70% |
| 3 | 3D Object Integration | ✅ 100% |
| 4 | CSV Enhancement | 🔄 In Progress |
| 5 | ComAPI Research | 📋 Planned |

**→ Sprint v0.4.0:** [docs/agile/SPRINT-v0.4.0.md](docs/agile/SPRINT-v0.4.0.md)
**→ Changelog:** [CHANGELOG.md](CHANGELOG.md)

---

## v0.4.0 Roadmap

### Bug Fixes (P0 - Critical)
- [ ] 🔴 검색창 영어 입력 불가능 오류
- [ ] 🔴 Save ViewPoint 저장 오류 (read-only)

### New Features (P1-P2)
- [ ] 🟠 트리 레벨별 Expand/Collapse (각 레벨 개별)
- [ ] 🟠 Selection Properties 출력 (All/Sele × Prop/Hier)
- [ ] 🟠 DisplayString 파싱 (Refined CSV)
- [ ] 🟡 관측점 초기화 기능
- [ ] 🟡 Object 검색 기능
- [ ] 🟡 Raw/Refined CSV 동시 관리

### Research
- [ ] ComAPI Property Write 가능성 조사

---

## Project Structure

```
dxtnavis/
├── Services/              # Business logic
│   ├── NavisworksDataExtractor.cs    # 속성 추출
│   ├── NavisworksSelectionService.cs # 3D 선택
│   ├── HierarchyFileWriter.cs        # Hierarchy CSV
│   └── PropertyFileWriter.cs         # Property CSV
├── ViewModels/            # MVVM ViewModels
│   ├── DXwindowViewModel.cs          # 메인 VM
│   └── HierarchyNodeViewModel.cs     # 트리 노드
├── Views/                 # WPF Views
│   └── DXwindow.xaml                 # 메인 UI
├── Models/                # Data models
└── docs/
    └── agile/
        ├── SPRINT-CURRENT.md
        └── SPRINT-v0.4.0.md
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
| 검색창 영어 입력 불가 | 🔴 Critical | Open |
| ViewPoint 저장 read-only | 🔴 Critical | Open |

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
// Property Write: 조사 필요 (read-only 제약)
```

---

## Key Files

| Task | File | Line |
|------|------|------|
| 3D selection | NavisworksSelectionService.cs | :45 |
| Filter apply | DXwindowViewModel.cs | :441 |
| Tree expand | DXwindowViewModel.cs | :520 |
| CSV export | PropertyFileWriter.cs | - |

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

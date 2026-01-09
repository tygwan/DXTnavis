# DXTnavis - Navisworks 2025 Property Viewer Plugin

> **Context:** Standalone Navisworks plugin for property viewing and 3D control
> **Version:** 0.4.2 (Released 2026-01-09)
> **Docs Index:** [docs/_INDEX.md](docs/_INDEX.md)

## Quick Reference

### Tech Stack
- C# .NET Framework 4.8 (locked)
- WPF MVVM Pattern
- Navisworks API 2025 (x64 only)
- ComAPI (ViewPoint 저장)

### Current Status
| Phase | Task | Status |
|-------|------|--------|
| 1 | Property Filtering | ✅ 100% |
| 2 | UI Enhancement | ✅ 100% |
| 3 | 3D Object Integration | ✅ 100% |
| 4 | CSV Enhancement | ✅ 100% |
| 5 | ComAPI Research | 📋 Planned |

**→ Changelog:** [CHANGELOG.md](CHANGELOG.md)

---

## v0.4.0 완료 기능

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

### Research
- [ ] ComAPI Property Write 가능성 조사

---

## Project Structure

```
dxtnavis/
├── Services/              # Business logic
│   ├── NavisworksDataExtractor.cs    # 속성 추출
│   ├── NavisworksSelectionService.cs # 3D 선택
│   ├── DisplayStringParser.cs        # VariantData 타입 파싱 (v0.4.0)
│   ├── SnapshotService.cs            # 뷰포인트/캡처
│   ├── HierarchyFileWriter.cs        # Hierarchy CSV
│   └── PropertyFileWriter.cs         # Property CSV + Verbose 로깅
├── ViewModels/            # MVVM ViewModels
│   ├── DXwindowViewModel.cs          # 메인 VM
│   └── HierarchyNodeViewModel.cs     # 트리 노드
├── Views/                 # WPF Views
│   └── DXwindow.xaml                 # 메인 UI
├── Models/                # Data models
└── docs/
    └── agile/
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
| 검색창 영어 입력 불가 | 🔴 Critical | ✅ Fixed |
| ViewPoint 저장 read-only | 🔴 Critical | ✅ Fixed |

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

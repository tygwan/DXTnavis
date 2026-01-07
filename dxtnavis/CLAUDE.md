# DXTnavis - Navisworks 2025 Property Viewer Plugin

> **Context:** Standalone Navisworks plugin for property viewing and 3D control
> **Version:** 0.3.0
> **Docs Index:** [docs/_INDEX.md](docs/_INDEX.md)

## Quick Reference

### Tech Stack
- C# .NET Framework 4.8 (locked)
- WPF MVVM Pattern
- Navisworks API 2025 (x64 only)

### Current Status (v0.3.0)
| Phase | Task | Status |
|-------|------|--------|
| 1 | Property Filtering | ✅ 100% |
| 2 | UI Enhancement | ⚠️ 70% |
| 3 | 3D Object Integration | ✅ 100% |
| 4 | 3D Snapshot | 📋 Planned |
| 5 | Data Validation | 📋 Planned |

**→ Sprint:** [docs/agile/SPRINT-CURRENT.md](docs/agile/SPRINT-CURRENT.md)
**→ Changelog:** [CHANGELOG.md](CHANGELOG.md)

---

## Project Structure

```
dxtnavis/
├── Services/              # Business logic
│   ├── NavisworksDataExtractor.cs    # Phase 1: 속성 추출
│   ├── NavisworksSelectionService.cs # Phase 3: 3D 선택
│   └── HierarchyFileWriter.cs        # CSV/JSON 내보내기
├── ViewModels/            # MVVM ViewModels
│   ├── DXwindowViewModel.cs          # 메인 VM
│   └── HierarchyNodeViewModel.cs     # 트리 노드
├── Views/                 # WPF Views
│   └── DXwindow.xaml                 # 메인 UI
├── Models/                # Data models
│   ├── HierarchicalPropertyRecord.cs
│   └── TreeNodeModel.cs
└── docs/
    ├── _INDEX.md          # Navigation
    └── agile/             # Agile documents
        └── SPRINT-CURRENT.md
```

---

## Completed Features

### Phase 1: Property Filtering
- Level Filter (L0~L10) - 레벨별 필터링
- SysPath Filter - 경로 기반 필터링
- TreeView Hierarchy - 계층 구조 시각화
- Visual Level Badges - 색상 코딩

### Phase 2: UI Enhancement (70%)
- Level-based Expand/Collapse (L0~L10)
- Expand All / Collapse All
- Node Icons (📁/🔷/📄)

### Phase 3: 3D Integration
- Select in 3D - 필터링된 객체 선택
- Show Only / Show All - 가시성 제어
- Zoom to Selection - 카메라 이동

---

## Critical Constraints

### Thread Safety
```csharp
// ❌ NEVER: Background thread with Navisworks API
Task.Run(() => Application.ActiveDocument.xxx);

// ✅ ALWAYS: UI thread only
Application.ActiveDocument.CurrentSelection.Add(items);
```

### COM API for Image Export
```csharp
// Use DriveIOPlugin, NOT DriveImage
comState.DriveIOPlugin("lcodpimage", path, options);
```

---

## Key Files

| Task | File | Line |
|------|------|------|
| 3D selection | NavisworksSelectionService.cs | :45 |
| Filter apply | DXwindowViewModel.cs | :441 |
| Tree expand | DXwindowViewModel.cs | :520 |
| Level filter | DXwindowViewModel.cs | :380 |

---

## Documentation

| Doc | Purpose |
|-----|---------|
| [Sprint](docs/agile/SPRINT-CURRENT.md) | Current sprint status |
| [Changelog](CHANGELOG.md) | Version history |
| [_INDEX.md](docs/_INDEX.md) | Full navigation |

---

## Git
- Repo: https://github.com/tygwan/DXTnavis.git
- Branch: main

---

## Recent Changes (v0.3.0)
- feat: Level-based tree expand/collapse
- feat: 3D object selection and visibility control
- feat: Level and SysPath filtering

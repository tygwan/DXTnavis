# DXTnavis Documentation Index

> **Last Updated:** 2026-01-11
> **Current Version:** v0.5.0
> **Next Version:** v0.6.0 (AWP 4D Automation)

---

## Quick Navigation

| Category | Document | Description |
|----------|----------|-------------|
| **Overview** | [CLAUDE.md](../CLAUDE.md) | Project context & quick ref |
| **Changelog** | [CHANGELOG.md](../CHANGELOG.md) | Version history |
| **README** | [README.md](../README.md) | Public documentation |

---

## Development Status

### Overall Progress
```
v0.5.0: [====================] 100% (Released 2026-01-09)
v0.6.0: [====                ] 20% (Planning - AWP 4D)
```

### Phase Status

| Phase | Task | Status | Progress | Doc |
|-------|------|--------|----------|-----|
| 1 | CSV Property Export | ✅ Complete | 100% | [phase-1](phases/phase-1-csv-export.md) |
| 2 | Filtering & UI | ✅ Complete | 100% | [phase-2](phases/phase-2-filtering-ui.md) |
| 3 | 3D Object Integration | ✅ Complete | 100% | [phase-3](phases/phase-3-3d-integration.md) |
| 4 | 3D Snapshot | ✅ Complete | 100% | [phase-4](phases/phase-4-snapshot-workflow.md) |
| 5 | Data Validation | ✅ Complete | 100% | [phase-5](phases/phase-5-data-validation.md) |
| 6 | Code Quality | ✅ Complete | 100% | - |
| 7 | CSV Viewer | ✅ Complete | 100% | - |
| **8** | **AWP 4D Automation** | 📋 Planning | 20% | [phase-8](phases/phase-8-awp-4d-automation.md) |

---

## v0.6.0 Roadmap: AWP 4D Automation

### Research Completed
- [x] ComAPI Property Write 가능성 조사 → **가능** (ADR-001)
- [x] TimeLiner API 연동 가능성 조사 → **가능** (ADR-002)
- [x] Selection Set 생성 API 검토 → **가능**
- [x] Task-Set 연결 방식 검토 → **가능**

### Implementation Plan

| Sprint | Task | Priority | Status |
|--------|------|----------|--------|
| Sprint 1 | PropertyWriteService 구현 | 🔴 P0 | 📋 Planned |
| Sprint 1 | ObjectMatcher 구현 | 🔴 P0 | 📋 Planned |
| Sprint 2 | SelectionSetService 구현 | 🟠 P1 | 📋 Planned |
| Sprint 2 | TimeLinerService 구현 | 🟠 P1 | 📋 Planned |
| Sprint 3 | AWP4DAutomationService 통합 | 🟠 P1 | 📋 Planned |
| Sprint 3 | UI Integration | 🟡 P2 | 📋 Planned |

### Key Documents
- **Tech Spec**: [AWP-4D-Automation-Spec.md](tech-specs/AWP-4D-Automation-Spec.md)
- **ADR-001**: [ComAPI Property Write](adr/ADR-001-ComAPI-Property-Write.md)
- **ADR-002**: [TimeLiner API Integration](adr/ADR-002-TimeLiner-API-Integration.md)

---

## Document Structure

```
docs/
├── _INDEX.md                    # This file
├── agile/                       # Agile tracking
│   ├── SPRINT-CURRENT.md
│   ├── SPRINT-v0.4.0.md
│   ├── SPRINT-v0.4.1.md
│   ├── SPRINT-v0.4.2.md
│   └── SPRINT-v0.5.0.md
├── adr/                         # Architecture Decision Records
│   ├── ADR-001-ComAPI-Property-Write.md      # ✅ Accepted
│   └── ADR-002-TimeLiner-API-Integration.md  # ✅ Accepted (NEW)
├── phases/                      # Phase documentation
│   ├── phase-1-csv-export.md
│   ├── phase-2-filtering-ui.md
│   ├── phase-3-3d-integration.md
│   ├── phase-4-snapshot-workflow.md
│   └── phase-5-data-validation.md
├── prd/                         # Product Requirements
│   ├── 3d-snapshot-workflow-prd.md
│   └── v0.4.0-feature-expansion-prd.md
├── progress/                    # Progress tracking
│   └── status.md
└── tech-specs/                  # Technical Specifications
    ├── 3d-snapshot-workflow-spec.md
    ├── v0.4.0-tech-spec.md
    └── AWP-4D-Automation-Spec.md  # NEW
```

---

## Key Implementation Files

### Current Services (v0.5.0)
| File | Purpose | Phase | Status |
|------|---------|-------|--------|
| NavisworksDataExtractor.cs | Property extraction | P1 | ✅ |
| NavisworksSelectionService.cs | 3D selection/visibility | P3 | ✅ |
| HierarchyFileWriter.cs | Hierarchy CSV/JSON | P1 | ✅ |
| PropertyFileWriter.cs | Property CSV | P1 | ✅ |
| SnapshotService.cs | Image capture, ViewPoint | P4 | ✅ |
| DisplayStringParser.cs | VariantData parsing | P4 | ✅ |

### Planned Services (v0.6.0)
| File | Purpose | Phase | Status |
|------|---------|-------|--------|
| PropertyWriteService.cs | ComAPI Property Write | P8 | 📋 |
| SelectionSetService.cs | Selection Set 생성 | P8 | 📋 |
| TimeLinerService.cs | TimeLiner Task 생성 | P8 | 📋 |
| AWP4DAutomationService.cs | 통합 파이프라인 | P8 | 📋 |
| ObjectMatcher.cs | SyncID → ModelItem 매칭 | P8 | 📋 |

### ViewModels (Partial Class Pattern)
| File | Lines | Key Features |
|------|-------|--------------|
| DXwindowViewModel.cs | 1020 | Core VM |
| DXwindowViewModel.Filter.cs | 144 | Filter logic |
| DXwindowViewModel.Search.cs | 110 | Search logic |
| DXwindowViewModel.Selection.cs | 219 | 3D selection |
| DXwindowViewModel.Snapshot.cs | 311 | Snapshot/ViewPoint |
| DXwindowViewModel.Tree.cs | 181 | Tree expand/collapse |
| DXwindowViewModel.Export.cs | 397 | CSV export |
| CsvViewerViewModel.cs | - | CSV Viewer (v0.5.0) |

---

## Architecture Decision Records (ADR)

| ADR | Title | Status | Date |
|-----|-------|--------|------|
| [ADR-001](adr/ADR-001-ComAPI-Property-Write.md) | ComAPI를 통한 Custom Property Write | ✅ Accepted | 2026-01-09 |
| [ADR-002](adr/ADR-002-TimeLiner-API-Integration.md) | TimeLiner API를 통한 4D 자동화 | ✅ Accepted | 2026-01-11 |

---

## Known Issues

| Issue | Priority | Phase | Status |
|-------|----------|-------|--------|
| ~~검색창 영어 입력 불가~~ | ~~Critical~~ | P2 | ✅ Fixed (v0.4.0) |
| ~~ViewPoint 저장 read-only~~ | ~~Critical~~ | P4 | ✅ Fixed (v0.4.3) |

---

## Version History

| Version | Date | Key Features |
|---------|------|--------------|
| **v0.5.0** | 2026-01-09 | ViewModel 리팩토링, CSV Viewer, ComAPI Research |
| v0.4.3 | 2026-01-09 | 필터 자동 적용, Show Only 토글, ViewPoint 저장 수정 |
| v0.4.2 | 2026-01-09 | Unit 컬럼, AccessViolation 처리 |
| v0.4.1 | 2026-01-08 | 트리 계층 구조 수정 |
| v0.4.0 | 2026-01-08 | 검색창 수정, CSV 4종, DisplayString 파싱 |
| v0.3.0 | 2026-01-06 | Tree expand/collapse |
| v0.2.0 | 2026-01-05 | 3D selection, visibility |
| v0.1.0 | 2026-01-03 | Level filter, SysPath filter |
| v0.0.1 | 2025-12-29 | Initial setup |

---

## Quick Links

### v0.6.0 Documents (AWP 4D)
- [Tech Spec: AWP 4D Automation](tech-specs/AWP-4D-Automation-Spec.md)
- [ADR-002: TimeLiner API](adr/ADR-002-TimeLiner-API-Integration.md)

### v0.5.0 Documents
- [Sprint v0.5.0](agile/SPRINT-v0.5.0.md)
- [ADR-001: ComAPI Property Write](adr/ADR-001-ComAPI-Property-Write.md)

### General
- [Changelog](../CHANGELOG.md)
- [CLAUDE.md](../CLAUDE.md)
- [GitHub](https://github.com/tygwan/DXTnavis)

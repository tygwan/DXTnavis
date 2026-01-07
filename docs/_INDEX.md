# DXTnavis Documentation Index

> **Last Updated:** 2026-01-08
> **Current Version:** v0.3.0
> **Next Version:** v0.4.0 (In Development)

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
v0.3.0: [====================] 100%
v0.4.0: [                    ] 0% (Planning)
```

### Phase Status

| Phase | Task | Status | Progress | Doc |
|-------|------|--------|----------|-----|
| 1 | CSV Property Export | ✅ Complete | 100% | [phase-1](phases/phase-1-csv-export.md) |
| 2 | Filtering & UI | ⚠️ Partial | 70% | [phase-2](phases/phase-2-filtering-ui.md) |
| 3 | 3D Object Integration | ✅ Complete | 100% | [phase-3](phases/phase-3-3d-integration.md) |
| 4 | 3D Snapshot | ⚠️ Partial | 50% | [phase-4](phases/phase-4-snapshot-workflow.md) |
| 5 | Data Validation | 📋 Planned | 0% | [phase-5](phases/phase-5-data-validation.md) |

---

## v0.4.0 Roadmap

### Bug Fixes (P0)
- [ ] 검색창 영어 입력 불가능 오류
- [ ] Save ViewPoint 저장 오류 (read-only)

### New Features (P1-P2)
- [ ] 트리 레벨별 Expand/Collapse
- [ ] Selection Properties 출력
- [ ] DisplayString 파싱 (Refined CSV)
- [ ] 관측점 초기화 기능
- [ ] Object 검색 기능
- [ ] Raw/Refined CSV 동시 관리

### Research
- [ ] ComAPI Property Write 가능성 조사

**→ [Sprint v0.4.0](agile/SPRINT-v0.4.0.md)**

---

## Document Structure

```
docs/
├── _INDEX.md              # This file
├── agile/                 # Agile tracking
│   ├── SPRINT-CURRENT.md  # Current sprint
│   └── SPRINT-v0.4.0.md   # v0.4.0 sprint plan
├── phases/                # Phase documentation
│   ├── phase-1-csv-export.md
│   ├── phase-2-filtering-ui.md    # Updated 2026-01-08
│   ├── phase-3-3d-integration.md
│   ├── phase-4-snapshot-workflow.md  # Updated 2026-01-08
│   └── phase-5-data-validation.md
├── prd/                   # Product Requirements
│   ├── 3d-snapshot-workflow-prd.md
│   └── v0.4.0-feature-expansion-prd.md  # NEW
├── progress/              # Progress tracking
│   └── status.md          # Updated 2026-01-08
└── tech-specs/            # Technical specs
    ├── 3d-snapshot-workflow-spec.md
    └── v0.4.0-tech-spec.md  # NEW
```

---

## Key Implementation Files

### Services
| File | Purpose | Phase | Status |
|------|---------|-------|--------|
| NavisworksDataExtractor.cs | Property extraction | P1 | ✅ |
| NavisworksSelectionService.cs | 3D selection/visibility | P3 | ✅ |
| HierarchyFileWriter.cs | Hierarchy CSV/JSON | P1 | ✅ |
| PropertyFileWriter.cs | Property CSV | P1 | ✅ |
| SnapshotService.cs | Image capture | P4 | ⚠️ |

### ViewModels
| File | Key Features |
|------|--------------|
| DXwindowViewModel.cs | Main VM, commands, filtering |
| HierarchyNodeViewModel.cs | Tree node representation |

---

## Known Issues

| Issue | Priority | Phase | Status |
|-------|----------|-------|--------|
| 검색창 영어 입력 불가 | 🔴 Critical | P2 | Open |
| ViewPoint 저장 read-only | 🔴 Critical | P4 | Open |
| 트리 레벨별 Expand 미구현 | 🟠 High | P2 | Open |

---

## Version History

| Version | Date | Key Features |
|---------|------|--------------|
| v0.3.0 | 2026-01-06 | Tree expand/collapse, 3D integration |
| v0.2.0 | 2026-01-05 | 3D selection, visibility control |
| v0.1.0 | 2026-01-03 | Level filter, SysPath filter |
| v0.0.1 | 2025-12-29 | Initial setup |

---

## Quick Links

### v0.4.0 Documents
- [Sprint v0.4.0](agile/SPRINT-v0.4.0.md) - 스프린트 계획
- [PRD v0.4.0](prd/v0.4.0-feature-expansion-prd.md) - 제품 요구사항
- [Tech Spec v0.4.0](tech-specs/v0.4.0-tech-spec.md) - 기술 설계서

### General
- [Changelog](../CHANGELOG.md)
- [CLAUDE.md](../CLAUDE.md)
- [GitHub](https://github.com/tygwan/DXTnavis)

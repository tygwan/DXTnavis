# DXTnavis Documentation Index

> **Last Updated:** 2026-01-13
> **Current Version:** v0.7.0 (Data Validation, UI Enhancement)
> **Next Version:** v0.8.0 (Load Optimization)

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
v0.6.0: [====================] 100% (Released 2026-01-11)
v0.7.0: [====================] 100% (Released 2026-01-13)
v0.8.0: [                    ]   0% (Planning - Load Optimization)
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
| 8 | AWP 4D Automation | ✅ Complete | 100% | [phase-8](phases/phase-8-awp-4d-automation.md) |
| 9 | UI Enhancement | ✅ Complete | 100% | [phase-9](phases/phase-9-ui-enhancement.md) |
| **10** | **Load Optimization** | 📋 Planning | 0% | [phase-10](phases/phase-10-load-optimization.md) |

---

## v0.8.0 Roadmap: Load Optimization

### Problem Analysis
- [x] 현재 LoadHierarchy 구현 분석 완료
- [x] 성능 병목 지점 식별 (이중 순회, UI 블로킹)
- [ ] 벤치마크 테스트

### Implementation Plan

| Sprint | Task | Priority | Status |
|--------|------|----------|--------|
| Sprint 1 | 비동기 로딩 (Task.Run) | 🔴 P0 | 📋 Planned |
| Sprint 1 | 진행률 표시 (ProgressBar) | 🔴 P0 | 📋 Planned |
| Sprint 2 | 취소 기능 (CancellationToken) | 🟠 P1 | 📋 Planned |
| Sprint 3 | 단일 순회 최적화 | 🟠 P1 | 📋 Planned |
| Sprint 4 | TreeView 가상화 | 🟡 P2 | 📋 Planned |

### Performance Targets

| Metric | Current | Target |
|--------|---------|--------|
| 10K 노드 로딩 | ~15초 | ~5초 |
| UI 응답성 | 프리징 | 60 FPS |
| 메모리 피크 | ~500MB | ~300MB |

### Key Documents
- **Phase Doc**: [Phase 10: Load Optimization](phases/phase-10-load-optimization.md)

---

## v0.7.0 Completed: Data Validation & UI Enhancement

### Features Released (2026-01-13)
- [x] ValidationService 구현 (단위/타입/필수속성 검증)
- [x] Select All 체크박스 (전체 선택/해제)
- [x] 객체별/카테고리별 그룹화 표시
- [x] Expand/Collapse All 버튼
- [x] AWP 4D 테스트 CSV 샘플

### Key Documents
- **Phase Doc**: [Phase 9: UI Enhancement](phases/phase-9-ui-enhancement.md)
- **Phase Doc**: [Phase 5: Data Validation](phases/phase-5-data-validation.md)

---

## v0.6.0 Completed: AWP 4D Automation

### Features Released (2026-01-11)
- [x] CSV → TimeLiner 자동 연결 파이프라인
- [x] Property Write (ComAPI SetUserDefined)
- [x] Selection Set 계층 구조 자동 생성
- [x] TimeLiner Task 자동 생성 및 Set 연결
- [x] AWP 4D 탭 UI 통합

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
│   ├── phase-5-data-validation.md
│   ├── phase-8-awp-4d-automation.md
│   ├── phase-9-ui-enhancement.md
│   └── phase-10-load-optimization.md  # NEW (v0.8.0)
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

### Current Services (v0.6.0)
| File | Purpose | Phase | Status |
|------|---------|-------|--------|
| NavisworksDataExtractor.cs | Property extraction | P1 | ✅ |
| NavisworksSelectionService.cs | 3D selection/visibility | P3 | ✅ |
| HierarchyFileWriter.cs | Hierarchy CSV/JSON | P1 | ✅ |
| PropertyFileWriter.cs | Property CSV | P1 | ✅ |
| SnapshotService.cs | Image capture, ViewPoint | P4 | ✅ |
| DisplayStringParser.cs | VariantData parsing | P4 | ✅ |
| PropertyWriteService.cs | ComAPI Property Write | P8 | ✅ |
| SelectionSetService.cs | Selection Set 생성 | P8 | ✅ |
| TimeLinerService.cs | TimeLiner Task 생성 | P8 | ✅ |
| AWP4DAutomationService.cs | 통합 파이프라인 | P8 | ✅ |
| ObjectMatcher.cs | SyncID → ModelItem 매칭 | P8 | ✅ |
| AWP4DValidator.cs | Pre/Post 검증 | P8 | ✅ |
| ScheduleCsvParser.cs | 한영 컬럼 매핑 CSV 파싱 | P8 | ✅ |

### Completed Changes (v0.7.0)
| File | Purpose | Phase | Status |
|------|---------|-------|--------|
| ValidationService.cs | 속성 검증 서비스 | P5 | ✅ |
| DXwindow.xaml | Select All, GroupStyle 추가 | P9 | ✅ |
| DXwindowViewModel.cs | SelectAllCommand, ValidateCommand 구현 | P5/P9 | ✅ |
| PropertyItemViewModel.cs | 속성 그룹화 ViewModel | P9 | ✅ |
| test_schedule_awp4d.csv | AWP 4D 테스트 샘플 | P9 | ✅ |

### Planned Changes (v0.8.0)
| File | Purpose | Phase | Status |
|------|---------|-------|--------|
| LoadHierarchyService.cs | 최적화된 로딩 서비스 | P10 | 📋 |
| DXwindowViewModel.cs | LoadModelHierarchyAsync 리팩토링 | P10 | 📋 |
| DXwindow.xaml | ProgressBar, Cancel 버튼 추가 | P10 | 📋 |
| LoadProgress.cs | 진행률 모델 | P10 | 📋 |

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
| AWP4DViewModel.cs | - | AWP 4D Automation (v0.6.0) |

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
| **v0.7.0** | 2026-01-13 | Data Validation, Grouped Property View, Select All |
| v0.6.0 | 2026-01-11 | AWP 4D Automation, TimeLiner 연동, Property Write |
| v0.5.0 | 2026-01-09 | ViewModel 리팩토링, CSV Viewer, ComAPI Research |
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

### v0.8.0 Documents (Load Optimization) - Planning
- [Phase 10: Load Optimization](phases/phase-10-load-optimization.md)

### v0.7.0 Documents (Data Validation & UI) - Released
- [Phase 9: UI Enhancement](phases/phase-9-ui-enhancement.md)
- [Phase 5: Data Validation](phases/phase-5-data-validation.md)

### v0.6.0 Documents (AWP 4D)
- [Phase 8: AWP 4D Automation](phases/phase-8-awp-4d-automation.md)
- [Tech Spec: AWP 4D Automation](tech-specs/AWP-4D-Automation-Spec.md)
- [ADR-002: TimeLiner API](adr/ADR-002-TimeLiner-API-Integration.md)
- [ADR-001: ComAPI Property Write](adr/ADR-001-ComAPI-Property-Write.md)

### General
- [Changelog](../CHANGELOG.md)
- [CLAUDE.md](../CLAUDE.md)
- [GitHub](https://github.com/tygwan/DXTnavis)

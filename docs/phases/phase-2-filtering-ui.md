# Phase 2: Filtering & UI Layout

> **Status:** ⚠️ Partial (70%)
> **Parent:** [_INDEX](../_INDEX.md) | **Prev:** [Phase 1](phase-1-csv-export.md) | **Next:** [Phase 3](phase-3-3d-integration.md)
> **Last Updated:** 2026-01-08

## Overview
조건부 필터링 및 UI 레이아웃 개선

---

## Completed (v0.1.0 ~ v0.3.0)

### Requirements (FR-201~203)
| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| FR-201 | HasGeometry 필터 | P0 | ✅ |
| FR-202 | IsHidden 필터 | P0 | ✅ |
| FR-203 | 속성 기반 조건 필터 (Category, Property, Value) | P0 | ✅ |

### UI Improvements Completed
| Feature | Status | Version |
|---------|--------|---------|
| Tree Expand/Collapse | ✅ Complete | v0.3.0 |
| Level selector (1-10) | ✅ Complete | v0.1.0 |
| Color-coded badges | ✅ Complete | v0.1.0 |
| Node icons (📁/🔷/📄) | ✅ Complete | v0.1.0 |
| Expand All / Collapse All | ✅ Complete | v0.3.0 |

---

## v0.4.0 Planned

### New Requirements
| ID | Requirement | Priority | Status |
|----|-------------|----------|--------|
| FR-204 | 복합 조건 (AND/OR) 지원 | P1 | ⏳ |
| FR-205 | 필터 프리셋 저장 | P2 | ⏳ |
| FR-206 | 레벨별 개별 Expand/Collapse | P1 | ⏳ |
| FR-207 | 검색창 영어 입력 버그 수정 | P0 | ⏳ |
| FR-208 | Object 검색 기능 | P2 | ⏳ |

### UI Improvements Planned
| Feature | Priority | Status |
|---------|----------|--------|
| Level별 개별 Expand/Collapse 버튼 | P1 | ⏳ |
| SearchBox UI | P2 | ⏳ |
| 검색 결과 하이라이트 | P2 | ⏳ |

---

## Known Issues

| Issue | Priority | Status |
|-------|----------|--------|
| 검색창 영어 입력 불가 | 🔴 Critical | Open |
| 트리 레벨별 Expand 미구현 | 🟠 High | Open |

---

## Implementation

### Key Files
- `ViewModels/DXwindowViewModel.cs:441-499` - ApplyFilter, ClearFilter
- `ViewModels/DXwindowViewModel.cs:520+` - Tree expand/collapse
- `Models/TreeNodeModel.cs` - ExpandToLevel, CollapseAll

### Key Methods
```csharp
// DXwindowViewModel.cs
private void ApplyFilter()
private void ExpandTreeToLevel(int targetLevel)
private void CollapseAllTreeNodes()

// v0.4.0 planned
public void ExpandToLevel(int targetLevel)
public void CollapseFromLevel(int targetLevel)
public void ToggleLevel(int level)
```

---

## References
- [Sprint v0.4.0](../agile/SPRINT-v0.4.0.md)
- [Tech Spec v0.4.0](../tech-specs/v0.4.0-tech-spec.md)

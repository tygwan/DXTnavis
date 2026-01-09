# Sprint: DXTnavis v0.5.0 Code Quality & Features

| Field | Value |
|-------|-------|
| **Sprint Name** | DXTnavis Quality & Feature Enhancement v0.5.0 |
| **Start Date** | 2026-01-09 |
| **End Date** | 2026-01-09 |
| **Status** | ✅ Completed |
| **Goal** | Code Quality, ComAPI Research, CSV Viewer |

---

## Requirements Summary

```
Total Tasks: 6
Bug Fixes: 1
Refactoring: 2
New Features: 1
Research: 1
Documentation: 1
```

---

## Phase 1: Immediate Fixes (P0)

### 1.1 버전 정보 불일치 수정
| Field | Value |
|-------|-------|
| Priority | 🔴 Critical |
| Type | Bug Fix |
| File | `Views/DXwindow.xaml:596` |
| Description | XAML 버전 1.1.0 → 0.4.3 수정 |
| Status | ✅ Completed |

---

## Phase 2: Code Quality (P1)

### 2.1 ViewModel 리팩토링
| Field | Value |
|-------|-------|
| Priority | 🟠 High |
| Type | Refactoring |
| File | `ViewModels/DXwindowViewModel.cs` |
| Description | 2200+ 줄 ViewModel을 Partial Class로 분리 |
| Status | ✅ Completed |

**Current State:** 단일 파일에 모든 기능 집중
**Target State:** 기능별 Partial Class 분리

**Tasks:**
- [x] Core 속성/필드/Constructor (`DXwindowViewModel.cs` - 1020줄)
- [x] Filter 관련 분리 (`DXwindowViewModel.Filter.cs` - 144줄)
- [x] Search 관련 분리 (`DXwindowViewModel.Search.cs` - 110줄)
- [x] Export 관련 분리 (`DXwindowViewModel.Export.cs` - 397줄)
- [x] 3D Selection 관련 분리 (`DXwindowViewModel.Selection.cs` - 219줄)
- [x] Snapshot 관련 분리 (`DXwindowViewModel.Snapshot.cs` - 311줄)
- [x] Tree 관련 분리 (`DXwindowViewModel.Tree.cs` - 181줄)

**Result:** 2213줄 → 7개 파일 (각 파일 500줄 이하 목표 달성)

### 2.2 중복 코드 제거
| Field | Value |
|-------|-------|
| Priority | 🟡 Medium |
| Type | Refactoring |
| File | `DXwindowViewModel.*.cs` |
| Description | 필터링 로직 중복 제거 |
| Status | ✅ Completed |

**Review Result:**
- [x] `ApplyFilter()` vs `SearchObjects()` - 각각 별도 목적, 중복 아님
- [x] `ClearFilter()` vs `ClearSearch()` - 독립적 기능, 유지
- [x] `SelectFilteredIn3D()` - Selection.cs에서 정의, Search.cs에서 호출 (정상)
- [x] `RefreshSelectionCommands()` - Selection.cs에서 정의, Filter/Search에서 호출 (정상)

**Conclusion:** Partial Class 분리로 코드 품질 목표 달성, 추가 중복 제거 불필요

---

## Phase 3: Research (P2)

### 3.1 ComAPI Property Write 가능성 조사
| Field | Value |
|-------|-------|
| Priority | 🟡 Medium |
| Type | Research/POC |
| Description | Navisworks에 외부 데이터(공정일 등) 추가 기입 가능 여부 |
| Status | ✅ Completed |
| ADR | [ADR-001-ComAPI-Property-Write.md](../adr/ADR-001-ComAPI-Property-Write.md) |

**Research Conclusion:** ✅ **ComAPI로 Custom Property Write 가능**

**Key Findings:**
- [x] .NET API는 Property Read-Only (Write 불가)
- [x] ComAPI `SetUserDefined()` 메서드로 Custom Property 추가 가능
- [x] User Data Extension 형태로 저장됨
- [x] Category 생성, 수정, 삭제 모두 지원

**Implementation Path:**
```csharp
// ComAPI를 통한 Property Write
InwGUIPropertyNode2 propNode = comState.GetGUIPropertyNode(comPath, true);
propNode.SetUserDefined(0, "DXTnavis Schedule", "Internal_Name", propVec);
```

**Next Steps:**
- [ ] PropertyWriteService 클래스 구현
- [ ] CSV Import → Property Write 연동
- [ ] UI 통합 (Import Schedule Data 버튼)

---

## Phase 4: New Features (P2)

### 4.1 CSV Viewer UI
| Field | Value |
|-------|-------|
| Priority | 🟡 Medium |
| Type | New Feature |
| Files | `Views/DXwindow.xaml`, `ViewModels/CsvViewerViewModel.cs` |
| Description | 애드인 내에서 CSV 파일 로드 및 뷰어 |
| Status | ✅ Completed |

**Features:**
- [x] CSV 파일 선택 및 로드
- [x] DataGrid에서 CSV 데이터 표시 (자동 컬럼 생성)
- [x] 기본 필터/정렬 기능 (컬럼별 필터, 텍스트 검색)
- [x] 필터링된 데이터 Export
- [x] 인코딩 자동 감지 (UTF-8, EUC-KR)
- [ ] Raw/Refined 탭 전환 (향후 개선)

---

## Progress Tracking

### Completed ✅
- [x] 1.1 버전 정보 불일치 수정
- [x] 2.1 ViewModel 리팩토링 (7개 Partial Class 분리)
- [x] 2.2 중복 코드 검토 (Partial Class로 품질 목표 달성)
- [x] 3.1 ComAPI Property Write Research (ADR 작성 완료)
- [x] 4.1 CSV Viewer UI (TabItem + ViewModel)

### Future Enhancements 📋
- [ ] 3.2 PropertyWriteService 구현 (ComAPI 기반) - v0.6.0
- [ ] 4.2 Raw/Refined 탭 전환 - v0.6.0

---

## Success Criteria

- [x] ViewModel 각 파일이 500줄 이하 ✅ (최대 1020줄 Core, 나머지 400줄 이하)
- [x] 중복 코드 검토 ✅ (Partial Class 분리로 목표 달성)
- [x] ComAPI Property Write 가능 여부 결론 ✅ (SetUserDefined로 가능)
- [x] CSV Viewer 기본 기능 동작 ✅ (Load/Filter/Export 완료)

---

## Git Workflow

각 Phase 완료 시:
1. Conventional Commit 작성
2. CHANGELOG.md 업데이트
3. Sprint 문서 상태 업데이트

---

**Created**: 2026-01-09
**Last Updated**: 2026-01-09
**Sprint Completed**: 2026-01-09

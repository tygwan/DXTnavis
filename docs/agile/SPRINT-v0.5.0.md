# Sprint: DXTnavis v0.5.0 Code Quality & Features

| Field | Value |
|-------|-------|
| **Sprint Name** | DXTnavis Quality & Feature Enhancement v0.5.0 |
| **Start Date** | 2026-01-09 |
| **End Date** | TBD |
| **Status** | 🔄 In Progress |
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
| File | `DXwindowViewModel.cs` |
| Description | 필터링 로직 중복 제거 |

**Issues:**
- [ ] `ApplyFilter()` vs `SearchObjects()` 로직 통합
- [ ] `ClearFilter()` vs `ClearSearch()` 로직 통합
- [ ] `SelectFilteredIn3D()` vs `SelectIn3D()` 중복 제거
- [ ] 미사용 `ShowAllObjects` Command 정리

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
| Files | `Views/CsvViewerControl.xaml`, `ViewModels/CsvViewerViewModel.cs` |
| Description | 애드인 내에서 CSV 파일 로드 및 뷰어 |

**Features:**
- [ ] CSV 파일 선택 및 로드
- [ ] DataGrid에서 CSV 데이터 표시
- [ ] Raw/Refined 탭 전환
- [ ] 기본 필터/정렬 기능
- [ ] 컬럼 숨기기/표시

---

## Progress Tracking

### Completed ✅
- [x] 1.1 버전 정보 불일치 수정
- [x] 2.1 ViewModel 리팩토링 (7개 Partial Class 분리)
- [x] 3.1 ComAPI Property Write Research (ADR 작성 완료)

### In Progress 🔄
- [ ] 4.1 CSV Viewer UI

### Pending 📋
- [ ] 2.2 중복 코드 제거
- [ ] 3.2 PropertyWriteService 구현 (ComAPI 기반)

---

## Success Criteria

- [ ] ViewModel 각 파일이 500줄 이하
- [ ] 중복 코드 90% 이상 제거
- [ ] ComAPI Property Write 가능 여부 결론
- [ ] CSV Viewer 기본 기능 동작

---

## Git Workflow

각 Phase 완료 시:
1. Conventional Commit 작성
2. CHANGELOG.md 업데이트
3. Sprint 문서 상태 업데이트

---

**Created**: 2026-01-09
**Last Updated**: 2026-01-09

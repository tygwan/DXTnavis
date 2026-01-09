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

**Current State:** 단일 파일에 모든 기능 집중
**Target State:** 기능별 Partial Class 분리

**Tasks:**
- [ ] Core 속성/필드 분리 (`DXwindowViewModel.Core.cs`)
- [ ] Filter 관련 분리 (`DXwindowViewModel.Filter.cs`)
- [ ] Export 관련 분리 (`DXwindowViewModel.Export.cs`)
- [ ] 3D Selection 관련 분리 (`DXwindowViewModel.Selection.cs`)
- [ ] Snapshot 관련 분리 (`DXwindowViewModel.Snapshot.cs`)
- [ ] Tree 관련 분리 (`DXwindowViewModel.Tree.cs`)

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

**Research Questions:**
- [ ] Navisworks Property Read-Only 제약 확인
- [ ] ComAPI로 Property Write 가능 여부
- [ ] User Data Extension 활용 가능성
- [ ] Custom Property Tab 생성 가능성

**Potential Solutions:**
1. ComAPI `InwOpState.ObjectProps` 활용
2. User Data (`InwUserData`) 활용
3. Custom Properties via API
4. External Database 연동 후 조회

**Tasks:**
- [ ] ComAPI 문서 조사
- [ ] POC 코드 작성
- [ ] Read-Only 우회 방법 테스트
- [ ] 결과 문서화 (ADR)

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

### In Progress 🔄
- [ ] 2.1 ViewModel 리팩토링

### Pending 📋
- [ ] 2.2 중복 코드 제거
- [ ] 3.1 ComAPI Property Write Research
- [ ] 4.1 CSV Viewer UI

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

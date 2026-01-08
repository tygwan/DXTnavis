# Sprint: DXTnavis v0.4.0 Feature Development

| Field | Value |
|-------|-------|
| **Sprint Name** | DXTnavis Feature Expansion v0.4.0 |
| **Start Date** | 2026-01-08 |
| **Status** | Planning |
| **Goal** | ViewPoint, Search, CSV Enhancement, Property Write |

---

## Requirements Summary

```
Total Features: 11
Bug Fixes: 3
New Features: 6
Enhancements: 2
```

---

## Phase 1: Bug Fixes (Critical)

### 1.1 검색 창 영어 입력 불가능 오류
| Field | Value |
|-------|-------|
| Priority | 🔴 Critical |
| Type | Bug Fix |
| File | `DXwindow.xaml` / `DXwindowViewModel.cs` |
| Description | 검색창에서 영어 입력이 안되는 문제 |

**Root Cause Analysis:**
- [ ] IME 관련 이슈 확인
- [ ] TextBox InputMethod 설정 확인
- [ ] KeyDown/PreviewKeyDown 이벤트 확인

### 1.2 Save ViewPoint 저장 오류 (Read-Only)
| Field | Value |
|-------|-------|
| Priority | 🔴 Critical |
| Type | Bug Fix |
| File | `SnapshotService.cs` |
| Description | ViewPoint 저장 시 read-only 오류 발생 |

**Investigation:**
- [ ] Document.SavedViewpoints 접근 권한 확인
- [ ] ComAPI를 통한 ViewPoint 저장 방법 검토
- [ ] Transaction/DocumentLock 필요 여부 확인

---

## Phase 2: Tree Enhancement

### 2.1 트리 레벨별 Expand/Collapse
| Field | Value |
|-------|-------|
| Priority | 🟠 High |
| Type | Enhancement |
| File | `DXwindowViewModel.cs`, `DXwindow.xaml` |
| Description | 각 레벨(L0~L10)에 개별 expand/collapse 버튼 추가 |

**Current State:** L0에만 expand/collapse 존재
**Target State:** Navisworks 트리와 동일한 구조

**Tasks:**
- [ ] 레벨별 Expand/Collapse 버튼 UI 추가
- [ ] `ExpandToLevel(int level)` 메서드 구현
- [ ] `CollapseFromLevel(int level)` 메서드 구현
- [ ] 레벨 선택 드롭다운 또는 버튼 그룹

### 2.2 Level 필터링 명세화
| Field | Value |
|-------|-------|
| Priority | 🟠 High |
| Type | Enhancement |
| File | `DXwindowViewModel.cs` |
| Description | 왼쪽 계층 트리 패널의 레벨 필터링 기능 명확화 |

**Tasks:**
- [ ] 필터 UI 개선 (Level 선택 명확화)
- [ ] 필터 적용 시 시각적 피드백
- [ ] 필터 상태 표시

---

## Phase 3: ViewPoint & Navigation

### 3.1 관측점 초기화 기능
| Field | Value |
|-------|-------|
| Priority | 🟡 Medium |
| Type | New Feature |
| File | `NavisworksSelectionService.cs` |
| Description | 현재 뷰를 초기 상태로 리셋 |

**Tasks:**
- [ ] Home ViewPoint 저장 기능
- [ ] Reset to Home ViewPoint 기능
- [ ] UI 버튼 추가 (🏠 아이콘)

### 3.2 Object 검색 기능
| Field | Value |
|-------|-------|
| Priority | 🟡 Medium |
| Type | New Feature |
| File | `DXwindowViewModel.cs`, `DXwindow.xaml` |
| Description | 이름/속성으로 Object 검색 |

**Tasks:**
- [ ] 검색 UI 구현 (SearchBox)
- [ ] 이름 기반 검색
- [ ] 속성 기반 검색
- [ ] 검색 결과 하이라이트
- [ ] 검색 결과 목록 표시

---

## Phase 4: CSV Export Enhancement

### 4.1 Selection 기반 Properties 출력
| Field | Value |
|-------|-------|
| Priority | 🟠 High |
| Type | New Feature |
| File | `PropertyFileWriter.cs`, `DXwindowViewModel.cs` |
| Description | 현재 All Properties만 출력 → Selection Properties 추가 |

**Current:**
- All Properties → 프로젝트 전체
- All Hierarchy → Selection 대상 ✅
- Selection Properties → ❌ 없음

**Target:**
```
┌─────────────────────────────────────────┐
│  All Prop  │  Sele Prop  │             │
├─────────────────────────────────────────┤
│  All Hier  │  Sele Hier  │             │
└─────────────────────────────────────────┘
```

**Tasks:**
- [ ] `ExportSelectionProperties()` 메서드 구현
- [ ] UI 버튼 분리 (All | Selection)
- [ ] 4개 출력 옵션 완성

### 4.2 DisplayString 접두사 처리 (Refined CSV)
| Field | Value |
|-------|-------|
| Priority | 🟠 High |
| Type | New Feature |
| File | `PropertyFileWriter.cs` |
| Description | DisplayString 값을 파싱하여 분리 |

**Example:**
```
Before: DisplayString:171.18 ft^2
After:  DisplayString | 171.18 | ft^2  (3개 셀)
```

**Tasks:**
- [ ] DisplayString 파싱 로직 구현
- [ ] 값/단위 분리 정규식
- [ ] Refined CSV 출력 포맷 정의

### 4.3 Raw/Refined CSV 동시 관리
| Field | Value |
|-------|-------|
| Priority | 🟡 Medium |
| Type | New Feature |
| File | `PropertyFileWriter.cs`, UI |
| Description | Raw CSV와 Refined CSV를 동시에 관리 |

**Options:**
1. **Best:** 애드인 내에서 Raw/Refined 동시 뷰어
   - Dropdown으로 CSV 선택
   - 탭으로 Raw/Refined 전환

2. **Better:** 두 파일 동시 출력
   - `*_raw.csv` / `*_refined.csv`
   - 외부 도구로 분석

**Tasks:**
- [ ] Dual export 기능 (`ExportBothFormats()`)
- [ ] CSV Viewer UI (DataGrid)
- [ ] Raw/Refined 탭 또는 드롭다운
- [ ] CSV 파일 선택 및 로드

### 4.4 CSV 출력 Verbose 로깅
| Field | Value |
|-------|-------|
| Priority | 🟢 Low |
| Type | Enhancement |
| File | `PropertyFileWriter.cs`, `HierarchyFileWriter.cs` |
| Description | CSV 출력 과정 상세 로그 |

**Tasks:**
- [ ] 로그 레벨 설정 (Verbose 옵션)
- [ ] 출력 행 수, 컬럼 수 로깅
- [ ] 오류 상세 정보 출력
- [ ] UI 로그 뷰어 (Optional)

---

## Phase 5: ComAPI Investigation (Research)

### 5.1 외부에서 Property 추가 기입
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

## Development Approach

### Sub-Agents 활용

| Agent | 용도 | Phase |
|-------|------|-------|
| `code-reviewer` | 버그 수정 코드 리뷰 | 1 |
| `progress-tracker` | 진행상황 추적 | All |
| `tech-spec-writer` | ComAPI 조사 문서화 | 5 |

### Skills 활용

| Skill | 용도 | Phase |
|-------|------|-------|
| `/sprint status` | 진행 현황 확인 | All |
| `/quality-gate pre-commit` | 코드 품질 검증 | All |
| `/feedback learning` | ComAPI 조사 학습 기록 | 5 |
| `/feedback adr` | 아키텍처 결정 기록 | 4, 5 |

### Hooks 활용

| Hook | 용도 |
|------|------|
| `auto-doc-sync` | CHANGELOG 자동 업데이트 |

---

## Priority Matrix

```
┌─────────────────────────────────────────────────────────────┐
│ CRITICAL (P0)          │ HIGH (P1)                          │
│ ─────────────────────  │ ──────────────────────────────     │
│ • 검색 영어 입력 버그   │ • 트리 레벨별 Expand/Collapse     │
│ • ViewPoint 저장 오류   │ • Selection Properties 출력       │
│                        │ • DisplayString 파싱              │
├─────────────────────────────────────────────────────────────┤
│ MEDIUM (P2)            │ LOW (P3)                           │
│ ─────────────────────  │ ──────────────────────────────     │
│ • 관측점 초기화         │ • Verbose 로깅                    │
│ • Object 검색          │                                    │
│ • Raw/Refined CSV      │                                    │
│ • ComAPI 조사          │                                    │
└─────────────────────────────────────────────────────────────┘
```

---

## Estimated Effort

| Phase | Tasks | Complexity | Estimate |
|-------|-------|------------|----------|
| 1 | Bug Fixes | Medium | - |
| 2 | Tree Enhancement | Medium | - |
| 3 | ViewPoint & Navigation | Medium | - |
| 4 | CSV Enhancement | High | - |
| 5 | ComAPI Research | High | - |

---

## Success Criteria

- [ ] 검색창 영어 입력 정상 작동
- [ ] ViewPoint 저장 성공
- [ ] 모든 레벨에 Expand/Collapse 작동
- [ ] All/Selection × Prop/Hier 4종 출력
- [ ] DisplayString 분리된 Refined CSV 출력
- [ ] Raw/Refined CSV 동시 관리 가능
- [ ] ComAPI Property Write 가능 여부 결론

---

## Notes

### Technical Constraints
- Navisworks API: UI Thread Only
- Property Write: ComAPI 제약 확인 필요
- Read-Only: Document 상태에 따른 제약

### Dependencies
- Phase 4.2 → Phase 4.3 (Refined CSV 정의 후 동시 출력)
- Phase 5 결과 → 향후 개발 방향 결정

---

**Created**: 2026-01-08
**Last Updated**: 2026-01-08

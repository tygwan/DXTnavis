# Development Status

> **Last Updated:** 2026-01-08
> **Current Version:** v0.3.0
> **Next Version:** v0.4.0

---

## Overall Progress

```
v0.3.0 Progress: [====================>] 100%
v0.4.0 Progress: [>                    ] Planning
```

---

## Phase Status

| Phase | Task | Status | Progress |
|-------|------|--------|----------|
| 1 | CSV Property Export | ✅ Complete | 100% |
| 2 | Filtering & UI Layout | ⚠️ Partial | 70% |
| 3 | 3D Object Integration | ✅ Complete | 100% |
| 4 | 3D Snapshot Workflow | ⚠️ Partial | 50% |
| 5 | Data Validation | 📋 Planned | 0% |

---

## Completed Features (v0.1.0 ~ v0.3.0)

### v0.1.0 (2026-01-03)
- ✅ Level Filter (L0~L10)
- ✅ SysPath Filter
- ✅ TreeView Hierarchy 시각화
- ✅ Visual Level Badges
- ✅ Node Icons (📁/🔷/📄)

### v0.2.0 (2026-01-05)
- ✅ Select in 3D
- ✅ Show Only / Show All
- ✅ Zoom to Selection
- ✅ NavisworksSelectionService 구현

### v0.3.0 (2026-01-06)
- ✅ Level-based Expand/Collapse
- ✅ Expand All / Collapse All
- ✅ Tree 성능 개선
- ✅ 문서 리팩토링

---

## Known Issues (v0.4.0 Target)

| Issue | Priority | Status | Phase |
|-------|----------|--------|-------|
| 검색창 영어 입력 불가 | 🔴 Critical | Open | P2 |
| ViewPoint 저장 read-only | 🔴 Critical | Open | P4 |
| 트리 레벨별 Expand 미완성 | 🟠 High | Open | P2 |
| Selection Properties 미구현 | 🟠 High | Open | P1 |
| DisplayString 파싱 미구현 | 🟠 High | Open | P1 |

---

## v0.4.0 Planned Features

### Bug Fixes (P0)
- [ ] 검색창 영어 입력 오류 수정
- [ ] Save ViewPoint read-only 오류 수정

### New Features (P1-P2)
- [ ] 관측점 초기화 기능
- [ ] Object 검색 기능
- [ ] 트리 레벨별 개별 Expand/Collapse
- [ ] Selection Properties 출력
- [ ] DisplayString 파싱 (Refined CSV)
- [ ] Raw/Refined CSV 동시 관리
- [ ] CSV 출력 Verbose 로깅

### Research
- [ ] ComAPI Property Write 가능성 조사

---

## Development Timeline

```
2025-12-29: v0.0.1 - Project Setup
     │
2026-01-03: v0.1.0 - Property Filtering
     │
2026-01-05: v0.2.0 - 3D Integration
     │
2026-01-06: v0.3.0 - Tree Enhancement
     │
2026-01-08: v0.4.0 Planning Start ← Current
     │
     ↓
2026-XX-XX: v0.4.0 - Feature Expansion
```

---

## Recent Changes

### 2026-01-08
- 📋 v0.4.0 개발 계획 수립
- 📝 SPRINT-v0.4.0.md 생성
- 📝 문서 업데이트 (CHANGELOG, CLAUDE.md, _INDEX.md)

### 2026-01-07
- 📝 Agile 문서화 시스템 적용
- 📝 CHANGELOG.md 생성
- 📝 SPRINT-CURRENT.md 생성

### 2026-01-06
- ✅ Phase 4 부분 완료: Snapshot COM API 수정
- 📝 문서 리팩토링: Phase별 개별 문서 분리

### 2026-01-05
- ✅ Phase 3 완료: 3D Selection/Visibility
- ✅ Phase 2 Tree Expand/Collapse 완료

---

## Next Steps

1. **Immediate (P0)**
   - 검색창 영어 입력 버그 수정
   - ViewPoint 저장 오류 수정

2. **Short-term (P1)**
   - 트리 레벨별 Expand/Collapse 완성
   - Selection Properties 출력 구현
   - DisplayString 파싱 구현

3. **Mid-term (P2)**
   - 관측점 초기화 기능
   - Object 검색 기능
   - Raw/Refined CSV 동시 관리

4. **Research**
   - ComAPI Property Write 조사

---

[← Back to Index](../_INDEX.md) | [Sprint v0.4.0](../agile/SPRINT-v0.4.0.md)

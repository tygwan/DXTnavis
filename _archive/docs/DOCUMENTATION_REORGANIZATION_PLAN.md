# Documentation Reorganization Plan

> **목적**: 프로젝트 문서를 체계적으로 정리하여 개발자 경험 향상
> **작성일**: 2025-12-22
> **버전**: 1.0

---

## 📋 현재 상태 분석

### 문서 현황

**루트 폴더 (13개 MD 파일)**:
```
✅ README.md                          # 유지 (프로젝트 개요)
✅ CONTRIBUTING.md                    # 유지 (기여 가이드)
✅ CHANGELOG.md                       # 유지 (버전 히스토리)
📦 CLAUDE.md                          # 이동 → docs/CLAUDE.md
📦 DATABASE_SCHEMA_V2_MIGRATION_SUMMARY.md  # 통합 → CHANGELOG
📦 DATA_INTEGRATION_STRATEGY.md      # 이동 → docs/architecture/
📦 DXNAVIS_PLUGIN_UPDATE_SUMMARY.md  # 통합 → CHANGELOG
📦 DXREVIT_PLUGIN_UPDATE_SUMMARY.md  # 통합 → CHANGELOG
📦 memo.md                           # 삭제 (임시 메모)
📦 Navisworkstimelinerhint.md        # 이동 → docs/guides/
📦 RELEASE_NOTES_v1.1.0.md           # 이동 → docs/archive/releases/
📦 SYSTEM_IMPROVEMENT_ROADMAP.md     # 통합 → PROJECT_STATUS.md
📦 SYSTEM_MAINTENANCE_GUIDE_V1.md    # 이동 → docs/operations/
📦 UI_UPDATE_SUMMARY.md              # 통합 → CHANGELOG
```

**docs 폴더 (21개 MD 파일)**:
```
docs/
├─ ✅ claude.md                      # 유지 (TDD 가이드)
├─ ✅ plan.md                        # 유지 (상세 계획)
├─ ✅ techspec.md                    # 유지 (기술 사양)
├─ ✅ TODO.md                        # 유지 (작업 목록)
├─ ✅ STATUS.md                      # 삭제 → PROJECT_STATUS.md로 대체
├─ 📦 BACKEND_FRONTEND_SETUP_PLAN.md   # 이동 → dev/setup-guide.md (통합)
├─ 📦 BACKEND_REVIT_NAV_UNIFICATION_PLAN.md  # 아카이브
├─ 📦 INSTALL_POSTGRESQL_MCP.md      # 이동 → operations/
├─ 📦 ISSUE_ANALYSIS_FOR_BACKEND.md  # 아카이브
├─ 📦 MCP_BYTEBASE_SETUP.md          # 이동 → operations/
├─ 📦 MCP_SETUP_COMPLETE.md          # 아카이브
├─ 📦 PERFORMANCE_METRICS_G.md       # 이동 → archive/releases/
├─ 📦 PROJECT_WORKFLOW_GUIDE.md      # 이동 → guides/
├─ 📦 RELEASE_GATES_COMPLETE_v1.1.0.md  # 이동 → archive/releases/
├─ 📦 RELEASE_GATES_v1.1.0.md        # 이동 → archive/releases/
├─ 📦 RETROSPECTIVE_v1.1.0.md        # 이동 → archive/releases/
├─ 📦 SQL_WORKFLOW_GUIDE.md          # 이동 → guides/
├─ 📦 TECHNICAL_CAPABILITIES.md      # 통합 → PROJECT_STATUS.md
├─ dev/
│  ├─ ✅ architecture.md             # 유지
│  ├─ ✅ data-pipeline.md            # 유지
│  ├─ ✅ runbook.md                  # 유지
│  └─ ✅ setup-guide.md              # 유지
└─ user/
   └─ ✅ installation.md             # 유지
```

### 문제점
1. **산발적 문서 구조**: 루트와 docs에 중복/유사 문서
2. **이름 불일치**: SUMMARY, UPDATE, NOTES 등 혼재
3. **시간 순서 문서**: 릴리스 노트가 분산됨
4. **중복 정보**: 여러 문서에 같은 내용 반복
5. **진입점 불명확**: 어떤 문서를 먼저 봐야 할지 불분명

---

## 🎯 정리 전략

### 원칙

1. **Single Source of Truth**: 같은 정보는 한 곳에만
2. **계층적 구조**: 주제별로 명확히 분류
3. **진입점 최적화**: README → 세부 문서로 자연스러운 흐름
4. **시간 순서 분리**: 릴리스 노트는 archive에 보관
5. **최신성 유지**: 구 버전 문서는 아카이브

### 목표 구조

```
AWP_2025/개발폴더/
├─ README.md                      # 프로젝트 개요, Quick Start
├─ CONTRIBUTING.md                # 기여 가이드
├─ CHANGELOG.md                   # 버전 히스토리 (모든 변경사항 통합)
├─ PROJECT_STATUS.md              # ⭐ 현재 프로젝트 상태 (기능 단위)
├─ CLAUDE.md                      # Claude 프롬프트
│
├─ docs/
│  ├─ README.md                   # 문서 디렉토리 가이드
│  │
│  ├─ getting-started/            # 시작하기
│  │  ├─ README.md
│  │  ├─ quick-start.md           # 5분 안에 시작하기
│  │  └─ installation.md          # 상세 설치 가이드
│  │
│  ├─ architecture/               # 아키텍처
│  │  ├─ README.md
│  │  ├─ overview.md              # 시스템 개요
│  │  ├─ data-pipeline.md         # 데이터 파이프라인
│  │  ├─ dual-identity.md         # 이중 식별자 패턴
│  │  └─ integration-strategy.md  # 통합 전략
│  │
│  ├─ development/                # 개발 가이드
│  │  ├─ README.md
│  │  ├─ claude.md                # TDD 가이드
│  │  ├─ plan.md                  # 개발 계획
│  │  ├─ TODO.md                  # 작업 목록
│  │  ├─ techspec.md              # 기술 사양
│  │  └─ workflow.md              # 개발 워크플로우
│  │
│  ├─ guides/                     # 사용자 가이드
│  │  ├─ README.md
│  │  ├─ revit-plugin.md          # DXrevit 사용법
│  │  ├─ navisworks-plugin.md     # DXnavis 사용법
│  │  ├─ timeliner.md             # Timeliner 연동
│  │  └─ sql-workflow.md          # SQL 워크플로우
│  │
│  ├─ api/                        # API 문서
│  │  ├─ README.md
│  │  ├─ endpoints.md             # 엔드포인트 목록
│  │  ├─ schemas.md               # 데이터 스키마
│  │  └─ examples.md              # 사용 예제
│  │
│  ├─ operations/                 # 운영 가이드
│  │  ├─ README.md
│  │  ├─ deployment.md            # 배포 가이드
│  │  ├─ runbook.md               # 운영 매뉴얼
│  │  ├─ monitoring.md            # 모니터링
│  │  ├─ database-setup.md        # DB 설정
│  │  └─ mcp-setup.md             # MCP 설정
│  │
│  └─ archive/                    # 과거 문서
│     ├─ README.md
│     ├─ releases/                # 릴리스별 문서
│     │  ├─ v1.1.0/
│     │  │  ├─ release-notes.md
│     │  │  ├─ performance-metrics.md
│     │  │  ├─ release-gates.md
│     │  │  └─ retrospective.md
│     │  └─ v1.0.0/
│     └─ migrations/              # 마이그레이션 가이드
│        ├─ schema-v2-migration.md
│        └─ backend-unification.md
│
├─ database/                      # DB 스키마 및 마이그레이션
│  └─ README.md                   # DB 문서
│
├─ scripts/                       # 운영 스크립트
│  └─ README.md                   # 스크립트 가이드
│
└─ [component folders...]
```

---

## 📝 실행 계획

### Phase 1: 문서 백업 및 준비

```bash
# 1. 현재 상태 백업
git add .
git commit -m "docs: Backup before reorganization"
git tag backup-before-reorganization

# 2. 새 브랜치 생성
git checkout -b docs/reorganization

# 3. 새 폴더 구조 생성
mkdir -p docs/getting-started
mkdir -p docs/architecture
mkdir -p docs/development
mkdir -p docs/guides
mkdir -p docs/api
mkdir -p docs/operations
mkdir -p docs/archive/releases/v1.1.0
mkdir -p docs/archive/migrations
```

### Phase 2: 문서 이동 및 통합

#### 2.1 루트 폴더 정리

```bash
# 유지
# - README.md
# - CONTRIBUTING.md
# - CHANGELOG.md
# - PROJECT_STATUS.md (새로 생성됨)

# 이동
mv CLAUDE.md docs/CLAUDE.md
mv DATA_INTEGRATION_STRATEGY.md docs/architecture/integration-strategy.md
mv Navisworkstimelinerhint.md docs/guides/timeliner.md
mv SYSTEM_MAINTENANCE_GUIDE_V1.md docs/operations/maintenance.md

# 아카이브
mv RELEASE_NOTES_v1.1.0.md docs/archive/releases/v1.1.0/release-notes.md
mv DATABASE_SCHEMA_V2_MIGRATION_SUMMARY.md docs/archive/migrations/schema-v2-migration.md

# 삭제
rm memo.md

# 통합 (내용을 CHANGELOG.md에 추가 후 삭제)
# - DXNAVIS_PLUGIN_UPDATE_SUMMARY.md
# - DXREVIT_PLUGIN_UPDATE_SUMMARY.md
# - UI_UPDATE_SUMMARY.md
# - SYSTEM_IMPROVEMENT_ROADMAP.md (→ PROJECT_STATUS.md)
```

#### 2.2 docs 폴더 정리

```bash
# development/ 구성
mv docs/claude.md docs/development/
mv docs/plan.md docs/development/
mv docs/techspec.md docs/development/
mv docs/TODO.md docs/development/
mv docs/PROJECT_WORKFLOW_GUIDE.md docs/development/workflow.md

# guides/ 구성
mv docs/SQL_WORKFLOW_GUIDE.md docs/guides/sql-workflow.md

# operations/ 구성
mv docs/INSTALL_POSTGRESQL_MCP.md docs/operations/database-setup.md
mv docs/MCP_BYTEBASE_SETUP.md docs/operations/mcp-setup.md

# archive/releases/v1.1.0/ 구성
mv docs/PERFORMANCE_METRICS_G.md docs/archive/releases/v1.1.0/performance-metrics.md
mv docs/RELEASE_GATES_v1.1.0.md docs/archive/releases/v1.1.0/release-gates.md
mv docs/RELEASE_GATES_COMPLETE_v1.1.0.md docs/archive/releases/v1.1.0/release-gates-complete.md
mv docs/RETROSPECTIVE_v1.1.0.md docs/archive/releases/v1.1.0/retrospective.md

# archive/migrations/ 구성
mv docs/BACKEND_REVIT_NAV_UNIFICATION_PLAN.md docs/archive/migrations/backend-unification.md

# 삭제
rm docs/STATUS.md  # PROJECT_STATUS.md로 대체
rm docs/MCP_SETUP_COMPLETE.md  # 임시 문서
rm docs/ISSUE_ANALYSIS_FOR_BACKEND.md  # 임시 분석 문서
rm docs/BACKEND_FRONTEND_SETUP_PLAN.md  # setup-guide.md에 통합됨

# getting-started/ 구성 (기존 파일 재구성)
cp docs/user/installation.md docs/getting-started/installation.md
```

#### 2.3 architecture/ 구성

```bash
# architecture/ 새 문서 생성
# - overview.md (architecture.md 기반)
# - data-pipeline.md (기존 유지)
# - dual-identity.md (새로 작성)
# - integration-strategy.md (이동 완료)

mv docs/dev/architecture.md docs/architecture/overview.md
mv docs/dev/data-pipeline.md docs/architecture/data-pipeline.md
```

#### 2.4 operations/ 구성

```bash
mv docs/dev/runbook.md docs/operations/runbook.md
```

#### 2.5 getting-started/ 구성

```bash
# quick-start.md 새로 작성 (5분 안에 시작하기)
```

### Phase 3: README 파일 생성

각 폴더에 README.md 생성:

1. `docs/README.md` - 문서 디렉토리 가이드
2. `docs/getting-started/README.md`
3. `docs/architecture/README.md`
4. `docs/development/README.md`
5. `docs/guides/README.md`
6. `docs/api/README.md`
7. `docs/operations/README.md`
8. `docs/archive/README.md`

### Phase 4: 링크 업데이트

모든 문서의 내부 링크 업데이트:
- 상대 경로 수정
- 존재하지 않는 파일 참조 제거
- PROJECT_STATUS.md의 Quick Links 섹션 업데이트

### Phase 5: CHANGELOG 통합

다음 문서의 내용을 CHANGELOG.md에 통합:
1. DXNAVIS_PLUGIN_UPDATE_SUMMARY.md
2. DXREVIT_PLUGIN_UPDATE_SUMMARY.md
3. UI_UPDATE_SUMMARY.md

### Phase 6: 검증 및 커밋

```bash
# 1. 모든 링크 확인
# 2. 문서 빌드 테스트 (있을 경우)
# 3. 리뷰

# 4. 커밋
git add .
git commit -m "docs: Reorganize documentation structure

- Create hierarchical structure (getting-started, architecture, development, guides, api, operations, archive)
- Move release-specific documents to archive/releases/v1.1.0/
- Consolidate UPDATE_SUMMARY files into CHANGELOG.md
- Add README.md to each documentation folder
- Update all internal links
- Create PROJECT_STATUS.md as single source of truth for current status"

# 5. PR 생성 및 리뷰
```

---

## 📊 Before & After

### Before (33개 MD 파일, 산발적 구조)

```
루트: 13개 (중복/임시 문서 혼재)
docs/: 21개 (계층 없음, 시간 순서 혼재)
→ 개발자가 어디서 시작해야 할지 불명확
```

### After (체계적 계층 구조)

```
루트: 4개 (README, CONTRIBUTING, CHANGELOG, PROJECT_STATUS)
docs/
  ├─ getting-started/ (3개)     # 첫 방문자
  ├─ architecture/ (4개)        # 아키텍트
  ├─ development/ (5개)         # 개발자
  ├─ guides/ (4개)              # 사용자
  ├─ api/ (3개)                 # API 클라이언트
  ├─ operations/ (5개)          # DevOps
  └─ archive/ (릴리스별 정리)   # 히스토리

→ 역할별/목적별 명확한 진입점
```

---

## ✅ 검증 체크리스트

### 구조 검증
- [ ] 모든 폴더에 README.md 존재
- [ ] 계층 구조가 3단계 이하
- [ ] 각 폴더의 목적이 명확
- [ ] 중복 문서 제거 완료

### 내용 검증
- [ ] 모든 내부 링크 작동
- [ ] 이미지/다이어그램 경로 정상
- [ ] 코드 블록 문법 정상
- [ ] 목차(TOC) 정확

### 완전성 검증
- [ ] 삭제된 문서 내용이 다른 곳에 보존됨
- [ ] 릴리스 노트가 archive에 모두 보관됨
- [ ] CHANGELOG가 모든 변경사항 포함
- [ ] PROJECT_STATUS가 최신 상태 반영

### 사용성 검증
- [ ] 신규 개발자가 5분 안에 시작 가능
- [ ] 각 역할별 진입점 명확
- [ ] 검색으로 원하는 문서 쉽게 찾을 수 있음

---

## 🔄 롤백 계획

문제 발생 시:

```bash
# 1. 태그로 롤백
git checkout backup-before-reorganization

# 2. 새 브랜치 삭제
git branch -D docs/reorganization

# 3. 원상 복구
git checkout main
```

---

## 📅 타임라인

- **Phase 1**: 백업 및 준비 (30분)
- **Phase 2**: 문서 이동 및 통합 (2시간)
- **Phase 3**: README 생성 (1시간)
- **Phase 4**: 링크 업데이트 (1시간)
- **Phase 5**: CHANGELOG 통합 (30분)
- **Phase 6**: 검증 및 커밋 (1시간)

**총 예상 시간**: 6시간

---

## 🎯 기대 효과

1. **신규 개발자 온보딩 시간 50% 단축**
   - 명확한 getting-started 가이드
   - 역할별 진입점

2. **문서 검색 시간 70% 단축**
   - 계층적 구조
   - 폴더명으로 목적 파악

3. **유지보수성 향상**
   - 중복 제거로 업데이트 부담 감소
   - 단일 진실 공급원 (SSOT)

4. **히스토리 보존**
   - 릴리스별 archive로 과거 추적 가능
   - 마이그레이션 가이드 보관

---

**작성자**: Claude Code
**승인 대기**: Development Team

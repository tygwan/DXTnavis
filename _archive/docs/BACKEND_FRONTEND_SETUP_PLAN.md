# Backend · Frontend Configuration Kickoff

본 문서는 FastAPI 백엔드와 신규 웹 프론트엔드를 동시에 구축하기 위한 초기 구성 로드맵입니다. 목표는 Revit/Navisworks 인입 데이터를 백엔드에서 안정적으로 처리하고, 프론트엔드에서 프로젝트/리비전 상태 및 Timeliner 준비 현황을 시각화·제어할 수 있는 기반을 마련하는 것입니다.

**아래 절차를 따르기 전, 이미 존재하거나 조치된 경우를 고려하여 진행한다**

---

## 1. Backend 준비 (FastAPI)

### 1.1 환경 구성
- Python 3.11+ 권장
- 가상환경 생성 및 활성화  
  ```bash
  cd fastapi_server
  python -m venv .venv
  .\.venv\Scripts\activate  # Windows
  pip install -r requirements.txt
  ```
- `.env` 생성 (`.env.example` 참조)  
  ```ini
  DATABASE_URL=postgresql://dx_api_role:***@localhost:5432/dx_platform
  HOST=0.0.0.0
  PORT=5000
  DEBUG=True
  ALLOWED_ORIGINS=http://localhost:5173,http://127.0.0.1:5173
  ```

### 1.2 데이터베이스
1. PostgreSQL 15+ 설치 → `dx_platform` DB 생성  
   ```bash
   createdb dx_platform
   ```
2. 초기 스키마 실행  
   ```bash
   psql -U postgres -d dx_platform -f database/init_database.sql
   ```
3. 통합 마이그레이션 적용  
   ```bash
   psql -U postgres -d dx_platform -f database/migrations/004_unified_revision_patch.sql
   ```
4. 적용 확인  
   ```sql
   \dt revision_versions
   \dt navisworks_hierarchy
   ```

### 1.3 서버 실행 & 테스트
```bash
uvicorn fastapi_server.main:app --reload
```
- `GET /api` : 상태 확인
- `GET /docs` : 스웨거 문서
- Revit 인입 후 `revision_versions`, `unified_objects` 테이블 카운트 확인

---

## 2. Frontend 준비 (Web Dashboard)

### 2.1 기술 스택 제안
| 항목 | 선택안 | 비고 |
|------|--------|------|
| 프레임워크 | React + Vite (TypeScript) | 빠른 개발, 풍부한 생태계 |
| UI 라이브러리 | Chakra UI 또는 Ant Design | 다국어 지원, 컴포넌트 풍부 |
| 상태관리 | TanStack Query | API 캐싱, 요청 상태 관리 용이 |
| 빌드/배포 | Vite + pnpm/npm | 경량, HMR 속도 빠름 |

### 2.2 초기 작업
1. 새 프로젝트 디렉터리 생성  
   ```bash
   cd 개발폴더
   npm create vite@latest web-frontend -- --template react-ts
   cd web-frontend
   npm install
   npm install @chakra-ui/react @emotion/react @emotion/styled framer-motion @tanstack/react-query axios
   ```
2. 환경 변수 정의 (`.env`)  
   ```env
   VITE_API_BASE_URL=http://localhost:5000/api/v1
   ```
3. API 클라이언트 구성 (`src/lib/api.ts`)  
   - Axios instance 생성 (Base URL, interceptors)
   - 주요 엔드포인트 래퍼: `projects`, `revisions`, `analytics`, `navisworks`
4. 라우팅/페이지 구성
   - `/projects` : 프로젝트 리스트 & 감지 결과 표시
   - `/projects/:code/revisions` : 리비전 상세 (Revit/Navisworks 객체 수, 최근 업로드)
   - `/ingest/navisworks` : CSV 업로드 UI (감지 → 결과 피드백)
   - `/analytics` : Timeliner 준비 현황, RAG 통계 (추후)
5. 인증·권한은 초기 버전에서는 생략, 추후 OAuth2/OpenID 기반 확장 고려

### 2.3 UI/UX 포인트
- 프로젝트 감지 UI: CSV 업로드 전에 감지 API 결과를 보여 주고 신뢰도를 색상으로 표시
- Revit/Navisworks 객체 카운트 비교 카드
- Timeliner용 Selection Set/Task 준비 현황 표시 (API 준비 후 연동)
- 로그/이벤트 스트림: FastAPI의 `/api/v1/system/logs` (미구현) 예정 시 확장

---

## 3. 연동 시나리오
1. **Revit 인입** → FastAPI `/api/v1/ingest` → `revision_versions`, `unified_objects` 갱신
2. **Navisworks CSV 업로드 (웹/애드인)**  
   - 감지 API `/api/v1/projects/detect-by-objects` 호출 → 선택/확정  
   - `/api/v1/navisworks/projects/{code}/revisions/{rev}/hierarchy` 업로드 → Navisworks 객체 저장
3. **프론트엔드 대시보드**  
   - `/api/v1/projects`, `/api/v1/projects/{code}/revisions` 로 기본 리스트  
   - `/api/v1/analytics`(추후) 로 Timeliner/변경 통계 시각화
4. **Timeliner 확장** (Phase 2)  
   - Selection Set 자동 생성 API (`/api/v1/navisworks/selection-sets` 예정)
   - 일정 CSV ↔ Activity 매핑 화면 설계

---

## 4. 권장 작업 순서
1. FastAPI `.env` 설정 및 DB 초기화
2. Revit 스냅샷 업로드 → 데이터 정상 적재 확인
3. Navisworks CSV 업로드 → 새 감지/연동 플로우 검증
4. `web-frontend` 생성 & API 통신 모듈 구현
5. 프로젝트/리비전 리스트 UI, Navisworks 업로드 UI 구현
6. 프론트엔드에서 Revit/Navisworks 통계 조회 화면 확장
7. 로그인/권한, 배포(Azure/AWS/On-prem) 등 운영 요소 단계적 도입

---

본 계획에 따라 환경을 구성하고 나면 Revit↔Navisworks 데이터 파이프라인을 시각적으로 점검할 수 있는 핵심 사용자 인터페이스를 빠르게 확보할 수 있습니다. 다음 단계는 프론트엔드 초기 구현(프로젝트 리스트/감지 UI)과 백엔드에서 Timeliner 및 Analytics 엔드포인트 확장입니다.

# DX Platform 기술 역량 개요

## 1. 플랫폼 개요
- BIM-DXPlatform은 Autodesk Revit과 Navisworks를 연결해 모델 데이터 추출, 변경 추적, 4D 시뮬레이션 연계를 자동화하도록 설계된 통합 솔루션이다.[^readme-intro]
- 현재 DXrevit 애드인과 FastAPI 기반 DXserver, PostgreSQL 데이터 레이어가 구현되어 있으며 DXnavis, AI 예측 기능 등은 단계적으로 확장 예정이다.[^readme-roadmap]

## 2. 클라이언트 애드인 역량
### DXrevit (Revit Add-in)
- Snapshot Capture: Revit 모델의 기하, 매개변수, 관계를 한 번에 추출해 서버로 전송한다.[^readme-snapshot]
- Shared Parameters: DX_ActivityId, DX_SyncId 등 커스텀 매개변수를 자동 생성해 추적성을 확보한다.[^readme-parameters]
- Settings & Progress: API 서버·타임아웃·로그 설정 UI와 대용량 모델 진행률 표시를 제공한다.[^readme-settings]
- 추출 데이터는 FastAPI `/api/v1/ingest` 엔드포인트로 전송돼 서버·DB 파이프라인을 통해 바로 적재된다.[^runbook-ingest]

### DXnavis (Navisworks Add-in, 단계적 도입)
- CSV 일정 데이터를 분석해 Timeliner에 자동 매핑하고 Selection Set을 생성하는 기능을 목표로 한다.[^readme-dxnavis]
- Phase 4 계획에 따라 Revit 추출 데이터와 Navisworks 4D 워크플로를 통합할 예정이다.[^readme-roadmap]

## 3. 서버/API 역량
- FastAPI 애플리케이션이 CORS, Trusted Host, 보안 헤더 미들웨어를 기본으로 포함해 배포 환경을 고려한 구성을 갖췄다.[^main-security]
- `ingest_payload`는 DXrevit가 전송한 스냅샷을 메타데이터·객체·관계 테이블에 UPSERT 처리하며 배치 실행, 경고 로그, 처리시간 측정을 수행한다.[^ingest-upsert]
- `analytics` 라우터는 모델 버전 페이지네이션 조회와 버전 간 비교를 제공하며, `fn_compare_versions` SQL 함수와 연계해 변경 이력 분석을 지원한다.[^analytics-compare][^fn-compare]
- `system` 라우터는 `/metrics` 엔드포인트와 HTTP 미들웨어를 통해 Prometheus 지표를 기본 노출한다.[^system-metrics]

## 4. 데이터베이스 및 분석 역량
- 핵심 테이블은 `metadata`, `objects`, `relationships`로 구성되어 모델 버전 메타 정보, 객체 속성 JSONB, 객체 관계를 저장한다.[^db-metadata][^db-objects][^db-relationships]
- 원본 데이터 테이블에는 업데이트·삭제를 차단하는 트리거를 적용해 버전 이력 일관성을 보장한다.[^db-triggers]
- 분석용 뷰 `analytics_version_summary`, `analytics_4d_link_data`가 배포되어 BI 도구에서 바로 버전 요약과 4D 매핑 데이터를 조회할 수 있다.[^view-summary][^view-4d]
- RAG용 `rag_documents`, `rag_document_chunks`, `rag_chunk_embeddings`, `rag_qa_sessions/interactions` 스키마를 정의해 텍스트 기반 분석과 질의응답 로그를 저장할 준비가 되어 있다.[^rag-tables][^rag-logs][^rag-view]

## 5. AI·RAG 기반 역량
- 데이터 파이프라인 문서는 문서 파싱 → JSON 정규화 → PostgreSQL(JSONB) → `pgvector` 임베딩 → RAG 서비스 → BI 대시보드로 이어지는 전체 흐름과 API 계약을 규정한다.[^pipeline-overview]
- `pgvector` 확장과 임베딩 차원 관리, KNN 검색 SQL 패턴 등이 정의되어 있어 외부 LLM·임베딩 모델 연동 시 즉시 활용 가능하다.[^pipeline-storage]
- `/api/rag/query`, `/api/embeddings/reindex` 등 RAG 관련 엔드포인트 스펙과 BI용 `analytics_rag_usage` 뷰가 마련되어 있으며, 실제 LLM 에이전트 구현 전 단계의 데이터·분석 토대를 제공한다.[^pipeline-api][^rag-view]

## 6. 운영 및 보안 역량
- Runbook은 가상환경 구성, `.env` 설정, Uvicorn 실행, 헬스체크/메트릭/API 스모크 테스트 절차를 명시해 운영 일관성을 확보한다.[^runbook-start]
- Prometheus 지표(`http_requests_total`, `http_request_duration_seconds`)와 프로젝트별 인입량 모니터링 권장사항이 제시되어 있다.[^runbook-observe]
- 데이터베이스 문서는 역할 기반 권한 분리(`dx_api_role`, `dx_readonly_role`)와 비밀번호 정책을 제공하며, BI 도구 연결 가이드를 포함한다.[^db-security]

## 7. 향후 확장 포인트
- 로드맵에는 AI 기반 변경 예측, DXnavis 애드인 완성, DXserver 정식 배포 등이 포함되어 있어 현재 역량을 기반으로 한 단계적 고도화 방향이 제시되어 있다.[^readme-roadmap]

---

[^readme-intro]: README.md:22
[^readme-roadmap]: README.md:172
[^readme-snapshot]: README.md:46
[^readme-parameters]: README.md:47
[^readme-settings]: README.md:48
[^runbook-ingest]: docs/dev/runbook.md:38
[^readme-dxnavis]: README.md:51
[^main-security]: fastapi_server/main.py:69
[^ingest-upsert]: fastapi_server/routers/ingest.py:17
[^analytics-compare]: fastapi_server/routers/analytics.py:102
[^fn-compare]: database/functions/fn_compare_versions.sql:5
[^system-metrics]: fastapi_server/routers/system.py:78
[^db-metadata]: database/README.md:100
[^db-objects]: database/README.md:105
[^db-relationships]: database/README.md:111
[^db-triggers]: database/triggers/prevent_raw_data_modification.sql:9
[^view-summary]: database/views/analytics_version_summary.sql:5
[^view-4d]: database/views/analytics_4d_link_data.sql:5
[^rag-tables]: database/tables/rag_document_chunks.sql:5
[^rag-logs]: database/tables/rag_qa_logs.sql:12
[^rag-view]: database/views/analytics_rag_usage.sql:5
[^pipeline-overview]: docs/dev/data-pipeline.md:18
[^pipeline-storage]: docs/dev/data-pipeline.md:28
[^pipeline-api]: docs/dev/data-pipeline.md:72
[^runbook-start]: docs/dev/runbook.md:21
[^runbook-observe]: docs/dev/runbook.md:57
[^db-security]: database/README.md:74

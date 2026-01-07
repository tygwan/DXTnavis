# Phase 0: DX 통합 플랫폼 아키텍처 개요

## 문서 목적
이 문서는 DXrevit과 DXnavis 프로젝트의 전체 아키텍처와 개발 철학을 정의합니다. 모든 개발 단계는 이 문서에 정의된 원칙을 준수해야 합니다.

---

## 1. 시스템 개요

### 1.1. 프로젝트 목표
**최종 목표**: Revit에서 Navisworks까지 이어지는 완전 자동화된 nD BIM 데이터 파이프라인 구축

**핵심 가치**:
- 설계 변경 이력의 완전한 추적 및 보존
- BIM 엔지니어의 수동 작업 최소화
- 데이터 무결성 및 보안 보장
- 확장 가능하고 유지보수 가능한 아키텍처

### 1.2. 시스템 구성 요소

```
┌──────────────────────────────────────────────────────────────┐
│                    DX 통합 플랫폼 아키텍처                     │
└──────────────────────────────────────────────────────────────┘

┌─────────────┐         ┌─────────────┐         ┌─────────────┐
│  DXrevit    │ HTTPS → │  FastAPI    │ ← SQL → │ PostgreSQL  │
│  애드인     │          │  서버       │          │ 데이터베이스 │
│ (데이터생산) │          │ (보안게이트) │          │ (중앙저장소) │
└─────────────┘         └─────────────┘         └─────────────┘
                               ↑
                          HTTPS│
                               ↓
                        ┌─────────────┐          ┌─────────────┐
                        │  DXnavis    │ ← OData →│  Power BI   │
                        │  애드인     │           │ (분석/시각화)│
                        │ (데이터소비)│            └─────────────┘
                        └─────────────┘
                               ↑
                        ┌──────┴──────┐
                        │   DXBase    │
                        │ (공유라이브러리)│
                        └─────────────┘
```

---

## 2. 데이터 파이프라인 5단계

### Phase 1: 데이터 생성 및 준비 (Source of Truth)
**시스템**: Autodesk Revit + DXrevit 애드인

**역할**:
- BIM 모델의 유일한 진실의 원천(Single Source of Truth)
- 공정 ID, 비용, 자재 등 분석용 속성 관리
- 설계 변경 시점의 스냅샷 생성

**데이터 흐름**:
1. BIM 엔지니어가 Revit에서 모델링 작업 수행
2. 주요 마일스톤에서 "스냅샷 저장" 버튼 클릭
3. DXrevit이 모델 데이터를 추출하고 JSON으로 패키징
4. HTTPS로 FastAPI 서버에 전송

### Phase 2: 데이터 수집 및 원시 저장 (Ingestion & Raw Storage)
**시스템**: FastAPI 서버 + PostgreSQL 데이터베이스

**역할**:
- 보안 게이트웨이 및 데이터 검증
- 원시 데이터의 영구 보존 (불변성 보장)
- 모든 버전 이력 시간순 누적

**데이터 흐름**:
1. FastAPI가 DXrevit의 JSON 데이터 수신
2. 데이터 유효성 검증 (스키마, 무결성)
3. PostgreSQL 원시 테이블에 INSERT (UPDATE/DELETE 없음)
4. 성공/실패 응답 반환

### Phase 3: 데이터 가공 및 분석용 뷰 생성 (Transformation)
**시스템**: PostgreSQL Views & Functions

**역할**:
- 원시 데이터를 비즈니스 인텔리전스로 변환
- 복잡한 계산 및 집계 사전 처리
- 분석용 데이터 웨어하우스 계층 구축

**주요 뷰**:
- `analytics_version_summary`: 버전별 총 물량, 비용, 객체 수
- `analytics_version_delta`: 버전 간 변경 사항 (추가/삭제/수정)
- `analytics_4d_link_data`: TimeLiner 자동화용 매핑 데이터

### Phase 4: 데이터 제공 (Serving)
**시스템**: FastAPI Analytics Endpoints

**역할**:
- 클라이언트 애플리케이션을 위한 RESTful API 제공
- SQL 복잡성을 추상화한 간단한 인터페이스
- 인증/인가 및 접근 제어

**주요 엔드포인트**:
- `GET /api/v1/models/versions`: 버전 목록 조회
- `GET /api/v1/models/{version}/summary`: 버전 요약 정보
- `GET /api/v1/models/compare?v1=...&v2=...`: 버전 비교
- `GET /api/v1/timeliner/{version}/mapping`: 4D 매핑 데이터

### Phase 5: 데이터 활용 및 시각화 (Utilization & Visualization)
**시스템**: DXnavis 애드인 + Power BI

**역할**:
- Navisworks에서 4D 시뮬레이션 자동 구성
- Power BI에서 다차원 분석 및 대시보드 제공
- 의사결정 지원

**주요 기능**:
- TimeLiner 작업과 객체 자동 연결
- 버전 간 변경 사항 3D 시각화
- 비용, 진행률, 공정 분석 대시보드

---

## 3. 핵심 아키텍처 원칙

### 3.1. 데이터 무결성 및 불변성
**원칙**: 한번 저장된 데이터는 절대 수정하거나 삭제하지 않는다.

**구현**:
- 원시 데이터 테이블은 INSERT만 허용
- 모든 변경은 새로운 버전으로 기록
- Timestamp와 ModelVersion으로 시간 추적
- 논리적 삭제만 허용 (is_deleted 플래그)

**이유**: 설계 변경 이력의 완전한 추적, 감사 추적, 롤백 가능성

### 3.2. 보안 및 접근 제어
**원칙**: 클라이언트는 데이터베이스에 직접 접근하지 않는다.

**구현**:
- 모든 데이터 통신은 HTTPS를 통한 API로만 수행
- 데이터베이스 자격증명은 API 서버에만 존재
- 클라이언트는 HttpClient만 사용
- Npgsql 등 DB 드라이버는 클라이언트에서 제거

**이유**: SQL 인젝션 방지, 자격증명 노출 방지, 방화벽 관리 용이

### 3.3. 역할 분리 (Separation of Concerns)
**원칙**: 각 컴포넌트는 명확한 단일 책임을 갖는다.

**DXrevit의 책임**:
- ✅ Revit API를 통한 데이터 추출
- ✅ 데이터 검증 및 JSON 직렬화
- ✅ API 서버로 데이터 전송 (Write Only)
- ❌ 데이터베이스 직접 접근
- ❌ 데이터 읽기 및 분석
- ❌ Navisworks 관련 로직

**DXnavis의 책임**:
- ✅ API 서버에서 데이터 조회 (Read Only)
- ✅ Navisworks API를 통한 자동화
- ✅ TimeLiner 구성 및 시각화
- ❌ 데이터베이스 직접 접근
- ❌ 데이터 수정 또는 삭제
- ❌ Revit 관련 로직

**FastAPI 서버의 책임**:
- ✅ 데이터베이스와의 유일한 통신 창구
- ✅ 데이터 검증 및 보안
- ✅ 비즈니스 로직 실행
- ✅ 인증/인가 관리
- ❌ 3D 모델링 로직
- ❌ UI/UX 관련 로직

### 3.4. DRY 원칙 (Don't Repeat Yourself)
**원칙**: 중복 코드를 최소화하고 재사용성을 극대화한다.

**구현**:
- DXBase 라이브러리에 공통 코드 집중
- DTO, 유틸리티, 설정 관리 공유
- 양 프로젝트에서 동일한 코드 참조

**공유 대상**:
- 데이터 모델 (MetadataRecord, ObjectRecord 등)
- 설정 서비스 (ConfigurationService)
- 로깅 서비스 (LoggingService)
- ID 생성 유틸리티 (IdGenerator)

### 3.5. 설정 외부화
**원칙**: 하드코딩된 설정 값을 제거하고 런타임에 변경 가능하게 한다.

**구현**:
- settings.json 파일로 설정 관리
- 개발/테스트/운영 환경별 설정 분리
- UI를 통한 사용자 친화적 설정 변경
- 코드 재컴파일 없이 설정 변경 가능

**설정 항목**:
- API 서버 URL
- 로그 파일 경로
- 타임아웃 값
- 배치 크기
- 기본 사용자 이름

---

## 4. 프로젝트 구조

### 4.1. 솔루션 구성
```
DX_Workspace/
├── DX.sln                          # 통합 솔루션
├── DXBase/                         # 공유 라이브러리 (.NET Standard 2.0)
│   ├── Models/
│   │   ├── MetadataRecord.cs       # 버전 메타데이터 DTO
│   │   ├── ObjectRecord.cs         # 객체 데이터 DTO
│   │   └── RelationshipRecord.cs   # 관계 데이터 DTO
│   ├── Services/
│   │   ├── ConfigurationService.cs # 설정 관리
│   │   └── LoggingService.cs       # 로깅 관리
│   └── Utils/
│       └── IdGenerator.cs          # ID 생성 유틸리티
├── DXrevit/                        # Revit 애드인 (.NET Core 8.0 - Revit 2025)
│   ├── DXrevit.sln
│   ├── Commands/
│   │   └── SnapshotCommand.cs      # 스냅샷 저장 커맨드
│   ├── Services/
│   │   ├── DataExtractor.cs        # Revit 데이터 추출
│   │   └── ApiDataWriter.cs        # API 클라이언트
│   ├── ViewModels/
│   │   └── SnapshotViewModel.cs    # UI 로직
│   └── Views/
│       └── SnapshotView.xaml       # 스냅샷 UI
└── DXnavis/                        # Navisworks 애드인 (.NET Framework 4.8)
    ├── DXnavis.sln
    ├── Commands/
    │   └── TimeLinerAutoCommand.cs # 4D 자동화 커맨드
    ├── Services/
    │   ├── ApiDataReader.cs        # API 클라이언트
    │   └── TimeLinerConnector.cs   # TimeLiner 연결
    ├── ViewModels/
    │   └── TimeLinerViewModel.cs   # UI 로직
    └── Views/
        └── TimeLinerView.xaml      # 4D 자동화 UI
```

### 4.2. API 서버 구조
```
fastapi_server/
├── main.py                         # FastAPI 애플리케이션 진입점
├── config.py                       # 서버 설정
├── database.py                     # DB 연결 관리
├── models/
│   ├── schemas.py                  # Pydantic 스키마
│   └── orm_models.py               # SQLAlchemy ORM 모델
├── routers/
│   ├── ingest.py                   # 데이터 수집 엔드포인트
│   ├── analytics.py                # 분석 데이터 엔드포인트
│   └── timeliner.py                # 4D 연동 엔드포인트
└── services/
    ├── validation.py               # 데이터 검증
    └── transformation.py           # 데이터 가공
```

---

## 5. 개발 환경 설정

### 5.1. 개발 도구
**필수 도구**:
- Visual Studio 2022 (v17.0+)
  - .NET Core 8.0 개발 도구
  - .NET Framework 4.8 개발 도구
  - .NET Standard 2.0 개발 도구
  - WPF 개발 도구
- Autodesk Revit 2025
- Autodesk Navisworks Manage 2024/2025
- PostgreSQL 15+
- Python 3.11+
- Postman (API 테스트용)

**권장 도구**:
- Git for Windows
- Visual Studio Code (Python 개발용)
- pgAdmin 4 (DB 관리용)
- Docker Desktop (선택사항)

### 5.2. 통합 솔루션 설정
1. Visual Studio에서 `DX.sln` 열기
2. 솔루션 탐색기에서 모든 프로젝트 확인
3. 프로젝트 참조 확인:
   - DXrevit → DXBase 참조
   - DXnavis → DXBase 참조
4. 시작 프로젝트 설정:
   - Revit 테스트 시: DXrevit을 시작 프로젝트로 설정
   - Navisworks 테스트 시: DXnavis를 시작 프로젝트로 설정

### 5.3. 개발 워크플로우
**일반적인 개발 순서**:
1. DXBase에서 공통 코드 작성
2. DXrevit과 DXnavis에서 각각 기능 구현
3. 로컬 FastAPI 서버 실행
4. Revit/Navisworks에서 애드인 테스트
5. 통합 테스트 수행
6. 커밋 및 버전 관리

---

## 6. 배포 고려사항

### 6.1. 배포 대상 사용자
**BIM 엔지니어 (Primary User)**:
- Revit과 Navisworks 전문 사용자
- 프로그래밍 지식 없음
- 개인 PC에서 애드인 실행

**시스템 관리자 (Secondary User)**:
- API 서버 및 데이터베이스 관리
- 백업 및 모니터링
- 사용자 계정 관리

### 6.2. 배포 시나리오

#### 시나리오 A: 개발자 환경
- 모든 컴포넌트를 로컬 PC에 설치
- 로컬 PostgreSQL 사용
- 로컬 FastAPI 서버 실행
- 디버깅 및 로깅 활성화

#### 시나리오 B: 단일 사용자 환경
- BIM 엔지니어 PC에 DXrevit/DXnavis 설치
- 회사 서버에 PostgreSQL + FastAPI 설치
- 사내 네트워크를 통한 HTTPS 통신
- 최소한의 디버깅 로그만 활성화

#### 시나리오 C: 다중 사용자 엔터프라이즈 환경
- 모든 BIM 엔지니어 PC에 애드인 배포
- 중앙 데이터베이스 서버
- 로드 밸런싱된 API 서버
- 인증/인가 시스템 활성화
- 감사 로그 및 모니터링

### 6.3. 배포 체크리스트

**애드인 배포 전**:
- [ ] 디버깅 코드 제거 또는 비활성화
- [ ] 설정 파일 경로 검증
- [ ] API 엔드포인트 URL 확인
- [ ] 오류 메시지 사용자 친화적으로 수정
- [ ] 로그 레벨 적절히 조정
- [ ] 애드인 매니페스트 파일 검증
- [ ] COM 등록 스크립트 준비
- [ ] 사용자 가이드 작성

**API 서버 배포 전**:
- [ ] 환경 변수 설정 (DB 연결 문자열 등)
- [ ] HTTPS 인증서 설치
- [ ] 방화벽 규칙 설정
- [ ] 데이터베이스 마이그레이션 실행
- [ ] 백업 및 복구 절차 수립
- [ ] 모니터링 도구 설정
- [ ] 로그 로테이션 설정

---

## 7. 주의사항 및 금지사항

### 7.1. ✅ 해야 할 것 (DO)

**데이터 관리**:
- ✅ 모든 데이터 변경을 새 버전으로 기록
- ✅ Timestamp와 ModelVersion 항상 포함
- ✅ 데이터 검증 후 저장
- ✅ 트랜잭션으로 데이터 무결성 보장

**보안**:
- ✅ HTTPS 통신만 사용
- ✅ API 키 및 자격증명 안전하게 저장
- ✅ 입력 데이터 검증 및 새니타이징
- ✅ 오류 메시지에서 민감 정보 제거

**코드 품질**:
- ✅ DXBase의 공통 코드 재사용
- ✅ 명확한 주석 및 문서화
- ✅ 의미 있는 변수 및 메서드 이름
- ✅ 예외 처리 및 로깅

**테스트**:
- ✅ 단위 테스트 작성
- ✅ 통합 테스트 수행
- ✅ 다양한 시나리오 테스트
- ✅ 성능 테스트

### 7.2. ❌ 하지 말아야 할 것 (DON'T)

**데이터 관리**:
- ❌ 원시 데이터 테이블에서 UPDATE/DELETE 사용
- ❌ 버전 정보 없이 데이터 저장
- ❌ 검증되지 않은 데이터 저장
- ❌ 트랜잭션 없이 관련 데이터 분산 저장

**보안**:
- ❌ 클라이언트에서 데이터베이스 직접 접근
- ❌ 소스코드에 자격증명 하드코딩
- ❌ HTTP (비암호화) 통신 사용
- ❌ SQL 쿼리에 사용자 입력 직접 삽입

**코드 품질**:
- ❌ DXrevit과 DXnavis 간 직접 참조
- ❌ 중복 코드 작성 (DRY 위반)
- ❌ 매직 넘버 및 하드코딩된 문자열
- ❌ 예외 무시 (empty catch 블록)

**API 설계**:
- ❌ API를 통한 데이터 수정/삭제 엔드포인트
- ❌ 인증 없는 민감 데이터 노출
- ❌ 과도한 데이터 반환 (페이지네이션 미사용)
- ❌ 잘못된 HTTP 메서드 사용

### 7.3. ⚠️ 사용자가 직접 제어해야 할 것

**BIM 엔지니어 책임**:
- ⚠️ Revit에서 공정 ID 등 공유 매개변수 입력
- ⚠️ 스냅샷 저장 타이밍 결정 (마일스톤)
- ⚠️ Navisworks에서 4D 시뮬레이션 최종 검토
- ⚠️ 오류 발생 시 로그 확인 및 보고

**시스템 관리자 책임**:
- ⚠️ API 서버 및 데이터베이스 유지보수
- ⚠️ 백업 스케줄 관리
- ⚠️ 사용자 계정 및 권한 관리
- ⚠️ 시스템 모니터링 및 로그 분석
- ⚠️ 분석용 뷰 및 함수 생성/수정

---

## 8. 성능 및 확장성 고려사항

### 8.1. 성능 목표
- 스냅샷 저장: 10,000 객체 기준 < 30초
- API 응답 시간: 일반 쿼리 < 1초, 복잡한 분석 < 5초
- TimeLiner 자동 연결: 1,000 객체 기준 < 10초
- 데이터베이스 크기: 100 버전 기준 < 10GB

### 8.2. 확장성 전략
**수평 확장 (Horizontal Scaling)**:
- API 서버를 여러 인스턴스로 실행
- 로드 밸런서로 트래픽 분산
- 읽기 전용 데이터베이스 복제본 추가

**수직 확장 (Vertical Scaling)**:
- 데이터베이스 서버 리소스 증대
- 인덱스 최적화
- 쿼리 성능 튜닝

**캐싱 전략**:
- 자주 조회되는 분석 데이터 캐싱
- Redis 등 인메모리 캐시 도입
- API 응답 캐싱

### 8.3. 데이터 보관 정책
- 원시 데이터: 무기한 보관
- 로그 파일: 6개월 보관 후 압축 아카이빙
- 임시 파일: 7일 후 자동 삭제
- 백업: 일일 백업, 30일 보관

---

## 9. 다음 단계

이 아키텍처 문서를 기반으로 다음 단계의 개발 문서를 참조하세요:

1. **Phase 1: DXBase 라이브러리 개발**
   - 공통 데이터 모델 정의
   - 공용 서비스 구현
   - 유틸리티 함수 작성

2. **Phase 2: DXrevit 데이터 생산자 개발**
   - Revit API를 통한 데이터 추출
   - API 클라이언트 구현
   - 사용자 인터페이스 개발

3. **Phase 3: PostgreSQL 데이터베이스 스키마**
   - 테이블 설계 및 생성
   - 인덱스 및 제약조건
   - 분석용 뷰 생성

4. **Phase 4: FastAPI 서버 개발**
   - 엔드포인트 구현
   - 데이터 검증 및 보안
   - 인증/인가 시스템

5. **Phase 5: DXnavis 데이터 소비자 개발**
   - API 클라이언트 구현
   - TimeLiner 자동화
   - 사용자 인터페이스 개발

6. **Phase 6: 통합 테스트 및 배포**
   - End-to-End 테스트
   - 성능 테스트
   - 배포 절차 및 가이드

---

## 부록: 용어 정의

**DTO (Data Transfer Object)**: 계층 간 데이터 전송을 위한 객체

**스냅샷 (Snapshot)**: 특정 시점의 BIM 모델 상태를 캡처한 데이터

**TimeLiner**: Navisworks의 4D 시뮬레이션 도구

**공정 ID (Activity ID)**: 시공 스케줄과 BIM 객체를 연결하는 식별자

**MCP (Model Context Protocol)**: PostgreSQL 및 Power BI와 연동을 위한 별도 프로토콜

**불변성 (Immutability)**: 한번 생성된 데이터는 수정되지 않는 특성

**역할 분리 (Separation of Concerns)**: 각 컴포넌트가 명확한 단일 책임을 갖는 설계 원칙

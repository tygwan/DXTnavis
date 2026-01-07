# PostgreSQL MCP 서버 사용 가이드

## 개요

이 프로젝트는 Bytebase DBHub MCP 서버를 통해 PostgreSQL 데이터베이스(DX_platform)에 자연어로 접근할 수 있도록 설정되어 있습니다.

## 설정 정보

### 데이터베이스 연결
- **Host**: localhost
- **Port**: 5432
- **Database**: DX_platform
- **User**: postgres
- **MCP 서버 이름**: `dx-platform-postgres`

### 설정 파일
- **MCP 설정**: [/.mcp.json](../.mcp.json)
- **환경 변수**: [/fastapi_server/.env](../fastapi_server/.env)

## 사용 방법

### 1. 기본 쿼리

#### 테이블 목록 조회
```
"DX_platform 데이터베이스의 모든 테이블을 보여줘"
"어떤 테이블들이 있는지 알려줘"
```

#### 스키마 확인
```
"navisworks_hierarchy 테이블의 구조를 설명해줘"
"테이블의 컬럼과 데이터 타입을 보여줘"
```

#### 데이터 조회
```
"navisworks_hierarchy 테이블의 최근 10개 레코드를 보여줘"
"SyncID가 'ABC123'인 데이터를 찾아줘"
```

### 2. 데이터 분석

#### 통계 정보
```
"각 테이블의 레코드 수를 알려줘"
"NULL 값이 있는 컬럼을 찾아줘"
"중복된 SyncID를 확인해줘"
```

#### 관계 분석
```
"테이블 간의 관계를 분석해줘"
"외래 키 제약조건을 보여줘"
```

### 3. 데이터 품질 검증

#### 무결성 검사
```
"SyncID 컬럼의 유일성을 검증해줘"
"필수 컬럼에 NULL 값이 있는지 확인해줘"
"데이터 타입 불일치를 찾아줘"
```

#### 성능 분석
```
"인덱스가 없는 컬럼을 찾아줘"
"쿼리 성능을 개선할 방법을 제안해줘"
```

### 4. 스키마 관리

#### 스키마 정보
```
"데이터베이스의 전체 스키마를 문서화해줘"
"테이블 생성 DDL을 보여줘"
```

#### 변경 사항 추적
```
"최근 변경된 테이블을 찾아줘"
"테이블 변경 이력을 보여줘"
```

## 주요 테이블

### navisworks_hierarchy
- **용도**: Navisworks 계층 구조 및 Revit 요소 매핑
- **주요 컬럼**: SyncID, element_id, category, family_name, type_name
- **관련 파일**: [import_hierarchy_csv.py](import_hierarchy_csv.py)

## 주의사항

### 보안
⚠️ `.mcp.json` 파일에는 데이터베이스 자격증명이 포함되어 있습니다.
- 이 파일을 Git에 커밋하지 마세요
- `.gitignore`에 추가되어 있는지 확인하세요

### 성능
- 대용량 데이터 조회 시 LIMIT를 사용하세요
- 복잡한 조인은 성능 영향을 고려하세요
- 인덱스 활용을 권장합니다

### 데이터 수정
- MCP를 통한 데이터 수정은 신중하게 진행하세요
- 중요한 변경은 백업 후 진행하세요
- FastAPI 엔드포인트를 통한 수정을 권장합니다

## 트러블슈팅

### MCP 서버가 로드되지 않는 경우
1. Claude Code 재시작
2. `.mcp.json` 파일 위치 확인 (프로젝트 루트)
3. Node.js 설치 확인 (`node --version`)
4. 콘솔 에러 메시지 확인

### 데이터베이스 연결 실패
1. PostgreSQL 서버 실행 확인
2. `.env` 파일의 자격증명 확인
3. 방화벽 설정 확인
4. 네트워크 연결 확인

### 쿼리 타임아웃
1. 쿼리 범위 축소 (LIMIT 사용)
2. 인덱스 추가 고려
3. 데이터베이스 성능 최적화

## 관련 문서

- [데이터베이스 연결 가이드](DB_CONNECTION_GUIDE.md)
- [CSV 임포트 가이드](README_IMPORT.md)
- [FastAPI 설정](../fastapi_server/config.py)
- [데이터베이스 헬퍼](../fastapi_server/database.py)

## 예제 시나리오

### 시나리오 1: 데이터 무결성 검증
```
1. "navisworks_hierarchy 테이블의 SyncID 컬럼에 중복이 있는지 확인해줘"
2. "NULL 값이 있는 레코드를 찾아줘"
3. "데이터 타입이 올바른지 검증해줘"
```

### 시나리오 2: 성능 분석
```
1. "각 테이블의 레코드 수와 크기를 분석해줘"
2. "인덱스가 없는 외래 키를 찾아줘"
3. "쿼리 성능 최적화 방안을 제안해줘"
```

### 시나리오 3: 스키마 문서화
```
1. "데이터베이스의 전체 ERD를 설명해줘"
2. "각 테이블의 목적과 관계를 문서화해줘"
3. "제약조건과 인덱스 정보를 정리해줘"
```

## 추가 리소스

- [Bytebase DBHub Documentation](https://github.com/bytebase/dbhub)
- [Model Context Protocol Specification](https://modelcontextprotocol.io/)
- [Claude Code MCP Guide](https://docs.claude.com/en/docs/claude-code/mcp)

---

**Last Updated**: 2025-10-18
**MCP Server Version**: @bytebase/dbhub (latest)
**Database Version**: PostgreSQL (확인 필요)

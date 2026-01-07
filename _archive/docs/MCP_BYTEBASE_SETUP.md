# 🔧 PostgreSQL MCP 서버 설정 가이드 (Bytebase DBHub)

**날짜**: 2025-10-30
**상태**: ✅ 설정 완료
**MCP 서버**: @bytebase/dbhub

---

## 📊 현재 설정 정보

### 데이터베이스 정보
- **PostgreSQL 버전**: 17.6
- **데이터베이스**: DX_platform
- **호스트**: localhost:5432
- **사용자**: postgres
- **현재 데이터**: 2,556 unified_objects (Phase A 마이그레이션 완료)

### MCP 서버 구성
- **서버 타입**: Bytebase DBHub
- **패키지**: @bytebase/dbhub
- **실행 방식**: npx (자동 다운로드 및 실행)
- **DSN 형식**: postgres://postgres:123456@localhost:5432/DX_platform?sslmode=disable

---

## 🔧 설정 파일

### 1. `.mcp.json` (프로젝트 루트)

```json
{
  "mcpServers": {
    "dx-platform-postgres": {
      "command": "cmd",
      "args": ["/c", "npx", "-y", "@bytebase/dbhub", "--stdio"],
      "env": {
        "DSN": "postgres://postgres:123456@localhost:5432/DX_platform?sslmode=disable",
        "POSTGRES_HOST": "localhost",
        "POSTGRES_PORT": "5432",
        "POSTGRES_DATABASE": "DX_platform",
        "POSTGRES_USER": "postgres",
        "POSTGRES_PASSWORD": "123456"
      }
    }
  }
}
```

### 2. `.env.mcp` (백업 설정)

```env
# Database Connection String (DSN)
DSN=postgres://postgres:123456@localhost:5432/DX_platform?sslmode=disable

# Individual connection parameters
POSTGRES_HOST=localhost
POSTGRES_PORT=5432
POSTGRES_DATABASE=DX_platform
POSTGRES_USER=postgres
POSTGRES_PASSWORD=123456
```

---

## 🚀 사용 방법

### 데이터베이스 쿼리 실행

Claude에게 자연어로 요청:

```
Show me all tables in the DX_platform database
```

```
Query the unified_objects table and show me:
- Total count
- Count by source_type
- Top 5 categories
```

```
Verify the Phase A migration:
- Check if unique_key, object_guid columns exist
- Show sample data from v_unified_objects_latest view
- Count objects by revision
```

### 스키마 탐색

```
Describe the structure of:
1. projects table
2. revisions table
3. unified_objects table
```

### 데이터 정합성 검증

```
Execute the following verification queries:

1. Check for duplicate unique_keys:
   SELECT revision_id, source_type, unique_key, COUNT(*)
   FROM unified_objects
   GROUP BY 1,2,3 HAVING COUNT(*) > 1;

2. Verify v_latest_revisions view:
   SELECT * FROM v_latest_revisions LIMIT 10;

3. Count objects in latest revision only:
   SELECT COUNT(*) FROM v_unified_objects_latest;
```

---

## 🎯 MCP 도구 기능

### 주요 기능
1. **SQL 쿼리 직접 실행**
2. **테이블 목록 조회**
3. **스키마 정보 확인**
4. **데이터베이스 탐색**
5. **실시간 데이터 분석**

### Bytebase DBHub 특징
- ✅ Multiple database support (PostgreSQL, MySQL, SQLite, etc.)
- ✅ Schema introspection
- ✅ Query execution
- ✅ Connection management
- ✅ DSN-based configuration

---

## 🔍 테스트 및 검증

### Step 1: 연결 테스트

PostgreSQL이 실행 중인지 확인:
```bash
export PGPASSWORD=123456
psql -h localhost -U postgres -d DX_platform -c "SELECT version();"
```

### Step 2: MCP 서버 테스트

```bash
# DSN으로 직접 테스트
npx -y @bytebase/dbhub --dsn="postgres://postgres:123456@localhost:5432/DX_platform?sslmode=disable"
```

### Step 3: Claude Code에서 테스트

Claude에게 요청:
```
List all tables in DX_platform database and show their row counts
```

예상 출력:
```
Tables in DX_platform:
- projects: X rows
- revisions: Y rows
- unified_objects: 2556 rows
- v_latest_revisions: (view)
- v_unified_objects_latest: (view)
- ...
```

---

## 🐛 문제 해결

### 문제 1: "DSN is required" 오류

**원인**: DSN 환경 변수 누락

**해결**:
```json
// .mcp.json의 env에 DSN 추가 확인
"env": {
  "DSN": "postgres://postgres:123456@localhost:5432/DX_platform?sslmode=disable"
}
```

### 문제 2: "connection refused"

**원인**: PostgreSQL 서버 미실행

**해결**:
```bash
# Windows: PostgreSQL 서비스 상태 확인
sc query postgresql-x64-17

# 서비스 시작
net start postgresql-x64-17
```

### 문제 3: "authentication failed"

**원인**: 잘못된 비밀번호

**해결**:
1. `.env` 파일에서 `POSTGRES_PASSWORD` 확인
2. DSN 연결 문자열에서 비밀번호 부분 확인
3. PostgreSQL에서 실제 비밀번호 확인:
   ```sql
   ALTER USER postgres WITH PASSWORD '123456';
   ```

### 문제 4: npx 캐시 문제

**해결**:
```bash
# npm 캐시 정리
npm cache clean --force

# 패키지 재설치
npx -y @bytebase/dbhub --version
```

---

## 🔒 보안 고려사항

### 현재 설정 (개발 환경)
- ✅ localhost만 접근 가능
- ⚠️ 비밀번호 평문 저장 (`.mcp.json`)
- ✅ sslmode=disable (로컬 개발용)

### 프로덕션 권장사항

#### 1. 읽기 전용 사용자 생성
```sql
-- 읽기 전용 사용자 생성
CREATE USER claude_readonly WITH PASSWORD 'secure_random_password';

-- 권한 부여
GRANT CONNECT ON DATABASE DX_platform TO claude_readonly;
GRANT USAGE ON SCHEMA public TO claude_readonly;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO claude_readonly;
GRANT SELECT ON ALL SEQUENCES IN SCHEMA public TO claude_readonly;

-- 뷰 접근 권한
GRANT SELECT ON v_latest_revisions TO claude_readonly;
GRANT SELECT ON v_unified_objects_latest TO claude_readonly;

-- 미래 테이블에 대한 기본 권한
ALTER DEFAULT PRIVILEGES IN SCHEMA public
  GRANT SELECT ON TABLES TO claude_readonly;
```

#### 2. 프로덕션 DSN 업데이트
```json
{
  "mcpServers": {
    "dx-platform-postgres": {
      "command": "cmd",
      "args": ["/c", "npx", "-y", "@bytebase/dbhub", "--stdio"],
      "env": {
        "DSN": "postgres://claude_readonly:secure_random_password@localhost:5432/DX_platform?sslmode=require"
      }
    }
  }
}
```

#### 3. 환경 변수 사용
```json
{
  "mcpServers": {
    "dx-platform-postgres": {
      "command": "cmd",
      "args": ["/c", "npx", "-y", "@bytebase/dbhub", "--stdio"],
      "env": {
        "DSN": "${POSTGRES_DSN}"
      }
    }
  }
}
```

그리고 시스템 환경 변수에 설정:
```bash
# Windows
setx POSTGRES_DSN "postgres://claude_readonly:password@localhost:5432/DX_platform?sslmode=require"
```

---

## 📈 성능 최적화

### 대용량 쿼리 처리

```json
{
  "mcpServers": {
    "dx-platform-postgres": {
      "command": "cmd",
      "args": ["/c", "npx", "-y", "@bytebase/dbhub", "--stdio"],
      "env": {
        "DSN": "postgres://postgres:123456@localhost:5432/DX_platform?sslmode=disable",
        "NODE_OPTIONS": "--max-old-space-size=4096"
      }
    }
  }
}
```

### 연결 풀 설정

DSN에 연결 풀 파라미터 추가:
```
postgres://postgres:123456@localhost:5432/DX_platform?sslmode=disable&pool_max_conns=10&pool_min_conns=2
```

---

## 💡 실전 활용 예시

### Phase A 마이그레이션 검증

```
Execute verification queries for Phase A migration:

1. Verify new columns exist:
   SELECT column_name, data_type
   FROM information_schema.columns
   WHERE table_name = 'unified_objects'
   AND column_name IN ('unique_key', 'object_guid', 'geometry', 'updated_at');

2. Check data backfill:
   SELECT
     COUNT(*) as total,
     COUNT(unique_key) as with_unique_key,
     COUNT(object_guid) as with_object_guid
   FROM unified_objects;

3. Verify unique constraint:
   SELECT constraint_name, constraint_type
   FROM information_schema.table_constraints
   WHERE table_name = 'unified_objects'
   AND constraint_name = 'uq_unified_object_by_unique_key';

4. Test latest revision view:
   SELECT COUNT(*) FROM v_unified_objects_latest;
```

### Revit 데이터 분석

```
Analyze Revit data in DX_platform:

1. Show all projects with Revit revisions
2. For each project, show:
   - Project name
   - Latest revision number
   - Object count in latest revision
   - Top 5 categories with most objects
```

### Navisworks 연동 확인

```
Compare Revit and Navisworks data:

1. Count objects by source_type
2. Identify common object_guids between both sources
3. Show discrepancies in category names
```

---

## ✅ 설치 완료 체크리스트

- [x] PostgreSQL 17.6 실행 중
- [x] DX_platform 데이터베이스 존재
- [x] Phase A 마이그레이션 완료 (2,556 rows)
- [x] `.mcp.json` 생성 및 DSN 설정
- [x] `.env.mcp` 백업 설정 생성
- [x] @bytebase/dbhub 패키지 설치 확인
- [ ] Claude Code 재시작 (필요 시)
- [ ] 테스트 쿼리 실행
- [ ] 마이그레이션 검증 쿼리 실행

---

## 📚 관련 문서

- [Phase A 마이그레이션 가이드](../docs/plan.md#phase-a--database-layer)
- [TODO 진행 상황](TODO.md)
- [기술 명세서](techspec.md)
- [이전 MCP 설정](MCP_SETUP_COMPLETE.md) - @modelcontextprotocol/server-postgres

---

## 🎉 완료!

PostgreSQL MCP 서버 (Bytebase DBHub)가 설정되었습니다!

**현재 가능한 작업**:
- ✅ SQL 쿼리를 자연어로 요청
- ✅ 데이터베이스 스키마 실시간 탐색
- ✅ Phase A 마이그레이션 결과 검증
- ✅ Revit-Navisworks 데이터 분석
- ✅ 복잡한 JOIN 쿼리 자동 생성

**다음 단계**: Phase B (FastAPI Backend) 진행! 🚀

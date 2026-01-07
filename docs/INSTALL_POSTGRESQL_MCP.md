# PostgreSQL MCP 서버 설치 가이드

## 📋 개요

PostgreSQL MCP (Model Context Protocol) 서버를 설치하여 Claude Code에서 직접 데이터베이스를 쿼리할 수 있습니다.

---

## ✅ 사전 요구사항

- ✅ Node.js v22.17.0 (이미 설치됨)
- ✅ npm 11.5.0 (이미 설치됨)
- ✅ PostgreSQL 서버 실행 중 (localhost:5432)

---

## 🚀 설치 방법

### 방법 1: npx로 직접 실행 (권장)

가장 간단한 방법입니다. 별도 설치 없이 바로 사용할 수 있습니다.

```bash
# Claude Code 설정 파일 열기
code ~/.claude/claude_desktop_config.json
```

설정 파일에 다음 내용 추가:

```json
{
  "mcpServers": {
    "postgres": {
      "command": "npx",
      "args": [
        "-y",
        "@modelcontextprotocol/server-postgres",
        "postgresql://postgres:123456@localhost:5432/DX_platform"
      ]
    }
  }
}
```

### 방법 2: 전역 설치

```bash
# PostgreSQL MCP 서버 전역 설치
npm install -g @modelcontextprotocol/server-postgres

# 설치 확인
npx @modelcontextprotocol/server-postgres --version
```

Claude Code 설정:

```json
{
  "mcpServers": {
    "postgres": {
      "command": "node",
      "args": [
        "C:\\Users\\Yoon taegwan\\AppData\\Roaming\\npm\\node_modules\\@modelcontextprotocol\\server-postgres\\dist\\index.js",
        "postgresql://postgres:123456@localhost:5432/DX_platform"
      ]
    }
  }
}
```

### 방법 3: 로컬 프로젝트에 설치

```bash
cd "c:\Users\Yoon taegwan\Desktop\AWP_2025\개발폴더"

# MCP 서버 디렉토리 생성
mkdir mcp-servers
cd mcp-servers

# package.json 생성
npm init -y

# PostgreSQL MCP 서버 설치
npm install @modelcontextprotocol/server-postgres
```

Claude Code 설정:

```json
{
  "mcpServers": {
    "postgres": {
      "command": "node",
      "args": [
        "c:\\Users\\Yoon taegwan\\Desktop\\AWP_2025\\개발폴더\\mcp-servers\\node_modules\\@modelcontextprotocol\\server-postgres\\dist\\index.js",
        "postgresql://postgres:123456@localhost:5432/DX_platform"
      ]
    }
  }
}
```

---

## 🔧 연결 문자열 형식

```
postgresql://[username]:[password]@[host]:[port]/[database]
```

**현재 환경**:
```
postgresql://postgres:123456@localhost:5432/DX_platform
```

**보안 강화 (프로덕션)**:
```json
{
  "mcpServers": {
    "postgres": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-postgres"],
      "env": {
        "POSTGRES_CONNECTION_STRING": "postgresql://postgres:123456@localhost:5432/DX_platform"
      }
    }
  }
}
```

---

## 📊 사용 가능한 MCP 도구

PostgreSQL MCP 서버가 설치되면 다음 도구들을 사용할 수 있습니다:

### 1. `postgres_query`
SQL 쿼리 실행

**예시**:
```sql
SELECT code, name, created_at
FROM projects
WHERE name LIKE '%배관테스트%';
```

### 2. `postgres_list_tables`
데이터베이스의 모든 테이블 목록 조회

### 3. `postgres_describe_table`
특정 테이블의 스키마 정보 조회

**예시**:
```
테이블: projects
```

### 4. `postgres_get_table_info`
테이블의 상세 정보 (컬럼, 타입, 제약조건 등)

---

## ✅ 설치 확인

### 1. Claude Code 재시작

설정 변경 후 Claude Code를 재시작합니다.

### 2. MCP 서버 확인

Claude Code에서 다음 명령어 실행:

```
Show me all tables in the database
```

또는

```
Query the projects table and show me all records
```

### 3. 예상 결과

```
Available tables:
- projects
- revisions
- unified_objects
- navisworks_hierarchy
- activities
- metadata
- ...
```

---

## 🐛 문제 해결

### 문제 1: "command not found: npx"

**해결**:
```bash
# Node.js 경로 확인
where node
where npm

# 환경 변수 PATH에 추가
C:\Program Files\nodejs\
```

### 문제 2: "connection refused"

**원인**: PostgreSQL 서버가 실행되지 않음

**해결**:
```bash
# PostgreSQL 서비스 상태 확인
sc query postgresql-x64-14

# 서비스 시작
net start postgresql-x64-14
```

### 문제 3: "authentication failed"

**원인**: 잘못된 사용자명/비밀번호

**해결**:
```bash
# 연결 문자열 확인
postgresql://postgres:123456@localhost:5432/DX_platform
#              ^^^^^^  ^^^^^^
#              사용자명  비밀번호

# PostgreSQL 비밀번호 재설정 (필요시)
psql -U postgres
ALTER USER postgres PASSWORD '123456';
```

### 문제 4: MCP 서버가 나타나지 않음

**해결**:
1. Claude Code 완전 종료 후 재시작
2. 설정 파일 경로 확인:
   - Windows: `C:\Users\Yoon taegwan\.claude\claude_desktop_config.json`
3. JSON 문법 오류 확인 (쉼표, 중괄호 등)
4. 로그 확인:
   - Claude Code 개발자 도구 열기
   - Console에서 MCP 관련 오류 확인

---

## 🎯 실전 활용 예시

### 1. 프로젝트 확인

```
Claude, query the database to check if project '배관테스트' exists
```

### 2. Revit 데이터 확인

```
Show me all Revit revisions with their object counts
```

### 3. 계층 구조 분석

```
Query navisworks_hierarchy table and show me the top 10 categories
```

### 4. 데이터 정합성 검증

```
Compare Revit and Navisworks object counts by project
```

---

## 📝 설정 파일 예시 (완전판)

**파일 위치**: `C:\Users\Yoon taegwan\.claude\claude_desktop_config.json`

```json
{
  "mcpServers": {
    "postgres": {
      "command": "npx",
      "args": [
        "-y",
        "@modelcontextprotocol/server-postgres",
        "postgresql://postgres:123456@localhost:5432/DX_platform"
      ],
      "env": {
        "NODE_OPTIONS": "--max-old-space-size=4096"
      }
    }
  },
  "globalShortcut": "CommandOrControl+Shift+.",
  "theme": "dark"
}
```

---

## 🔒 보안 고려사항

### 개발 환경 (현재)
- ✅ localhost 연결만 허용
- ⚠️ 비밀번호 평문 저장 (claude_desktop_config.json)

### 프로덕션 환경
1. **환경 변수 사용**:
```json
{
  "mcpServers": {
    "postgres": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-postgres"],
      "env": {
        "DATABASE_URL": "${POSTGRES_CONNECTION_STRING}"
      }
    }
  }
}
```

2. **읽기 전용 사용자 생성**:
```sql
CREATE USER claude_readonly WITH PASSWORD 'secure_password';
GRANT CONNECT ON DATABASE DX_platform TO claude_readonly;
GRANT USAGE ON SCHEMA public TO claude_readonly;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO claude_readonly;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO claude_readonly;
```

연결 문자열:
```
postgresql://claude_readonly:secure_password@localhost:5432/DX_platform
```

---

## 📚 추가 리소스

- [MCP PostgreSQL 공식 문서](https://github.com/modelcontextprotocol/servers/tree/main/src/postgres)
- [Claude Code MCP 가이드](https://docs.anthropic.com/claude/docs/model-context-protocol)
- [PostgreSQL 연결 문자열](https://www.postgresql.org/docs/current/libpq-connect.html#LIBPQ-CONNSTRING)

---

**설치 완료 체크리스트**:
- [ ] Node.js 및 npm 버전 확인
- [ ] PostgreSQL 서버 실행 확인
- [ ] MCP 서버 설치 (npx 또는 npm)
- [ ] claude_desktop_config.json 설정
- [ ] Claude Code 재시작
- [ ] 테스트 쿼리 실행
- [ ] 프로젝트 데이터 확인

**다음 단계**: 실제 쿼리로 Revit 스냅샷 데이터를 확인해봅시다!

# AWP 2025 BIM Data Integration System - 배포 및 사용 가이드

## 📋 목차

1. [시스템 개요](#시스템-개요)
2. [배포 준비](#배포-준비)
3. [데이터베이스 서버 설정](#데이터베이스-서버-설정)
4. [FastAPI 서버 배포](#fastapi-서버-배포)
5. [Revit 플러그인 설치](#revit-플러그인-설치)
6. [Navisworks 플러그인 설치](#navisworks-플러그인-설치)
7. [데이터 전송 및 관리](#데이터-전송-및-관리)
8. [데이터 조회 및 시각화](#데이터-조회-및-시각화)
9. [문제 해결](#문제-해결)

---

## 시스템 개요

### 시스템 아키텍처

```
┌─────────────────┐
│  Revit 2025     │ ──┐
│  (BIM 모델링)   │   │
└─────────────────┘   │
                      │ HTTP/REST API
┌─────────────────┐   │
│ Navisworks 2025 │ ──┤
│  (4D 시뮬레이션)│   │
└─────────────────┘   │
                      ↓
              ┌─────────────────┐
              │  FastAPI Server │
              │  (Python 3.10)  │
              └─────────────────┘
                      ↓
              ┌─────────────────┐
              │   PostgreSQL    │
              │   Database 17   │
              └─────────────────┘
                      ↓
              ┌─────────────────┐
              │   Dashboard     │
              │  (웹 브라우저)  │
              └─────────────────┘
```

### 주요 기능

1. **Revit → Database**: BIM 모델 데이터 자동 추출 및 저장
2. **Navisworks 연동**: 4D 시뮬레이션 데이터 연계
3. **실시간 모니터링**: 웹 대시보드를 통한 데이터 현황 확인
4. **데이터 분석**: PostgreSQL 쿼리를 통한 심층 분석

---

## 배포 준비

### 시스템 요구사항

#### 서버 (중앙 관리)
- **운영체제**: Windows Server 2019 이상 또는 Windows 10/11 Pro
- **CPU**: 4코어 이상
- **메모리**: 8GB 이상 (16GB 권장)
- **저장공간**: 100GB 이상 SSD
- **네트워크**: 고정 IP 또는 도메인

#### 클라이언트 (사용자 PC)
- **운영체제**: Windows 10/11 (64bit)
- **Revit**: Autodesk Revit 2025
- **Navisworks**: Autodesk Navisworks Manage 2025
- **네트워크**: 서버 접속 가능

### 필수 소프트웨어

#### 서버
1. **PostgreSQL 17**
   - 다운로드: https://www.postgresql.org/download/windows/
   - 설치 시 포트: 5432 (기본값)
   - 관리 도구: pgAdmin 4 포함

2. **Python 3.10**
   - 다운로드: https://www.python.org/downloads/
   - 설치 시 "Add Python to PATH" 체크 필수

3. **Git** (선택사항)
   - 다운로드: https://git-scm.com/downloads
   - 소스코드 관리용

#### 클라이언트
1. **Autodesk Revit 2025**
2. **Autodesk Navisworks Manage 2025**
3. **.NET Framework 4.8** (Revit/Navisworks 플러그인용)

---

## 데이터베이스 서버 설정

### 1. PostgreSQL 설치

#### 1.1 설치 파일 실행
```
postgresql-17-windows-x64.exe 실행
```

#### 1.2 설치 옵션
- Installation Directory: `C:\Program Files\PostgreSQL\17`
- Data Directory: `C:\Program Files\PostgreSQL\17\data`
- Port: `5432`
- **Superuser Password**: 안전한 비밀번호 설정 (예: `YourSecurePassword123!`)
  - ⚠️ **이 비밀번호를 기록해두세요!**

#### 1.3 추가 구성요소 선택
- [x] PostgreSQL Server
- [x] pgAdmin 4
- [x] Command Line Tools

### 2. 데이터베이스 생성

#### 2.1 pgAdmin 4 실행
1. 시작 메뉴 → PostgreSQL 17 → pgAdmin 4
2. Servers → PostgreSQL 17 우클릭 → Connect
3. 설치 시 설정한 비밀번호 입력

#### 2.2 데이터베이스 생성
1. Databases 우클릭 → Create → Database
2. Database 이름: `DX_platform`
3. Owner: `postgres`
4. Encoding: `UTF8`
5. Save 클릭

### 3. 테이블 생성

#### 3.1 SQL 스크립트 준비
프로젝트 폴더에서 `temp_init.sql` 파일을 찾습니다:
```
AWP_2025\개발폴더\temp_init.sql
```

#### 3.2 SQL 실행
1. pgAdmin 4에서 DX_platform 데이터베이스 선택
2. 상단 메뉴 → Tools → Query Tool
3. 파일 열기 → `temp_init.sql` 선택
4. Execute (F5) 클릭

#### 3.3 테이블 생성 확인
```sql
-- Query Tool에서 실행
SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public';
```

**예상 결과**:
- `metadata` (버전 메타데이터)
- `objects` (BIM 객체 데이터)
- `relationships` (객체 간 관계)

### 4. 네트워크 접근 설정 (원격 접속 허용)

#### 4.1 postgresql.conf 수정
```bash
# 파일 위치
C:\Program Files\PostgreSQL\17\data\postgresql.conf

# 수정 내용 (메모장으로 열기)
listen_addresses = '*'    # 모든 IP에서 접속 허용
port = 5432              # 기본 포트
```

#### 4.2 pg_hba.conf 수정
```bash
# 파일 위치
C:\Program Files\PostgreSQL\17\data\pg_hba.conf

# 맨 아래 추가 (로컬 네트워크 접근 허용)
# TYPE  DATABASE        USER            ADDRESS                 METHOD
host    DX_platform     postgres        192.168.0.0/16          md5
host    DX_platform     postgres        10.0.0.0/8              md5
```

**설명**:
- `192.168.0.0/16`: 일반적인 사무실 네트워크
- `10.0.0.0/8`: 기업 내부 네트워크
- `md5`: 암호화된 비밀번호 인증

#### 4.3 PostgreSQL 서비스 재시작
```
1. Windows 키 + R
2. services.msc 입력
3. postgresql-x64-17 찾기
4. 우클릭 → Restart
```

#### 4.4 방화벽 설정
```
1. 제어판 → Windows Defender 방화벽
2. 고급 설정
3. 인바운드 규칙 → 새 규칙
   - 규칙 유형: 포트
   - 프로토콜: TCP
   - 특정 로컬 포트: 5432
   - 연결 허용
   - 이름: PostgreSQL
```

### 5. 연결 테스트

#### 5.1 로컬 연결 테스트
```bash
# 명령 프롬프트 (cmd)
psql -U postgres -d DX_platform

# 비밀번호 입력 후
DX_platform=# \dt
```

**성공 시**: 테이블 목록 표시

#### 5.2 원격 연결 테스트 (다른 PC에서)
```bash
# 서버 IP를 확인 (서버에서 실행)
ipconfig

# 클라이언트 PC에서 테스트
psql -h 192.168.1.100 -U postgres -d DX_platform
```

---

## FastAPI 서버 배포

### 1. 소스코드 배포

#### 1.1 프로젝트 폴더 복사
서버 PC로 다음 폴더를 복사:
```
AWP_2025\개발폴더\
├── fastapi_server\
│   ├── __init__.py
│   ├── main.py
│   ├── config.py
│   ├── database.py
│   ├── routers\
│   ├── models\
│   ├── middleware\
│   └── templates\
├── scripts\
└── .env (생성 필요)
```

**배포 경로 예시**:
```
C:\AWP_Server\
```

#### 1.2 환경 변수 파일 생성
`C:\AWP_Server\.env` 파일을 생성하고 아래 내용 입력:

```env
# 데이터베이스 연결
DATABASE_URL=postgresql://postgres:YourSecurePassword123!@localhost:5432/DX_platform
DB_POOL_MIN=1
DB_POOL_MAX=10

# 서버 설정
HOST=0.0.0.0
PORT=8000
DEBUG=False

# 로깅
LOG_LEVEL=INFO

# CORS 설정 (클라이언트 PC IP 추가)
ALLOWED_ORIGINS=http://localhost,http://127.0.0.1,http://192.168.1.*
ALLOWED_HOSTS=*
```

**⚠️ 주의사항**:
- `YourSecurePassword123!`를 실제 PostgreSQL 비밀번호로 변경
- `DEBUG=False`: 운영 환경에서는 반드시 False
- `ALLOWED_ORIGINS`: 필요한 클라이언트 IP만 추가

### 2. Python 패키지 설치

#### 2.1 가상 환경 생성 (권장)
```bash
cd C:\AWP_Server

# 가상 환경 생성
python -m venv venv

# 가상 환경 활성화
venv\Scripts\activate
```

#### 2.2 필수 패키지 설치
```bash
# requirements.txt 내용
pip install fastapi==0.104.1
pip install uvicorn[standard]==0.24.0
pip install asyncpg==0.29.0
pip install python-dotenv==1.0.0
pip install jinja2==3.1.2
```

또는 `requirements.txt` 파일이 있다면:
```bash
pip install -r requirements.txt
```

### 3. 서버 실행 테스트

#### 3.1 수동 실행 테스트
```bash
cd C:\AWP_Server
venv\Scripts\activate
python -m uvicorn fastapi_server.main:app --host 0.0.0.0 --port 8000
```

#### 3.2 접속 테스트
브라우저에서:
```
http://localhost:8000/
```

**예상 결과**:
대시보드가 표시되어야 합니다.

#### 3.3 API 문서 확인
```
http://localhost:8000/docs
```

Swagger UI가 표시됩니다.

### 4. Windows 서비스 등록 (자동 시작)

#### 4.1 NSSM 다운로드
```
https://nssm.cc/download
```

NSSM을 `C:\nssm\` 폴더에 압축 해제

#### 4.2 서비스 설치
```bash
# 관리자 권한 명령 프롬프트
cd C:\nssm\win64

nssm install AWP_FastAPI
```

#### 4.3 서비스 설정
NSSM GUI에서:
- **Path**: `C:\AWP_Server\venv\Scripts\python.exe`
- **Startup directory**: `C:\AWP_Server`
- **Arguments**: `-m uvicorn fastapi_server.main:app --host 0.0.0.0 --port 8000`
- **Service name**: `AWP_FastAPI`

#### 4.4 환경 변수 설정
Environment 탭:
```
PYTHONPATH=C:\AWP_Server
```

#### 4.5 서비스 시작
```bash
nssm start AWP_FastAPI
```

#### 4.6 서비스 상태 확인
```bash
nssm status AWP_FastAPI
```

### 5. 방화벽 설정

```
1. 제어판 → Windows Defender 방화벽
2. 고급 설정
3. 인바운드 규칙 → 새 규칙
   - 규칙 유형: 포트
   - 프로토콜: TCP
   - 특정 로컬 포트: 8000
   - 연결 허용
   - 이름: AWP FastAPI
```

### 6. 서버 IP 확인 및 클라이언트 설정

#### 6.1 서버 IP 확인
서버 PC에서:
```bash
ipconfig
```

**예시 결과**:
```
IPv4 주소: 192.168.1.100
```

#### 6.2 클라이언트 접속 테스트
클라이언트 PC 브라우저에서:
```
http://192.168.1.100:8000/
```

---

## Revit 플러그인 설치

### 1. 플러그인 파일 준비

#### 1.1 빌드된 파일 확인
```
DXrevit\
├── DXrevit.dll
├── DXrevit.addin
└── dependencies\
```

#### 1.2 설치 경로
```
C:\ProgramData\Autodesk\Revit\Addins\2025\
```

### 2. 플러그인 설치

#### 2.1 폴더 생성
```
C:\ProgramData\Autodesk\Revit\Addins\2025\DXrevit\
```

#### 2.2 파일 복사
```
DXrevit.dll → C:\ProgramData\Autodesk\Revit\Addins\2025\DXrevit\
dependencies\ → C:\ProgramData\Autodesk\Revit\Addins\2025\DXrevit\
```

#### 2.3 매니페스트 파일 생성
`C:\ProgramData\Autodesk\Revit\Addins\2025\DXrevit.addin` 생성:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>DXrevit</Name>
    <Assembly>DXrevit\DXrevit.dll</Assembly>
    <AddInId>12345678-1234-1234-1234-123456789012</AddInId>
    <FullClassName>DXrevit.Application</FullClassName>
    <VendorId>AWP</VendorId>
    <VendorDescription>AWP 2025 BIM Integration</VendorDescription>
  </AddIn>
</RevitAddIns>
```

### 3. Revit에서 확인

#### 3.1 Revit 실행
1. Autodesk Revit 2025 실행
2. Add-ins 탭 확인
3. "DXrevit" 버튼이 있어야 함

#### 3.2 플러그인 로드 확인
Revit 메뉴:
```
Add-ins → External Tools → DXrevit
```

### 4. API 서버 설정

#### 4.1 설정 파일 위치
```
C:\Users\[사용자명]\AppData\Roaming\DXrevit\config.json
```

#### 4.2 설정 내용
```json
{
  "apiUrl": "http://192.168.1.100:8000",
  "autoSync": false,
  "syncInterval": 300
}
```

**설정 설명**:
- `apiUrl`: FastAPI 서버 주소 (서버 IP:포트)
- `autoSync`: 자동 동기화 여부
- `syncInterval`: 자동 동기화 간격 (초)

#### 4.3 GUI에서 설정 (플러그인 패널)
```
DXrevit 패널 → Settings
  - API Server URL: http://192.168.1.100:8000
  - Test Connection 클릭
```

**성공 시**: "✅ 연결 성공!" 메시지

---

## Navisworks 플러그인 설치

### 1. 플러그인 파일 준비

#### 1.1 빌드된 파일 확인
```
navisworks_addin\
├── NavisworksTimelinerPlugin.dll
├── NavisworksTimelinerPlugin.xaml
└── dependencies\
```

#### 1.2 설치 경로
```
C:\ProgramData\Autodesk\Navisworks Manage 2025\Plugins\
```

### 2. 플러그인 설치

#### 2.1 폴더 생성
```
C:\ProgramData\Autodesk\Navisworks Manage 2025\Plugins\NavisworksTimelinerPlugin\
```

#### 2.2 파일 복사
```
NavisworksTimelinerPlugin.dll → ...Plugins\NavisworksTimelinerPlugin\
NavisworksTimelinerPlugin.xaml → ...Plugins\NavisworksTimelinerPlugin\
dependencies\ → ...Plugins\NavisworksTimelinerPlugin\
```

### 3. COM 등록 (필요 시)

#### 3.1 관리자 권한 명령 프롬프트
```bash
cd "C:\ProgramData\Autodesk\Navisworks Manage 2025\Plugins\NavisworksTimelinerPlugin"
RegAsm.exe /codebase NavisworksTimelinerPlugin.dll
```

### 4. Navisworks에서 확인

#### 4.1 Navisworks 실행
1. Autodesk Navisworks Manage 2025 실행
2. Home 탭 → Tools 패널 확인
3. "Timeliner Connector" 버튼이 있어야 함

#### 4.2 플러그인 활성화
```
Options → Add-ins → External Tools
  - [x] Navisworks Timeliner Plugin 체크
```

---

## 데이터 전송 및 관리

### 1. Revit에서 데이터 전송

#### 1.1 스냅샷 생성
```
1. Revit에서 프로젝트 열기
2. DXrevit 탭 → Snapshot 버튼 클릭
3. 스냅샷 정보 입력:
   - Project Name: 프로젝트 이름
   - Created By: 작성자
   - Description: 변경 사유
4. "스냅샷 저장" 클릭
```

#### 1.2 전송 확인
```
DXrevit 패널:
  - Status: ✅ 전송 완료
  - Objects: 852개
  - Time: 213ms
```

#### 1.3 로그 확인
```
C:\Users\[사용자명]\AppData\Local\DXrevit\Logs\
  - dxrevit_20251016.log
```

### 2. 전송된 데이터 구조

#### 2.1 metadata 테이블
```sql
model_version: 프로젝트이름_20251016_030006
timestamp: 2025-10-16 03:00:06+09
project_name: 프로젝트이름
created_by: yoon
description: 2025-10-16 스냅샷
total_object_count: 852
revit_file_path: C:\Users\...\프로젝트.rvt
```

#### 2.2 objects 테이블
```sql
id: 1
model_version: 프로젝트이름_20251016_030006
object_id: 7f8a9b1c-2d3e-4f5a-6b7c-8d9e0f1a2b3c
element_id: 123456
category: Walls
family: 기본 벽
type: 200mm
activity_id: null
properties: {
  "Level": "1F",
  "Height": 3000,
  "Length": 5000,
  ...
}
bounding_box: {
  "min": {"x": 0, "y": 0, "z": 0},
  "max": {"x": 5000, "y": 200, "z": 3000}
}
created_at: 2025-10-16 03:00:06+09
```

### 3. Navisworks 연동

#### 3.1 CSV 스케줄 준비
```csv
TaskName,Start,Finish,SyncID
작업1,2025-01-01,2025-01-15,7f8a9b1c-2d3e-4f5a-6b7c-8d9e0f1a2b3c
작업2,2025-01-16,2025-01-31,8g9b0c2d-3e4f-5g6b-7c8d-9e0f1a2b3c4d
```

**컬럼 설명**:
- `TaskName`: 작업 이름
- `Start`: 시작일 (YYYY-MM-DD)
- `Finish`: 종료일 (YYYY-MM-DD)
- `SyncID`: Revit ObjectId (UUID)

#### 3.2 Timeliner 연결
```
1. Navisworks에서 모델 열기
2. Timeliner Connector 패널 → Load CSV
3. CSV 파일 선택
4. "Connect to Timeliner" 클릭
5. Object Set 자동 생성 확인
```

#### 3.3 시뮬레이션 실행
```
1. Timeliner 탭 열기
2. Tasks 확인 (CSV에서 가져온 작업들)
3. Simulate → Play
```

---

## 데이터 조회 및 시각화

### 1. 웹 대시보드

#### 1.1 대시보드 접속
```
http://192.168.1.100:8000/
```

#### 1.2 대시보드 기능
- **시스템 상태**: FastAPI, PostgreSQL 상태
- **통계**:
  - 총 버전 수
  - 총 객체 수
  - 최근 스냅샷 시간
- **Revit 상태**: 마지막 연결 시간, 전송 객체 수
- **Navisworks 상태**: 연결 상태

#### 1.3 실시간 업데이트
- 자동 새로고침: 10초마다
- 수동 새로고침: F5

### 2. pgAdmin 4로 데이터 조회

#### 2.1 pgAdmin 접속
```
1. pgAdmin 4 실행
2. Servers → PostgreSQL 17 → Databases → DX_platform
```

#### 2.2 기본 쿼리

##### 2.2.1 버전 목록 조회
```sql
SELECT
    model_version,
    project_name,
    created_by,
    total_object_count,
    timestamp
FROM metadata
ORDER BY timestamp DESC;
```

##### 2.2.2 카테고리별 객체 수
```sql
SELECT
    category,
    COUNT(*) as count
FROM objects
WHERE model_version = '프로젝트이름_20251016_030006'
GROUP BY category
ORDER BY count DESC;
```

##### 2.2.3 특정 레벨 객체 조회
```sql
SELECT
    object_id,
    category,
    family,
    type,
    properties->>'Level' as level
FROM objects
WHERE
    model_version = '프로젝트이름_20251016_030006'
    AND properties->>'Level' = '1F';
```

##### 2.2.4 바운딩 박스 검색 (특정 범위 객체)
```sql
SELECT
    object_id,
    category,
    (bounding_box->'max'->>'x')::float - (bounding_box->'min'->>'x')::float as width,
    (bounding_box->'max'->>'y')::float - (bounding_box->'min'->>'y')::float as depth,
    (bounding_box->'max'->>'z')::float - (bounding_box->'min'->>'z')::float as height
FROM objects
WHERE
    model_version = '프로젝트이름_20251016_030006'
    AND category = 'Walls';
```

### 3. Python 스크립트로 데이터 분석

#### 3.1 데이터 추출 스크립트
`C:\AWP_Server\scripts\export_data.py`:

```python
import asyncio
import asyncpg
import csv

async def export_to_csv():
    conn = await asyncpg.connect(
        "postgresql://postgres:YourPassword@localhost:5432/DX_platform"
    )

    # 데이터 조회
    rows = await conn.fetch("""
        SELECT
            object_id,
            category,
            family,
            type,
            properties->>'Level' as level,
            created_at
        FROM objects
        WHERE model_version = '프로젝트이름_20251016_030006'
        ORDER BY category, family
    """)

    # CSV 저장
    with open('export.csv', 'w', newline='', encoding='utf-8-sig') as f:
        writer = csv.writer(f)
        writer.writerow(['ObjectId', 'Category', 'Family', 'Type', 'Level', 'CreatedAt'])

        for row in rows:
            writer.writerow([
                row['object_id'],
                row['category'],
                row['family'],
                row['type'],
                row['level'],
                row['created_at']
            ])

    await conn.close()
    print(f"✅ {len(rows)}개 객체 내보내기 완료!")

if __name__ == "__main__":
    asyncio.run(export_to_csv())
```

실행:
```bash
cd C:\AWP_Server\scripts
python export_data.py
```

### 4. Power BI 연동 (고급)

#### 4.1 Power BI Desktop 설치
```
https://powerbi.microsoft.com/downloads/
```

#### 4.2 PostgreSQL 연결
```
1. Power BI Desktop 실행
2. 데이터 가져오기 → PostgreSQL
3. 서버: localhost:5432
4. 데이터베이스: DX_platform
5. 테이블 선택: metadata, objects
```

#### 4.3 시각화 생성
- **막대 그래프**: 카테고리별 객체 수
- **꺾은선 그래프**: 시간별 버전 변화
- **테이블**: 상위 100개 객체 목록

---

## 문제 해결

### 1. 연결 오류

#### 1.1 "Connection refused"
**원인**: 서버가 실행 중이 아님

**해결**:
```bash
# 서비스 상태 확인
nssm status AWP_FastAPI

# 서비스 시작
nssm start AWP_FastAPI
```

#### 1.2 "Database connection failed"
**원인**: PostgreSQL 서버 중지 또는 비밀번호 오류

**해결**:
```bash
# PostgreSQL 서비스 확인
services.msc → postgresql-x64-17 → 시작

# .env 파일 비밀번호 확인
C:\AWP_Server\.env
```

#### 1.3 "404 Not Found"
**원인**: 잘못된 API URL

**해결**:
```
Revit 플러그인 설정:
  - http://192.168.1.100:8000 (올바름)
  - http://192.168.1.100:8000/ (올바름)
  - http://192.168.1.100 (잘못됨 - 포트 누락)
```

### 2. 데이터 전송 오류

#### 2.1 "스냅샷 저장 실패"
**Revit 로그 확인**:
```
C:\Users\[사용자]\AppData\Local\DXrevit\Logs\dxrevit_[날짜].log
```

**일반적인 원인**:
1. API 서버 연결 실패 → 서버 상태 확인
2. 데이터베이스 테이블 없음 → temp_init.sql 재실행
3. 네트워크 타임아웃 → 방화벽 확인

#### 2.2 "체크 제약 조건 위반"
**원인**: 데이터 형식 불일치

**해결**:
```bash
# 제약 조건 제거
cd C:\AWP_Server\scripts
python fix_constraint.py
```

### 3. 성능 문제

#### 3.1 느린 응답 속도
**데이터베이스 인덱스 확인**:
```sql
-- 인덱스 목록
SELECT * FROM pg_indexes WHERE tablename = 'objects';

-- 인덱스 재생성
REINDEX TABLE objects;
```

#### 3.2 메모리 부족
**PostgreSQL 메모리 설정**:
```
postgresql.conf:
  shared_buffers = 256MB → 512MB
  work_mem = 4MB → 8MB
```

서비스 재시작 후 적용

---

## 부록

### A. 연결 문자열 포맷

```
postgresql://[사용자]:[비밀번호]@[호스트]:[포트]/[데이터베이스]

예시:
postgresql://postgres:123456@localhost:5432/DX_platform
postgresql://postgres:123456@192.168.1.100:5432/DX_platform
```

### B. 기본 포트 정보

| 서비스 | 포트 | 프로토콜 |
|--------|------|----------|
| PostgreSQL | 5432 | TCP |
| FastAPI | 8000 | HTTP |
| pgAdmin 4 | 5050 | HTTP |

### C. 로그 파일 위치

| 구분 | 경로 |
|------|------|
| FastAPI | `C:\AWP_Server\logs\` |
| PostgreSQL | `C:\Program Files\PostgreSQL\17\data\log\` |
| Revit 플러그인 | `C:\Users\[사용자]\AppData\Local\DXrevit\Logs\` |
| Navisworks 플러그인 | `C:\Users\[사용자]\AppData\Local\NavisTimeliner\Logs\` |

### D. 백업 및 복원

#### 데이터베이스 백업
```bash
pg_dump -U postgres -d DX_platform > backup_20251016.sql
```

#### 데이터베이스 복원
```bash
psql -U postgres -d DX_platform < backup_20251016.sql
```

---

## 라이선스 및 지원

- **개발**: AWP 2025 프로젝트
- **버전**: 1.0.0
- **문의**: [이메일 또는 연락처]

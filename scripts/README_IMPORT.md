# Navisworks Hierarchy CSV Import Guide

## 📋 개요

Navisworks 계층구조 CSV 파일을 PostgreSQL 데이터베이스로 안전하게 Import하는 가이드

## 🚀 실행 방법

### 방법 1: Python 스크립트 사용 (권장)

#### Step 1: 필수 패키지 설치
```bash
cd c:\Users\Yoon taegwan\Desktop\AWP_2025\개발폴더\scripts
pip install asyncpg
```

#### Step 2: 스크립트 실행
```bash
python import_hierarchy_csv.py
```

#### 예상 출력:
```
============================================================
🚀 Navisworks Hierarchy CSV Import Tool
============================================================

2025-01-15 10:30:00 - INFO - ✅ Database connected: postgresql://postgresql:1234@localhost:5432/dx_platform
2025-01-15 10:30:00 - INFO - 📋 Checking/Creating table...
2025-01-15 10:30:00 - INFO - ✅ Table ready: navisworks_hierarchy
2025-01-15 10:30:00 - INFO - 📂 Reading CSV: Hierarchy_20251012_170425.csv
2025-01-15 10:30:00 - INFO - 📦 Batch size: 1000
2025-01-15 10:30:00 - INFO - 🏷️  Model version: Hierarchy_20251012_170425
2025-01-15 10:30:05 - INFO - 📊 Progress: 10,000 rows inserted...
2025-01-15 10:30:10 - INFO - 📊 Progress: 20,000 rows inserted...
...
2025-01-15 10:32:00 - INFO - 📊 Progress: 440,000 rows inserted...

🔍 Validating import results...

============================================================
📊 Import Summary
============================================================
Total CSV rows:        445,730
Successfully inserted: 445,730
Error rows:            0
Unique objects:        12,345

🏷️  Top 5 Categories:
  - 항목                            123,456 rows
  - 객체                             98,765 rows
  - 지오메트리                        87,654 rows
  - 재질                             76,543 rows
  - 기타                             59,312 rows
============================================================

2025-01-15 10:32:05 - INFO - ✅ Import completed successfully!
2025-01-15 10:32:05 - INFO - 🔌 Database connection closed
```

### 방법 2: pgAdmin4에서 SQL 실행

#### Step 1: 테이블 생성
1. pgAdmin4 열기
2. `dx_platform` 데이터베이스 선택
3. Query Tool 열기 (Alt+Shift+Q)
4. `database/tables/navisworks_hierarchy.sql` 파일 내용 복사
5. 실행 (F5)

#### Step 2: psql 명령어로 Import
```bash
# CMD 또는 PowerShell에서 실행
psql -h localhost -U postgresql -d dx_platform -c "\COPY navisworks_hierarchy (object_id, parent_id, level, display_name, category, property_name, property_value) FROM 'c:\Users\Yoon taegwan\Desktop\AWP_2025\개발폴더\Hierarchy_20251012_170425.csv' WITH (FORMAT CSV, HEADER TRUE, ENCODING 'UTF8');"
```

## 📊 Import 후 검증 쿼리

### 1. 총 행 수 확인
```sql
SELECT COUNT(*) AS total_rows
FROM navisworks_hierarchy;
-- 예상: 445,730 rows
```

### 2. 고유 객체 수 확인
```sql
SELECT COUNT(DISTINCT object_id) AS unique_objects
FROM navisworks_hierarchy;
```

### 3. 카테고리별 통계
```sql
SELECT
    category,
    COUNT(*) AS property_count,
    COUNT(DISTINCT object_id) AS object_count
FROM navisworks_hierarchy
GROUP BY category
ORDER BY property_count DESC;
```

### 4. 샘플 데이터 확인
```sql
SELECT *
FROM navisworks_hierarchy
WHERE object_id = '8dd55e0a-2aee-5612-8465-b8f7ff0e7da3'
ORDER BY property_name;
```

### 5. 계층 구조 확인 (루트 객체)
```sql
SELECT DISTINCT
    object_id,
    display_name,
    level
FROM navisworks_hierarchy
WHERE level = 0
ORDER BY display_name;
```

## 🔧 트러블슈팅

### 문제 1: "asyncpg not installed"
```bash
pip install asyncpg
```

### 문제 2: "Database connection failed"
- PostgreSQL이 실행 중인지 확인
- 데이터베이스 URL이 올바른지 확인
- 포트(5432)가 열려있는지 확인

```bash
# Windows에서 PostgreSQL 서비스 확인
sc query postgresql-x64-15

# PostgreSQL 시작
net start postgresql-x64-15
```

### 문제 3: "Permission denied"
- PostgreSQL 사용자 권한 확인
- 데이터베이스 소유자 확인

```sql
-- 권한 부여
GRANT ALL PRIVILEGES ON DATABASE dx_platform TO postgresql;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO postgresql;
```

### 문제 4: "CSV file not found"
- 파일 경로가 올바른지 확인
- 파일이 존재하는지 확인
- `import_hierarchy_csv.py` 파일의 `CSV_PATH` 수정

```python
CSV_PATH = r"c:\Users\Yoon taegwan\Desktop\AWP_2025\개발폴더\Hierarchy_20251012_170425.csv"
```

### 문제 5: "Too many errors"
- CSV 파일 인코딩 확인 (UTF-8)
- CSV 형식 확인 (쉼표 구분자)
- 데이터 무결성 확인

## 📁 생성된 파일

```
개발폴더/
├── database/
│   └── tables/
│       └── navisworks_hierarchy.sql  ← 테이블 스키마
└── scripts/
    ├── import_hierarchy_csv.py       ← Import 스크립트
    └── README_IMPORT.md              ← 이 파일
```

## 🎯 다음 단계

1. ✅ 테이블 생성 완료
2. ✅ CSV Import 완료
3. 🔄 데이터 검증
4. 📊 FastAPI 엔드포인트 생성
5. 🔗 Revit/Navisworks 통합

## 📞 지원

문제가 발생하면:
1. 로그 파일 확인
2. PostgreSQL 로그 확인 (`C:\Program Files\PostgreSQL\15\data\log\`)
3. 스크립트의 에러 메시지 확인

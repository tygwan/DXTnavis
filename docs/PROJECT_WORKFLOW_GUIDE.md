# AWP 프로젝트 워크플로우 가이드

## 📋 개요

AWP 시스템에서 Revit과 Navisworks를 연동하기 위한 올바른 프로젝트 관리 워크플로우입니다.

---

## ✅ 올바른 순서 (CRITICAL!)

### 1단계: Revit에서 프로젝트 생성 (필수!)

**반드시 Revit에서 먼저 시작해야 합니다!**

1. Revit에서 프로젝트 파일 열기 (예: `배관테스트.rvt`)
2. DXrevit 플러그인 실행
3. **"프로젝트 정보 추출 & 업로드"** 버튼 클릭
4. 로그 확인: "프로젝트 생성 완료: 배관테스트" 메시지 확인

**이 단계를 건너뛰면 Navisworks에서 오류가 발생합니다!**

### 2단계: Navisworks에서 프로젝트 연결

Revit에서 프로젝트를 생성한 후:

1. Navisworks Manage 2025 실행
2. 프로젝트 NWC 파일 열기
3. DXnavis 플러그인 실행 (View → DXnavis 속성 확인기)
4. "프로젝트 감지" 버튼 클릭
5. CSV 파일 선택 (Navisworks 계층 정보 CSV)
6. 시스템이 자동으로 프로젝트 코드 감지
7. API에서 프로젝트 존재 확인
8. ✅ "프로젝트 '배관테스트'를 찾았습니다" 메시지 확인

---

## 🔍 현재 상황 진단

### 문제: "프로젝트 '배관테스트'가 API에 등록되어 있지 않습니다"

**원인**: Revit에서 프로젝트를 생성하지 않았습니다.

**확인 방법**:
```bash
cd "c:\Users\Yoon taegwan\Desktop\AWP_2025\개발폴더\scripts"
python check_project.py
```

**예상 출력**:
```
📋 전체 프로젝트 목록:
❌ 프로젝트 '배관테스트'를 찾을 수 없습니다!
```

---

## 🛠️ 해결 방법

### 방법 1: Revit에서 프로젝트 생성 (권장)

1. Revit 2025 실행
2. `배관테스트.rvt` 파일 열기
3. DXrevit 플러그인 → "프로젝트 정보 추출 & 업로드" 실행
4. 로그에서 성공 메시지 확인
5. 다시 Navisworks로 돌아가서 "프로젝트 감지" 실행

### 방법 2: API로 수동 생성 (테스트용)

```bash
cd "c:\Users\Yoon taegwan\Desktop\AWP_2025\개발폴더\scripts"
python create_project_manually.py
```

---

## 📊 프로젝트 코드 생성 규칙

Revit과 Navisworks 모두 동일한 규칙을 사용합니다:

### 입력 예시
- Revit 파일: `배관테스트.rvt`
- CSV 소스 파일: `DisplayString:C:\...\배관테스트.rvt`

### 변환 과정
1. 파일명 추출: `배관테스트.rvt` → `배관테스트`
2. 대문자 변환: `배관테스트` → `배관테스트` (한글은 그대로)
3. 공백/하이픈을 언더스코어로: `My Project.rvt` → `MY_PROJECT`
4. 특수문자 제거: `Project#123.rvt` → `PROJECT123`
5. 최대 50자 제한

### 예시
| Revit 파일 | 프로젝트 코드 |
|-----------|------------|
| `배관테스트.rvt` | `배관테스트` |
| `Snowdon Towers.rvt` | `SNOWDON_TOWERS` |
| `My-Project 2024.rvt` | `MY_PROJECT_2024` |

---

## 🔧 데이터베이스 직접 확인

### PostgreSQL로 확인
```sql
-- 모든 프로젝트 조회
SELECT code, name, created_by, created_at
FROM projects
ORDER BY created_at DESC;

-- 특정 프로젝트 확인
SELECT * FROM projects WHERE code = '배관테스트';
```

### Python 스크립트로 확인
```bash
cd "c:\Users\Yoon taegwan\Desktop\AWP_2025\개발폴더\scripts"
python check_project.py
```

---

## 🚨 일반적인 문제 해결

### 문제 1: "프로젝트가 API에 등록되어 있지 않습니다"
**원인**: Revit에서 프로젝트를 생성하지 않음
**해결**: Revit에서 "프로젝트 정보 추출 & 업로드" 실행

### 문제 2: "프로젝트 코드를 감지할 수 없습니다"
**원인**: CSV 파일에 "소스 파일 이름" 속성이 없음
**해결**: Navisworks에서 속성 내보내기 설정 확인

### 문제 3: "중복 프로젝트 코드"
**원인**: 이미 동일한 코드가 존재
**해결**:
1. 기존 프로젝트 삭제 또는 비활성화
2. Revit 파일명 변경 후 재생성

### 문제 4: "API 서버에 연결할 수 없습니다"
**원인**: FastAPI 서버가 실행되지 않음
**해결**:
```bash
cd "c:\Users\Yoon taegwan\Desktop\AWP_2025\개발폴더\fastapi_server"
python -m uvicorn main:app --reload
```

---

## 📝 체크리스트

프로젝트 작업 시 다음 순서를 따르세요:

- [ ] 1. FastAPI 서버 실행 확인 (http://127.0.0.1:8000)
- [ ] 2. Revit에서 프로젝트 파일 열기
- [ ] 3. DXrevit 플러그인으로 프로젝트 생성
- [ ] 4. 데이터베이스에서 프로젝트 확인 (`python check_project.py`)
- [ ] 5. Navisworks에서 NWC 파일 열기
- [ ] 6. DXnavis 플러그인으로 프로젝트 감지
- [ ] 7. CSV 업로드 및 계층 정보 동기화

---

## 🎯 요약

**핵심 규칙**:
1. **항상 Revit을 먼저 사용하여 프로젝트를 생성합니다**
2. Navisworks는 기존 프로젝트에 연결만 합니다
3. 프로젝트 코드는 Revit 파일명에서 자동 생성됩니다
4. 두 시스템 모두 동일한 코드 생성 로직을 사용합니다

**현재 문제 해결**:
```
문제: "프로젝트 배관테스트가 API에 등록되어 있지 않습니다"
원인: Revit에서 프로젝트를 생성하지 않음
해결: Revit → DXrevit → "프로젝트 정보 추출 & 업로드" 실행
```

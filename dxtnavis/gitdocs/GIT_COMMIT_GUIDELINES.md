# Git 커밋 및 푸시 규칙

## 📌 목적
이 문서는 DXnavis 프로젝트의 Git 워크플로우 일관성을 유지하고, 명확한 커밋 히스토리를 관리하기 위한 가이드라인을 제공합니다.

---

## 🔖 커밋 메시지 규칙

### 기본 형식
```
<type>(<scope>): <subject>

<body>

<footer>
```

### Type (필수)
커밋의 목적을 나타내는 접두어입니다.

| Type | 설명 | 예시 |
|------|------|------|
| `feat` | 새로운 기능 추가 | `feat(ui): TreeView 계층 구조 탐색 추가` |
| `fix` | 버그 수정 | `fix(api): AccessViolationException 해결` |
| `docs` | 문서 변경 (코드 변경 없음) | `docs: README.md 업데이트` |
| `style` | 코드 포맷팅, 세미콜론 누락 등 (기능 변경 없음) | `style: 들여쓰기 통일` |
| `refactor` | 코드 리팩토링 (기능 변경 없음) | `refactor(service): NavisworksDataExtractor 최적화` |
| `perf` | 성능 개선 | `perf(viewmodel): Debouncing 패턴 적용` |
| `test` | 테스트 추가/수정 | `test: SetCreationService 단위 테스트 추가` |
| `build` | 빌드 시스템, 패키지 의존성 변경 | `build: Newtonsoft.Json 13.0.4 추가` |
| `ci` | CI/CD 설정 변경 | `ci: GitHub Actions 워크플로우 추가` |
| `chore` | 기타 변경사항 (코드 변경 없음) | `chore: .gitignore 업데이트` |
| `revert` | 이전 커밋 되돌리기 | `revert: "feat: TreeView 기능" 되돌리기` |

### Scope (선택)
변경 범위를 나타냅니다. 프로젝트의 주요 모듈명을 사용합니다.

- `ui`, `viewmodel`, `model`, `service`, `api`, `config`, `docs`

### Subject (필수)
변경 사항을 간결하게 설명합니다.

**규칙**:
- 50자 이내로 작성
- 첫 글자는 소문자
- 마침표(.) 없음
- 명령형 어조 사용 ("추가했음" ❌ → "추가" ✅)
- 한글 또는 영어 사용 가능

### Body (선택)
상세한 변경 이유와 내용을 설명합니다.

**규칙**:
- Subject와 한 줄 띄우기
- 72자마다 줄바꿈
- "무엇을" 보다 "왜"와 "어떻게"를 설명
- 여러 줄 작성 가능

### Footer (선택)
이슈 트래킹, 호환성 변경 등을 명시합니다.

**형식**:
- `Closes #123` - 이슈 닫기
- `Refs #456` - 이슈 참조
- `BREAKING CHANGE: ...` - 호환성 깨지는 변경

---

## 📋 커밋 메시지 예시

### 예시 1: 새 기능 추가
```
feat(ui): 계층 구조 TreeView 탐색 기능 추가

HierarchicalDataTemplate을 활용하여 모델의 전체 계층 구조를
TreeView로 시각화했습니다. 사용자가 트리 노드를 선택하면
해당 객체의 속성이 자동으로 로드됩니다.

- HierarchyNodeViewModel 추가
- TreeNodeModel 재귀 구조 구현
- XAML에 HierarchicalDataTemplate 적용

Closes #12
```

### 예시 2: 버그 수정
```
fix(service): UI 스레드 위반으로 인한 AccessViolationException 해결

NavisworksDataExtractor에서 Task.Run 내부에서
Navisworks API 객체에 접근하던 패턴을 제거했습니다.
모든 API 호출을 UI 스레드에서만 실행하도록 수정하여
예외 발생을 90% 감소시켰습니다.

Refs #ERROR7
```

### 예시 3: 문서 업데이트
```
docs: v2 README.md 업데이트

PRD v3-v7 구현 완료에 따른 README 업데이트
- 주요 기능 섹션에 TreeView 탐색 추가
- 버전 히스토리 v2 작성
- 알려진 제한사항 업데이트
```

### 예시 4: 리팩토링
```
refactor(viewmodel): DXwindowViewModel 코드 정리

- 중복 코드 메서드로 추출
- 매직 넘버를 상수로 변경
- 불필요한 주석 제거

기능 변경 없음.
```

---

## 🌿 브랜치 전략

### 브랜치 명명 규칙

| 브랜치 타입 | 접두어 | 예시 |
|------------|--------|------|
| 메인 브랜치 | `main` | `main` |
| 개발 브랜치 | `develop` | `develop` |
| 기능 개발 | `feature/` | `feature/treeview-navigation` |
| 버그 수정 | `fix/` | `fix/access-violation` |
| 핫픽스 | `hotfix/` | `hotfix/crash-on-startup` |
| 릴리즈 | `release/` | `release/v2.0` |
| 실험 | `experiment/` | `experiment/virtualization` |

### 브랜치 워크플로우

```
main (프로덕션)
 ├─ develop (개발)
 │   ├─ feature/treeview-navigation
 │   ├─ feature/search-set-creation
 │   └─ fix/access-violation
 └─ hotfix/critical-crash (긴급 수정)
```

**규칙**:
1. `main`: 안정적인 프로덕션 코드만 포함
2. `develop`: 다음 릴리즈 준비 브랜치
3. `feature/*`: 새 기능 개발 (develop에서 분기)
4. `fix/*`: 버그 수정 (develop에서 분기)
5. `hotfix/*`: 긴급 수정 (main에서 직접 분기)

---

## 🚀 푸시 규칙

### 푸시 전 체크리스트

- [ ] 빌드 성공 확인
- [ ] 컴파일 경고 없음 확인
- [ ] 코드 포맷팅 정리
- [ ] 불필요한 파일 제외 (.vs, bin, obj, .user 파일)
- [ ] 커밋 메시지 규칙 준수 확인

### 푸시 주기

**개발 중**:
- 의미 있는 단위로 작은 커밋 작성
- 하루 종료 시 반드시 푸시
- 기능 완성 시 즉시 푸시

**금지 사항**:
- ❌ 여러 기능을 하나의 커밋에 포함
- ❌ "작업 중", "임시 저장" 등의 모호한 메시지
- ❌ 빌드 실패 상태로 푸시
- ❌ 개인 설정 파일 (*.user, .vs/) 푸시

---

## 📦 커밋 단위 원칙

### 원자적 커밋 (Atomic Commit)
하나의 커밋은 하나의 논리적 변경만 포함해야 합니다.

**좋은 예**:
```
feat(model): TreeNodeModel 추가
feat(viewmodel): HierarchyNodeViewModel 추가
feat(ui): TreeView XAML 바인딩 구현
```

**나쁜 예**:
```
feat: TreeView 기능 전부 추가 (Model, ViewModel, View 모두 포함)
```

### 커밋 크기 가이드

| 크기 | 설명 | 예시 |
|------|------|------|
| 🟢 작음 | 1-3개 파일, 50줄 이하 | 버그 수정, 주석 추가 |
| 🟡 중간 | 3-7개 파일, 50-200줄 | 새 기능 1개 추가 |
| 🔴 큼 | 7개+ 파일, 200줄+ | 리팩토링, 아키텍처 변경 |

**원칙**: 큰 변경은 여러 개의 작은 커밋으로 분할하세요.

---

## 🛡️ 코드 리뷰 및 머지 규칙

### Pull Request 작성

**제목 형식**:
```
[Type] 간단한 설명
```

**예시**:
```
[Feature] TreeView 계층 구조 탐색 기능 추가
[Fix] AccessViolationException 해결
[Docs] v2 README 업데이트
```

**설명 템플릿**:
```markdown
## 변경 사항
- 기능/버그 설명

## 테스트 방법
1. 단계별 테스트 방법
2. 예상 결과

## 스크린샷 (UI 변경 시)
![screenshot](url)

## 체크리스트
- [ ] 빌드 성공
- [ ] 자가 테스트 완료
- [ ] 문서 업데이트
```

### 머지 규칙

1. **Squash Merge**: 여러 커밋을 하나로 합치기 (기능 브랜치)
2. **Merge Commit**: 커밋 히스토리 유지 (develop → main)
3. **Rebase**: 히스토리 정리 (개인 브랜치만)

---

## 🏷️ 태그 규칙

### 버전 태그
릴리즈 시 태그를 생성합니다.

**형식**: `v<major>.<minor>.<patch>`

**예시**:
```bash
git tag -a v2.0.0 -m "Release v2: TreeView 및 검색 세트 기능 추가"
git push origin v2.0.0
```

**버전 번호 규칙** (Semantic Versioning):
- `major`: 호환성 깨지는 변경
- `minor`: 새 기능 추가 (하위 호환)
- `patch`: 버그 수정

---

## 🔧 Git 설정

### 추천 Git 설정

```bash
# 사용자 정보 설정
git config --global user.name "Yoon taegwan"
git config --global user.email "your-email@example.com"

# 기본 에디터 설정 (VS Code)
git config --global core.editor "code --wait"

# 줄바꿈 자동 변환 (Windows)
git config --global core.autocrlf true

# 기본 브랜치명
git config --global init.defaultBranch main

# Alias 설정
git config --global alias.co checkout
git config --global alias.br branch
git config --global alias.ci commit
git config --global alias.st status
git config --global alias.lg "log --oneline --graph --all --decorate"
```

---

## 📚 참고 자료

- [Conventional Commits](https://www.conventionalcommits.org/)
- [Git Flow](https://nvie.com/posts/a-successful-git-branching-model/)
- [Semantic Versioning](https://semver.org/)

---

**마지막 업데이트**: 2025-01-13
**버전**: v1.0

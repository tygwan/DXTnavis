---
name: dev-doc-planner
description: 개발 문서 프롬프트 작성 도우미. PRD, 기술 설계서, 진행상황 체크 문서 작성 시 사용. "문서 작성", "PRD", "설계서", "진행상황", "개발 계획" 키워드에 반응.
---

# 개발 문서 플래너 (Dev Doc Planner)

개발 프로젝트의 문서화를 체계적으로 지원하는 스킬입니다.

## 문서 저장 규칙

- **위치**: `{project_root}/docs/`
- **형식**: Markdown (.md)
- **네이밍**: kebab-case (예: `user-authentication-prd.md`)

## 문서 유형별 가이드

### 1. PRD (Product Requirements Document)

**용도**: 제품 요구사항 정의
**템플릿**: [PRD-TEMPLATE.md](dev-doc-planner/PRD-TEMPLATE.md)
**권장 길이**: 섹션당 1,500-2,000자

```
docs/
├── prd/
│   ├── {feature-name}-prd.md
│   └── ...
```

### 2. 기술 설계서 (Technical Specification)

**용도**: 구현 상세 설계
**템플릿**: [TECH-SPEC-TEMPLATE.md](dev-doc-planner/TECH-SPEC-TEMPLATE.md)
**권장 길이**: 섹션당 2,000-2,500자

```
docs/
├── tech-specs/
│   ├── {feature-name}-spec.md
│   └── ...
```

### 3. 진행상황 체크 (Progress Tracking)

**용도**: 개발 진척 관리
**템플릿**: [PROGRESS-TEMPLATE.md](dev-doc-planner/PROGRESS-TEMPLATE.md)
**권장 길이**: 800-1,200자

```
docs/
├── progress/
│   ├── {feature-name}-progress.md
│   └── ...
```

## 개발 생명주기 매핑

| Phase | 문서 | 서브에이전트 |
|-------|------|-------------|
| 기획 | PRD 작성 | prd-writer |
| 설계 | 기술 설계서 | tech-spec-writer |
| 개발 | 진행상황 체크 | progress-tracker |
| 검증 | 문서 완성도 | doc-validator |
| 테스트 | 테스트 케이스 | progress-tracker |
| 배포 | 배포 체크리스트 | doc-validator |

## 토큰 최적화 규칙

1. **섹션 분리**: 하나의 문서에 3,000자 이상 작성 금지
2. **참조 링크**: 상세 내용은 별도 파일로 분리 후 링크
3. **코드 블록**: 핵심 코드만 포함, 전체 코드는 파일 경로로 참조

## 빠른 시작

```bash
# PRD 작성 시작
"user-authentication 기능의 PRD를 작성해줘"

# 기술 설계서 작성
"user-authentication의 기술 설계서를 작성해줘"

# 진행상황 체크
"user-authentication 개발 진행상황을 업데이트해줘"

# 문서 검증
"user-authentication 문서들의 완성도를 검증해줘"
```

## 관련 서브에이전트

- **prd-writer**: PRD 문서 작성 전문
- **tech-spec-writer**: 기술 설계서 작성 전문
- **progress-tracker**: 진행상황 추적 및 업데이트
- **doc-validator**: 문서 완성도 검증

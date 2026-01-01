# tygwan/skills 저장소 전략

## 목표

다른 프로젝트에서 Claude Code 개발 도구(agents, skills, commands, hooks)를 쉽게 초기화할 수 있도록 tygwan/skills 저장소를 정리합니다.

## 현재 도구 분류

### 범용 도구 (모든 프로젝트에서 사용 가능)

**Agents:**
| Agent | 용도 |
|-------|------|
| work-unit-manager | 작업 단위 커밋 관리 |
| doc-splitter | 문서 분할 관리 |
| code-reviewer | 코드 리뷰 |
| commit-helper | 커밋 메시지 작성 |
| doc-validator | 문서 완성도 검증 |
| branch-manager | Git 브랜치 관리 |
| git-troubleshooter | Git 문제 해결 |
| pr-creator | PR 생성 |
| prd-writer | PRD 작성 |
| progress-tracker | 진행상황 추적 |
| tech-spec-writer | 기술 설계서 작성 |
| brand-logo-finder | 브랜드 로고 검색 |

**Skills:**
| Skill | 용도 |
|-------|------|
| context-optimizer | 컨텍스트/토큰 최적화 |
| brainstorming | 아이디어 브레인스토밍 |
| code-changelog | 코드 변경 로그 |
| consensus-engine | Multi-LLM 합의 엔진 |
| hook-creator | Hook 생성 가이드 |
| prompt-enhancer | 프롬프트 개선 |
| skill-creator | Skill 생성 가이드 |
| skill-manager | Skill 마켓플레이스 관리 |
| slash-command-creator | Slash 명령어 생성 |
| subagent-creator | Sub-agent 생성 |
| using-git-worktrees | Git Worktree 활용 |

**Commands:**
| Command | 용도 |
|---------|------|
| dev-doc-planner | 개발 문서 계획 |
| git-workflow | Git 워크플로우 |
| sc/atomic-commit | 작업 단위 커밋 |

**Hooks:**
| Hook | 용도 |
|------|------|
| pre-commit-check | 커밋 전 검사 |
| post-commit-update | 커밋 후 업데이트 |
| pr-doc-linker | PR-문서 연결 |

### 프로젝트 특화 도구

| 도구 | 프로젝트 |
|------|----------|
| dxtnavis-deployer | DXTnavis 빌드/배포 |
| codex | Codex CLI 전용 |
| codex-claude-loop | Codex-Claude 루프 |

## 저장소 구조 제안

```
tygwan/skills/
├── README.md                    # 저장소 설명 및 사용법
├── init.sh                      # Linux/Mac 초기화 스크립트
├── init.ps1                     # Windows 초기화 스크립트
├── init.bat                     # Windows 배치 초기화
│
├── core/                        # 핵심 범용 도구
│   ├── agents/
│   │   ├── work-unit-manager.md
│   │   ├── doc-splitter.md
│   │   ├── code-reviewer.md
│   │   ├── commit-helper.md
│   │   └── ...
│   ├── skills/
│   │   ├── context-optimizer/
│   │   ├── brainstorming/
│   │   └── ...
│   ├── commands/
│   │   ├── dev-doc-planner/
│   │   ├── git-workflow/
│   │   └── sc/
│   └── hooks/
│       ├── pre-commit-check.sh
│       └── ...
│
├── project-templates/           # 프로젝트 유형별 템플릿
│   ├── navisworks-plugin/       # Navisworks 플러그인 개발용
│   │   └── agents/
│   │       └── dxtnavis-deployer.md
│   ├── web-app/                 # 웹 앱 개발용
│   └── dotnet/                  # .NET 개발용
│
└── experimental/                # 실험적 도구
    └── codex/
```

## 초기화 스크립트

### init.ps1 (Windows PowerShell)
```powershell
# Usage: .\init.ps1 [target-path] [template]
# Example: .\init.ps1 C:\MyProject core
#          .\init.ps1 C:\MyProject navisworks-plugin

param(
    [string]$TargetPath = ".",
    [string]$Template = "core"
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ClaudePath = Join-Path $TargetPath ".claude"

# Create .claude directory structure
New-Item -ItemType Directory -Force -Path "$ClaudePath\agents"
New-Item -ItemType Directory -Force -Path "$ClaudePath\skills"
New-Item -ItemType Directory -Force -Path "$ClaudePath\commands"
New-Item -ItemType Directory -Force -Path "$ClaudePath\hooks"

# Copy core tools
Copy-Item -Recurse "$ScriptDir\core\*" $ClaudePath

# Copy template-specific tools if requested
if ($Template -ne "core") {
    $TemplatePath = "$ScriptDir\project-templates\$Template"
    if (Test-Path $TemplatePath) {
        Copy-Item -Recurse "$TemplatePath\*" $ClaudePath -Force
    }
}

Write-Host "Claude Code tools initialized at: $ClaudePath"
```

### init.sh (Linux/Mac)
```bash
#!/bin/bash
# Usage: ./init.sh [target-path] [template]
# Example: ./init.sh ~/MyProject core
#          ./init.sh ~/MyProject navisworks-plugin

TARGET_PATH="${1:-.}"
TEMPLATE="${2:-core}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
CLAUDE_PATH="$TARGET_PATH/.claude"

# Create directory structure
mkdir -p "$CLAUDE_PATH/agents" "$CLAUDE_PATH/skills" "$CLAUDE_PATH/commands" "$CLAUDE_PATH/hooks"

# Copy core tools
cp -r "$SCRIPT_DIR/core/"* "$CLAUDE_PATH/"

# Copy template-specific tools
if [ "$TEMPLATE" != "core" ] && [ -d "$SCRIPT_DIR/project-templates/$TEMPLATE" ]; then
    cp -r "$SCRIPT_DIR/project-templates/$TEMPLATE/"* "$CLAUDE_PATH/"
fi

echo "Claude Code tools initialized at: $CLAUDE_PATH"
```

## 사용 방법

### 1. 저장소 클론
```bash
git clone https://github.com/tygwan/skills.git ~/.claude-skills
```

### 2. 새 프로젝트에 도구 초기화

**Windows:**
```powershell
~/.claude-skills/init.ps1 C:\MyNewProject core
```

**Linux/Mac:**
```bash
~/.claude-skills/init.sh ~/my-new-project core
```

### 3. 프로젝트 템플릿 사용
```bash
# Navisworks 플러그인 개발 프로젝트
~/.claude-skills/init.sh ~/my-navis-plugin navisworks-plugin
```

## 다음 단계

1. [ ] tygwan/skills 저장소 구조 정리
2. [ ] core/ 폴더에 범용 도구 이동
3. [ ] 초기화 스크립트 작성 및 테스트
4. [ ] README.md 작성
5. [ ] project-templates/ 폴더 구성
6. [ ] 버전 관리 및 릴리스 태그 추가

## 유지보수

- 새 도구 추가 시 core/ 또는 project-templates/에 배치
- 범용성 기준: 3개 이상 프로젝트에서 사용 가능하면 core/
- 프로젝트 특화: 특정 기술/프레임워크 전용이면 project-templates/

---

*작성일: 2026-01-02*

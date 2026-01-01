---
allowed-tools: [Bash, Read, Grep, Glob, TodoWrite]
description: "Quick atomic commit with auto-detected type and scope"
---

# /sc:atomic-commit - Atomic Work Unit Commit

## Purpose
Create atomic commits with automatically detected type and scope based on staged changes.

## Usage
```
/sc:atomic-commit [description]
/sc:atomic-commit "add login validation"
/sc:atomic-commit --dry-run
```

## Arguments
- `description` - Brief description of the change (optional, auto-generated if omitted)
- `--dry-run` - Show commit message without executing
- `--all` - Stage all changes before commit
- `--scope <name>` - Override auto-detected scope

## Workflow

### 1. Analyze Changes
```bash
git diff --cached --stat
git diff --cached --name-only
```

### 2. Auto-Detect Type
| Changed Files Pattern | Detected Type |
|----------------------|---------------|
| New feature files | `feat` |
| Bug fix in existing | `fix` |
| .md files only | `docs` |
| .csproj, config | `chore` |
| Test files | `test` |
| Refactoring | `refactor` |
| Performance | `perf` |

### 3. Auto-Detect Scope
Extract from file paths:
- `src/auth/*` → `auth`
- `api/users/*` → `users`
- `ViewModels/*` → `viewmodel`
- `Services/*` → `service`

### 4. Generate & Execute
```bash
git commit -m "<type>(<scope>): <description>"
```

## Output Format
```
Atomic Commit Analysis
=====================
Type: feat
Scope: auth
Files: 3 staged

Message: feat(auth): add login validation

Committed: abc1234
```

## Quick Examples
```bash
# Auto everything
/sc:atomic-commit

# With description
/sc:atomic-commit "implement password reset"

# Preview only
/sc:atomic-commit --dry-run

# Stage all and commit
/sc:atomic-commit --all "fix typo in readme"
```

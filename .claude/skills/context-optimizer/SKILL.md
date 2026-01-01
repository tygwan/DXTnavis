---
name: context-optimizer
description: Optimize context loading for efficient token usage. Use when working with large codebases, context limits, or when the user mentions "context", "token", "optimize", "summarize", or asks to reduce context size.
---

# Context Optimizer

Optimize AI context loading for efficient token usage and focused development sessions.

## When to Use This Skill

Use this skill when:
- Working with large codebases (>50 files)
- Context window is approaching limits
- User mentions "context", "token", "optimize"
- Need to focus on specific subsystem
- Starting a new development session

## Core Workflow

### Step 1: Analyze Current Context

**Identify loaded files:**
- Recently read files
- Referenced documentation
- Active working files

**Assess relevance:**
- Current task requirements
- File dependencies
- Historical usage patterns

### Step 2: Context Scoring

Score files by relevance (1-10):

| Factor | Weight | Description |
|--------|--------|-------------|
| Direct relevance | 40% | Directly related to current task |
| Dependency chain | 25% | Required by relevant files |
| Recent access | 20% | Recently read or modified |
| Reference frequency | 15% | Often referenced in codebase |

### Step 3: Optimization Strategies

#### Strategy A: Essential Only
```
Load only:
- Files directly being modified
- Critical type definitions
- Immediate dependencies
Token savings: 60-80%
```

#### Strategy B: Focused Context
```
Load:
- Working files + 1 level dependencies
- Relevant documentation
- Key configuration
Token savings: 40-60%
```

#### Strategy C: Summarized Context
```
Load:
- Full working files
- Summaries of related files
- Index of available resources
Token savings: 30-50%
```

## Context Summary Format

### File Summary Template
```markdown
## [filename] Summary
**Purpose:** [one-line description]
**Key exports:** [list of main functions/classes]
**Dependencies:** [key imports]
**Size:** [lines] lines

### Key Sections
- [Section 1]: Lines X-Y - [description]
- [Section 2]: Lines X-Y - [description]
```

### Project Context Template
```markdown
# Project Context Summary

## Architecture
- Pattern: [MVVM/MVC/etc]
- Language: [language + version]
- Framework: [framework details]

## Key Files
| File | Purpose | Priority |
|------|---------|----------|
| file1.cs | Main entry | High |
| file2.cs | Core logic | High |
| file3.cs | Utilities | Medium |

## Current Focus
Working on: [current task]
Relevant files: [list]
```

## Output Format

### Context Analysis Report
```markdown
## Context Optimization Analysis

### Current Context
- Files loaded: 25
- Estimated tokens: ~45,000
- Utilization: 75%

### Recommended Optimization

**Strategy:** Focused Context
**Expected savings:** 40%

#### Keep (High Priority)
- ViewModel.cs - direct modification
- Model.cs - type definitions
- Services/*.cs - active dependencies

#### Summarize (Medium Priority)
- Utils/*.cs - create summaries
- Helpers/*.cs - create summaries

#### Defer (Low Priority)
- Tests/*.cs - load on demand
- Docs/*.md - reference only

### Action
Apply optimization? [Yes/No]
```

## Best Practices

1. **Start Lean**: Load minimum required context
2. **Expand as Needed**: Add files when referenced
3. **Summarize Utilities**: Keep only interfaces for helpers
4. **Cache Summaries**: Reuse context summaries across sessions
5. **Document Dependencies**: Track what requires what

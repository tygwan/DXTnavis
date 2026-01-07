# DX Platform - Git Repository Initialization Script
# 이 스크립트는 Git 저장소를 초기화하고 GitHub에 푸시합니다.

param(
    [Parameter(Mandatory=$false)]
    [string]$GitHubRepoUrl = "",

    [Parameter(Mandatory=$false)]
    [switch]$SkipPush
)

Write-Host "=== DX Platform Git 저장소 초기화 ===" -ForegroundColor Cyan

# 1. Git 설치 확인
Write-Host "`n[1/8] Git 설치 확인..." -ForegroundColor Yellow
$gitVersion = git --version 2>$null
if (-not $gitVersion) {
    Write-Host "❌ Git이 설치되어 있지 않습니다." -ForegroundColor Red
    Write-Host "   https://git-scm.com/downloads 에서 다운로드하세요." -ForegroundColor Red
    exit 1
}
Write-Host "✅ Git 설치 확인: $gitVersion" -ForegroundColor Green

# 2. Git 저장소 초기화
Write-Host "`n[2/8] Git 저장소 초기화..." -ForegroundColor Yellow
$rootPath = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $rootPath

if (Test-Path ".git") {
    Write-Host "⚠️  Git 저장소가 이미 존재합니다." -ForegroundColor Yellow
    $response = Read-Host "기존 저장소를 삭제하고 다시 초기화하시겠습니까? (y/N)"
    if ($response -eq "y") {
        Remove-Item ".git" -Recurse -Force
        Write-Host "✅ 기존 저장소 삭제 완료" -ForegroundColor Green
    } else {
        Write-Host "❌ 초기화 취소" -ForegroundColor Red
        exit 1
    }
}

git init
Write-Host "✅ Git 저장소 초기화 완료" -ForegroundColor Green

# 3. .gitignore 확인
Write-Host "`n[3/8] .gitignore 확인..." -ForegroundColor Yellow
if (-not (Test-Path ".gitignore")) {
    Write-Host "❌ .gitignore 파일이 없습니다." -ForegroundColor Red
    exit 1
}
Write-Host "✅ .gitignore 파일 존재" -ForegroundColor Green

# 4. README.md 확인
Write-Host "`n[4/8] README.md 확인..." -ForegroundColor Yellow
if (-not (Test-Path "README.md")) {
    Write-Host "❌ README.md 파일이 없습니다." -ForegroundColor Red
    exit 1
}
Write-Host "✅ README.md 파일 존재" -ForegroundColor Green

# 5. 파일 스테이징
Write-Host "`n[5/8] 파일 스테이징..." -ForegroundColor Yellow
git add .
$stagedFiles = git diff --cached --name-only
$fileCount = ($stagedFiles | Measure-Object).Count
Write-Host "✅ $fileCount 개 파일 스테이징 완료" -ForegroundColor Green

# 6. 초기 커밋
Write-Host "`n[6/8] 초기 커밋 생성..." -ForegroundColor Yellow
$commitMessage = @"
Initial commit: DX Platform v0.1.0

- Phase 1: DXBase (공용 라이브러리) 완료
- Phase 2: DXrevit (Revit 애드인) 완료
- 통합 솔루션 구조 설정
- .gitignore 및 README.md 추가

Components:
- DXBase: .NET Standard 2.0 / .NET 8.0
- DXrevit: .NET 8.0-windows (Revit 2025)
"@

git commit -m $commitMessage
Write-Host "✅ 초기 커밋 완료" -ForegroundColor Green

# 7. 기본 브랜치명 변경 (main)
Write-Host "`n[7/8] 기본 브랜치명 변경 (master → main)..." -ForegroundColor Yellow
git branch -M main
Write-Host "✅ 브랜치명 변경 완료" -ForegroundColor Green

# 8. GitHub 원격 저장소 설정 및 푸시
if (-not $SkipPush) {
    Write-Host "`n[8/8] GitHub 원격 저장소 설정..." -ForegroundColor Yellow

    if ([string]::IsNullOrEmpty($GitHubRepoUrl)) {
        Write-Host "`n📝 GitHub 저장소 생성 방법:" -ForegroundColor Cyan
        Write-Host "   1. https://github.com/new 접속" -ForegroundColor White
        Write-Host "   2. Repository name: dx-platform" -ForegroundColor White
        Write-Host "   3. Private/Public 선택" -ForegroundColor White
        Write-Host "   4. Create repository 클릭" -ForegroundColor White
        Write-Host "`n생성된 저장소 URL을 입력하세요 (예: https://github.com/username/dx-platform.git):"
        $GitHubRepoUrl = Read-Host
    }

    if (-not [string]::IsNullOrEmpty($GitHubRepoUrl)) {
        git remote add origin $GitHubRepoUrl
        Write-Host "✅ 원격 저장소 추가: $GitHubRepoUrl" -ForegroundColor Green

        Write-Host "`n📤 GitHub에 푸시 중..." -ForegroundColor Yellow
        git push -u origin main

        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ GitHub 푸시 완료!" -ForegroundColor Green
        } else {
            Write-Host "❌ 푸시 실패. 인증 정보를 확인하세요." -ForegroundColor Red
            Write-Host "   GitHub Personal Access Token이 필요할 수 있습니다." -ForegroundColor Yellow
            Write-Host "   https://github.com/settings/tokens" -ForegroundColor Yellow
        }
    } else {
        Write-Host "⚠️  원격 저장소 설정을 건너뜁니다." -ForegroundColor Yellow
        Write-Host "   나중에 다음 명령어로 추가하세요:" -ForegroundColor Yellow
        Write-Host "   git remote add origin <your-repo-url>" -ForegroundColor White
        Write-Host "   git push -u origin main" -ForegroundColor White
    }
} else {
    Write-Host "`n[8/8] GitHub 푸시 건너뜀 (-SkipPush 옵션)" -ForegroundColor Yellow
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "🎉 Git 저장소 초기화 완료!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan

Write-Host "`n📋 다음 단계:" -ForegroundColor Cyan
Write-Host "   1. GitHub에서 저장소 확인" -ForegroundColor White
Write-Host "   2. 브랜치 전략 설정 (main, develop, feature/*)" -ForegroundColor White
Write-Host "   3. GitHub Actions CI/CD 설정" -ForegroundColor White
Write-Host "   4. 이슈 템플릿 추가" -ForegroundColor White

Write-Host "`n💡 유용한 Git 명령어:" -ForegroundColor Cyan
Write-Host "   git status              # 현재 상태 확인" -ForegroundColor White
Write-Host "   git add .               # 모든 변경사항 스테이징" -ForegroundColor White
Write-Host "   git commit -m 'msg'     # 커밋" -ForegroundColor White
Write-Host "   git push                # 원격 저장소에 푸시" -ForegroundColor White
Write-Host "   git pull                # 원격 저장소에서 풀" -ForegroundColor White
Write-Host "   git log --oneline       # 커밋 이력 확인" -ForegroundColor White

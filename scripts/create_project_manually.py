"""
프로젝트 수동 생성 스크립트 (테스트용)
- Revit 없이 프로젝트를 직접 생성할 때 사용
- 실제 운영 환경에서는 Revit 플러그인을 사용하세요
"""
import asyncpg
import asyncio
from datetime import datetime


def generate_project_code(project_name: str) -> str:
    """
    프로젝트 이름에서 코드 생성
    (projects.py의 로직과 동일)
    """
    import re

    code = project_name.replace(" ", "_").replace("-", "_").upper()
    code = re.sub(r'[^A-Z0-9_가-힣]', '', code)

    if len(code) > 50:
        code = code[:50]

    return code if code else "UNKNOWN_PROJECT"


async def create_project(
    project_name: str,
    revit_file_name: str,
    revit_file_path: str,
    created_by: str = "manual_script"
):
    """프로젝트 생성"""

    # 프로젝트 코드 생성
    project_code = generate_project_code(project_name)

    print(f"📝 프로젝트 생성 중...")
    print(f"   이름: {project_name}")
    print(f"   코드: {project_code}")
    print(f"   Revit 파일: {revit_file_name}")
    print(f"   경로: {revit_file_path}")
    print(f"   생성자: {created_by}")
    print()

    # 데이터베이스 연결
    conn = await asyncpg.connect(
        host='localhost',
        port=5432,
        user='postgres',
        password='123456',
        database='DX_platform'
    )

    try:
        # 중복 확인
        existing = await conn.fetchval(
            "SELECT id FROM projects WHERE code = $1",
            project_code
        )

        if existing:
            print(f"⚠️  프로젝트 코드 '{project_code}'가 이미 존재합니다!")
            print(f"   기존 프로젝트를 삭제하거나 다른 이름을 사용하세요.")
            return False

        # 프로젝트 생성
        project_id = await conn.fetchval("""
            INSERT INTO projects (
                code, name, revit_file_name, revit_file_path, created_by
            ) VALUES ($1, $2, $3, $4, $5)
            RETURNING id
        """,
            project_code, project_name, revit_file_name, revit_file_path, created_by
        )

        print(f"✅ 프로젝트 생성 완료!")
        print(f"   ID: {project_id}")
        print(f"   코드: {project_code}")
        print()

        # 생성된 프로젝트 확인
        project = await conn.fetchrow(
            "SELECT * FROM projects WHERE id = $1",
            project_id
        )

        print("📋 생성된 프로젝트 정보:")
        print(f"   코드: {project['code']}")
        print(f"   이름: {project['name']}")
        print(f"   Revit 파일: {project['revit_file_name']}")
        print(f"   생성자: {project['created_by']}")
        print(f"   생성일: {project['created_at']}")
        print(f"   활성 상태: {project['is_active']}")

        return True

    except Exception as e:
        print(f"❌ 오류 발생: {e}")
        return False

    finally:
        await conn.close()


async def list_all_projects():
    """모든 프로젝트 목록 조회"""

    conn = await asyncpg.connect(
        host='localhost',
        port=5432,
        user='postgres',
        password='123456',
        database='DX_platform'
    )

    try:
        projects = await conn.fetch("""
            SELECT code, name, created_by, created_at, is_active
            FROM projects
            ORDER BY created_at DESC
        """)

        print("=" * 80)
        print("📋 전체 프로젝트 목록:")
        print("=" * 80)

        if not projects:
            print("⚠️  데이터베이스에 프로젝트가 없습니다!")
        else:
            for i, proj in enumerate(projects, 1):
                status = "✅ 활성" if proj['is_active'] else "❌ 비활성"
                print(f"{i}. [{status}] {proj['code']} - {proj['name']}")
                print(f"   생성자: {proj['created_by']}, 생성일: {proj['created_at']}")
                print()

    finally:
        await conn.close()


async def main():
    """메인 함수"""
    print("\n" + "=" * 80)
    print("🔧 AWP 프로젝트 수동 생성 도구")
    print("=" * 80 + "\n")

    print("⚠️  주의: 이 스크립트는 테스트용입니다!")
    print("   실제 운영 환경에서는 Revit 플러그인을 사용하세요.\n")

    # 배관테스트 프로젝트 생성
    success = await create_project(
        project_name="배관테스트",
        revit_file_name="배관테스트.rvt",
        revit_file_path=r"C:\Users\Yoon taegwan\Desktop\AWP_2025\250729테스트\배관테스트.rvt",
        created_by="manual_script"
    )

    if success:
        print("\n" + "=" * 80)
        print("✅ 프로젝트가 성공적으로 생성되었습니다!")
        print("=" * 80 + "\n")

        # 전체 프로젝트 목록 표시
        await list_all_projects()

        print("\n💡 다음 단계:")
        print("   1. Navisworks Manage 2025 실행")
        print("   2. DXnavis 플러그인 열기")
        print("   3. '프로젝트 감지' 버튼 클릭")
        print("   4. CSV 파일 선택")
        print("   5. ✅ '프로젝트 배관테스트를 찾았습니다' 메시지 확인\n")


if __name__ == "__main__":
    asyncio.run(main())

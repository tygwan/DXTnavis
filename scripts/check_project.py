"""
프로젝트 존재 여부 확인 스크립트
"""
import asyncpg
import asyncio


async def check_project(project_code: str):
    """데이터베이스에서 프로젝트 확인"""

    # 데이터베이스 연결
    conn = await asyncpg.connect(
        host='localhost',
        port=5432,
        user='postgres',
        password='123456',
        database='DX_platform'
    )

    try:
        # 모든 프로젝트 조회
        print("=" * 80)
        print("📋 전체 프로젝트 목록:")
        print("=" * 80)
        all_projects = await conn.fetch("""
            SELECT code, name, created_by, created_at, is_active
            FROM projects
            ORDER BY created_at DESC
        """)

        if not all_projects:
            print("⚠️  데이터베이스에 프로젝트가 없습니다!")
        else:
            for i, proj in enumerate(all_projects, 1):
                status = "✅ 활성" if proj['is_active'] else "❌ 비활성"
                print(f"{i}. [{status}] {proj['code']} - {proj['name']}")
                print(f"   생성자: {proj['created_by']}, 생성일: {proj['created_at']}")
                print()

        # 특정 프로젝트 조회
        print("=" * 80)
        print(f"🔍 프로젝트 '{project_code}' 검색 중...")
        print("=" * 80)

        project = await conn.fetchrow("""
            SELECT * FROM projects WHERE code = $1
        """, project_code)

        if project:
            print(f"✅ 프로젝트를 찾았습니다!")
            print(f"   ID: {project['id']}")
            print(f"   코드: {project['code']}")
            print(f"   이름: {project['name']}")
            print(f"   Revit 파일: {project['revit_file_name']}")
            print(f"   생성자: {project['created_by']}")
            print(f"   생성일: {project['created_at']}")
            print(f"   활성 상태: {project['is_active']}")
        else:
            print(f"❌ 프로젝트 '{project_code}'를 찾을 수 없습니다!")

            # 유사한 프로젝트 찾기
            similar = await conn.fetch("""
                SELECT code, name
                FROM projects
                WHERE code LIKE $1 OR name LIKE $1
                LIMIT 5
            """, f"%{project_code}%")

            if similar:
                print("\n💡 유사한 프로젝트:")
                for proj in similar:
                    print(f"   - {proj['code']} ({proj['name']})")

    finally:
        await conn.close()


async def main():
    """메인 함수"""
    print("\n" + "=" * 80)
    print("🔍 AWP 프로젝트 확인 도구")
    print("=" * 80 + "\n")

    # 배관테스트 프로젝트 확인
    await check_project("배관테스트")

    print("\n" + "=" * 80)
    print("✅ 확인 완료")
    print("=" * 80 + "\n")


if __name__ == "__main__":
    asyncio.run(main())

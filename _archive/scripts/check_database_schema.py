"""
데이터베이스 스키마 확인 스크립트
"""
import asyncpg
import asyncio


async def check_schema():
    """데이터베이스 스키마 확인"""

    conn = await asyncpg.connect(
        host='localhost',
        port=5432,
        user='postgres',
        password='123456',
        database='DX_platform'
    )

    try:
        print("=" * 80)
        print("📊 데이터베이스 테이블 목록")
        print("=" * 80)

        tables = await conn.fetch("""
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
            AND table_type = 'BASE TABLE'
            ORDER BY table_name
        """)

        for i, table in enumerate(tables, 1):
            print(f"{i}. {table['table_name']}")

        print()
        print("=" * 80)
        print("🔍 Revit 데이터 관련 테이블 상세")
        print("=" * 80)

        # projects 테이블
        if any(t['table_name'] == 'projects' for t in tables):
            print("\n📋 projects 테이블 컬럼:")
            columns = await conn.fetch("""
                SELECT column_name, data_type, is_nullable
                FROM information_schema.columns
                WHERE table_name = 'projects'
                ORDER BY ordinal_position
            """)
            for col in columns:
                nullable = "NULL" if col['is_nullable'] == 'YES' else "NOT NULL"
                print(f"   - {col['column_name']}: {col['data_type']} ({nullable})")

        # unified_objects 테이블
        if any(t['table_name'] == 'unified_objects' for t in tables):
            print("\n📦 unified_objects 테이블 컬럼:")
            columns = await conn.fetch("""
                SELECT column_name, data_type, is_nullable
                FROM information_schema.columns
                WHERE table_name = 'unified_objects'
                ORDER BY ordinal_position
            """)
            for col in columns:
                nullable = "NULL" if col['is_nullable'] == 'YES' else "NOT NULL"
                print(f"   - {col['column_name']}: {col['data_type']} ({nullable})")

        # revisions 테이블
        if any(t['table_name'] == 'revisions' for t in tables):
            print("\n🔄 revisions 테이블 컬럼:")
            columns = await conn.fetch("""
                SELECT column_name, data_type, is_nullable
                FROM information_schema.columns
                WHERE table_name = 'revisions'
                ORDER BY ordinal_position
            """)
            for col in columns:
                nullable = "NULL" if col['is_nullable'] == 'YES' else "NOT NULL"
                print(f"   - {col['column_name']}: {col['data_type']} ({nullable})")

        # navisworks_hierarchy 테이블
        if any(t['table_name'] == 'navisworks_hierarchy' for t in tables):
            print("\n🏗️ navisworks_hierarchy 테이블 컬럼:")
            columns = await conn.fetch("""
                SELECT column_name, data_type, is_nullable
                FROM information_schema.columns
                WHERE table_name = 'navisworks_hierarchy'
                ORDER BY ordinal_position
            """)
            for col in columns:
                nullable = "NULL" if col['is_nullable'] == 'YES' else "NOT NULL"
                print(f"   - {col['column_name']}: {col['data_type']} ({nullable})")

        print()
        print("=" * 80)
        print("📊 데이터 통계")
        print("=" * 80)

        # 프로젝트 수
        if any(t['table_name'] == 'projects' for t in tables):
            count = await conn.fetchval("SELECT COUNT(*) FROM projects")
            print(f"   프로젝트 수: {count}")

        # Revision 수
        if any(t['table_name'] == 'revisions' for t in tables):
            count = await conn.fetchval("SELECT COUNT(*) FROM revisions")
            print(f"   Revision 수: {count}")

        # Unified objects 수
        if any(t['table_name'] == 'unified_objects' for t in tables):
            count = await conn.fetchval("SELECT COUNT(*) FROM unified_objects")
            print(f"   Unified Objects 수: {count}")

        # Navisworks hierarchy 수
        if any(t['table_name'] == 'navisworks_hierarchy' for t in tables):
            count = await conn.fetchval("SELECT COUNT(*) FROM navisworks_hierarchy")
            print(f"   Navisworks Hierarchy 레코드 수: {count}")

    finally:
        await conn.close()


async def main():
    await check_schema()


if __name__ == "__main__":
    asyncio.run(main())
